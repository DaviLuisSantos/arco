using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace Arco.Api.Tests;

public sealed class ApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Login_rejects_wrong_password_and_accepts_right_one()
    {
        var email = await _factory.RegisterAsync(_factory.CreateClient());
        var client = _factory.CreateClient();

        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "errada-errada" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("invalid_credentials", (string?)(await wrong.JsonAsync())["code"]);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        var right = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.DefaultPassword });
        Assert.Equal(HttpStatusCode.NoContent, right.StatusCode);
        Assert.Contains(right.Headers.GetValues("Set-Cookie"), c => c.StartsWith("arco_auth=") && c.Contains("httponly"));

        var me = await client.GetJsonAsync("/api/auth/me");
        Assert.Equal(email, (string?)me["email"]);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_is_rate_limited()
    {
        var client = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/auth/login", new { email = "x@teste.arco", password = "errada-errada" });

        var res = await client.PostAsJsonAsync("/api/auth/login", new { email = "x@teste.arco", password = "errada-errada" });
        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
        Assert.Equal("rate_limited", (string?)(await res.JsonAsync())["code"]);
    }

    [Fact]
    public async Task Api_requires_cookie_and_never_redirects()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var res = await client.GetAsync("/api/today");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("unauthorized", (string?)(await res.JsonAsync())["code"]);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/logout", null)).StatusCode);

        var health = await client.GetJsonAsync("/api/health");
        Assert.Equal("healthy", (string?)health["status"]);
        Assert.Equal("0.1.0", (string?)health["version"]);
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Unknown_api_route_is_json_404_and_malformed_json_is_400()
    {
        var client = await _factory.LoggedInClientAsync();

        var missing = await client.GetAsync("/api/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("not_found", (string?)(await missing.JsonAsync())["code"]);

        var bad = await client.PutAsync("/api/checkins",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("validation", (string?)(await bad.JsonAsync())["code"]);

        var badEnum = await client.PutAsync("/api/checkins",
            new StringContent("""{"habitId":1,"date":"2026-01-01","status":"Nope"}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, badEnum.StatusCode);
    }

    [Fact]
    public async Task Habit_checkin_flow_updates_xp_today_and_reverts()
    {
        var client = await _factory.LoggedInClientAsync();
        var habit = await CreateHabitAsync(client, "Correr");
        var habitId = (int)habit["id"]!;
        Assert.Equal(1, (int)habit["level"]!);
        Assert.Equal("Daily", (string?)habit["frequencyType"]);

        var today = (string)(await client.GetJsonAsync("/api/today"))["date"]!;

        // Done → +10
        var done = await PutCheckInAsync(client, habitId, today, "Done");
        Assert.Equal(10, (int)done["xpDelta"]!);
        Assert.Equal(10, (int)done["checkIn"]!["xp"]!);
        Assert.Equal(10, (int)done["player"]!["totalXp"]!);

        var view = await client.GetJsonAsync("/api/today");
        Assert.Equal(10, (int)view["xpToday"]!);
        var h = view["habits"]!.AsArray().Single(x => (int)x!["id"]! == habitId)!;
        Assert.Equal("Done", (string?)h["checkIn"]!["status"]);
        Assert.True((bool)h["isDueToday"]!);
        Assert.Equal(1, (int)h["weekCount"]!);
        Assert.Null(h["weekTarget"]);

        // Done → Minimum: −5
        var minimum = await PutCheckInAsync(client, habitId, today, "Minimum");
        Assert.Equal(-5, (int)minimum["xpDelta"]!);
        Assert.Equal(5, (int)minimum["player"]!["totalXp"]!);
        Assert.Equal(5, (int)(await client.GetJsonAsync("/api/today"))["xpToday"]!);

        // Delete → back to 0
        var del = await client.DeleteAsync($"/api/checkins/{habitId}/{today}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        var delBody = await del.JsonAsync();
        Assert.Equal(-5, (int)delBody["xpDelta"]!);
        Assert.Equal(0, (int)delBody["player"]!["totalXp"]!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/checkins/{habitId}/{today}")).StatusCode);

        var after = await client.GetJsonAsync("/api/today");
        Assert.Equal(0, (int)after["xpToday"]!);
        Assert.Null(after["habits"]!.AsArray().Single()!["checkIn"]);

        // Future date is rejected.
        var future = DateOnly.Parse(today).AddDays(1).ToString("yyyy-MM-dd");
        var res = await client.PutAsJsonAsync("/api/checkins", new { habitId, date = future, status = "Done" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var range = await client.GetJsonAsync($"/api/checkins?from={today}&to={today}");
        Assert.Empty(range.AsArray());
    }

    [Fact]
    public async Task Level_up_gives_50_and_raises_checkin_xp()
    {
        var client = await _factory.LoggedInClientAsync();
        var habitId = (int)(await CreateHabitAsync(client, "Ler"))["id"]!;

        var res = await client.PostAsJsonAsync($"/api/habits/{habitId}/level-up",
            new { levelDescription = "30 páginas", minimumDescription = "5 páginas" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.JsonAsync();
        Assert.Equal(50, (int)body["xpDelta"]!);
        Assert.Equal(50, (int)body["player"]!["totalXp"]!);
        Assert.Equal(2, (int)body["habit"]!["level"]!);
        Assert.Equal("30 páginas", (string?)body["habit"]!["levelDescription"]);
        Assert.Equal(2, body["habit"]!["levels"]!.AsArray().Count);

        var today = (string)(await client.GetJsonAsync("/api/today"))["date"]!;
        var done = await PutCheckInAsync(client, habitId, today, "Done");
        Assert.Equal(12, (int)done["xpDelta"]!);
    }

    [Fact]
    public async Task Creating_a_second_active_cycle_conflicts()
    {
        var client = await _factory.LoggedInClientAsync();

        var first = await client.PostAsJsonAsync("/api/cycles", new { name = "Ciclo 1" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var cycle = await first.JsonAsync();
        Assert.Equal(42, (int)cycle["totalDays"]!);
        Assert.Equal(1, (int)cycle["dayNumber"]!);
        Assert.Equal("Active", (string?)cycle["status"]);

        var second = await client.PostAsJsonAsync("/api/cycles", new { name = "Ciclo 2" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("active_cycle_exists", (string?)(await second.JsonAsync())["code"]);

        var complete = await client.PostAsJsonAsync($"/api/cycles/{(int)cycle["id"]!}/complete", new { reviewNotes = "Bom ciclo" });
        var completed = await complete.JsonAsync();
        Assert.Equal(100, (int)completed["xpDelta"]!);
        Assert.EndsWith("Z", (string)completed["cycle"]!["completedAt"]!);

        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/cycles/current")).StatusCode);
    }

    [Fact]
    public async Task Milestone_toggles_award_and_revert_xp()
    {
        var client = await _factory.LoggedInClientAsync();
        var cycle = await (await client.PostAsJsonAsync("/api/cycles", new { name = "Ciclo" })).JsonAsync();

        var created = await client.PostAsJsonAsync("/api/goals", new
        {
            cycleId = (int)cycle["id"]!,
            title = "Lançar o app",
            type = "Milestones",
            milestones = new[] { "Backend", "Frontend" },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var goal = await created.JsonAsync();
        var goalId = (int)goal["id"]!;
        Assert.Equal((string?)cycle["endDate"], (string?)goal["deadline"]);
        var milestones = goal["milestones"]!.AsArray();
        var m1 = (int)milestones[0]!["id"]!;
        var m2 = (int)milestones[1]!["id"]!;

        var t1 = await ToggleAsync(client, goalId, m1);
        Assert.Equal(25, (int)t1["xpDelta"]!);
        Assert.Equal(0.5, (double)t1["goal"]!["progress"]!);
        Assert.Null(t1["goal"]!["completedAt"]);

        var t2 = await ToggleAsync(client, goalId, m2);
        Assert.Equal(175, (int)t2["xpDelta"]!);
        Assert.NotNull(t2["goal"]!["completedAt"]);
        Assert.Equal(1.0, (double)t2["goal"]!["progress"]!);
        Assert.Equal(200, (int)t2["player"]!["totalXp"]!);

        var t3 = await ToggleAsync(client, goalId, m2);
        Assert.Equal(-175, (int)t3["xpDelta"]!);
        Assert.Null(t3["goal"]!["completedAt"]);
        Assert.Equal(25, (int)t3["player"]!["totalXp"]!);

        var player = await client.GetJsonAsync("/api/player");
        Assert.Equal(25, (int)player["totalXp"]!);
        Assert.Single(player["recentEvents"]!.AsArray());
        Assert.Equal("Milestone", (string?)player["recentEvents"]![0]!["source"]);

        var today = await client.GetJsonAsync("/api/today");
        Assert.Single(today["goals"]!.AsArray());
        Assert.Equal(25, (int)today["xpToday"]!);
    }

    [Fact]
    public async Task Numeric_goal_completes_when_decreasing_target_is_reached()
    {
        var client = await _factory.LoggedInClientAsync();
        var cycle = await (await client.PostAsJsonAsync("/api/cycles", new { name = "Ciclo" })).JsonAsync();
        var goal = await (await client.PostAsJsonAsync("/api/goals", new
        {
            cycleId = (int)cycle["id"]!, title = "Peso", type = "Numeric", unit = "kg", startValue = 90, targetValue = 80,
        })).JsonAsync();
        var goalId = (int)goal["id"]!;

        var p1 = await (await client.PostAsJsonAsync($"/api/goals/{goalId}/progress", new { value = 85 })).JsonAsync();
        Assert.Equal(0, (int)p1["xpDelta"]!);
        Assert.Equal(0.5, (double)p1["goal"]!["progress"]!);
        Assert.Equal(85, (double)p1["goal"]!["currentValue"]!);

        var p2 = await (await client.PostAsJsonAsync($"/api/goals/{goalId}/progress", new { value = 79.5 })).JsonAsync();
        Assert.Equal(150, (int)p2["xpDelta"]!);
        Assert.NotNull(p2["goal"]!["completedAt"]);

        var again = await client.PostAsync($"/api/goals/{goalId}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Settings_validate_iana_timezone()
    {
        var client = await _factory.LoggedInClientAsync();
        Assert.Equal("America/Sao_Paulo", (string?)(await client.GetJsonAsync("/api/settings"))["timezone"]);

        var bad = await client.PutAsJsonAsync("/api/settings", new { timezone = "Mars/Olympus" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var ok = await client.PutAsJsonAsync("/api/settings", new { timezone = "Europe/Lisbon" });
        Assert.Equal("Europe/Lisbon", (string?)(await ok.JsonAsync())["timezone"]);
    }

    private static async Task<JsonNode> CreateHabitAsync(HttpClient client, string name)
    {
        var res = await client.PostAsJsonAsync("/api/habits", new
        {
            name,
            emoji = "🏃",
            frequencyType = "Daily",
            levelDescription = "20 min",
            minimumDescription = "5 min",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await res.JsonAsync();
    }

    private static async Task<JsonNode> PutCheckInAsync(HttpClient client, int habitId, string date, string status)
    {
        var res = await client.PutAsJsonAsync("/api/checkins", new { habitId, date, status });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.JsonAsync();
    }

    private static async Task<JsonNode> ToggleAsync(HttpClient client, int goalId, int milestoneId)
    {
        var res = await client.PostAsync($"/api/goals/{goalId}/milestones/{milestoneId}/toggle", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.JsonAsync();
    }
}
