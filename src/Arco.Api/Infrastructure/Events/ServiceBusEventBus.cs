using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace Arco.Api.Infrastructure.Events;

/// <summary>Bus backed by an Azure Service Bus queue; only used when <c>ServiceBus:ConnectionString</c> is set.</summary>
public sealed class ServiceBusEventBus(ServiceBusClient client) : IEventBus, IAsyncDisposable
{
    public const string WeekClosedQueue = "week-closed";

    private readonly ServiceBusSender _sender = client.CreateSender(WeekClosedQueue);

    public async Task PublishAsync(WeekClosed e, CancellationToken ct)
    {
        var message = new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(e))
        {
            ContentType = "application/json",
            MessageId = $"week-closed:{e.OwnerUserId}:{e.WeekStart:yyyy-MM-dd}",
        };
        await _sender.SendMessageAsync(message, ct);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}

/// <summary>Reads the <c>week-closed</c> queue and hands each event to <see cref="WeekClosedConsumer"/>.</summary>
public sealed class ServiceBusConsumerService(ServiceBusClient client, IServiceScopeFactory scopes,
    ILogger<ServiceBusConsumerService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = client.CreateProcessor(ServiceBusEventBus.WeekClosedQueue,
            new ServiceBusProcessorOptions { MaxConcurrentCalls = 2, AutoCompleteMessages = true });

        processor.ProcessMessageAsync += async args =>
        {
            var e = JsonSerializer.Deserialize<WeekClosed>(args.Message.Body.ToArray())
                    ?? throw new InvalidOperationException("Evento week-closed vazio.");
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<WeekClosedConsumer>().HandleAsync(e, args.CancellationToken);
        };
        processor.ProcessErrorAsync += args =>
        {
            log.LogError(args.Exception, "Service Bus ({Source}) falhou em {Entity}", args.ErrorSource, args.EntityPath);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        await processor.StopProcessingAsync(CancellationToken.None);
    }
}
