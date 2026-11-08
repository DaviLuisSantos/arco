using Microsoft.AspNetCore.DataProtection;

namespace Arco.Api.Features.Auth;

/// <summary>Sessions, two-step verification, language and onboarding of the signed-in account (app cookie only).</summary>
public static class AccountSecurityEndpoints
{
    public sealed record TwoFactorCodeRequest(string? Code);
    public sealed record LocaleRequest(string? Locale);
    public sealed record DisplayNameRequest(string? DisplayName);
    public sealed record TwoFactorSetup(string Secret, string OtpauthUri);
    public sealed record TwoFactorEnabled(List<string> RecoveryCodes);
    public sealed record SessionInfo(Guid Id, DateTime CreatedAt, DateTime LastSeenAt, string UserAgent, bool Current);

    public static RouteGroupBuilder MapAccountSecurity(this RouteGroupBuilder group)
    {
        group.MapPost("/2fa/setup", Setup).RequireAuthorization()
            .WithSummary("Gera um segredo TOTP pendente (confirme com /2fa/enable)")
            .Produces<TwoFactorSetup>().Produces<ApiError>(StatusCodes.Status403Forbidden).Produces<ApiError>(StatusCodes.Status409Conflict);
        group.MapPost("/2fa/enable", Enable).RequireAuthorization()
            .WithSummary("Ativa a verificação em duas etapas e devolve os códigos de recuperação")
            .Produces<TwoFactorEnabled>().Produces<ApiError>(StatusCodes.Status400BadRequest).Produces<ApiError>(StatusCodes.Status403Forbidden);
        group.MapPost("/2fa/disable", Disable).RequireAuthorization()
            .WithSummary("Desativa a verificação em duas etapas (código TOTP ou de recuperação)")
            .Produces(StatusCodes.Status204NoContent).Produces<ApiError>(StatusCodes.Status400BadRequest).Produces<ApiError>(StatusCodes.Status403Forbidden);
        group.MapGet("/sessions", ListSessions).RequireAuthorization()
            .WithSummary("Lista as sessões abertas da conta")
            .Produces<List<SessionInfo>>().Produces<ApiError>(StatusCodes.Status403Forbidden);
        group.MapDelete("/sessions/{id:guid}", RevokeSession).RequireAuthorization()
            .WithSummary("Encerra uma sessão")
            .Produces(StatusCodes.Status204NoContent).Produces<ApiError>(StatusCodes.Status403Forbidden).Produces<ApiError>(StatusCodes.Status404NotFound);
        group.MapPost("/sessions/revoke-others", RevokeOthers).RequireAuthorization()
            .WithSummary("Encerra todas as sessões, exceto a atual")
            .Produces(StatusCodes.Status204NoContent).Produces<ApiError>(StatusCodes.Status403Forbidden);
        group.MapPut("/display-name", SetDisplayName).RequireAuthorization().WithSummary("Altera o nome de exibição");
        group.MapPut("/locale", SetLocale).RequireAuthorization()
            .WithSummary("Define o idioma da conta (pt-BR ou en)")
            .Produces(StatusCodes.Status204NoContent).Produces<ApiError>(StatusCodes.Status400BadRequest).Produces<ApiError>(StatusCodes.Status403Forbidden);
        group.MapPost("/onboarding/complete", CompleteOnboarding).RequireAuthorization()
            .WithSummary("Marca os primeiros passos como concluídos")
            .Produces(StatusCodes.Status204NoContent).Produces<ApiError>(StatusCodes.Status403Forbidden);
        return group;
    }

    // ---- Two-step verification ----

    private static async Task<IResult> Setup(HttpContext http, AppDbContext db, CurrentUser current,
        IDataProtectionProvider protection, CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return AuthEndpoints.CookieOnly();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        if (user is null) return ApiResults.Unauthorized();
        if (user.IsDemo) return DemoForbidden();
        if (user.TotpEnabledAt is not null)
            return ApiResults.Conflict("A verificação em duas etapas já está ativa.");

        var secret = Totp.NewSecret();
        user.TotpPendingSecretProtected = TwoFactor.SecretProtector(protection).Protect(secret);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new TwoFactorSetup(secret, Totp.OtpAuthUri(secret, user.Email)));
    }

    private static async Task<IResult> Enable(TwoFactorCodeRequest? request, HttpContext http, AppDbContext db,
        CurrentUser current, IDataProtectionProvider protection, TimeProvider time, CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return AuthEndpoints.CookieOnly();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        if (user is null) return ApiResults.Unauthorized();
        if (user.TotpEnabledAt is not null)
            return ApiResults.Conflict("A verificação em duas etapas já está ativa.");
        if (user.TotpPendingSecretProtected is null)
            return ApiResults.Validation("Gere o código de configuração antes de ativar.");

        var now = time.GetUtcNow();
        var secret = TwoFactor.UnprotectSecret(protection, user.TotpPendingSecretProtected);
        if (secret is null || !Totp.Verify(secret, request?.Code, now)) return InvalidCode();

        user.TotpSecretProtected = user.TotpPendingSecretProtected;
        user.TotpPendingSecretProtected = null;
        user.TotpEnabledAt = now.UtcDateTime;
        await db.TotpRecoveryCodes.Where(c => c.UserId == user.Id).ExecuteDeleteAsync(ct);
        var codes = TwoFactor.NewRecoveryCodes();
        foreach (var code in codes)
            db.TotpRecoveryCodes.Add(new TotpRecoveryCode { UserId = user.Id, CodeHash = TwoFactor.HashRecoveryCode(code) });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new TwoFactorEnabled(codes));
    }

    private static async Task<IResult> Disable(TwoFactorCodeRequest? request, HttpContext http, AppDbContext db,
        CurrentUser current, IDataProtectionProvider protection, TimeProvider time, CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return AuthEndpoints.CookieOnly();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        if (user is null) return ApiResults.Unauthorized();
        if (user.TotpEnabledAt is null) return ApiResults.Validation("A verificação em duas etapas não está ativa.");

        if (!await TwoFactor.VerifyAsync(db, protection, user, request?.Code, time.GetUtcNow().UtcDateTime, ct))
            return InvalidCode();

        user.TotpSecretProtected = null;
        user.TotpPendingSecretProtected = null;
        user.TotpEnabledAt = null;
        await db.TotpRecoveryCodes.Where(c => c.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ---- Sessions ----

    private static async Task<IResult> ListSessions(HttpContext http, AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return AuthEndpoints.CookieOnly();
        var currentSid = AuthSession.CurrentSessionId(http.User);
        var since = time.GetUtcNow().UtcDateTime - AuthSession.SessionLifetime;
        var sessions = await db.UserSessions.AsNoTracking()
            .Where(s => s.RevokedAt == null && s.LastSeenAt > since)
            .OrderByDescending(s => s.LastSeenAt)
            .Select(s => new { s.Id, s.CreatedAt, s.LastSeenAt, s.UserAgent })
            .ToListAsync(ct);
        return Results.Ok(sessions.Select(s => new SessionInfo(s.Id, s.CreatedAt, s.LastSeenAt, s.UserAgent, s.Id == currentSid)));
    }

    private static async Task<IResult> RevokeSession(Guid id, HttpContext http, AppDbContext db, TimeProvider time,
        CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return AuthEndpoints.CookieOnly();
        if (id == AuthSession.CurrentSessionId(http.User))
        {
            await AuthSession.SignOutAsync(http);
            return Results.NoContent();
        }

        var now = time.GetUtcNow().UtcDateTime;
        var revoked = await db.UserSessions
            .Where(s => s.Id == id && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
        return revoked == 0 ? ApiResults.NotFound("Sessão não encontrada.") : Results.NoContent();
    }

    private static async Task<IResult> RevokeOthers(HttpContext http, AppDbContext db, CurrentUser current, TimeProvider time,
        CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return AuthEndpoints.CookieOnly();
        await AuthSession.RevokeAllAsync(db, current.Id, AuthSession.CurrentSessionId(http.User), time.GetUtcNow().UtcDateTime, ct);
        return Results.NoContent();
    }

    // ---- Preferences ----

    private static async Task<IResult> SetDisplayName(DisplayNameRequest? request, AppDbContext db, CurrentUser current, CancellationToken ct)
    {
        var name = Text.Clean(request?.DisplayName);
        if (name is null) return ApiResults.Validation("Informe um nome.");
        if (name.Length > 80) return ApiResults.Validation("O nome deve ter no máximo 80 caracteres.");
        await db.Users.Where(u => u.Id == current.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.DisplayName, name), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetLocale(LocaleRequest? request, HttpContext http, AppDbContext db, CurrentUser current,
        CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return AuthEndpoints.CookieOnly();
        var locale = Locales.Normalize(request?.Locale);
        if (locale is null) return ApiResults.Validation("Idioma inválido. Use \"pt-BR\" ou \"en\".");
        await db.Users.Where(u => u.Id == current.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Locale, locale), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CompleteOnboarding(HttpContext http, AppDbContext db, CurrentUser current,
        TimeProvider time, CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return AuthEndpoints.CookieOnly();
        var now = time.GetUtcNow().UtcDateTime;
        await db.Users.Where(u => u.Id == current.Id && u.OnboardedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.OnboardedAt, now), ct);
        return Results.NoContent();
    }

    private static IResult InvalidCode() =>
        // 400 (not 401): the caller is signed in; a 401 would read as "session ended".
        ApiResults.Error(StatusCodes.Status400BadRequest, ErrorCodes.InvalidTwoFactorCode, "Código inválido.");

    private static IResult DemoForbidden() =>
        ApiResults.Error(StatusCodes.Status403Forbidden, ErrorCodes.Forbidden,
            "Contas de demonstração não podem ativar a verificação em duas etapas.");
}

/// <summary>The account languages: "pt-BR" (default) and "en".</summary>
public static class Locales
{
    public const string Portuguese = "pt-BR";
    public const string English = "en";

    /// <summary>"pt-BR" / "en" for any pt* / en* tag (case-insensitive); null otherwise.</summary>
    public static string? Normalize(string? value)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.StartsWith("pt", StringComparison.OrdinalIgnoreCase)) return Portuguese;
        if (v.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return English;
        return null;
    }

    /// <summary>
    /// Like the web app: Portuguese when the visitor's top-ranked Accept-Language is pt* (or there is none),
    /// English otherwise.
    /// </summary>
    public static string FromRequest(HttpRequest request)
    {
        var top = request.GetTypedHeaders().AcceptLanguage
            .Where(l => l.Value.HasValue && l.Value != "*" && (l.Quality ?? 1.0) > 0)
            .OrderByDescending(l => l.Quality ?? 1.0)
            .FirstOrDefault();
        if (top is null) return Portuguese;
        return Normalize(top.Value.Value) == Portuguese ? Portuguese : English;
    }
}
