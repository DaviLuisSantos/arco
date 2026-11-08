using Arco.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Arco.Api.Infrastructure.Outbox;

/// <summary>
/// Delivers outbox messages. A message is claimed with an atomic UPDATE of <c>LockedUntil</c>, so the immediate send
/// of the request and the worker never deliver the same one twice.
/// </summary>
public sealed class OutboxProcessor(AppDbContext db, IEnumerable<IOutboxHandler> handlers, TimeProvider time,
    ILogger<OutboxProcessor> log)
{
    public const int MaxAttempts = 6;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    private const int BatchSize = 50;

    /// <summary>Wait before attempt N+1 after attempt N failed (N = 1..5).</summary>
    private static readonly TimeSpan[] Backoff =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(12)];

    public static TimeSpan BackoffAfter(int attempts) => Backoff[Math.Clamp(attempts, 1, Backoff.Length) - 1];

    /// <summary>One worker cycle: delivers what is due and cleans up old messages. Returns how many it tried.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var ids = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.SentAt == null && m.FailedAt == null && m.NextAttemptAt <= now
                        && (m.LockedUntil == null || m.LockedUntil < now))
            .OrderBy(m => m.NextAttemptAt).Select(m => m.Id).Take(BatchSize).ToListAsync(ct);

        var tried = 0;
        foreach (var id in ids)
            if (await DispatchAsync(id, ct)) tried++;

        var cutoff = now - Retention;
        await db.OutboxMessages
            .Where(m => (m.SentAt != null && m.SentAt < cutoff) || (m.FailedAt != null && m.FailedAt < cutoff))
            .ExecuteDeleteAsync(ct);
        return tried;
    }

    /// <summary>Sends one message now. Never throws: a failure is recorded and left for the worker.</summary>
    public async Task TryDispatchAsync(long id, CancellationToken ct)
    {
        try
        {
            await DispatchAsync(id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Outbox: falha ao despachar a mensagem {Id}.", id);
        }
    }

    /// <returns>true if this call claimed the message and tried to deliver it.</returns>
    private async Task<bool> DispatchAsync(long id, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var lockedUntil = now + LockDuration;
        var claimed = await db.OutboxMessages
            .Where(m => m.Id == id && m.SentAt == null && m.FailedAt == null && (m.LockedUntil == null || m.LockedUntil < now))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedUntil, lockedUntil), ct);
        if (claimed == 0) return false;

        var message = await db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == id, ct);
        string? error = null;
        try
        {
            var handler = handlers.FirstOrDefault(h => h.Kind == message.Kind)
                          ?? throw new InvalidOperationException($"Sem handler para a mensagem do tipo {message.Kind}.");
            await handler.HandleAsync(message, ct);
        }
        catch (OperationCanceledException)
        {
            // Shutting down: release the claim without counting an attempt.
            await db.OutboxMessages.Where(m => m.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.LockedUntil, (DateTime?)null), CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            error = Truncate($"{ex.GetType().Name}: {ex.Message}", 500);
            log.LogWarning(ex, "Outbox: falha ao entregar a mensagem {Id} ({Kind}), tentativa {Attempt}.", id, message.Kind,
                message.Attempts + 1);
        }

        var done = time.GetUtcNow().UtcDateTime;
        var attempts = message.Attempts + 1;
        if (error is null)
        {
            // The payload can hold a reset token: do not keep it after delivery.
            await db.OutboxMessages.Where(m => m.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.SentAt, done).SetProperty(m => m.Attempts, attempts)
                .SetProperty(m => m.Payload, "").SetProperty(m => m.LockedUntil, (DateTime?)null)
                .SetProperty(m => m.LastError, (string?)null), CancellationToken.None);
        }
        else if (attempts >= MaxAttempts)
        {
            await db.OutboxMessages.Where(m => m.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.FailedAt, done).SetProperty(m => m.Attempts, attempts)
                .SetProperty(m => m.Payload, "").SetProperty(m => m.LockedUntil, (DateTime?)null)
                .SetProperty(m => m.LastError, error), CancellationToken.None);
        }
        else
        {
            var next = done + BackoffAfter(attempts);
            await db.OutboxMessages.Where(m => m.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Attempts, attempts).SetProperty(m => m.NextAttemptAt, next)
                .SetProperty(m => m.LockedUntil, (DateTime?)null).SetProperty(m => m.LastError, error), CancellationToken.None);
        }
        return true;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
