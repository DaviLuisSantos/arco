using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using System.Text.RegularExpressions;
using Arco.Api.Domain;
using Arco.Api.Infrastructure.Email;
using Arco.Api.Infrastructure.Push;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Arco.Api.Tests;

/// <summary>
/// Runs the real app (Development env) against a temp SQLite file. Users sign up through the API
/// (<see cref="LoggedInClientAsync"/>); e-mails go to <see cref="Email"/> instead of the network.
/// With <paramref name="now"/>, the app runs on a <see cref="MutableTime"/> clock that tests can advance.
/// Push messages go to <see cref="Push"/> instead of the network; the reminder loop is off.
/// </summary>
public sealed class ApiFactory(DateTimeOffset? now = null) : WebApplicationFactory<Program>
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arco-tests", Guid.NewGuid().ToString("N"));

    public MutableTime? Time { get; } = now is { } n ? new MutableTime(n) : null;
    public FakePushSender Push { get; } = new();
    public FakeEmailSender Email { get; } = new();

    public const string DefaultPassword = "senha-segura-1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Arco", $"Data Source={Path.Combine(_dir, "arco.db")}");
        builder.UseSetting("Push:SchedulerEnabled", "false");
        builder.UseSetting("Backup:Enabled", "false");
        builder.UseSetting("Demo:CleanupEnabled", "false");
        builder.UseSetting("Outbox:WorkerEnabled", "false");
        builder.UseSetting("Auth:ActionsPerMinute", "1000");
        builder.ConfigureTestServices(services =>
        {
            if (Time is not null) services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Time));
            services.RemoveAll<IPushSender>();
            services.AddSingleton<IPushSender>(Push);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
            // The MCP tools call the API over HTTP: route those calls into the test server.
            services.AddHttpClient(Arco.Api.Features.Mcp.ArcoApiClient.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => Server.CreateHandler());
        });
    }

    private int _firstUserId;

    /// <summary>
    /// Runs an action with a scoped service (e.g. a DbContext), acting as the first user registered through
    /// <see cref="RegisterAsync"/> (like a request would); use <see cref="AsUserAsync"/> to pick another.
    /// </summary>
    public async Task<T> WithScopeAsync<TService, T>(Func<TService, Task<T>> action) where TService : notnull
    {
        using var scope = Services.CreateScope();
        if (_firstUserId > 0) scope.ServiceProvider.GetRequiredService<Arco.Api.Infrastructure.CurrentUser>().Impersonate(_firstUserId);
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    /// <summary>A new client, signed up (and so logged in) as a unique user; pass an e-mail to choose it.</summary>
    public async Task<HttpClient> LoggedInClientAsync(string? email = null)
    {
        var client = CreateClient();
        await RegisterAsync(client, email);
        return client;
    }

    public async Task<string> RegisterAsync(HttpClient client, string? email = null, string password = DefaultPassword)
    {
        email ??= $"user-{Guid.NewGuid():N}@teste.arco";
        var res = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.Equal(System.Net.HttpStatusCode.Created, res.StatusCode);
        if (_firstUserId == 0) _firstUserId = await UserIdAsync(email);
        return email;
    }

    /// <summary>Runs an action with a scope acting as the given user (like a background job).</summary>
    public async Task<T> AsUserAsync<T>(int userId, Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<Arco.Api.Infrastructure.CurrentUser>().Impersonate(userId);
        return await action(scope.ServiceProvider);
    }

    public async Task<int> UserIdAsync(string email)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Arco.Api.Infrastructure.AppDbContext>();
        var lower = email.ToLowerInvariant();
        return await db.Users.Where(u => u.Email == lower).Select(u => u.Id).SingleAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

public sealed class FakeEmailSender : IEmailSender
{
    public sealed record Mail(string To, string Subject, string Html)
    {
        /// <summary>The token of the link in the e-mail (<c>?token=...</c>).</summary>
        public string Token => Regex.Match(Html, "token=([A-Za-z0-9_-]+)").Groups[1].Value;
        public string Link => System.Net.WebUtility.HtmlDecode(Regex.Match(Html, "href=\"([^\"]+)\"").Groups[1].Value);
    }

    private readonly List<Mail> _sent = [];

    public IReadOnlyList<Mail> Sent { get { lock (_sent) return [.. _sent]; } }

    public Mail Last(string to) => Sent.Last(m => m.To == to.ToLowerInvariant());

    private int _failures;
    private readonly List<string?> _keys = [];

    /// <summary>Makes the next <paramref name="count"/> sends throw (use int.MaxValue for "always").</summary>
    public void FailNext(int count) => Interlocked.Exchange(ref _failures, count);

    /// <summary>Idempotency keys of the sends the provider accepted.</summary>
    public IReadOnlyList<string?> Keys { get { lock (_sent) return [.. _keys]; } }

    /// <summary>Delay applied before accepting a send (to test concurrent claims).</summary>
    public TimeSpan Delay { get; set; }

    public async Task SendAsync(string to, string subject, string html, CancellationToken ct, string? idempotencyKey = null)
    {
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
        if (Volatile.Read(ref _failures) > 0)
        {
            Interlocked.Decrement(ref _failures);
            throw new EmailSendException("Falha simulada do provedor.");
        }
        lock (_sent)
        {
            _sent.Add(new Mail(to, subject, html));
            _keys.Add(idempotencyKey);
        }
    }
}

public static class HttpJson
{
    public static async Task<JsonNode> JsonAsync(this HttpResponseMessage res) =>
        JsonNode.Parse(await res.Content.ReadAsStringAsync())!;

    public static async Task<JsonNode> GetJsonAsync(this HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        res.EnsureSuccessStatusCode();
        return await res.JsonAsync();
    }
}

public sealed class MutableTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
    public void AdvanceDays(int days) => Now = Now.AddDays(days);
}

public sealed class FakePushSender : IPushSender
{
    public List<(string Endpoint, JsonNode Payload)> Sent { get; } = [];
    public PushOutcome Outcome { get; set; } = PushOutcome.Sent;

    public Task<PushOutcome> SendAsync(PushSubscription subscription, string payloadJson, CancellationToken ct)
    {
        lock (Sent) Sent.Add((subscription.Endpoint, JsonNode.Parse(payloadJson)!));
        return Task.FromResult(Outcome);
    }
}
