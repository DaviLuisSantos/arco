using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Arco.Api.Tests;

public sealed class CycleArchApiTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static async Task<int> CreateHabitAsync(HttpClient client, string name)
    {
        var res = await client.PostAsJsonAsync("/api/habits",
            new { name, emoji = "📚", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static async Task<int> CreateCycleAsync(HttpClient client, string start = "2030-02-25", int weeks = 2)
    {
        var res = await client.PostAsJsonAsync("/api/cycles", new { name = "Ciclo", startDate = start, weeks });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static async Task FocusAsync(HttpClient client, int habitId, int minutes, string date)
    {
        var res = await client.PostAsJsonAsync("/api/focus-sessions", new { habitId, minutes, date });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Groups_sessions_by_week_with_day_7_and_8_on_the_boundary()
    {
        var client = await _factory.LoggedInClientAsync();
        var reading = await CreateHabitAsync(client, "Ler");
        var writing = await CreateHabitAsync(client, "Escrever");
        await CreateHabitAsync(client, "Sem foco");
        var id = await CreateCycleAsync(client); // 2030-02-25 .. 2030-03-10

        await FocusAsync(client, reading, 30, "2030-02-25");  // day 1, week 1
        await FocusAsync(client, reading, 20, "2030-03-03");  // day 7, week 1
        await FocusAsync(client, reading, 40, "2030-03-04");  // day 8, week 2
        await FocusAsync(client, writing, 15, "2030-03-04");  // day 8, week 2

        var arch = await client.GetJsonAsync($"/api/cycles/{id}/arch");
        Assert.Equal(id, (int)arch["cycleId"]!);
        Assert.Equal("2030-02-25", (string?)arch["startDate"]);
        Assert.Equal("2030-03-10", (string?)arch["endDate"]);
        Assert.False((bool)arch["closed"]!);
        Assert.Equal(2, (int)arch["weeks"]!);
        Assert.Equal("Arch", (string?)arch["layout"]);
        Assert.Equal(420, (int)arch["weeklyTargetMinutes"]!);
        Assert.Equal(105, (int)arch["totalMinutes"]!);

        var habits = arch["habits"]!.AsArray();
        Assert.Equal(2, habits.Count); // only habits with sessions in the cycle
        Assert.Equal(reading, (int)habits[0]!["habitId"]!);
        Assert.Equal("Ler", (string?)habits[0]!["name"]);
        Assert.Equal("📚", (string?)habits[0]!["emoji"]);
        Assert.Equal(writing, (int)habits[1]!["habitId"]!);

        var stones = arch["stones"]!.AsArray();
        Assert.Equal(2, stones.Count);

        var w1 = stones[0]!;
        Assert.Equal(1, (int)w1["week"]!);
        Assert.Equal("2030-02-25", (string?)w1["from"]);
        Assert.Equal("2030-03-03", (string?)w1["to"]);
        Assert.Equal(50, (int)w1["minutes"]!);
        Assert.Equal(7, w1["days"]!.AsArray().Count);
        Assert.Equal(30, (int)w1["days"]![0]!["minutes"]!);
        Assert.Equal("2030-03-03", (string?)w1["days"]![6]!["date"]);
        Assert.Equal(20, (int)w1["days"]![6]!["minutes"]!);
        var w1Habit = w1["byHabit"]!.AsArray().Single()!;
        Assert.Equal(reading, (int)w1Habit["habitId"]!);
        Assert.Equal(50, (int)w1Habit["minutes"]!);

        var w2 = stones[1]!;
        Assert.Equal(2, (int)w2["week"]!);
        Assert.Equal("2030-03-04", (string?)w2["from"]);
        Assert.Equal("2030-03-10", (string?)w2["to"]);
        Assert.Equal(55, (int)w2["minutes"]!);
        var day8 = w2["days"]![0]!;
        Assert.Equal("2030-03-04", (string?)day8["date"]);
        Assert.Equal(55, (int)day8["minutes"]!);
        var bricks = day8["bricks"]!.AsArray();
        Assert.Equal(2, bricks.Count);
        Assert.Equal(reading, (int)bricks[0]!["habitId"]!);
        Assert.Equal(40, (int)bricks[0]!["minutes"]!);
        Assert.Equal("Timer", (string?)bricks[0]!["source"]);
        Assert.True((int)bricks[0]!["sessionId"]! > 0);
        Assert.Equal(2, w2["byHabit"]!.AsArray().Count);
    }

    [Fact]
    public async Task Future_days_and_weeks_are_empty()
    {
        var client = await _factory.LoggedInClientAsync();
        var habit = await CreateHabitAsync(client, "Ler");
        var id = await CreateCycleAsync(client, "2030-03-04", weeks: 4); // starts today
        await FocusAsync(client, habit, 25, "2030-03-04");

        var arch = await client.GetJsonAsync($"/api/cycles/{id}/arch");
        var stones = arch["stones"]!.AsArray();
        Assert.Equal(4, stones.Count);
        Assert.Equal(25, (int)stones[0]!["minutes"]!);
        foreach (var future in stones.Skip(1))
        {
            Assert.Equal(0, (int)future!["minutes"]!);
            Assert.Empty(future["byHabit"]!.AsArray());
            var days = future["days"]!.AsArray();
            Assert.Equal(7, days.Count);
            Assert.All(days, d =>
            {
                Assert.Equal(0, (int)d!["minutes"]!);
                Assert.Empty(d["bricks"]!.AsArray());
            });
        }
        // Empty days of the current week are listed too.
        Assert.Equal(0, (int)stones[0]!["days"]![6]!["minutes"]!);
    }

    [Fact]
    public async Task A_completed_cycle_is_closed_and_keeps_its_stones()
    {
        var client = await _factory.LoggedInClientAsync();
        var habit = await CreateHabitAsync(client, "Ler");
        var id = await CreateCycleAsync(client);
        await FocusAsync(client, habit, 45, "2030-03-01");

        Assert.True((await client.PostAsJsonAsync($"/api/cycles/{id}/complete", new { })).IsSuccessStatusCode);

        var arch = await client.GetJsonAsync($"/api/cycles/{id}/arch");
        Assert.True((bool)arch["closed"]!);
        Assert.Equal(45, (int)arch["totalMinutes"]!);
        Assert.Equal(2, arch["stones"]!.AsArray().Count);
    }

    [Fact]
    public async Task A_cycle_that_is_not_a_whole_even_number_of_weeks_uses_the_row_layout()
    {
        var client = await _factory.LoggedInClientAsync();
        var habit = await CreateHabitAsync(client, "Ler");
        var id = await CreateCycleAsync(client); // 14 days

        // 18 days -> 3 weeks, the last one partial (4 days).
        var put = await client.PutAsJsonAsync($"/api/cycles/{id}", new { name = "Ciclo", endDate = "2030-03-14" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        await FocusAsync(client, habit, 10, "2030-03-04");

        var arch = await client.GetJsonAsync($"/api/cycles/{id}/arch");
        Assert.Equal(3, (int)arch["weeks"]!);
        Assert.Equal("Row", (string?)arch["layout"]);
        var stones = arch["stones"]!.AsArray();
        Assert.Equal(3, stones.Count);
        Assert.Equal(7, stones[1]!["days"]!.AsArray().Count);
        var last = stones[2]!;
        Assert.Equal("2030-03-11", (string?)last["from"]);
        Assert.Equal("2030-03-14", (string?)last["to"]);
        Assert.Equal(4, last["days"]!.AsArray().Count);
        Assert.Equal(10, (int)stones[1]!["minutes"]!);

        // Back to 4 weeks: even again.
        await client.PutAsJsonAsync($"/api/cycles/{id}", new { name = "Ciclo", endDate = "2030-03-24" });
        var four = await client.GetJsonAsync($"/api/cycles/{id}/arch");
        Assert.Equal(4, (int)four["weeks"]!);
        Assert.Equal("Arch", (string?)four["layout"]);
    }

    [Fact]
    public async Task Unknown_cycle_is_404()
    {
        var client = await _factory.LoggedInClientAsync();
        var res = await client.GetAsync("/api/cycles/999/arch");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("Ciclo não encontrado.", (string?)(await res.JsonAsync())["error"]);
    }

    [Fact]
    public async Task Requires_login_and_accepts_an_api_key()
    {
        var anon = _factory.CreateClient(new() { HandleCookies = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/cycles/1/arch")).StatusCode);

        var app = await _factory.LoggedInClientAsync();
        var id = await CreateCycleAsync(app);
        var created = await (await app.PostAsJsonAsync("/api/api-keys", new { name = "Claude" })).JsonAsync();

        var api = _factory.CreateClient(new() { HandleCookies = false });
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (string)created["key"]!);
        var arch = await api.GetJsonAsync($"/api/cycles/{id}/arch");
        Assert.Equal(id, (int)arch["cycleId"]!);
    }
}
