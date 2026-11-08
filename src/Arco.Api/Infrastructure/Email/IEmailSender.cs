namespace Arco.Api.Infrastructure.Email;

public interface IEmailSender
{
    /// <summary>Sends the e-mail. Throws <see cref="EmailSendException"/> when the provider does not accept it.
    /// <paramref name="idempotencyKey"/> lets the provider drop a repeated send of the same message.</summary>
    Task SendAsync(string to, string subject, string html, CancellationToken ct, string? idempotencyKey = null);
}

/// <summary>The provider refused the e-mail or could not be reached; the outbox will retry.</summary>
public sealed class EmailSendException(string message, Exception? inner = null) : Exception(message, inner);
