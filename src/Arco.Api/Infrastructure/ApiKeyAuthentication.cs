using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Arco.Api.Infrastructure;

/// <summary>
/// API keys: <c>Authorization: Bearer arco_…</c> works on every <c>/api</c> route, like the app's cookie.
/// Only the SHA-256 of a key is stored; the key itself is shown once, when created.
/// </summary>
public static class ApiKeys
{
    public const string Scheme = "api-key";
    public const string KeyPrefix = "arco_";
    /// <summary>Claim present only on requests authenticated by an API key.</summary>
    public const string AuthMethodClaim = "amr";
    public const string AuthMethodApiKey = "api_key";
    /// <summary>Characters of the key kept in clear, so the owner can recognize it.</summary>
    public const int DisplayLength = 12;

    public static string Generate() =>
        KeyPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public static string Display(string key) => key[..DisplayLength];

    public static bool IsApiKeyRequest(HttpRequest request) =>
        request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

    public static bool UsedApiKey(this ClaimsPrincipal user) => user.HasClaim(AuthMethodClaim, AuthMethodApiKey);
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db,
    TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Last use is recorded at most once per minute per key, to avoid a write on every request.</summary>
    private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(1);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();

        var key = header["Bearer ".Length..].Trim();
        if (!key.StartsWith(ApiKeys.KeyPrefix, StringComparison.Ordinal)) return AuthenticateResult.Fail("Invalid API key.");

        var hash = ApiKeys.Hash(key);
        var apiKey = await db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash, Context.RequestAborted);
        if (apiKey is null) return AuthenticateResult.Fail("Invalid API key.");

        var now = time.GetUtcNow().UtcDateTime;
        if (apiKey.LastUsedAt is not { } last || now - last >= LastUsedResolution)
        {
            apiKey.LastUsedAt = now;
            await db.SaveChangesAsync(Context.RequestAborted);
        }
        // The endpoint gets a clean context.
        db.ChangeTracker.Clear();

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "owner"),
            new Claim(ApiKeys.AuthMethodClaim, ApiKeys.AuthMethodApiKey),
            new Claim("api_key_id", apiKey.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ], ApiKeys.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeys.Scheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        ApiResults.WriteErrorAsync(Context, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized,
            "Chave de API inválida ou revogada.");
}
