using System.Net;
using System.Net.Http.Json;
using Arco.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task Stats_cover_12_weeks_and_the_last_30_days()
    {
        var client = await _factory.LoggedInClientAsync();
        async Task<int> CreateAsync(string name)
        {
            var res = await client.PostAsJsonAsync("/api/habits",
                new { name, frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
            return (int)(await res.JsonAsync())["id"]!;
        }
        var ler = await CreateAsync("Ler");
        var correr = await CreateAsync("Correr");

        // Monday 2030-03-04 to Wednesday 2030-03-06: Ler Done, Minimum, Missed; Correr Done on Monday only.
        foreach (var (status, day) in new[] { ("Done", 0), ("Minimum", 1), ("Missed", 2) })
        {
            var date = (string)(await client.GetJsonAsync("/api/today"))["date"]!;
            await client.PutAsJsonAsync("/api/checkins", new { habitId = ler, date, status });
            if (day == 0) await client.PutAsJsonAsync("/api/checkins", new { habitId = correr, date, status = "Done" });
            if (day < 2) Time.AdvanceDays(1);
        }

        var stats = await client.GetJsonAsync("/api/stats");
        var weeks = stats["weeks"]!.AsArray();
        Assert.Equal(12, weeks.Count);
        var current = weeks[^1]!;
        Assert.Equal("2030-03-04", (string?)current["weekStart"]);
        Assert.Equal(2, (int)current["done"]!);
        Assert.Equal(1, (int)current["minimum"]!);
        Assert.Equal(1, (int)current["missed"]!);
        Assert.Equal(6, (int)current["expected"]!);   // 2 daily habits × 3 days so far
        Assert.Equal(0.5, (double)current["rate"]!);
        Assert.Equal(25, (int)current["xp"]!);        // 10 + 5 + 0 + 10
        Assert.Equal(0, (int)weeks[0]!["expected"]!); // before the habits existed

        // A check-in backdated before the habit existed starts its expected days there.
        await client.PutAsJsonAsync("/api/checkins", new { habitId = ler, date = "2030-03-02", status = "Done" });
        var previous = (await client.GetJsonAsync("/api/stats"))["weeks"]!.AsArray()[^2]!;
        Assert.Equal(2, (int)previous["expected"]!);  // Saturday and Sunday, Ler only
        Assert.Equal(0.5, (double)previous["rate"]!);

        var recent = stats["recent"]!;
        Assert.Equal("2030-02-05", (string?)recent["from"]);
        Assert.Equal(2, recent["habits"]!.AsArray().Count);

        var onlyLer = (await client.GetJsonAsync($"/api/checkins?from=2030-03-01&to=2030-03-31&habitId={ler}")).AsArray();
        Assert.Equal(4, onlyLer.Count); // includes the backdated one
        Assert.All(onlyLer, c => Assert.Equal(ler, (int)c!["habitId"]!));
    }

    [Fact]
    public async Task Backup_downloads_a_consistent_copy_and_keeps_one_automatic_copy_per_day()
    {
        var client = await _factory.LoggedInClientAsync();
        await client.PostAsJsonAsync("/api/habits",
            new { name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });

        var res = await client.GetAsync("/api/backup");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("arco-2030-03-04.db", res.Content.Headers.ContentDisposition?.FileNameStar ?? res.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal("SQLite format 3 ", System.Text.Encoding.ASCII.GetString(bytes, 0, 16));

        // The copy is a working database with the data.
        var copy = Path.Combine(Path.GetTempPath(), $"arco-test-{Guid.NewGuid():N}.db");
        await File.WriteAllBytesAsync(copy, bytes);
        try
        {
            await using var conn = new SqliteConnection($"Data Source={copy};Pooling=False");
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Name FROM Habits";
            Assert.Equal("Ler", (string?)await cmd.ExecuteScalarAsync());
        }
        finally
        {
            File.Delete(copy);
        }

        Assert.Empty((await client.GetJsonAsync("/api/backups")).AsArray());
        var backup = _factory.Services.GetRequiredService<DatabaseBackup>();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var today = new DateOnly(2030, 3, 4);
            Assert.NotNull(await backup.CreateDailyAsync(db, today, CancellationToken.None));
            Assert.Null(await backup.CreateDailyAsync(db, today, CancellationToken.None)); // once a day
        }
        var list = (await client.GetJsonAsync("/api/backups")).AsArray();
        Assert.Equal("2030-03-04", (string?)list.Single()!["date"]);
        Assert.True((long)list.Single()!["sizeBytes"]! > 0);
    }
}
