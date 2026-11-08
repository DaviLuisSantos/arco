using System.Net;
using System.Net.Http.Json;
using Arco.Api.Features.Push;
using Arco.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Arco.Api.Tests;

public sealed class AdminApiTests : IDisposable
{
    // Monday 2030-03-04, 15:00 UTC = 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static async Task CheckInAsync(HttpClient client, string name)
    {
        var res = await client.PostAsJsonAsync("/api/habits", new
        {
            name, frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var id = (int)(await res.JsonAsync())["id"]!;
        var ci = await client.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-04", status = "Done" });
        Assert.True(ci.IsSuccessStatusCode, await ci.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Non_admin_gets_404()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Admin:Emails", "boss@teste.arco"));
        var client = await _factory.LoggedInClientAsync("user@teste.arco");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/metrics")).StatusCode);
    }

    [Fact]
    public async Task Admin_sees_metrics_for_users_and_check_ins()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Admin:Emails", "x@y.z, boss@teste.arco"));
        var admin = factory.CreateClient();
        await _factory.RegisterAsync(admin, "boss@teste.arco");
        var other = factory.CreateClient();
        await _factory.RegisterAsync(other, "other@teste.arco");
        await CheckInAsync(admin, "Meditar");
        await CheckInAsync(other, "Correr");

        var res = await admin.GetAsync("/api/admin/metrics");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.JsonAsync();
        Assert.Equal(2, (int)json["users"]!["total"]!);
        Assert.Equal(0, (int)json["users"]!["demo"]!);
        Assert.Equal(30, json["signupsByDay"]!.AsArray().Count);
        Assert.Equal(2, (int)json["signupsByDay"]!.AsArray().Last()!["count"]!);
        Assert.Equal(2, (int)json["checkInsByDay"]!.AsArray().Last()!["count"]!);
        Assert.Equal(2, (int)json["activeUsers"]!["d1"]!);
        Assert.Equal(2, (int)json["activeUsers"]!["d30"]!);
        Assert.Equal(8, json["retention"]!.AsArray().Count);
        Assert.Equal(2, (int)json["retention"]!.AsArray().Last()!["size"]!);
    }

    [Fact]
    public async Task Retention_cohort_has_known_rates()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Admin:Emails", "boss@teste.arco"));
        var admin = factory.CreateClient();
        await _factory.RegisterAsync(admin, "boss@teste.arco");
        var other = factory.CreateClient();
        await _factory.RegisterAsync(other, "other@teste.arco");
        // Bump the clock-independent cohort: both users "signed up" on Tue 2030-02-26 (cohort week 2030-02-25).
        await _factory.AsUserAsync(await _factory.UserIdAsync("boss@teste.arco"), async sp =>
        {
            await sp.GetRequiredService<AppDbContext>().Users
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.CreatedAt, new DateTime(2030, 2, 26, 10, 0, 0, DateTimeKind.Utc)));
            return 0;
        });
        // Only the boss checks in, on 2030-03-04 (cohort week + 1).
        var res = await admin.PostAsJsonAsync("/api/habits", new
        {
            name = "Meditar", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min",
        });
        var id = (int)(await res.JsonAsync())["id"]!;
        var ci = await admin.PutAsJsonAsync("/api/checkins", new { habitId = id, date = "2030-03-04", status = "Done" });
        Assert.True(ci.IsSuccessStatusCode, await ci.Content.ReadAsStringAsync());

        var json = await (await admin.GetAsync("/api/admin/metrics")).JsonAsync();
        var row = json["retention"]!.AsArray().Single(r => (string)r!["cohortWeek"]! == "2030-02-25")!;
        Assert.Equal(2, (int)row["size"]!);
        Assert.Equal(0.5, (double)row["week1"]!);
        Assert.Equal(0, (double)row["week2"]!);
        Assert.Equal(0, (double)row["week4"]!);
    }

    [Fact]
    public async Task Push_is_in_english_for_an_english_user()
    {
        var client = await _factory.LoggedInClientAsync("en@teste.arco");
        var id = await _factory.UserIdAsync("en@teste.arco");
        await _factory.AsUserAsync(id, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Locale, "en"));
            return 0;
        });
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/push/subscriptions", Sub())).StatusCode);
        var res = await client.PostAsJsonAsync("/api/habits", new
        {
            name = "Run", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min", reminderTime = "12:00",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var sent = await ReminderDispatcher.RunForAllUsersAsync(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(), null, CancellationToken.None);
        Assert.Equal(1, sent);
        var body = (string?)Assert.Single(_factory.Push.Sent).Payload["body"];
        Assert.Contains("No time?", body);
    }

    private static object Sub()
    {
        using var key = System.Security.Cryptography.ECDiffieHellman.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        return new
        {
            endpoint = "https://push.example.com/en",
            keys = new
            {
                p256dh = System.Buffers.Text.Base64Url.EncodeToString(Arco.Api.Infrastructure.Push.WebPushCrypto.ExportUncompressed(key.ExportParameters(false))),
                auth = System.Buffers.Text.Base64Url.EncodeToString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
            },
        };
    }
}
