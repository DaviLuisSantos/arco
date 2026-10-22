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
        StreakDto streak) : HabitDto(habit)
    {
        public bool IsDueToday { get; } = isDueToday;
        public int WeekCount { get; } = weekCount;
        public int? WeekTarget { get; } = habit.FrequencyType == FrequencyType.WeeklyTarget ? habit.TimesPerWeek : null;
        public CheckInDto? CheckIn { get; } = checkIn;
        public string? GoalTitle { get; } = goalTitle;
        public ConsistencyDto Consistency { get; } = consistency;
        public bool LevelUpSuggested { get; } = levelUpSuggested;
        public StreakDto Streak { get; } = streak;
    }

    /// <param name="Due">The reviewable week has check-ins and no review yet.</param>
    public sealed record WeeklyReviewStatusDto(DateOnly WeekStart, DateOnly WeekEnd, bool Due);

    public sealed record TodayResponse(
        DateOnly Date,
        PlayerDto Player,
        int XpToday,
        CycleDto? Cycle,
        IReadOnlyList<GoalDto> Goals,
        IReadOnlyList<TodayHabitDto> Habits,
        WeeklyReviewStatusDto WeeklyReview);

    public static IEndpointRouteBuilder MapToday(this IEndpointRouteBuilder api)
    {
        api.MapGet("/today", GetToday);
        return api;
    }

    private static async Task<TodayResponse> GetToday(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var today = await clock.GetTodayAsync(ct);
        var timezone = await clock.GetTimezoneAsync(ct);
        var player = await Queries.GetPlayerAsync(db, ct);
        var xpToday = await db.XpEvents.Where(e => e.Date == today).SumAsync(e => e.Amount, ct);

        var cycle = await Queries.GetActiveCycleAsync(db, ct);
        var goals = cycle is null ? [] : await Queries.GetGoalDtosForCycleAsync(db, cycle.Id, ct);

        var habits = await db.Habits.AsNoTracking()
            .Include(h => h.Levels)
            .Include(h => h.Goal)
            .Where(h => h.ArchivedAt == null)
            .ToListAsync(ct);

        var weekStart = HabitSchedule.WeekStart(today);
        var weekEnd = HabitSchedule.WeekEnd(today);
        // The whole history feeds the streak; the current week and the last 15 days, the rest.
        var suggestionStart = today.AddDays(-LevelUpSuggestion.WindowDays);
        var from = weekStart < suggestionStart ? weekStart : suggestionStart;
        var allCheckIns = (await db.CheckIns.AsNoTracking()
                .Where(c => c.Date <= weekEnd && c.Habit!.ArchivedAt == null)
                .ToListAsync(ct))
            .ToLookup(c => c.HabitId);

        var items = habits.Select(h =>
        {
            var history = allCheckIns[h.Id].ToList();
            var recent = history.Where(c => c.Date >= from).ToList();
            var week = recent.Where(c => c.Date >= weekStart).ToList();
            var todayCheckIn = week.FirstOrDefault(c => c.Date == today);
            var weekCount = HabitSchedule.WeekCount(week.Select(c => (c.Date, c.Status)), today);
            var due = HabitSchedule.IsDueToday(h.FrequencyType, h.FrequencyDays, h.TimesPerWeek, today, weekCount,
                todayCheckIn?.Status);
            var (consistency, suggested) = Queries.LevelUp(h, recent, today, timezone);
            return new TodayHabitDto(h, due, weekCount,
                todayCheckIn is null ? null : CheckInDto.From(todayCheckIn), h.Goal?.Title, consistency, suggested,
                Queries.Streak(h, history, today, timezone));
        })
        .OrderByDescending(h => h.IsDueToday)
        .ThenBy(h => h.Name, StringComparer.InvariantCultureIgnoreCase)
        .ThenBy(h => h.Id)
        .ToList();

        var reviewStart = WeeklyReviewRules.ReviewableWeekStart(today);
        var reviewEnd = HabitSchedule.WeekEnd(reviewStart);
        var reviewDue = !await db.WeeklyReviews.AnyAsync(r => r.WeekStart == reviewStart, ct)
                        && await db.CheckIns.AnyAsync(c => c.Date >= reviewStart && c.Date <= reviewEnd, ct);

        return new TodayResponse(today, player, xpToday,
            cycle is null ? null : CycleDto.From(cycle, today), goals, items,
            new WeeklyReviewStatusDto(reviewStart, reviewEnd, reviewDue));
    }
}
