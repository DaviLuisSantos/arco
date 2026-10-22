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
        int? LevelUpDaysPerWeek = null);

    public sealed record PlannedLevelRequest(string? LevelDescription, string? MinimumDescription);

    /// <summary>Both fields optional when the habit has a planned next level (it is used instead).</summary>
    public sealed record LevelUpRequest(string? LevelDescription, string? MinimumDescription);

    private const int MaxPlannedLevels = 10;

    public sealed class HabitDetailDto(
        Habit habit,
        IReadOnlyList<CheckInDto> recentCheckIns,
        ConsistencyDto consistency,
        bool levelUpSuggested,
        int? levelUpWeeksDone,
        StreakDto streak) : HabitDto(habit)
    {
        public IReadOnlyList<CheckInDto> RecentCheckIns { get; } = recentCheckIns;
        public ConsistencyDto Consistency { get; } = consistency;
        public bool LevelUpSuggested { get; } = levelUpSuggested;
        public int? LevelUpWeeksDone { get; } = levelUpWeeksDone;
        public StreakDto Streak { get; } = streak;
    }

    public static IEndpointRouteBuilder MapHabits(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/habits");
        group.MapGet("", List);
        group.MapGet("/{id:int}", Get);
        group.MapPost("", Create);
        group.MapPut("/{id:int}", Update);
        group.MapPost("/{id:int}/level-up", LevelUp);
        group.MapPost("/{id:int}/level-up-suggestion/dismiss", DismissSuggestion);
        group.MapPost("/{id:int}/archive", Archive);
        group.MapPost("/{id:int}/restore", Restore);
        return api;
    }

    private const string HabitNotFound = "Hábito não encontrado.";

    private static async Task<List<HabitDto>> List(AppDbContext db, CancellationToken ct, bool includeArchived = false)
    {
        var query = db.Habits.AsNoTracking().Include(h => h.Levels).Include(h => h.PlannedLevels).AsQueryable();
        if (!includeArchived) query = query.Where(h => h.ArchivedAt == null);
        var habits = await query.ToListAsync(ct);
        return habits
            .OrderBy(h => h.Name, StringComparer.InvariantCultureIgnoreCase).ThenBy(h => h.Id)
            .Select(h => new HabitDto(h)).ToList();
    }

    private static async Task<IResult> Get(int id, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var habit = await db.Habits.AsNoTracking().Include(h => h.Levels).Include(h => h.PlannedLevels)
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
        var (consistency, weeksDone, suggested) = Queries.LevelUp(habit, history, today, timezone);
        return Results.Ok(new HabitDetailDto(habit, checkIns.Select(CheckInDto.From).ToList(), consistency, suggested,
            weeksDone, Queries.Streak(habit, history, today, timezone)));
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

    private static async Task<IResult> Create(HabitRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        if (request is null) return ApiResults.Validation("Corpo da requisição ausente.");
        var error = await ValidateAsync(request, db, ct);
        if (error is not null) return ApiResults.Validation(error);

        var now = clock.UtcNow;
        var habit = new Habit { CreatedAt = now };
        Apply(habit, request);
        habit.Levels.Add(new HabitLevel
        {
            Level = 1,
            Description = request.LevelDescription!.Trim(),
            MinimumDescription = request.MinimumDescription!.Trim(),
            ReachedAt = now,
        });
        db.Habits.Add(habit);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/habits/{habit.Id}", new HabitDto(habit));
    }

    private static async Task<IResult> Update(int id, HabitRequest? request, AppDbContext db, CancellationToken ct)
    {
        if (request is null) return ApiResults.Validation("Corpo da requisição ausente.");
        var habit = await db.Habits.Include(h => h.Levels).Include(h => h.PlannedLevels).FirstOrDefaultAsync(h => h.Id == id, ct);
        if (habit is null) return ApiResults.NotFound(HabitNotFound);

        var error = await ValidateAsync(request, db, ct);
        if (error is not null) return ApiResults.Validation(error);

        Apply(habit, request);
        var current = habit.CurrentLevel;
        if (current is not null)
        {
            current.Description = request.LevelDescription!.Trim();
            current.MinimumDescription = request.MinimumDescription!.Trim();
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new HabitDto(habit));
    }

    private static async Task<IResult> LevelUp(int id, LevelUpRequest? request, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var habit = await db.Habits.Include(h => h.Levels).Include(h => h.PlannedLevels).FirstOrDefaultAsync(h => h.Id == id, ct);
        if (habit is null) return ApiResults.NotFound(HabitNotFound);
        if (habit.ArchivedAt is not null) return ApiResults.Validation("Não é possível subir de nível um hábito arquivado.");

        var planned = habit.PlannedLevels.OrderBy(p => p.Position).ThenBy(p => p.Id).FirstOrDefault();
        var description = Text.Clean(request?.LevelDescription) ?? planned?.Description;
        var minimum = Text.Clean(request?.MinimumDescription) ?? planned?.MinimumDescription;
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
            ReachedAt = now,
        });
        habit.LevelUpSnoozedUntil = null;
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

        return Results.Ok(new HabitXpResult(new HabitDto(habit), XpRules.HabitLevelUp, await Queries.GetPlayerAsync(db, ct)));
    }

    private static Task<IResult> Archive(int id, AppDbContext db, AppClock clock, CancellationToken ct) =>
        SetArchived(id, archive: true, db, clock, ct);

    private static Task<IResult> Restore(int id, AppDbContext db, AppClock clock, CancellationToken ct) =>
        SetArchived(id, archive: false, db, clock, ct);

    private static async Task<IResult> SetArchived(int id, bool archive, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var habit = await db.Habits.Include(h => h.Levels).Include(h => h.PlannedLevels).FirstOrDefaultAsync(h => h.Id == id, ct);
        if (habit is null) return ApiResults.NotFound(HabitNotFound);

        if (archive) habit.ArchivedAt ??= clock.UtcNow;
        else habit.ArchivedAt = null;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new HabitDto(habit));
    }

    private const string LevelDescriptionError = "A descrição do nível deve ter entre 1 e 120 caracteres.";
    private const string MinimumDescriptionError = "A versão mínima deve ter entre 1 e 120 caracteres.";

    private static async Task<string?> ValidateAsync(HabitRequest r, AppDbContext db, CancellationToken ct)
    {
        if (!Text.LengthBetween(r.Name?.Trim(), 1, 80))
            return "O nome deve ter entre 1 e 80 caracteres.";

        var emoji = Text.Clean(r.Emoji);
        if (emoji is not null && (new StringInfo(emoji).LengthInTextElements > 8 || emoji.Length > 32))
            return "O emoji deve ter no máximo 8 caracteres.";

        switch (r.FrequencyType)
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

        if (!Text.LengthBetween(r.LevelDescription?.Trim(), 1, 120)) return LevelDescriptionError;
        if (!Text.LengthBetween(r.MinimumDescription?.Trim(), 1, 120)) return MinimumDescriptionError;

        if (r.PlannedLevels is { } plannedLevels)
        {
            if (plannedLevels.Length > MaxPlannedLevels) return $"Planeje no máximo {MaxPlannedLevels} níveis à frente.";
            if (plannedLevels.Any(l => !Text.LengthBetween(l?.LevelDescription?.Trim(), 1, 120)))
                return "A dose de cada nível planejado deve ter entre 1 e 120 caracteres.";
            if (plannedLevels.Any(l => !Text.LengthBetween(l?.MinimumDescription?.Trim(), 1, 120)))
                return "O mínimo de cada nível planejado deve ter entre 1 e 120 caracteres.";
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
        habit.FrequencyType = r.FrequencyType!.Value;
        habit.FrequencyDays = habit.FrequencyType == FrequencyType.SpecificDays ? r.FrequencyDays!.Order().ToList() : [];
        habit.TimesPerWeek = habit.FrequencyType == FrequencyType.WeeklyTarget ? r.TimesPerWeek : null;
        habit.ReminderTime = Text.Clean(r.ReminderTime) is { } reminder && TryParseTime(reminder, out var time) ? time : null;
        habit.LevelUpWeeks = r.LevelUpWeeks;
        habit.LevelUpDaysPerWeek = r.LevelUpWeeks is null ? null : r.LevelUpDaysPerWeek;

        // The ladder ahead is replaced as a whole.
        habit.PlannedLevels.Clear();
        foreach (var (level, i) in (r.PlannedLevels ?? []).Select((l, i) => (l, i)))
        {
            habit.PlannedLevels.Add(new HabitPlannedLevel
            {
                Position = i,
                Description = level.LevelDescription!.Trim(),
                MinimumDescription = level.MinimumDescription!.Trim(),
            });
        }
    }

    /// <summary>Strict "HH:mm" (24h).</summary>
    internal static bool TryParseTime(string value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200];
}
