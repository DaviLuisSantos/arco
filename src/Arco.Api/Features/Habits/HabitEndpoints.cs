using Arco.Api.Features.Achievements;
using Arco.Api.Features.Quantities;
using System.Globalization;

namespace Arco.Api.Features.Habits;

public static class HabitEndpoints
{
    public sealed record HabitRequest(
        string? Name,
        string? Emoji,
        int? GoalId,
        FrequencyType? FrequencyType,
        int[]? FrequencyDays,
        int? TimesPerWeek,
        string? LevelDescription,
        string? MinimumDescription,
        string? ReminderTime = null,
        PlannedLevelRequest[]? PlannedLevels = null,
        int? LevelUpWeeks = null,
        int? LevelUpDaysPerWeek = null,
        TimeOfDay? TimeOfDay = null,
        int? AnchorHabitId = null,
        string? AnchorText = null,
        string? Unit = null,
        decimal? LevelTarget = null,
        decimal? LevelMinimumTarget = null,
        HabitKind? Kind = null);

    /// <summary>The exact set of active habits of one period, in the wanted order.</summary>
    public sealed record ReorderRequest(TimeOfDay? TimeOfDay, int[]? HabitIds);

    public const string AnchorConflictCode = "anchor_conflict";
    public const string AnchorCycleCode = "anchor_cycle";
    public const string OrderMismatchCode = "order_mismatch";
    public const string KindChangeCode = "kind_change";
    /// <summary>Default dose and minimum of a negative habit.</summary>
    public const string NoRelapseText = "Sem recaída";
    private const int MaxAnchorTextLength = 60;

    /// <summary>
    /// A planned level, with the same field names as <c>plannedLevels[]</c> in the responses, so a ladder read with GET
    /// can be sent back as is. <c>LevelDescription</c> is the old name of <c>Description</c>, still accepted.
    /// </summary>
    public sealed record PlannedLevelRequest(string? Description, string? MinimumDescription, string? LevelDescription = null,
        decimal? Target = null, decimal? MinimumTarget = null)
    {
        public string? EffectiveDescription => Description ?? LevelDescription;
    }

    /// <summary>
    /// Both descriptions optional when the habit has a planned next level (it is used instead). In a quantitative habit,
    /// <c>Target</c>/<c>MinimumTarget</c> come from the request or, when absent, from the planned level.
    /// </summary>
    public sealed record LevelUpRequest(string? LevelDescription, string? MinimumDescription,
        decimal? Target = null, decimal? MinimumTarget = null);

    private const int MaxPlannedLevels = 10;

    public sealed class HabitDetailDto(
        Habit habit,
        IReadOnlyList<CheckInDto> recentCheckIns,
        ConsistencyDto consistency,
        bool levelUpSuggested,
        int? levelUpWeeksDone,
        bool levelDownSuggested,
        StreakDto streak,
        DateOnly? today = null) : HabitDto(habit, today)
    {
        public IReadOnlyList<CheckInDto> RecentCheckIns { get; } = recentCheckIns;
        public ConsistencyDto Consistency { get; } = consistency;
        public bool LevelUpSuggested { get; } = levelUpSuggested;
        public int? LevelUpWeeksDone { get; } = levelUpWeeksDone;
        public bool LevelDownSuggested { get; } = levelDownSuggested;
        public StreakDto Streak { get; } = streak;
    }

    public static IEndpointRouteBuilder MapHabits(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/habits").WithTags("Hábitos");
        group.MapGet("", List).WithSummary("Lista hábitos");
        group.MapPut("/order", Reorder).WithSummary("Reordena os hábitos de um período");
        group.MapGet("/{id:int}", Get).WithSummary("Detalha um hábito");
        group.MapPost("", Create).WithSummary("Cria um hábito");
        group.MapPut("/{id:int}", Update).WithSummary("Atualiza um hábito");
        group.MapPost("/{id:int}/level-up", LevelUp).WithSummary("Sobe o nível do hábito");
        group.MapPost("/{id:int}/level-up-suggestion/dismiss", DismissSuggestion).WithSummary("Dispensa sugestão de subir nível");
        group.MapPost("/{id:int}/level-down", LevelDown).WithSummary("Desce o nível do hábito");
        group.MapPost("/{id:int}/level-down-suggestion/dismiss", DismissLevelDownSuggestion).WithSummary("Dispensa sugestão de descer nível");
        group.MapPost("/{id:int}/archive", Archive).WithSummary("Arquiva um hábito");
        group.MapPost("/{id:int}/restore", Restore).WithSummary("Restaura um hábito");
        return api;
    }

    private const string HabitNotFound = "Hábito não encontrado.";

    private static async Task<List<HabitDto>> List(AppDbContext db, AppClock clock, CancellationToken ct, bool includeArchived = false)
    {
        var query = db.Habits.AsNoTracking().Include(h => h.Levels).Include(h => h.PlannedLevels).Include(h => h.Pauses).AsQueryable();
        if (!includeArchived) query = query.Where(h => h.ArchivedAt == null);
        var habits = await query.ToListAsync(ct);
        var today = await clock.GetTodayAsync(ct);
        // Active habits in the order of the day; archived ones (only with includeArchived) go after, by name.
        var active = habits.Where(h => h.ArchivedAt is null).ToList();
        var position = DayOrder.Order(active.Select(h => new DayOrder.Item(h.Id, h.Name, h.TimeOfDay, h.SortOrder, h.AnchorHabitId)))
            .Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return habits
            .OrderBy(h => h.ArchivedAt is null ? 0 : 1)
            .ThenBy(h => position.GetValueOrDefault(h.Id))
            .ThenBy(h => h.Name, StringComparer.InvariantCultureIgnoreCase).ThenBy(h => h.Id)
            .Select(h => new HabitDto(h, today)).ToList();
    }

    /// <summary>Saves the manual order of a period: <c>SortOrder</c> = index in <c>habitIds</c>.</summary>
    private static async Task<IResult> Reorder(ReorderRequest? request, AppDbContext db, CancellationToken ct)
    {
        if (request?.TimeOfDay is not { } period) return ApiResults.Validation("Informe o período.");
        var ids = request.HabitIds ?? [];
        var habits = await db.Habits.Where(h => h.ArchivedAt == null && h.TimeOfDay == period).ToListAsync(ct);
        if (ids.Length != habits.Count || ids.Distinct().Count() != ids.Length || !habits.All(h => ids.Contains(h.Id)))
            return ApiResults.Error(StatusCodes.Status400BadRequest, OrderMismatchCode,
                "A lista deve conter exatamente os hábitos ativos do período.");

        var byId = habits.ToDictionary(h => h.Id);
        for (var i = 0; i < ids.Length; i++) byId[ids[i]].SortOrder = i;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>The next <c>SortOrder</c> at the end of a period's active habits.</summary>
    private static async Task<int> NextSortOrderAsync(AppDbContext db, TimeOfDay period, int? exceptId, CancellationToken ct)
    {
        var max = await db.Habits.Where(h => h.ArchivedAt == null && h.TimeOfDay == period && h.Id != exceptId)
            .Select(h => (int?)h.SortOrder).MaxAsync(ct);
        return max is { } m ? m + 1 : 0;
    }

    private static async Task<IResult> Get(int id, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var habit = await db.Habits.AsNoTracking().Include(h => h.Levels).Include(h => h.PlannedLevels).Include(h => h.Pauses)
            .FirstOrDefaultAsync(h => h.Id == id, ct);
        if (habit is null) return ApiResults.NotFound(HabitNotFound);

        var today = await clock.GetTodayAsync(ct);
        var timezone = await clock.GetTimezoneAsync(ct);
        var history = await db.CheckIns.AsNoTracking()
            .Where(c => c.HabitId == id && c.Date <= today)
            .OrderBy(c => c.Date)
            .ToListAsync(ct);
        var from = today.AddDays(-27);
        var checkIns = history.Where(c => c.Date >= from).ToList();
        var manualMinutes = (await db.FocusSessions.AsNoTracking()
                .Where(s => s.HabitId == id && s.Date >= from && s.Date <= today && s.Source == FocusSource.Manual)
                .OrderBy(s => s.Id).Select(s => new { s.Date, s.Minutes }).ToListAsync(ct))
            .GroupBy(s => s.Date).ToDictionary(g => g.Key, g => g.First().Minutes);
        var (consistency, weeksDone, suggested) = Queries.LevelUp(habit, history, today, timezone);
        return Results.Ok(new HabitDetailDto(habit,
            checkIns.Select(c => CheckInDto.From(c) with { Minutes = manualMinutes.TryGetValue(c.Date, out var m) ? m : null }).ToList(),
            consistency, suggested,
            weeksDone, !suggested && Queries.LevelDown(habit, history, today, timezone),
            Queries.Streak(habit, history, today, timezone), today));
    }

    /// <summary>Hides the level-up suggestion for <see cref="LevelUpSuggestion.SnoozeDays"/> days.</summary>
    private static async Task<IResult> DismissSuggestion(int id, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var habit = await db.Habits.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (habit is null) return ApiResults.NotFound(HabitNotFound);

        habit.LevelUpSnoozedUntil = (await clock.GetTodayAsync(ct)).AddDays(LevelUpSuggestion.SnoozeDays);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>Hides the level-down suggestion for <see cref="LevelDownSuggestion.SnoozeDays"/> days.</summary>
    private static async Task<IResult> DismissLevelDownSuggestion(int id, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var habit = await db.Habits.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (habit is null) return ApiResults.NotFound(HabitNotFound);

        habit.LevelDownSnoozedUntil = (await clock.GetTodayAsync(ct)).AddDays(LevelDownSuggestion.SnoozeDays);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>
    /// Goes back one level: the current level leaves the history and returns to the top of the planned ladder,
    /// so the next level-up starts from it. XP already earned stays.
    /// </summary>
    private static async Task<IResult> LevelDown(int id, AppDbContext db, AppClock clock, QuantityService quantities, CancellationToken ct)
    {
        var habit = await db.Habits.Include(h => h.Levels).Include(h => h.PlannedLevels).Include(h => h.Pauses).FirstOrDefaultAsync(h => h.Id == id, ct);
        if (habit is null) return ApiResults.NotFound(HabitNotFound);
        if (habit.ArchivedAt is not null) return ApiResults.Validation("Não é possível mudar o nível de um hábito arquivado.");
        if (habit.CurrentLevel is not { Level: >= 2 } current) return ApiResults.Validation("O hábito já está no nível 1.");

        foreach (var planned in habit.PlannedLevels) planned.Position++;
        var first = habit.PlannedLevels.Count == 0 ? 0 : habit.PlannedLevels.Min(p => p.Position) - 1;
        habit.PlannedLevels.Add(new HabitPlannedLevel
        {
            Position = first,
            Description = current.Description,
            MinimumDescription = current.MinimumDescription,
            TargetTenths = current.TargetTenths,
            MinimumTenths = current.MinimumTenths,
        });
        habit.Levels.Remove(current);
        habit.LevelDownSnoozedUntil = null;
        habit.LevelUpSnoozedUntil = null;
        await db.SaveChangesAsync(ct);
        // The previous level's goal/minimum may change today's status of a quantitative habit.
        await quantities.RecalculateAsync(habit, await clock.GetTodayAsync(ct), ct);
        return Results.Ok(new HabitDto(habit));
    }

    private static async Task<IResult> Create(HabitRequest? request, AppDbContext db, AppClock clock, CurrentUser user, CancellationToken ct)
    {
        if (request is null) return ApiResults.Validation("Corpo da requisição ausente.");
        request = request with { Kind = request.Kind ?? HabitKind.Positive };
        if (await ValidateAsync(request, db, null, ct) is { } error) return error;
        if (await PlanGate.CheckHabitLimitAsync(db, user, ct) is { } limit) return limit;

        var now = clock.UtcNow;
        var habit = new Habit { CreatedAt = now };
        Apply(habit, request);
        habit.SortOrder = await NextSortOrderAsync(db, habit.TimeOfDay, null, ct);
        var target = Tenths(request.LevelTarget, habit.Unit);
        var minimum = Tenths(request.LevelMinimumTarget, habit.Unit);
        habit.Levels.Add(new HabitLevel
        {
            Level = 1,
            Description = LevelText(request.LevelDescription, target, habit.Unit, habit.Kind),
            MinimumDescription = LevelText(request.MinimumDescription, minimum, habit.Unit, habit.Kind),
            TargetTenths = target,
            MinimumTenths = minimum,
            ReachedAt = now,
        });
        db.Habits.Add(habit);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/habits/{habit.Id}", new HabitDto(habit));
    }

    private static async Task<IResult> Update(int id, HabitRequest? request, AppDbContext db, CancellationToken ct)
    {
        if (request is null) return ApiResults.Validation("Corpo da requisição ausente.");
        var habit = await db.Habits.Include(h => h.Levels).Include(h => h.PlannedLevels).Include(h => h.Pauses).FirstOrDefaultAsync(h => h.Id == id, ct);
        if (habit is null) return ApiResults.NotFound(HabitNotFound);

        // The kind is set on creation; absent keeps it, a different one is refused.
        if (request.Kind is { } kind && kind != habit.Kind)
            return ApiResults.Error(StatusCodes.Status400BadRequest, KindChangeCode,
                "O tipo do hábito (positivo ou negativo) não pode ser alterado depois de criado.");
        request = request with { Kind = habit.Kind };

        // Absent quantity fields keep the current ones, so clients that predate quantitative habits do not turn
        // them back into regular habits; only unit "" does that.
        if (habit.Unit is not null && request.Unit != "")
        {
            var level = habit.CurrentLevel;
            request = request with
            {
                Unit = request.Unit ?? habit.Unit,
                LevelTarget = request.LevelTarget ?? (level?.TargetTenths is { } t ? QuantityRules.FromTenths(t) : null),
                LevelMinimumTarget = request.LevelMinimumTarget ?? (level?.MinimumTenths is { } m ? QuantityRules.FromTenths(m) : null),
            };
        }

        if (await ValidateAsync(request, db, id, ct) is { } error) return error;

        var previousPeriod = habit.TimeOfDay;
        Apply(habit, request);
        if (habit.TimeOfDay != previousPeriod) habit.SortOrder = await NextSortOrderAsync(db, habit.TimeOfDay, id, ct);
        var current = habit.CurrentLevel;
        if (current is not null)
        {
            current.TargetTenths = Tenths(request.LevelTarget, habit.Unit);
            current.MinimumTenths = Tenths(request.LevelMinimumTarget, habit.Unit);
            current.Description = LevelText(request.LevelDescription, current.TargetTenths, habit.Unit, habit.Kind);
            current.MinimumDescription = LevelText(request.MinimumDescription, current.MinimumTenths, habit.Unit, habit.Kind);
        }
        // A regular habit has no goals: back from quantitative, the history stays but its goals go.
        if (habit.Unit is null)
        {
            foreach (var level in habit.Levels) (level.TargetTenths, level.MinimumTenths) = (null, null);
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new HabitDto(habit));
    }

    private static async Task<IResult> LevelUp(int id, LevelUpRequest? request, AppDbContext db, AppClock clock, QuantityService quantities, CancellationToken ct)
    {
        var habit = await db.Habits.Include(h => h.Levels).Include(h => h.PlannedLevels).Include(h => h.Pauses).FirstOrDefaultAsync(h => h.Id == id, ct);
        if (habit is null) return ApiResults.NotFound(HabitNotFound);
        if (habit.ArchivedAt is not null) return ApiResults.Validation("Não é possível subir de nível um hábito arquivado.");

        var planned = habit.PlannedLevels.OrderBy(p => p.Position).ThenBy(p => p.Id).FirstOrDefault();
        int? targetTenths = null, minimumTenths = null;
        string? description, minimum;
        if (habit.Unit is { } unit)
        {
            // Quantitative: the numbers come from the request or the planned level; texts are optional (auto "<n> <unit>").
            if (request?.Target is { } t)
            {
                if (!QuantityRules.TryToTenths(t, out var parsed) || parsed <= 0) return ApiResults.Validation(TargetError);
                targetTenths = parsed;
            }
            else targetTenths = planned?.TargetTenths;
            if (request?.MinimumTarget is { } m)
            {
                if (!QuantityRules.TryToTenths(m, out var parsed) || parsed <= 0) return ApiResults.Validation(MinimumTargetError);
                minimumTenths = parsed;
            }
            else minimumTenths = planned?.MinimumTenths;
            if (targetTenths is null) return ApiResults.Validation("Informe a meta do novo nível.");
            if (minimumTenths is null) return ApiResults.Validation("Informe o mínimo do novo nível.");
            if (minimumTenths > targetTenths) return ApiResults.Validation(MinimumAboveTargetError);

            description = Text.Clean(request?.LevelDescription)
                ?? (request?.Target is null ? planned?.Description : null) ?? QuantityRules.Describe(targetTenths.Value, unit);
            minimum = Text.Clean(request?.MinimumDescription)
                ?? (request?.MinimumTarget is null ? planned?.MinimumDescription : null) ?? QuantityRules.Describe(minimumTenths.Value, unit);
        }
        else if (habit.Kind == HabitKind.Negative)
        {
            // Negative: texts are optional. Auto: the milestone being left ("30 dias limpo") and "Sem recaída".
            var milestone = CleanMilestones.ForLevel(habit.CurrentLevelNumber);
            description = Text.Clean(request?.LevelDescription) ?? Text.Clean(planned?.Description)
                ?? (milestone is { } days ? $"{days} dias limpo" : NoRelapseText);
            minimum = Text.Clean(request?.MinimumDescription) ?? Text.Clean(planned?.MinimumDescription) ?? NoRelapseText;
        }
        else
        {
            description = Text.Clean(request?.LevelDescription) ?? planned?.Description;
            minimum = Text.Clean(request?.MinimumDescription) ?? planned?.MinimumDescription;
        }
        if (!Text.LengthBetween(description, 1, 120)) return ApiResults.Validation(LevelDescriptionError);
        if (!Text.LengthBetween(minimum, 1, 120)) return ApiResults.Validation(MinimumDescriptionError);
        if (planned is not null) habit.PlannedLevels.Remove(planned);

        var now = clock.UtcNow;
        var newLevel = habit.CurrentLevelNumber + 1;
        habit.Levels.Add(new HabitLevel
        {
            Level = newLevel,
            Description = description!,
            MinimumDescription = minimum!,
            TargetTenths = targetTenths,
            MinimumTenths = minimumTenths,
            ReachedAt = now,
        });
        habit.LevelUpSnoozedUntil = null;
        habit.LevelDownSnoozedUntil = null;
        db.XpEvents.Add(new XpEvent
        {
            Date = await clock.GetTodayAsync(ct),
            Amount = XpRules.HabitLevelUp,
            Source = XpSource.HabitLevelUp,
            Description = Truncate($"{habit.Name} subiu para o nível {newLevel}"),
            CreatedAt = now,
            HabitId = habit.Id,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return ApiResults.Conflict("O hábito foi alterado ao mesmo tempo. Tente novamente.");
        }

        // The new goal/minimum may change today's status of a quantitative habit (and its XP).
        var quantityXp = await quantities.RecalculateAsync(habit, await clock.GetTodayAsync(ct), ct);

        return Results.Ok(new HabitXpResult(new HabitDto(habit), XpRules.HabitLevelUp + quantityXp, await Queries.GetPlayerAsync(db, ct),
            await AchievementService.UnlockAsync(db, clock, ct)));
    }

    private static Task<IResult> Archive(int id, AppDbContext db, AppClock clock, CurrentUser user, CancellationToken ct) =>
        SetArchived(id, archive: true, db, clock, user, ct);

    private static Task<IResult> Restore(int id, AppDbContext db, AppClock clock, CurrentUser user, CancellationToken ct) =>
        SetArchived(id, archive: false, db, clock, user, ct);

    private static async Task<IResult> SetArchived(int id, bool archive, AppDbContext db, AppClock clock, CurrentUser user, CancellationToken ct)
    {
        var habit = await db.Habits.Include(h => h.Levels).Include(h => h.PlannedLevels).Include(h => h.Pauses).FirstOrDefaultAsync(h => h.Id == id, ct);
        if (habit is null) return ApiResults.NotFound(HabitNotFound);
        if (!archive && habit.ArchivedAt is not null && await PlanGate.CheckHabitLimitAsync(db, user, ct) is { } limit) return limit;

        if (archive) habit.ArchivedAt ??= clock.UtcNow;
        else habit.ArchivedAt = null;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new HabitDto(habit));
    }

    private const string LevelDescriptionError = "A descrição do nível deve ter entre 1 e 120 caracteres.";
    private const string MinimumDescriptionError = "A versão mínima deve ter entre 1 e 120 caracteres.";
    private const string TargetError = "A meta deve ser um número maior que zero, com no máximo 1 casa decimal e até 100000.";
    private const string MinimumTargetError = "O mínimo deve ser um número maior que zero, com no máximo 1 casa decimal e até 100000.";
    private const string MinimumAboveTargetError = "O mínimo não pode ser maior que a meta.";

    /// <summary>The amount in tenths, or null for a regular habit (no unit) or an absent value.</summary>
    private static int? Tenths(decimal? value, string? unit) =>
        unit is not null && value is { } v && QuantityRules.TryToTenths(v, out var tenths) ? tenths : null;

    /// <summary>The level text: what was typed or, in a quantitative habit, "&lt;amount&gt; &lt;unit&gt;".</summary>
    private static string LevelText(string? typed, int? tenths, string? unit, HabitKind kind = HabitKind.Positive) =>
        Text.Clean(typed) ?? (unit is not null && tenths is { } t ? QuantityRules.Describe(t, unit)
            : kind == HabitKind.Negative ? NoRelapseText : "");

    /// <summary>Validates a goal/minimum pair of a quantitative habit; null when valid.</summary>
    private static string? ValidateAmounts(decimal? target, decimal? minimum)
    {
        if (target is not { } t || !QuantityRules.TryToTenths(t, out var targetTenths) || targetTenths <= 0) return TargetError;
        if (minimum is not { } m || !QuantityRules.TryToTenths(m, out var minimumTenths) || minimumTenths <= 0) return MinimumTargetError;
        return minimumTenths > targetTenths ? MinimumAboveTargetError : null;
    }

    /// <summary>A description is required in a regular habit; in a quantitative one it may be empty (auto text).</summary>
    private static bool DescriptionOk(string? value, bool quantitative) =>
        Text.LengthBetween(value?.Trim(), 1, 120) || (quantitative && string.IsNullOrWhiteSpace(value));

    /// <summary>Error response for an invalid request, or null. <paramref name="habitId"/> is null when creating.</summary>
    private static async Task<IResult?> ValidateAsync(HabitRequest r, AppDbContext db, int? habitId, CancellationToken ct)
    {
        if (await ValidateFieldsAsync(r, db, ct) is { } message) return ApiResults.Validation(message);

        var anchorText = Text.Clean(r.AnchorText);
        if (anchorText is not null && anchorText.Length > MaxAnchorTextLength)
            return ApiResults.Validation($"O texto de \"Depois de…\" deve ter no máximo {MaxAnchorTextLength} caracteres.");
        if (r.AnchorHabitId is not { } anchorId) return null;

        if (anchorText is not null)
            return ApiResults.Error(StatusCodes.Status400BadRequest, AnchorConflictCode,
                "Use um hábito ou um texto em \"Depois de…\", não os dois.");
        if (anchorId == habitId) return ApiResults.Validation("Um hábito não pode vir depois dele mesmo.");

        var anchors = await db.Habits.AsNoTracking().Select(h => new { h.Id, h.AnchorHabitId })
            .ToDictionaryAsync(h => h.Id, h => h.AnchorHabitId, ct);
        if (!anchors.ContainsKey(anchorId)) return ApiResults.Validation("Hábito de \"Depois de…\" não encontrado.");
        if (habitId is { } id && DayOrder.CreatesCycle(id, anchorId, anchors))
            return ApiResults.Error(StatusCodes.Status400BadRequest, AnchorCycleCode,
                "Esse encadeamento criaria um ciclo entre os hábitos.");
        return null;
    }

    private static async Task<string?> ValidateFieldsAsync(HabitRequest r, AppDbContext db, CancellationToken ct)
    {
        if (!Text.LengthBetween(r.Name?.Trim(), 1, 80))
            return "O nome deve ter entre 1 e 80 caracteres.";

        var emoji = Text.Clean(r.Emoji);
        if (emoji is not null && (new StringInfo(emoji).LengthInTextElements > 8 || emoji.Length > 32))
            return "O emoji deve ter no máximo 8 caracteres.";

        // A negative habit is always daily and never quantitative; its dose and minimum are optional.
        var negative = r.Kind == HabitKind.Negative;
        if (negative)
        {
            if (r.FrequencyType is { } f && f != FrequencyType.Daily) return "Hábito negativo é sempre diário.";
            if (Text.Clean(r.Unit) is not null) return "Hábito negativo não tem quantidade (unidade).";
        }

        switch (negative ? FrequencyType.Daily : r.FrequencyType)
        {
            case null:
                return "Informe a frequência do hábito.";
            case FrequencyType.SpecificDays:
                if (r.FrequencyDays is not { Length: > 0 }) return "Escolha ao menos um dia da semana.";
                if (r.FrequencyDays.Any(d => d is < 0 or > 6))
                    return "Os dias da semana devem estar entre 0 (domingo) e 6 (sábado).";
                if (r.FrequencyDays.Distinct().Count() != r.FrequencyDays.Length)
                    return "Os dias da semana não podem se repetir.";
                break;
            case FrequencyType.WeeklyTarget:
                if (r.TimesPerWeek is not (>= 1 and <= 7)) return "A meta semanal deve ser de 1 a 7 vezes por semana.";
                break;
        }

        var unit = Text.Clean(r.Unit);
        var quantitative = unit is not null;
        if (unit is not null && unit.Length > QuantityRules.MaxUnitLength)
            return $"A unidade deve ter entre 1 e {QuantityRules.MaxUnitLength} caracteres.";
        if (quantitative && ValidateAmounts(r.LevelTarget, r.LevelMinimumTarget) is { } amountsError) return amountsError;

        var optionalText = quantitative || negative;
        if (!DescriptionOk(r.LevelDescription, optionalText)) return LevelDescriptionError;
        if (!DescriptionOk(r.MinimumDescription, optionalText)) return MinimumDescriptionError;

        if (r.PlannedLevels is { } plannedLevels)
        {
            if (plannedLevels.Length > MaxPlannedLevels) return $"Planeje no máximo {MaxPlannedLevels} níveis à frente.";
            if (plannedLevels.Any(l => !DescriptionOk(l?.EffectiveDescription, optionalText)))
                return "A dose de cada nível planejado deve ter entre 1 e 120 caracteres.";
            if (plannedLevels.Any(l => !DescriptionOk(l?.MinimumDescription, optionalText)))
                return "O mínimo de cada nível planejado deve ter entre 1 e 120 caracteres.";
            if (quantitative && plannedLevels.Select(l => ValidateAmounts(l?.Target, l?.MinimumTarget)).FirstOrDefault(e => e is not null) is { } plannedError)
                return $"Nível planejado: {plannedError}";
        }

        if (r.LevelUpWeeks is not null || r.LevelUpDaysPerWeek is not null)
        {
            if (r.LevelUpWeeks is not (>= 1 and <= LevelUpSuggestion.MaxRuleWeeks))
                return $"A regra de subida deve ter de 1 a {LevelUpSuggestion.MaxRuleWeeks} semanas seguidas.";
            if (r.LevelUpDaysPerWeek is not (>= 1 and <= 7))
                return "A regra de subida deve ter de 1 a 7 dias por semana.";
        }

        if (Text.Clean(r.ReminderTime) is { } reminder && !TryParseTime(reminder, out _))
            return "O horário do lembrete deve estar no formato HH:mm.";

        if (r.GoalId is { } goalId && !await db.Goals.AnyAsync(g => g.Id == goalId, ct))
            return "Objetivo não encontrado.";

        return null;
    }

    private static void Apply(Habit habit, HabitRequest r)
    {
        habit.Name = r.Name!.Trim();
        habit.Emoji = Text.Clean(r.Emoji);
        habit.GoalId = r.GoalId;
        if (r.Kind is { } kind) habit.Kind = kind;
        habit.FrequencyType = habit.Kind == HabitKind.Negative ? FrequencyType.Daily : r.FrequencyType!.Value;
        habit.FrequencyDays = habit.FrequencyType == FrequencyType.SpecificDays ? r.FrequencyDays!.Order().ToList() : [];
        habit.TimesPerWeek = habit.FrequencyType == FrequencyType.WeeklyTarget ? r.TimesPerWeek : null;
        habit.ReminderTime = Text.Clean(r.ReminderTime) is { } reminder && TryParseTime(reminder, out var time) ? time : null;
        habit.LevelUpWeeks = r.LevelUpWeeks;
        habit.LevelUpDaysPerWeek = r.LevelUpWeeks is null ? null : r.LevelUpDaysPerWeek;
        habit.Unit = Text.Clean(r.Unit);
        // Absent fields keep their value (new habits start as Anytime with no anchor), so clients that predate
        // the day order do not reset it. "Depois de…": a habit id replaces the text; anchorText "" removes the anchor.
        if (r.TimeOfDay is { } period) habit.TimeOfDay = period;
        if (r.AnchorHabitId is { } anchorId)
        {
            habit.AnchorHabitId = anchorId;
            habit.AnchorText = null;
        }
        else if (r.AnchorText is not null)
        {
            habit.AnchorHabitId = null;
            habit.AnchorText = Text.Clean(r.AnchorText);
        }

        // The ladder ahead is replaced as a whole.
        habit.PlannedLevels.Clear();
        foreach (var (level, i) in (r.PlannedLevels ?? []).Select((l, i) => (l, i)))
        {
            var target = Tenths(level.Target, habit.Unit);
            var minimum = Tenths(level.MinimumTarget, habit.Unit);
            habit.PlannedLevels.Add(new HabitPlannedLevel
            {
                Position = i,
                Description = LevelText(level.EffectiveDescription, target, habit.Unit),
                MinimumDescription = LevelText(level.MinimumDescription, minimum, habit.Unit),
                TargetTenths = target,
                MinimumTenths = minimum,
            });
        }
    }

    /// <summary>Strict "HH:mm" (24h).</summary>
    internal static bool TryParseTime(string value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200];
}
