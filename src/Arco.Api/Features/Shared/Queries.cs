namespace Arco.Api.Features.Shared;

/// <summary>Read helpers shared by several slices.</summary>
public static class Queries
{
    public static async Task<PlayerDto> GetPlayerAsync(AppDbContext db, CancellationToken ct) =>
        new(await db.XpEvents.SumAsync(e => e.Amount, ct));

    public static Task<Cycle?> GetActiveCycleAsync(AppDbContext db, CancellationToken ct) =>
        db.Cycles.AsNoTracking().FirstOrDefaultAsync(c => c.Status == CycleStatus.Active, ct);

    /// <summary>Maps goals (with Milestones loaded) to DTOs, filling habitIds (non-archived habits).</summary>
    public static async Task<List<GoalDto>> ToGoalDtosAsync(AppDbContext db, IReadOnlyCollection<Goal> goals, CancellationToken ct)
    {
        var ids = goals.Select(g => g.Id).ToList();
        var links = await db.Habits.AsNoTracking()
            .Where(h => h.GoalId != null && ids.Contains(h.GoalId.Value) && h.ArchivedAt == null)
            .OrderBy(h => h.Id)
            .Select(h => new { h.Id, GoalId = h.GoalId!.Value })
            .ToListAsync(ct);
        var byGoal = links.ToLookup(l => l.GoalId, l => l.Id);
        return goals.Select(g => GoalDto.From(g, byGoal[g.Id].ToList())).ToList();
    }

    public static async Task<GoalDto> ToGoalDtoAsync(AppDbContext db, Goal goal, CancellationToken ct) =>
        (await ToGoalDtosAsync(db, [goal], ct))[0];

    public static async Task<List<GoalDto>> GetGoalDtosForCycleAsync(AppDbContext db, int cycleId, CancellationToken ct)
    {
        var goals = await db.Goals.AsNoTracking().Include(g => g.Milestones)
            .Where(g => g.CycleId == cycleId)
            .OrderBy(g => g.Id)
            .ToListAsync(ct);
        return await ToGoalDtosAsync(db, goals, ct);
    }

    /// <summary>
    /// The excused days (paused or Skipped) of a habit. <paramref name="habit"/> needs Pauses loaded
    /// (without them only Skipped check-ins count).
    /// </summary>
    public static PauseCalendar Calendar(Habit habit, IEnumerable<CheckIn> checkIns) =>
        PauseCalendar.From(habit.Pauses.Select(p => (p.StartDate, p.EndDate)), checkIns.Select(c => (c.Date, c.Status)));

    /// <summary>
    /// Streak of a habit; <paramref name="checkIns"/> must be its whole history. Without a
    /// <paramref name="calendar"/>, it is built from the habit's loaded Pauses and the check-ins.
    /// </summary>
    public static StreakDto Streak(Habit habit, IReadOnlyCollection<CheckIn> checkIns, DateOnly today, string timezone,
        PauseCalendar? calendar = null)
    {
        var created = TimeZones.ToLocalDate(habit.CreatedAt, timezone);
        var start = checkIns.Count == 0 ? created : DateOnly.FromDayNumber(Math.Min(created.DayNumber, checkIns.Min(c => c.Date).DayNumber));
        return StreakDto.Of(StreakRules.Compute(habit.FrequencyType, habit.FrequencyDays, habit.TimesPerWeek,
            checkIns.Select(c => (c.Date, c.Status)), start, today, calendar ?? Calendar(habit, checkIns)));
    }

    /// <summary>
    /// Whether to suggest going back one level. <paramref name="checkIns"/> must cover the last
    /// <see cref="LevelDownSuggestion.Weeks"/> finished weeks.
    /// </summary>
    public static bool LevelDown(Habit habit, IEnumerable<CheckIn> checkIns, DateOnly today, string timezone,
        PauseCalendar? calendar = null)
    {
        var list = checkIns as IReadOnlyCollection<CheckIn> ?? checkIns.ToList();
        var reachedOn = habit.CurrentLevel is { } level ? TimeZones.ToLocalDate(level.ReachedAt, timezone) : today;
        var target = LevelDownSuggestion.WeeklyTarget(habit.FrequencyType, habit.FrequencyDays, habit.TimesPerWeek,
            habit.LevelUpDaysPerWeek);
        return LevelDownSuggestion.ShouldSuggest(list.Select(c => (c.Date, c.Status)), target,
            habit.CurrentLevelNumber, reachedOn, habit.LevelDownSnoozedUntil, today, habit.ArchivedAt is not null,
            calendar ?? Calendar(habit, list));
    }

    /// <summary>
    /// 14-day consistency of a habit (Levels loaded), the weeks counted by its weeks rule (null with the default
    /// rule) and whether to suggest leveling up. <paramref name="checkIns"/> must cover at least
    /// <see cref="LevelUpSuggestion.HistoryDays"/> days ending today.
    /// </summary>
    public static (ConsistencyDto Consistency, int? WeeksDone, bool Suggested) LevelUp(
        Habit habit, IEnumerable<CheckIn> checkIns, DateOnly today, string timezone, PauseCalendar? calendar = null)
    {
        var source = checkIns as IReadOnlyCollection<CheckIn> ?? checkIns.ToList();
        calendar ??= Calendar(habit, source);
        var list = source.Select(c => (c.Date, c.Status)).ToList();
        var consistency = LevelUpSuggestion.Measure(habit.FrequencyType, habit.FrequencyDays, habit.TimesPerWeek,
            list, today, calendar);
        var reachedOn = habit.CurrentLevel is { } level ? TimeZones.ToLocalDate(level.ReachedAt, timezone) : today;
        var archived = habit.ArchivedAt is not null;

        if (habit is { LevelUpWeeks: { } weeks, LevelUpDaysPerWeek: { } days })
        {
            var done = LevelUpSuggestion.QualifyingWeeks(list, days, reachedOn, today, calendar);
            return (ConsistencyDto.Of(consistency), done,
                LevelUpSuggestion.ShouldSuggestWeeks(done, weeks, habit.LevelUpSnoozedUntil, today, archived));
        }

        var suggested = LevelUpSuggestion.ShouldSuggest(consistency, reachedOn, habit.LevelUpSnoozedUntil, today, archived);
        return (ConsistencyDto.Of(consistency), null, suggested);
    }
}
