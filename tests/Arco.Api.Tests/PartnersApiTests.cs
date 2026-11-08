using System.Net;
using System.Net.Http.Json;

namespace Arco.Api.Tests;

public sealed class PartnersApiTests : IDisposable
{
    // Wednesday 2030-03-13, 12:00 in São Paulo: the week started on Monday the 11th.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 13, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static string TokenOf(string link) => link[(link.LastIndexOf('/') + 1)..];

    private static async Task<(int Id, string Token)> InviteAsync(HttpClient owner)
    {
        var res = await owner.PostAsync("/api/partners/invites", null);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var json = await res.JsonAsync();
        var link = (string)json["link"]!;
        Assert.Contains("/invite/", link);
        return ((int)json["id"]!, TokenOf(link));
    }

    private static Task<HttpResponseMessage> AcceptAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/partners/accept", new { token });

    [Fact]
    public async Task Invite_accept_and_see_summary_without_notes_or_reviews()
    {
        var owner = await _factory.LoggedInClientAsync();
        var partner = await _factory.LoggedInClientAsync();

        var habit = await owner.PostAsJsonAsync("/api/habits", new
        {
            name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min",
        });
        habit.EnsureSuccessStatusCode();
        var habitId = (int)(await habit.JsonAsync())["id"]!;
        (await owner.PutAsJsonAsync("/api/checkins", new
        {
            habitId, date = "2030-03-12", status = "Done", note = "segredo-da-nota",
        })).EnsureSuccessStatusCode();

        var (inviteId, token) = await InviteAsync(owner);
        var pending = await owner.GetJsonAsync("/api/partners");
        Assert.Single(pending["pendingInvites"]!.AsArray());

        var accepted = await AcceptAsync(partner, token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var body = await accepted.JsonAsync();
        Assert.Equal(inviteId, (int)body["id"]!);
        Assert.False(string.IsNullOrEmpty((string?)body["owner"]!["displayName"]));

        var ownerList = await owner.GetJsonAsync("/api/partners");
        Assert.Single(ownerList["sharingWith"]!.AsArray());
        Assert.Empty(ownerList["pendingInvites"]!.AsArray());
        var partnerList = await partner.GetJsonAsync("/api/partners");
        Assert.Single(partnerList["sharedWithMe"]!.AsArray());
        Assert.Empty(partnerList["sharingWith"]!.AsArray());

        var res = await partner.GetAsync($"/api/partners/{inviteId}/summary");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var raw = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("segredo-da-nota", raw);
        Assert.DoesNotContain("wins", raw);
        Assert.DoesNotContain("obstacles", raw);

        var summary = await res.JsonAsync();
        Assert.Equal(1, (int)summary["owner"]!["level"]!);
        var weeks = summary["weeks"]!.AsArray();
        Assert.Equal(2, weeks.Count);
        Assert.Equal("2030-03-11", (string)weeks[0]!["start"]!);
        Assert.Equal("2030-03-13", (string)weeks[0]!["end"]!);
        Assert.Equal("2030-03-04", (string)weeks[1]!["start"]!);
        Assert.Equal("2030-03-10", (string)weeks[1]!["end"]!);
        var h = weeks[0]!["habits"]!.AsArray().Single()!;
        Assert.Equal("Ler", (string)h["name"]!);
        Assert.Equal(1, (int)h["done"]!);
        Assert.Equal(1, (int)weeks[0]!["totals"]!["done"]!);
    }

    [Fact]
    public async Task Third_party_and_owner_cannot_read_the_summary()
    {
        var owner = await _factory.LoggedInClientAsync();
        var partner = await _factory.LoggedInClientAsync();
        var stranger = await _factory.LoggedInClientAsync();
        var (id, token) = await InviteAsync(owner);

        // Not accepted yet: nobody sees it.
        Assert.Equal(HttpStatusCode.NotFound, (await partner.GetAsync($"/api/partners/{id}/summary")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(partner, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/partners/{id}/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/partners/{id}/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/partners/{id}")).StatusCode);
    }

    [Fact]
    public async Task Expired_used_and_unknown_invites_are_rejected()
    {
        var owner = await _factory.LoggedInClientAsync();
        var partner = await _factory.LoggedInClientAsync();
        var other = await _factory.LoggedInClientAsync();

        var (_, used) = await InviteAsync(owner);
        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(partner, used)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AcceptAsync(other, used)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AcceptAsync(other, "token-que-nao-existe")).StatusCode);

        var (_, expiring) = await InviteAsync(owner);
        _factory.Time!.AdvanceDays(8);
        Assert.Equal(HttpStatusCode.NotFound, (await AcceptAsync(other, expiring)).StatusCode);
        Assert.Empty((await owner.GetJsonAsync("/api/partners"))["pendingInvites"]!.AsArray());
    }

    [Fact]
    public async Task Accepting_own_invite_is_a_validation_error()
    {
        var owner = await _factory.LoggedInClientAsync();
        var (_, token) = await InviteAsync(owner);
        var res = await AcceptAsync(owner, token);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("validation", (string)(await res.JsonAsync())["code"]!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Either_side_can_remove_the_partnership(bool byOwner)
    {
        var owner = await _factory.LoggedInClientAsync();
        var partner = await _factory.LoggedInClientAsync();
        var (id, token) = await InviteAsync(owner);
        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(partner, token)).StatusCode);

        var remover = byOwner ? owner : partner;
        Assert.Equal(HttpStatusCode.NoContent, (await remover.DeleteAsync($"/api/partners/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await partner.GetAsync($"/api/partners/{id}/summary")).StatusCode);
        Assert.Empty((await owner.GetJsonAsync("/api/partners"))["sharingWith"]!.AsArray());
        Assert.Empty((await partner.GetJsonAsync("/api/partners"))["sharedWithMe"]!.AsArray());
    }
}
