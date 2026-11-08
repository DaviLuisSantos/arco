namespace Arco.Api.Infrastructure;

/// <summary>Plan limits (403 <c>plan_limit</c>) checked by the endpoints that create things.</summary>
public static class PlanGate
{
    public static IResult Limit(string message) =>
        ApiResults.Error(StatusCodes.Status403Forbidden, ErrorCodes.PlanLimit, message);

    public static async Task<UserPlan> PlanOfAsync(AppDbContext db, int userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Plan).FirstOrDefaultAsync(ct);

    /// <summary>Null when the user may have one more active habit; otherwise the 403 to return.</summary>
    public static async Task<IResult?> CheckHabitLimitAsync(AppDbContext db, CurrentUser user, CancellationToken ct)
    {
        var max = PlanLimits.MaxActiveHabits(await PlanOfAsync(db, user.Id, ct));
        if (max is null) return null;
        return await db.Habits.CountAsync(h => h.ArchivedAt == null, ct) >= max
            ? Limit($"O plano Free permite até {max} hábitos ativos. Arquive algum ou assine o Pro.")
            : null;
    }

    /// <summary>Null when the user may create one more API key; otherwise the 403 to return.</summary>
    public static async Task<IResult?> CheckApiKeyLimitAsync(AppDbContext db, CurrentUser user, CancellationToken ct)
    {
        var max = PlanLimits.MaxApiKeys(await PlanOfAsync(db, user.Id, ct));
        if (max is null) return null;
        return await db.ApiKeys.CountAsync(ct) >= max
            ? Limit($"O plano Free permite {max} chave de API. Revogue a atual ou assine o Pro.")
            : null;
    }
}

/// <summary>Bloqueia <c>/api/mcp</c> no plano Free (403 <c>plan_limit</c>).</summary>
public sealed class McpPlanFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (await IsAllowedAsync(http)) return await next(context);
        return ApiResults.Error(StatusCodes.Status403Forbidden, ErrorCodes.PlanLimit,
            "O MCP está disponível só no plano Pro.");
    }

    internal static async Task<bool> IsAllowedAsync(HttpContext http)
    {
        var current = http.RequestServices.GetRequiredService<CurrentUser>();
        if (!current.IsSet) return true; // not authenticated: the authorization layer answers
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        return PlanLimits.Mcp(await PlanGate.PlanOfAsync(db, current.Id, http.RequestAborted));
    }
}

public static class PlanGateExtensions
{
    /// <summary>
    /// <c>AddEndpointFilter</c> só existe para <c>RouteHandlerBuilder</c>; <c>MapMcp</c> devolve o builder genérico,
    /// então o filtro entra direto na lista de filtros do endpoint.
    /// </summary>
    public static TBuilder AddMcpPlanFilter<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(eb => eb.FilterFactories.Add((_, next) =>
        {
            var filter = new McpPlanFilter();
            return ctx => filter.InvokeAsync(ctx, next);
        }));
        return builder;
    }
}
