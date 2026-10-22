using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Arco.Api.Tests;

/// <summary>Runs the real app (Development env, password "arco") against a temp SQLite file.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arco-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Arco", $"Data Source={Path.Combine(_dir, "arco.db")}");
    }

    public async Task<HttpClient> LoggedInClientAsync()
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { password = "arco" });
        res.EnsureSuccessStatusCode();
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

public static class HttpJson
{
    public static async Task<JsonNode> JsonAsync(this HttpResponseMessage res) =>
        JsonNode.Parse(await res.Content.ReadAsStringAsync())!;

    public static async Task<JsonNode> GetJsonAsync(this HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        res.EnsureSuccessStatusCode();
        return await res.JsonAsync();
    }
}
