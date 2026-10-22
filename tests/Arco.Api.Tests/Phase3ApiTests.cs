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

    [Fact]
    public async Task Planned_ladder_and_weeks_rule_drive_the_level_up()
    {
        var client = await _factory.LoggedInClientAsync();
        var res = await client.PostAsJsonAsync("/api/habits", new
        {
            name = "Inglês",
            frequencyType = "WeeklyTarget",
            timesPerWeek = 5,
            levelDescription = "15 min",
            minimumDescription = "5 min",
            plannedLevels = new[]
            {
                new { levelDescription = "25 min", minimumDescription = "5 min" },
                new { levelDescription = "35 min", minimumDescription = "5 min" },
            },
            levelUpWeeks = 2,
            levelUpDaysPerWeek = 5,
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.JsonAsync();
        var habitId = (int)created["id"]!;
        Assert.Equal(2, (int)created["plannedLevels"]![0]!["level"]!);
        Assert.Equal("35 min", (string?)created["plannedLevels"]![1]!["description"]);
        Assert.Equal(2, (int)created["levelUpWeeks"]!);

        // The habit was created on Monday 2030-03-04: two weeks with 5 days (Minimum counts).
        for (var day = 0; day < 14; day++)
        {
            if (day % 7 < 5)
            {
                var date = (string)(await client.GetJsonAsync("/api/today"))["date"]!;
                var status = day < 7 ? "Done" : "Minimum";
                Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/checkins", new { habitId, date, status })).StatusCode);
            }
            if (day < 13) Time.AdvanceDays(1);
        }

        var detail = await client.GetJsonAsync($"/api/habits/{habitId}");
        Assert.Equal(2, (int)detail["levelUpWeeksDone"]!);
        Assert.True((bool)detail["levelUpSuggested"]!);
        var todayHabit = (await client.GetJsonAsync("/api/today"))["habits"]!.AsArray().Single()!;
        Assert.True((bool)todayHabit["levelUpSuggested"]!);

        // Leveling up with an empty body uses (and consumes) the planned next level.
        var levelUp = await client.PostAsJsonAsync($"/api/habits/{habitId}/level-up", new { });
        Assert.Equal(HttpStatusCode.OK, levelUp.StatusCode);
        var habit = (await levelUp.JsonAsync())["habit"]!;
        Assert.Equal(2, (int)habit["level"]!);
        Assert.Equal("25 min", (string?)habit["levelDescription"]);
        var planned = habit["plannedLevels"]!.AsArray();
        Assert.Equal(3, (int)planned.Single()!["level"]!);
        Assert.False((bool)(await client.GetJsonAsync($"/api/habits/{habitId}"))["levelUpSuggested"]!);

        // Without a planned level and without a body, the level-up is rejected.
        var put = await client.PutAsJsonAsync($"/api/habits/{habitId}", new
        {
            name = "Inglês", frequencyType = "WeeklyTarget", timesPerWeek = 5,
            levelDescription = "25 min", minimumDescription = "5 min",
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await put.JsonAsync();
        Assert.Empty(updated["plannedLevels"]!.AsArray());
        Assert.Null(updated["levelUpWeeks"]);
        var rejected = await client.PostAsJsonAsync($"/api/habits/{habitId}/level-up", new { });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var invalid = await client.PutAsJsonAsync($"/api/habits/{habitId}", new
        {
            name = "Inglês", frequencyType = "Daily", levelDescription = "25 min", minimumDescription = "5 min",
            levelUpWeeks = 9, levelUpDaysPerWeek = 5,
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Achievements_unlock_once_and_come_back_in_the_xp_response()
    {
        var client = await _factory.LoggedInClientAsync();
        var res = await client.PostAsJsonAsync("/api/habits",
            new { name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        var habitId = (int)(await res.JsonAsync())["id"]!;

        var list = (await client.GetJsonAsync("/api/achievements")).AsArray();
        Assert.All(list, a => Assert.Null(a!["unlockedAt"]));
        Assert.Equal(0, (int)list.Single(a => (string?)a!["key"] == "checkins-1")!["progress"]!);

        async Task<string[]> CheckInAsync(string status)
        {
            var date = (string)(await client.GetJsonAsync("/api/today"))["date"]!;
            var put = await client.PutAsJsonAsync("/api/checkins", new { habitId, date, status });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            return (await put.JsonAsync())["achievements"]!.AsArray().Select(a => (string)a!["key"]!).ToArray();
        }

        Assert.Equal(["checkins-1"], await CheckInAsync("Done"));
        // Changing the same check-in unlocks nothing new.
        Assert.Empty(await CheckInAsync("Minimum"));

        for (var day = 1; day < 7; day++)
        {
            Time.AdvanceDays(1);
            var unlocked = await CheckInAsync("Done");
            Assert.Equal(day == 6 ? ["streak-7"] : [], unlocked);
        }

        list = (await client.GetJsonAsync("/api/achievements")).AsArray();
        var streak7 = list.Single(a => (string?)a!["key"] == "streak-7")!;
        Assert.NotNull(streak7["unlockedAt"]);
        Assert.Equal(7, (int)streak7["progress"]!);
        Assert.Equal(7, (int)list.Single(a => (string?)a!["key"] == "streak-30")!["progress"]!);

        // Undoing a check-in never revokes an achievement.
        var date = (string)(await client.GetJsonAsync("/api/today"))["date"]!;
        var delete = await client.DeleteAsync($"/api/checkins/{habitId}/{date}");
        Assert.Empty((await delete.JsonAsync())["achievements"]!.AsArray());
        list = (await client.GetJsonAsync("/api/achievements")).AsArray();
        Assert.NotNull(list.Single(a => (string?)a!["key"] == "streak-7")!["unlockedAt"]);
    }
}
