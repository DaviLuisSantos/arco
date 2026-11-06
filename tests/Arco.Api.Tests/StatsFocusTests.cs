using System.Net.Http.Json;
using Arco.Api.Domain;
using Arco.Api.Infrastructure;

namespace Arco.Api.Tests;

public sealed class StatsFocusTests : IDisposable
{
    // Wednesday 2030-03-06, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 6, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private async Task<int> CreateHabitAsync(HttpClient client, string name)
    {
        var res = await client.PostAsJsonAsync("/api/habits",
            new { name, frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        return (int)(await res.JsonAsync())["id"]!;
    }

    private Task AddAsync(int habitId, string date, int minutes, FocusSource source = FocusSource.Timer) =>
        _factory.WithScopeAsync<AppDbContext, int>(async db =>
        {
            db.FocusSessions.Add(new FocusSession
            {
                HabitId = habitId, Date = DateOnly.Parse(date), Minutes = minutes, Source = source,
                CreatedAt = DateTime.UtcNow,
            });
            return await db.SaveChangesAsync();
        });

    [Fact]
    public async Task Stats_sum_minutes_per_week_period_and_habit()
    {
        var client = await _factory.LoggedInClientAsync();
        var ler = await CreateHabitAsync(client, "Ler");
        var correr = await CreateHabitAsync(client, "Correr");

        await AddAsync(ler, "2030-03-04", 25);                      // this week (Mon)
        await AddAsync(ler, "2030-03-06", 30, FocusSource.Manual);  // this week (today)
        await AddAsync(correr, "2030-03-05", 40);                   // this week
        await AddAsync(ler, "2030-02-25", 50);                      // previous week, inside the last 30 days
        await AddAsync(ler, "2030-03-07", 99);                      // tomorrow: outside the current week range

        var stats = await client.GetJsonAsync("/api/stats");
        var weeks = stats["weeks"]!.AsArray();
        Assert.Equal(95, (int)weeks[^1]!["minutes"]!);
        Assert.Equal(50, (int)weeks[^2]!["minutes"]!);
        Assert.Equal(0, (int)weeks[0]!["minutes"]!);

        var recent = stats["recent"]!;
        Assert.Equal(145, (int)recent["minutes"]!);
        var habits = recent["habits"]!.AsArray();
        Assert.Equal(105, (int)habits.Single(h => (int)h!["habitId"]! == ler)!["minutes"]!);
        Assert.Equal(40, (int)habits.Single(h => (int)h!["habitId"]! == correr)!["minutes"]!);
    }
}
