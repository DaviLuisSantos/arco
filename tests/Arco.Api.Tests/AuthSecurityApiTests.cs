using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Arco.Api.Features.Auth;
using Microsoft.AspNetCore.Hosting;

namespace Arco.Api.Tests;

/// <summary>Sessions, two-step verification, the extended <c>Me</c>, language and onboarding.</summary>
public sealed class AuthSecurityApiTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo (future date, so the login cookie stays valid).
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));
    private const string Password = ApiFactory.DefaultPassword;

    public void Dispose() => _factory.Dispose();

    private string CodeNow(string secret) => Totp.Compute(secret, _factory.Time!.Now);

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

    /// <summary>Registers a user and turns on 2FA; returns the e-mail, secret and recovery codes.</summary>
    private async Task<(string Email, string Secret, string[] RecoveryCodes)> UserWithTwoFactorAsync()
    {
        var client = _factory.CreateClient();
        var email = await _factory.RegisterAsync(client);

        var setup = await client.PostAsync("/api/auth/2fa/setup", null);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var body = await setup.JsonAsync();
        var secret = (string)body["secret"]!;
        Assert.StartsWith("otpauth://totp/Arco:", (string)body["otpauthUri"]!);
        Assert.Contains($"secret={secret}", (string)body["otpauthUri"]!);

        var wrong = await client.PostAsJsonAsync("/api/auth/2fa/enable", new { code = "000000" == CodeNow(secret) ? "111111" : "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal("invalid_two_factor_code", (string?)(await wrong.JsonAsync())["code"]);

        var enable = await client.PostAsJsonAsync("/api/auth/2fa/enable", new { code = CodeNow(secret) });
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        var codes = (await enable.JsonAsync())["recoveryCodes"]!.AsArray().Select(c => (string)c!).ToArray();
        Assert.Equal(10, codes.Length);
        Assert.Equal(10, codes.Distinct().Count());
        Assert.True((bool)(await client.GetJsonAsync("/api/auth/me"))["twoFactorEnabled"]!);
        return (email, secret, codes);
    }

    private static async Task<string> TicketAsync(HttpClient client, string email)
    {
        var res = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.JsonAsync();
        Assert.True((bool)body["twoFactorRequired"]!);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        return (string)body["ticket"]!;
    }

    [Fact]
    public async Task Login_with_two_factor_needs_the_totp_code()
    {
        var (email, secret, _) = await UserWithTwoFactorAsync();

        var client = _factory.CreateClient();
        var ticket = await TicketAsync(client, email);

        var bad = await client.PostAsJsonAsync("/api/auth/login/2fa", new { ticket, code = "12345" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal("invalid_two_factor_code", (string?)(await bad.JsonAsync())["code"]);

        var ok = await client.PostAsJsonAsync("/api/auth/login/2fa", new { ticket, code = CodeNow(secret) });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(email, (string?)(await client.GetJsonAsync("/api/auth/me"))["email"]);

        // A forged ticket is refused.
        var forged = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login/2fa", new { ticket = "abc", code = CodeNow(secret) });
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
    }

    [Fact]
    public async Task Recovery_codes_work_once()
    {
        var (email, _, codes) = await UserWithTwoFactorAsync();

        var client = _factory.CreateClient();
        var ticket = await TicketAsync(client, email);
        var ok = await client.PostAsJsonAsync("/api/auth/login/2fa", new { ticket, code = codes[0].ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        var other = _factory.CreateClient();
        var ticket2 = await TicketAsync(other, email);
        var reused = await other.PostAsJsonAsync("/api/auth/login/2fa", new { ticket = ticket2, code = codes[0] });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        var next = await other.PostAsJsonAsync("/api/auth/login/2fa", new { ticket = ticket2, code = codes[1] });
        Assert.Equal(HttpStatusCode.NoContent, next.StatusCode);
    }

    [Fact]
    public async Task Expired_ticket_is_refused_and_disable_turns_two_factor_off()
    {
        var (email, secret, _) = await UserWithTwoFactorAsync();

        var client = _factory.CreateClient();
        var ticket = await TicketAsync(client, email);
        _factory.Time!.Advance(TimeSpan.FromMinutes(6));
        var expired = await client.PostAsJsonAsync("/api/auth/login/2fa", new { ticket, code = CodeNow(secret) });
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal("invalid_two_factor_code", (string?)(await expired.JsonAsync())["code"]);

        ticket = await TicketAsync(client, email);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/auth/login/2fa", new { ticket, code = CodeNow(secret) })).StatusCode);

        var disable = await client.PostAsJsonAsync("/api/auth/2fa/disable", new { code = CodeNow(secret) });
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);
        Assert.False((bool)(await client.GetJsonAsync("/api/auth/me"))["twoFactorEnabled"]!);

        // Back to a plain login.
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(_factory.CreateClient(), email)).StatusCode);
    }

    [Fact]
    public async Task Revoking_a_session_drops_its_cookie()
    {
        var a = _factory.CreateClient();
        var email = await _factory.RegisterAsync(a);
        var b = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(b, email)).StatusCode);

        var sessions = (await b.GetJsonAsync("/api/auth/sessions")).AsArray();
        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions, s => (bool)s!["current"]!);
        var aId = (string)sessions.Single(s => !(bool)s!["current"]!)!["id"]!;

        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/api/today")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.DeleteAsync($"/api/auth/sessions/{aId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.GetAsync("/api/today")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/api/today")).StatusCode);
        Assert.Single((await b.GetJsonAsync("/api/auth/sessions")).AsArray());

        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/auth/sessions/{Guid.NewGuid()}")).StatusCode);

        // Another user's session cannot be revoked.
        var stranger = await _factory.LoggedInClientAsync();
        var strangerId = (string)(await stranger.GetJsonAsync("/api/auth/sessions")).AsArray()[0]!["id"]!;
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/auth/sessions/{strangerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await stranger.GetAsync("/api/today")).StatusCode);

        // Logout revokes the current session.
        Assert.Equal(HttpStatusCode.NoContent, (await b.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Revoke_others_keeps_the_current_session()
    {
        var a = _factory.CreateClient();
        var email = await _factory.RegisterAsync(a);
        var b = _factory.CreateClient();
        var c = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(b, email)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(c, email)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await b.PostAsync("/api/auth/sessions/revoke-others", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        var left = (await b.GetJsonAsync("/api/auth/sessions")).AsArray();
        Assert.True((bool)Assert.Single(left)!["current"]!);
    }

    [Fact]
    public async Task Me_has_the_new_fields_and_locale_and_onboarding_update_them()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", new { email = "nova@exemplo.com", password = Password });
        var me = await res.JsonAsync();
        Assert.Equal("free", (string?)me["plan"]);
        Assert.Equal("pt-BR", (string?)me["locale"]);
        Assert.False((bool)me["onboarded"]!);
        Assert.False((bool)me["twoFactorEnabled"]!);
        Assert.False((bool)me["isAdmin"]!);
        var profile = me["publicProfile"]!;
        Assert.False((bool)profile["enabled"]!);
        Assert.Null(profile["slug"]);
        Assert.True((bool)profile["showHabits"]!);
        Assert.Equal("indigo", (string?)profile["theme"]);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/auth/locale", new { locale = "en" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/auth/locale", new { locale = "fr" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/onboarding/complete", null)).StatusCode);
        me = await client.GetJsonAsync("/api/auth/me");
        Assert.Equal("en", (string?)me["locale"]);
        Assert.True((bool)me["onboarded"]!);

        // Cookie-only routes refuse API keys.
        var key = (string)(await (await client.PostAsJsonAsync("/api/api-keys", new { name = "teste" })).JsonAsync())["key"]!;
        var bot = _factory.CreateClient();
        bot.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        Assert.Equal(HttpStatusCode.OK, (await bot.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bot.PutAsJsonAsync("/api/auth/locale", new { locale = "pt-BR" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bot.GetAsync("/api/auth/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bot.PostAsync("/api/auth/2fa/setup", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bot.PostAsync("/api/auth/onboarding/complete", null)).StatusCode);

        // Demo accounts are Pro and skip onboarding.
        var demo = await (await _factory.CreateClient().PostAsync("/api/auth/demo", null)).JsonAsync();
        Assert.Equal("pro", (string?)demo["plan"]);
        Assert.True((bool)demo["onboarded"]!);
    }

    [Fact]
    public async Task Admins_come_from_configuration()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Admin:Emails", " chefe@exemplo.com , outra@exemplo.com"));
        var admin = factory.CreateClient();
        var res = await admin.PostAsJsonAsync("/api/auth/register", new { email = "Chefe@Exemplo.com", password = Password });
        Assert.True((bool)(await res.JsonAsync())["isAdmin"]!);
        Assert.True((bool)(await admin.GetJsonAsync("/api/auth/me"))["isAdmin"]!);

        var user = factory.CreateClient();
        var other = await user.PostAsJsonAsync("/api/auth/register", new { email = "comum@exemplo.com", password = Password });
        Assert.False((bool)(await other.JsonAsync())["isAdmin"]!);
    }

    [Fact]
    public async Task Emails_follow_the_account_language()
    {
        // Chosen at sign-up.
        var en = _factory.CreateClient();
        await en.PostAsJsonAsync("/api/auth/register", new { email = "john@example.com", password = Password, locale = "en" });
        var mail = _factory.Email.Last("john@example.com");
        Assert.Equal("Confirm your e-mail on Arco", mail.Subject);
        Assert.Contains("Confirm e-mail", mail.Html);
        Assert.Equal("en", (string?)(await en.GetJsonAsync("/api/auth/me"))["locale"]);

        // From the browser when not given.
        var browser = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = new StringContent("""{"email":"jane@example.com","password":"senha-segura-1"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9,pt;q=0.5");
        Assert.Equal(HttpStatusCode.Created, (await browser.SendAsync(request)).StatusCode);
        Assert.Equal("Confirm your e-mail on Arco", _factory.Email.Last("jane@example.com").Subject);

        // Default Portuguese; switching the language changes later e-mails.
        var pt = _factory.CreateClient();
        await pt.PostAsJsonAsync("/api/auth/register", new { email = "maria@exemplo.com", password = Password });
        var ptMail = _factory.Email.Last("maria@exemplo.com");
        Assert.Equal("Confirme seu e-mail no Arco", ptMail.Subject);
        Assert.Equal(HttpStatusCode.NoContent,
            (await pt.PostAsJsonAsync("/api/auth/verify-email", new { token = ptMail.Token })).StatusCode);
        await pt.PutAsJsonAsync("/api/auth/locale", new { locale = "en" });
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = "maria@exemplo.com" });
        var reset = _factory.Email.Last("maria@exemplo.com");
        Assert.Equal("Reset your Arco password", reset.Subject);
        Assert.Contains("/reset-password?token=", reset.Link);
    }

    [Fact]
    public void Totp_matches_the_rfc_6238_vector()
    {
        var secret = Totp.Base32Encode(Encoding.ASCII.GetBytes("12345678901234567890"));
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", secret);
        Assert.Equal("287082", Totp.Compute(secret, DateTimeOffset.FromUnixTimeSeconds(59)));
        Assert.Equal("081804", Totp.Compute(secret, DateTimeOffset.FromUnixTimeSeconds(1111111109)));
        // One step of drift either way is accepted; two are not.
        Assert.True(Totp.Verify(secret, "287082", DateTimeOffset.FromUnixTimeSeconds(59 + 30)));
        Assert.False(Totp.Verify(secret, "287082", DateTimeOffset.FromUnixTimeSeconds(59 + 61)));
    }
}
