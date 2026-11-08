using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Arco.Api.Infrastructure.Email;

public sealed class ResendEmailSender(HttpClient http, IConfiguration config, ILogger<ResendEmailSender> log) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        var from = config["Email:From"] ?? "Arco <onboarding@resend.dev>";
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new { from, to = new[] { to }, subject, html })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config["Email:ResendApiKey"]);
        try
        {
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
                log.LogError("Resend falhou ({Status}): {Body}", (int)res.StatusCode, await res.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Falha ao enviar e-mail para {To}", to);
        }
    }
}
