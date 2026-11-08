using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Arco.Api.Features.Auth;

/// <summary>How the session was opened (claim <c>amr</c> of the app cookie).</summary>
public static class AuthMethods
{
    public const string Password = "pwd";
    public const string Google = "google";
    public const string Demo = "demo";
}

/// <summary>Builds and validates the app's session cookie: <c>NameIdentifier</c>, <c>stamp</c> and <c>amr</c> claims.</summary>
public static class AuthSession
{
    public const string StampClaim = "stamp";

    public static ClaimsPrincipal CreatePrincipal(User user, string method)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(StampClaim, user.SecurityStamp.ToString()),
            new Claim(Infrastructure.ApiKeys.AuthMethodClaim, method),
        ], Startup.AuthScheme);
        return new ClaimsPrincipal(identity);
    }

    public static Task SignInAsync(HttpContext http, User user, string method) =>
        http.SignInAsync(Startup.AuthScheme, CreatePrincipal(user, method),
            new AuthenticationProperties { IsPersistent = method != AuthMethods.Demo });

    /// <summary>Rejects sessions whose user no longer exists or whose security stamp changed.</summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext ctx)
    {
        var principal = ctx.Principal;
        if (!int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
                CultureInfo.InvariantCulture, out var userId)
            || !Guid.TryParse(principal?.FindFirstValue(StampClaim), out var stamp))
        {
            await Reject(ctx);
            return;
        }

        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var current = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (Guid?)u.SecurityStamp)
            .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);
        if (current != stamp) await Reject(ctx);
    }

    private static async Task Reject(CookieValidatePrincipalContext ctx)
    {
        ctx.RejectPrincipal();
        await ctx.HttpContext.SignOutAsync(Startup.AuthScheme);
    }

    public static string DisplayNameFromEmail(string email)
    {
        var at = email.IndexOf('@');
        var name = at > 0 ? email[..at] : email;
        return name.Length > 80 ? name[..80] : name;
    }
}
