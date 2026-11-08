using Arco.Api.Features.Achievements;
namespace Arco.Api.Features.Goals;

public static class GoalEndpoints
{
    public sealed record CreateGoalRequest(
        int? CycleId,
        string? Title,
        string? Description,
        GoalType? Type,
        string? Unit,
        double? StartValue,
        double? TargetValue,
        DateOnly? Deadline,
        string[]? Milestones);

    public sealed record UpdateGoalRequest(string? Title, string? Description, string? Unit, double? TargetValue, DateOnly? Deadline);

    public sealed record ProgressRequest(double? Value, string? Note);

    private const string NotFound = "Objetivo não encontrado.";
    private const string TitleError = "O título deve ter entre 1 e 120 caracteres.";
    private const int MaxDescription = 1000;
    private const int MaxUnit = 20;
    private const int MaxNote = 280;
    private const int MaxMilestones = 20;

    public static IEndpointRouteBuilder MapGoals(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/goals").WithTags("Objetivos");
        group.MapGet("", List).WithSummary("Lista objetivos");
        group.MapPost("", Create).WithSummary("Cria um objetivo");
        group.MapPut("/{id:int}", Update).WithSummary("Atualiza um objetivo");
        group.MapDelete("/{id:int}", Delete).WithSummary("Exclui um objetivo");
        group.MapPost("/{id:int}/progress", Progress).WithSummary("Registra progresso do objetivo");
        group.MapPost("/{id:int}/milestones/{milestoneId:int}/toggle", ToggleMilestone).WithSummary("Alterna um marco do objetivo");
        group.MapPost("/{id:int}/complete", Complete).WithSummary("Conclui o objetivo");
        return api;
    }

    private static async Task<List<GoalDto>> List(int? cycleId, AppDbContext db, CancellationToken ct)
    {
        var id = cycleId ?? (await Queries.GetActiveCycleAsync(db, ct))?.Id;
        return id is null ? [] : await Queries.GetGoalDtosForCycleAsync(db, id.Value, ct);
    }

    private static async Task<IResult> Create(CreateGoalRequest? r, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        if (r is null) return ApiResults.Validation("Corpo da requisição ausente.");
        if (r.CycleId is not { } cycleId) return ApiResults.Validation("Informe o ciclo do objetivo.");
        var cycle = await db.Cycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cycleId, ct);
        if (cycle is null) return ApiResults.Validation("Ciclo não encontrado.");

        var title = r.Title?.Trim();
        if (!Text.LengthBetween(title, 1, 120)) return ApiResults.Validation(TitleError);
        var description = Text.Clean(r.Description);
        if (description is { Length: > MaxDescription })
            return ApiResults.Validation($"A descrição deve ter no máximo {MaxDescription} caracteres.");
        if (r.Type is not { } type) return ApiResults.Validation("Informe o tipo do objetivo.");

        var goal = new Goal
        {
            CycleId = cycleId,
            Title = title!,
            Description = description,
            Type = type,
            Deadline = r.Deadline ?? cycle.EndDate,
            CreatedAt = clock.UtcNow,
        };

        if (type == GoalType.Numeric)
        {
            if (r.StartValue is not { } start || r.TargetValue is not { } target)
                return ApiResults.Validation("Informe o valor inicial e a meta.");
            if (start == target) return ApiResults.Validation("A meta deve ser diferente do valor inicial.");
            var unit = Text.Clean(r.Unit);
            if (unit is { Length: > MaxUnit })
                return ApiResults.Validation($"A unidade deve ter no máximo {MaxUnit} caracteres.");
            goal.StartValue = start;
            goal.TargetValue = target;
            goal.Unit = unit;
        }
        else
        {
            var titles = r.Milestones?.Select(m => m?.Trim()).ToList() ?? [];
            if (titles.Count is < 1 or > MaxMilestones)
                return ApiResults.Validation($"O objetivo deve ter de 1 a {MaxMilestones} marcos.");
            if (titles.Any(t => !Text.LengthBetween(t, 1, 120)))
                return ApiResults.Validation("Cada marco deve ter entre 1 e 120 caracteres.");
            goal.Milestones = titles.Select((t, i) => new Milestone { Title = t!, Order = i + 1 }).ToList();
        }

        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/goals/{goal.Id}", await Queries.ToGoalDtoAsync(db, goal, ct));
    }

    private static async Task<IResult> Update(int id, UpdateGoalRequest? r, AppDbContext db, CancellationToken ct)
    {
        var goal = await db.Goals.Include(g => g.Milestones).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (goal is null) return ApiResults.NotFound(NotFound);

        var title = r?.Title?.Trim();
        if (!Text.LengthBetween(title, 1, 120)) return ApiResults.Validation(TitleError);
        var description = Text.Clean(r!.Description);
        if (description is { Length: > MaxDescription })
            return ApiResults.Validation($"A descrição deve ter no máximo {MaxDescription} caracteres.");

        goal.Title = title!;
        goal.Description = description;
        if (r.Deadline is { } deadline) goal.Deadline = deadline;

        if (goal.Type == GoalType.Numeric)
        {
            var unit = Text.Clean(r.Unit);
            if (unit is { Length: > MaxUnit })
                return ApiResults.Validation($"A unidade deve ter no máximo {MaxUnit} caracteres.");
            goal.Unit = unit;
            if (r.TargetValue is { } target)
            {
                if (target == goal.StartValue) return ApiResults.Validation("A meta deve ser diferente do valor inicial.");
                goal.TargetValue = target;
            }
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(await Queries.ToGoalDtoAsync(db, goal, ct));
    }

    /// <summary>
    /// Deletes the goal and takes back the XP it gave (completion and milestones). Habits linked to it stay, unlinked;
    /// milestones and updates go with it.
    /// </summary>
    private static async Task<IResult> Delete(int id, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (goal is null) return ApiResults.NotFound(NotFound);

        var events = await db.XpEvents.Where(e => e.GoalId == id).ToListAsync(ct);
        db.XpEvents.RemoveRange(events);
        db.Goals.Remove(goal);
        await db.SaveChangesAsync(ct);
        await AchievementService.RevokeUnmetAsync(db, clock, ct);

        return Results.Ok(new XpResult(-events.Sum(e => e.Amount), await Queries.GetPlayerAsync(db, ct), []));
    }

    private static async Task<IResult> Progress(int id, ProgressRequest? r, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var goal = await db.Goals.Include(g => g.Milestones).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (goal is null) return ApiResults.NotFound(NotFound);
        if (goal.Type != GoalType.Numeric)
            return ApiResults.Validation("Só objetivos numéricos aceitam atualização de valor.");
        if (r?.Value is not { } value) return ApiResults.Validation("Informe o valor atual.");
        var note = Text.Clean(r.Note);
        if (note is { Length: > MaxNote }) return ApiResults.Validation($"A nota deve ter no máximo {MaxNote} caracteres.");

        var now = clock.UtcNow;
        goal.Updates.Add(new GoalUpdate { Value = value, Note = note, CreatedAt = now });
        goal.CurrentValue = value;

        var xpDelta = 0;
        if (goal.CompletedAt is null && goal is { StartValue: { } start, TargetValue: { } target }
            && GoalProgress.IsTargetReached(start, target, value))
        {
            xpDelta += CompleteGoal(db, goal, now, await clock.GetTodayAsync(ct));
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(await ResultAsync(db, clock, goal, xpDelta, ct));
    }

    private static async Task<IResult> ToggleMilestone(int id, int milestoneId, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var goal = await db.Goals.Include(g => g.Milestones).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (goal is null) return ApiResults.NotFound(NotFound);
        var milestone = goal.Milestones.FirstOrDefault(m => m.Id == milestoneId);
        if (milestone is null) return ApiResults.NotFound("Marco não encontrado.");

        var now = clock.UtcNow;
        var today = await clock.GetTodayAsync(ct);
        var xpDelta = 0;

        if (milestone.DoneAt is null)
        {
            milestone.DoneAt = now;
            db.XpEvents.Add(new XpEvent
            {
                Date = today,
                Amount = XpRules.MilestoneDone,
                Source = XpSource.Milestone,
                Description = Truncate($"Marco concluído: {milestone.Title}"),
                CreatedAt = now,
                GoalId = goal.Id,
                MilestoneId = milestone.Id,
            });
            xpDelta += XpRules.MilestoneDone;

            if (goal.CompletedAt is null && goal.Milestones.All(m => m.DoneAt is not null))
                xpDelta += CompleteGoal(db, goal, now, today);
        }
        else
        {
            milestone.DoneAt = null;
            var milestoneEvents = await db.XpEvents
                .Where(e => e.MilestoneId == milestone.Id && e.Source == XpSource.Milestone)
                .ToListAsync(ct);
            db.XpEvents.RemoveRange(milestoneEvents);
            xpDelta -= milestoneEvents.Sum(e => e.Amount);

            if (goal.CompletedAt is not null)
            {
                goal.CompletedAt = null;
                var goalEvents = await db.XpEvents
                    .Where(e => e.GoalId == goal.Id && e.Source == XpSource.GoalCompleted)
                    .ToListAsync(ct);
                db.XpEvents.RemoveRange(goalEvents);
                xpDelta -= goalEvents.Sum(e => e.Amount);
            }
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(await ResultAsync(db, clock, goal, xpDelta, ct));
    }

    private static async Task<IResult> Complete(int id, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var goal = await db.Goals.Include(g => g.Milestones).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (goal is null) return ApiResults.NotFound(NotFound);
        if (goal.CompletedAt is not null) return ApiResults.Conflict("Este objetivo já foi concluído.");

        var xpDelta = CompleteGoal(db, goal, clock.UtcNow, await clock.GetTodayAsync(ct));
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ResultAsync(db, clock, goal, xpDelta, ct));
    }

    /// <summary>Marks the goal completed and queues its +150 XP event (saved by the caller).</summary>
    private static int CompleteGoal(AppDbContext db, Goal goal, DateTime now, DateOnly today)
    {
        goal.CompletedAt = now;
        db.XpEvents.Add(new XpEvent
        {
            Date = today,
            Amount = XpRules.GoalCompleted,
            Source = XpSource.GoalCompleted,
            Description = Truncate($"Objetivo concluído: {goal.Title}"),
            CreatedAt = now,
            GoalId = goal.Id,
        });
        return XpRules.GoalCompleted;
    }

    private static async Task<GoalXpResult> ResultAsync(AppDbContext db, AppClock clock, Goal goal, int xpDelta,
        CancellationToken ct) =>
        new(await Queries.ToGoalDtoAsync(db, goal, ct), xpDelta, await Queries.GetPlayerAsync(db, ct),
            xpDelta > 0 ? await AchievementService.UnlockAsync(db, clock, ct) : []);

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200];
}
