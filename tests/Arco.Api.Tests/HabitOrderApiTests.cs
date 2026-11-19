using System.Net;
using System.Net.Http.Json;

namespace Arco.Api.Tests;

public sealed class HabitOrderApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

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

    private static async Task AssertErrorAsync(HttpResponseMessage res, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(code, (string?)(await res.JsonAsync())["code"]);
    }

    private static async Task<string[]> NamesAsync(HttpClient c) =>
        (await c.GetJsonAsync("/api/habits")).AsArray().Select(h => (string)h!["name"]!).ToArray();

    [Fact]
    public async Task A_new_habit_defaults_to_anytime_and_goes_to_the_end_of_its_period()
    {
        var c = await _factory.LoggedInClientAsync();
        var plain = await c.PostAsJsonAsync("/api/habits",
            new { name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        var json = await plain.JsonAsync();
        Assert.Equal("Anytime", (string?)json["timeOfDay"]);
        Assert.Null(json["anchorHabitId"]);
        Assert.Null(json["anchorText"]);
        Assert.Equal(0, (int)json["sortOrder"]!);

        await CreateAsync(c, "Meditar", new { timeOfDay = "Morning" });
        var second = await c.PostAsJsonAsync("/api/habits", new
        {
            name = "Alongar", frequencyType = "Daily", levelDescription = "5 min", minimumDescription = "1 min", timeOfDay = "Morning",
        });
        Assert.Equal(1, (int)(await second.JsonAsync())["sortOrder"]!);
    }

    [Fact]
    public async Task Habits_are_listed_in_the_order_of_the_day()
    {
        var c = await _factory.LoggedInClientAsync();
        await CreateAsync(c, "Qualquer");
        await CreateAsync(c, "Noite", new { timeOfDay = "Evening" });
        await CreateAsync(c, "Manha Z", new { timeOfDay = "Morning" });
        await CreateAsync(c, "Manha A", new { timeOfDay = "Morning" });
        await CreateAsync(c, "Tarde", new { timeOfDay = "Afternoon" });

        // Same period: creation order (SortOrder), not the name.
        Assert.Equal(["Manha Z", "Manha A", "Tarde", "Noite", "Qualquer"], await NamesAsync(c));
    }

    [Fact]
    public async Task A_habit_can_be_created_with_a_habit_anchor_and_follows_it()
    {
        var c = await _factory.LoggedInClientAsync();
        var coffee = await CreateAsync(c, "Café", new { timeOfDay = "Morning" });
        await CreateAsync(c, "Diário", new { timeOfDay = "Morning" });
        var stretch = await CreateAsync(c, "Alongar", new { timeOfDay = "Morning", anchorHabitId = coffee });

        var detail = await c.GetJsonAsync($"/api/habits/{stretch}");
        Assert.Equal(coffee, (int)detail["anchorHabitId"]!);
        Assert.Null(detail["anchorText"]);
        Assert.Equal(["Café", "Alongar", "Diário"], await NamesAsync(c));
    }

    [Fact]
    public async Task A_habit_can_have_a_text_anchor_and_replacing_it_clears_the_other()
    {
        var c = await _factory.LoggedInClientAsync();
        var other = await CreateAsync(c, "Outro");
        var id = await CreateAsync(c, "Vitamina", new { anchorText = "  Depois do café  " });
        Assert.Equal("Depois do café", (string?)(await c.GetJsonAsync($"/api/habits/{id}"))["anchorText"]);

        var put = await c.PutAsJsonAsync($"/api/habits/{id}", new
        {
            name = "Vitamina", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min", anchorHabitId = other,
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var json = await put.JsonAsync();
        Assert.Equal(other, (int)json["anchorHabitId"]!);
        Assert.Null(json["anchorText"]);

        var tooLong = await c.PostAsJsonAsync("/api/habits", new
        {
            name = "X", frequencyType = "Daily", levelDescription = "a", minimumDescription = "b", anchorText = new string('a', 61),
        });
        await AssertErrorAsync(tooLong, "validation");
    }

    [Fact]
    public async Task A_put_without_the_new_fields_keeps_them_and_an_empty_text_removes_the_anchor()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateAsync(c, "Vitamina", new { timeOfDay = "Morning", anchorText = "o café" });
        var basic = new { name = "Vitamina D", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" };

        // An older client (no timeOfDay / anchor fields) does not reset the day order.
        var kept = await (await c.PutAsJsonAsync($"/api/habits/{id}", basic)).JsonAsync();
        Assert.Equal("Morning", (string?)kept["timeOfDay"]);
        Assert.Equal("o café", (string?)kept["anchorText"]);

        var removed = await (await c.PutAsJsonAsync($"/api/habits/{id}", new
        {
            basic.name, basic.frequencyType, basic.levelDescription, basic.minimumDescription, anchorText = "",
        })).JsonAsync();
        Assert.Equal("Morning", (string?)removed["timeOfDay"]);
        Assert.Null(removed["anchorText"]);
        Assert.Null(removed["anchorHabitId"]);
    }

    [Fact]
    public async Task Habit_and_text_anchors_together_are_a_conflict()
    {
        var c = await _factory.LoggedInClientAsync();
        var a = await CreateAsync(c, "A");
        var res = await c.PostAsJsonAsync("/api/habits", new
        {
            name = "B", frequencyType = "Daily", levelDescription = "a", minimumDescription = "b", anchorHabitId = a, anchorText = "algo",
        });
        await AssertErrorAsync(res, "anchor_conflict");
        Assert.Single(await NamesAsync(c));
    }

    [Fact]
    public async Task Anchors_must_exist_belong_to_the_user_and_not_be_the_habit_itself()
    {
        var c = await _factory.LoggedInClientAsync();
        var other = await _factory.LoggedInClientAsync();
        var foreign = await CreateAsync(other, "De outro usuário");
        var mine = await CreateAsync(c, "Meu");

        object Body(int anchor) => new
        {
            name = "Meu", frequencyType = "Daily", levelDescription = "a", minimumDescription = "b", anchorHabitId = anchor,
        };
        await AssertErrorAsync(await c.PostAsJsonAsync("/api/habits", Body(foreign)), "validation");
        await AssertErrorAsync(await c.PostAsJsonAsync("/api/habits", Body(99999)), "validation");
        await AssertErrorAsync(await c.PutAsJsonAsync($"/api/habits/{mine}", Body(mine)), "validation");
    }

    [Fact]
    public async Task A_loop_of_anchors_is_refused()
    {
        var c = await _factory.LoggedInClientAsync();
        var a = await CreateAsync(c, "A");
        var b = await CreateAsync(c, "B", new { anchorHabitId = a });
        var d = await CreateAsync(c, "D", new { anchorHabitId = b });

        object Body(string name, int anchor) => new
        {
            name, frequencyType = "Daily", levelDescription = "a", minimumDescription = "b", anchorHabitId = anchor,
        };
        await AssertErrorAsync(await c.PutAsJsonAsync($"/api/habits/{a}", Body("A", b)), "anchor_cycle");
        await AssertErrorAsync(await c.PutAsJsonAsync($"/api/habits/{a}", Body("A", d)), "anchor_cycle");
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/habits/{d}", Body("D", a))).StatusCode);
    }

    [Fact]
    public async Task Reorder_saves_the_manual_order_of_a_period()
    {
        var c = await _factory.LoggedInClientAsync();
        var a = await CreateAsync(c, "A", new { timeOfDay = "Morning" });
        var b = await CreateAsync(c, "B", new { timeOfDay = "Morning" });
        var d = await CreateAsync(c, "D", new { timeOfDay = "Morning" });
        await CreateAsync(c, "Noite", new { timeOfDay = "Evening" });

        var res = await c.PutAsJsonAsync("/api/habits/order", new { timeOfDay = "Morning", habitIds = new[] { d, a, b } });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        Assert.Equal(["D", "A", "B", "Noite"], await NamesAsync(c));
        var list = (await c.GetJsonAsync("/api/habits")).AsArray();
        Assert.Equal([0, 1, 2], list.Take(3).Select(h => (int)h!["sortOrder"]!).ToArray());
    }

    [Fact]
    public async Task Reorder_requires_exactly_the_active_habits_of_the_period()
    {
        var c = await _factory.LoggedInClientAsync();
        var a = await CreateAsync(c, "A", new { timeOfDay = "Morning" });
        var b = await CreateAsync(c, "B", new { timeOfDay = "Morning" });
        var evening = await CreateAsync(c, "Noite", new { timeOfDay = "Evening" });
        var archived = await CreateAsync(c, "Arquivado", new { timeOfDay = "Morning" });
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/habits/{archived}/archive", null)).StatusCode);

        async Task<HttpResponseMessage> Put(string period, int[] ids) =>
            await c.PutAsJsonAsync("/api/habits/order", new { timeOfDay = period, habitIds = ids });

        await AssertErrorAsync(await Put("Morning", [a]), "order_mismatch"); // missing one
        await AssertErrorAsync(await Put("Morning", [a, b, evening]), "order_mismatch"); // another period
        await AssertErrorAsync(await Put("Morning", [a, b, archived]), "order_mismatch"); // archived
        await AssertErrorAsync(await Put("Morning", [a, a]), "order_mismatch"); // duplicate
        await AssertErrorAsync(await Put("Afternoon", [a]), "order_mismatch"); // empty period
        Assert.Equal(HttpStatusCode.NoContent, (await Put("Afternoon", [])).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Put("Morning", [b, a])).StatusCode);
    }

    [Fact]
    public async Task Reorder_does_not_touch_other_users_habits()
    {
        var c = await _factory.LoggedInClientAsync();
        var other = await _factory.LoggedInClientAsync();
        var mine = await CreateAsync(c, "Meu", new { timeOfDay = "Morning" });
        var foreign = await CreateAsync(other, "Alheio", new { timeOfDay = "Morning" });

        await AssertErrorAsync(
            await c.PutAsJsonAsync("/api/habits/order", new { timeOfDay = "Morning", habitIds = new[] { mine, foreign } }), "order_mismatch");
        Assert.Equal(HttpStatusCode.NoContent,
            (await c.PutAsJsonAsync("/api/habits/order", new { timeOfDay = "Morning", habitIds = new[] { mine } })).StatusCode);
    }

    [Fact]
    public async Task Changing_the_period_sends_the_habit_to_the_end_of_the_new_one()
    {
        var c = await _factory.LoggedInClientAsync();
        var a = await CreateAsync(c, "A", new { timeOfDay = "Morning" });
        await CreateAsync(c, "B", new { timeOfDay = "Evening" });
        await CreateAsync(c, "C", new { timeOfDay = "Evening" });

        var put = await c.PutAsJsonAsync($"/api/habits/{a}", new
        {
            name = "A", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min", timeOfDay = "Evening",
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var json = await put.JsonAsync();
        Assert.Equal("Evening", (string?)json["timeOfDay"]);
        Assert.Equal(2, (int)json["sortOrder"]!);
        Assert.Equal(["B", "C", "A"], await NamesAsync(c));

        // Saving without changing the period keeps the position.
        var again = await c.PutAsJsonAsync($"/api/habits/{a}", new
        {
            name = "A2", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min", timeOfDay = "Evening",
        });
        Assert.Equal(2, (int)(await again.JsonAsync())["sortOrder"]!);
    }

    [Fact]
    public async Task Archiving_the_anchor_keeps_the_link_and_the_export_has_the_new_fields()
    {
        var c = await _factory.LoggedInClientAsync();
        var a = await CreateAsync(c, "A", new { timeOfDay = "Morning" });
        var b = await CreateAsync(c, "B", new { timeOfDay = "Morning", anchorHabitId = a });
        await CreateAsync(c, "C", new { timeOfDay = "Evening", anchorText = "Depois do jantar" });
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/habits/{a}/archive", null)).StatusCode);

        Assert.Equal(a, (int)(await c.GetJsonAsync($"/api/habits/{b}"))["anchorHabitId"]!);

        var habits = (await c.GetJsonAsync("/api/backup"))["habits"]!.AsArray();
        var exportedB = habits.Single(h => (int)h!["id"]! == b)!;
        Assert.Equal("Morning", (string?)exportedB["timeOfDay"]);
        Assert.Equal(a, (int)exportedB["anchorHabitId"]!);
        Assert.Equal(1, (int)exportedB["sortOrder"]!);
        var exportedC = habits.Single(h => (string?)h!["name"] == "C")!;
        Assert.Equal("Depois do jantar", (string?)exportedC["anchorText"]);
        Assert.Equal("Evening", (string?)exportedC["timeOfDay"]);
    }
}
