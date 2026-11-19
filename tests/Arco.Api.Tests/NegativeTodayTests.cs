using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Arco.Api.Tests;

public sealed class NegativeTodayTests : IDisposable
{
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static async Task<int> CreateAsync(HttpClient c, string kind)
    {
        var res = await c.PostAsJsonAsync("/api/habits", new { name = "Hábito " + kind, kind, frequencyType = "Daily", levelDescription = "Meta", minimumDescription = "Mínimo" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static async Task CheckInAsync(HttpClient c, int habitId, string date, string status)
    {
        var res = await c.PutAsJsonAsync("/api/checkins", new { habitId, date, status });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    /// <summary>Registers <paramref name="status"/> for the current day, then moves the clock to the next day.</summary>
    private async Task DayAsync(HttpClient c, int habitId, string status)
    {
        await CheckInAsync(c, habitId, DateOnly.FromDateTime(_factory.Time!.Now.UtcDateTime).ToString("yyyy-MM-dd"), status);
        _factory.Time!.AdvanceDays(1);
    }

    private async Task CleanRunAsync(HttpClient c, int habitId, int days)
    {
        for (var i = 0; i < days; i++) await DayAsync(c, habitId, "Done");
    }

    private static async Task<JsonNode> TodayHabitAsync(HttpClient c, int habitId)
    {
        var today = await c.GetJsonAsync("/api/today");
        return today["habits"]!.AsArray().First(h => (int)h!["id"]! == habitId)!;
    }

    [Fact]
    public async Task Level_up_is_suggested_when_the_first_milestone_is_reached()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateAsync(c, "Negative");

        await CleanRunAsync(c, id, 6);
        var before = await TodayHabitAsync(c, id);
        Assert.Equal("Negative", (string?)before["kind"]);
        Assert.Equal(6, (int)before["streak"]!["current"]!);
        Assert.Equal(7, (int)before["nextMilestone"]!);
        Assert.False((bool)before["levelUpSuggested"]!);

        await DayAsync(c, id, "Done");
        var after = await TodayHabitAsync(c, id);
        Assert.Equal(7, (int)after["streak"]!["current"]!);
        Assert.True((bool)after["levelUpSuggested"]!);
    }

    [Fact]
    public async Task Dismissing_the_suggestion_snoozes_it()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateAsync(c, "Negative");
        await CleanRunAsync(c, id, 7);
        Assert.True((bool)(await TodayHabitAsync(c, id))["levelUpSuggested"]!);

        var dismiss = await c.PostAsync($"/api/habits/{id}/level-up-suggestion/dismiss", null);
        Assert.True(dismiss.IsSuccessStatusCode);
        Assert.False((bool)(await TodayHabitAsync(c, id))["levelUpSuggested"]!);

        await CleanRunAsync(c, id, 7);
        Assert.True((bool)(await TodayHabitAsync(c, id))["levelUpSuggested"]!);
    }

    [Fact]
    public async Task A_negative_habit_never_suggests_going_back_a_level()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateAsync(c, "Negative");
        await CheckInAsync(c, id, "2030-03-04", "Done");
        // A level up, then relapses for weeks: the level-down rule would fire for a regular habit.
        var up = await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { });
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        _factory.Time!.AdvanceDays(1);
        for (var i = 1; i <= 21; i++) await DayAsync(c, id, "Missed");

        var h = await TodayHabitAsync(c, id);
        Assert.False((bool)h["levelDownSuggested"]!);
        Assert.Equal(0, (int)h["streak"]!["current"]!);
    }

    [Fact]
    public async Task A_relapse_resets_the_clean_days_but_keeps_the_record()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateAsync(c, "Negative");
        await CleanRunAsync(c, id, 5);
        await DayAsync(c, id, "Missed");
        await DayAsync(c, id, "Done");
        await DayAsync(c, id, "Done");

        var h = await TodayHabitAsync(c, id);
        Assert.Equal(2, (int)h["streak"]!["current"]!);
        Assert.Equal(5, (int)h["streak"]!["best"]!);
        Assert.False((bool)h["levelUpSuggested"]!);
    }

    [Fact]
    public async Task Next_milestone_follows_the_level_and_is_null_for_positive_habits()
    {
        var c = await _factory.LoggedInClientAsync();
        var negative = await CreateAsync(c, "Negative");
        var positive = await CreateAsync(c, "Positive");
        Assert.Equal(7, (int)(await TodayHabitAsync(c, negative))["nextMilestone"]!);
        Assert.Null((await TodayHabitAsync(c, positive))["nextMilestone"]);

        var up = await c.PostAsJsonAsync($"/api/habits/{negative}/level-up", new { });
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        Assert.Equal(14, (int)(await TodayHabitAsync(c, negative))["nextMilestone"]!);
    }
}
