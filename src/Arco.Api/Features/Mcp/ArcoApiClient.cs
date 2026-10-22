using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;

namespace Arco.Api.Features.Mcp;

/// <summary>
/// The MCP tools go through the app's own REST API, forwarding the caller's API key, so the MCP server behaves
/// exactly like the API (validation, XP, achievements, error messages) without duplicating any logic.
/// </summary>
public sealed class ArcoApiClient(IHttpClientFactory httpClients, IHttpContextAccessor http, IConfiguration config)
{
    public const string HttpClientName = "arco-self";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Routes the MCP must not proxy: itself, binary downloads and key management (cookie only).</summary>
    private static readonly string[] Blocked = ["/api/mcp", "/api/backup", "/api/api-keys", "/api/auth/"];

    public Task<string> GetAsync(string path, CancellationToken ct) => SendAsync(HttpMethod.Get, path, null, ct);

    /// <summary>GET that may answer 204 (e.g. no active cycle): null then.</summary>
    public async Task<JsonNode?> GetJsonOrNullAsync(string path, CancellationToken ct)
    {
        var text = await SendRawAsync(HttpMethod.Get, path, null, ct);
        return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    }

    public async Task<JsonNode> GetJsonAsync(string path, CancellationToken ct) =>
        JsonNode.Parse(await GetAsync(path, ct)) ?? throw new McpException("Resposta vazia da API.");

    /// <param name="body">An object (serialized as JSON) or a raw JSON string.</param>
    /// <returns>The response body (JSON), or a short confirmation for empty responses.</returns>
    public async Task<string> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var text = await SendRawAsync(method, path, body, ct);
        return string.IsNullOrWhiteSpace(text) ? "Feito." : text;
    }

    private async Task<string> SendRawAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        if (!path.StartsWith("/api/", StringComparison.Ordinal))
            throw new McpException("A rota deve começar com /api/ (ex.: /api/today).");
        if (Blocked.Any(b => path.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
            throw new McpException("Essa rota não está disponível pelo MCP. Backup e chaves de API ficam no app.");

        var context = http.HttpContext ?? throw new InvalidOperationException("MCP tool called outside an HTTP request.");
        var baseUrl = config["Mcp:SelfBaseUrl"]?.TrimEnd('/')
                      ?? $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";

        using var request = new HttpRequestMessage(method, baseUrl + path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var auth))
            request.Headers.Authorization = auth;
        if (body is not null)
        {
            var json = body as string ?? JsonSerializer.Serialize(body, Json);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await httpClients.CreateClient(HttpClientName).SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new McpException(ErrorMessage((int)response.StatusCode, text));
        return text;
    }

    /// <summary>The API's pt-BR message (<c>{ error, code }</c>), with the status for context.</summary>
    private static string ErrorMessage(int status, string body)
    {
        try
        {
            if (JsonNode.Parse(body) is JsonObject error && error["error"]?.GetValue<string>() is { } message)
                return $"{message} (HTTP {status}, {error["code"]?.GetValue<string>()})";
        }
        catch (JsonException)
        {
            // corpo não-JSON
        }
        return $"A API respondeu HTTP {status}.";
    }
}
