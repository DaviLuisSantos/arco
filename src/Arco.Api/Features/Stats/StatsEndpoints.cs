using Arco.Api.Features.Reviews;

namespace Arco.Api.Features.Stats;

public static class StatsEndpoints
{
    public const int Weeks = 12;
    public const int RecentDays = 30;

    /// <summary>One Monday–Sunday week; the current week counts only up to today.</summary>
    public sealed record WeekStatsDto(
        DateOnly WeekStart, DateOnly WeekEnd, int Xp, int Done, int Minimum, int Missed, int Expected, double Rate);

    public sealed record PeriodDto(
        DateOnly From, DateOnly To, int Xp, CheckInTotalsDto CheckIns, IReadOnlyList<HabitPeriodStatsDto> Habits);

    public sealed record StatsResponse(IReadOnlyList<WeekStatsDto> Weeks, PeriodDto Recent);

    public static IEndpointRouteBuilder MapStats(this IEndpointRouteBuilder api)
    {
        api.MapGet("/stats", Get);
        return api;
    }

    /// <summary>
    /// The last 12 weeks (oldest first, the current one included) and the last 30 days. Uses the same per-habit
    /// numbers as the weekly review, so both always agree.
    /// </summary>
    private static async Task<StatsResponse> Get(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var today = await clock.GetTodayAsync(ct);
        var timezone = await clock.GetTimezoneAsync(ct);

        var firstWeek = HabitSchedule.WeekStart(today).AddDays(-7 * (Weeks - 1));
        var xpByWeek = (await db.XpEvents.AsNoTracking()
                .Where(e => e.Date >= firstWeek && e.Date <= today)
                .Select(e => new { e.Date, e.Amount })
                .ToListAsync(ct))
            .GroupBy(e => HabitSchedule.WeekStart(e.Date))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        var weeks = new List<WeekStatsDto>(Weeks);
        for (var i = 0; i < Weeks; i++)
        {
            var start = firstWeek.AddDays(7 * i);
            var end = start.AddDays(6);
            var stats = await ReviewStats.ForRangeAsync(db, start, end < today ? end : today, today, timezone, ct);
            var totals = ReviewStats.Totals(stats);
            var expected = stats.Sum(s => s.Expected);
            var rate = expected == 0 ? 0 : Math.Min(1, (double)(totals.Done + totals.Minimum) / expected);
            weeks.Add(new WeekStatsDto(start, end, xpByWeek.GetValueOrDefault(start), totals.Done, totals.Minimum,
                totals.Missed, expected, Math.Round(rate, 4)));
        }

        var from = today.AddDays(-(RecentDays - 1));
        var habits = await ReviewStats.ForRangeAsync(db, from, today, today, timezone, ct);
        var recent = new PeriodDto(from, today, await ReviewStats.XpInRangeAsync(db, from, today, ct),
            ReviewStats.Totals(habits), habits);

        return new StatsResponse(weeks, recent);
    }
}
