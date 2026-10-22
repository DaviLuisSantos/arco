using System.Text.Json;

namespace Arco.Api.Infrastructure.Push;

/// <summary>What the service worker shows: title, body, the page opened on click and a tag that replaces older ones.</summary>
public sealed record PushMessage(string Title, string Body, string Url, string Tag);

/// <summary>Sends a message to every subscribed device and keeps the subscription list clean.</summary>
public sealed class PushNotifier(AppDbContext db, IPushSender sender, AppClock clock, ILogger<PushNotifier> log)
{
    /// <summary>A subscription that fails this many times in a row is dropped.</summary>
    public const int MaxConsecutiveFailures = 10;

    /// <returns>How many devices accepted the message.</returns>
    public async Task<int> SendToAllAsync(PushMessage message, CancellationToken ct)
    {
        var subscriptions = await db.PushSubscriptions.ToListAsync(ct);
        var json = JsonSerializer.Serialize(message, JsonSerializerOptions.Web);
        var sent = 0;

        foreach (var subscription in subscriptions)
        {
            PushOutcome outcome;
            try
            {
                outcome = await sender.SendAsync(subscription, json, ct);
            }
            catch (HttpRequestException ex)
            {
                log.LogWarning(ex, "Push to subscription {Id} failed", subscription.Id);
                outcome = PushOutcome.Failed;
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "Push to subscription {Id} timed out", subscription.Id);
                outcome = PushOutcome.Failed;
            }

            switch (outcome)
            {
                case PushOutcome.Sent:
                    subscription.LastSuccessAt = clock.UtcNow;
                    subscription.FailureCount = 0;
                    sent++;
                    break;
                case PushOutcome.Gone:
                    db.PushSubscriptions.Remove(subscription);
                    break;
                default:
                    if (++subscription.FailureCount >= MaxConsecutiveFailures) db.PushSubscriptions.Remove(subscription);
                    break;
            }
        }

        await db.SaveChangesAsync(ct);
        return sent;
    }
}
