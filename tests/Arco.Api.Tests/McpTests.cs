using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Arco.Api.Tests;

public sealed class McpTests : IDisposable
{
    // Monday 2030-03-04, 12:00 in São Paulo.
    private readonly ApiFactory _factory = new(new DateTimeOffset(2030, 3, 4, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => _factory.Dispose();

    /// <summary>MCP is a Pro feature: promotes every user created so far.</summary>
    private async Task MakeEveryoneProAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Arco.Api.Infrastructure.AppDbContext>().Users
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Plan, Arco.Api.Domain.UserPlan.Pro));
    }

    private async Task<string> CreateKeyAsync()
    {
        var app = await _factory.LoggedInClientAsync();
        await MakeEveryoneProAsync();
        var res = await app.PostAsJsonAsync("/api/api-keys", new { name = "MCP" });
        return (string)(await res.JsonAsync())["key"]!;
    }

    private async Task<McpClient> ConnectAsync(string key)
    {
        var http = _factory.CreateClient(new() { HandleCookies = false });
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(http.BaseAddress!, "/api/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {key}" },
        }, http);
        return await McpClient.CreateAsync(transport);
    }

    private static string Text(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private static async Task<JsonNode> CallJsonAsync(McpClient mcp, string tool, Dictionary<string, object?>? args = null)
    {
        var result = await mcp.CallToolAsync(tool, args);
        Assert.True(result.IsError is not true, Text(result));
        return JsonNode.Parse(Text(result))!;
    }

    [Fact]
    public async Task Tools_drive_the_app_through_the_api_with_the_callers_key()
    {
        await using var mcp = await ConnectAsync(await CreateKeyAsync());

        var tools = (await mcp.ListToolsAsync()).Select(t => t.Name).ToHashSet();
        Assert.Subset(tools, new HashSet<string>
        {
            "ver_hoje", "fazer_checkin", "desfazer_checkin", "listar_habitos", "ver_habito", "criar_habito",
            "subir_nivel", "voltar_nivel", "ver_ciclo_atual", "atualizar_objetivo", "marcar_marco",
            "ver_revisao_semanal", "fazer_revisao_semanal", "ver_estatisticas", "ver_conquistas",
            "ler_documentacao_api", "chamar_api", "registrar_foco", "ver_floresta", "ver_arco",
        });

        var habit = await CallJsonAsync(mcp, "criar_habito", new()
        {
            ["nome"] = "Inglês", ["frequencia"] = "Daily", ["dose"] = "15 min", ["minimo"] = "5 min", ["emoji"] = "🇬🇧",
        });
        var habitId = (int)habit["id"]!;

        // No date: today, from the server's clock and timezone.
        var checkIn = await CallJsonAsync(mcp, "fazer_checkin", new() { ["habitId"] = habitId, ["status"] = "Done" });
        Assert.Equal("2030-03-04", (string?)checkIn["checkIn"]!["date"]);
        Assert.Equal(10, (int)checkIn["xpDelta"]!);
        Assert.Equal(["checkins-1"], checkIn["achievements"]!.AsArray().Select(a => (string)a!["key"]!).ToArray());

        var focus = await CallJsonAsync(mcp, "registrar_foco", new() { ["habitId"] = habitId, ["minutos"] = 30 });
        Assert.Equal(30, (int)focus["session"]!["minutes"]!);
        var forest = await CallJsonAsync(mcp, "ver_floresta");
        Assert.Equal(30, (int)forest["totalMinutes"]!);

        var today = await CallJsonAsync(mcp, "ver_hoje");
        Assert.Equal("Done", (string?)today["habits"]![0]!["checkIn"]!["status"]);

        // Everything else through chamar_api.
        var cycle = await CallJsonAsync(mcp, "chamar_api", new()
        {
            ["metodo"] = "POST", ["rota"] = "/api/cycles", ["corpoJson"] = """{ "name": "Inglês: constância" }""",
        });
        Assert.Equal("Inglês: constância", (string?)cycle["name"]);
        var current = await CallJsonAsync(mcp, "ver_ciclo_atual");
        Assert.Equal((int)cycle["id"]!, (int)current["cycle"]!["id"]!);

        var arch = await CallJsonAsync(mcp, "ver_arco");
        Assert.Equal((int)cycle["id"]!, (int)arch["cycleId"]!);
        Assert.Equal(30, (int)arch["totalMinutes"]!);

        Assert.Contains("## Hábitos", Text(await mcp.CallToolAsync("ler_documentacao_api")));
    }

    [Fact]
    public async Task Api_errors_come_back_as_tool_errors_in_portuguese()
    {
        await using var mcp = await ConnectAsync(await CreateKeyAsync());

        var notFound = await mcp.CallToolAsync("ver_habito", new Dictionary<string, object?> { ["habitId"] = 999 });
        Assert.True(notFound.IsError);
        Assert.Contains("Hábito não encontrado", Text(notFound));

        var blocked = await mcp.CallToolAsync("chamar_api",
            new Dictionary<string, object?> { ["metodo"] = "POST", ["rota"] = "/api/api-keys", ["corpoJson"] = "{}" });
        Assert.True(blocked.IsError);

        var badJson = await mcp.CallToolAsync("chamar_api",
            new Dictionary<string, object?> { ["metodo"] = "POST", ["rota"] = "/api/cycles", ["corpoJson"] = "{ nope" });
        Assert.True(badJson.IsError);
        Assert.Contains("JSON", Text(badJson));
    }

    [Fact]
    public async Task The_mcp_endpoint_requires_an_api_key()
    {
        var anonymous = _factory.CreateClient(new() { HandleCookies = false });
        var res = await anonymous.PostAsJsonAsync("/api/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);

        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync("arco_invalida"));
    }
}
