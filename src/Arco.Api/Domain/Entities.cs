namespace Arco.Api.Domain;

/// <summary>An entity that belongs to one <see cref="User"/>. Queries are filtered by the current user.</summary>
public interface IUserOwned
{
    int UserId { get; set; }
}

/// <summary>An account. Owns every <see cref="IUserOwned"/> row (cascade delete).</summary>
public class User
{
    public int Id { get; set; }
    /// <summary>Always lower-case; unique.</summary>
    public string Email { get; set; } = "";
    /// <summary>BCrypt hash, or null for accounts that only sign in with Google.</summary>
    public string? PasswordHash { get; set; }
    /// <summary>Google "sub" claim; unique.</summary>
    public string? GoogleSubject { get; set; }
    public string DisplayName { get; set; } = "";
    public DateTime? EmailVerifiedAt { get; set; }
    public bool IsDemo { get; set; }
    /// <summary>Changes on password reset/change; sessions carrying an older stamp are rejected.</summary>
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; }
}

/// <summary>Single-use token sent by e-mail. Only its SHA-256 is stored.</summary>
public class UserToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public UserTokenPurpose Purpose { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

/// <summary>One row per user with their preferences. Find-or-create via <c>db.Settings.FirstOrDefaultAsync()</c>.</summary>
public class Settings : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Timezone { get; set; } = TimeZones.Default;

    /// <summary>Push reminder for the weekly review (local day and time in <see cref="Timezone"/>).</summary>
    public bool WeeklyReviewReminderEnabled { get; set; } = true;
    /// <summary>0 = Sunday … 6 = Saturday.</summary>
    public int WeeklyReviewReminderDay { get; set; } = DefaultWeeklyReviewReminderDay;
    public TimeOnly WeeklyReviewReminderTime { get; set; } = DefaultWeeklyReviewReminderTime;

    public const int DefaultWeeklyReviewReminderDay = 0;
    public static readonly TimeOnly DefaultWeeklyReviewReminderTime = new(18, 0);
}

public class Cycle : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
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
    /// <summary>Focus-minutes goal per cycle week (60..3360).</summary>
    public int WeeklyFocusMinutes { get; set; } = 420;
    public List<Goal> Goals { get; set; } = [];
}

public class Goal : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
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

public class Milestone : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int GoalId { get; set; }
    public Goal? Goal { get; set; }
    public string Title { get; set; } = "";
    public int Order { get; set; }
    public DateTime? DoneAt { get; set; }
}

public class GoalUpdate : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int GoalId { get; set; }
    public double Value { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Habit : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
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

public class HabitPlannedLevel : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int HabitId { get; set; }
    public int Position { get; set; }
    public string Description { get; set; } = "";
    public string MinimumDescription { get; set; } = "";
}

public class HabitLevel : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int HabitId { get; set; }
    public int Level { get; set; }
    public string Description { get; set; } = "";
    public string MinimumDescription { get; set; } = "";
    public DateTime ReachedAt { get; set; }
}

public class CheckIn : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int HabitId { get; set; }
    public Habit? Habit { get; set; }
    public DateOnly Date { get; set; }
    public CheckInStatus Status { get; set; }
    /// <summary>XP awarded by this check-in (mirrors its XpEvent; 0 when Missed).</summary>
    public int Xp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Time spent on a habit on a given local day (timer or manual minutes). No XP.</summary>
public class FocusSession : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int HabitId { get; set; }
    public Habit? Habit { get; set; }
    public DateOnly Date { get; set; }
    public int Minutes { get; set; }
    public FocusSource Source { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>XP ledger entry. The player's total XP is the sum of all events.</summary>
public class XpEvent : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
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
public class WeeklyReview : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly WeekStart { get; set; }
    public string? Wins { get; set; }
    public string? Obstacles { get; set; }
    public string? Next { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A browser/device subscribed to Web Push.</summary>
public class PushSubscription : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
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

/// <summary>A key for the API (<c>Authorization: Bearer arco_…</c>). Only its SHA-256 is stored.</summary>
public class ApiKey : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>First characters of the key, shown so the owner can recognize it.</summary>
    public string Prefix { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>An achievement of <see cref="AchievementCatalog"/> already unlocked (never revoked).</summary>
public class UnlockedAchievement : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Key { get; set; } = "";
    public DateTime UnlockedAt { get; set; }
}

/// <summary>Records a reminder that was already sent, so it is sent at most once (e.g. "habit:5:2026-10-21").</summary>
public class PushDispatch : IUserOwned
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Key { get; set; } = "";
    public DateTime SentAt { get; set; }
}
