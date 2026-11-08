using System.Text.Json;
using Arco.Api.Domain;
using Arco.Api.Infrastructure.Outbox;

namespace Arco.Api.Infrastructure.Events;

/// <summary>The user closed their week (weekly review). Partners are told about it.</summary>
public sealed record WeekClosed(int OwnerUserId, DateOnly WeekStart);

public interface IEventBus
{
    Task PublishAsync(WeekClosed e, CancellationToken ct);
}

/// <summary>Default bus: calls the consumer in the same process.</summary>
public sealed class InMemoryEventBus(WeekClosedConsumer consumer) : IEventBus
{
    public Task PublishAsync(WeekClosed e, CancellationToken ct) => consumer.HandleAsync(e, ct);
}

/// <summary>Outbox handler: publishes a saved <see cref="WeekClosed"/> on the bus (retried by the worker on failure).</summary>
public sealed class WeekClosedOutboxHandler(IEventBus bus) : IOutboxHandler
{
    public string Kind => OutboxKinds.WeekClosed;

    public async Task HandleAsync(OutboxMessage m, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<WeekClosed>(m.Payload)
                ?? throw new InvalidOperationException("Payload de semana fechada vazio.");
        await bus.PublishAsync(e, ct);
    }
}
