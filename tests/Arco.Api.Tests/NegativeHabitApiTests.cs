using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Arco.Api.Tests;

public sealed class NegativeHabitApiTests : IDisposable
{
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static async Task<JsonNode> CreateNegativeAsync(HttpClient c, object? extra = null, string name = "Não fumar")
    {
        var body = new Dictionary<string, object?> { ["name"] = name, ["kind"] = "Negative", ["frequencyType"] = "Daily" };
        if (extra is not null)
            foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        var res = await c.PostAsJsonAsync("/api/habits", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await res.JsonAsync();
    }

    private static async Task AssertErrorAsync(HttpResponseMessage res, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(code, (string?)(await res.JsonAsync())["code"]);
    }

    [Fact]
    public async Task Creating_a_negative_habit_fills_the_descriptions_and_a_regular_one_is_positive()
    {
        var c = await _factory.LoggedInClientAsync();
        var json = await CreateNegativeAsync(c);
        Assert.Equal("Negative", (string?)json["kind"]);
        Assert.Equal("Daily", (string?)json["frequencyType"]);
        Assert.Equal("Sem recaída", (string?)json["levelDescription"]);
        Assert.Equal("Sem recaída", (string?)json["minimumDescription"]);
        Assert.Null(json["unit"]);

        var typed = await CreateNegativeAsync(c, new { levelDescription = "Zero cigarros", minimumDescription = "No máximo 1" }, "Cigarro");
        Assert.Equal("Zero cigarros", (string?)typed["levelDescription"]);
        Assert.Equal("No máximo 1", (string?)typed["minimumDescription"]);

        var positive = await c.PostAsJsonAsync("/api/habits", new
        {
            name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min",
        });
        Assert.Equal("Positive", (string?)(await positive.JsonAsync())["kind"]);
    }

    [Fact]
    public async Task A_negative_habit_is_forced_daily_and_refuses_other_frequencies()
    {
        var c = await _factory.LoggedInClientAsync();
        // Frequency omitted: it is Daily.
        var omitted = await c.PostAsJsonAsync("/api/habits", new { name = "Sem açúcar", kind = "Negative" });
        Assert.Equal(HttpStatusCode.Created, omitted.StatusCode);
        Assert.Equal("Daily", (string?)(await omitted.JsonAsync())["frequencyType"]);

        await AssertErrorAsync(await c.PostAsJsonAsync("/api/habits",
            new { name = "X", kind = "Negative", frequencyType = "WeeklyTarget", timesPerWeek = 3 }), "validation");
        await AssertErrorAsync(await c.PostAsJsonAsync("/api/habits",
            new { name = "X", kind = "Negative", frequencyType = "SpecificDays", frequencyDays = new[] { 1, 3 } }), "validation");
    }

    [Fact]
    public async Task A_negative_habit_refuses_a_unit()
    {
        var c = await _factory.LoggedInClientAsync();
        await AssertErrorAsync(await c.PostAsJsonAsync("/api/habits", new
        {
            name = "Café", kind = "Negative", frequencyType = "Daily", unit = "xícaras", levelTarget = 1, levelMinimumTarget = 1,
        }), "validation");
    }

    [Fact]
    public async Task The_kind_cannot_change_and_an_absent_kind_keeps_it()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = (int)(await CreateNegativeAsync(c))["id"]!;

        await AssertErrorAsync(await c.PutAsJsonAsync($"/api/habits/{id}",
            new { name = "Não fumar", kind = "Positive", frequencyType = "Daily", levelDescription = "a", minimumDescription = "b" }),
            "kind_change");

        var kept = await c.PutAsJsonAsync($"/api/habits/{id}", new { name = "Sem cigarro", frequencyType = "Daily" });
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        var json = await kept.JsonAsync();
        Assert.Equal("Negative", (string?)json["kind"]);
        Assert.Equal("Sem cigarro", (string?)json["name"]);
        Assert.Equal("Sem recaída", (string?)json["levelDescription"]);

        var same = await c.PutAsJsonAsync($"/api/habits/{id}", new { name = "Sem cigarro", kind = "Negative", frequencyType = "Daily" });
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);

        // A positive habit cannot become negative either.
        var positive = await c.PostAsJsonAsync("/api/habits", new
        {
            name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min",
        });
        var positiveId = (int)(await positive.JsonAsync())["id"]!;
        await AssertErrorAsync(await c.PutAsJsonAsync($"/api/habits/{positiveId}", new
        {
            name = "Ler", kind = "Negative", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min",
        }), "kind_change");
        var keptPositive = await c.PutAsJsonAsync($"/api/habits/{positiveId}", new
        {
            name = "Ler mais", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min",
        });
        Assert.Equal("Positive", (string?)(await keptPositive.JsonAsync())["kind"]);
    }

    [Fact]
    public async Task Check_in_accepts_clean_relapse_and_skipped_but_not_minimum()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = (int)(await CreateNegativeAsync(c))["id"]!;

        await AssertErrorAsync(await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-04", status = "Minimum" }),
            "invalid_status");

        var clean = await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-04", status = "Done" });
        Assert.Equal(HttpStatusCode.OK, clean.StatusCode);
        Assert.Equal(10, (int)(await clean.JsonAsync())["xpDelta"]!);

        var relapse = await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-04", status = "Missed" });
        Assert.Equal(HttpStatusCode.OK, relapse.StatusCode);
        Assert.Equal(-10, (int)(await relapse.JsonAsync())["xpDelta"]!);

        var skipped = await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-03", status = "Skipped" });
        Assert.Equal(HttpStatusCode.OK, skipped.StatusCode);
    }

    [Fact]
    public async Task Minimum_is_still_valid_for_a_positive_habit()
    {
        var c = await _factory.LoggedInClientAsync();
        var res = await c.PostAsJsonAsync("/api/habits", new
        {
            name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min",
        });
        var id = (int)(await res.JsonAsync())["id"]!;
        var ci = await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-04", status = "Minimum" });
        Assert.Equal(HttpStatusCode.OK, ci.StatusCode);
    }

    [Fact]
    public async Task Level_up_of_a_negative_habit_uses_the_milestone_as_the_automatic_text()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = (int)(await CreateNegativeAsync(c))["id"]!;

        var up = await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { });
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        var habit = (await up.JsonAsync())["habit"]!;
        Assert.Equal(2, (int)habit["level"]!);
        Assert.Equal("7 dias limpo", (string?)habit["levelDescription"]);
        Assert.Equal("Sem recaída", (string?)habit["minimumDescription"]);

        var second = await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { });
        var secondHabit = (await second.JsonAsync())["habit"]!;
        Assert.Equal(3, (int)secondHabit["level"]!);
        Assert.Equal("14 dias limpo", (string?)secondHabit["levelDescription"]);

        // Typed texts win over the automatic one.
        var typed = await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { levelDescription = "Um mês limpo" });
        Assert.Equal("Um mês limpo", (string?)(await typed.JsonAsync())["habit"]!["levelDescription"]);
    }

    [Fact]
    public async Task The_export_has_the_kind()
    {
        var c = await _factory.LoggedInClientAsync();
        var negative = (int)(await CreateNegativeAsync(c))["id"]!;
        var positive = (int)(await (await c.PostAsJsonAsync("/api/habits", new
        {
            name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min",
        })).JsonAsync())["id"]!;

        var habits = (await c.GetJsonAsync("/api/backup"))["habits"]!.AsArray();
        Assert.Equal("Negative", (string?)habits.Single(h => (int)h!["id"]! == negative)!["kind"]);
        Assert.Equal("Positive", (string?)habits.Single(h => (int)h!["id"]! == positive)!["kind"]);
    }
}
