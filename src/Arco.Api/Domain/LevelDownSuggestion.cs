namespace Arco.Api.Domain;

/// <summary>
/// Suggests going back one level when the last <see cref="Weeks"/> finished weeks, all at the current level,
/// stayed below half of the habit's weekly target (Done and Minimum count). Going back is always manual.
/// </summary>
public static class LevelDownSuggestion
{
    public const int Weeks = 2;
    public const int SnoozeDays = 7;

    /// <summary>
    /// Days expected per week: the weeks rule's days when set, otherwise the frequency
    /// (Daily = 7, SpecificDays = number of days, WeeklyTarget = times per week).
    /// </summary>
    public static int WeeklyTarget(FrequencyType type, IReadOnlyCollection<int> frequencyDays, int? timesPerWeek,
        int? levelUpDaysPerWeek) =>
        levelUpDaysPerWeek ?? type switch
        {
            FrequencyType.Daily => 7,
            FrequencyType.SpecificDays => Math.Max(1, frequencyDays.Count),
            FrequencyType.WeeklyTarget => timesPerWeek ?? 1,
            _ => 7,
        };

    /// <summary>A week is weak when it has fewer than half of the target (e.g. target 5 → 2 days or fewer).</summary>
    public static bool IsWeak(int days, int target) => days * 2 < target;

    public static bool ShouldSuggest(IEnumerable<(DateOnly Date, CheckInStatus Status)> checkIns, int weeklyTarget,
        int level, DateOnly levelReachedOn, DateOnly? snoozedUntil, DateOnly today, bool archived)
    {
        if (archived || level < 2 || snoozedUntil is { } until && today < until) return false;

        var perWeek = checkIns
            .Where(c => c.Date < HabitSchedule.WeekStart(today) && HabitSchedule.CountsTowardWeek(c.Status))
            .GroupBy(c => HabitSchedule.WeekStart(c.Date))
            .ToDictionary(g => g.Key, g => g.Select(c => c.Date).Distinct().Count());

        // The last finished weeks, each entirely at the current level.
        var lastFinished = HabitSchedule.WeekStart(today).AddDays(-7);
        return Enumerable.Range(0, Weeks)
            .Select(i => lastFinished.AddDays(-7 * i))
            .All(week => week >= levelReachedOn && IsWeak(perWeek.GetValueOrDefault(week), weeklyTarget));
    }
}
