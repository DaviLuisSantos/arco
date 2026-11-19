using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Arco.Api.Features.Push;
using Arco.Api.Infrastructure.Push;
using Microsoft.Extensions.DependencyInjection;

namespace Arco.Api.Tests;

/// <summary>Pauses on the Today screen, in the streak and in the push reminders.</summary>
public sealed class TodayPauseTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static async Task<int> CreateHabitAsync(HttpClient c, string name, string? reminderTime = null)
    {
        var res = await c.PostAsJsonAsync("/api/habits", new
        {
            name, frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min", reminderTime,
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static async Task PauseAsync(HttpClient c, int habitId, string start, string? end)
    {
        var res = await c.PostAsJsonAsync($"/api/habits/{habitId}/pauses", new { startDate = start, endDate = end });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    private static async Task<System.Text.Json.Nodes.JsonNode> TodayHabitAsync(HttpClient c, int habitId)
    {
        var today = await c.GetJsonAsync("/api/today");
        return today["habits"]!.AsArray().Single(h => (int)h!["id"]! == habitId)!;
    }

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

    private Task<int> DispatchAsync() =>
        ReminderDispatcher.RunForAllUsersAsync(_factory.Services.GetRequiredService<IServiceScopeFactory>(), null, CancellationToken.None);

    [Fact]
    public async Task A_habit_paused_today_is_flagged_not_due_and_listed_last()
    {
        var c = await _factory.LoggedInClientAsync();
        var paused = await CreateHabitAsync(c, "Aaa pausado");
        var normal = await CreateHabitAsync(c, "Zzz normal");
        await PauseAsync(c, paused, "2030-03-04", "2030-03-10");

        var a = await TodayHabitAsync(c, paused);
        Assert.True((bool)a["isPaused"]!);
        Assert.Equal("2030-03-10", (string?)a["pausedUntil"]);
        Assert.False((bool)a["isDueToday"]!);
        Assert.Single(a["pauses"]!.AsArray());

        var b = await TodayHabitAsync(c, normal);
        Assert.False((bool)b["isPaused"]!);
        Assert.Null(b["pausedUntil"]);
        Assert.True((bool)b["isDueToday"]!);

        var order = (await c.GetJsonAsync("/api/today"))["habits"]!.AsArray().Select(h => (int)h!["id"]!).ToArray();
        Assert.Equal([normal, paused], order);
    }

    [Fact]
    public async Task An_open_ended_pause_has_no_end_and_a_future_pause_does_not_pause_today()
    {
        var c = await _factory.LoggedInClientAsync();
        var open = await CreateHabitAsync(c, "Aberto");
        var future = await CreateHabitAsync(c, "Futuro");
        await PauseAsync(c, open, "2030-03-04", null);
        await PauseAsync(c, future, "2030-03-08", "2030-03-12");

        var a = await TodayHabitAsync(c, open);
        Assert.True((bool)a["isPaused"]!);
        Assert.Null(a["pausedUntil"]);

        var b = await TodayHabitAsync(c, future);
        Assert.False((bool)b["isPaused"]!);
        Assert.True((bool)b["isDueToday"]!);
        Assert.Single(b["pauses"]!.AsArray());
    }

    [Fact]
    public async Task Paused_days_do_not_break_the_streak_in_the_today_view()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateHabitAsync(c, "Ler");
        await PauseAsync(c, id, "2030-03-05", "2030-03-07"); // Tue..Thu

        async Task CheckInAsync(string date) =>
            Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date, status = "Done" })).StatusCode);

        await CheckInAsync("2030-03-04");
        _factory.Time!.AdvanceDays(4); // Friday 2030-03-08
        await CheckInAsync("2030-03-08");

        var habit = await TodayHabitAsync(c, id);
        Assert.Equal(2, (int)habit["streak"]!["current"]!); // Mon + Fri, the paused days are neutral
    }

    [Fact]
    public async Task Without_the_pause_the_same_gap_breaks_the_streak()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateHabitAsync(c, "Ler");
        Assert.Equal(HttpStatusCode.OK,
            (await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-04", status = "Done" })).StatusCode);
        _factory.Time!.AdvanceDays(4);
        Assert.Equal(HttpStatusCode.OK,
            (await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-08", status = "Done" })).StatusCode);

        var habit = await TodayHabitAsync(c, id);
        Assert.Equal(1, (int)habit["streak"]!["current"]!);
    }

    [Fact]
    public async Task A_skipped_day_keeps_the_streak_in_the_today_view()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateHabitAsync(c, "Ler");
        Assert.Equal(HttpStatusCode.OK,
            (await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-04", status = "Done" })).StatusCode);
        _factory.Time!.AdvanceDays(1);
        Assert.Equal(HttpStatusCode.OK,
            (await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-05", status = "Skipped" })).StatusCode);
        _factory.Time!.AdvanceDays(1);
        Assert.Equal(HttpStatusCode.OK,
            (await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-06", status = "Done" })).StatusCode);

        var habit = await TodayHabitAsync(c, id);
        Assert.Equal(2, (int)habit["streak"]!["current"]!);
    }

    [Fact]
    public async Task The_reminder_is_not_sent_on_a_paused_day_but_is_on_a_normal_one()
    {
        var c = await _factory.LoggedInClientAsync();
        Assert.Equal(HttpStatusCode.NoContent,
            (await c.PostAsJsonAsync("/api/push/subscriptions", Subscription("https://push.example.com/a"))).StatusCode);
        var paused = await CreateHabitAsync(c, "Pausado", "12:00");
        await CreateHabitAsync(c, "Normal", "12:00");
        await PauseAsync(c, paused, "2030-03-04", "2030-03-04");

        Assert.Equal(1, await DispatchAsync());
        var push = Assert.Single(_factory.Push.Sent);
        Assert.Contains("Normal", (string?)push.Payload["title"]);

        // The next day the pause is over: the habit is reminded again.
        _factory.Time!.AdvanceDays(1);
        Assert.Equal(2, await DispatchAsync());
        Assert.Contains(_factory.Push.Sent, s => ((string?)s.Payload["title"])!.Contains("Pausado"));
    }
}
