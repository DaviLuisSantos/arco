using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Arco.Api.Tests;

public sealed class FocusApiTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static async Task<int> CreateHabitAsync(HttpClient client, string name = "Ler")
    {
        var res = await client.PostAsJsonAsync("/api/habits",
            new { name, emoji = "📚", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    [Fact]
    public async Task Requires_login()
    {
        var anon = _factory.CreateClient(new() { HandleCookies = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/focus-sessions?from=2030-03-01&to=2030-03-04")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/focus-sessions", new { habitId = 1, minutes = 10 })).StatusCode);
    }

    [Fact]
    public async Task Creates_lists_and_deletes_sessions()
    {
        var client = await _factory.LoggedInClientAsync();
        var habitId = await CreateHabitAsync(client);

        var res = await client.PostAsJsonAsync("/api/focus-sessions", new { habitId, minutes = 25 });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var session = (await res.JsonAsync())["session"]!;
        Assert.Equal("2030-03-04", (string?)session["date"]);
        Assert.Equal(25, (int)session["minutes"]!);
        Assert.Equal("Timer", (string?)session["source"]);
        Assert.Null(session["tree"]);
        var id = (int)session["id"]!;

        await client.PostAsJsonAsync("/api/focus-sessions", new { habitId, minutes = 60, date = "2030-03-02" });

        var list = await client.GetJsonAsync("/api/focus-sessions?from=2030-03-01&to=2030-03-04");
        Assert.Equal(2, list["sessions"]!.AsArray().Count);
        Assert.Equal(85, (int)list["totalMinutes"]!);
        var by = list["byHabit"]!.AsArray().Single()!;
        Assert.Equal(habitId, (int)by["habitId"]!);
        Assert.Equal("Ler", (string?)by["name"]);
        Assert.Equal(85, (int)by["minutes"]!);
        Assert.Equal(2, (int)by["sessions"]!);

        var other = await client.GetJsonAsync($"/api/focus-sessions?from=2030-03-01&to=2030-03-04&habitId={habitId + 99}");
        Assert.Equal(0, (int)other["totalMinutes"]!);

        Assert.True((await client.DeleteAsync($"/api/focus-sessions/{id}")).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/focus-sessions/{id}")).StatusCode);
        var after = await client.GetJsonAsync("/api/focus-sessions?from=2030-03-01&to=2030-03-04");
        Assert.Equal(60, (int)after["totalMinutes"]!);
    }

    [Fact]
    public async Task Rejects_invalid_requests()
    {
        var client = await _factory.LoggedInClientAsync();
        var habitId = await CreateHabitAsync(client);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/focus-sessions", new { habitId, minutes = 10, date = "2030-03-05" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/focus-sessions", new { habitId, minutes = 10, date = "2030-02-24" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/focus-sessions", new { habitId, minutes = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/focus-sessions", new { habitId, minutes = 481 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/focus-sessions", new { habitId = habitId + 99, minutes = 10 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/focus-sessions?from=2030-03-04&to=2030-03-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/focus-sessions?from=2020-01-01&to=2030-03-01")).StatusCode);

        await client.PostAsync($"/api/habits/{habitId}/archive", null);
        var archived = await client.PostAsJsonAsync("/api/focus-sessions", new { habitId, minutes = 10 });
        Assert.Equal(HttpStatusCode.BadRequest, archived.StatusCode);
    }

    [Fact]
    public async Task Works_with_an_api_key()
    {
        var app = await _factory.LoggedInClientAsync();
        var key = (string)(await (await app.PostAsJsonAsync("/api/api-keys", new { name = "Claude" })).JsonAsync())["key"]!;
        var api = _factory.CreateClient(new() { HandleCookies = false });
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var habitId = await CreateHabitAsync(api);
        var res = await api.PostAsJsonAsync("/api/focus-sessions", new { habitId, minutes = 30 });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
