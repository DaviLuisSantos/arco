using System.Text.Json;
using Arco.Api.Domain;
using Arco.Api.Infrastructure.Email;

namespace Arco.Api.Infrastructure.Outbox;

public sealed class EmailOutboxHandler(IEmailSender email) : IOutboxHandler
{
    public string Kind => OutboxKinds.Email;

    public async Task HandleAsync(OutboxMessage m, CancellationToken ct)
    {
        var p = JsonSerializer.Deserialize<EmailOutboxPayload>(m.Payload)
                ?? throw new InvalidOperationException("Payload de e-mail vazio.");
        await email.SendAsync(p.To, p.Subject, p.Html, ct, $"outbox-{m.Id}");
    }
}
