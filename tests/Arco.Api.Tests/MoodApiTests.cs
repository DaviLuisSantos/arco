using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Arco.Api.Tests;

public sealed class MoodApiTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static async Task AssertErrorAsync(HttpResponseMessage res, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(code, (string?)(await res.JsonAsync())["code"]);
    }

    [Fact]
    public async Task Upsert_changes_lists_and_deletes()
    {
        var c = await _factory.LoggedInClientAsync();

        var today = await c.PutAsJsonAsync("/api/moods", new { value = 4 });
        Assert.Equal(HttpStatusCode.OK, today.StatusCode);
        var json = await today.JsonAsync();
        Assert.Equal("2030-03-04", (string?)json["date"]);
        Assert.Equal(4, (int)json["value"]!);

        // Changing replaces (still one row for the day).
        await c.PutAsJsonAsync("/api/moods", new { date = "2030-03-04", value = 2 });
        await c.PutAsJsonAsync("/api/moods", new { date = "2030-03-02", value = 5 });

        var list = (await c.GetJsonAsync("/api/moods")).AsArray();
        Assert.Equal(2, list.Count);
        Assert.Equal("2030-03-02", (string?)list[0]!["date"]);
        Assert.Equal(2, (int)list[1]!["value"]!);

        var ranged = (await c.GetJsonAsync("/api/moods?from=2030-03-03&to=2030-03-04")).AsArray();
        Assert.Single(ranged);

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync("/api/moods/2030-03-04")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync("/api/moods/2030-03-04")).StatusCode);
        Assert.Single((await c.GetJsonAsync("/api/moods")).AsArray());
    }

    [Fact]
    public async Task Value_and_date_window_are_validated()
    {
        var c = await _factory.LoggedInClientAsync();

        await AssertErrorAsync(await c.PutAsJsonAsync("/api/moods", new { value = 0 }), "mood_invalid_value");
        await AssertErrorAsync(await c.PutAsJsonAsync("/api/moods", new { value = 6 }), "mood_invalid_value");
        await AssertErrorAsync(await c.PutAsJsonAsync("/api/moods", new { }), "mood_invalid_value");

        await AssertErrorAsync(await c.PutAsJsonAsync("/api/moods", new { date = "2030-03-05", value = 3 }), "date_out_of_range");
        await AssertErrorAsync(await c.PutAsJsonAsync("/api/moods", new { date = "2030-02-24", value = 3 }), "date_out_of_range"); // 8 days ago
        Assert.Equal(HttpStatusCode.OK,
            (await c.PutAsJsonAsync("/api/moods", new { date = "2030-02-25", value = 3 })).StatusCode); // exactly 7 days ago
    }

    [Fact]
    public async Task Moods_are_isolated_between_users()
    {
        var a = await _factory.LoggedInClientAsync();
        var b = await _factory.LoggedInClientAsync();

        await a.PutAsJsonAsync("/api/moods", new { value = 5 });
        Assert.Empty((await b.GetJsonAsync("/api/moods")).AsArray());
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync("/api/moods/2030-03-04")).StatusCode);

        await b.PutAsJsonAsync("/api/moods", new { value = 1 });
        Assert.Equal(5, (int)(await a.GetJsonAsync("/api/moods")).AsArray().Single()!["value"]!);
    }

    [Fact]
    public async Task Export_includes_daily_moods()
    {
        var c = await _factory.LoggedInClientAsync();
        await c.PutAsJsonAsync("/api/moods", new { value = 3 });

        var export = (await c.GetJsonAsync("/api/backup"))["dailyMoods"]!.AsArray();
        Assert.Equal("2030-03-04", (string?)export.Single()!["date"]);
        Assert.Equal(3, (int)export.Single()!["value"]!);
    }

    [Fact]
    public async Task An_api_key_can_register_moods()
    {
        var app = await _factory.LoggedInClientAsync();
        var key = (string)(await (await app.PostAsJsonAsync("/api/api-keys", new { name = "Claude" })).JsonAsync())["key"]!;
        var api = _factory.CreateClient(new() { HandleCookies = false });
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);

        Assert.Equal(HttpStatusCode.OK, (await api.PutAsJsonAsync("/api/moods", new { value = 4 })).StatusCode);
        Assert.Single((await api.GetJsonAsync("/api/moods")).AsArray());
        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync("/api/moods/2030-03-04")).StatusCode);
    }
}
