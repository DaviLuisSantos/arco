namespace Arco.Api.Features.Focus;

public static class FocusEndpoints
{
    public const int MaxDaysInPast = 7;
    public const int MaxRangeDays = 366;

    public sealed record CreateFocusSessionRequest(int? HabitId, int? Minutes, DateOnly? Date, DateTime? StartedAt);

    public sealed record FocusSessionDto(int Id, int HabitId, DateOnly Date, int Minutes, FocusSource Source, DateTime? StartedAt)
    {
        public static FocusSessionDto From(FocusSession s) =>
            new(s.Id, s.HabitId, s.Date, s.Minutes, s.Source, s.StartedAt);
    }

    public sealed record FocusHabitTotalDto(int HabitId, string Name, string? Emoji, int Minutes, int Sessions);

    public sealed record FocusListResult(List<FocusSessionDto> Sessions, int TotalMinutes, List<FocusHabitTotalDto> ByHabit);

    public sealed record FocusSessionResult(FocusSessionDto Session);

    public static IEndpointRouteBuilder MapFocus(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/focus-sessions").WithTags("Foco");
        group.MapPost("", Create).WithSummary("Registra sessão de foco");
        group.MapDelete("/{id:int}", Delete).WithSummary("Remove sessão de foco");
        group.MapGet("", List).WithSummary("Lista sessões de foco");
        return api;
    }

    private static async Task<IResult> Create(CreateFocusSessionRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        if (request?.HabitId is not { } habitId) return ApiResults.Validation("Informe o hábito.");
        if (request.Minutes is not { } minutes || minutes < 1 || minutes > FocusRules.MaxMinutes)
            return ApiResults.Validation($"Informe os minutos de foco (de 1 a {FocusRules.MaxMinutes}).");

        var today = await clock.GetTodayAsync(ct);
        var date = request.Date ?? today;
        if (date > today) return ApiResults.Validation("Não é possível registrar foco em uma data futura.");
        if (date < today.AddDays(-MaxDaysInPast))
            return ApiResults.Validation($"Só é possível registrar foco dos últimos {MaxDaysInPast} dias.");

        var habit = await db.Habits.FirstOrDefaultAsync(h => h.Id == habitId, ct);
        if (habit is null) return ApiResults.NotFound("Hábito não encontrado.");
        if (habit.ArchivedAt is not null) return ApiResults.Validation("Hábitos arquivados não aceitam sessões de foco.");

        var session = new FocusSession
        {
            HabitId = habitId,
            Date = date,
            Minutes = minutes,
            Source = FocusSource.Timer,
            StartedAt = request.StartedAt?.ToUniversalTime(),
            CreatedAt = clock.UtcNow,
        };
        db.FocusSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new FocusSessionResult(FocusSessionDto.From(session)));
    }

    private static async Task<IResult> Delete(int id, AppDbContext db, CancellationToken ct)
    {
        var session = await db.FocusSessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null) return ApiResults.NotFound("Sessão de foco não encontrada.");
        db.FocusSessions.Remove(session);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> List(DateOnly? from, DateOnly? to, int? habitId, AppDbContext db, CancellationToken ct)
    {
        if (from is not { } start || to is not { } end)
            return ApiResults.Validation("Informe o intervalo de datas (from e to, no formato AAAA-MM-DD).");
        if (start > end) return ApiResults.Validation("A data inicial deve ser anterior ou igual à data final.");
        if (end.DayNumber - start.DayNumber + 1 > MaxRangeDays)
            return ApiResults.Validation($"O intervalo máximo é de {MaxRangeDays} dias.");

        var rows = await db.FocusSessions.AsNoTracking()
            .Where(s => s.Date >= start && s.Date <= end && (habitId == null || s.HabitId == habitId))
            .OrderBy(s => s.Date).ThenBy(s => s.CreatedAt).ThenBy(s => s.Id)
            .ToListAsync(ct);

        var ids = rows.Select(s => s.HabitId).Distinct().ToList();
        var habits = await db.Habits.AsNoTracking().Where(h => ids.Contains(h.Id))
            .Select(h => new { h.Id, h.Name, h.Emoji }).ToListAsync(ct);

        var byHabit = habits
            .Select(h => new FocusHabitTotalDto(h.Id, h.Name, h.Emoji,
                rows.Where(s => s.HabitId == h.Id).Sum(s => s.Minutes), rows.Count(s => s.HabitId == h.Id)))
            .OrderByDescending(x => x.Minutes).ThenBy(x => x.Name)
            .ToList();

        return Results.Ok(new FocusListResult(rows.Select(FocusSessionDto.From).ToList(), rows.Sum(s => s.Minutes), byHabit));
    }
}
