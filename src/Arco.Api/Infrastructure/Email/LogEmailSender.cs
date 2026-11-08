namespace Arco.Api.Infrastructure.Email;

public sealed class LogEmailSender(ILogger<LogEmailSender> log) : IEmailSender
{
    public Task SendAsync(string to, string subject, string html, CancellationToken ct, string? idempotencyKey = null)
    {
        log.LogInformation("E-mail (não enviado, sem Resend) para {To}: {Subject}\n{Html}", to, subject, html);
        return Task.CompletedTask;
    }
}
