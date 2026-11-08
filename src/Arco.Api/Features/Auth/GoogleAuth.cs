using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

namespace Arco.Api.Features.Auth;

/// <summary>Login with Google: active only with <c>Auth:Google:ClientId</c> and <c>Auth:Google:ClientSecret</c>.</summary>
public static class GoogleAuth
{
    public const string CallbackRoute = "/api/auth/google/callback";
    private const string FailureRedirect = "/login?error=google";

    public static bool IsConfigured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config["Auth:Google:ClientId"]) && !string.IsNullOrWhiteSpace(config["Auth:Google:ClientSecret"]);

    /// <summary>Only same-site absolute paths; anything else becomes "/".</summary>
    public static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl[0] == '/' && (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'))
            ? returnUrl
            : "/";

    public static IResult Challenge(string? returnUrl, IConfiguration config)
    {
        if (!IsConfigured(config)) return ApiResults.NotFound("Login com Google não está configurado.");
        var props = new AuthenticationProperties
        {
            RedirectUri = CallbackRoute + "?returnUrl=" + Uri.EscapeDataString(SafeReturnUrl(returnUrl)),
        };
        return Results.Challenge(props, [GoogleDefaults.AuthenticationScheme]);
    }

    public static async Task<IResult> Callback(string? returnUrl, HttpContext http, AppDbContext db, TimeProvider time,
        CancellationToken ct)
    {
        var external = await http.AuthenticateAsync(Startup.ExternalScheme);
        await http.SignOutAsync(Startup.ExternalScheme);
        if (!external.Succeeded || external.Principal is not { } principal) return Results.Redirect(FailureRedirect);

        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        var emailVerified = string.Equals(principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(subject)) return Results.Redirect(FailureRedirect);

        var now = time.GetUtcNow().UtcDateTime;
        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleSubject == subject, ct);
        if (user is null)
        {
            if (string.IsNullOrEmpty(email) || !emailVerified) return Results.Redirect(FailureRedirect);

            user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
            if (user is not null)
            {
                // Linking by a verified e-mail. If the local account never proved the e-mail, whoever registered it
                // may not be the owner: drop their password and sessions.
                if (user.EmailVerifiedAt is null)
                {
                    user.PasswordHash = null;
                    user.SecurityStamp = Guid.NewGuid();
                }
                user.GoogleSubject = subject;
                user.EmailVerifiedAt ??= now;
            }
            else
            {
                var name = Text.Clean(principal.FindFirstValue(ClaimTypes.Name)) ?? AuthSession.DisplayNameFromEmail(email);
                user = new User
                {
                    Email = email,
                    GoogleSubject = subject,
                    DisplayName = name.Length > 80 ? name[..80] : name,
                    EmailVerifiedAt = now,
                    CreatedAt = now,
                };
                db.Users.Add(user);
            }
            await db.SaveChangesAsync(ct);
        }

        await AuthSession.SignInAsync(http, user, AuthMethods.Google);
        return Results.Redirect(SafeReturnUrl(returnUrl));
    }
}
