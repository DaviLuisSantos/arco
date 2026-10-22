using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Arco.Api.Infrastructure.Push;

public enum PushOutcome
{
    Sent,
    /// <summary>The push service says the subscription no longer exists (404/410): delete it.</summary>
    Gone,
    Failed,
}

public interface IPushSender
{
    Task<PushOutcome> SendAsync(PushSubscription subscription, string payloadJson, CancellationToken ct);
}

/// <summary>Delivers an encrypted message to the subscription's push service.</summary>
public sealed class WebPushSender(HttpClient http, VapidKeys keys, TimeProvider time, ILogger<WebPushSender> log)
    : IPushSender
{
    /// <summary>How long the push service keeps an undelivered message (a reminder is useless the next day).</summary>
    public const int TtlSeconds = 4 * 60 * 60;

    public async Task<PushOutcome> SendAsync(PushSubscription subscription, string payloadJson, CancellationToken ct)
    {
        var body = WebPushCrypto.Encrypt(Encoding.UTF8.GetBytes(payloadJson),
            Base64Url.DecodeFromChars(subscription.P256dh), Base64Url.DecodeFromChars(subscription.Auth));

        using var signer = keys.CreateSigner();
        using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Endpoint);
        request.Headers.TryAddWithoutValidation("Authorization",
            WebPushCrypto.VapidAuthorization(subscription.Endpoint, keys.Subject, signer, keys.PublicKey, time.GetUtcNow()));
        request.Headers.TryAddWithoutValidation("TTL", TtlSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("Urgency", "normal");
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");

        using var response = await http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return PushOutcome.Sent;
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return PushOutcome.Gone;

        var detail = await response.Content.ReadAsStringAsync(ct);
        log.LogWarning("Push to {Host} failed: {Status} {Detail}", request.RequestUri?.Host, (int)response.StatusCode,
            detail.Length > 300 ? detail[..300] : detail);
        return PushOutcome.Failed;
    }
}
