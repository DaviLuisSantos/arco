namespace Arco.Api.Features.Demo;

/// <summary>Hourly deletes demo accounts older than 24h; their data goes with them (cascade).</summary>
public sealed class DemoCleanupService(IServiceScopeFactory scopes, TimeProvider time, ILogger<DemoCleanupService> logger)
    : BackgroundService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), time);
        do
        {
            try { await CleanAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Demo cleanup failed."); }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    public async Task<int> CleanAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = time.GetUtcNow().UtcDateTime - Lifetime;
        var deleted = await db.Users.IgnoreQueryFilters()
            .Where(u => u.IsDemo && u.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);
        if (deleted > 0) logger.LogInformation("Removed {Count} expired demo account(s).", deleted);
        return deleted;
    }
}
