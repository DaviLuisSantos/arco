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
}
