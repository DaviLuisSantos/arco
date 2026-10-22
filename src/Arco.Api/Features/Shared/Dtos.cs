namespace Arco.Api.Features.Shared;

public class PlayerDto
{
    public PlayerDto(int totalXp)
    {
        var p = PlayerLevel.FromTotalXp(totalXp);
        Level = p.Level;
        TotalXp = p.TotalXp;
        XpIntoLevel = p.XpIntoLevel;
        XpForNextLevel = p.XpForNextLevel;
    }

    public int Level { get; }
    public int TotalXp { get; }
    public int XpIntoLevel { get; }
    public int XpForNextLevel { get; }
}

public sealed record CycleDto(
    int Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    CycleStatus Status,
    int DayNumber,
    int TotalDays,
    string? ReviewNotes,
    string? ReviewWins,
    string? ReviewObstacles,
    string? ReviewNext,
    DateTime? CompletedAt)
{
    public static CycleDto From(Cycle c, DateOnly today) => new(
        c.Id, c.Name, c.StartDate, c.EndDate, c.Status,
        CycleMath.DayNumber(c.StartDate, c.EndDate, today),
        CycleMath.TotalDays(c.StartDate, c.EndDate),
        c.ReviewNotes, c.ReviewWins, c.ReviewObstacles, c.ReviewNext, c.CompletedAt);
}

public sealed record MilestoneDto(int Id, string Title, int Order, DateTime? DoneAt);

public sealed record GoalDto(
    int Id,
    int CycleId,
    string Title,
    string? Description,
    GoalType Type,
    string? Unit,
    double? StartValue,
    double? TargetValue,
    double? CurrentValue,
    IReadOnlyList<MilestoneDto> Milestones,
    DateOnly Deadline,
    double Progress,
    DateTime? CompletedAt,
    IReadOnlyList<int> HabitIds)
{
    /// <param name="g">Goal with Milestones loaded.</param>
    public static GoalDto From(Goal g, IReadOnlyList<int> habitIds)
    {
        var milestones = g.Milestones.OrderBy(m => m.Order).ThenBy(m => m.Id)
            .Select(m => new MilestoneDto(m.Id, m.Title, m.Order, m.DoneAt)).ToList();
        double progress = g.CompletedAt is not null
            ? 1
            : g.Type == GoalType.Numeric
                ? GoalProgress.Numeric(g.StartValue, g.TargetValue, g.CurrentValue)
                : GoalProgress.Milestones(milestones.Count(m => m.DoneAt is not null), milestones.Count);
        return new GoalDto(g.Id, g.CycleId, g.Title, g.Description, g.Type, g.Unit,
            g.StartValue, g.TargetValue, g.CurrentValue,
            g.Type == GoalType.Numeric ? [] : milestones,
            g.Deadline, progress, g.CompletedAt, habitIds);
    }
}

public sealed record HabitLevelDto(int Level, string Description, string MinimumDescription, DateTime ReachedAt);

/// <summary>A planned (not yet reached) level; <c>Level</c> is the number it will get.</summary>
public sealed record HabitPlannedLevelDto(int Level, string Description, string MinimumDescription);

public sealed record CheckInDto(int HabitId, DateOnly Date, CheckInStatus Status, int Xp)
{
    public static CheckInDto From(CheckIn c) => new(c.HabitId, c.Date, c.Status, c.Xp);
}

public class HabitDto
{
    /// <param name="h">Habit with Levels and PlannedLevels loaded.</param>
    public HabitDto(Habit h)
    {
        var levels = h.Levels.OrderBy(l => l.Level).ToList();
        var current = levels.LastOrDefault();
        Id = h.Id;
        Name = h.Name;
        Emoji = h.Emoji;
        GoalId = h.GoalId;
        FrequencyType = h.FrequencyType;
        FrequencyDays = h.FrequencyType == FrequencyType.SpecificDays ? h.FrequencyDays.Order().ToList() : [];
        TimesPerWeek = h.FrequencyType == FrequencyType.WeeklyTarget ? h.TimesPerWeek : null;
        ReminderTime = h.ReminderTime?.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        Level = current?.Level ?? 1;
        LevelDescription = current?.Description ?? "";
        MinimumDescription = current?.MinimumDescription ?? "";
        Levels = levels.Select(l => new HabitLevelDto(l.Level, l.Description, l.MinimumDescription, l.ReachedAt)).ToList();
        PlannedLevels = h.PlannedLevels.OrderBy(p => p.Position).ThenBy(p => p.Id)
            .Select((p, i) => new HabitPlannedLevelDto(Level + i + 1, p.Description, p.MinimumDescription)).ToList();
        LevelUpWeeks = h.LevelUpWeeks;
        LevelUpDaysPerWeek = h.LevelUpDaysPerWeek;
        ArchivedAt = h.ArchivedAt;
        CreatedAt = h.CreatedAt;
    }

    public int Id { get; }
    public string Name { get; }
    public string? Emoji { get; }
    public int? GoalId { get; }
    public FrequencyType FrequencyType { get; }
    public IReadOnlyList<int> FrequencyDays { get; }
    public int? TimesPerWeek { get; }
    /// <summary>"HH:mm" or null.</summary>
    public string? ReminderTime { get; }
    public int Level { get; }
    public string LevelDescription { get; }
    public string MinimumDescription { get; }
    public IReadOnlyList<HabitLevelDto> Levels { get; }
    public IReadOnlyList<HabitPlannedLevelDto> PlannedLevels { get; }
    /// <summary>Weeks rule (null = default 80%-in-14-days rule).</summary>
    public int? LevelUpWeeks { get; }
    public int? LevelUpDaysPerWeek { get; }
    public DateTime? ArchivedAt { get; }
    public DateTime CreatedAt { get; }
}

public sealed record ConsistencyDto(DateOnly From, DateOnly To, int Done, int Expected, double Rate)
{
    public static ConsistencyDto Of(LevelUpSuggestion.Consistency c) =>
        new(c.From, c.To, c.Done, c.Expected, Math.Round(c.Rate, 4));
}

public sealed record StreakDto(int Current, int Best, StreakUnit Unit)
{
    public static StreakDto Of(StreakRules.Streak s) => new(s.Current, s.Best, s.Unit);
}

/// <summary>Response shape for XP-changing actions: <c>{ &lt;name&gt;, xpDelta, player }</c>.</summary>
public sealed record CheckInResult(CheckInDto CheckIn, int XpDelta, PlayerDto Player);
public sealed record XpResult(int XpDelta, PlayerDto Player);
public sealed record HabitXpResult(HabitDto Habit, int XpDelta, PlayerDto Player);
public sealed record CycleXpResult(CycleDto Cycle, int XpDelta, PlayerDto Player);
public sealed record GoalXpResult(GoalDto Goal, int XpDelta, PlayerDto Player);
