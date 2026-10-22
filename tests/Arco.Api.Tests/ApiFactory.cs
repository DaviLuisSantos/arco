using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Arco.Api.Domain;
using Arco.Api.Infrastructure.Push;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Arco.Api.Tests;

/// <summary>
/// Runs the real app (Development env, password "arco") against a temp SQLite file.
/// With <paramref name="now"/>, the app runs on a <see cref="MutableTime"/> clock that tests can advance.
/// Push messages go to <see cref="Push"/> instead of the network; the reminder loop is off.
/// </summary>
public sealed class ApiFactory(DateTimeOffset? now = null) : WebApplicationFactory<Program>
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arco-tests", Guid.NewGuid().ToString("N"));

    public MutableTime? Time { get; } = now is { } n ? new MutableTime(n) : null;
    public FakePushSender Push { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Arco", $"Data Source={Path.Combine(_dir, "arco.db")}");
        builder.UseSetting("Push:SchedulerEnabled", "false");
        builder.ConfigureTestServices(services =>
        {
            if (Time is not null) services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Time));
            services.RemoveAll<IPushSender>();
            services.AddSingleton<IPushSender>(Push);
        });
    }

    /// <summary>Runs an action with a scoped service (e.g. the reminder dispatcher).</summary>
    public async Task<T> WithScopeAsync<TService, T>(Func<TService, Task<T>> action) where TService : notnull
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task<HttpClient> LoggedInClientAsync()
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { password = "arco" });
        res.EnsureSuccessStatusCode();
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
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
