using System.Net;
using System.Net.Http.Json;

namespace Arco.Api.Tests;

public sealed class CycleGoalApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(0)]
    [InlineData(14)]
    public async Task Create_rejects_odd_or_out_of_range_weeks(int weeks)
    {
        var client = await _factory.LoggedInClientAsync();
        var res = await client.PostAsJsonAsync("/api/cycles", new { name = "Ciclo", weeks });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("número par de semanas", (string)(await res.JsonAsync())["error"]!);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(12)]
    public async Task Create_accepts_even_weeks_and_reports_them(int weeks)
    {
        var client = await _factory.LoggedInClientAsync();
        var res = await client.PostAsJsonAsync("/api/cycles", new { name = "Ciclo", weeks });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var cycle = await res.JsonAsync();
        Assert.Equal(weeks, (int)cycle["weeks"]!);
        Assert.Equal(weeks * 7, (int)cycle["totalDays"]!);
        Assert.Equal(420, (int)cycle["weeklyFocusMinutes"]!);
    }

    [Fact]
    public async Task Create_stores_weekly_focus_goal()
    {
        var client = await _factory.LoggedInClientAsync();
        var res = await client.PostAsJsonAsync("/api/cycles", new { name = "Ciclo", weeklyFocusMinutes = 600 });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(600, (int)(await res.JsonAsync())["weeklyFocusMinutes"]!);
    }

    [Theory]
    [InlineData(59)]
    [InlineData(3361)]
    public async Task Create_rejects_goal_out_of_range(int minutes)
    {
        var client = await _factory.LoggedInClientAsync();
        var res = await client.PostAsJsonAsync("/api/cycles", new { name = "Ciclo", weeklyFocusMinutes = minutes });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Create_accepts_goal_bounds()
    {
        var client = await _factory.LoggedInClientAsync();
        var res = await client.PostAsJsonAsync("/api/cycles", new { name = "Ciclo", weeklyFocusMinutes = 60 });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var id = (int)(await res.JsonAsync())["id"]!;
        var put = await client.PutAsJsonAsync($"/api/cycles/{id}", new { name = "Ciclo", endDate = (await client.GetJsonAsync("/api/cycles/current"))["endDate"]!.ToString(), weeklyFocusMinutes = 3360 });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(3360, (int)(await put.JsonAsync())["weeklyFocusMinutes"]!);
    }

    [Fact]
    public async Task Put_without_goal_keeps_it_and_with_goal_changes_it()
    {
        var client = await _factory.LoggedInClientAsync();
        var cycle = await (await client.PostAsJsonAsync("/api/cycles", new { name = "Ciclo", weeklyFocusMinutes = 500 })).JsonAsync();
        var id = (int)cycle["id"]!;
        var endDate = (string)cycle["endDate"]!;

        var keep = await client.PutAsJsonAsync($"/api/cycles/{id}", new { name = "Novo nome", endDate });
        Assert.Equal(HttpStatusCode.OK, keep.StatusCode);
        Assert.Equal(500, (int)(await keep.JsonAsync())["weeklyFocusMinutes"]!);

        var change = await client.PutAsJsonAsync($"/api/cycles/{id}", new { name = "Novo nome", endDate, weeklyFocusMinutes = 900 });
        Assert.Equal(900, (int)(await change.JsonAsync())["weeklyFocusMinutes"]!);

        var bad = await client.PutAsJsonAsync($"/api/cycles/{id}", new { name = "Novo nome", endDate, weeklyFocusMinutes = 10 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(900, (int)(await client.GetJsonAsync("/api/cycles/current"))["weeklyFocusMinutes"]!);
    }

    [Fact]
    public async Task Put_can_leave_a_partial_last_week_and_weeks_rounds_up()
    {
        var client = await _factory.LoggedInClientAsync();
        var cycle = await (await client.PostAsJsonAsync("/api/cycles", new { name = "Ciclo", startDate = "2030-01-01", weeks = 2 })).JsonAsync();
        var put = await client.PutAsJsonAsync($"/api/cycles/{(int)cycle["id"]!}", new { name = "Ciclo", endDate = "2030-01-17" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await put.JsonAsync();
        Assert.Equal(17, (int)updated["totalDays"]!);
        Assert.Equal(3, (int)updated["weeks"]!);
    }
}
