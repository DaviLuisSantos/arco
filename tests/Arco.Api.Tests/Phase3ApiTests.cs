using System.Net;
using System.Net.Http.Json;

namespace Arco.Api.Tests;

public sealed class Phase3ApiTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    private MutableTime Time => _factory.Time!;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Streak_is_returned_on_today_and_habit_detail()
    {
        var client = await _factory.LoggedInClientAsync();
        var res = await client.PostAsJsonAsync("/api/habits",
            new { name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var habitId = (int)(await res.JsonAsync())["id"]!;

        // Done, Done, Missed (forgiven), Minimum; day 4 not marked yet.
        foreach (var status in new[] { "Done", "Done", "Missed", "Minimum" })
        {
            var date = (string)(await client.GetJsonAsync("/api/today"))["date"]!;
            var put = await client.PutAsJsonAsync("/api/checkins", new { habitId, date, status });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            Time.AdvanceDays(1);
        }

        var todayStreak = (await client.GetJsonAsync("/api/today"))["habits"]!.AsArray().Single()!["streak"]!;
        Assert.Equal(3, (int)todayStreak["current"]!);
        Assert.Equal(3, (int)todayStreak["best"]!);
        Assert.Equal("Days", (string?)todayStreak["unit"]);

        // Missing a second day in the same week resets the current streak; the record stays.
        Time.AdvanceDays(1);
        var detailStreak = (await client.GetJsonAsync($"/api/habits/{habitId}"))["streak"]!;
        Assert.Equal(0, (int)detailStreak["current"]!);
        Assert.Equal(3, (int)detailStreak["best"]!);
    }
}
