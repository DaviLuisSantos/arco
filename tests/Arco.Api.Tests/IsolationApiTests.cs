using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Arco.Api.Infrastructure.Push;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Arco.Api.Tests;

/// <summary>User B must never see or change what user A owns: not by id, not in lists, not through keys, MCP or export.</summary>
public sealed class IsolationApiTests : IDisposable
{
    // Sunday 2030-03-10, 12:00 in São Paulo: the current week can be reviewed.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 10, 15, 0, 0, TimeSpan.Zero));
    private const string Today = "2030-03-10";

    public void Dispose() => _factory.Dispose();

    /// <summary>What user A created, by id.</summary>
    private sealed record Owned(
        int HabitId, int CycleId, int GoalId, int MilestoneId, int FocusId, int KeyId, string Key, string Endpoint, string Email);

    private static object HabitBody(string name) =>
        new { name, frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" };

    private static object PushSubscription(string endpoint)
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return new
        {
            endpoint,
            keys = new
            {
                p256dh = Base64Url.EncodeToString(WebPushCrypto.ExportUncompressed(key.ExportParameters(false))),
                auth = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)),
            },
        };
    }

    private static async Task<int> IdOf(HttpResponseMessage res)
    {
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (int)(await res.JsonAsync())["id"]!;
    }

    /// <summary>User A gets one of everything.</summary>
    private async Task<(HttpClient Client, Owned Data)> CreateUserAAsync()
    {
        var a = _factory.CreateClient();
        var email = await _factory.RegisterAsync(a);

        var habitId = await IdOf(await a.PostAsJsonAsync("/api/habits", HabitBody("Ler")));
        var cycleId = await IdOf(await a.PostAsJsonAsync("/api/cycles", new { name = "Ciclo do A" }));
        var goal = await (await a.PostAsJsonAsync("/api/goals",
            new { cycleId, title = "Projeto do A", type = "Milestones", milestones = new[] { "Um", "Dois" } })).JsonAsync();
        var goalId = (int)goal["id"]!;
        var milestoneId = (int)goal["milestones"]![0]!["id"]!;

        Assert.Equal(HttpStatusCode.OK,
            (await a.PutAsJsonAsync("/api/checkins", new { habitId, date = Today, status = "Done", minutes = 30 })).StatusCode);
        var focus = await a.PostAsJsonAsync("/api/focus-sessions", new { habitId, minutes = 25 });
        var focusId = (int)(await focus.JsonAsync())["session"]!["id"]!;
        Assert.Equal(HttpStatusCode.Created,
            (await a.PostAsJsonAsync("/api/reviews/weekly", new { weekStart = "2030-03-04", wins = "Semana do A" })).StatusCode);

        var created = await (await a.PostAsJsonAsync("/api/api-keys", new { name = "Chave do A" })).JsonAsync();
        var endpoint = "https://push.example.com/user-a";
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync("/api/push/subscriptions", PushSubscription(endpoint))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await a.PutAsJsonAsync("/api/settings", new { timezone = "Asia/Tokyo" })).StatusCode);

        return (a, new Owned(habitId, cycleId, goalId, milestoneId, focusId, (int)created["id"]!, (string)created["key"]!, endpoint, email));
    }

    private static async Task AssertStatus(HttpStatusCode expected, Task<HttpResponseMessage> call, string what)
    {
        var res = await call;
        Assert.True(res.StatusCode == expected, $"{what}: expected {expected} but got {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task B_cannot_read_change_or_delete_anything_of_A_by_id_and_sees_empty_lists()
    {
        var (a, x) = await CreateUserAAsync();
        var b = await _factory.LoggedInClientAsync();

        // Lists are empty for B.
        Assert.Empty((await b.GetJsonAsync("/api/habits?includeArchived=true")).AsArray());
        Assert.Empty((await b.GetJsonAsync("/api/cycles")).AsArray());
        Assert.Empty((await b.GetJsonAsync("/api/goals")).AsArray());
        Assert.Empty((await b.GetJsonAsync("/api/reviews/weekly")).AsArray());
        Assert.Empty((await b.GetJsonAsync("/api/api-keys")).AsArray());
        var focus = await b.GetJsonAsync("/api/focus-sessions?from=2030-03-01&to=2030-03-31");
        Assert.Empty(focus["sessions"]!.AsArray());
        Assert.Equal(0, (int)focus["totalMinutes"]!);
        Assert.Equal(HttpStatusCode.NoContent, (await b.GetAsync("/api/cycles/current")).StatusCode);
        Assert.Null((await b.GetJsonAsync("/api/reviews/weekly/current"))["review"]);
        Assert.Equal(0, (int)(await b.GetJsonAsync("/api/push/config"))["subscriptions"]!);

        var today = await b.GetJsonAsync("/api/today");
        Assert.Empty(today["habits"]!.AsArray());
        Assert.Empty(today["goals"]!.AsArray());
        Assert.Equal(0, (int)(await b.GetJsonAsync("/api/player"))["totalXp"]!);
        Assert.All((await b.GetJsonAsync("/api/achievements")).AsArray(), ach => Assert.Null(ach!["unlockedAt"]));
        Assert.Equal("America/Sao_Paulo", (string?)(await b.GetJsonAsync("/api/settings"))["timezone"]);

        // By id: not found.
        var habit = $"/api/habits/{x.HabitId}";
        await AssertStatus(HttpStatusCode.NotFound, b.GetAsync(habit), "GET habit");
        await AssertStatus(HttpStatusCode.NotFound, b.PutAsJsonAsync(habit, HabitBody("Invadido")), "PUT habit");
        foreach (var action in new[] { "level-up", "level-down", "archive", "restore", "level-up-suggestion/dismiss", "level-down-suggestion/dismiss" })
            await AssertStatus(HttpStatusCode.NotFound,
                b.PostAsJsonAsync($"{habit}/{action}", new { levelDescription = "x", minimumDescription = "y" }), $"POST habit {action}");

        var cycle = $"/api/cycles/{x.CycleId}";
        await AssertStatus(HttpStatusCode.NotFound, b.GetAsync($"{cycle}/summary"), "GET cycle summary");
        await AssertStatus(HttpStatusCode.NotFound, b.GetAsync($"{cycle}/arch"), "GET cycle arch");
        await AssertStatus(HttpStatusCode.NotFound, b.PutAsJsonAsync(cycle, new { name = "Invadido" }), "PUT cycle");
        await AssertStatus(HttpStatusCode.NotFound, b.PostAsJsonAsync($"{cycle}/complete", new { wins = "x" }), "complete cycle");
        await AssertStatus(HttpStatusCode.NotFound, b.DeleteAsync(cycle), "DELETE cycle");

        var goal = $"/api/goals/{x.GoalId}";
        await AssertStatus(HttpStatusCode.NotFound, b.PutAsJsonAsync(goal, new { title = "Invadido" }), "PUT goal");
        await AssertStatus(HttpStatusCode.NotFound, b.PostAsJsonAsync($"{goal}/progress", new { value = 1 }), "goal progress");
        await AssertStatus(HttpStatusCode.NotFound, b.PostAsync($"{goal}/milestones/{x.MilestoneId}/toggle", null), "toggle milestone");
        await AssertStatus(HttpStatusCode.NotFound, b.PostAsync($"{goal}/complete", null), "complete goal");
        await AssertStatus(HttpStatusCode.NotFound, b.DeleteAsync(goal), "DELETE goal");

        await AssertStatus(HttpStatusCode.NotFound,
            b.PutAsJsonAsync("/api/checkins", new { habitId = x.HabitId, date = Today, status = "Done" }), "PUT check-in");
        await AssertStatus(HttpStatusCode.NotFound, b.DeleteAsync($"/api/checkins/{x.HabitId}/{Today}"), "DELETE check-in");
        await AssertStatus(HttpStatusCode.NotFound,
            b.PostAsJsonAsync("/api/focus-sessions", new { habitId = x.HabitId, minutes = 25 }), "POST focus on A's habit");
        await AssertStatus(HttpStatusCode.NotFound, b.DeleteAsync($"/api/focus-sessions/{x.FocusId}"), "DELETE focus");
        await AssertStatus(HttpStatusCode.NotFound, b.DeleteAsync($"/api/api-keys/{x.KeyId}"), "DELETE api key");

        // B removing A's endpoint does not drop A's subscription.
        await b.PostAsJsonAsync("/api/push/subscriptions/remove", new { endpoint = x.Endpoint });

        // Nothing of A's changed.
        var after = await a.GetJsonAsync(habit);
        Assert.Equal("Ler", (string?)after["name"]);
        Assert.Null(after["archivedAt"]);
        Assert.Equal("Ciclo do A", (string?)(await a.GetJsonAsync("/api/cycles/current"))["name"]);
        Assert.Equal("Projeto do A", (string?)(await a.GetJsonAsync("/api/goals")).AsArray().Single()!["title"]);
        Assert.Equal(2, (await a.GetJsonAsync("/api/focus-sessions?from=2030-03-01&to=2030-03-31"))["sessions"]!.AsArray().Count); // manual (check-in) + timer
        Assert.Single((await a.GetJsonAsync("/api/api-keys")).AsArray());
        Assert.Equal(1, (int)(await a.GetJsonAsync("/api/push/config"))["subscriptions"]!);
        Assert.Single((await a.GetJsonAsync("/api/reviews/weekly")).AsArray());
        Assert.True((int)(await a.GetJsonAsync("/api/player"))["totalXp"]! > 0);
        Assert.Equal("Asia/Tokyo", (string?)(await a.GetJsonAsync("/api/settings"))["timezone"]);
        Assert.Contains((await a.GetJsonAsync("/api/achievements")).AsArray(), ach => ach!["unlockedAt"] is not null);
    }

    [Fact]
    public async Task B_works_on_their_own_data_without_touching_A()
    {
        var (a, x) = await CreateUserAAsync();
        var b = await _factory.LoggedInClientAsync();

        // Same shape of data, own ids and own XP.
        var habitId = await IdOf(await b.PostAsJsonAsync("/api/habits", HabitBody("Correr")));
        Assert.NotEqual(x.HabitId, habitId);
        Assert.Equal(HttpStatusCode.OK,
            (await b.PutAsJsonAsync("/api/checkins", new { habitId, date = Today, status = "Done" })).StatusCode);
        Assert.Equal(10, (int)(await b.GetJsonAsync("/api/player"))["totalXp"]!);
        Assert.Single((await b.GetJsonAsync("/api/habits")).AsArray());

        // Each user can have an active cycle and one weekly review for the same week.
        Assert.Equal(HttpStatusCode.Created, (await b.PostAsJsonAsync("/api/cycles", new { name = "Ciclo do B" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await b.PostAsJsonAsync("/api/reviews/weekly", new { weekStart = "2030-03-04", wins = "Semana do B" })).StatusCode);

        Assert.Equal("Ciclo do A", (string?)(await a.GetJsonAsync("/api/cycles/current"))["name"]);
        Assert.Equal("Ler", (string?)(await a.GetJsonAsync("/api/habits")).AsArray().Single()!["name"]);
        Assert.Equal("Semana do A", (string?)(await a.GetJsonAsync("/api/reviews/weekly")).AsArray().Single()!["wins"]);

        // Account settings are per user too.
        Assert.Equal(HttpStatusCode.OK, (await b.PutAsJsonAsync("/api/settings", new { timezone = "Europe/Lisbon" })).StatusCode);
        Assert.Equal("Asia/Tokyo", (string?)(await a.GetJsonAsync("/api/settings"))["timezone"]);
    }

    [Fact]
    public async Task Keys_and_the_export_only_reach_the_owner()
    {
        var (a, x) = await CreateUserAAsync();
        var b = await _factory.LoggedInClientAsync();
        await b.PostAsJsonAsync("/api/habits", HabitBody("Correr"));
        var keyB = (string)(await (await b.PostAsJsonAsync("/api/api-keys", new { name = "Chave do B" })).JsonAsync())["key"]!;

        HttpClient KeyClient(string key)
        {
            var client = _factory.CreateClient(new() { HandleCookies = false });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            return client;
        }

        var apiA = KeyClient(x.Key);
        var apiB = KeyClient(keyB);
        Assert.Equal(x.Email, (string?)(await apiA.GetJsonAsync("/api/auth/me"))["email"]);
        Assert.NotEqual(x.Email, (string?)(await apiB.GetJsonAsync("/api/auth/me"))["email"]);
        Assert.Equal("Ler", (string?)(await apiA.GetJsonAsync("/api/habits")).AsArray().Single()!["name"]);
        Assert.Equal("Correr", (string?)(await apiB.GetJsonAsync("/api/habits")).AsArray().Single()!["name"]);
        await AssertStatus(HttpStatusCode.NotFound, apiB.GetAsync($"/api/habits/{x.HabitId}"), "key B GET habit of A");

        // Export: only the caller's rows, in the cookie session and with a key.
        foreach (var (client, name, other) in new[] { (a, "Ler", "Correr"), (b, "Correr", "Ler"), (apiB, "Correr", "Ler") })
        {
            var res = await client.GetAsync("/api/backup");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var text = await res.Content.ReadAsStringAsync();
            var export = JsonNode.Parse(text)!;
            Assert.Equal(name, (string?)export["habits"]!.AsArray().Single()!["name"]);
            Assert.DoesNotContain(other, text.Replace("Correr do", ""));
            Assert.Single(export["apiKeys"]!.AsArray());
            Assert.DoesNotContain("keyHash", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("passwordHash", text, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("push.example.com", await (await a.GetAsync("/api/backup")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mcp_tools_only_see_the_data_of_the_key_owner()
    {
        var (_, x) = await CreateUserAAsync();
        var b = await _factory.LoggedInClientAsync();
        await b.PostAsJsonAsync("/api/habits", HabitBody("Correr"));
        var keyB = (string)(await (await b.PostAsJsonAsync("/api/api-keys", new { name = "MCP do B" })).JsonAsync())["key"]!;

        async Task<List<string>> HabitNamesAsync(string key)
        {
            var http = _factory.CreateClient(new() { HandleCookies = false });
            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, "/api/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {key}" },
            }, http);
            await using var mcp = await McpClient.CreateAsync(transport);
            var result = await mcp.CallToolAsync("listar_habitos", new Dictionary<string, object?> { ["incluirArquivados"] = true });
            Assert.True(result.IsError is not true);
            var json = JsonNode.Parse(string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text)))!;
            return json.AsArray().Select(h => (string)h!["name"]!).ToList();
        }

        Assert.Equal(["Ler"], await HabitNamesAsync(x.Key));
        Assert.Equal(["Correr"], await HabitNamesAsync(keyB));
    }
}
