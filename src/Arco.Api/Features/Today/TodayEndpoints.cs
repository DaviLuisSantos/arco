namespace Arco.Api.Features.Today;

public static class TodayEndpoints
{
    public sealed class TodayHabitDto(
        Habit habit,
        bool isDueToday,
        int weekCount,
        CheckInDto? checkIn,
        string? goalTitle) : HabitDto(habit)
    {
        public bool IsDueToday { get; } = isDueToday;
        public int WeekCount { get; } = weekCount;
        public int? WeekTarget { get; } = habit.FrequencyType == FrequencyType.WeeklyTarget ? habit.TimesPerWeek : null;
        public CheckInDto? CheckIn { get; } = checkIn;
        public string? GoalTitle { get; } = goalTitle;
    }

    public sealed record TodayResponse(
        DateOnly Date,
        PlayerDto Player,
        int XpToday,
        CycleDto? Cycle,
        IReadOnlyList<GoalDto> Goals,
        IReadOnlyList<TodayHabitDto> Habits);

    public static IEndpointRouteBuilder MapToday(this IEndpointRouteBuilder api)
    {
        api.MapGet("/today", GetToday);
        return api;
    }

    private static async Task<TodayResponse> GetToday(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var today = await clock.GetTodayAsync(ct);
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
        var weekCheckIns = (await db.CheckIns.AsNoTracking()
                .Where(c => c.Date >= weekStart && c.Date <= weekEnd && c.Habit!.ArchivedAt == null)
                .ToListAsync(ct))
            .ToLookup(c => c.HabitId);

        var items = habits.Select(h =>
        {
            var week = weekCheckIns[h.Id].ToList();
            var todayCheckIn = week.FirstOrDefault(c => c.Date == today);
            var weekCount = HabitSchedule.WeekCount(week.Select(c => (c.Date, c.Status)), today);
            var due = HabitSchedule.IsDueToday(h.FrequencyType, h.FrequencyDays, h.TimesPerWeek, today, weekCount,
                todayCheckIn?.Status);
            return new TodayHabitDto(h, due, weekCount,
                todayCheckIn is null ? null : CheckInDto.From(todayCheckIn), h.Goal?.Title);
        })
        .OrderByDescending(h => h.IsDueToday)
        .ThenBy(h => h.Name, StringComparer.InvariantCultureIgnoreCase)
        .ThenBy(h => h.Id)
        .ToList();

        return new TodayResponse(today, player, xpToday,
            cycle is null ? null : CycleDto.From(cycle, today), goals, items);
    }
}
