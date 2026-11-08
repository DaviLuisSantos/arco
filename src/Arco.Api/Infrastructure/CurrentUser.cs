using System.Globalization;
using System.Security.Claims;

namespace Arco.Api.Infrastructure;

/// <summary>
/// Scoped: the user the request (or job) acts as. Read from <see cref="ClaimTypes.NameIdentifier"/> of the
/// authenticated principal; background jobs call <see cref="Impersonate"/>. Id 0 = nobody (queries on
/// <see cref="IUserOwned"/> entities return nothing).
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor? accessor = null)
{
    private int? _impersonated;

    public int Id => _impersonated ?? FromPrincipal(accessor?.HttpContext?.User);

    public bool IsSet => Id > 0;

    public void Impersonate(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        _impersonated = id;
    }

    private static int FromPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true) return 0;
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : 0;
    }
}
