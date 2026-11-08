using System.Net;
using System.Net.Http.Json;
using Arco.Api.Domain;
using Arco.Api.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Arco.Api.Tests;

public sealed class PublicProfileApiTests : IDisposable
{
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 10, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    private static Task<HttpResponseMessage> Put(HttpClient c, object body) => c.PutAsJsonAsync("/api/profile/public", body);

    private async Task<HttpClient> ProfileAsync(string slug, bool showHabits = true)
    {
        var client = await _factory.LoggedInClientAsync();
        var res = await Put(client, new { enabled = true, slug, showHabits });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return client;
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Com Espaco")]
    [InlineData("under_score")]
    [InlineData("admin")]
    [InlineData("api")]
    [InlineData("login")]
    public async Task Invalid_or_reserved_slug_is_rejected(string slug)
    {
        var client = await _factory.LoggedInClientAsync();
        var res = await Put(client, new { enabled = true, slug, showHabits = true });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("validation", (string?)(await res.JsonAsync())["code"]);
    }

    [Fact]
    public async Task Valid_slug_is_saved_and_duplicate_is_a_conflict()
    {
        var a = await ProfileAsync("davi-silva");
        var settings = await (await Put(a, new { enabled = true, slug = "davi-silva", showHabits = true })).JsonAsync();
        Assert.Equal("davi-silva", (string?)settings["slug"]);

        var b = await _factory.LoggedInClientAsync();
        var res = await Put(b, new { enabled = true, slug = "davi-silva", showHabits = true });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("slug_taken", (string?)(await res.JsonAsync())["code"]);
    }

    [Fact]
    public async Task Enabling_without_a_slug_is_rejected()
    {
        var client = await _factory.LoggedInClientAsync();
        var res = await Put(client, new { enabled = true });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Disabled_or_unknown_profile_is_404_everywhere()
    {
        var anon = _factory.CreateClient();
        var client = await _factory.LoggedInClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/public/profiles/ninguem")).StatusCode);

        // Slug saved but profile off.
        Assert.Equal(HttpStatusCode.OK, (await Put(client, new { enabled = false, slug = "desligado" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/public/profiles/desligado")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/u/desligado")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/u/desligado/og.png")).StatusCode);
    }

    [Fact]
    public async Task Public_profile_is_anonymous_and_shows_habits_and_cycle()
    {
        var owner = await ProfileAsync("leitor");
        await owner.PostAsJsonAsync("/api/habits",
            new { name = "Ler", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync("/api/cycles", new { name = "Ciclo X" })).StatusCode);

        var anon = _factory.CreateClient();
        var json = await anon.GetJsonAsync("/api/public/profiles/LEITOR");
        Assert.Equal(1, (int)json["level"]!);
        Assert.Equal("indigo", (string?)json["theme"]);
        Assert.Equal("Ciclo X", (string?)json["cycle"]!["name"]);
        Assert.Equal((int)json["cycle"]!["totalWeeks"]!, json["cycle"]!["stones"]!.AsArray().Count);
        Assert.Equal("Ler", (string?)json["habits"]![0]!["name"]);

        var page = await anon.GetAsync("/u/leitor");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("og:image", html);
        Assert.Contains("/u/leitor/og.png", html);
        Assert.Contains("lang=\"pt-BR\"", html);
        Assert.Contains("Ler", html);
    }

    [Fact]
    public async Task ShowHabits_false_hides_the_habits()
    {
        var owner = await ProfileAsync("reservado", showHabits: false);
        await owner.PostAsJsonAsync("/api/habits",
            new { name = "Segredo", frequencyType = "Daily", levelDescription = "10 min", minimumDescription = "2 min" });

        var anon = _factory.CreateClient();
        var json = await anon.GetJsonAsync("/api/public/profiles/reservado");
        Assert.Null(json["habits"]);
        var html = await (await anon.GetAsync("/u/reservado")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Segredo", html);
    }

    [Fact]
    public async Task Page_escapes_the_display_name_and_uses_the_owner_locale()
    {
        var owner = await ProfileAsync("escape");
        var id = await _factory.UserIdAsync(await EmailOfAsync(owner));
        await _factory.AsUserAsync(id, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var user = await db.Users.FindAsync(id);
            user!.DisplayName = "<script>alert(1)</script>";
            user.Locale = "en";
            await db.SaveChangesAsync();
            return 0;
        });

        var html = await (await _factory.CreateClient().GetAsync("/u/escape")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("lang=\"en\"", html);
    }

    private static async Task<string> EmailOfAsync(HttpClient client) =>
        (string)(await client.GetJsonAsync("/api/auth/me"))["email"]!;

    [Fact]
    public async Task OgImage_is_a_png_cached_for_an_hour()
    {
        await ProfileAsync("imagem");
        var res = await _factory.CreateClient().GetAsync("/u/imagem/og.png");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("image/png", res.Content.Headers.ContentType!.MediaType);
        Assert.Contains("max-age=3600", res.Headers.CacheControl!.ToString());
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes[..4]);
    }

    [Fact]
    public async Task Pro_theme_on_free_is_403_and_works_on_pro()
    {
        var client = await ProfileAsync("tematico");
        var res = await Put(client, new { enabled = true, slug = "tematico", showHabits = true, theme = "emerald" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("plan_limit", (string?)(await res.JsonAsync())["code"]);

        // The default theme is fine on Free.
        Assert.Equal(HttpStatusCode.OK,
            (await Put(client, new { enabled = true, slug = "tematico", showHabits = true, theme = "indigo" })).StatusCode);

        var id = await _factory.UserIdAsync(await EmailOfAsync(client));
        await _factory.AsUserAsync(id, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            (await db.Users.FindAsync(id))!.Plan = UserPlan.Pro;
            await db.SaveChangesAsync();
            return 0;
        });

        Assert.Equal(HttpStatusCode.OK,
            (await Put(client, new { enabled = true, slug = "tematico", showHabits = true, theme = "emerald" })).StatusCode);
        var json = await _factory.CreateClient().GetJsonAsync("/api/public/profiles/tematico");
        Assert.Equal("emerald", (string?)json["theme"]);
    }
}
