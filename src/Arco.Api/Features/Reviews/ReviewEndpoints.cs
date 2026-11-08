using Arco.Api.Features.Achievements;
namespace Arco.Api.Features.Reviews;

public static class ReviewEndpoints
{
    public sealed record CreateWeeklyReviewRequest(DateOnly? WeekStart, string? Wins, string? Obstacles, string? Next);

    public sealed record WeeklyReviewDto(
        int Id, DateOnly WeekStart, DateOnly WeekEnd, string? Wins, string? Obstacles, string? Next, DateTime CreatedAt)
    {
        public static WeeklyReviewDto From(WeeklyReview r) =>
            new(r.Id, r.WeekStart, HabitSchedule.WeekEnd(r.WeekStart), r.Wins, r.Obstacles, r.Next, r.CreatedAt);
    }

    public sealed record WeeklySummaryResponse(
        DateOnly WeekStart,
        DateOnly WeekEnd,
        WeeklyReviewDto? Review,
        int Xp,
        CheckInTotalsDto CheckIns,
        IReadOnlyList<HabitPeriodStatsDto> Habits,
        CycleDto? Cycle,
        IReadOnlyList<GoalDto> Goals);

    public sealed record WeeklyReviewXpResult(WeeklyReviewDto Review, int XpDelta, PlayerDto Player,
        IReadOnlyList<AchievementDto> Achievements);

    public static IEndpointRouteBuilder MapReviews(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/reviews/weekly").WithTags("Revisões");
        group.MapGet("", List).WithSummary("Lista revisões semanais");
        group.MapGet("/current", Current).WithSummary("Revisão semanal atual");
        group.MapPost("", Create).WithSummary("Cria revisão semanal");
        return api;
    }

    private static async Task<List<WeeklyReviewDto>> List(AppDbContext db, CancellationToken ct)
    {
        var reviews = await db.WeeklyReviews.AsNoTracking().OrderByDescending(r => r.WeekStart).ToListAsync(ct);
        return reviews.Select(WeeklyReviewDto.From).ToList();
    }

    private static async Task<WeeklySummaryResponse> Current(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var today = await clock.GetTodayAsync(ct);
        var timezone = await clock.GetTimezoneAsync(ct);
        var weekStart = WeeklyReviewRules.ReviewableWeekStart(today);
        var weekEnd = HabitSchedule.WeekEnd(weekStart);

        var review = await db.WeeklyReviews.AsNoTracking().FirstOrDefaultAsync(r => r.WeekStart == weekStart, ct);
        var habits = await ReviewStats.ForRangeAsync(db, weekStart, weekEnd, today, timezone, ct);
        var cycle = await Queries.GetActiveCycleAsync(db, ct);
        var goals = cycle is null ? [] : await Queries.GetGoalDtosForCycleAsync(db, cycle.Id, ct);

        return new WeeklySummaryResponse(weekStart, weekEnd,
            review is null ? null : WeeklyReviewDto.From(review),
            await ReviewStats.XpInRangeAsync(db, weekStart, weekEnd, ct),
            ReviewStats.Totals(habits), habits,
            cycle is null ? null : CycleDto.From(cycle, today), goals);
    }

    private static async Task<IResult> Create(CreateWeeklyReviewRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var today = await clock.GetTodayAsync(ct);
        var weekStart = WeeklyReviewRules.ReviewableWeekStart(today);
        if (request?.WeekStart is not { } requested) return ApiResults.Validation("Informe a semana da revisão.");
        if (requested != weekStart)
            return ApiResults.Validation("Só é possível revisar a semana que acabou de terminar.");

        var wins = Text.Clean(request.Wins);
        var obstacles = Text.Clean(request.Obstacles);
        var next = Text.Clean(request.Next);
        if (wins is null && obstacles is null && next is null)
            return ApiResults.Validation("Responda ao menos uma das perguntas da revisão.");
        if (new[] { wins, obstacles, next }.Any(a => a is { Length: > WeeklyReviewRules.MaxAnswerLength }))
            return ApiResults.Validation($"Cada resposta deve ter no máximo {WeeklyReviewRules.MaxAnswerLength} caracteres.");

        const string AlreadyDone = "A revisão desta semana já foi feita.";
        if (await db.WeeklyReviews.AnyAsync(r => r.WeekStart == weekStart, ct)) return ApiResults.Conflict(AlreadyDone);

        var now = clock.UtcNow;
        var review = new WeeklyReview { WeekStart = weekStart, Wins = wins, Obstacles = obstacles, Next = next, CreatedAt = now };
        db.WeeklyReviews.Add(review);
        db.XpEvents.Add(new XpEvent
        {
            Date = today,
            Amount = XpRules.WeeklyReview,
            Source = XpSource.WeeklyReview,
            Description = $"Revisão da semana de {weekStart:dd/MM}",
            CreatedAt = now,
            WeeklyReview = review,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return ApiResults.Conflict(AlreadyDone);
        }

        return Results.Created("/api/reviews/weekly",
            new WeeklyReviewXpResult(WeeklyReviewDto.From(review), XpRules.WeeklyReview, await Queries.GetPlayerAsync(db, ct),
                await AchievementService.UnlockAsync(db, clock, ct)));
    }
}
