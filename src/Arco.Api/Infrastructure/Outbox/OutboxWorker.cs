namespace Arco.Api.Infrastructure.Outbox;

/// <summary>Runs <see cref="OutboxProcessor"/> every 30 seconds.</summary>
public sealed class OutboxWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<OutboxWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), time);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Outbox worker failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public static class OutboxServiceCollectionExtensions
{
    public static IServiceCollection AddArcoOutbox(this IServiceCollection s, IConfiguration c)
    {
        s.AddScoped<OutboxProcessor>();
        s.AddScoped<IOutboxHandler, EmailOutboxHandler>();
        if (c.GetValue("Outbox:WorkerEnabled", true)) s.AddHostedService<OutboxWorker>();
        return s;
    }
}
