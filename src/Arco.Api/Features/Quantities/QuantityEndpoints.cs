using Arco.Api.Features.Achievements;
using Arco.Api.Features.CheckIns;

namespace Arco.Api.Features.Quantities;

public static class QuantityEndpoints
{
    /// <param name="Total">Absolute total of the day (not an increment); 0 removes it.</param>
    public sealed record SetQuantityRequest(int? HabitId, DateOnly? Date, decimal? Total);

    public sealed record QuantityDto(int HabitId, DateOnly Date, decimal Total);

    public sealed record QuantityResult(int HabitId, DateOnly Date, decimal Total, CheckInDto? CheckIn, int XpDelta,
        PlayerDto Player, IReadOnlyList<AchievementDto> Achievements);

    public static IEndpointRouteBuilder MapQuantities(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/quantities").WithTags("Quantidades");
        group.MapPut("", Set).WithSummary("Registra o total do dia de um hábito quantitativo");
        group.MapGet("", List).WithSummary("Lista os totais diários de hábitos quantitativos");
        return api;
    }

    private static async Task<IResult> Set(SetQuantityRequest? request, AppDbContext db, AppClock clock, QuantityService quantities, CancellationToken ct)
    {
        if (request?.HabitId is not { } habitId) return ApiResults.Validation("Informe o hábito.");
        if (request.Total is not { } total || !QuantityRules.TryToTenths(total, out var tenths))
            return ApiResults.Error(StatusCodes.Status400BadRequest, QuantityCodes.InvalidQuantity,
                "A quantidade deve ser um número de 0 a 100000, com no máximo 1 casa decimal.");

        var today = await clock.GetTodayAsync(ct);
        var date = request.Date ?? today;
        if (date > today || date < today.AddDays(-CheckInEndpoints.MaxDaysInPast))
            return ApiResults.Error(StatusCodes.Status400BadRequest, QuantityCodes.DateOutOfRange,
                $"Só é possível registrar quantidades de hoje até {CheckInEndpoints.MaxDaysInPast} dias atrás.");

        var habit = await db.Habits.Include(h => h.Levels).FirstOrDefaultAsync(h => h.Id == habitId, ct);
        if (habit is null) return ApiResults.NotFound("Hábito não encontrado.");
        if (habit.ArchivedAt is not null) return ApiResults.Validation("Hábitos arquivados não aceitam quantidade.");
        if (habit.Unit is null)
            return ApiResults.Error(StatusCodes.Status400BadRequest, QuantityCodes.NotQuantitative,
                "Este hábito não é quantitativo (não tem unidade).");
        if (habit.CurrentLevel is not { TargetTenths: not null, MinimumTenths: not null })
            return ApiResults.Validation("O nível atual do hábito não tem meta e mínimo definidos.");

        QuantityOutcome outcome;
        try
        {
            outcome = await quantities.ApplyAsync(habit, date, tenths, ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return ApiResults.Conflict("A quantidade foi registrada ao mesmo tempo por outra requisição. Tente novamente.");
        }

        CheckInDto? checkIn = null;
        if (outcome.CheckIn is { } c)
        {
            var minutes = await db.FocusSessions
                .Where(s => s.HabitId == habitId && s.Date == date && s.Source == FocusSource.Manual)
                .OrderBy(s => s.Id).Select(s => (int?)s.Minutes).FirstOrDefaultAsync(ct);
            checkIn = CheckInDto.From(c) with { Minutes = minutes };
        }
        return Results.Ok(new QuantityResult(habitId, date, QuantityRules.FromTenths(tenths), checkIn, outcome.XpDelta,
            await Queries.GetPlayerAsync(db, ct), await AchievementService.UnlockAsync(db, clock, ct)));
    }

    private static async Task<IResult> List(DateOnly? from, DateOnly? to, int? habitId, AppDbContext db, CancellationToken ct)
    {
        if (from is not { } start || to is not { } end)
            return ApiResults.Validation("Informe o intervalo de datas (from e to, no formato AAAA-MM-DD).");
        if (start > end) return ApiResults.Validation("A data inicial deve ser anterior ou igual à data final.");
        if (end.DayNumber - start.DayNumber + 1 > CheckInEndpoints.MaxRangeDays)
            return ApiResults.Validation($"O intervalo máximo é de {CheckInEndpoints.MaxRangeDays} dias.");

        var rows = await db.HabitDayQuantities.AsNoTracking()
            .Where(q => q.Date >= start && q.Date <= end && (habitId == null || q.HabitId == habitId))
            .OrderBy(q => q.Date).ThenBy(q => q.HabitId)
            .Select(q => new { q.HabitId, q.Date, q.TotalTenths })
            .ToListAsync(ct);
        return Results.Ok(rows.Select(q => new QuantityDto(q.HabitId, q.Date, QuantityRules.FromTenths(q.TotalTenths))));
    }
}
