namespace Arco.Api.Domain;

/// <summary>Single-row table (Id = 1) with user preferences.</summary>
public class Settings
{
    public const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;
    public string Timezone { get; set; } = TimeZones.Default;

    /// <summary>Push reminder for the weekly review (local day and time in <see cref="Timezone"/>).</summary>
    public bool WeeklyReviewReminderEnabled { get; set; } = true;
    /// <summary>0 = Sunday … 6 = Saturday.</summary>
    public int WeeklyReviewReminderDay { get; set; } = DefaultWeeklyReviewReminderDay;
    public TimeOnly WeeklyReviewReminderTime { get; set; } = DefaultWeeklyReviewReminderTime;

    public const int DefaultWeeklyReviewReminderDay = 0;
    public static readonly TimeOnly DefaultWeeklyReviewReminderTime = new(18, 0);
}

public class Cycle
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public CycleStatus Status { get; set; } = CycleStatus.Active;
    /// <summary>Free notes of the end-of-cycle review.</summary>
    public string? ReviewNotes { get; set; }
    /// <summary>Guided review: what worked.</summary>
    public string? ReviewWins { get; set; }
    /// <summary>Guided review: what got in the way.</summary>
    public string? ReviewObstacles { get; set; }
    /// <summary>Guided review: what to take into the next cycle.</summary>
    public string? ReviewNext { get; set; }
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
    /// <summary>Local time of the daily push reminder (in Settings.Timezone), or null for none.</summary>
    public TimeOnly? ReminderTime { get; set; }
    /// <summary>A dismissed level-up suggestion stays hidden until this date (exclusive).</summary>
    public DateOnly? LevelUpSnoozedUntil { get; set; }
    /// <summary>A dismissed level-down suggestion stays hidden until this date (exclusive).</summary>
    public DateOnly? LevelDownSnoozedUntil { get; set; }
    /// <summary>
    /// Level-up rule: suggest after this many weeks in a row with at least <see cref="LevelUpDaysPerWeek"/>
    /// Done/Minimum days. Both null = the default 80%-in-14-days rule.
    /// </summary>
    public int? LevelUpWeeks { get; set; }
    public int? LevelUpDaysPerWeek { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<HabitLevel> Levels { get; set; } = [];
    /// <summary>The ladder ahead: the next levels, in order. Leveling up consumes the first one.</summary>
    public List<HabitPlannedLevel> PlannedLevels { get; set; } = [];

    public HabitLevel? CurrentLevel => Levels.Count == 0 ? null : Levels.MaxBy(l => l.Level);
    public int CurrentLevelNumber => CurrentLevel?.Level ?? 1;
}

public class HabitPlannedLevel
{
    public int Id { get; set; }
    public int HabitId { get; set; }
    public int Position { get; set; }
    public string Description { get; set; } = "";
    public string MinimumDescription { get; set; } = "";
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
    public int? WeeklyReviewId { get; set; }
    public WeeklyReview? WeeklyReview { get; set; }
}

/// <summary>Guided review of a Monday–Sunday week. At most one per week.</summary>
public class WeeklyReview
{
    public int Id { get; set; }
    public DateOnly WeekStart { get; set; }
    public string? Wins { get; set; }
    public string? Obstacles { get; set; }
    public string? Next { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A browser/device subscribed to Web Push.</summary>
public class PushSubscription
{
    public int Id { get; set; }
    public string Endpoint { get; set; } = "";
    /// <summary>Client public key (base64url, uncompressed P-256 point).</summary>
    public string P256dh { get; set; } = "";
    /// <summary>Client auth secret (base64url, 16 bytes).</summary>
    public string Auth { get; set; } = "";
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public int FailureCount { get; set; }
}

/// <summary>An achievement of <see cref="AchievementCatalog"/> already unlocked (never revoked).</summary>
public class UnlockedAchievement
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public DateTime UnlockedAt { get; set; }
}

/// <summary>Records a reminder that was already sent, so it is sent at most once (e.g. "habit:5:2026-10-21").</summary>
public class PushDispatch
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public DateTime SentAt { get; set; }
}
