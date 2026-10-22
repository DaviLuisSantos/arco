namespace Arco.Api.Features.Cycles;

public static class CycleEndpoints
{
    public sealed record CreateCycleRequest(string? Name, DateOnly? StartDate, int? Weeks);
    public sealed record UpdateCycleRequest(string? Name, DateOnly? EndDate);
    public sealed record CompleteCycleRequest(string? ReviewNotes);

    private const string NameError = "O nome do ciclo deve ter entre 1 e 80 caracteres.";
    private const string NotFound = "Ciclo não encontrado.";
    private const string ActiveExists = "Já existe um ciclo ativo. Encerre-o antes de criar outro.";
    private const int MaxReviewLength = 4000;

    public static IEndpointRouteBuilder MapCycles(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/cycles");
        group.MapGet("", List);
        group.MapGet("/current", Current);
        group.MapPost("", Create);
        group.MapPut("/{id:int}", Update);
        group.MapPost("/{id:int}/complete", Complete);
        return api;
    }

    private static async Task<List<CycleDto>> List(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var today = await clock.GetTodayAsync(ct);
        var cycles = await db.Cycles.AsNoTracking()
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .ToListAsync(ct);
        return cycles.Select(c => CycleDto.From(c, today)).ToList();
    }

    private static async Task<IResult> Current(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var cycle = await Queries.GetActiveCycleAsync(db, ct);
        return cycle is null ? Results.NoContent() : Results.Ok(CycleDto.From(cycle, await clock.GetTodayAsync(ct)));
    }

    private static async Task<IResult> Create(CreateCycleRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var name = request?.Name?.Trim();
        if (!Text.LengthBetween(name, 1, 80)) return ApiResults.Validation(NameError);
        var weeks = request!.Weeks ?? CycleMath.DefaultWeeks;
        if (weeks is < CycleMath.MinWeeks or > CycleMath.MaxWeeks)
            return ApiResults.Validation($"O ciclo deve ter de {CycleMath.MinWeeks} a {CycleMath.MaxWeeks} semanas.");

        if (await db.Cycles.AnyAsync(c => c.Status == CycleStatus.Active, ct))
            return ApiResults.Conflict(ActiveExists, ErrorCodes.ActiveCycleExists);

        var today = await clock.GetTodayAsync(ct);
        var start = request.StartDate ?? today;
        var cycle = new Cycle
        {
            Name = name!,
            StartDate = start,
            EndDate = CycleMath.EndDate(start, weeks),
            Status = CycleStatus.Active,
            CreatedAt = clock.UtcNow,
        };
        db.Cycles.Add(cycle);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return ApiResults.Conflict(ActiveExists, ErrorCodes.ActiveCycleExists);
        }
        return Results.Created($"/api/cycles/{cycle.Id}", CycleDto.From(cycle, today));
    }

    private static async Task<IResult> Update(int id, UpdateCycleRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var cycle = await db.Cycles.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cycle is null) return ApiResults.NotFound(NotFound);
        if (cycle.Status != CycleStatus.Active) return ApiResults.Conflict("Só é possível editar ciclos ativos.");

        var name = request?.Name?.Trim();
        if (!Text.LengthBetween(name, 1, 80)) return ApiResults.Validation(NameError);
        if (request!.EndDate is not { } endDate) return ApiResults.Validation("Informe a data final do ciclo.");
        if (endDate < cycle.StartDate)
            return ApiResults.Validation("A data final deve ser igual ou posterior à data inicial.");

        cycle.Name = name!;
        cycle.EndDate = endDate;
        await db.SaveChangesAsync(ct);
        return Results.Ok(CycleDto.From(cycle, await clock.GetTodayAsync(ct)));
    }

    private static async Task<IResult> Complete(int id, CompleteCycleRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var cycle = await db.Cycles.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cycle is null) return ApiResults.NotFound(NotFound);
        if (cycle.Status == CycleStatus.Completed) return ApiResults.Conflict("Este ciclo já foi encerrado.");

        var notes = Text.Clean(request?.ReviewNotes);
        if (notes is { Length: > MaxReviewLength })
            return ApiResults.Validation($"A revisão deve ter no máximo {MaxReviewLength} caracteres.");

        var now = clock.UtcNow;
        var today = await clock.GetTodayAsync(ct);
        cycle.Status = CycleStatus.Completed;
        cycle.CompletedAt = now;
        cycle.ReviewNotes = notes;
        db.XpEvents.Add(new XpEvent
        {
            Date = today,
            Amount = XpRules.CycleCompleted,
            Source = XpSource.CycleCompleted,
            Description = Truncate($"Ciclo encerrado: {cycle.Name}"),
            CreatedAt = now,
            CycleId = cycle.Id,
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new CycleXpResult(CycleDto.From(cycle, today), XpRules.CycleCompleted,
            await Queries.GetPlayerAsync(db, ct)));
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200];
}
