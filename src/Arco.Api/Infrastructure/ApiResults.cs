namespace Arco.Api.Infrastructure;

public sealed record ApiError(string Error, string Code);

public static class ErrorCodes
{
    public const string Validation = "validation";
    public const string Unauthorized = "unauthorized";
    public const string InvalidPassword = "invalid_password";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string ActiveCycleExists = "active_cycle_exists";
    public const string Conflict = "conflict";
    public const string RateLimited = "rate_limited";
    public const string InternalError = "internal_error";
}

/// <summary>Helpers that produce the API's <c>{ error, code }</c> error bodies.</summary>
public static class ApiResults
{
    public static IResult Error(int status, string code, string message) =>
        Results.Json(new ApiError(message, code), statusCode: status);

    public static IResult Validation(string message) =>
        Error(StatusCodes.Status400BadRequest, ErrorCodes.Validation, message);

    public static IResult NotFound(string message = "Recurso não encontrado.") =>
        Error(StatusCodes.Status404NotFound, ErrorCodes.NotFound, message);

    public static IResult Conflict(string message, string code = ErrorCodes.Conflict) =>
        Error(StatusCodes.Status409Conflict, code, message);

    public static IResult Unauthorized(string message = "Não autenticado.") =>
        Error(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, message);

    public static Task WriteErrorAsync(HttpContext http, int status, string code, string message, CancellationToken ct = default)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(new ApiError(message, code), ct);
    }
}

/// <summary>Small text-validation helpers (pt-BR messages are built by callers).</summary>
public static class Text
{
    public static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static bool LengthBetween(string? value, int min, int max) =>
        value is not null && value.Length >= min && value.Length <= max;
}
