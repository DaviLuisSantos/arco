namespace Arco.Api.Domain;

/// <summary>Single-row table (Id = 1) with user preferences.</summary>
public class Settings
{
    public const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;
    public string Timezone { get; set; } = TimeZones.Default;
}

public class Cycle
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public CycleStatus Status { get; set; } = CycleStatus.Active;
    public string? ReviewNotes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Goal> Goals { get; set; } = [];
}

public class Goal
{
    public int Id { get; set; }
    public int CycleId { get; set; }
    public Cycle? Cycle { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public GoalType Type { get; set; }
    public string? Unit { get; set; }
    public double? StartValue { get; set; }
    public double? TargetValue { get; set; }
    /// <summary>Denormalized: value of the latest <see cref="GoalUpdate"/>.</summary>
    public double? CurrentValue { get; set; }
    public DateOnly Deadline { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Milestone> Milestones { get; set; } = [];
    public List<GoalUpdate> Updates { get; set; } = [];
}

public class Milestone
{
    public int Id { get; set; }
    public int GoalId { get; set; }
    public Goal? Goal { get; set; }
    public string Title { get; set; } = "";
    public int Order { get; set; }
    public DateTime? DoneAt { get; set; }
}

public class GoalUpdate
{
    public int Id { get; set; }
    public int GoalId { get; set; }
    public double Value { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Habit
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Emoji { get; set; }
    public int? GoalId { get; set; }
    public Goal? Goal { get; set; }
    public FrequencyType FrequencyType { get; set; }
    public List<int> FrequencyDays { get; set; } = [];
    public int? TimesPerWeek { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<HabitLevel> Levels { get; set; } = [];

    public HabitLevel? CurrentLevel => Levels.Count == 0 ? null : Levels.MaxBy(l => l.Level);
    public int CurrentLevelNumber => CurrentLevel?.Level ?? 1;
}

public class HabitLevel
{
    public int Id { get; set; }
    public int HabitId { get; set; }
    public int Level { get; set; }
    public string Description { get; set; } = "";
    public string MinimumDescription { get; set; } = "";
    public DateTime ReachedAt { get; set; }
}

public class CheckIn
{
    public int Id { get; set; }
    public int HabitId { get; set; }
    public Habit? Habit { get; set; }
    public DateOnly Date { get; set; }
    public CheckInStatus Status { get; set; }
    /// <summary>XP awarded by this check-in (mirrors its XpEvent; 0 when Missed).</summary>
    public int Xp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>XP ledger entry. The player's total XP is the sum of all events.</summary>
public class XpEvent
{
    public int Id { get; set; }
    /// <summary>Calendar date the XP belongs to (check-in date for check-ins, "today" otherwise).</summary>
    public DateOnly Date { get; set; }
    public int Amount { get; set; }
    public XpSource Source { get; set; }
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public int? CheckInId { get; set; }
    public CheckIn? CheckIn { get; set; }
    public int? HabitId { get; set; }
    public int? GoalId { get; set; }
    public int? MilestoneId { get; set; }
    public int? CycleId { get; set; }
}
