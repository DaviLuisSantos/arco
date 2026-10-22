using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Arco.Api.Features.Auth;

public static class AuthEndpoints
{
    public sealed record LoginRequest(string? Password);

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/auth");
        group.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting(Startup.LoginRateLimitPolicy);
        group.MapPost("/logout", (Delegate)Logout).RequireAuthorization();
        group.MapGet("/me", Me).AllowAnonymous();
        return api;
    }

    private static async Task<IResult> Login(LoginRequest? request, PasswordVerifier verifier, HttpContext http)
    {
        if (string.IsNullOrEmpty(request?.Password))
            return ApiResults.Validation("Informe a senha.");

        if (!verifier.Verify(request.Password))
            return ApiResults.Error(StatusCodes.Status401Unauthorized, ErrorCodes.InvalidPassword, "Senha incorreta.");

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "owner")], Startup.AuthScheme);
        await http.SignInAsync(Startup.AuthScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
        return Results.NoContent();
    }

    private static async Task<IResult> Logout(HttpContext http)
    {
        await http.SignOutAsync(Startup.AuthScheme);
        return Results.NoContent();
    }

    private static IResult Me(HttpContext http) =>
        http.User.Identity?.IsAuthenticated == true
            ? Results.Ok(new { authenticated = true })
            : ApiResults.Unauthorized();
}
