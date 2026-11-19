using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Arco.Api.Tests;

public sealed class QuantityApiTests : IDisposable
{
    // Default time zone is America/Sao_Paulo (UTC-3, no DST in 2030): 2030-03-04 is a Monday.
    private const string Today = "2030-03-04";
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 9, 0, 0, TimeSpan.FromHours(-3)).ToUniversalTime());

    public void Dispose() => _factory.Dispose();

    /// <summary>Water habit: goal 8, minimum 4 (level 1: Done = 10 XP, Minimum = 5 XP).</summary>
    private static async Task<int> CreateWaterAsync(HttpClient c, string name = "Água")
    {
        var res = await c.PostAsJsonAsync("/api/habits", new Dictionary<string, object?>
        {
            ["name"] = name, ["frequencyType"] = "Daily", ["unit"] = "copos", ["levelTarget"] = 8, ["levelMinimumTarget"] = 4,
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static async Task<int> CreateRegularAsync(HttpClient c)
    {
        var res = await c.PostAsJsonAsync("/api/habits", new
        {
            name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static Task<HttpResponseMessage> SetAsync(HttpClient c, int habitId, object total, string? date = null) =>
        c.PutAsJsonAsync("/api/quantities", new { habitId, date, total });

    private static async Task<JsonNode> SetOkAsync(HttpClient c, int habitId, object total, string? date = null)
    {
        var res = await SetAsync(c, habitId, total, date);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.JsonAsync();
    }

    private static async Task AssertErrorAsync(HttpResponseMessage res, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(code, (string?)(await res.JsonAsync())["code"]);
    }

    private static Task<JsonNode> CheckInsAsync(HttpClient c, int habitId) =>
        c.GetJsonAsync($"/api/checkins?from=2030-02-25&to={Today}&habitId={habitId}");

    [Fact]
    public async Task Adding_up_reaches_Minimum_then_Done_with_the_right_XP()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateWaterAsync(c);

        var below = await SetOkAsync(c, id, 3);
        Assert.Equal(3m, (decimal)below["total"]!);
        Assert.Null(below["checkIn"]);
        Assert.Equal(0, (int)below["xpDelta"]!);

        var minimum = await SetOkAsync(c, id, 4);
        Assert.Equal("Minimum", (string?)minimum["checkIn"]!["status"]);
        Assert.Equal(5, (int)minimum["xpDelta"]!);
        Assert.Equal(5, (int)minimum["checkIn"]!["xp"]!);

        var done = await SetOkAsync(c, id, 8);
        Assert.Equal("Done", (string?)done["checkIn"]!["status"]);
        Assert.Equal(5, (int)done["xpDelta"]!); // 10 - 5
        Assert.Equal(10, (int)done["checkIn"]!["xp"]!);

        // Going further keeps Done and gives no more XP.
        var more = await SetOkAsync(c, id, 12.5m);
        Assert.Equal("Done", (string?)more["checkIn"]!["status"]);
        Assert.Equal(0, (int)more["xpDelta"]!);
        Assert.Equal(12.5m, (decimal)more["total"]!);

        var list = await c.GetJsonAsync($"/api/quantities?from={Today}&to={Today}");
        Assert.Single(list.AsArray());
        Assert.Equal(12.5m, (decimal)list[0]!["total"]!);
        Assert.Equal(Today, (string?)list[0]!["date"]);
        Assert.Equal("Done", (string?)(await CheckInsAsync(c, id))[0]!["status"]);
    }

    [Fact]
    public async Task Dropping_below_the_minimum_removes_the_check_in_and_its_XP()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateWaterAsync(c);
        var before = (int)(await c.GetJsonAsync("/api/player"))["totalXp"]!;

        await SetOkAsync(c, id, 8);
        Assert.Equal(before + 10, (int)(await c.GetJsonAsync("/api/player"))["totalXp"]!);

        var dropped = await SetOkAsync(c, id, 2);
        Assert.Null(dropped["checkIn"]);
        Assert.Equal(-10, (int)dropped["xpDelta"]!);
        Assert.Equal(before, (int)(await c.GetJsonAsync("/api/player"))["totalXp"]!);
        Assert.Empty((await CheckInsAsync(c, id)).AsArray());
        // The total itself stays (2), only the derived check-in goes.
        Assert.Equal(2m, (decimal)(await c.GetJsonAsync($"/api/quantities?from={Today}&to={Today}"))[0]!["total"]!);

        // Zero removes the total.
        await SetOkAsync(c, id, 0);
        Assert.Empty((await c.GetJsonAsync($"/api/quantities?from={Today}&to={Today}")).AsArray());
    }

    [Fact]
    public async Task Manual_Missed_stays_below_the_minimum_and_is_replaced_once_it_is_reached()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateWaterAsync(c);
        (await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = Today, status = "Missed" })).EnsureSuccessStatusCode();

        var below = await SetOkAsync(c, id, 2);
        Assert.Equal("Missed", (string?)below["checkIn"]!["status"]);
        Assert.Equal(0, (int)below["xpDelta"]!);
        Assert.Equal("Missed", (string?)(await CheckInsAsync(c, id))[0]!["status"]);

        var reached = await SetOkAsync(c, id, 4);
        Assert.Equal("Minimum", (string?)reached["checkIn"]!["status"]);
        Assert.Equal(5, (int)reached["xpDelta"]!);
        Assert.Single((await CheckInsAsync(c, id)).AsArray());

        // Falling back removes the derived check-in (the manual Missed was replaced for good).
        await SetOkAsync(c, id, 1);
        Assert.Empty((await CheckInsAsync(c, id)).AsArray());
    }

    [Fact]
    public async Task The_derived_status_can_be_resent_to_edit_the_note_without_changing_xp()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateWaterAsync(c);
        await SetOkAsync(c, id, 8); // reaches the goal: Done

        var noted = await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = Today, status = "Done", note = "copo grande" });
        noted.EnsureSuccessStatusCode();
        var json = await noted.JsonAsync();
        Assert.Equal(0, (int)json["xpDelta"]!);
        Assert.Equal("copo grande", (string?)json["checkIn"]!["note"]);

        // Another status than the derived one is still refused.
        await AssertErrorAsync(await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = Today, status = "Minimum" }), "quantity_required");
    }

    [Fact]
    public async Task Manual_Done_or_Minimum_is_refused_in_a_quantitative_habit()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateWaterAsync(c);

        await AssertErrorAsync(await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = Today, status = "Done" }), "quantity_required");
        await AssertErrorAsync(await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = Today, status = "Minimum" }), "quantity_required");
        Assert.Empty((await CheckInsAsync(c, id)).AsArray());

        (await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = Today, status = "Skipped" })).EnsureSuccessStatusCode();
        // A regular habit is unaffected.
        var regular = await CreateRegularAsync(c);
        (await c.PutAsJsonAsync("/api/checkins", new { habitId = regular, date = Today, status = "Done" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Deleting_the_check_in_also_deletes_the_day_total()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateWaterAsync(c);

        // Total below the minimum (no check-in): DELETE still clears it.
        await SetOkAsync(c, id, 3);
        var cleared = await c.DeleteAsync($"/api/checkins/{id}/{Today}");
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Empty((await c.GetJsonAsync($"/api/quantities?from={Today}&to={Today}")).AsArray());
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"/api/checkins/{id}/{Today}")).StatusCode);

        await SetOkAsync(c, id, 8);
        var deleted = await c.DeleteAsync($"/api/checkins/{id}/{Today}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(-10, (int)(await deleted.JsonAsync())["xpDelta"]!);
        Assert.Empty((await c.GetJsonAsync($"/api/quantities?from={Today}&to={Today}")).AsArray());
        Assert.Empty((await CheckInsAsync(c, id)).AsArray());
    }

    [Fact]
    public async Task Leveling_up_and_down_recalculates_todays_status()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateWaterAsync(c);
        await SetOkAsync(c, id, 6); // level 1: Minimum (5 XP)

        // Level 2 with goal 5 / minimum 3: 6 is Done at level 2 = 12 XP (+7 over the old 5), plus 50 for the level.
        var up = await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { target = 5, minimumTarget = 3 });
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        Assert.Equal(57, (int)(await up.JsonAsync())["xpDelta"]!);
        var afterUp = (await CheckInsAsync(c, id))[0]!;
        Assert.Equal("Done", (string?)afterUp["status"]);
        Assert.Equal(12, (int)afterUp["xp"]!);

        // Back to level 1 (goal 8 / minimum 4): 6 is Minimum again.
        var down = await c.PostAsync($"/api/habits/{id}/level-down", null);
        Assert.Equal(HttpStatusCode.OK, down.StatusCode);
        var afterDown = (await CheckInsAsync(c, id))[0]!;
        Assert.Equal("Minimum", (string?)afterDown["status"]);
        Assert.Equal(5, (int)afterDown["xp"]!);

        // A harder level (goal 20 / minimum 10) leaves 6 below the minimum: the check-in goes.
        (await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { target = 20, minimumTarget = 10 })).EnsureSuccessStatusCode();
        Assert.Empty((await CheckInsAsync(c, id)).AsArray());
    }

    [Fact]
    public async Task The_date_window_matches_check_ins()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateWaterAsync(c);

        await AssertErrorAsync(await SetAsync(c, id, 8, "2030-03-05"), "date_out_of_range");
        await AssertErrorAsync(await SetAsync(c, id, 8, "2030-02-24"), "date_out_of_range");
        var edge = await SetOkAsync(c, id, 8, "2030-02-25");
        Assert.Equal("2030-02-25", (string?)edge["date"]);
        Assert.Equal("Done", (string?)edge["checkIn"]!["status"]);

        var list = await c.GetJsonAsync("/api/quantities?from=2030-02-01&to=2030-03-31");
        Assert.Equal("2030-02-25", (string?)list[0]!["date"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/quantities")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/quantities?from=2030-03-05&to=2030-03-01")).StatusCode);
    }

    [Fact]
    public async Task Validation_regular_habit_and_unknown_habit()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateWaterAsync(c);
        var regular = await CreateRegularAsync(c);

        await AssertErrorAsync(await SetAsync(c, regular, 3), "not_quantitative");
        await AssertErrorAsync(await SetAsync(c, id, -1), "invalid_quantity");
        await AssertErrorAsync(await SetAsync(c, id, 1.25m), "invalid_quantity");
        await AssertErrorAsync(await SetAsync(c, id, 100000.1m), "invalid_quantity");
        await AssertErrorAsync(await c.PutAsJsonAsync("/api/quantities", new { habitId = id }), "invalid_quantity");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/quantities", new { total = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetAsync(c, 9999, 3)).StatusCode);

        (await c.PostAsync($"/api/habits/{id}/archive", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await SetAsync(c, id, 3)).StatusCode);
    }

    [Fact]
    public async Task Works_with_an_API_key()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateWaterAsync(c);
        var key = (string)(await (await c.PostAsJsonAsync("/api/api-keys", new { name = "teste" })).JsonAsync())["key"]!;
        var bot = _factory.CreateClient();
        bot.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);

        var done = await SetOkAsync(bot, id, 8);
        Assert.Equal("Done", (string?)done["checkIn"]!["status"]);
        Assert.Equal(8m, (decimal)(await bot.GetJsonAsync($"/api/quantities?from={Today}&to={Today}&habitId={id}"))[0]!["total"]!);
    }

    [Fact]
    public async Task Quantities_are_isolated_per_user()
    {
        var a = await _factory.LoggedInClientAsync();
        var b = await _factory.LoggedInClientAsync();
        var idA = await CreateWaterAsync(a);
        await SetOkAsync(a, idA, 8);

        Assert.Equal(HttpStatusCode.NotFound, (await SetAsync(b, idA, 5)).StatusCode);
        Assert.Empty((await b.GetJsonAsync($"/api/quantities?from={Today}&to={Today}")).AsArray());
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/checkins/{idA}/{Today}")).StatusCode);
        Assert.Single((await a.GetJsonAsync($"/api/quantities?from={Today}&to={Today}")).AsArray());
    }
}
