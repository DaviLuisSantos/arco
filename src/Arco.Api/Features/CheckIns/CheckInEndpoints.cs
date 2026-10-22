using Arco.Api.Features.Achievements;
namespace Arco.Api.Features.CheckIns;

public static class CheckInEndpoints
{
    public const int MaxDaysInPast = 7;
    public const int MaxRangeDays = 366;

    public sealed record UpsertCheckInRequest(int? HabitId, DateOnly? Date, CheckInStatus? Status);

    public static IEndpointRouteBuilder MapCheckIns(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/checkins");
        group.MapPut("", Upsert);
        group.MapDelete("/{habitId:int}/{date}", Delete);
        group.MapGet("", List);
        return api;
    }

    private static async Task<IResult> Upsert(UpsertCheckInRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        if (request?.HabitId is not { } habitId) return ApiResults.Validation("Informe o hábito.");
        if (request.Date is not { } date) return ApiResults.Validation("Informe a data do check-in.");
        if (request.Status is not { } status) return ApiResults.Validation("Informe o status do check-in.");

        var today = await clock.GetTodayAsync(ct);
        if (date > today) return ApiResults.Validation("Não é possível registrar check-in em uma data futura.");
        if (date < today.AddDays(-MaxDaysInPast))
            return ApiResults.Validation($"Só é possível registrar check-ins dos últimos {MaxDaysInPast} dias.");

        var habit = await db.Habits.Include(h => h.Levels).FirstOrDefaultAsync(h => h.Id == habitId, ct);
        if (habit is null) return ApiResults.NotFound("Hábito não encontrado.");
        if (habit.ArchivedAt is not null) return ApiResults.Validation("Hábitos arquivados não aceitam check-in.");

        var now = clock.UtcNow;
        var xp = XpRules.ForCheckIn(status, habit.CurrentLevelNumber);

        var checkIn = await db.CheckIns.FirstOrDefaultAsync(c => c.HabitId == habitId && c.Date == date, ct);
        List<XpEvent> events = checkIn is null
            ? []
            : await db.XpEvents.Where(e => e.CheckInId == checkIn.Id).ToListAsync(ct);
        var previousXp = events.Sum(e => e.Amount);

        if (checkIn is null)
        {
            checkIn = new CheckIn { HabitId = habitId, Date = date, CreatedAt = now };
            db.CheckIns.Add(checkIn);
        }
        checkIn.Status = status;
        checkIn.Xp = xp;
        checkIn.UpdatedAt = now;

        // The check-in's XP event is replaced (one event per check-in, none when 0 XP).
        db.XpEvents.RemoveRange(events);
        if (xp > 0)
        {
            db.XpEvents.Add(new XpEvent
            {
                CheckIn = checkIn,
                HabitId = habitId,
                Date = date,
                Amount = xp,
                Source = XpSource.CheckIn,
                Description = Describe(habit.Name, status),
                CreatedAt = now,
            });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return ApiResults.Conflict("Este check-in foi registrado ao mesmo tempo por outra requisição. Tente novamente.");
        }

        return Results.Ok(new CheckInResult(CheckInDto.From(checkIn), xp - previousXp, await Queries.GetPlayerAsync(db, ct),
            await AchievementService.UnlockAsync(db, clock, ct)));
    }

    private static async Task<IResult> Delete(int habitId, DateOnly date, AppDbContext db, CancellationToken ct)
    {
        var checkIn = await db.CheckIns.FirstOrDefaultAsync(c => c.HabitId == habitId && c.Date == date, ct);
        if (checkIn is null) return ApiResults.NotFound("Check-in não encontrado.");

        var events = await db.XpEvents.Where(e => e.CheckInId == checkIn.Id).ToListAsync(ct);
        db.XpEvents.RemoveRange(events);
        db.CheckIns.Remove(checkIn);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new XpResult(-events.Sum(e => e.Amount), await Queries.GetPlayerAsync(db, ct), []));
    }

    private static async Task<IResult> List(DateOnly? from, DateOnly? to, int? habitId, AppDbContext db, CancellationToken ct)
    {
        if (from is not { } start || to is not { } end)
            return ApiResults.Validation("Informe o intervalo de datas (from e to, no formato AAAA-MM-DD).");
        if (start > end) return ApiResults.Validation("A data inicial deve ser anterior ou igual à data final.");
        if (end.DayNumber - start.DayNumber + 1 > MaxRangeDays)
            return ApiResults.Validation($"O intervalo máximo é de {MaxRangeDays} dias.");

        var checkIns = await db.CheckIns.AsNoTracking()
            .Where(c => c.Date >= start && c.Date <= end && (habitId == null || c.HabitId == habitId))
            .OrderBy(c => c.Date).ThenBy(c => c.HabitId)
            .Select(c => new CheckInDto(c.HabitId, c.Date, c.Status, c.Xp))
            .ToListAsync(ct);
        return Results.Ok(checkIns);
    }

    private static string Describe(string habitName, CheckInStatus status)
    {
        var label = status == CheckInStatus.Done ? "feito" : "mínimo";
        var text = $"{habitName} ({label})";
        return text.Length <= 200 ? text : text[..200];
    }
}
