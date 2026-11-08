using System.Globalization;
using System.Net.Mail;
using System.Security.Claims;
using Arco.Api.Features.Demo;
using Arco.Api.Infrastructure.Email;
using Microsoft.AspNetCore.Authentication;
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

    public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName);
    public sealed record LoginRequest(string? Email, string? Password);
    public sealed record TokenRequest(string? Token);
    public sealed record ForgotPasswordRequest(string? Email);
    public sealed record ResetPasswordRequest(string? Token, string? Password);
    public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
    public sealed record DeleteAccountRequest(string? Password);

    public sealed record Me(int Id, string Email, string DisplayName, bool EmailVerified, bool IsDemo, bool HasPassword, bool HasGoogle)
    {
        public static Me From(User u) =>
            new(u.Id, u.Email, u.DisplayName, u.EmailVerifiedAt is not null, u.IsDemo, u.PasswordHash is not null, u.GoogleSubject is not null);
    }

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/auth");
        var limit = Startup.AuthActionsRateLimitPolicy;

        group.MapPost("/register", Register).AllowAnonymous().RequireRateLimiting(limit);
        group.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting(Startup.LoginRateLimitPolicy);
        group.MapPost("/logout", (Delegate)Logout).RequireAuthorization();
        group.MapGet("/me", GetMe).AllowAnonymous();
        group.MapGet("/providers", (IConfiguration config) => Results.Ok(new { google = GoogleAuth.IsConfigured(config) })).AllowAnonymous();
        group.MapPost("/demo", StartDemo).AllowAnonymous().RequireRateLimiting(limit);
        group.MapPost("/verify-email", VerifyEmail).AllowAnonymous();
        group.MapPost("/resend-verification", ResendVerification).RequireAuthorization().RequireRateLimiting(limit);
        group.MapPost("/forgot-password", ForgotPassword).AllowAnonymous().RequireRateLimiting(limit);
        group.MapPost("/reset-password", ResetPassword).AllowAnonymous();
        group.MapPost("/change-password", ChangePassword).RequireAuthorization();
        group.MapDelete("/account", DeleteAccount).RequireAuthorization();
        group.MapGet("/google", GoogleAuth.Challenge).AllowAnonymous();
        group.MapGet("/google/callback", GoogleAuth.Callback).AllowAnonymous();
        return api;
    }

    // ---- Sign up / in / out ----

    private static async Task<IResult> Register(RegisterRequest? request, AppDbContext db, TimeProvider time,
        IEmailSender email, IConfiguration config, HttpContext http, ILoggerFactory logs, CancellationToken ct)
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

        await SendVerificationAsync(db, email, config, http, user, now, logs, ct);
        await AuthSession.SignInAsync(http, user, AuthMethods.Password);
        return Results.Created("/api/auth/me", Me.From(user));
    }

    private static async Task<IResult> Login(LoginRequest? request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var address = NormalizeEmail(request?.Email);
        if (address is null || string.IsNullOrEmpty(request?.Password)) return ApiResults.Validation("Informe e-mail e senha.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address, ct);
        var ok = VerifyPassword(request.Password, user?.PasswordHash);
        if (user is null || !ok) return InvalidCredentials();

        await AuthSession.SignInAsync(http, user, AuthMethods.Password);
        return Results.NoContent();
    }

    private static async Task<IResult> Logout(HttpContext http)
    {
        await http.SignOutAsync(Startup.AuthScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMe(HttpContext http, AppDbContext db, CurrentUser current, CancellationToken ct)
    {
        if (!current.IsSet) return ApiResults.Unauthorized();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        return user is null ? ApiResults.Unauthorized() : Results.Ok(Me.From(user));
    }

    private static async Task<IResult> StartDemo(AppDbContext db, CurrentUser current, TimeProvider time, HttpContext http,
        CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var user = new User
        {
            Email = $"demo-{Guid.NewGuid():N}@demo.arco.local",
            DisplayName = "Visitante",
            IsDemo = true,
            EmailVerifiedAt = now,
            CreatedAt = now,
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
        return Results.Created("/api/auth/me", Me.From(user));
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
        IEmailSender email, IConfiguration config, HttpContext http, ILoggerFactory logs, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        if (user is null) return ApiResults.Unauthorized();
        if (user.EmailVerifiedAt is not null || user.IsDemo) return Results.NoContent();

        await SendVerificationAsync(db, email, config, http, user, time.GetUtcNow().UtcDateTime, logs, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPassword(ForgotPasswordRequest? request, AppDbContext db, TimeProvider time,
        IEmailSender email, IConfiguration config, HttpContext http, ILoggerFactory logs, CancellationToken ct)
    {
        // Always 204, so the endpoint does not reveal which e-mails have an account.
        var address = NormalizeEmail(request?.Email);
        if (address is null) return Results.NoContent();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address && u.EmailVerifiedAt != null && !u.IsDemo, ct);
        if (user is null) return Results.NoContent();

        var now = time.GetUtcNow().UtcDateTime;
        var raw = AuthTokens.Issue(db, user.Id, UserTokenPurpose.ResetPassword, AuthTokens.ResetPasswordLifetime, now);
        await db.SaveChangesAsync(ct);

        var (subject, html) = EmailTemplates.ResetPassword($"{BaseUrl(config, http)}/reset-password?token={raw}");
        await TrySendAsync(email, user.Email, subject, html, logs, ct);
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
        return Results.NoContent();
    }

    // ---- Account ----

    private static async Task<IResult> ChangePassword(ChangePasswordRequest? request, HttpContext http, AppDbContext db,
        CurrentUser current, CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return Forbidden();
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

        // Other sessions die with the old stamp; this one gets a cookie with the new stamp.
        var method = http.User.FindFirstValue(Infrastructure.ApiKeys.AuthMethodClaim) ?? AuthMethods.Password;
        await AuthSession.SignInAsync(http, user, method);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAccount([FromBody] DeleteAccountRequest? request, HttpContext http, AppDbContext db,
        CurrentUser current, CancellationToken ct)
    {
        if (http.User.UsedApiKey()) return Forbidden();
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

    private static async Task SendVerificationAsync(AppDbContext db, IEmailSender email, IConfiguration config, HttpContext http,
        User user, DateTime now, ILoggerFactory logs, CancellationToken ct)
    {
        var raw = AuthTokens.Issue(db, user.Id, UserTokenPurpose.VerifyEmail, AuthTokens.VerifyEmailLifetime, now);
        await db.SaveChangesAsync(ct);
        var (subject, html) = EmailTemplates.VerifyEmail($"{BaseUrl(config, http)}/verify-email?token={raw}");
        await TrySendAsync(email, user.Email, subject, html, logs, ct);
    }

    /// <summary>A failing mail provider must not break sign-up or leak whether an account exists.</summary>
    private static async Task TrySendAsync(IEmailSender email, string to, string subject, string html, ILoggerFactory logs,
        CancellationToken ct)
    {
        try
        {
            await email.SendAsync(to, subject, html, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logs.CreateLogger("Arco.Api.Features.Auth").LogError(ex, "Failed to send e-mail to {To}.", to);
        }
    }

    private static IResult EmailTaken() =>
        ApiResults.Error(StatusCodes.Status409Conflict, AuthCodes.EmailTaken, "Já existe uma conta com este e-mail.");

    private static IResult InvalidCredentials() =>
        ApiResults.Error(StatusCodes.Status401Unauthorized, AuthCodes.InvalidCredentials, "E-mail ou senha incorretos.");

    private static IResult InvalidToken() =>
        ApiResults.Error(StatusCodes.Status400BadRequest, AuthCodes.InvalidToken, "Link inválido ou expirado.");

    private static IResult Forbidden() =>
        ApiResults.Error(StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, CookieOnlyMessage);
}
