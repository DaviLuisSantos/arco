using System.Security.Cryptography;
using System.Text;

namespace Arco.Api.Features.Auth;

/// <summary>Error codes specific to accounts (the shared ones live in <see cref="ErrorCodes"/>).</summary>
public static class AuthCodes
{
    public const string EmailTaken = "email_taken";
    public const string InvalidCredentials = "invalid_credentials";
    public const string InvalidToken = "invalid_token";
}

/// <summary>Single-use e-mail tokens: 32 random bytes in base64url; only their SHA-256 is stored.</summary>
public static class AuthTokens
{
    public static readonly TimeSpan VerifyEmailLifetime = TimeSpan.FromHours(48);
    public static readonly TimeSpan ResetPasswordLifetime = TimeSpan.FromHours(1);

    public static string NewRawToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Adds (without saving) a new token and returns its raw value, to be sent by e-mail.</summary>
    public static string Issue(AppDbContext db, int userId, UserTokenPurpose purpose, TimeSpan lifetime, DateTime now)
    {
        var raw = NewRawToken();
        db.UserTokens.Add(new UserToken
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = Hash(raw),
            ExpiresAt = now + lifetime,
        });
        return raw;
    }

    /// <summary>The unused, unexpired token that matches <paramref name="raw"/>, or null.</summary>
    public static Task<UserToken?> FindValidAsync(AppDbContext db, string? raw, UserTokenPurpose purpose, DateTime now,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Task.FromResult<UserToken?>(null);
        var hash = Hash(raw.Trim());
        return db.UserTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash && t.Purpose == purpose && t.UsedAt == null && t.ExpiresAt > now, ct);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
