using System.Buffers.Text;
using Arco.Api.Infrastructure.Push;

namespace Arco.Api.Features.Push;

public static class PushEndpoints
{
    public sealed record PushConfigDto(string PublicKey, int Subscriptions);
    public sealed record SubscriptionKeys(string? P256dh, string? Auth);
    /// <summary>Same shape as the browser's <c>PushSubscription.toJSON()</c>.</summary>
    public sealed record SubscribeRequest(string? Endpoint, SubscriptionKeys? Keys);
    public sealed record UnsubscribeRequest(string? Endpoint);
    public sealed record TestResult(int Sent);

    private const int MaxEndpointLength = 2048;

    public static IEndpointRouteBuilder MapPush(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/push");
        group.MapGet("/config", Config);
        group.MapPost("/subscriptions", Subscribe);
        group.MapPost("/subscriptions/remove", Unsubscribe);
        group.MapPost("/test", Test);
        return api;
    }

    private static async Task<PushConfigDto> Config(VapidKeys keys, AppDbContext db, CancellationToken ct) =>
        new(keys.PublicKeyBase64Url, await db.PushSubscriptions.CountAsync(ct));

    private static async Task<IResult> Subscribe(SubscribeRequest? request, HttpContext http, AppDbContext db,
        AppClock clock, CancellationToken ct)
    {
        var endpoint = request?.Endpoint?.Trim();
        if (string.IsNullOrEmpty(endpoint) || endpoint.Length > MaxEndpointLength
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return ApiResults.Validation("Inscrição inválida: o endpoint precisa ser uma URL https.");

        var p256dh = request!.Keys?.P256dh?.Trim();
        var auth = request.Keys?.Auth?.Trim();
        if (!WebPushCrypto.IsValidPublicKey(TryDecode(p256dh)) || TryDecode(auth) is not { Length: 16 })
            return ApiResults.Validation("Inscrição inválida: chaves p256dh/auth ausentes ou malformadas.");

        var userAgent = http.Request.Headers.UserAgent.ToString();
        var subscription = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);
        if (subscription is null)
        {
            subscription = new PushSubscription { Endpoint = endpoint, CreatedAt = clock.UtcNow };
            db.PushSubscriptions.Add(subscription);
        }
        subscription.P256dh = p256dh!;
        subscription.Auth = auth!;
        subscription.UserAgent = string.IsNullOrEmpty(userAgent) ? null : userAgent.Length > 300 ? userAgent[..300] : userAgent;
        subscription.FailureCount = 0;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            // Registered by a concurrent request: same endpoint, nothing else to do.
        }
        return Results.NoContent();
    }

    private static async Task<IResult> Unsubscribe(UnsubscribeRequest? request, AppDbContext db, CancellationToken ct)
    {
        var endpoint = request?.Endpoint?.Trim();
        if (string.IsNullOrEmpty(endpoint)) return ApiResults.Validation("Informe o endpoint da inscrição.");
        await db.PushSubscriptions.Where(s => s.Endpoint == endpoint).ExecuteDeleteAsync(ct);
        return Results.NoContent();
    }

    private static async Task<TestResult> Test(PushNotifier notifier, CancellationToken ct) =>
        new(await notifier.SendToAllAsync(
            new PushMessage("Arco", "Notificações funcionando. Os lembretes chegam assim.", "/profile", "test"), ct));

    private static byte[]? TryDecode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        try
        {
            return Base64Url.DecodeFromChars(value.TrimEnd('='));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
