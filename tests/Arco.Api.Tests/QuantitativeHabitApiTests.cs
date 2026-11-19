using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Arco.Api.Tests;

public sealed class QuantitativeHabitApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static Dictionary<string, object?> Body(string name = "Água", object? extra = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = name, ["frequencyType"] = "Daily", ["unit"] = "copos", ["levelTarget"] = 8, ["levelMinimumTarget"] = 4,
        };
        if (extra is not null)
            foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        return body;
    }

    private static async Task<JsonNode> CreateAsync(HttpClient c, Dictionary<string, object?> body)
    {
        var res = await c.PostAsJsonAsync("/api/habits", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await res.JsonAsync();
    }

    private static async Task AssertInvalidAsync(HttpClient c, Dictionary<string, object?> body)
    {
        var res = await c.PostAsJsonAsync("/api/habits", body);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task A_quantitative_habit_gets_automatic_descriptions_and_numbers_in_the_dto()
    {
        var c = await _factory.LoggedInClientAsync();
        var json = await CreateAsync(c, Body());

        Assert.Equal("copos", (string?)json["unit"]);
        Assert.Equal(8m, (decimal)json["target"]!);
        Assert.Equal(4m, (decimal)json["minimumTarget"]!);
        Assert.Equal("8 copos", (string?)json["levelDescription"]);
        Assert.Equal("4 copos", (string?)json["minimumDescription"]);
        var level = json["levels"]![0]!;
        Assert.Equal(8m, (decimal)level["target"]!);
        Assert.Equal(4m, (decimal)level["minimumTarget"]!);

        var read = await c.GetJsonAsync($"/api/habits/{(int)json["id"]!}");
        Assert.Equal("8 copos", (string?)read["levelDescription"]);
        Assert.Equal(4m, (decimal)read["minimumTarget"]!);
    }

    [Fact]
    public async Task Decimal_goals_use_a_comma_in_the_text_and_typed_descriptions_are_kept()
    {
        var c = await _factory.LoggedInClientAsync();
        var json = await CreateAsync(c, new Dictionary<string, object?>
        {
            ["name"] = "Corrida", ["frequencyType"] = "Daily", ["unit"] = "km", ["levelTarget"] = 2.5, ["levelMinimumTarget"] = 0.5,
        });
        Assert.Equal("2,5 km", (string?)json["levelDescription"]);
        Assert.Equal("0,5 km", (string?)json["minimumDescription"]);
        Assert.Equal(2.5m, (decimal)json["target"]!);

        var typed = await CreateAsync(c, Body("Ler", new { unit = "páginas", levelDescription = "Ler 8 páginas", minimumDescription = "Ler 4" }));
        Assert.Equal("Ler 8 páginas", (string?)typed["levelDescription"]);
        Assert.Equal("Ler 4", (string?)typed["minimumDescription"]);
        Assert.Equal(8m, (decimal)typed["target"]!);
    }

    [Fact]
    public async Task A_regular_habit_still_needs_descriptions_and_has_no_numbers()
    {
        var c = await _factory.LoggedInClientAsync();
        await AssertInvalidAsync(c, new Dictionary<string, object?> { ["name"] = "Ler", ["frequencyType"] = "Daily" });

        var json = await CreateAsync(c, new Dictionary<string, object?>
        {
            ["name"] = "Ler", ["frequencyType"] = "Daily", ["levelDescription"] = "10 min", ["minimumDescription"] = "2 min",
            ["levelTarget"] = 5, ["levelMinimumTarget"] = 1,
        });
        Assert.Null(json["unit"]);
        Assert.Null(json["target"]);
        Assert.Null(json["minimumTarget"]);
    }

    [Theory]
    [InlineData("unit", "unidade-muito-longa-demais")]
    [InlineData("levelTarget", null)]
    [InlineData("levelTarget", 0)]
    [InlineData("levelTarget", -1)]
    [InlineData("levelTarget", 2.55)]
    [InlineData("levelTarget", 100000.1)]
    [InlineData("levelMinimumTarget", null)]
    [InlineData("levelMinimumTarget", 0)]
    [InlineData("levelMinimumTarget", 4.25)]
    [InlineData("levelMinimumTarget", 9)]
    public async Task Invalid_unit_or_numbers_are_refused(string field, object? value)
    {
        var c = await _factory.LoggedInClientAsync();
        var body = Body();
        body[field] = value;
        await AssertInvalidAsync(c, body);
    }

    [Fact]
    public async Task The_limits_are_accepted()
    {
        var c = await _factory.LoggedInClientAsync();
        var json = await CreateAsync(c, new Dictionary<string, object?>
        {
            ["name"] = "Passos", ["frequencyType"] = "Daily", ["unit"] = "0123456789abcdef",
            ["levelTarget"] = 100000, ["levelMinimumTarget"] = 100000,
        });
        Assert.Equal(100000m, (decimal)json["target"]!);
    }

    [Fact]
    public async Task The_ladder_carries_goals_and_fills_automatic_descriptions()
    {
        var c = await _factory.LoggedInClientAsync();
        var json = await CreateAsync(c, Body(extra: new
        {
            plannedLevels = new object[]
            {
                new { target = 10, minimumTarget = 5 },
                new { target = 12.5, minimumTarget = 6, description = "Doze e meio", minimumDescription = "Seis" },
            },
        }));

        var planned = json["plannedLevels"]!.AsArray();
        Assert.Equal(2, planned.Count);
        Assert.Equal(2, (int)planned[0]!["level"]!);
        Assert.Equal("10 copos", (string?)planned[0]!["description"]);
        Assert.Equal("5 copos", (string?)planned[0]!["minimumDescription"]);
        Assert.Equal(10m, (decimal)planned[0]!["target"]!);
        Assert.Equal(5m, (decimal)planned[0]!["minimumTarget"]!);
        Assert.Equal("Doze e meio", (string?)planned[1]!["description"]);
        Assert.Equal(12.5m, (decimal)planned[1]!["target"]!);
    }

    [Fact]
    public async Task A_quantitative_ladder_needs_valid_numbers_in_every_level()
    {
        var c = await _factory.LoggedInClientAsync();
        await AssertInvalidAsync(c, Body(extra: new { plannedLevels = new object[] { new { description = "10 copos", minimumDescription = "5 copos" } } }));
        await AssertInvalidAsync(c, Body(extra: new { plannedLevels = new object[] { new { target = 5, minimumTarget = 6 } } }));
        await AssertInvalidAsync(c, Body(extra: new { plannedLevels = new object[] { new { target = 5.55, minimumTarget = 1 } } }));
    }

    [Fact]
    public async Task Leveling_up_uses_the_planned_level_goals_or_the_request()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = (int)(await CreateAsync(c, Body(extra: new
        {
            plannedLevels = new object[] { new { target = 10, minimumTarget = 5 } },
        })))["id"]!;

        var up = await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { });
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        var habit = (await up.JsonAsync())["habit"]!;
        Assert.Equal(2, (int)habit["level"]!);
        Assert.Equal(10m, (decimal)habit["target"]!);
        Assert.Equal(5m, (decimal)habit["minimumTarget"]!);
        Assert.Equal("10 copos", (string?)habit["levelDescription"]);
        Assert.Equal(2, habit["levels"]!.AsArray().Count);
        Assert.Equal(8m, (decimal)habit["levels"]![0]!["target"]!);

        // No planned level left: the request brings the numbers.
        var third = await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { target = 12.5, minimumTarget = 6 });
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        var level3 = (await third.JsonAsync())["habit"]!;
        Assert.Equal(12.5m, (decimal)level3["target"]!);
        Assert.Equal("12,5 copos", (string?)level3["levelDescription"]);
        Assert.Equal("6 copos", (string?)level3["minimumDescription"]);
    }

    [Fact]
    public async Task Leveling_up_without_numbers_or_with_invalid_ones_is_refused()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = (int)(await CreateAsync(c, Body()))["id"]!;

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { target = 5, minimumTarget = 6 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { target = 5.55, minimumTarget = 1 })).StatusCode);
        Assert.Equal(1, (int)(await c.GetJsonAsync($"/api/habits/{id}"))["level"]!);
    }

    [Fact]
    public async Task Going_back_a_level_puts_its_goals_back_on_top_of_the_ladder()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = (int)(await CreateAsync(c, Body(extra: new
        {
            plannedLevels = new object[] { new { target = 10, minimumTarget = 5 } },
        })))["id"]!;
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { })).StatusCode);

        var down = await c.PostAsync($"/api/habits/{id}/level-down", null);
        Assert.Equal(HttpStatusCode.OK, down.StatusCode);
        var habit = await down.JsonAsync();
        Assert.Equal(1, (int)habit["level"]!);
        Assert.Equal(8m, (decimal)habit["target"]!);
        var planned = habit["plannedLevels"]!.AsArray();
        Assert.Single(planned);
        Assert.Equal(10m, (decimal)planned[0]!["target"]!);
        Assert.Equal(5m, (decimal)planned[0]!["minimumTarget"]!);

        // And the ladder works again: leveling up returns to level 2 with the same goals.
        var up = await (await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { })).JsonAsync();
        Assert.Equal(10m, (decimal)up["habit"]!["target"]!);
    }

    [Fact]
    public async Task A_regular_habit_can_become_quantitative_and_back_keeping_the_history()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = (int)(await CreateAsync(c, new Dictionary<string, object?>
        {
            ["name"] = "Ler", ["frequencyType"] = "Daily", ["levelDescription"] = "Ler um pouco", ["minimumDescription"] = "Abrir o livro",
        }))["id"]!;

        // Without numbers the conversion is refused.
        var refused = await c.PutAsJsonAsync($"/api/habits/{id}", new Dictionary<string, object?>
        {
            ["name"] = "Ler", ["frequencyType"] = "Daily", ["unit"] = "páginas",
        });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var converted = await c.PutAsJsonAsync($"/api/habits/{id}", new Dictionary<string, object?>
        {
            ["name"] = "Ler", ["frequencyType"] = "Daily", ["unit"] = "páginas", ["levelTarget"] = 20, ["levelMinimumTarget"] = 5,
        });
        Assert.Equal(HttpStatusCode.OK, converted.StatusCode);
        var json = await converted.JsonAsync();
        Assert.Equal("páginas", (string?)json["unit"]);
        Assert.Equal(20m, (decimal)json["target"]!);
        Assert.Equal("20 páginas", (string?)json["levelDescription"]);

        var back = await c.PutAsJsonAsync($"/api/habits/{id}", new Dictionary<string, object?>
        {
            ["name"] = "Ler", ["frequencyType"] = "Daily", ["levelDescription"] = "Ler 20 páginas", ["minimumDescription"] = "Ler 5",
            ["unit"] = "",
        });
        Assert.Equal(HttpStatusCode.OK, back.StatusCode);
        var plain = await back.JsonAsync();
        Assert.Null(plain["unit"]);
        Assert.Null(plain["target"]);
        Assert.Null(plain["minimumTarget"]);
        Assert.Null(plain["levels"]![0]!["target"]);
        Assert.Equal("Ler 20 páginas", (string?)plain["levelDescription"]);
        Assert.Single(plain["levels"]!.AsArray());
    }

    [Fact]
    public async Task Switching_back_to_a_regular_habit_drops_the_goals_of_every_level_and_of_the_ladder()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = (int)(await CreateAsync(c, Body(extra: new
        {
            plannedLevels = new object[] { new { target = 10, minimumTarget = 5 } },
        })))["id"]!;
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/habits/{id}/level-up", new { })).StatusCode);

        var back = await c.PutAsJsonAsync($"/api/habits/{id}", new Dictionary<string, object?>
        {
            ["name"] = "Água", ["frequencyType"] = "Daily", ["levelDescription"] = "Beber bastante", ["minimumDescription"] = "Beber um pouco",
            ["unit"] = "",
        });
        Assert.Equal(HttpStatusCode.OK, back.StatusCode);
        var json = await back.JsonAsync();
        Assert.All(json["levels"]!.AsArray(), l => Assert.Null(l!["target"]));
        Assert.Equal(2, json["levels"]!.AsArray().Count);
    }

    [Fact]
    public async Task A_put_without_unit_or_goals_keeps_the_habit_quantitative()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = (int)(await CreateAsync(c, Body()))["id"]!;

        // An older client (no unit / levelTarget / levelMinimumTarget) only renames the habit.
        var put = await c.PutAsJsonAsync($"/api/habits/{id}", new Dictionary<string, object?>
        {
            ["name"] = "Água filtrada", ["frequencyType"] = "Daily", ["levelDescription"] = "8 copos", ["minimumDescription"] = "4 copos",
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var json = await put.JsonAsync();
        Assert.Equal("Água filtrada", (string?)json["name"]);
        Assert.Equal("copos", (string?)json["unit"]);
        Assert.Equal(8, (decimal)json["target"]!);
        Assert.Equal(4, (decimal)json["minimumTarget"]!);
    }

    [Fact]
    public async Task The_export_has_the_unit_the_goals_and_the_day_totals_collection()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = (int)(await CreateAsync(c, Body(extra: new
        {
            plannedLevels = new object[] { new { target = 10, minimumTarget = 5 } },
        })))["id"]!;

        var export = await c.GetJsonAsync("/api/backup");
        var habit = export["habits"]!.AsArray().Single(h => (int)h!["id"]! == id)!;
        Assert.Equal("copos", (string?)habit["unit"]);

        var level = export["habitLevels"]!.AsArray().Single(l => (int)l!["habitId"]! == id)!;
        Assert.Equal(80, (int)level["targetTenths"]!);
        Assert.Equal(40, (int)level["minimumTenths"]!);

        var planned = export["habitPlannedLevels"]!.AsArray().Single(l => (int)l!["habitId"]! == id)!;
        Assert.Equal(100, (int)planned["targetTenths"]!);
        Assert.Equal(50, (int)planned["minimumTenths"]!);

        Assert.NotNull(export["habitDayQuantities"]);
        Assert.Empty(export["habitDayQuantities"]!.AsArray());
    }
}
