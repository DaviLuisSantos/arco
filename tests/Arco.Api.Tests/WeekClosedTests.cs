using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Arco.Api.Infrastructure.Events;
using Arco.Api.Infrastructure.Outbox;
using Arco.Api.Infrastructure.Push;
using Microsoft.Extensions.DependencyInjection;

namespace Arco.Api.Tests;

/// <summary>Closing the week (weekly review) tells the accepted partners: push if they have a device, otherwise e-mail.</summary>
public sealed class WeekClosedTests : IDisposable
{
    // Monday 2030-03-04: the reviewable week is the one that started on 2030-02-25.
    private static readonly DateOnly Week = new(2030, 2, 25);
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static object Subscription(string endpoint)
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return new
        {
            endpoint,
            keys = new
            {
                p256dh = Base64Url.EncodeToString(WebPushCrypto.ExportUncompressed(key.ExportParameters(false))),
                auth = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)),
            },
        };
    }

    private static async Task<int> PairAsync(HttpClient owner, HttpClient partner)
    {
        var invite = await owner.PostAsync("/api/partners/invites", null);
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var json = await invite.JsonAsync();
        var link = (string)json["link"]!;
        var accepted = await partner.PostAsJsonAsync("/api/partners/accept", new { token = link[(link.LastIndexOf('/') + 1)..] });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        return (int)json["id"]!;
    }

    private async Task CloseWeekAsync(HttpClient owner)
    {
        var res = await owner.PostAsJsonAsync("/api/reviews/weekly", new { weekStart = Week.ToString("yyyy-MM-dd"), wins = "Foi bem" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        // The request only queues the event; delivery is the outbox worker's job.
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().RunOnceAsync(CancellationToken.None);
    }

    private async Task RedeliverAsync(int ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<WeekClosedConsumer>()
            .HandleAsync(new WeekClosed(ownerId, Week), CancellationToken.None);
    }

    private int WeekClosedMails() => _factory.Email.Sent.Count(m => m.Subject.Contains("fechou a semana"));

    [Fact]
    public async Task Partner_with_a_device_gets_a_push_and_no_email()
    {
        var owner = await _factory.LoggedInClientAsync();
        var partner = await _factory.LoggedInClientAsync();
        var partnershipId = await PairAsync(owner, partner);
        Assert.Equal(HttpStatusCode.NoContent,
            (await partner.PostAsJsonAsync("/api/push/subscriptions", Subscription("https://push.test/partner"))).StatusCode);

        await CloseWeekAsync(owner);

        var push = Assert.Single(_factory.Push.Sent);
        Assert.Equal("https://push.test/partner", push.Endpoint);
        Assert.EndsWith("fechou a semana", (string)push.Payload["title"]!);
        Assert.Equal($"/partners/{partnershipId}", (string)push.Payload["url"]!);
        Assert.Equal(0, WeekClosedMails());
    }

    [Fact]
    public async Task Partner_without_a_device_gets_an_email()
    {
        var owner = await _factory.LoggedInClientAsync();
        var partnerEmail = $"parceiro-{Guid.NewGuid():N}@teste.arco";
        var partner = await _factory.LoggedInClientAsync(partnerEmail);
        var partnershipId = await PairAsync(owner, partner);

        await CloseWeekAsync(owner);

        var mail = Assert.Single(_factory.Email.Sent, m => m.Subject.Contains("fechou a semana"));
        Assert.Equal(partnerEmail, mail.To);
        Assert.Contains($"/partners/{partnershipId}", mail.Html);
        Assert.Empty(_factory.Push.Sent);
    }

    [Fact]
    public async Task Redelivered_event_does_not_notify_twice()
    {
        var ownerEmail = $"dono-{Guid.NewGuid():N}@teste.arco";
        var owner = await _factory.LoggedInClientAsync(ownerEmail);
        var withPush = await _factory.LoggedInClientAsync();
        var withoutPush = await _factory.LoggedInClientAsync();
        await PairAsync(owner, withPush);
        await PairAsync(owner, withoutPush);
        Assert.Equal(HttpStatusCode.NoContent,
            (await withPush.PostAsJsonAsync("/api/push/subscriptions", Subscription("https://push.test/a"))).StatusCode);

        await CloseWeekAsync(owner);
        Assert.Single(_factory.Push.Sent);
        Assert.Equal(1, WeekClosedMails());

        var ownerId = await _factory.UserIdAsync(ownerEmail);
        await RedeliverAsync(ownerId);
        await RedeliverAsync(ownerId);

        Assert.Single(_factory.Push.Sent);
        Assert.Equal(1, WeekClosedMails());
    }

    [Fact]
    public async Task Account_without_partner_notifies_nobody()
    {
        var owner = await _factory.LoggedInClientAsync();
        var stranger = await _factory.LoggedInClientAsync();
        Assert.Equal(HttpStatusCode.NoContent,
            (await stranger.PostAsJsonAsync("/api/push/subscriptions", Subscription("https://push.test/stranger"))).StatusCode);

        await CloseWeekAsync(owner);

        Assert.Empty(_factory.Push.Sent);
        Assert.Equal(0, WeekClosedMails());
    }
}
