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

/// <summary>
/// Builds and validates the app's session cookie: <c>NameIdentifier</c>, <c>stamp</c>, <c>amr</c> and <c>sid</c> claims.
/// Every cookie points to a <see cref="UserSession"/> row; revoking the row ends the cookie.
/// </summary>
public static class AuthSession
{
    public const string StampClaim = "stamp";
    public const string SessionClaim = "sid";
    /// <summary><see cref="UserSession.LastSeenAt"/> is written at most once per this interval.</summary>
    public static readonly TimeSpan LastSeenResolution = TimeSpan.FromMinutes(5);
    /// <summary>Matches the cookie's sliding lifetime: older sessions are no longer listed (their cookie has expired).</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public static ClaimsPrincipal CreatePrincipal(User user, string method, Guid sessionId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(StampClaim, user.SecurityStamp.ToString()),
            new Claim(Infrastructure.ApiKeys.AuthMethodClaim, method),
            new Claim(SessionClaim, sessionId.ToString()),
        ], Startup.AuthScheme);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Issues the app cookie. Opens a new <see cref="UserSession"/> unless <paramref name="sessionId"/> is given
    /// (re-issuing the cookie of the current session, e.g. after a password change).
    /// </summary>
    public static async Task SignInAsync(HttpContext http, User user, string method, Guid? sessionId = null)
    {
        var sid = sessionId ?? await OpenSessionAsync(http, user.Id);
        await http.SignInAsync(Startup.AuthScheme, CreatePrincipal(user, method, sid),
            new AuthenticationProperties { IsPersistent = method != AuthMethods.Demo });
    }

    /// <summary>The session id carried by the current cookie, if any.</summary>
    public static Guid? CurrentSessionId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(SessionClaim), out var sid) ? sid : null;

    /// <summary>Marks the current session revoked and drops the cookie.</summary>
    public static async Task SignOutAsync(HttpContext http)
    {
        if (CurrentSessionId(http.User) is { } sid && int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier),
                NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
        {
            var db = http.RequestServices.GetRequiredService<AppDbContext>();
            var now = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            await db.UserSessions.IgnoreQueryFilters()
                .Where(s => s.Id == sid && s.UserId == userId && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), http.RequestAborted);
        }
        await http.SignOutAsync(Startup.AuthScheme);
    }

    /// <summary>Revokes every open session of the user except <paramref name="keep"/>.</summary>
    public static Task RevokeAllAsync(AppDbContext db, int userId, Guid? keep, DateTime now, CancellationToken ct) =>
        db.UserSessions.IgnoreQueryFilters()
            .Where(s => s.UserId == userId && s.RevokedAt == null && (keep == null || s.Id != keep))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);

    private static async Task<Guid> OpenSessionAsync(HttpContext http, int userId)
    {
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var now = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId, // set explicitly: the request is still anonymous here
            CreatedAt = now,
            LastSeenAt = now,
            UserAgent = Truncate(http.Request.Headers.UserAgent.ToString(), 200),
            Ip = Truncate(http.Connection.RemoteIpAddress?.ToString() ?? "", 45),
        };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync(http.RequestAborted);
        return session.Id;
    }

    /// <summary>
    /// Rejects cookies whose user no longer exists, whose security stamp changed or whose session was revoked.
    /// Runs before <c>HttpContext.User</c> is set, so the user filter is bypassed with an explicit owner check.
    /// </summary>
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

        var http = ctx.HttpContext;
        var ct = http.RequestAborted;
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var current = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (Guid?)u.SecurityStamp)
            .FirstOrDefaultAsync(ct);
        if (current != stamp)
        {
            await Reject(ctx);
            return;
        }

        var now = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        if (CurrentSessionId(principal!) is not { } sid)
        {
            // Cookie issued before sessions existed: adopt it into a new session instead of signing the user out.
            var newSid = await OpenSessionAsync(http, userId);
            var identity = new ClaimsIdentity(principal!.Claims, Startup.AuthScheme);
            identity.AddClaim(new Claim(SessionClaim, newSid.ToString()));
            ctx.ReplacePrincipal(new ClaimsPrincipal(identity));
            ctx.ShouldRenew = true;
            return;
        }

        var session = await db.UserSessions.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.Id == sid && s.UserId == userId)
            .Select(s => new { s.RevokedAt, s.LastSeenAt })
            .FirstOrDefaultAsync(ct);
        if (session is null || session.RevokedAt is not null)
        {
            await Reject(ctx);
            return;
        }

        if (now - session.LastSeenAt >= LastSeenResolution)
        {
            await db.UserSessions.IgnoreQueryFilters()
                .Where(s => s.Id == sid && s.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenAt, now), ct);
        }
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

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}
