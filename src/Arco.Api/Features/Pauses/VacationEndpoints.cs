namespace Arco.Api.Features.Pauses;

public static class VacationEndpoints
{
    public sealed record VacationRequest(DateOnly? StartDate, DateOnly? EndDate, string? Reason);
    public sealed record VacationDto(bool Active, DateOnly? StartDate, DateOnly? EndDate);

    public static IEndpointRouteBuilder MapVacation(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/vacation").WithTags("Pausas");
        group.MapGet("", Get).WithSummary("Estado do modo férias");
        group.MapPost("", Start).WithSummary("Ativa o modo férias (pausa todos os hábitos ativos)");
        group.MapDelete("", End).WithSummary("Encerra o modo férias");
        return api;
    }

    /// <summary>The vacation is the earliest current or future pause created by the vacation mode.</summary>
    private static async Task<IResult> Get(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var today = await clock.GetTodayAsync(ct);
        var pause = await db.HabitPauses.AsNoTracking()
            .Where(p => p.IsVacation && p.EndDate != null && p.EndDate >= today)
            .OrderBy(p => p.StartDate)
            .Select(p => new { p.StartDate, p.EndDate })
            .FirstOrDefaultAsync(ct);
        return Results.Ok(pause is null
            ? new VacationDto(false, null, null)
            : new VacationDto(true, pause.StartDate, pause.EndDate));
    }

    private static async Task<IResult> Start(VacationRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        if (request is null) return ApiResults.Validation("Corpo da requisição ausente.");
        var today = await clock.GetTodayAsync(ct);
        if (PauseEndpoints.ValidateRange(request.StartDate, request.EndDate, endRequired: true, today, request.Reason, out var reason) is { } error)
            return error;
        var start = request.StartDate!.Value;
        var end = request.EndDate!.Value;

        var habits = await db.Habits.Include(h => h.Pauses).Where(h => h.ArchivedAt == null).ToListAsync(ct);
        var now = clock.UtcNow;
        var paused = 0;
        foreach (var habit in habits.Where(h => !PauseEndpoints.Overlaps(h.Pauses, start, end)))
        {
            db.HabitPauses.Add(new HabitPause { HabitId = habit.Id, StartDate = start, EndDate = end, Reason = reason, IsVacation = true, CreatedAt = now });
            paused++;
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { paused });
    }

    /// <summary>Resumes the vacation pauses in progress and deletes the future ones; individual pauses stay.</summary>
    private static async Task<IResult> End(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var today = await clock.GetTodayAsync(ct);
        var pauses = await db.HabitPauses
            .Where(p => p.IsVacation && (p.EndDate == null || p.EndDate >= today))
            .ToListAsync(ct);
        var resumed = 0;
        foreach (var pause in pauses)
        {
            if (pause.StartDate > today) db.HabitPauses.Remove(pause);
            else
            {
                PauseEndpoints.EndPause(db, pause, today);
                resumed++;
            }
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { resumed });
    }
}
