namespace Arco.Api.Features.Settings;

public static class SettingsEndpoints
{
    public sealed record SettingsDto(string Timezone);
    public sealed record UpdateSettingsRequest(string? Timezone);

    public static IEndpointRouteBuilder MapSettings(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/settings");
        group.MapGet("", Get);
        group.MapPut("", Update);
        return api;
    }

    private static async Task<SettingsDto> Get(AppClock clock, CancellationToken ct) =>
        new(await clock.GetTimezoneAsync(ct));

    private static async Task<IResult> Update(UpdateSettingsRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var timezone = Text.Clean(request?.Timezone);
        if (!TimeZones.IsValidIana(timezone))
            return ApiResults.Validation("Fuso horário inválido. Use um identificador IANA, como America/Sao_Paulo.");

        var settings = await db.Settings.FirstOrDefaultAsync(s => s.Id == Domain.Settings.SingletonId, ct);
        if (settings is null)
        {
            settings = new Domain.Settings();
            db.Settings.Add(settings);
        }
        settings.Timezone = timezone!;
        await db.SaveChangesAsync(ct);
        clock.Invalidate();
        return Results.Ok(new SettingsDto(settings.Timezone));
    }
}
