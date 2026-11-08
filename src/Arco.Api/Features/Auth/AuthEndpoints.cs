using System.Globalization;
using System.Net.Mail;
using System.Security.Claims;
using Arco.Api.Features.Demo;
using Arco.Api.Infrastructure.Email;
using Arco.Api.Infrastructure.Outbox;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Arco.Api.Features.Auth;

public static class AuthEndpoints
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 72; // BCrypt ignores anything past 72 bytes.
    private const int BcryptWorkFactor = 11;
    private const string CookieOnlyMessage = "Esta ação só pode ser feita pelo app, não por uma chave de API.";

    /// <summary>Verified against when the user does not exist, so both failures take the same time.</summary>
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("arco-dummy-password", BcryptWorkFactor);

    public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName, string? Locale = null);
    public sealed record LoginRequest(string? Email, string? Password);
    public sealed record LoginTwoFactorRequest(string? Ticket, string? Code);
    public sealed record TwoFactorChallenge(bool TwoFactorRequired, string Ticket);
    public sealed record TokenRequest(string? Token);
    public sealed record ForgotPasswordRequest(string? Email);
    public sealed record ResetPasswordRequest(string? Token, string? Password);
    public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
    public sealed record DeleteAccountRequest(string? Password);

    public sealed record PublicProfileInfo(bool Enabled, string? Slug, bool ShowHabits, string Theme);

    public sealed record Me(int Id, string Email, string DisplayName, bool EmailVerified, bool IsDemo, bool HasPassword,
        bool HasGoogle, bool TwoFactorEnabled, string Plan, string Locale, bool IsAdmin, bool Onboarded, PublicProfileInfo PublicProfile)
    {
        public static Me From(User u, IConfiguration config) =>
            new(u.Id, u.Email, u.DisplayName, u.EmailVerifiedAt is not null, u.IsDemo, u.PasswordHash is not null,
                u.GoogleSubject is not null, u.TotpEnabledAt is not null, u.Plan == UserPlan.Pro ? "pro" : "free", u.Locale,
                IsAdminEmail(config, u.Email), u.OnboardedAt is not null,
                new PublicProfileInfo(u.PublicProfileEnabled, u.PublicSlug, u.PublicShowHabits, u.PublicTheme));
    }

    /// <summary>Admins are the accounts whose e-mail is listed in <c>Admin:Emails</c> (comma-separated).</summary>
    public static bool IsAdminEmail(IConfiguration config, string email) =>
        !string.IsNullOrEmpty(email) && (config["Admin:Emails"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase));

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Autenticação");
        var limit = Startup.AuthActionsRateLimitPolicy;

        group.MapPost("/register", Register).AllowAnonymous().RequireRateLimiting(limit).WithSummary("Cria uma conta e entra");
        group.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting(Startup.LoginRateLimitPolicy)
            .WithSummary("Entra com e-mail e senha (204; com 2FA, 200 com o ticket)")
            .Produces(StatusCodes.Status204NoContent).Produces<TwoFactorChallenge>().Produces<ApiError>(StatusCodes.Status401Unauthorized);
        group.MapPost("/login/2fa", LoginTwoFactor).AllowAnonymous().RequireRateLimiting(Startup.LoginRateLimitPolicy)
            .WithSummary("Conclui o login com o código TOTP ou de recuperação")
            .Produces(StatusCodes.Status204NoContent).Produces<ApiError>(StatusCodes.Status401Unauthorized);
        group.MapPost("/logout", (Delegate)Logout).RequireAuthorization().WithSummary("Sai e encerra a sessão atual");
        group.MapGet("/me", GetMe).AllowAnonymous().WithSummary("Usuário logado").Produces<Me>()
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);
        group.MapGet("/providers", (IConfiguration config) => Results.Ok(new { google = GoogleAuth.IsConfigured(config) })).AllowAnonymous()
            .WithSummary("Provedores de login disponíveis");
        group.MapPost("/demo", StartDemo).AllowAnonymous().RequireRateLimiting(limit).WithSummary("Cria uma conta de demonstração");
        group.MapPost("/verify-email", VerifyEmail).AllowAnonymous().WithSummary("Confirma o e-mail");
        group.MapPost("/resend-verification", ResendVerification).RequireAuthorization().RequireRateLimiting(limit)
            .WithSummary("Reenvia o e-mail de confirmação");
        group.MapPost("/forgot-password", ForgotPassword).AllowAnonymous().RequireRateLimiting(limit)
            .WithSummary("Envia o link de redefinição de senha");
        group.MapPost("/reset-password", ResetPassword).AllowAnonymous().WithSummary("Redefine a senha");
        group.MapPost("/change-password", ChangePassword).RequireAuthorization().WithSummary("Troca a senha");
        group.MapDelete("/account", DeleteAccount).RequireAuthorization().WithSummary("Exclui a conta");
        group.MapGet("/google", GoogleAuth.Challenge).AllowAnonymous().WithSummary("Inicia o login com Google");
        group.MapGet("/google/complete", GoogleAuth.Callback).AllowAnonymous().ExcludeFromDescription();
        group.MapAccountSecurity();
        return api;
    }

    // ---- Sign up / in / out ----

    private static async Task<IResult> Register(RegisterRequest? request, AppDbContext db, TimeProvider time,
        OutboxProcessor outbox, IConfiguration config, HttpContext http, CancellationToken ct)
    {
        var address = NormalizeEmail(request?.Email);
        if (address is null) return ApiResults.Validation("Informe um e-mail válido.");
        if (!ValidPassword(request?.Password, out var passwordError)) return ApiResults.Validation(passwordError);
        var name = Text.Clean(request?.DisplayName) ?? AuthSession.DisplayNameFromEmail(address);
        if (name.Length > 80) return ApiResults.Validation("O nome deve ter no máximo 80 caracteres.");

        if (await db.Users.AnyAsync(u => u.Email == address, ct)) return EmailTaken();

        var now = time.GetUtcNow().UtcDateTime;
        var user = new User
        {
            Email = address,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request!.Password, BcryptWorkFactor),
            DisplayName = name,
            CreatedAt = now,
            Locale = Locales.Normalize(request?.Locale) ?? Locales.FromRequest(http.Request),
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race for the same e-mail (unique index).
            db.ChangeTracker.Clear();
            if (await db.Users.AsNoTracking().AnyAsync(u => u.Email == address, ct)) return EmailTaken();
            throw;
        }

        await SendVerificationAsync(db, outbox, config, http, user, now, ct);
        await AuthSession.SignInAsync(http, user, AuthMethods.Password);
        return Results.Created("/api/auth/me", Me.From(user, config));
    }

    private static async Task<IResult> Login(LoginRequest? request, AppDbContext db, HttpContext http,
        IDataProtectionProvider protection, TimeProvider time, CancellationToken ct)
    {
        var address = NormalizeEmail(request?.Email);
        if (address is null || string.IsNullOrEmpty(request?.Password)) return ApiResults.Validation("Informe e-mail e senha.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address, ct);
        var ok = VerifyPassword(request.Password, user?.PasswordHash);
        if (user is null || !ok) return InvalidCredentials();

        if (user.TotpEnabledAt is not null)
        {
            var ticket = TwoFactor.IssueTicket(protection, user, AuthMethods.Password, time.GetUtcNow().UtcDateTime);
            return Results.Ok(new TwoFactorChallenge(true, ticket));
        }

        await AuthSession.SignInAsync(http, user, AuthMethods.Password);
        return Results.NoContent();
    }

    /// <summary>Second step of a login (password or Google) on an account with two-step verification.</summary>
    private static async Task<IResult> LoginTwoFactor(LoginTwoFactorRequest? request, AppDbContext db, HttpContext http,
        IDataProtectionProvider protection, TimeProvider time, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var ticket = TwoFactor.ReadTicket(protection, request?.Ticket, now);
        if (ticket is null)
        {
            return ApiResults.Error(StatusCodes.Status401Unauthorized, ErrorCodes.InvalidTwoFactorCode,
                "A verificação expirou. Entre novamente.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == ticket.UserId, ct);
        if (user is null || user.SecurityStamp != ticket.Stamp || user.TotpEnabledAt is null
            || !await TwoFactor.VerifyAsync(db, protection, user, request?.Code, now, ct))
        {
            return ApiResults.Error(StatusCodes.Status401Unauthorized, ErrorCodes.InvalidTwoFactorCode, "Código inválido.");
        }

        await db.SaveChangesAsync(ct); // a recovery code is now used
        await AuthSession.SignInAsync(http, user, ticket.Method);
        return Results.NoContent();
    }

    private static async Task<IResult> Logout(HttpContext http)
    {
        await AuthSession.SignOutAsync(http);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMe(HttpContext http, AppDbContext db, CurrentUser current, IConfiguration config,
        CancellationToken ct)
    {
        if (!current.IsSet) return ApiResults.Unauthorized();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        return user is null ? ApiResults.Unauthorized() : Results.Ok(Me.From(user, config));
    }

    private static async Task<IResult> StartDemo(AppDbContext db, CurrentUser current, TimeProvider time, HttpContext http,
        IConfiguration config, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var user = new User
        {
            Email = $"demo-{Guid.NewGuid():N}@demo.arco.local",
            DisplayName = "Visitante",
            IsDemo = true,
            EmailVerifiedAt = now,
            CreatedAt = now,
            Plan = UserPlan.Pro,
            OnboardedAt = now,
            Locale = Locales.FromRequest(http.Request),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        try
        {
            current.Impersonate(user.Id);
            await DemoSeeder.SeedAsync(db, TimeZones.Today(time, TimeZones.Default), ct);
        }
        catch
        {
            db.ChangeTracker.Clear();
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync(CancellationToken.None);
            throw;
        }

        await AuthSession.SignInAsync(http, user, AuthMethods.Demo);
        return Results.Created("/api/auth/me", Me.From(user, config));
    }

    // ---- E-mail verification and password recovery ----

    private static async Task<IResult> VerifyEmail(TokenRequest? request, AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var token = await AuthTokens.FindValidAsync(db, request?.Token, UserTokenPurpose.VerifyEmail, now, ct);
        if (token is null) return InvalidToken();

        token.UsedAt = now;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is not null) user.EmailVerifiedAt ??= now;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ResendVerification(AppDbContext db, CurrentUser current, TimeProvider time,
        OutboxProcessor outbox, IConfiguration config, HttpContext http, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        if (user is null) return ApiResults.Unauthorized();
        if (user.EmailVerifiedAt is not null || user.IsDemo) return Results.NoContent();

        await SendVerificationAsync(db, outbox, config, http, user, time.GetUtcNow().UtcDateTime, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPassword(ForgotPasswordRequest? request, AppDbContext db, TimeProvider time,
        OutboxProcessor outbox, IConfiguration config, HttpContext http, CancellationToken ct)
    {
        // Always 204, so the endpoint does not reveal which e-mails have an account.
        var address = NormalizeEmail(request?.Email);
        if (address is null) return Results.NoContent();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address && u.EmailVerifiedAt != null && !u.IsDemo, ct);
        if (user is null) return Results.NoContent();

        var now = time.GetUtcNow().UtcDateTime;
        var raw = AuthTokens.Issue(db, user.Id, UserTokenPurpose.ResetPassword, AuthTokens.ResetPasswordLifetime, now);
        var (subject, html) = EmailTemplates.ResetPassword($"{BaseUrl(config, http)}/reset-password?token={raw}", user.Locale);
        // The token and the e-mail are saved together: if the provider is down the worker sends it later.
        var message = Outbox.Enqueue(db, OutboxKinds.Email, new EmailOutboxPayload(user.Email, subject, html), now);
        await db.SaveChangesAsync(ct);
        await outbox.TryDispatchAsync(message.Id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetPassword(ResetPasswordRequest? request, AppDbContext db, TimeProvider time,
        CancellationToken ct)
    {
        if (!ValidPassword(request?.Password, out var passwordError)) return ApiResults.Validation(passwordError);

        var now = time.GetUtcNow().UtcDateTime;
        var token = await AuthTokens.FindValidAsync(db, request?.Token, UserTokenPurpose.ResetPassword, now, ct);
        if (token is null) return InvalidToken();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null) return InvalidToken();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request!.Password, BcryptWorkFactor);
        user.SecurityStamp = Guid.NewGuid(); // signs out every session
        user.EmailVerifiedAt ??= now; // receiving the link proves the e-mail
        token.UsedAt = now;
        await db.UserTokens
            .Where(t => t.UserId == user.Id && t.Purpose == UserTokenPurpose.ResetPassword && t.UsedAt == null && t.Id != token.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct);
        await db.SaveChangesAsync(ct);
        await AuthSession.RevokeAllAsync(db, user.Id, keep: null, now, ct);
        return Results.NoContent();
    }

    // ---- Account ----

    private static async Task<IResult> ChangePassword(ChangePasswordRequest? request, HttpContext http, AppDbContext db,
        CurrentUser current, TimeProvider time, CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return CookieOnly();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        if (user is null) return ApiResults.Unauthorized();
        if (user.IsDemo) return ApiResults.Error(StatusCodes.Status403Forbidden, ErrorCodes.Forbidden,
            "Contas de demonstração não podem definir senha.");
        if (!ValidPassword(request?.NewPassword, out var passwordError)) return ApiResults.Validation(passwordError);

        if (user.PasswordHash is not null && !VerifyPassword(request?.CurrentPassword, user.PasswordHash))
            return InvalidCredentials();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request!.NewPassword, BcryptWorkFactor);
        user.SecurityStamp = Guid.NewGuid();
        await db.SaveChangesAsync(ct);

        // Other sessions die with the old stamp (and are marked revoked); this one gets a cookie with the new stamp.
        var method = http.User.FindFirstValue(Infrastructure.ApiKeys.AuthMethodClaim) ?? AuthMethods.Password;
        var sid = AuthSession.CurrentSessionId(http.User);
        await AuthSession.RevokeAllAsync(db, user.Id, sid, time.GetUtcNow().UtcDateTime, ct);
        await AuthSession.SignInAsync(http, user, method, sid);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAccount([FromBody] DeleteAccountRequest? request, HttpContext http, AppDbContext db,
        CurrentUser current, CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return CookieOnly();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        if (user is null) return ApiResults.Unauthorized();
        if (user.PasswordHash is not null && !VerifyPassword(request?.Password, user.PasswordHash)) return InvalidCredentials();

        await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync(ct); // owned rows go away by cascade
        await http.SignOutAsync(Startup.AuthScheme);
        return Results.NoContent();
    }

    // ---- Helpers ----

    private static bool VerifyPassword(string? password, string? hash)
    {
        try
        {
            // With no hash (unknown user, or a Google-only account) still burn the same time.
            var ok = BCrypt.Net.BCrypt.Verify(password ?? "", hash ?? DummyHash);
            return ok && hash is not null && !string.IsNullOrEmpty(password);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    private static string? NormalizeEmail(string? value)
    {
        var email = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email) || email.Length > 254) return null;
        return MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Contains('@') && email.Contains('.', StringComparison.Ordinal)
            ? email
            : null;
    }

    private static bool ValidPassword(string? password, out string error)
    {
        error = $"A senha deve ter entre {MinPasswordLength} e {MaxPasswordLength} caracteres.";
        return password is { Length: >= MinPasswordLength and <= MaxPasswordLength };
    }

    private static string BaseUrl(IConfiguration config, HttpContext http) =>
        (config["App:BaseUrl"] ?? $"{http.Request.Scheme}://{http.Request.Host}").TrimEnd('/');

    private static async Task SendVerificationAsync(AppDbContext db, OutboxProcessor outbox, IConfiguration config, HttpContext http,
        User user, DateTime now, CancellationToken ct)
    {
        var raw = AuthTokens.Issue(db, user.Id, UserTokenPurpose.VerifyEmail, AuthTokens.VerifyEmailLifetime, now);
        var (subject, html) = EmailTemplates.VerifyEmail($"{BaseUrl(config, http)}/verify-email?token={raw}", user.Locale);
        // The token and the e-mail are saved together: a failing mail provider must not break sign-up, the worker retries.
        var message = Outbox.Enqueue(db, OutboxKinds.Email, new EmailOutboxPayload(user.Email, subject, html), now);
        await db.SaveChangesAsync(ct);
        await outbox.TryDispatchAsync(message.Id, ct);
    }

    private static IResult EmailTaken() =>
        ApiResults.Error(StatusCodes.Status409Conflict, AuthCodes.EmailTaken, "Já existe uma conta com este e-mail.");

    private static IResult InvalidCredentials() =>
        ApiResults.Error(StatusCodes.Status401Unauthorized, AuthCodes.InvalidCredentials, "E-mail ou senha incorretos.");

    private static IResult InvalidToken() =>
        ApiResults.Error(StatusCodes.Status400BadRequest, AuthCodes.InvalidToken, "Link inválido ou expirado.");

    /// <summary>403 for actions that only the app (cookie) may perform, not an API key.</summary>
    internal static IResult CookieOnly() =>
        ApiResults.Error(StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, CookieOnlyMessage);
}
