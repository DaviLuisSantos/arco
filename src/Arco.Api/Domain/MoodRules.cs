namespace Arco.Api.Domain;

/// <summary>Pure rules for the daily mood (1..5) and its relation to habit consistency.</summary>
public static class MoodRules
{
    public const int Min = 1, Max = 5;

    public static bool IsGood(int v) => v >= 4;
    public static bool IsBad(int v) => v <= 2;

    /// <summary>One habit's schedule, pause calendar and check-ins, as needed to measure consistency on given days.</summary>
    public sealed record HabitDays(
        FrequencyType Type,
        IReadOnlyList<int> Days,
        PauseCalendar Calendar,
        IReadOnlyDictionary<DateOnly, CheckInStatus> CheckIns,
        DateOnly Start);

    public sealed record MoodSummary(
        int Days, double? Average, int GoodDays, int BadDays, double? GoodConsistency, double? BadConsistency);

    /// <summary>
    /// Average mood (1 decimal) and consistency on good (4-5) and bad (1-2) days: Done/Minimum check-ins divided by the
    /// due days (Daily and SpecificDays habits, from their start, not excused). Weekly-target habits are left out.
    /// A group without days or without expected check-ins is null.
    /// </summary>
    public static MoodSummary Summarize(IReadOnlyDictionary<DateOnly, int> moods, IEnumerable<HabitDays> habits)
    {
        if (moods.Count == 0) return new MoodSummary(0, null, 0, 0, null, null);

        var list = habits as IReadOnlyCollection<HabitDays> ?? habits.ToList();
        var good = moods.Where(m => IsGood(m.Value)).Select(m => m.Key).ToList();
        var bad = moods.Where(m => IsBad(m.Value)).Select(m => m.Key).ToList();

        return new MoodSummary(
            moods.Count,
            Math.Round(moods.Values.Average(), 1, MidpointRounding.AwayFromZero),
            good.Count,
            bad.Count,
            Consistency(good, list),
            Consistency(bad, list));
    }

    private static double? Consistency(IReadOnlyList<DateOnly> days, IReadOnlyCollection<HabitDays> habits)
    {
        int expected = 0, done = 0;
        foreach (var day in days)
        foreach (var h in habits)
        {
            if (day < h.Start || !IsDue(h, day) || h.Calendar.IsExcused(day)) continue;
            expected++;
            if (h.CheckIns.TryGetValue(day, out var status) && HabitSchedule.CountsTowardWeek(status)) done++;
        }
        return expected == 0 ? null : done / (double)expected;
    }

    private static bool IsDue(HabitDays h, DateOnly day) => h.Type switch
    {
        FrequencyType.Daily => true,
        FrequencyType.SpecificDays => h.Days.Contains((int)day.DayOfWeek),
        _ => false,
    };
}
