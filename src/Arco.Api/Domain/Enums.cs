namespace Arco.Api.Domain;

public enum FrequencyType { Daily, SpecificDays, WeeklyTarget }

/// <summary>Fixed part of the day a habit belongs to; <c>Anytime</c> is the default.</summary>
public enum TimeOfDay { Anytime, Morning, Afternoon, Evening }

/// <summary>Positive = something to do; Negative = something to quit (clean days, a relapse is a Missed).</summary>
public enum HabitKind { Positive, Negative }

public enum CheckInStatus{ Done, Minimum, Missed, Skipped }

public enum FocusSource { Timer, Manual }

public enum GoalType { Numeric, Milestones }

public enum XpSource { CheckIn, HabitLevelUp, Milestone, GoalCompleted, CycleCompleted, WeeklyReview }

public enum CycleStatus { Active, Completed }

public enum UserTokenPurpose { VerifyEmail, ResetPassword }

public enum UserPlan { Free, Pro }
