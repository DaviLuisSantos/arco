namespace Arco.Api.Features.Pauses;

public static class PauseEndpoints
{
    public const int MaxStartDaysInPast = 7;
    public const int MaxDurationDays = 365;
    public const int MaxReasonLength = 140;

    public const string InvalidRangeCode = "pause_invalid_range";
    public const string OverlapCode = "pause_overlap";
    public const string HabitArchivedCode = "habit_archived";
    public const string NoActivePauseCode = "no_active_pause";

    public sealed record PauseRequest(DateOnly? StartDate, DateOnly? EndDate, string? Reason);

    public static IEndpointRouteBuilder MapPauses(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/habits/{id:int}").WithTags("Pausas");
        group.MapPost("/pauses", Create).WithSummary("Pausa um hábito por um período");
        group.MapDelete("/pauses/{pauseId:int}", Delete).WithSummary("Remove uma pausa");
        group.MapPost("/resume", Resume).WithSummary("Retoma um hábito pausado");
        return api;
    }

    /// <summary>Validates a pause period. Returns an error result or null; <paramref name="reason"/> comes back cleaned.</summary>
    internal static IResult? ValidateRange(DateOnly? start, DateOnly? end, bool endRequired, DateOnly today, string? rawReason, out string? reason)
    {
        reason = Text.Clean(rawReason);
        if (start is not { } s) return ApiResults.Error(400, InvalidRangeCode, "Informe a data de início da pausa.");
        if (endRequired && end is null) return ApiResults.Error(400, InvalidRangeCode, "Informe a data de término.");
        if (s < today.AddDays(-MaxStartDaysInPast))
            return ApiResults.Error(400, InvalidRangeCode, $"A pausa só pode começar nos últimos {MaxStartDaysInPast} dias ou depois.");
        if (end is { } e)
        {
            if (e < s) return ApiResults.Error(400, InvalidRangeCode, "A data de término deve ser igual ou posterior ao início.");
            if (e.DayNumber - s.DayNumber > MaxDurationDays)
                return ApiResults.Error(400, InvalidRangeCode, $"A pausa pode durar no máximo {MaxDurationDays} dias.");
        }
        if (reason is not null && reason.Length > MaxReasonLength)
            return ApiResults.Validation($"O motivo deve ter no máximo {MaxReasonLength} caracteres.");
        return null;
    }

    internal static bool Overlaps(IEnumerable<HabitPause> existing, DateOnly start, DateOnly? end) =>
        existing.Any(p => start <= (p.EndDate ?? DateOnly.MaxValue) && (end ?? DateOnly.MaxValue) >= p.StartDate);

    internal static bool IsCurrent(HabitPause p, DateOnly today) =>
        p.StartDate <= today && (p.EndDate is null || p.EndDate >= today);

    /// <summary>Resumes a pause in progress: deleted when it starts today, otherwise ends yesterday.</summary>
    internal static void EndPause(AppDbContext db, HabitPause pause, DateOnly today)
    {
        if (pause.StartDate >= today) db.HabitPauses.Remove(pause);
        else pause.EndDate = today.AddDays(-1);
    }

    private static async Task<IResult> Create(int id, PauseRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        if (request is null) return ApiResults.Validation("Corpo da requisição ausente.");
        var habit = await db.Habits.Include(h => h.Pauses).FirstOrDefaultAsync(h => h.Id == id, ct);
        if (habit is null) return ApiResults.NotFound("Hábito não encontrado.");
        if (habit.ArchivedAt is not null)
            return ApiResults.Error(400, HabitArchivedCode, "Hábitos arquivados não podem ser pausados.");

        var today = await clock.GetTodayAsync(ct);
        if (ValidateRange(request.StartDate, request.EndDate, endRequired: false, today, request.Reason, out var reason) is { } error)
            return error;
        if (Overlaps(habit.Pauses, request.StartDate!.Value, request.EndDate))
            return ApiResults.Error(400, OverlapCode, "Já existe uma pausa nesse período para este hábito.");

        var pause = new HabitPause
        {
            HabitId = habit.Id,
            StartDate = request.StartDate!.Value,
            EndDate = request.EndDate,
            Reason = reason,
            CreatedAt = clock.UtcNow,
        };
        db.HabitPauses.Add(pause);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/habits/{id}/pauses/{pause.Id}", HabitPauseDto.From(pause));
    }

    private static async Task<IResult> Delete(int id, int pauseId, AppDbContext db, CancellationToken ct)
    {
        var pause = await db.HabitPauses.FirstOrDefaultAsync(p => p.Id == pauseId && p.HabitId == id, ct);
        if (pause is null) return ApiResults.NotFound("Pausa não encontrada.");
        db.HabitPauses.Remove(pause);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Resume(int id, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        if (!await db.Habits.AnyAsync(h => h.Id == id, ct)) return ApiResults.NotFound("Hábito não encontrado.");
        var today = await clock.GetTodayAsync(ct);
        var pauses = await db.HabitPauses.Where(p => p.HabitId == id).ToListAsync(ct);
        var current = pauses.FirstOrDefault(p => IsCurrent(p, today));
        if (current is null)
            return ApiResults.Error(404, NoActivePauseCode, "Este hábito não tem uma pausa em curso.");
        EndPause(db, current, today);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
