using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Arco.Api.Infrastructure;

namespace Arco.Api.Tests;

public sealed class ApiKeyTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private HttpClient KeyClient(string key)
    {
        var client = _factory.CreateClient(new() { HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    [Fact]
    public void Keys_are_random_prefixed_and_hashed()
    {
        var a = ApiKeys.Generate();
        var b = ApiKeys.Generate();
        Assert.StartsWith("arco_", a);
        Assert.Equal(48, a.Length); // "arco_" + 43 base64url chars (32 bytes)
        Assert.NotEqual(a, b);
        Assert.Equal(64, ApiKeys.Hash(a).Length);
        Assert.Equal(a[..12], ApiKeys.Display(a));
    }

    [Fact]
    public async Task An_api_key_does_everything_the_app_does_but_cannot_manage_keys()
    {
        var app = await _factory.LoggedInClientAsync();
        var create = await app.PostAsJsonAsync("/api/api-keys", new { name = "Claude" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.JsonAsync();
        var key = (string)created["key"]!;
        var keyId = (int)created["id"]!;
        Assert.Equal(key[..12], (string?)created["prefix"]);

        // The list never shows the key again.
        var listed = (await app.GetJsonAsync("/api/api-keys")).AsArray().Single()!;
        Assert.Null(listed["key"]);
        Assert.Null(listed["lastUsedAt"]);

        var api = KeyClient(key);
        Assert.True((bool)(await api.GetJsonAsync("/api/auth/me"))["authenticated"]!);
        var habit = await api.PostAsJsonAsync("/api/habits",
            new { name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        Assert.Equal(HttpStatusCode.Created, habit.StatusCode);
        var habitId = (int)(await habit.JsonAsync())["id"]!;
        var checkIn = await api.PutAsJsonAsync("/api/checkins", new { habitId, date = "2030-03-04", status = "Done" });
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        Assert.Equal(10, (int)(await checkIn.JsonAsync())["xpDelta"]!);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/backup")).StatusCode);

        // Last use is recorded.
        Assert.NotNull((await app.GetJsonAsync("/api/api-keys")).AsArray().Single()!["lastUsedAt"]);

        // A key cannot create or revoke keys.
        var byKey = await api.PostAsJsonAsync("/api/api-keys", new { name = "outra" });
        Assert.Equal(HttpStatusCode.Forbidden, byKey.StatusCode);
        Assert.Equal("forbidden", (string?)(await byKey.JsonAsync())["code"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.DeleteAsync($"/api/api-keys/{keyId}")).StatusCode);

        // Revoked: 401 in JSON from then on.
        Assert.Equal(HttpStatusCode.NoContent, (await app.DeleteAsync($"/api/api-keys/{keyId}")).StatusCode);
        var revoked = await api.GetAsync("/api/today");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        Assert.Equal("unauthorized", (string?)(await revoked.JsonAsync())["code"]);
        Assert.Equal(HttpStatusCode.NotFound, (await app.DeleteAsync($"/api/api-keys/{keyId}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_keys_and_names_are_rejected()
    {
        foreach (var bad in new[] { "arco_naoexiste", "outra-coisa", "" })
        {
            var res = await KeyClient(bad).GetAsync("/api/today");
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        var app = await _factory.LoggedInClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsJsonAsync("/api/api-keys", new { name = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await app.PostAsJsonAsync("/api/api-keys", new { name = new string('x', 61) })).StatusCode);
    }
}
