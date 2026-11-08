using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Arco.Api.Features.Push;
using Arco.Api.Infrastructure.Push;
using Microsoft.Extensions.DependencyInjection;

namespace Arco.Api.Tests;

public sealed class Phase2ApiTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    private MutableTime Time => _factory.Time!;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Level_up_is_suggested_after_14_days_at_80_percent_and_can_be_dismissed()
    {
        var client = await _factory.LoggedInClientAsync();
        var habitId = await CreateHabitAsync(client, "Correr");

        for (var day = 0; day < 13; day++)
        {
            await PutCheckInAsync(client, habitId, await TodayAsync(client), "Done");
            Time.AdvanceDays(1);
        }

        // Day 13, not checked in yet: the window ends yesterday and starts before the level was reached.
        var detail = await client.GetJsonAsync($"/api/habits/{habitId}");
        Assert.False((bool)detail["levelUpSuggested"]!);
        Assert.Equal(13, (int)detail["consistency"]!["done"]!);
        Assert.Equal(14, (int)detail["consistency"]!["expected"]!);

        await PutCheckInAsync(client, habitId, await TodayAsync(client), "Done");
        detail = await client.GetJsonAsync($"/api/habits/{habitId}");
        Assert.True((bool)detail["levelUpSuggested"]!);
        Assert.Equal(1.0, (double)detail["consistency"]!["rate"]!);
        Assert.Equal("2030-03-04", (string?)detail["consistency"]!["from"]);
        var todayHabit = (await client.GetJsonAsync("/api/today"))["habits"]!.AsArray().Single()!;
        Assert.True((bool)todayHabit["levelUpSuggested"]!);

        var dismiss = await client.PostAsync($"/api/habits/{habitId}/level-up-suggestion/dismiss", null);
        Assert.Equal(HttpStatusCode.NoContent, dismiss.StatusCode);
        Assert.False((bool)(await client.GetJsonAsync($"/api/habits/{habitId}"))["levelUpSuggested"]!);

        // The snooze lasts 7 days.
        for (var day = 0; day < 7; day++)
        {
            Time.AdvanceDays(1);
            await PutCheckInAsync(client, habitId, await TodayAsync(client), "Done");
            var suggested = (bool)(await client.GetJsonAsync($"/api/habits/{habitId}"))["levelUpSuggested"]!;
            Assert.Equal(day == 6, suggested);
        }

        var levelUp = await client.PostAsJsonAsync($"/api/habits/{habitId}/level-up",
            new { levelDescription = "30 min", minimumDescription = "10 min" });
        Assert.Equal(HttpStatusCode.OK, levelUp.StatusCode);
        Assert.False((bool)(await client.GetJsonAsync($"/api/habits/{habitId}"))["levelUpSuggested"]!);
    }

    [Fact]
    public async Task Weekly_review_opens_for_the_finished_week_and_gives_xp_once()
    {
        var client = await _factory.LoggedInClientAsync();
        var habitId = await CreateHabitAsync(client, "Ler");
        await PutCheckInAsync(client, habitId, "2030-03-04", "Done");

        // Monday: the reviewable week is the previous one, which had no check-ins.
        var status = (await client.GetJsonAsync("/api/today"))["weeklyReview"]!;
        Assert.Equal("2030-02-25", (string?)status["weekStart"]);
        Assert.False((bool)status["due"]!);

        Time.AdvanceDays(6); // Sunday: the current week can be reviewed.
        status = (await client.GetJsonAsync("/api/today"))["weeklyReview"]!;
        Assert.Equal("2030-03-04", (string?)status["weekStart"]);
        Assert.Equal("2030-03-10", (string?)status["weekEnd"]);
        Assert.True((bool)status["due"]!);

        var summary = await client.GetJsonAsync("/api/reviews/weekly/current");
        Assert.Null(summary["review"]);
        Assert.Equal(10, (int)summary["xp"]!);
        Assert.Equal(1, (int)summary["checkIns"]!["done"]!);
        var habit = summary["habits"]!.AsArray().Single()!;
        Assert.Equal(1, (int)habit["done"]!);
        Assert.Equal(7, (int)habit["expected"]!);

        var wrongWeek = await client.PostAsJsonAsync("/api/reviews/weekly", new { weekStart = "2030-02-25", wins = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongWeek.StatusCode);
        var empty = await client.PostAsJsonAsync("/api/reviews/weekly", new { weekStart = "2030-03-04", wins = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var created = await client.PostAsJsonAsync("/api/reviews/weekly",
            new { weekStart = "2030-03-04", wins = "Li todo dia útil", obstacles = "Viagem", next = "Ler antes de dormir" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.JsonAsync();
        Assert.Equal(30, (int)body["xpDelta"]!);
        Assert.Equal(40, (int)body["player"]!["totalXp"]!);
        Assert.Equal("Viagem", (string?)body["review"]!["obstacles"]);

        var again = await client.PostAsJsonAsync("/api/reviews/weekly", new { weekStart = "2030-03-04", wins = "de novo" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        Assert.False((bool)(await client.GetJsonAsync("/api/today"))["weeklyReview"]!["due"]!);
        Assert.NotNull((await client.GetJsonAsync("/api/reviews/weekly/current"))["review"]);
        Assert.Single((await client.GetJsonAsync("/api/reviews/weekly")).AsArray());
        var events = (await client.GetJsonAsync("/api/player"))["recentEvents"]!.AsArray();
        Assert.Contains(events, e => (string?)e!["source"] == "WeeklyReview");
    }

    [Fact]
    public async Task Cycle_summary_and_guided_completion()
    {
        var client = await _factory.LoggedInClientAsync();
        var cycle = await (await client.PostAsJsonAsync("/api/cycles", new { name = "Primavera", weeks = 2 })).JsonAsync();
        var cycleId = (int)cycle["id"]!;
        var habitId = await CreateHabitAsync(client, "Correr");
        await PutCheckInAsync(client, habitId, "2030-03-04", "Done");
        Time.AdvanceDays(1);
        await PutCheckInAsync(client, habitId, "2030-03-05", "Minimum");

        var summary = await client.GetJsonAsync($"/api/cycles/{cycleId}/summary");
        Assert.Equal("2030-03-04", (string?)summary["from"]);
        Assert.Equal("2030-03-05", (string?)summary["to"]);
        Assert.Equal(15, (int)summary["xp"]!);
        Assert.Equal(1, (int)summary["checkIns"]!["minimum"]!);
        var habit = summary["habits"]!.AsArray().Single()!;
        Assert.Equal(2, (int)habit["expected"]!);
        Assert.Equal(1.0, (double)habit["rate"]!);
        Assert.Empty(summary["weeklyReviews"]!.AsArray());

        var complete = await client.PostAsJsonAsync($"/api/cycles/{cycleId}/complete",
            new { reviewNotes = "Bom começo", wins = "Constância", obstacles = "Chuva", next = "Correr cedo" });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var done = (await complete.JsonAsync())["cycle"]!;
        Assert.Equal("Constância", (string?)done["reviewWins"]);
        Assert.Equal("Chuva", (string?)done["reviewObstacles"]);
        Assert.Equal("Correr cedo", (string?)done["reviewNext"]);
        Assert.Equal("Bom começo", (string?)done["reviewNotes"]);
    }

    [Fact]
    public async Task Push_subscriptions_are_validated_and_test_message_is_sent()
    {
        var client = await _factory.LoggedInClientAsync();

        var http = await client.PostAsJsonAsync("/api/push/subscriptions", Subscription("http://push.example.com/x"));
        Assert.Equal(HttpStatusCode.BadRequest, http.StatusCode);
        var badKeys = await client.PostAsJsonAsync("/api/push/subscriptions",
            new { endpoint = "https://push.example.com/x", keys = new { p256dh = "abc", auth = "def" } });
        Assert.Equal(HttpStatusCode.BadRequest, badKeys.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/push/subscriptions", Subscription("https://push.example.com/a"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/push/subscriptions", Subscription("https://push.example.com/a"))).StatusCode);

        var config = await client.GetJsonAsync("/api/push/config");
        Assert.Equal(1, (int)config["subscriptions"]!);
        Assert.True(WebPushCrypto.IsValidPublicKey(Base64Url.DecodeFromChars((string)config["publicKey"]!)));

        var test = await (await client.PostAsync("/api/push/test", null)).JsonAsync();
        Assert.Equal(1, (int)test["sent"]!);
        Assert.Equal("test", (string?)_factory.Push.Sent.Single().Payload["tag"]);

        // A subscription the push service no longer knows is dropped.
        _factory.Push.Outcome = PushOutcome.Gone;
        Assert.Equal(0, (int)(await (await client.PostAsync("/api/push/test", null)).JsonAsync())["sent"]!);
        Assert.Equal(0, (int)(await client.GetJsonAsync("/api/push/config"))["subscriptions"]!);

        _factory.Push.Outcome = PushOutcome.Sent;
        await client.PostAsJsonAsync("/api/push/subscriptions", Subscription("https://push.example.com/b"));
        var remove = await client.PostAsJsonAsync("/api/push/subscriptions/remove", new { endpoint = "https://push.example.com/b" });
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Equal(0, (int)(await client.GetJsonAsync("/api/push/config"))["subscriptions"]!);
    }

    [Fact]
    public async Task Habit_reminder_is_sent_once_when_due_and_not_checked_in()
    {
        var client = await _factory.LoggedInClientAsync();
        await client.PostAsJsonAsync("/api/push/subscriptions", Subscription("https://push.example.com/a"));

        var invalid = await client.PostAsJsonAsync("/api/habits", HabitBody("Meditar", reminderTime: "25:00"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var created = await client.PostAsJsonAsync("/api/habits", HabitBody("Meditar", reminderTime: "13:00"));
        var habit = await created.JsonAsync();
        Assert.Equal("13:00", (string?)habit["reminderTime"]);
        var habitId = (int)habit["id"]!;

        Assert.Equal(0, await DispatchAsync()); // 12:00 local: not yet

        Time.Advance(TimeSpan.FromMinutes(90)); // 13:30
        Assert.Equal(1, await DispatchAsync());
        var payload = _factory.Push.Sent.Single().Payload;
        Assert.Contains("Meditar", (string?)payload["title"]);
        Assert.Equal("/", (string?)payload["url"]);
        Assert.Equal($"habit-{habitId}", (string?)payload["tag"]);
        Assert.Equal(0, await DispatchAsync()); // already sent today

        // Next day, already checked in: no reminder.
        Time.AdvanceDays(1);
        await PutCheckInAsync(client, habitId, "2030-03-05", "Done");
        Assert.Equal(0, await DispatchAsync());

        // Day after, too late (more than 2h after the time): skipped.
        Time.AdvanceDays(1);
        Time.Advance(TimeSpan.FromHours(3));
        Assert.Equal(0, await DispatchAsync());

        // Clearing the reminder.
        var update = await client.PutAsJsonAsync($"/api/habits/{habitId}", HabitBody("Meditar", reminderTime: null));
        Assert.Null((await update.JsonAsync())["reminderTime"]);
    }

    [Fact]
    public async Task Weekly_review_reminder_follows_settings()
    {
        var client = await _factory.LoggedInClientAsync();
        var settings = await client.GetJsonAsync("/api/settings");
        Assert.True((bool)settings["weeklyReviewReminder"]!["enabled"]!);
        Assert.Equal(0, (int)settings["weeklyReviewReminder"]!["day"]!);
        Assert.Equal("18:00", (string?)settings["weeklyReviewReminder"]!["time"]);

        var bad = await client.PutAsJsonAsync("/api/settings", new { weeklyReviewReminder = new { day = 7 } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var badTime = await client.PutAsJsonAsync("/api/settings", new { weeklyReviewReminder = new { time = "6pm" } });
        Assert.Equal(HttpStatusCode.BadRequest, badTime.StatusCode);

        // Monday 12:00 (partial update keeps the timezone).
        var saved = await (await client.PutAsJsonAsync("/api/settings",
            new { weeklyReviewReminder = new { day = 1, time = "11:30" } })).JsonAsync();
        Assert.Equal("America/Sao_Paulo", (string?)saved["timezone"]);
        Assert.Equal(1, (int)saved["weeklyReviewReminder"]!["day"]!);

        Assert.Equal(0, await DispatchAsync()); // no subscriptions yet
        await client.PostAsJsonAsync("/api/push/subscriptions", Subscription("https://push.example.com/a"));
        Assert.Equal(1, await DispatchAsync());
        Assert.Equal("/review", (string?)_factory.Push.Sent.Single().Payload["url"]);
        Assert.Equal(0, await DispatchAsync());

        await client.PutAsJsonAsync("/api/settings", new { weeklyReviewReminder = new { enabled = false } });
        Time.AdvanceDays(7);
        Assert.Equal(0, await DispatchAsync());
    }

    private Task<int> DispatchAsync() =>
        ReminderDispatcher.RunForAllUsersAsync(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(), null, CancellationToken.None);

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

    private static object HabitBody(string name, string? reminderTime) => new
    {
        name,
        frequencyType = "Daily",
        levelDescription = "10 min",
        minimumDescription = "2 min",
        reminderTime,
    };

    private static async Task<string> TodayAsync(HttpClient client) =>
        (string)(await client.GetJsonAsync("/api/today"))["date"]!;

    private static async Task<int> CreateHabitAsync(HttpClient client, string name)
    {
        var res = await client.PostAsJsonAsync("/api/habits", HabitBody(name, null));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static async Task PutCheckInAsync(HttpClient client, int habitId, string date, string status)
    {
        var res = await client.PutAsJsonAsync("/api/checkins", new { habitId, date, status });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
