namespace Arco.Api.Domain;

public enum FrequencyType { Daily, SpecificDays, WeeklyTarget }

public enum CheckInStatus { Done, Minimum, Missed }

public enum GoalType { Numeric, Milestones }

public enum XpSource { CheckIn, HabitLevelUp, Milestone, GoalCompleted, CycleCompleted }

public enum CycleStatus { Active, Completed }
