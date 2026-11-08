namespace Arco.Api.Features.Health;

public static class HealthEndpoints
{
    public sealed record HealthResponse(string Status, string Version);

    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder api)
    {
        api.MapGet("/health", GetHealth).AllowAnonymous().WithTags("Saúde").WithSummary("Verifica a saúde da API");
        return api;
    }

    private static async Task<IResult> GetHealth(AppDbContext db, ILoggerFactory loggers, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            return Results.Ok(new HealthResponse("healthy", AppInfo.Version));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggers.CreateLogger("Health").LogError(ex, "Database ping failed");
            return Results.Json(new HealthResponse("unhealthy", AppInfo.Version),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
