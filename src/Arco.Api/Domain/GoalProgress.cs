namespace Arco.Api.Domain;

public static class GoalProgress
{
    public static double Clamp01(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);

    /// <summary>(current − start) / (target − start), clamped to 0..1. Works for decreasing targets.</summary>
    public static double Numeric(double? start, double? target, double? current)
    {
        if (start is not { } s || target is not { } t || current is not { } c || s == t) return 0;
        return Clamp01((c - s) / (t - s));
    }

    public static double Milestones(int done, int total) => total <= 0 ? 0 : Clamp01((double)done / total);

    /// <summary>Increasing target: value ≥ target. Decreasing target: value ≤ target.</summary>
    public static bool IsTargetReached(double start, double target, double value) =>
        target >= start ? value >= target : value <= target;
}
