using System.Text.Json;
using Arco.Api.Domain;

namespace Arco.Api.Infrastructure.Outbox;

public static class OutboxKinds
{
    public const string Email = "email";
    public const string WeekClosed = "week_closed";
}

/// <summary>Payload of an <see cref="OutboxKinds.Email"/> message.</summary>
public sealed record EmailOutboxPayload(string To, string Subject, string Html);

public static class Outbox
{
    /// <summary>
    /// Adds a message to the outbox without saving: it goes into the SaveChanges of the action that caused it.
    /// After saving, <see cref="OutboxProcessor.TryDispatchAsync"/> sends it right away (the worker retries on failure).
    /// The returned message gets its Id when saved.
    /// </summary>
    public static OutboxMessage Enqueue(AppDbContext db, string kind, object payload, DateTime? now = null)
    {
        var at = now ?? DateTime.UtcNow;
        var message = new OutboxMessage
        {
            Kind = kind,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = at,
            NextAttemptAt = at,
        };
        db.OutboxMessages.Add(message);
        return message;
    }
}

public interface IOutboxHandler
{
    string Kind { get; }

    /// <summary>Delivers the message; throws to have it retried.</summary>
    Task HandleAsync(OutboxMessage m, CancellationToken ct);
}
