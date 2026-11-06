using System.Net;
using System.Net.Http.Json;
using Arco.Api.Domain;
using Arco.Api.Infrastructure;

namespace Arco.Api.Tests;

public sealed class TodayMinutesApiTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));
    private const string Today = "2030-03-04";

    public void Dispose() => _factory.Dispose();

    private static async Task<int> CreateHabitAsync(HttpClient client, string name)
    {
        var res = await client.PostAsJsonAsync("/api/habits",
            new { name, frequencyType = "Daily", levelDescription = "1 h", minimumDescription = "10 min" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    [Fact]
    public async Task Today_and_habit_detail_expose_manual_minutes_only()
    {
        var client = await _factory.LoggedInClientAsync();
        var withMinutes = await CreateHabitAsync(client, "Estudar");
        var without = await CreateHabitAsync(client, "Ler");

        await client.PutAsJsonAsync("/api/checkins", new { habitId = withMinutes, date = Today, status = "Done", minutes = 40 });
        await client.PutAsJsonAsync("/api/checkins", new { habitId = without, date = Today, status = "Done" });
        // A Timer session must not show up as minutes.
        await _factory.WithScopeAsync<AppDbContext, int>(async db =>
        {
            db.FocusSessions.Add(new FocusSession
            {
                HabitId = without, Date = DateOnly.Parse(Today), Minutes = 25, Source = FocusSource.Timer,
                CreatedAt = DateTime.UtcNow,
            });
            return await db.SaveChangesAsync();
        });

        var today = await client.GetJsonAsync("/api/today");
        foreach (var h in today["habits"]!.AsArray())
        {
            var id = (int)h!["id"]!;
            if (id == withMinutes) Assert.Equal(40, (int)h["checkIn"]!["minutes"]!);
            else Assert.Null(h["checkIn"]!["minutes"]);
        }

        var detail = await client.GetJsonAsync($"/api/habits/{withMinutes}");
        Assert.Equal(40, (int)detail["recentCheckIns"]!.AsArray().Single()!["minutes"]!);
        var detail2 = await client.GetJsonAsync($"/api/habits/{without}");
        Assert.Null(detail2["recentCheckIns"]!.AsArray().Single()!["minutes"]);
    }
}
