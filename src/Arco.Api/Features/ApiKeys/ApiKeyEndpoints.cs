namespace Arco.Api.Features.ApiKeys;

public static class ApiKeyEndpoints
{
    public const int MaxKeys = 20;

    public sealed record ApiKeyDto(int Id, string Name, string Prefix, DateTime CreatedAt, DateTime? LastUsedAt)
    {
        public static ApiKeyDto From(ApiKey k) => new(k.Id, k.Name, k.Prefix, k.CreatedAt, k.LastUsedAt);
    }

    /// <summary>Returned only once, on creation: <c>Key</c> is the full secret.</summary>
    public sealed record CreatedApiKeyDto(int Id, string Name, string Prefix, DateTime CreatedAt, string Key);

    public sealed record CreateApiKeyRequest(string? Name);

    public static IEndpointRouteBuilder MapApiKeys(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/api-keys");
        group.MapGet("", List);
        group.MapPost("", Create);
        group.MapDelete("/{id:int}", Revoke);
        return api;
    }

    private const string ManagedInAppMessage = "As chaves de API só podem ser criadas e revogadas pelo app, não por outra chave.";

    private static async Task<List<ApiKeyDto>> List(AppDbContext db, CancellationToken ct) =>
        (await db.ApiKeys.AsNoTracking().OrderByDescending(k => k.CreatedAt).ThenByDescending(k => k.Id).ToListAsync(ct))
            .Select(ApiKeyDto.From).ToList();

    private static async Task<IResult> Create(CreateApiKeyRequest? request, HttpContext http, AppDbContext db, AppClock clock,
        CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return ApiResults.Error(StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, ManagedInAppMessage);

        var name = request?.Name?.Trim();
        if (!Text.LengthBetween(name, 1, 60)) return ApiResults.Validation("O nome da chave deve ter entre 1 e 60 caracteres.");
        if (await db.ApiKeys.CountAsync(ct) >= MaxKeys)
            return ApiResults.Validation($"Você já tem {MaxKeys} chaves. Revogue alguma antes de criar outra.");

        var key = Infrastructure.ApiKeys.Generate();
        var entity = new ApiKey
        {
            Name = name!,
            Prefix = Infrastructure.ApiKeys.Display(key),
            KeyHash = Infrastructure.ApiKeys.Hash(key),
            CreatedAt = clock.UtcNow,
        };
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/api-keys/{entity.Id}",
            new CreatedApiKeyDto(entity.Id, entity.Name, entity.Prefix, entity.CreatedAt, key));
    }

    /// <summary>Deletes the key: requests using it get 401 from now on.</summary>
    private static async Task<IResult> Revoke(int id, HttpContext http, AppDbContext db, CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return ApiResults.Error(StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, ManagedInAppMessage);

        var deleted = await db.ApiKeys.Where(k => k.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ApiResults.NotFound("Chave de API não encontrada.") : Results.NoContent();
    }
}
