namespace Arco.Api.Features.Player;

public static class PlayerEndpoints
{
    public sealed record XpEventDto(DateOnly Date, int Amount, XpSource Source, string Description);

    public sealed class PlayerDetailDto(int totalXp, IReadOnlyList<XpEventDto> recentEvents) : PlayerDto(totalXp)
    {
        public IReadOnlyList<XpEventDto> RecentEvents { get; } = recentEvents;
    }

    public static IEndpointRouteBuilder MapPlayer(this IEndpointRouteBuilder api)
    {
        api.MapGet("/player", GetPlayer);
        return api;
    }

    private static async Task<PlayerDetailDto> GetPlayer(AppDbContext db, CancellationToken ct)
    {
        var total = await db.XpEvents.SumAsync(e => e.Amount, ct);
        var recent = await db.XpEvents.AsNoTracking()
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Take(20)
            .Select(e => new XpEventDto(e.Date, e.Amount, e.Source, e.Description))
            .ToListAsync(ct);
        return new PlayerDetailDto(total, recent);
    }
}
