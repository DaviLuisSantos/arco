using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Arco.Api.Infrastructure.Email;

public sealed class ResendEmailSender(HttpClient http, IConfiguration config) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string html, CancellationToken ct, string? idempotencyKey = null)
    {
        var from = config["Email:From"] ?? "Arco <onboarding@resend.dev>";
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new { from, to = new[] { to }, subject, html })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config["Email:ResendApiKey"]);
        if (!string.IsNullOrEmpty(idempotencyKey)) req.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        try
        {
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
                throw new EmailSendException($"Resend respondeu {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync(ct)}");
        }
        catch (HttpRequestException ex)
        {
            throw new EmailSendException("Falha ao falar com o Resend.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new EmailSendException("Tempo esgotado ao falar com o Resend.", ex);
        }
    }
}
