using System.Globalization;
using Arco.Api.Features.Habits;

namespace Arco.Api.Features.Settings;

public static class SettingsEndpoints
{
    /// <param name="Day">0 = domingo … 6 = sábado.</param>
    /// <param name="Time">"HH:mm" no fuso configurado.</param>
    public sealed record WeeklyReviewReminderDto(bool Enabled, int Day, string Time);
    public sealed record SettingsDto(string Timezone, WeeklyReviewReminderDto WeeklyReviewReminder);
    public sealed record WeeklyReviewReminderRequest(bool? Enabled, int? Day, string? Time);
    /// <summary>Fields left out stay as they are.</summary>
    public sealed record UpdateSettingsRequest(string? Timezone, WeeklyReviewReminderRequest? WeeklyReviewReminder);

    public static IEndpointRouteBuilder MapSettings(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/settings");
        group.MapGet("", Get);
        group.MapPut("", Update);
        return api;
    }

    private static async Task<SettingsDto> Get(AppDbContext db, CancellationToken ct) =>
        ToDto(await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == Domain.Settings.SingletonId, ct)
              ?? new Domain.Settings());

    private static async Task<IResult> Update(UpdateSettingsRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        if (request is null) return ApiResults.Validation("Corpo da requisição ausente.");

        var timezone = Text.Clean(request.Timezone);
        if (request.Timezone is not null && !TimeZones.IsValidIana(timezone))
            return ApiResults.Validation("Fuso horário inválido. Use um identificador IANA, como America/Sao_Paulo.");

        var reminder = request.WeeklyReviewReminder;
        if (reminder?.Day is < 0 or > 6)
            return ApiResults.Validation("O dia da revisão deve estar entre 0 (domingo) e 6 (sábado).");
        TimeOnly? time = null;
        if (reminder?.Time is not null)
        {
            if (!HabitEndpoints.TryParseTime(reminder.Time.Trim(), out var parsed))
                return ApiResults.Validation("O horário da revisão deve estar no formato HH:mm.");
            time = parsed;
        }

        var settings = await db.Settings.FirstOrDefaultAsync(s => s.Id == Domain.Settings.SingletonId, ct);
        if (settings is null)
        {
            settings = new Domain.Settings();
            db.Settings.Add(settings);
        }
        if (timezone is not null) settings.Timezone = timezone;
        if (reminder?.Enabled is { } enabled) settings.WeeklyReviewReminderEnabled = enabled;
        if (reminder?.Day is { } day) settings.WeeklyReviewReminderDay = day;
        if (time is { } t) settings.WeeklyReviewReminderTime = t;

        await db.SaveChangesAsync(ct);
        clock.Invalidate();
        return Results.Ok(ToDto(settings));
    }

    private static SettingsDto ToDto(Domain.Settings s) => new(s.Timezone, new WeeklyReviewReminderDto(
        s.WeeklyReviewReminderEnabled, s.WeeklyReviewReminderDay,
        s.WeeklyReviewReminderTime.ToString("HH:mm", CultureInfo.InvariantCulture)));
}
