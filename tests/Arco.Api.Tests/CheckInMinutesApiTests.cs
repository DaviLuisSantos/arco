using System.Net;
using System.Net.Http.Json;
using Arco.Api.Domain;
using Arco.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Arco.Api.Tests;

public sealed class CheckInMinutesApiTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));
    private const string Today = "2030-03-04";

    public void Dispose() => _factory.Dispose();

    private static async Task<int> CreateHabitAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/habits",
            new { name = "Estudar", frequencyType = "Daily", levelDescription = "1 h", minimumDescription = "10 min" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    private Task<List<FocusSession>> SessionsAsync(int habitId) =>
        _factory.WithScopeAsync<AppDbContext, List<FocusSession>>(db =>
            db.FocusSessions.AsNoTracking().Where(s => s.HabitId == habitId).OrderBy(s => s.Id).ToListAsync());

    [Fact]
    public async Task Minutes_create_replace_and_remove_the_single_manual_session()
    {
        var client = await _factory.LoggedInClientAsync();
        var habitId = await CreateHabitAsync(client);

        var put = await client.PutAsJsonAsync("/api/checkins", new { habitId, date = Today, status = "Done", minutes = 45 });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(45, (int)(await put.JsonAsync())["checkIn"]!["minutes"]!);

        // Same PUT again (offline resend) and a new value both keep a single Manual session.
        await client.PutAsJsonAsync("/api/checkins", new { habitId, date = Today, status = "Done", minutes = 45 });
        var s1 = await SessionsAsync(habitId);
        Assert.Single(s1);
        Assert.Equal(FocusSource.Manual, s1[0].Source);

        await client.PutAsJsonAsync("/api/checkins", new { habitId, date = Today, status = "Done", minutes = 90 });
        var s2 = await SessionsAsync(habitId);
        Assert.Single(s2);
        Assert.Equal(90, s2[0].Minutes);
        Assert.Equal(s1[0].Id, s2[0].Id);

        // GET /checkins exposes the manual minutes.
        var list = await client.GetJsonAsync($"/api/checkins?from={Today}&to={Today}");
        Assert.Equal(90, (int)list.AsArray().Single()!["minutes"]!);

        // Absent minutes leave the session alone.
        var keep = await client.PutAsJsonAsync("/api/checkins", new { habitId, date = Today, status = "Minimum" });
        Assert.Equal(90, (int)(await keep.JsonAsync())["checkIn"]!["minutes"]!);
        Assert.Equal(90, Assert.Single(await SessionsAsync(habitId)).Minutes);

        // Zero removes it.
        var zero = await client.PutAsJsonAsync("/api/checkins", new { habitId, date = Today, status = "Done", minutes = 0 });
        Assert.Equal(HttpStatusCode.OK, zero.StatusCode);
        Assert.Null((await zero.JsonAsync())["checkIn"]!["minutes"]);
        Assert.Empty(await SessionsAsync(habitId));
    }

    [Fact]
    public async Task Minutes_without_done_or_minimum_and_out_of_range_are_rejected()
    {
        var client = await _factory.LoggedInClientAsync();
        var habitId = await CreateHabitAsync(client);

        var missed = await client.PutAsJsonAsync("/api/checkins", new { habitId, date = Today, status = "Missed", minutes = 10 });
        Assert.Equal(HttpStatusCode.BadRequest, missed.StatusCode);

        foreach (var minutes in new[] { -1, 481 })
        {
            var res = await client.PutAsJsonAsync("/api/checkins", new { habitId, date = Today, status = "Done", minutes });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        // Missed with 0 minutes is fine; nothing was persisted by the rejected calls.
        var ok = await client.PutAsJsonAsync("/api/checkins", new { habitId, date = Today, status = "Missed", minutes = 0 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Empty(await SessionsAsync(habitId));
    }

    [Fact]
    public async Task Delete_removes_manual_but_keeps_timer_sessions()
    {
        var client = await _factory.LoggedInClientAsync();
        var habitId = await CreateHabitAsync(client);

        await client.PutAsJsonAsync("/api/checkins", new { habitId, date = Today, status = "Done", minutes = 30 });
        await _factory.WithScopeAsync<AppDbContext, int>(async db =>
        {
            db.FocusSessions.Add(new FocusSession
            {
                HabitId = habitId, Date = DateOnly.Parse(Today), Minutes = 25, Source = FocusSource.Timer,
                CreatedAt = DateTime.UtcNow,
            });
            return await db.SaveChangesAsync();
        });

        var del = await client.DeleteAsync($"/api/checkins/{habitId}/{Today}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var left = Assert.Single(await SessionsAsync(habitId));
        Assert.Equal(FocusSource.Timer, left.Source);
        Assert.Equal(25, left.Minutes);
    }
}
