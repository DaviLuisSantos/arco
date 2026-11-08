using System.Security.Cryptography;
using System.Text;

namespace Arco.Api.Features.Auth;

/// <summary>TOTP (RFC 6238): HMAC-SHA1, 6 digits, 30-second steps, accepting one step of drift either way.</summary>
public static class Totp
{
    public const int Digits = 6;
    public const int PeriodSeconds = 30;
    public const string Issuer = "Arco";
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>A new 160-bit secret, in Base32 (no padding), as authenticator apps expect.</summary>
    public static string NewSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    public static long Step(DateTimeOffset now) => now.ToUnixTimeSeconds() / PeriodSeconds;

    /// <summary>The code for <paramref name="secret"/> at <paramref name="now"/>.</summary>
    public static string Compute(string secret, DateTimeOffset now) => Compute(Base32Decode(secret), Step(now));

    public static string Compute(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(step & 0xFF);
            step >>= 8;
        }

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, counter, hash);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>True when <paramref name="code"/> matches the current step or the one before/after it.</summary>
    public static bool Verify(string secret, string? code, DateTimeOffset now)
    {
        var normalized = Normalize(code);
        if (normalized is null) return false;
        byte[] key;
        try
        {
            key = Base32Decode(secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var step = Step(now);
        var ok = false;
        for (var drift = -1; drift <= 1; drift++)
        {
            var expected = Compute(key, step + drift);
            ok |= CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(normalized));
        }
        return ok;
    }

    /// <summary>Six digits (spaces allowed, e.g. "123 456"), or null.</summary>
    public static string? Normalize(string? code)
    {
        var digits = code?.Replace(" ", "", StringComparison.Ordinal).Trim();
        return digits is { Length: Digits } && digits.All(char.IsAsciiDigit) ? digits : null;
    }

    public static string OtpAuthUri(string secret, string account) =>
        $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={secret}&issuer={Uri.EscapeDataString(Issuer)}&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}";

    public static string Base32Encode(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Base32Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0) sb.Append(Base32Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    public static byte[] Base32Decode(string value)
    {
        var clean = value.Trim().TrimEnd('=').Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        var result = new List<byte>(clean.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in clean)
        {
            var index = Base32Alphabet.IndexOf(c, StringComparison.Ordinal);
            if (index < 0) throw new FormatException("Invalid Base32 character.");
            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                result.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return [.. result];
    }
}
