namespace Arco.Api.Domain;

/// <summary>XP amounts per PRODUCT.md.</summary>
public static class XpRules
{
    public const int HabitLevelUp = 50;
    public const int MilestoneDone = 25;
    public const int GoalCompleted = 150;
    public const int CycleCompleted = 100;

    /// <summary>Done = 10 + 2 × (habit level − 1).</summary>
    public static int ForDone(int habitLevel) => 10 + 2 * (Math.Max(1, habitLevel) - 1);

    /// <summary>Done → full; Minimum → half of Done rounded down; Missed → 0.</summary>
    public static int ForCheckIn(CheckInStatus status, int habitLevel) => status switch
    {
        CheckInStatus.Done => ForDone(habitLevel),
        CheckInStatus.Minimum => ForDone(habitLevel) / 2,
        _ => 0,
    };
}
