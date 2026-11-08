namespace Arco.Api.Infrastructure.Email;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string html, CancellationToken ct);
}
