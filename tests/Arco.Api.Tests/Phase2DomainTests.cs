using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Arco.Api.Domain;
using Arco.Api.Features.Push;
using Arco.Api.Infrastructure.Push;

namespace Arco.Api.Tests;

public class LevelUpSuggestionTests
{
    // 2026-03-15 is a Sunday.
    private static readonly DateOnly Today = new(2026, 3, 15);

    private static IEnumerable<(DateOnly, CheckInStatus)> Days(DateOnly from, DateOnly to, CheckInStatus status = CheckInStatus.Done)
    {
        for (var d = from; d <= to; d = d.AddDays(1)) yield return (d, status);
    }

    [Fact]
    public void Window_ends_today_only_when_today_has_a_check_in()
    {
        Assert.Equal((new DateOnly(2026, 3, 2), Today), LevelUpSuggestion.Window(Today, checkedInToday: true));
        Assert.Equal((new DateOnly(2026, 3, 1), Today.AddDays(-1)), LevelUpSuggestion.Window(Today, checkedInToday: false));
    }

    [Fact]
    public void Expected_count_follows_frequency()
    {
        var from = new DateOnly(2026, 3, 2); // Monday
        var to = from.AddDays(13);
        Assert.Equal(14, LevelUpSuggestion.ExpectedCount(FrequencyType.Daily, [], null, from, to));
        // Mon/Wed/Fri over two weeks.
        Assert.Equal(6, LevelUpSuggestion.ExpectedCount(FrequencyType.SpecificDays, [1, 3, 5], null, from, to));
        Assert.Equal(6, LevelUpSuggestion.ExpectedCount(FrequencyType.WeeklyTarget, [], 3, from, to));
        // Partial week rounds up: 3x/week over 3 days → 2.
        Assert.Equal(2, LevelUpSuggestion.ExpectedCount(FrequencyType.WeeklyTarget, [], 3, from, from.AddDays(2)));
        Assert.Equal(0, LevelUpSuggestion.ExpectedCount(FrequencyType.Daily, [], null, to, from));
    }

    [Fact]
    public void Only_done_counts_and_rate_is_capped()
    {
        var checkIns = Days(Today.AddDays(-13), Today).ToList();
        checkIns[0] = (checkIns[0].Item1, CheckInStatus.Minimum);
        var c = LevelUpSuggestion.Measure(FrequencyType.Daily, [], null, checkIns, Today);
        Assert.Equal(13, c.Done);
        Assert.Equal(14, c.Expected);

        var weekly = LevelUpSuggestion.Measure(FrequencyType.WeeklyTarget, [], 1, Days(Today.AddDays(-13), Today), Today);
        Assert.Equal(1, weekly.Rate);
    }

    [Theory]
    [InlineData(12, true)]  // 12/14 = 85.7%
    [InlineData(11, false)] // 11/14 = 78.6%
    public void Suggests_at_80_percent(int doneDays, bool expected)
    {
        var checkIns = Days(Today.AddDays(-13), Today.AddDays(-14 + doneDays));
        var c = LevelUpSuggestion.Measure(FrequencyType.Daily, [], null, checkIns, Today);
        Assert.Equal(expected, LevelUpSuggestion.ShouldSuggest(c, levelReachedOn: new DateOnly(2026, 1, 1), null, Today, false));
    }

    [Fact]
    public void Needs_the_whole_window_at_the_current_level_and_respects_snooze_and_archive()
    {
        var c = LevelUpSuggestion.Measure(FrequencyType.Daily, [], null, Days(Today.AddDays(-13), Today), Today);
        var old = new DateOnly(2026, 1, 1);
        Assert.True(LevelUpSuggestion.ShouldSuggest(c, c.From, null, Today, false));
        Assert.False(LevelUpSuggestion.ShouldSuggest(c, c.From.AddDays(1), null, Today, false));
        Assert.False(LevelUpSuggestion.ShouldSuggest(c, old, snoozedUntil: Today.AddDays(1), Today, false));
        Assert.True(LevelUpSuggestion.ShouldSuggest(c, old, snoozedUntil: Today, Today, false));
        Assert.False(LevelUpSuggestion.ShouldSuggest(c, old, null, Today, archived: true));
    }
}

public class WeeklyReviewRulesTests
{
    [Theory]
    [InlineData("2026-03-15", "2026-03-09")] // Sunday → current week
    [InlineData("2026-03-16", "2026-03-09")] // Monday → previous week
    [InlineData("2026-03-21", "2026-03-09")] // Saturday → previous week
    [InlineData("2026-03-22", "2026-03-16")]
    public void Reviewable_week(string today, string weekStart) =>
        Assert.Equal(DateOnly.Parse(weekStart), WeeklyReviewRules.ReviewableWeekStart(DateOnly.Parse(today)));
}

public class ReminderTimingTests
{
    [Theory]
    [InlineData("08:00", "08:00", true)]
    [InlineData("08:00", "09:59", true)]
    [InlineData("08:00", "10:01", false)] // too late: skipped, not sent late
    [InlineData("08:00", "07:59", false)]
    public void Fires_from_the_time_up_to_two_hours_later(string at, string now, bool expected) =>
        Assert.Equal(expected, ReminderDispatcher.IsTime(TimeOnly.Parse(at), TimeOnly.Parse(now)));
}

public class WebPushCryptoTests
{
    /// <summary>Decrypts like a browser would (RFC 8291 as the user agent).</summary>
    private static byte[] Decrypt(byte[] body, ECDiffieHellman uaKey, byte[] authSecret)
    {
        var salt = body[..16];
        Assert.Equal((uint)WebPushCrypto.RecordSize, BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4)));
        var idLength = body[20];
        var asPublic = body[21..(21 + idLength)];
        var cipher = body[(21 + idLength)..];

        var uaPublic = WebPushCrypto.ExportUncompressed(uaKey.ExportParameters(false));
        using var asKey = WebPushCrypto.ImportPublicKey(asPublic);
        var (cek, nonce) = WebPushCrypto.DeriveKeys(uaKey.DeriveRawSecretAgreement(asKey.PublicKey), authSecret,
            uaPublic, asPublic, salt);

        var plaintext = new byte[cipher.Length - 16];
        using var aes = new AesGcm(cek, 16);
        aes.Decrypt(nonce, cipher[..^16], cipher[^16..], plaintext);
        Assert.Equal(0x02, plaintext[^1]);
        return plaintext[..^1];
    }

    [Fact]
    public void Encrypted_payload_round_trips()
    {
        using var ua = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var uaPublic = WebPushCrypto.ExportUncompressed(ua.ExportParameters(false));
        var auth = RandomNumberGenerator.GetBytes(16);
        var payload = Encoding.UTF8.GetBytes("""{"title":"Correr","body":"Hoje: 20 min"}""");

        var body = WebPushCrypto.Encrypt(payload, uaPublic, auth);

        Assert.Equal(payload, Decrypt(body, ua, auth));
    }

    [Fact]
    public void Matches_rfc8291_appendix_a_example()
    {
        static byte[] B(string s) => Base64Url.DecodeFromChars(s);
        var asPublic = B("BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8");
        using var asKey = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = B("yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw"),
            Q = new ECPoint { X = asPublic[1..33], Y = asPublic[33..] },
        });

        var body = WebPushCrypto.Encrypt(
            Encoding.UTF8.GetBytes("When I grow up, I want to be a watermelon"),
            B("BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4"),
            B("BTBZMqHH6r4Tts7J_aSIgg"),
            asKey,
            B("DGv6ra1nlYgDCS1FRnbzlw"));

        Assert.Equal(
            "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPTpK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN",
            Base64Url.EncodeToString(body));
    }

    [Fact]
    public void Rejects_malformed_keys()
    {
        Assert.False(WebPushCrypto.IsValidPublicKey(new byte[65]));
        Assert.False(WebPushCrypto.IsValidPublicKey(new byte[33]));
        Assert.Throws<ArgumentException>(() => WebPushCrypto.Encrypt([1], new byte[65], new byte[16]));
    }

    [Fact]
    public void Vapid_header_is_a_valid_es256_jwt()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = WebPushCrypto.ExportUncompressed(key.ExportParameters(false));
        var now = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

        var header = WebPushCrypto.VapidAuthorization("https://fcm.googleapis.com/fcm/send/abc", "mailto:me@example.com",
            key, publicKey, now);

        Assert.StartsWith("vapid t=", header);
        var parts = header["vapid t=".Length..].Split(", k=");
        Assert.Equal(Base64Url.EncodeToString(publicKey), parts[1]);
        var jwt = parts[0].Split('.');
        var claims = JsonNode.Parse(Base64Url.DecodeFromChars(jwt[1]))!;
        Assert.Equal("https://fcm.googleapis.com", (string?)claims["aud"]);
        Assert.Equal("mailto:me@example.com", (string?)claims["sub"]);
        Assert.Equal(now.AddHours(12).ToUnixTimeSeconds(), (long)claims["exp"]!);
        Assert.True(key.VerifyData(Encoding.ASCII.GetBytes($"{jwt[0]}.{jwt[1]}"), Base64Url.DecodeFromChars(jwt[2]),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }
}
