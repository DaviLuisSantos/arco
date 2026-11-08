using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Arco.Api.Features.Demo;
using Arco.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arco.Api.Tests;

public sealed class AuthApiTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));
    private const string Password = ApiFactory.DefaultPassword;

    public void Dispose() => _factory.Dispose();

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private async Task VerifyAsync(HttpClient client, string email)
    {
        var res = await client.PostAsJsonAsync("/api/auth/verify-email", new { token = _factory.Email.Last(email).Token });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    private static async Task<HttpResponseMessage> DeleteAccountAsync(HttpClient client, object? body)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account") { Content = JsonContent.Create(body) };
        return await client.SendAsync(request);
    }

    private static async Task<string> CreateKeyAsync(HttpClient app)
    {
        var res = await app.PostAsJsonAsync("/api/api-keys", new { name = "teste" });
        return (string)(await res.JsonAsync())["key"]!;
    }

    [Fact]
    public async Task Register_signs_in_sends_the_verification_mail_and_rejects_duplicates_and_bad_input()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { email = "  Ana@Exemplo.com ", password = Password, displayName = "Ana" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Contains(res.Headers.GetValues("Set-Cookie"), c => c.StartsWith("arco_auth=") && c.Contains("httponly"));
        var me = await res.JsonAsync();
        Assert.Equal("ana@exemplo.com", (string?)me["email"]);
        Assert.Equal("Ana", (string?)me["displayName"]);
        Assert.False((bool)me["emailVerified"]!);
        Assert.False((bool)me["isDemo"]!);
        Assert.True((bool)me["hasPassword"]!);
        Assert.False((bool)me["hasGoogle"]!);

        // Logged in right away, and the verification link was mailed.
        Assert.Equal("ana@exemplo.com", (string?)(await client.GetJsonAsync("/api/auth/me"))["email"]);
        var mail = _factory.Email.Last("ana@exemplo.com");
        Assert.NotEmpty(mail.Token);
        Assert.Contains("/verify-email?token=", mail.Link);

        var other = _factory.CreateClient();
        var dup = await other.PostAsJsonAsync("/api/auth/register", new { email = "ANA@exemplo.com", password = Password });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("email_taken", (string?)(await dup.JsonAsync())["code"]);

        var shortPassword = await other.PostAsJsonAsync("/api/auth/register", new { email = "b@exemplo.com", password = "1234567" });
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);
        Assert.Equal("validation", (string?)(await shortPassword.JsonAsync())["code"]);
        var badEmail = await other.PostAsJsonAsync("/api/auth/register", new { email = "nao-e-email", password = Password });
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_works_with_the_email_in_any_case_and_fails_the_same_way_for_unknown_users()
    {
        var email = await _factory.RegisterAsync(_factory.CreateClient(), "Carla@Exemplo.com");

        var client = _factory.CreateClient();
        var ok = await LoginAsync(client, email.ToUpperInvariant(), Password);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal("carla@exemplo.com", (string?)(await client.GetJsonAsync("/api/auth/me"))["email"]);

        var wrong = await LoginAsync(_factory.CreateClient(), email, "outra-senha-qualquer");
        var unknown = await LoginAsync(_factory.CreateClient(), "ninguem@exemplo.com", Password);
        foreach (var res in new[] { wrong, unknown })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
            Assert.Equal("invalid_credentials", (string?)(await res.JsonAsync())["code"]);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await LoginAsync(_factory.CreateClient(), "", "")).StatusCode);
    }

    [Fact]
    public async Task Providers_reports_google_off_when_it_is_not_configured()
    {
        var res = await _factory.CreateClient().GetJsonAsync("/api/auth/providers");
        Assert.False((bool)res["google"]!);
    }

    [Fact]
    public async Task Verify_email_marks_the_account_once_and_rejects_bad_tokens()
    {
        var client = _factory.CreateClient();
        var email = await _factory.RegisterAsync(client);
        Assert.False((bool)(await client.GetJsonAsync("/api/auth/me"))["emailVerified"]!);

        // Works with or without a session.
        var anonymous = _factory.CreateClient();
        await VerifyAsync(anonymous, email);
        Assert.True((bool)(await client.GetJsonAsync("/api/auth/me"))["emailVerified"]!);

        var reuse = await anonymous.PostAsJsonAsync("/api/auth/verify-email", new { token = _factory.Email.Last(email).Token });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        Assert.Equal("invalid_token", (string?)(await reuse.JsonAsync())["code"]);

        foreach (var token in new[] { "lixo", "", null })
        {
            var bad = await anonymous.PostAsJsonAsync("/api/auth/verify-email", new { token });
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            Assert.Equal("invalid_token", (string?)(await bad.JsonAsync())["code"]);
        }
    }

    [Fact]
    public async Task Verification_token_expires_after_48_hours()
    {
        var email = await _factory.RegisterAsync(_factory.CreateClient());
        var token = _factory.Email.Last(email).Token;

        _factory.Time!.Advance(TimeSpan.FromHours(49));
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_token", (string?)(await res.JsonAsync())["code"]);
    }

    [Fact]
    public async Task Resend_verification_mails_a_new_link_only_while_unverified()
    {
        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/auth/resend-verification", null)).StatusCode);

        var client = _factory.CreateClient();
        var email = await _factory.RegisterAsync(client);
        var before = _factory.Email.Sent.Count;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/resend-verification", null)).StatusCode);
        Assert.Equal(before + 1, _factory.Email.Sent.Count);

        await VerifyAsync(client, email);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/resend-verification", null)).StatusCode);
        Assert.Equal(before + 1, _factory.Email.Sent.Count); // verified: nothing sent
    }

    [Fact]
    public async Task Forgot_password_only_mails_verified_accounts_and_always_answers_204()
    {
        var anonymous = _factory.CreateClient();
        var unverified = await _factory.RegisterAsync(_factory.CreateClient());
        var verified = await _factory.RegisterAsync(_factory.CreateClient());
        await VerifyAsync(anonymous, verified);

        var before = _factory.Email.Sent.Count;
        foreach (var email in new[] { "ninguem@exemplo.com", unverified, "invalido" })
            Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email })).StatusCode);
        Assert.Equal(before, _factory.Email.Sent.Count);

        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email = verified })).StatusCode);
        var mail = Assert.Single(_factory.Email.Sent.Skip(before));
        Assert.Equal(verified, mail.To);
        Assert.Contains("/reset-password?token=", mail.Link);
    }

    [Fact]
    public async Task Reset_password_sets_the_new_password_ends_old_sessions_and_burns_the_token()
    {
        var first = _factory.CreateClient();
        var email = await _factory.RegisterAsync(first);
        await VerifyAsync(first, email);
        var second = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(second, email, Password)).StatusCode);

        await first.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = _factory.Email.Last(email).Token;

        var anonymous = _factory.CreateClient();
        var weak = await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "curta" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        var bad = await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { token = "lixo", password = "nova-senha-123" });
        Assert.Equal("invalid_token", (string?)(await bad.JsonAsync())["code"]);

        var reset = await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "nova-senha-123" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        // The security stamp changed: every old cookie stops working.
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/habits")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(_factory.CreateClient(), email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(_factory.CreateClient(), email, "nova-senha-123")).StatusCode);

        var reuse = await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "outra-senha-123" });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        Assert.Equal("invalid_token", (string?)(await reuse.JsonAsync())["code"]);
    }

    [Fact]
    public async Task Reset_token_expires_after_one_hour()
    {
        var client = _factory.CreateClient();
        var email = await _factory.RegisterAsync(client);
        await VerifyAsync(client, email);
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = _factory.Email.Last(email).Token;

        _factory.Time!.Advance(TimeSpan.FromMinutes(61));
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password", new { token, password = "nova-senha-123" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_token", (string?)(await res.JsonAsync())["code"]);
    }

    [Fact]
    public async Task Change_password_checks_the_current_one_and_keeps_only_this_session()
    {
        var email = $"dana-{Guid.NewGuid():N}@exemplo.com";
        var here = _factory.CreateClient();
        await _factory.RegisterAsync(here, email);
        var elsewhere = _factory.CreateClient();
        await LoginAsync(elsewhere, email, Password);

        var wrong = await here.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "errada-errada", newPassword = "nova-senha-123" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("invalid_credentials", (string?)(await wrong.JsonAsync())["code"]);
        var weak = await here.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = Password, newPassword = "curta" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var ok = await here.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = Password, newPassword = "nova-senha-123" });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await here.GetAsync("/api/auth/me")).StatusCode); // re-issued cookie
        Assert.Equal(HttpStatusCode.Unauthorized, (await elsewhere.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(_factory.CreateClient(), email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await LoginAsync(_factory.CreateClient(), email, "nova-senha-123")).StatusCode);

        var anonymous = await _factory.CreateClient().PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = Password, newPassword = "nova-senha-123" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Delete_account_needs_the_password_and_removes_the_user_with_all_their_data()
    {
        var client = _factory.CreateClient();
        var email = await _factory.RegisterAsync(client);
        var other = await _factory.LoggedInClientAsync();
        await client.PostAsJsonAsync("/api/habits",
            new { name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        await other.PostAsJsonAsync("/api/habits",
            new { name = "Correr", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        var userId = await _factory.UserIdAsync(email);

        Assert.Equal(HttpStatusCode.Unauthorized, (await DeleteAccountAsync(client, new { password = "errada-errada" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await DeleteAccountAsync(client, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(client, new { password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(_factory.CreateClient(), email, Password)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Id == userId));
        Assert.Equal(0, await db.Habits.IgnoreQueryFilters().CountAsync(h => h.UserId == userId));
        Assert.Equal(0, await db.Settings.IgnoreQueryFilters().CountAsync(s => s.UserId == userId));
        Assert.Equal(1, await db.Habits.IgnoreQueryFilters().CountAsync()); // the other user's habit is untouched

        // The e-mail is free again.
        await _factory.RegisterAsync(_factory.CreateClient(), email);
    }

    [Fact]
    public async Task Demo_creates_a_seeded_throwaway_account_that_cannot_set_a_password()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/auth/demo", null);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var me = await res.JsonAsync();
        Assert.True((bool)me["isDemo"]!);
        Assert.True((bool)me["emailVerified"]!);
        Assert.False((bool)me["hasPassword"]!);

        Assert.NotEmpty((await client.GetJsonAsync("/api/habits")).AsArray());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/cycles/current")).StatusCode);
        Assert.True((int)(await client.GetJsonAsync("/api/player"))["totalXp"]! > 0);

        var password = await client.PostAsJsonAsync("/api/auth/change-password", new { newPassword = "senha-nova-123" });
        Assert.Equal(HttpStatusCode.Forbidden, password.StatusCode);
        Assert.Equal("forbidden", (string?)(await password.JsonAsync())["code"]);

        // No mail is sent for a demo, and forgot-password ignores it.
        Assert.Empty(_factory.Email.Sent);
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = (string?)me["email"] });
        Assert.Empty(_factory.Email.Sent);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
    }

    [Fact]
    public async Task Each_demo_visit_gets_its_own_data()
    {
        var a = _factory.CreateClient();
        var b = _factory.CreateClient();
        await a.PostAsync("/api/auth/demo", null);
        await b.PostAsync("/api/auth/demo", null);

        var habitsA = (await a.GetJsonAsync("/api/habits")).AsArray().Select(h => (int)h!["id"]!).ToHashSet();
        var habitsB = (await b.GetJsonAsync("/api/habits")).AsArray().Select(h => (int)h!["id"]!).ToHashSet();
        Assert.NotEmpty(habitsA);
        Assert.Empty(habitsA.Intersect(habitsB));
    }

    [Fact]
    public async Task Demo_cleanup_removes_expired_demos_with_their_data_and_keeps_real_accounts()
    {
        var demo = _factory.CreateClient();
        await demo.PostAsync("/api/auth/demo", null);
        var real = await _factory.LoggedInClientAsync();
        await real.PostAsJsonAsync("/api/habits",
            new { name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });

        var cleanup = new DemoCleanupService(_factory.Services.GetRequiredService<IServiceScopeFactory>(), _factory.Time!,
            NullLogger<DemoCleanupService>.Instance);

        _factory.Time!.Advance(TimeSpan.FromHours(23));
        Assert.Equal(0, await cleanup.CleanAsync(CancellationToken.None));
        Assert.Equal(HttpStatusCode.OK, (await demo.GetAsync("/api/auth/me")).StatusCode);

        _factory.Time!.Advance(TimeSpan.FromHours(2));
        Assert.Equal(1, await cleanup.CleanAsync(CancellationToken.None));
        Assert.Equal(HttpStatusCode.Unauthorized, (await demo.GetAsync("/api/auth/me")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var realId = await db.Users.Select(u => u.Id).SingleAsync(); // only the real user is left
        Assert.Equal(1, await db.Habits.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.CheckIns.IgnoreQueryFilters().CountAsync(c => c.UserId != realId));
        Assert.Equal(0, await db.XpEvents.IgnoreQueryFilters().CountAsync(x => x.UserId != realId));
        Assert.Equal(0, await db.Cycles.IgnoreQueryFilters().CountAsync(c => c.UserId != realId));
        Assert.Equal(HttpStatusCode.OK, (await real.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task An_api_key_cannot_change_the_password_or_delete_the_account()
    {
        var app = await _factory.LoggedInClientAsync();
        var key = await CreateKeyAsync(app);
        var api = _factory.CreateClient(new() { HandleCookies = false });
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);

        var change = await api.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = Password, newPassword = "nova-senha-123" });
        Assert.Equal(HttpStatusCode.Forbidden, change.StatusCode);
        Assert.Equal("forbidden", (string?)(await change.JsonAsync())["code"]);

        var delete = await DeleteAccountAsync(api, new { password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);

        // Nothing happened: the key and the cookie session both still work.
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Auth_routes_that_need_a_session_answer_401_in_json()
    {
        var anonymous = _factory.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await DeleteAccountAsync(anonymous, new { password = Password })).StatusCode);
        var res = await anonymous.PostAsJsonAsync("/api/auth/change-password", new { newPassword = "nova-senha-123" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("unauthorized", (string?)(await res.JsonAsync())["code"]);
    }
}
