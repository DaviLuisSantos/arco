using System.Net;
using System.Net.Http.Json;
using Arco.Api.Domain;
using Arco.Api.Infrastructure;

namespace Arco.Api.Tests;

public sealed class TodayQuantityTests : IDisposable
{
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static async Task<int> CreateAsync(HttpClient c, object body)
    {
        var res = await c.PostAsJsonAsync("/api/habits", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (int)(await res.JsonAsync())["id"]!;
    }

    [Fact]
    public async Task Today_exposes_quantity_fields()
    {
        var c = await _factory.LoggedInClientAsync();
        var q = await CreateAsync(c, new { name = "Água", frequencyType = "Daily", unit = "L", levelTarget = 2.5, levelMinimumTarget = 1 });
        var plain = await CreateAsync(c, new { name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });

        var before = (await c.GetJsonAsync("/api/today"))["habits"]!.AsArray();
        var b = before.Single(h => (int)h!["id"]! == q)!;
        Assert.Equal(0m, (decimal)b["quantityToday"]!);

        await _factory.WithScopeAsync<AppDbContext, int>(async db =>
        {
            db.HabitDayQuantities.Add(new HabitDayQuantity
            {
                HabitId = q, Date = new DateOnly(2030, 3, 4), TotalTenths = 17, UpdatedAt = DateTime.UtcNow
            });
            return await db.SaveChangesAsync();
        });

        var habits = (await c.GetJsonAsync("/api/today"))["habits"]!.AsArray();
        var h = habits.Single(x => (int)x!["id"]! == q)!;
        Assert.Equal("L", (string?)h["unit"]);
        Assert.Equal(2.5m, (decimal)h["target"]!);
        Assert.Equal(1m, (decimal)h["minimumTarget"]!);
        Assert.Equal(1.7m, (decimal)h["quantityToday"]!);

        var p = habits.Single(x => (int)x!["id"]! == plain)!;
        Assert.Null(p["unit"]);
        Assert.Null(p["target"]);
        Assert.Equal(0m, (decimal)p["quantityToday"]!);
    }
}
