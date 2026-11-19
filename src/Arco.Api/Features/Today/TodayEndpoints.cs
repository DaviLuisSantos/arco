namespace Arco.Api.Features.Today;

public static class TodayEndpoints
{
    public sealed class TodayHabitDto(
        Habit habit,
        bool isDueToday,
        int weekCount,
        CheckInDto? checkIn,
        string? goalTitle,
        ConsistencyDto consistency,
        bool levelUpSuggested,
        int? levelUpWeeksDone,
        bool levelDownSuggested,
        StreakDto streak,
        DateOnly today,
        bool isPaused,
        DateOnly? pausedUntil,
        string? anchorName,
        bool anchorDone,
        decimal quantityToday = 0) : HabitDto(habit, today)
    {
        /// <summary>Running total of a quantitative habit today (0 without a record or for regular habits).</summary>
        public decimal QuantityToday { get; } = quantityToday;
        /// <summary>Name of the active anchor habit, or the free-text anchor; null when none (archived anchors are ignored).</summary>
        public string? AnchorName { get; } = anchorName;
        /// <summary>The anchor habit was done (Done/Minimum) today and this habit has no check-in yet: its turn.</summary>
        public bool AnchorDone { get; } = anchorDone;
        /// <summary>Paused today: listed apart ("Pausados"), never due.</summary>
        public bool IsPaused { get; } = isPaused;
        /// <summary>Last paused day of the pause in progress (null = until resumed, or not paused).</summary>
        public DateOnly? PausedUntil { get; } = pausedUntil;
        public bool IsDueToday { get; } = isDueToday;
        public int WeekCount { get; } = weekCount;
        public int? WeekTarget { get; } = habit.FrequencyType == FrequencyType.WeeklyTarget ? habit.TimesPerWeek : null;
        public CheckInDto? CheckIn { get; } = checkIn;
        public string? GoalTitle { get; } = goalTitle;
        public ConsistencyDto Consistency { get; } = consistency;
        public bool LevelUpSuggested { get; } = levelUpSuggested;
        public int? LevelUpWeeksDone { get; } = levelUpWeeksDone;
        public bool LevelDownSuggested { get; } = levelDownSuggested;
        public StreakDto Streak { get; } = streak;
    }

    /// <param name="Due">The reviewable week has check-ins and no review yet.</param>
    public sealed record WeeklyReviewStatusDto(DateOnly WeekStart, DateOnly WeekEnd, bool Due);

    public sealed record TodayResponse(
        DateOnly Date,
        TimeOfDay CurrentTimeOfDay,
        PlayerDto Player,
        int XpToday,
        CycleDto? Cycle,
        IReadOnlyList<GoalDto> Goals,
        IReadOnlyList<TodayHabitDto> Habits,
        WeeklyReviewStatusDto WeeklyReview);

    public static IEndpointRouteBuilder MapToday(this IEndpointRouteBuilder api)
    {
        api.MapGet("/today", GetToday).WithTags("Hoje").WithSummary("Visão do dia");
        return api;
    }

    private static async Task<TodayResponse> GetToday(AppDbContext db, AppClock clock, TimeProvider time, CancellationToken ct)
    {
        var today = await clock.GetTodayAsync(ct);
        var timezone = await clock.GetTimezoneAsync(ct);
        var currentPeriod = DayOrder.CurrentPeriod(TimeOnly.FromDateTime(TimeZones.LocalNow(time, timezone)));
        var player = await Queries.GetPlayerAsync(db, ct);
        var xpToday = await db.XpEvents.Where(e => e.Date == today).SumAsync(e => e.Amount, ct);

        var cycle = await Queries.GetActiveCycleAsync(db, ct);
        var goals = cycle is null ? [] : await Queries.GetGoalDtosForCycleAsync(db, cycle.Id, ct);

        var habits = await db.Habits.AsNoTracking()
            .Include(h => h.Levels)
            .Include(h => h.PlannedLevels)
            .Include(h => h.Goal)
            .Include(h => h.Pauses)
            .Where(h => h.ArchivedAt == null)
            .ToListAsync(ct);

        var weekStart = HabitSchedule.WeekStart(today);
        var weekEnd = HabitSchedule.WeekEnd(today);
        // The whole history feeds the streak and the level-up rules.
        var allCheckIns = (await db.CheckIns.AsNoTracking()
                .Where(c => c.Date <= weekEnd && c.Habit!.ArchivedAt == null)
                .ToListAsync(ct))
            .ToLookup(c => c.HabitId);

        var manualMinutes = (await db.FocusSessions.AsNoTracking()
                .Where(s => s.Date == today && s.Source == FocusSource.Manual)
                .OrderBy(s => s.Id).Select(s => new { s.HabitId, s.Minutes }).ToListAsync(ct))
            .GroupBy(s => s.HabitId).ToDictionary(g => g.Key, g => g.First().Minutes);

        var quantityToday = await db.HabitDayQuantities.AsNoTracking()
            .Where(q => q.Date == today).ToDictionaryAsync(q => q.HabitId, q => q.TotalTenths, ct);

        var habitsById = habits.ToDictionary(h => h.Id);
        var dayRank = DayOrder.Order(habits.Select(h => new DayOrder.Item(h.Id, h.Name, h.TimeOfDay, h.SortOrder, h.AnchorHabitId)))
            .Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);

        var items = habits.Select(h =>
        {
            var history = allCheckIns[h.Id].ToList();
            var week = history.Where(c => c.Date >= weekStart).ToList();
            var todayCheckIn = week.FirstOrDefault(c => c.Date == today);
            var weekCount = HabitSchedule.WeekCount(week.Select(c => (c.Date, c.Status)), today);
            var calendar = Queries.Calendar(h, history);
            var paused = calendar.IsPaused(today);
            // Weekly target: the goal shrinks with the excused days of the week.
            var target = h.TimesPerWeek is { } t ? HabitSchedule.AdjustedWeekTarget(t, calendar.ExcusedInWeek(weekStart)) : (int?)null;
            var due = !paused && HabitSchedule.IsDueToday(h.FrequencyType, h.FrequencyDays, target, today, weekCount,
                todayCheckIn?.Status);
            var (consistency, weeksDone, suggested) = Queries.LevelUp(h, history, today, timezone, calendar);
            // Archived anchors are not in habitsById: the link stays but is ignored here.
            var anchor = h.AnchorHabitId is { } aid && habitsById.TryGetValue(aid, out var a) ? a : null;
            var anchorName = anchor?.Name ?? h.AnchorText;
            var anchorDone = anchor is not null && todayCheckIn is null
                && allCheckIns[anchor.Id].Any(c => c.Date == today && c.Status is CheckInStatus.Done or CheckInStatus.Minimum);
            return new TodayHabitDto(h, due, weekCount,
                todayCheckIn is null ? null
                    : CheckInDto.From(todayCheckIn) with { Minutes = manualMinutes.TryGetValue(h.Id, out var m) ? m : null }, h.Goal?.Title, consistency, suggested, weeksDone,
                !suggested && Queries.LevelDown(h, history, today, timezone, calendar),
                Queries.Streak(h, history, today, timezone, calendar),
                today, paused, paused ? calendar.PausedUntil(today) : null,
                anchorName, anchorDone,
                quantityToday.TryGetValue(h.Id, out var qt) ? QuantityRules.FromTenths(qt) : 0m);
        })
        .OrderBy(h => h.IsPaused)
        .ThenByDescending(h => h.IsDueToday)
        .ThenBy(h => dayRank[h.Id])
        .ToList();

        var reviewStart = WeeklyReviewRules.ReviewableWeekStart(today);
        var reviewEnd = HabitSchedule.WeekEnd(reviewStart);
        var reviewDue = !await db.WeeklyReviews.AnyAsync(r => r.WeekStart == reviewStart, ct)
                        && await db.CheckIns.AnyAsync(c => c.Date >= reviewStart && c.Date <= reviewEnd, ct);

        return new TodayResponse(today, currentPeriod, player, xpToday,
            cycle is null ? null : CycleDto.From(cycle, today), goals, items,
            new WeeklyReviewStatusDto(reviewStart, reviewEnd, reviewDue));
    }
}
