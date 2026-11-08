using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Arco.Api.Features.Auth;

/// <summary>
/// Two-step verification: the TOTP secret (protected with the "arco.totp" purpose), single-use recovery codes
/// (SHA-256 only) and the short-lived login ticket handed out between the password and the code.
/// </summary>
public static class TwoFactor
{
    public const string SecretPurpose = "arco.totp";
    public const string TicketPurpose = "login-2fa";
    public const int RecoveryCodeCount = 10;
    public static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(5);
    private const string RecoveryAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    public sealed record Ticket(int UserId, Guid Stamp, string Method);

    public static IDataProtector SecretProtector(IDataProtectionProvider provider) => provider.CreateProtector(SecretPurpose);

    private static IDataProtector TicketProtector(IDataProtectionProvider provider) => provider.CreateProtector(TicketPurpose);

    /// <summary>Protected <c>userId|stamp|method|expiresTicks</c>; a password change (new stamp) also kills it.</summary>
    public static string IssueTicket(IDataProtectionProvider provider, User user, string method, DateTime now)
    {
        var expires = (now + TicketLifetime).Ticks.ToString(CultureInfo.InvariantCulture);
        var payload = string.Join('|', user.Id.ToString(CultureInfo.InvariantCulture), user.SecurityStamp.ToString("N"), method, expires);
        return TicketProtector(provider).Protect(payload);
    }

    /// <summary>The ticket's content, or null when it is malformed, tampered with or expired.</summary>
    public static Ticket? ReadTicket(IDataProtectionProvider provider, string? ticket, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(ticket)) return null;
        string payload;
        try
        {
            payload = TicketProtector(provider).Unprotect(ticket.Trim());
        }
        catch (CryptographicException)
        {
            return null;
        }

        var parts = payload.Split('|');
        if (parts.Length != 4
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            || !Guid.TryParseExact(parts[1], "N", out var stamp)
            || !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var expiresTicks)
            || now.Ticks > expiresTicks)
        {
            return null;
        }
        return new Ticket(userId, stamp, parts[2]);
    }

    /// <summary>Ten new codes like <c>k3m9p-x7c2h</c> (about 50 bits each).</summary>
    public static List<string> NewRecoveryCodes()
    {
        var codes = new List<string>(RecoveryCodeCount);
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var raw = RandomNumberGenerator.GetString(RecoveryAlphabet, 10);
            codes.Add($"{raw[..5]}-{raw[5..]}");
        }
        return codes;
    }

    public static string HashRecoveryCode(string code) => AuthTokens.Hash(NormalizeRecoveryCode(code));

    private static string NormalizeRecoveryCode(string code) =>
        code.Trim().Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();

    /// <summary>
    /// Checks a TOTP code against the active secret, or else consumes a matching unused recovery code
    /// (marked used on the tracked entity; the caller saves). Reads codes by explicit owner, so it also works
    /// before the request is authenticated.
    /// </summary>
    public static async Task<bool> VerifyAsync(AppDbContext db, IDataProtectionProvider provider, User user, string? code,
        DateTime now, CancellationToken ct)
    {
        if (user.TotpSecretProtected is null || string.IsNullOrWhiteSpace(code)) return false;

        if (Totp.Normalize(code) is not null)
        {
            var secret = UnprotectSecret(provider, user.TotpSecretProtected);
            return secret is not null && Totp.Verify(secret, code, new DateTimeOffset(now, TimeSpan.Zero));
        }

        var hash = HashRecoveryCode(code);
        var match = await db.TotpRecoveryCodes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.UserId == user.Id && c.CodeHash == hash && c.UsedAt == null, ct);
        if (match is null) return false;
        match.UsedAt = now;
        return true;
    }

    public static string? UnprotectSecret(IDataProtectionProvider provider, string protectedSecret)
    {
        try
        {
            return SecretProtector(provider).Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
