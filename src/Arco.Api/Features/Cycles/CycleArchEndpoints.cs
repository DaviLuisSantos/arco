namespace Arco.Api.Features.Cycles;

/// <summary>The cycle as a stone arch: one stone per week, filled by focus minutes (see docs/PRODUCT.md).</summary>
public static class CycleArchEndpoints
{
    public sealed record ArchHabitDto(int HabitId, string Name, string? Emoji);

    public sealed record ArchBrickDto(int SessionId, int HabitId, int Minutes, FocusSource Source);

    public sealed record ArchDayDto(DateOnly Date, int Minutes, IReadOnlyList<ArchBrickDto> Bricks);

    public sealed record ArchHabitMinutesDto(int HabitId, int Minutes);

    public sealed record ArchStoneDto(
        int Week,
        DateOnly From,
        DateOnly To,
        int Minutes,
        IReadOnlyList<ArchHabitMinutesDto> ByHabit,
        IReadOnlyList<ArchDayDto> Days);

    public sealed record CycleArchDto(
        int CycleId,
        DateOnly StartDate,
        DateOnly EndDate,
        bool Closed,
        int Weeks,
        string Layout,
        int WeeklyTargetMinutes,
        int TotalMinutes,
        IReadOnlyList<ArchHabitDto> Habits,
        IReadOnlyList<ArchStoneDto> Stones);

    public static IEndpointRouteBuilder MapCycleArch(this IEndpointRouteBuilder api)
    {
        api.MapGet("/cycles/{id:int}/arch", Get);
        return api;
    }

    private static async Task<IResult> Get(int id, AppDbContext db, CancellationToken ct)
    {
        var cycle = await db.Cycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cycle is null) return ApiResults.NotFound("Ciclo não encontrado.");

        var start = cycle.StartDate;
        var end = cycle.EndDate;
        var sessions = await db.FocusSessions.AsNoTracking()
            .Where(s => s.Date >= start && s.Date <= end)
            .OrderBy(s => s.Date).ThenBy(s => s.CreatedAt).ThenBy(s => s.Id)
            .ToListAsync(ct);

        var habitIds = sessions.Select(s => s.HabitId).Distinct().ToList();
        var habits = await db.Habits.AsNoTracking()
            .Where(h => habitIds.Contains(h.Id))
            .OrderBy(h => h.Id)
            .Select(h => new ArchHabitDto(h.Id, h.Name, h.Emoji))
            .ToListAsync(ct);

        var byDate = sessions.ToLookup(s => s.Date);
        var weeks = CycleMath.WeekCount(start, end);
        var stones = new List<ArchStoneDto>(weeks);
        for (var week = 1; week <= weeks; week++)
        {
            var from = start.AddDays((week - 1) * 7);
            var to = from.AddDays(6) < end ? from.AddDays(6) : end;
            var days = new List<ArchDayDto>();
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var bricks = byDate[date]
                    .Select(s => new ArchBrickDto(s.Id, s.HabitId, s.Minutes, s.Source))
                    .ToList();
                days.Add(new ArchDayDto(date, bricks.Sum(b => b.Minutes), bricks));
            }
            var byHabit = days.SelectMany(d => d.Bricks)
                .GroupBy(b => b.HabitId)
                .OrderBy(g => g.Key)
                .Select(g => new ArchHabitMinutesDto(g.Key, g.Sum(b => b.Minutes)))
                .ToList();
            stones.Add(new ArchStoneDto(week, from, to, days.Sum(d => d.Minutes), byHabit, days));
        }

        return Results.Ok(new CycleArchDto(
            cycle.Id, start, end, cycle.Status == CycleStatus.Completed, weeks, CycleMath.ArchLayout(weeks),
            cycle.WeeklyFocusMinutes, stones.Sum(s => s.Minutes), habits, stones));
    }
}
