using System.Net;
using System.Net.Http.Json;

namespace Arco.Api.Tests;

public sealed class StatsPauseTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static async Task<int> CreateHabitAsync(HttpClient c)
    {
        var res = await c.PostAsJsonAsync("/api/habits",
            new { name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private static async Task CheckInAsync(HttpClient c, int id, string date, string status) =>
        Assert.Equal(HttpStatusCode.OK,
            (await c.PutAsJsonAsync("/api/checkins", new { habitId = id, date, status })).StatusCode);

    [Fact]
    public async Task Skipped_leaves_expected_and_is_not_counted_as_a_check_in()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateHabitAsync(c);
        await CheckInAsync(c, id, "2030-03-02", "Done");
        await CheckInAsync(c, id, "2030-03-03", "Skipped");
        await CheckInAsync(c, id, "2030-03-04", "Done");

        var habit = (await c.GetJsonAsync("/api/stats"))["recent"]!["habits"]!.AsArray().Single()!;
        Assert.Equal(2, (int)habit["done"]!);
        Assert.Equal(0, (int)habit["missed"]!);
        Assert.Equal(2, (int)habit["expected"]!); // 3 days minus the skipped one
        Assert.Equal(1.0, (double)habit["rate"]!);

        var first = (await c.GetJsonAsync("/api/achievements")).AsArray()
            .Single(a => (string?)a!["key"] == "checkins-1");
        Assert.Equal("Primeiro passo", (string?)first!["title"]);
    }

    [Fact]
    public async Task Paused_days_leave_expected_but_a_done_check_in_on_a_paused_day_still_counts()
    {
        var c = await _factory.LoggedInClientAsync();
        var id = await CreateHabitAsync(c);
        await CheckInAsync(c, id, "2030-02-28", "Done");
        var pause = await c.PostAsJsonAsync($"/api/habits/{id}/pauses", new { startDate = "2030-03-01", endDate = "2030-03-03" });
        Assert.Equal(HttpStatusCode.Created, pause.StatusCode);
        await CheckInAsync(c, id, "2030-03-02", "Done"); // done on a paused day: counts normally
        await CheckInAsync(c, id, "2030-03-04", "Done");

        var habit = (await c.GetJsonAsync("/api/stats"))["recent"]!["habits"]!.AsArray().Single()!;
        Assert.Equal(3, (int)habit["done"]!);
        Assert.Equal(3, (int)habit["expected"]!); // Feb 28..Mar 4 = 5 days, 2 excused (Mar 1 and 3)
        Assert.Equal(0, (int)habit["missed"]!);
    }
}
