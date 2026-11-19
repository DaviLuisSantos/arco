using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Arco.Api.Features.Habits;
using Microsoft.Extensions.DependencyInjection;

namespace Arco.Api.Tests;

public sealed class CleanDaysTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private Task<int> RunAsync() =>
        _factory.Services.GetRequiredService<CleanDaysService>().RunAsync(CancellationToken.None);

    private static async Task<int> CreateAsync(HttpClient c, string kind = "Negative", string name = "Não fumar")
    {
        var res = await c.PostAsJsonAsync("/api/habits", new
        {
            name, kind, frequencyType = "Daily", levelDescription = "Sem recaída", minimumDescription = "Sem recaída",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static async Task<Dictionary<string, JsonNode>> CheckInsAsync(HttpClient c, int habitId)
    {
        var json = await c.GetJsonAsync($"/api/checkins?from=2030-01-01&to=2030-12-31&habitId={habitId}");
        return json.AsArray().ToDictionary(x => (string)x!["date"]!, x => x!);
    }

    private static async Task<int> TotalXpAsync(HttpClient c) =>
        (int)(await c.GetJsonAsync("/api/player"))["totalXp"]!;

    [Fact]
    public async Task Past_days_without_check_in_become_clean_with_xp_and_today_is_left_pending()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateAsync(c);
        _factory.Time!.AdvanceDays(3); // today = 2030-03-07

        // Created on 03-04: the 4th, 5th and 6th are clean; the 7th (today) is not.
        Assert.Equal(3, await RunAsync());
        var days = await CheckInsAsync(c, id);
        Assert.Equal(["2030-03-04", "2030-03-05", "2030-03-06"], days.Keys.Order());
        Assert.All(days.Values, d =>
        {
            Assert.Equal("Done", (string?)d["status"]);
            Assert.Equal(10, (int)d["xp"]!);
        });
        Assert.Equal(30, await TotalXpAsync(c));
    }

    [Fact]
    public async Task Days_with_a_relapse_or_a_skip_are_left_alone()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateAsync(c);
        _factory.Time!.AdvanceDays(3); // today = 03-07
        Assert.True((await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-05", status = "Missed" })).IsSuccessStatusCode);
        Assert.True((await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-06", status = "Skipped" })).IsSuccessStatusCode);

        Assert.Equal(1, await RunAsync()); // only 03-04
        var days = await CheckInsAsync(c, id);
        Assert.Equal("Done", (string?)days["2030-03-04"]["status"]);
        Assert.Equal("Missed", (string?)days["2030-03-05"]["status"]);
        Assert.Equal(0, (int)days["2030-03-05"]["xp"]!);
        Assert.Equal("Skipped", (string?)days["2030-03-06"]["status"]);
        Assert.False(days.ContainsKey("2030-03-07"));
    }

    [Fact]
    public async Task Paused_days_are_not_filled()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateAsync(c);
        var pause = await c.PostAsJsonAsync($"/api/habits/{id}/pauses", new { startDate = "2030-03-05", endDate = "2030-03-06" });
        Assert.Equal(HttpStatusCode.Created, pause.StatusCode);
        _factory.Time!.AdvanceDays(4); // today = 03-08

        Assert.Equal(2, await RunAsync()); // 03-04 and 03-07
        Assert.Equal(["2030-03-04", "2030-03-07"], (await CheckInsAsync(c, id)).Keys.Order());
    }

    [Fact]
    public async Task Days_before_the_habit_existed_are_not_filled()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateAsync(c);
        _factory.Time!.AdvanceDays(2); // today = 03-06

        Assert.Equal(2, await RunAsync());
        var days = await CheckInsAsync(c, id);
        Assert.Equal(["2030-03-04", "2030-03-05"], days.Keys.Order());
        Assert.False(days.ContainsKey("2030-03-03"));
    }

    [Fact]
    public async Task Only_the_last_seven_days_are_filled_after_a_long_absence()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateAsync(c);
        _factory.Time!.AdvanceDays(20); // today = 03-24

        Assert.Equal(7, await RunAsync());
        var days = await CheckInsAsync(c, id);
        Assert.Equal(["2030-03-17", "2030-03-18", "2030-03-19", "2030-03-20", "2030-03-21", "2030-03-22", "2030-03-23"],
            days.Keys.Order());
    }

    [Fact]
    public async Task Running_again_creates_nothing_and_keeps_the_xp()
    {
        var c = await _factory.LoggedInClientAsync();
        await CreateAsync(c);
        _factory.Time!.AdvanceDays(3);

        Assert.Equal(3, await RunAsync());
        var xp = await TotalXpAsync(c);
        Assert.Equal(0, await RunAsync());
        Assert.Equal(xp, await TotalXpAsync(c));

        // The next day adds exactly one more.
        _factory.Time.AdvanceDays(1);
        Assert.Equal(1, await RunAsync());
        Assert.Equal(xp + 10, await TotalXpAsync(c));
    }

    [Fact]
    public async Task The_user_timezone_decides_which_days_have_passed()
    {
        var c = await _factory.LoggedInClientAsync();
        Assert.True((await c.PutAsJsonAsync("/api/settings", new { timezone = "Pacific/Auckland" })).IsSuccessStatusCode);
        // 03-04 15:00 UTC is already 03-05 04:00 (UTC+13) in Auckland, where the habit starts.
        var id = await CreateAsync(c);
        Assert.Equal(0, await RunAsync()); // created today: nothing has passed yet

        _factory.Time!.AdvanceDays(1); // 03-06 04:00 in Auckland (03-05 12:00 in São Paulo)
        Assert.Equal(1, await RunAsync());
        Assert.Equal(["2030-03-05"], (await CheckInsAsync(c, id)).Keys.Order());
    }

    [Fact]
    public async Task Archived_and_positive_habits_are_ignored()
    {
        var c = await _factory.LoggedInClientAsync();
        var archived = await CreateAsync(c, name: "Arquivado");
        var positive = await CreateAsync(c, "Positive", "Ler");
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/habits/{archived}/archive", null)).StatusCode);
        _factory.Time!.AdvanceDays(3);

        Assert.Equal(0, await RunAsync());
        Assert.Empty(await CheckInsAsync(c, archived));
        Assert.Empty(await CheckInsAsync(c, positive));
    }

    [Fact]
    public async Task Each_user_gets_only_their_own_clean_days()
    {
        var a = await _factory.LoggedInClientAsync();
        var b = await _factory.LoggedInClientAsync();
        var none = await _factory.LoggedInClientAsync();
        var habitA = await CreateAsync(a);
        var habitB = await CreateAsync(b);
        await CreateAsync(none, "Positive", "Ler");
        _factory.Time!.AdvanceDays(2);

        Assert.Equal(4, await RunAsync()); // two days each
        Assert.Equal(2, (await CheckInsAsync(a, habitA)).Count);
        Assert.Equal(2, (await CheckInsAsync(b, habitB)).Count);
        Assert.Empty(await CheckInsAsync(a, habitB)); // not visible across users
        Assert.Equal(20, await TotalXpAsync(a));
        Assert.Equal(20, await TotalXpAsync(b));
        Assert.Equal(0, await TotalXpAsync(none));
    }

    [Fact]
    public async Task A_week_of_clean_days_unlocks_the_streak_achievement()
    {
        var c = await _factory.LoggedInClientAsync();
        await CreateAsync(c);
        _factory.Time!.AdvanceDays(8); // today = 03-12: clean days 03-05..03-11

        Assert.Equal(7, await RunAsync());
        var achievements = await c.GetJsonAsync("/api/achievements");
        var streak7 = achievements.AsArray().Single(a => (string?)a!["key"] == "streak-7")!;
        Assert.NotNull(streak7["unlockedAt"]);
    }
}
