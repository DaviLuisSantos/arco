using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Arco.Api.Features.Push;
using Arco.Api.Infrastructure.Push;
using Microsoft.Extensions.DependencyInjection;

namespace Arco.Api.Tests;

/// <summary>The reminder job runs once for every user with a push subscription, in that user's own timezone.</summary>
public sealed class MultiUserReminderTests : IDisposable
{
    // Monday 2030-03-04, 15:00 UTC = 12:00 in São Paulo = 00:00 (next day) in Tokyo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private Task<int> DispatchAsync() =>
        ReminderDispatcher.RunForAllUsersAsync(_factory.Services.GetRequiredService<IServiceScopeFactory>(), null, CancellationToken.None);

    private static object Subscription(string endpoint)
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return new
        {
            endpoint,
            keys = new
            {
                p256dh = Base64Url.EncodeToString(WebPushCrypto.ExportUncompressed(key.ExportParameters(false))),
                auth = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)),
            },
        };
    }

    private static async Task<int> CreateHabitAsync(HttpClient client, string name, string reminderTime)
    {
        var res = await client.PostAsJsonAsync("/api/habits", new
        {
            name, frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min", reminderTime,
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private async Task<HttpClient> UserAsync(string timezone, string endpoint, string habit)
    {
        var client = await _factory.LoggedInClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/settings", new { timezone })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/push/subscriptions", Subscription(endpoint))).StatusCode);
        await CreateHabitAsync(client, habit, "12:00");
        return client;
    }

    [Fact]
    public async Task Each_user_gets_their_reminders_at_their_local_time_on_their_own_devices()
    {
        await UserAsync("America/Sao_Paulo", "https://push.example.com/sao-paulo", "Meditar");
        await UserAsync("Asia/Tokyo", "https://push.example.com/tokyo", "Correr");

        // 12:00 in São Paulo, midnight in Tokyo: only the first user's habit is due.
        Assert.Equal(1, await DispatchAsync());
        var first = Assert.Single(_factory.Push.Sent);
        Assert.Equal("https://push.example.com/sao-paulo", first.Endpoint);
        Assert.Contains("Meditar", (string?)first.Payload["title"]);
        Assert.Equal(0, await DispatchAsync()); // claimed: not sent twice

        // 12:00 the next day in Tokyo, midnight in São Paulo: now the second user's.
        _factory.Time!.Advance(TimeSpan.FromHours(12));
        Assert.Equal(1, await DispatchAsync());
        var second = _factory.Push.Sent.Last();
        Assert.Equal("https://push.example.com/tokyo", second.Endpoint);
        Assert.Contains("Correr", (string?)second.Payload["title"]);
        Assert.Equal(2, _factory.Push.Sent.Count);
    }

    [Fact]
    public async Task A_user_who_already_checked_in_does_not_stop_anyone_elses_reminder()
    {
        var a = await UserAsync("America/Sao_Paulo", "https://push.example.com/a", "Meditar");
        await UserAsync("America/Sao_Paulo", "https://push.example.com/b", "Meditar");
        var habitA = (int)(await a.GetJsonAsync("/api/habits")).AsArray().Single()!["id"]!;
        await a.PutAsJsonAsync("/api/checkins", new { habitId = habitA, date = "2030-03-04", status = "Done" });

        Assert.Equal(1, await DispatchAsync());
        Assert.Equal("https://push.example.com/b", Assert.Single(_factory.Push.Sent).Endpoint);
    }

    [Fact]
    public async Task Users_without_subscriptions_are_skipped()
    {
        var quiet = await _factory.LoggedInClientAsync();
        await CreateHabitAsync(quiet, "Sem push", "12:00");
        await UserAsync("America/Sao_Paulo", "https://push.example.com/with-push", "Com push");

        Assert.Equal(1, await DispatchAsync());
        Assert.Equal("https://push.example.com/with-push", Assert.Single(_factory.Push.Sent).Endpoint);
    }
}
