using Arco.Api.Features.Achievements;

namespace Arco.Api.Features.Demo;

/// <summary>Fills a fresh demo account with believable data. <c>CurrentUser</c> must already be set.</summary>
public static class DemoSeeder
{
    private static readonly string[] NotesDone =
        ["Foi tranquilo hoje.", "Rendeu bem de manhã.", "Fiz com vontade.", "Ritmo bom, sem pressa."];
    private static readonly string[] NotesMinimum =
        ["Dia corrido, fiz o mínimo.", "Pouca energia hoje.", "Reunião longa me cansou."];
    private static readonly string[] NotesMissed =
        ["Dormi mal e não rendeu.", "Imprevisto no trabalho.", "Esqueci no meio da correria."];

    public static async Task SeedAsync(AppDbContext db, DateOnly today, CancellationToken ct)
    {
        var rng = new Random(42);
        DateTime At(DateOnly d) => d.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

        if (!await db.Settings.AnyAsync(ct)) db.Settings.Add(new Arco.Api.Domain.Settings());

        var start = today.AddDays(-21);
        var cycle = new Cycle
        {
            Name = "Ciclo de demonstração",
            StartDate = start,
            EndDate = start.AddDays(6 * 7 - 1),
            Status = CycleStatus.Active,
            CreatedAt = At(start),
            WeeklyFocusMinutes = 300,
        };
        var numeric = new Goal
        {
            Title = "Ler 6 livros",
            Type = GoalType.Numeric,
            Unit = "livros",
            StartValue = 0,
            TargetValue = 6,
            CurrentValue = 2,
            Deadline = cycle.EndDate,
            CreatedAt = At(start),
        };
        var milestones = new Goal
        {
            Title = "Lançar meu site pessoal",
            Type = GoalType.Milestones,
            Deadline = cycle.EndDate,
            CreatedAt = At(start),
            Milestones =
            [
                new Milestone { Title = "Escolher o domínio", Order = 1, DoneAt = At(start.AddDays(5)) },
                new Milestone { Title = "Escrever o conteúdo", Order = 2, DoneAt = At(start.AddDays(14)) },
                new Milestone { Title = "Publicar", Order = 3 },
            ],
        };
        numeric.Updates.Add(new GoalUpdate { Value = 1, Note = "Primeiro livro", CreatedAt = At(start.AddDays(9)) });
        numeric.Updates.Add(new GoalUpdate { Value = 2, CreatedAt = At(start.AddDays(18)) });
        cycle.Goals.Add(numeric);
        cycle.Goals.Add(milestones);
        db.Cycles.Add(cycle);

        var levelUpDate = start.AddDays(10);
        var habits = new[]
        {
            new Habit
            {
                Name = "Leitura", Emoji = "📚", Goal = numeric, FrequencyType = FrequencyType.Daily,
                CreatedAt = At(start),
                Levels =
                [
                    new HabitLevel { Level = 1, Description = "Ler 10 páginas", MinimumDescription = "Ler 1 página", ReachedAt = At(start) },
                    new HabitLevel { Level = 2, Description = "Ler 20 páginas", MinimumDescription = "Ler 5 páginas", ReachedAt = At(levelUpDate) },
                ],
                PlannedLevels =
                [
                    new HabitPlannedLevel { Position = 1, Description = "Ler 30 páginas", MinimumDescription = "Ler 10 páginas" },
                    new HabitPlannedLevel { Position = 2, Description = "Ler 40 páginas", MinimumDescription = "Ler 15 páginas" },
                ],
            },
            new Habit
            {
                Name = "Exercício", Emoji = "🏃", FrequencyType = FrequencyType.WeeklyTarget, TimesPerWeek = 3,
                LevelUpWeeks = 2, LevelUpDaysPerWeek = 3, CreatedAt = At(start),
                Levels = [new HabitLevel { Level = 1, Description = "Treinar 30 minutos", MinimumDescription = "Caminhar 10 minutos", ReachedAt = At(start) }],
            },
            new Habit
            {
                Name = "Meditação", Emoji = "🧘", FrequencyType = FrequencyType.SpecificDays, FrequencyDays = [1, 3, 5],
                CreatedAt = At(start),
                Levels = [new HabitLevel { Level = 1, Description = "Meditar 10 minutos", MinimumDescription = "Respirar fundo 5 vezes", ReachedAt = At(start) }],
            },
            new Habit
            {
                Name = "Escrever", Emoji = "✍️", Goal = milestones, FrequencyType = FrequencyType.Daily,
                CreatedAt = At(start),
                Levels = [new HabitLevel { Level = 1, Description = "Escrever 200 palavras", MinimumDescription = "Escrever uma frase", ReachedAt = At(start) }],
            },
        };
        // Ordem do dia: Meditação -> Leitura (manhã), Exercício (tarde), Escrever (noite, âncora de texto).
        habits[2].TimeOfDay = TimeOfDay.Morning; habits[2].SortOrder = 0;
        habits[0].TimeOfDay = TimeOfDay.Morning; habits[0].SortOrder = 1;
        habits[1].TimeOfDay = TimeOfDay.Afternoon; habits[1].SortOrder = 0;
        habits[3].TimeOfDay = TimeOfDay.Evening; habits[3].SortOrder = 0;
        habits[3].AnchorText = "jantar"; // the UI prefixes "Depois de": "Depois de jantar"
        db.Habits.AddRange(habits);
        await db.SaveChangesAsync(ct);
        habits[0].AnchorHabitId = habits[2].Id;
        await db.SaveChangesAsync(ct);

        var events = new List<XpEvent>();
        var checkIns = new List<CheckIn>();
        var noteRng = new Random(7);
        var dayRolls = new Dictionary<DateOnly, (double Sum, int Count)>();
        foreach (var habit in habits)
        {
            for (var d = start; d <= today; d = d.AddDays(1))
            {
                var scheduled = habit.FrequencyType switch
                {
                    FrequencyType.Daily => true,
                    FrequencyType.SpecificDays => habit.FrequencyDays.Contains((int)d.DayOfWeek),
                    _ => (d.DayNumber + habit.Id) % 2 == 0, // roughly 3-4 per week
                };
                if (!scheduled || (d == today && rng.NextDouble() < 0.5)) continue;

                var roll = rng.NextDouble();
                var status = roll < 0.55 ? CheckInStatus.Done : roll < 0.75 ? CheckInStatus.Minimum : CheckInStatus.Missed;
                var level = habit.Levels.Where(l => DateOnly.FromDateTime(l.ReachedAt) <= d).Max(l => l.Level);
                var xp = XpRules.ForCheckIn(status, level);
                var ci = new CheckIn { HabitId = habit.Id, Date = d, Status = status, Xp = xp, CreatedAt = At(d), UpdatedAt = At(d) };
                if (noteRng.NextDouble() < 0.18)
                {
                    var notes = status switch
                    {
                        CheckInStatus.Done => NotesDone,
                        CheckInStatus.Minimum => NotesMinimum,
                        _ => NotesMissed,
                    };
                    ci.Note = notes[noteRng.Next(notes.Length)];
                }
                dayRolls[d] = dayRolls.TryGetValue(d, out var acc) ? (acc.Sum + roll, acc.Count + 1) : (roll, 1);
                checkIns.Add(ci);
                if (xp > 0)
                    events.Add(new XpEvent
                    {
                        Date = d, Amount = xp, Source = XpSource.CheckIn, HabitId = habit.Id, CreatedAt = At(d),
                        Description = $"{habit.Name}: {(status == CheckInStatus.Done ? "feito" : "mínimo")}", CheckIn = ci,
                    });
            }
        }
        db.CheckIns.AddRange(checkIns);

        // Quantitative habit (own rng so the data above stays unchanged): daily totals drive the check-ins.
        var water = new Habit
        {
            Name = "Água", Emoji = "💧", FrequencyType = FrequencyType.Daily, Unit = "copos",
            TimeOfDay = TimeOfDay.Anytime, SortOrder = 0, CreatedAt = At(start),
            Levels = [new HabitLevel
            {
                Level = 1, Description = "8 copos", MinimumDescription = "4 copos",
                TargetTenths = 80, MinimumTenths = 40, ReachedAt = At(start),
            }],
        };
        db.Habits.Add(water);
        await db.SaveChangesAsync(ct);
        var waterRng = new Random(99);
        for (var d = start; d <= today; d = d.AddDays(1))
        {
            if (d == today && waterRng.NextDouble() < 0.5) continue;
            var total = waterRng.Next(0, 11) * 10; // 0..10 copos, in tenths
            if (total == 0) continue;
            db.HabitDayQuantities.Add(new HabitDayQuantity
            {
                HabitId = water.Id, Date = d, TotalTenths = total, UpdatedAt = At(d),
            });
            if (QuantityRules.StatusFor(total, 80, 40) is not { } ws) continue;
            var wxp = XpRules.ForCheckIn(ws, 1);
            var wci = new CheckIn { HabitId = water.Id, Date = d, Status = ws, Xp = wxp, CreatedAt = At(d), UpdatedAt = At(d) };
            db.CheckIns.Add(wci);
            if (wxp > 0)
                events.Add(new XpEvent
                {
                    Date = d, Amount = wxp, Source = XpSource.CheckIn, HabitId = water.Id, CreatedAt = At(d),
                    Description = $"{water.Name}: {(ws == CheckInStatus.Done ? "feito" : "mínimo")}", CheckIn = wci,
                });
        }

        // Negative habit (no rng, so the data above stays unchanged): clean every day up to yesterday,
        // one relapse mid-way, then 7 clean days reach the first milestone (level 2). Today stays pending.
        var relapseDate = start.AddDays(8);
        var soda = new Habit
        {
            Name = "Sem refrigerante", Emoji = "🥤", Kind = HabitKind.Negative, FrequencyType = FrequencyType.Daily,
            TimeOfDay = TimeOfDay.Anytime, SortOrder = 1, CreatedAt = At(start),
            Levels =
            [
                new HabitLevel { Level = 1, Description = "Sem recaída", MinimumDescription = "Sem recaída", ReachedAt = At(start) },
                new HabitLevel { Level = 2, Description = "7 dias limpo", MinimumDescription = "Sem recaída", ReachedAt = At(relapseDate.AddDays(7)) },
            ],
        };
        db.Habits.Add(soda);
        await db.SaveChangesAsync(ct);
        for (var d = start; d < today; d = d.AddDays(1))
        {
            var sstatus = d == relapseDate ? CheckInStatus.Missed : CheckInStatus.Done;
            var slevel = soda.Levels.Where(l => DateOnly.FromDateTime(l.ReachedAt) <= d).Max(l => l.Level);
            var sxp = XpRules.ForCheckIn(sstatus, slevel);
            var sci = new CheckIn { HabitId = soda.Id, Date = d, Status = sstatus, Xp = sxp, CreatedAt = At(d), UpdatedAt = At(d) };
            if (d == relapseDate) sci.Note = "Cedi numa festa.";
            db.CheckIns.Add(sci);
            if (sxp > 0)
                events.Add(new XpEvent
                {
                    Date = d, Amount = sxp, Source = XpSource.CheckIn, HabitId = soda.Id, CreatedAt = At(d),
                    Description = $"{soda.Name}: feito", CheckIn = sci,
                });
        }
        events.Add(new XpEvent
        {
            Date = relapseDate.AddDays(7), Amount = XpRules.HabitLevelUp, Source = XpSource.HabitLevelUp, HabitId = soda.Id,
            CreatedAt = At(relapseDate.AddDays(7)), Description = "Sem refrigerante: nível 2",
        });

        // Mood on most days, correlated with how the day went (lower roll = better day).
        var moodRng = new Random(11);
        foreach (var (d, (sum, count)) in dayRolls)
        {
            if (moodRng.NextDouble() < 0.2) continue;
            var avg = sum / count + (moodRng.NextDouble() - 0.5) * 0.3;
            var value = avg < 0.25 ? 5 : avg < 0.45 ? 4 : avg < 0.65 ? 3 : avg < 0.85 ? 2 : 1;
            db.DailyMoods.Add(new DailyMood { Date = d, Value = value, CreatedAt = At(d), UpdatedAt = At(d) });
        }

        events.Add(new XpEvent { Date = levelUpDate, Amount = XpRules.HabitLevelUp, Source = XpSource.HabitLevelUp, HabitId = habits[0].Id, CreatedAt = At(levelUpDate), Description = "Leitura: nível 2" });
        foreach (var m in milestones.Milestones.Where(m => m.DoneAt != null))
            events.Add(new XpEvent
            {
                Date = DateOnly.FromDateTime(m.DoneAt!.Value), Amount = XpRules.MilestoneDone, Source = XpSource.Milestone,
                GoalId = milestones.Id, MilestoneId = m.Id, CreatedAt = m.DoneAt.Value, Description = $"Marco: {m.Title}",
            });

        // Two weekly reviews for the last two full Monday-Sunday weeks.
        var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        foreach (var weekStart in new[] { thisMonday.AddDays(-14), thisMonday.AddDays(-7) })
        {
            var review = new WeeklyReview
            {
                WeekStart = weekStart, CreatedAt = At(weekStart.AddDays(6)),
                Wins = "Mantive a leitura quase todos os dias.",
                Obstacles = "Cansaço no meio da semana.",
                Next = "Dormir mais cedo para treinar de manhã.",
            };
            db.WeeklyReviews.Add(review);
            events.Add(new XpEvent
            {
                Date = weekStart.AddDays(6), Amount = XpRules.WeeklyReview, Source = XpSource.WeeklyReview,
                CreatedAt = review.CreatedAt, Description = "Revisão semanal", WeeklyReview = review,
            });
        }

        // A few focus sessions.
        for (var i = 0; i < 8; i++)
        {
            var d = today.AddDays(-rng.Next(0, 14));
            db.FocusSessions.Add(new FocusSession
            {
                HabitId = habits[i % 2 == 0 ? 0 : 3].Id, Date = d, Minutes = 15 + 15 * rng.Next(0, 4),
                Source = i % 3 == 0 ? FocusSource.Timer : FocusSource.Manual, CreatedAt = At(d),
            });
        }

        db.XpEvents.AddRange(events);
        await db.SaveChangesAsync(ct);

        await AchievementService.UnlockAsync(db, new AppClock(TimeProvider.System, db), ct);
    }
}
