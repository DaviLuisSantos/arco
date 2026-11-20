using System.Text.Json;
using Arco.Api.Domain;
using Arco.Api.Infrastructure.Outbox;

namespace Arco.Api.Infrastructure.Events;

/// <summary>The user closed their week (weekly review). Partners are told about it.</summary>
public sealed record WeekClosed(int OwnerUserId, DateOnly WeekStart);

/// <summary>Outbox handler: hands a saved <see cref="WeekClosed"/> to its consumer (retried by the worker on failure).</summary>
public sealed class WeekClosedOutboxHandler(WeekClosedConsumer consumer) : IOutboxHandler
{
    public string Kind => OutboxKinds.WeekClosed;

    public async Task HandleAsync(OutboxMessage m, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<WeekClosed>(m.Payload)
                ?? throw new InvalidOperationException("Payload de semana fechada vazio.");
        await consumer.HandleAsync(e, ct);
    }
}
