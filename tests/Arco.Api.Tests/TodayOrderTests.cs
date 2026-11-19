using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Arco.Api.Tests;

public sealed class TodayOrderTests
{
    // Default time zone is America/Sao_Paulo (UTC-3, no DST in 2030): 2030-03-04 is a Monday.
    private const string Day = "2030-03-04";

    private static DateTimeOffset LocalAt(int hour, int minute = 0) =>
        new DateTimeOffset(2030, 3, 4, hour, minute, 0, TimeSpan.FromHours(-3)).ToUniversalTime();

    private static async Task<int> CreateAsync(HttpClient c, string name, object? extra = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = name, ["frequencyType"] = "Daily", ["levelDescription"] = "10 min", ["minimumDescription"] = "2 min",
        };
        if (extra is not null)
            foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        var res = await c.PostAsJsonAsync("/api/habits", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static async Task<JsonNode> TodayAsync(HttpClient c) => await c.GetJsonAsync("/api/today");

    private static string[] Names(JsonNode today) =>
        today["habits"]!.AsArray().Select(h => (string)h!["name"]!).ToArray();

    private static JsonNode Habit(JsonNode today, string name) =>
        today["habits"]!.AsArray().Single(h => (string)h!["name"]! == name)!;

    [Theory]
    [InlineData(4, 59, "Evening")]
    [InlineData(5, 0, "Morning")]
    [InlineData(11, 59, "Morning")]
    [InlineData(12, 0, "Afternoon")]
    [InlineData(17, 59, "Afternoon")]
    [InlineData(18, 0, "Evening")]
    public async Task Current_period_follows_the_local_time(int hour, int minute, string expected)
    {
        using var factory = new ApiFactory(LocalAt(hour, minute));
        var c = await factory.LoggedInClientAsync();
        var today = await TodayAsync(c);
        Assert.Equal(expected, (string?)today["currentTimeOfDay"]);
        Assert.Equal(Day, (string?)today["date"]);
    }

    [Fact]
    public async Task Habits_come_in_the_order_of_the_day_with_chains_and_paused_last()
    {
        using var factory = new ApiFactory(LocalAt(9));
        var c = await factory.LoggedInClientAsync();
        // Free plan: at most 5 active habits.
        await CreateAsync(c, "Noite", new { timeOfDay = "Evening" });
        var cafe = await CreateAsync(c, "Cafe", new { timeOfDay = "Morning" });
        await CreateAsync(c, "Treino", new { timeOfDay = "Morning" });
        await CreateAsync(c, "Vitamina", new { timeOfDay = "Morning", anchorHabitId = cafe });
        var pausado = await CreateAsync(c, "Pausado", new { timeOfDay = "Morning" });
        (await c.PostAsJsonAsync($"/api/habits/{pausado}/pauses", new { startDate = Day })).EnsureSuccessStatusCode();

        var today = await TodayAsync(c);
        // Vitamina sits right after its anchor, ahead of Treino; the paused habit goes last.
        Assert.Equal(["Cafe", "Vitamina", "Treino", "Noite", "Pausado"], Names(today));
        Assert.True((bool)Habit(today, "Pausado")["isPaused"]!);
        Assert.Equal("Morning", (string?)Habit(today, "Vitamina")["timeOfDay"]);
        Assert.Equal(cafe, (int)Habit(today, "Vitamina")["anchorHabitId"]!);
    }

    [Fact]
    public async Task Anchor_name_is_the_habit_or_the_text_and_anchor_done_marks_the_turn()
    {
        using var factory = new ApiFactory(LocalAt(8));
        var c = await factory.LoggedInClientAsync();
        var cafe = await CreateAsync(c, "Cafe", new { timeOfDay = "Morning" });
        var vitamina = await CreateAsync(c, "Vitamina", new { timeOfDay = "Morning", anchorHabitId = cafe });
        await CreateAsync(c, "Alongar", new { timeOfDay = "Morning", anchorText = "Acordar" });

        var before = await TodayAsync(c);
        Assert.Equal("Cafe", (string?)Habit(before, "Vitamina")["anchorName"]);
        Assert.False((bool)Habit(before, "Vitamina")["anchorDone"]!);
        Assert.Equal("Acordar", (string?)Habit(before, "Alongar")["anchorName"]);
        Assert.False((bool)Habit(before, "Alongar")["anchorDone"]!);
        Assert.Null(Habit(before, "Cafe")["anchorName"]);

        (await c.PutAsJsonAsync("/api/checkins", new { habitId = cafe, date = Day, status = "Minimum" })).EnsureSuccessStatusCode();
        var turn = await TodayAsync(c);
        Assert.True((bool)Habit(turn, "Vitamina")["anchorDone"]!);
        Assert.False((bool)Habit(turn, "Alongar")["anchorDone"]!);

        // Once the habit itself has a check-in, the highlight goes away.
        (await c.PutAsJsonAsync("/api/checkins", new { habitId = vitamina, date = Day, status = "Done" })).EnsureSuccessStatusCode();
        Assert.False((bool)Habit(await TodayAsync(c), "Vitamina")["anchorDone"]!);
    }

    [Fact]
    public async Task A_missed_anchor_does_not_count_as_done()
    {
        using var factory = new ApiFactory(LocalAt(8));
        var c = await factory.LoggedInClientAsync();
        var cafe = await CreateAsync(c, "Cafe", new { timeOfDay = "Morning" });
        await CreateAsync(c, "Vitamina", new { timeOfDay = "Morning", anchorHabitId = cafe });

        (await c.PutAsJsonAsync("/api/checkins", new { habitId = cafe, date = Day, status = "Missed" })).EnsureSuccessStatusCode();
        Assert.False((bool)Habit(await TodayAsync(c), "Vitamina")["anchorDone"]!);
    }

    [Fact]
    public async Task An_archived_anchor_is_ignored_but_the_link_stays()
    {
        using var factory = new ApiFactory(LocalAt(8));
        var c = await factory.LoggedInClientAsync();
        var cafe = await CreateAsync(c, "Cafe", new { timeOfDay = "Morning" });
        await CreateAsync(c, "Vitamina", new { timeOfDay = "Morning", anchorHabitId = cafe });
        (await c.PutAsJsonAsync("/api/checkins", new { habitId = cafe, date = Day, status = "Done" })).EnsureSuccessStatusCode();

        (await c.PostAsync($"/api/habits/{cafe}/archive", null)).EnsureSuccessStatusCode();

        var today = await TodayAsync(c);
        Assert.Equal(["Vitamina"], Names(today));
        var v = Habit(today, "Vitamina");
        Assert.Null(v["anchorName"]);
        Assert.False((bool)v["anchorDone"]!);
        Assert.Equal(cafe, (int)v["anchorHabitId"]!);
    }
}
