using Arco.Api.Features.CheckIns;

namespace Arco.Api.Features.Moods;

public static class MoodEndpoints
{
    public const string InvalidValueCode = "mood_invalid_value";
    public const string DateOutOfRangeCode = "date_out_of_range";
    public const int DefaultRangeDays = 7;
    public const int MaxRangeDays = CheckInEndpoints.MaxRangeDays;

    public sealed record UpsertMoodRequest(DateOnly? Date, int? Value);
    public sealed record MoodDto(DateOnly Date, int Value);

    public static IEndpointRouteBuilder MapMoods(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/moods").WithTags("Humor");
        group.MapGet("", List).WithSummary("Lista o humor dos dias (padrão: últimos 7 dias)");
        group.MapPut("", Upsert).WithSummary("Registra ou altera o humor de um dia");
        group.MapDelete("/{date}", Delete).WithSummary("Remove o humor de um dia");
        return api;
    }

    private static async Task<IResult> List(DateOnly? from, DateOnly? to, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var today = await clock.GetTodayAsync(ct);
        var end = to ?? today;
        var start = from ?? end.AddDays(-(DefaultRangeDays - 1));
        if (start > end) return ApiResults.Validation("A data inicial deve ser anterior ou igual à data final.");
        if (end.DayNumber - start.DayNumber + 1 > MaxRangeDays)
            return ApiResults.Validation($"O intervalo máximo é de {MaxRangeDays} dias.");

        var moods = await db.DailyMoods.AsNoTracking()
            .Where(m => m.Date >= start && m.Date <= end)
            .OrderBy(m => m.Date)
            .Select(m => new MoodDto(m.Date, m.Value))
            .ToListAsync(ct);
        return Results.Ok(moods);
    }

    private static async Task<IResult> Upsert(UpsertMoodRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        if (request?.Value is not { } value || value < MoodRules.Min || value > MoodRules.Max)
            return ApiResults.Error(400, InvalidValueCode, $"O humor deve ser um número de {MoodRules.Min} a {MoodRules.Max}.");

        var today = await clock.GetTodayAsync(ct);
        var date = request.Date ?? today;
        if (date > today) return ApiResults.Error(400, DateOutOfRangeCode, "Não é possível registrar humor em uma data futura.");
        if (date < today.AddDays(-CheckInEndpoints.MaxDaysInPast))
            return ApiResults.Error(400, DateOutOfRangeCode,
                $"Só é possível registrar o humor dos últimos {CheckInEndpoints.MaxDaysInPast} dias.");

        var now = clock.UtcNow;
        var mood = await db.DailyMoods.FirstOrDefaultAsync(m => m.Date == date, ct);
        if (mood is null)
        {
            mood = new DailyMood { Date = date, CreatedAt = now };
            db.DailyMoods.Add(mood);
        }
        mood.Value = value;
        mood.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return ApiResults.Conflict("O humor deste dia foi registrado ao mesmo tempo por outra requisição. Tente novamente.");
        }
        return Results.Ok(new MoodDto(mood.Date, mood.Value));
    }

    private static async Task<IResult> Delete(DateOnly date, AppDbContext db, CancellationToken ct)
    {
        var mood = await db.DailyMoods.FirstOrDefaultAsync(m => m.Date == date, ct);
        if (mood is null) return ApiResults.NotFound("Humor não encontrado.");
        db.DailyMoods.Remove(mood);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
