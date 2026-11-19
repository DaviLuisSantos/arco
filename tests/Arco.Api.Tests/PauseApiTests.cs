using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Arco.Api.Tests;

public sealed class PauseApiTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static async Task<int> CreateHabitAsync(HttpClient c, string name = "Ler")
    {
        var res = await c.PostAsJsonAsync("/api/habits",
            new { name, frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static async Task AssertErrorAsync(HttpResponseMessage res, HttpStatusCode status, string code)
    {
        Assert.Equal(status, res.StatusCode);
        Assert.Equal(code, (string?)(await res.JsonAsync())["code"]);
    }

    [Fact]
    public async Task Pause_is_created_listed_and_cannot_overlap()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateHabitAsync(c);

        var created = await c.PostAsJsonAsync($"/api/habits/{id}/pauses",
            new { startDate = "2030-03-10", endDate = "2030-03-20", reason = "Viagem" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var json = await created.JsonAsync();
        Assert.Equal("2030-03-10", (string?)json["startDate"]);
        Assert.Equal("Viagem", (string?)json["reason"]);

        var habit = await c.GetJsonAsync($"/api/habits/{id}");
        Assert.Single(habit["pauses"]!.AsArray());

        await AssertErrorAsync(await c.PostAsJsonAsync($"/api/habits/{id}/pauses",
            new { startDate = "2030-03-20", endDate = "2030-03-25" }), HttpStatusCode.BadRequest, "pause_overlap");
        await AssertErrorAsync(await c.PostAsJsonAsync($"/api/habits/{id}/pauses",
            new { startDate = "2030-03-05" }), HttpStatusCode.BadRequest, "pause_overlap"); // open-ended reaches the existing one

        var ok = await c.PostAsJsonAsync($"/api/habits/{id}/pauses", new { startDate = "2030-03-21" });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);

        var pauseId = (int)json["id"]!;
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/habits/{id}/pauses/{pauseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"/api/habits/{id}/pauses/{pauseId}")).StatusCode);
    }

    [Fact]
    public async Task Pause_range_is_validated_and_archived_habits_are_refused()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateHabitAsync(c);

        await AssertErrorAsync(await c.PostAsJsonAsync($"/api/habits/{id}/pauses",
            new { startDate = "2030-02-20" }), HttpStatusCode.BadRequest, "pause_invalid_range"); // > 7 days ago
        await AssertErrorAsync(await c.PostAsJsonAsync($"/api/habits/{id}/pauses",
            new { startDate = "2030-03-10", endDate = "2030-03-09" }), HttpStatusCode.BadRequest, "pause_invalid_range");
        await AssertErrorAsync(await c.PostAsJsonAsync($"/api/habits/{id}/pauses",
            new { startDate = "2030-03-04", endDate = "2031-03-06" }), HttpStatusCode.BadRequest, "pause_invalid_range"); // 367 days
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync($"/api/habits/{id}/pauses",
            new { startDate = "2030-02-25", endDate = "2030-02-26" })).StatusCode); // exactly 7 days ago
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync("/api/habits/9999/pauses", new { startDate = "2030-03-04" })).StatusCode);

        await c.PostAsync($"/api/habits/{id}/archive", null);
        await AssertErrorAsync(await c.PostAsJsonAsync($"/api/habits/{id}/pauses",
            new { startDate = "2030-03-04" }), HttpStatusCode.BadRequest, "habit_archived");
    }

    [Fact]
    public async Task Resume_deletes_a_pause_starting_today_or_ends_it_yesterday()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateHabitAsync(c);

        await AssertErrorAsync(await c.PostAsync($"/api/habits/{id}/resume", null), HttpStatusCode.NotFound, "no_active_pause");

        await c.PostAsJsonAsync($"/api/habits/{id}/pauses", new { startDate = "2030-03-04" });
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/api/habits/{id}/resume", null)).StatusCode);
        Assert.Empty((await c.GetJsonAsync($"/api/habits/{id}"))["pauses"]!.AsArray());

        await c.PostAsJsonAsync($"/api/habits/{id}/pauses", new { startDate = "2030-03-01", endDate = "2030-03-31" });
        await c.PostAsJsonAsync($"/api/habits/{id}/pauses", new { startDate = "2030-04-01", endDate = "2030-04-05" });
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/api/habits/{id}/resume", null)).StatusCode);

        var pauses = (await c.GetJsonAsync($"/api/habits/{id}"))["pauses"]!.AsArray();
        // The one that ended yesterday is past (not listed); the future one stays untouched.
        Assert.Single(pauses);
        Assert.Equal("2030-04-01", (string?)pauses[0]!["startDate"]);
    }

    [Fact]
    public async Task Vacation_pauses_active_habits_and_ending_it_resumes_and_clears_future()
    {
        var c = await _factory.LoggedInClientAsync();
        var a = await CreateHabitAsync(c, "A");
        var b = await CreateHabitAsync(c, "B");
        var d = await CreateHabitAsync(c, "D");
        var archived = await CreateHabitAsync(c, "C");
        await c.PostAsync($"/api/habits/{archived}/archive", null);
        await c.PostAsJsonAsync($"/api/habits/{b}/pauses", new { startDate = "2030-03-06", endDate = "2030-03-08" });

        Assert.False((bool)(await c.GetJsonAsync("/api/vacation"))["active"]!);

        await AssertErrorAsync(await c.PostAsJsonAsync("/api/vacation", new { startDate = "2030-03-04" }),
            HttpStatusCode.BadRequest, "pause_invalid_range"); // end is required

        var started = await c.PostAsJsonAsync("/api/vacation", new { startDate = "2030-03-04", endDate = "2030-03-15" });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(2, (int)(await started.JsonAsync())["paused"]!); // B overlaps, archived is skipped

        var state = await c.GetJsonAsync("/api/vacation");
        Assert.True((bool)state["active"]!);
        Assert.Equal("2030-03-15", (string?)state["endDate"]);

        // Individual pauses (A's future one, B's own) are not touched when vacation ends.
        await c.PostAsJsonAsync($"/api/habits/{a}/pauses", new { startDate = "2030-05-01", endDate = "2030-05-03" });
        var ended = await c.DeleteAsync("/api/vacation");
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        Assert.Equal(2, (int)(await ended.JsonAsync())["resumed"]!); // A's and D's vacation pauses
        Assert.False((bool)(await c.GetJsonAsync("/api/vacation"))["active"]!);
        Assert.Single((await c.GetJsonAsync($"/api/habits/{a}"))["pauses"]!.AsArray());
        Assert.Single((await c.GetJsonAsync($"/api/habits/{b}"))["pauses"]!.AsArray());
        Assert.Empty((await c.GetJsonAsync($"/api/habits/{d}"))["pauses"]!.AsArray());
    }

    [Fact]
    public async Task Skipped_gives_no_xp_removes_previous_xp_and_keeps_the_note()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateHabitAsync(c);

        var done = await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-04", status = "Done" });
        Assert.Equal(10, (int)(await done.JsonAsync())["xpDelta"]!);

        var skipped = await c.PutAsJsonAsync("/api/checkins",
            new { habitId = id, date = "2030-03-04", status = "Skipped", note = "Gripe" });
        Assert.Equal(HttpStatusCode.OK, skipped.StatusCode);
        var json = await skipped.JsonAsync();
        Assert.Equal(-10, (int)json["xpDelta"]!);
        Assert.Equal(0, (int)json["player"]!["totalXp"]!);
        Assert.Equal("Skipped", (string?)json["checkIn"]!["status"]);
        Assert.Equal("Gripe", (string?)json["checkIn"]!["note"]);

        var list = (await c.GetJsonAsync($"/api/checkins?from=2030-03-04&to=2030-03-04")).AsArray();
        Assert.Equal("Gripe", (string?)list.Single()!["note"]);

        await AssertErrorAsync(await c.PutAsJsonAsync("/api/checkins",
            new { habitId = id, date = "2030-03-03", status = "Skipped", minutes = 5 }), HttpStatusCode.BadRequest, "validation");
        await AssertErrorAsync(await c.PutAsJsonAsync("/api/checkins",
            new { habitId = id, date = "2030-03-03", status = "Done", note = new string('x', 281) }), HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task Pauses_are_isolated_between_users_and_work_with_an_api_key()
    {
        var alice = await _factory.LoggedInClientAsync();
        var bob = await _factory.LoggedInClientAsync();
        var habit = await CreateHabitAsync(alice);

        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync($"/api/habits/{habit}/pauses", new { startDate = "2030-03-04" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/habits/{habit}/resume", null)).StatusCode);

        await alice.PostAsJsonAsync($"/api/habits/{habit}/pauses", new { startDate = "2030-03-04", endDate = "2030-03-09" });
        Assert.False((bool)(await bob.GetJsonAsync("/api/vacation"))["active"]!);
        Assert.Equal(0, (int)(await (await bob.DeleteAsync("/api/vacation")).JsonAsync())["resumed"]!);
        Assert.Single((await alice.GetJsonAsync($"/api/habits/{habit}"))["pauses"]!.AsArray());

        var key = (string)(await (await alice.PostAsJsonAsync("/api/api-keys", new { name = "Claude" })).JsonAsync())["key"]!;
        var api = _factory.CreateClient(new() { HandleCookies = false });
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        Assert.Equal(HttpStatusCode.NoContent, (await api.PostAsync($"/api/habits/{habit}/resume", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.PostAsJsonAsync("/api/vacation", new { startDate = "2030-03-04", endDate = "2030-03-10" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.DeleteAsync("/api/vacation")).StatusCode);
    }
}
