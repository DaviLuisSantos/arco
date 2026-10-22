namespace Arco.Api.Domain;

/// <summary>
/// Suggests leveling up a habit when it reached ≥ 80% consistency over the last 14 days
/// at its current level. Only Done counts: Minimum keeps the habit alive but does not show
/// that the dose became easy.
/// </summary>
public static class LevelUpSuggestion
{
    public const int WindowDays = 14;
    public const double Threshold = 0.8;
    public const int SnoozeDays = 7;

    /// <summary>Limit of the "N weeks in a row with X days" rule.</summary>
    public const int MaxRuleWeeks = 8;

    /// <summary>Days of check-in history (ending today) that every rule needs.</summary>
    public const int HistoryDays = 7 * (MaxRuleWeeks + 1);

    /// <summary>
    /// Weeks rule: consecutive Monday–Sunday weeks, each entirely at the current level, with at least
    /// <paramref name="daysPerWeek"/> Done/Minimum check-ins. The current week counts once it qualifies;
    /// until then the run ends at the previous week.
    /// </summary>
    public static int QualifyingWeeks(IEnumerable<(DateOnly Date, CheckInStatus Status)> checkIns, int daysPerWeek,
        DateOnly levelReachedOn, DateOnly today)
    {
        var perWeek = checkIns
            .Where(c => c.Date <= today && HabitSchedule.CountsTowardWeek(c.Status))
            .GroupBy(c => HabitSchedule.WeekStart(c.Date))
            .ToDictionary(g => g.Key, g => g.Select(c => c.Date).Distinct().Count());
        int Count(DateOnly week) => perWeek.GetValueOrDefault(week);

        var week = HabitSchedule.WeekStart(today);
        if (Count(week) < daysPerWeek) week = week.AddDays(-7);
        var weeks = 0;
        while (week >= levelReachedOn && Count(week) >= daysPerWeek && weeks < MaxRuleWeeks)
        {
            weeks++;
            week = week.AddDays(-7);
        }
        return weeks;
    }

    public sealed record Consistency(DateOnly From, DateOnly To, int Done, int Expected)
    {
        /// <summary>Done / expected, limited to 0..1 (0 when nothing was expected).</summary>
        public double Rate => Expected == 0 ? 0 : Math.Min(1, (double)Done / Expected);
    }

    /// <summary>
    /// The 14 days ending today when today already has a check-in, otherwise ending yesterday,
    /// so an unmarked "today" never lowers the rate.
    /// </summary>
    public static (DateOnly From, DateOnly To) Window(DateOnly today, bool checkedInToday)
    {
        var to = checkedInToday ? today : today.AddDays(-1);
        return (to.AddDays(-(WindowDays - 1)), to);
    }

    /// <summary>
    /// Expected check-ins in [from, to]. Daily: every day. SpecificDays: matching weekdays.
    /// WeeklyTarget: timesPerWeek per 7 days, rounded up.
    /// </summary>
    public static int ExpectedCount(FrequencyType type, IReadOnlyCollection<int> frequencyDays, int? timesPerWeek,
        DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber + 1;
        if (days <= 0) return 0;
        return type switch
        {
            FrequencyType.Daily => days,
            FrequencyType.SpecificDays => Enumerable.Range(0, days)
                .Count(i => frequencyDays.Contains((int)from.AddDays(i).DayOfWeek)),
            FrequencyType.WeeklyTarget => (int)Math.Ceiling((timesPerWeek ?? 1) * days / 7.0),
            _ => 0,
        };
    }

    public static Consistency Measure(FrequencyType type, IReadOnlyCollection<int> frequencyDays, int? timesPerWeek,
        IEnumerable<(DateOnly Date, CheckInStatus Status)> checkIns, DateOnly today)
    {
        var list = checkIns.ToList();
        var (from, to) = Window(today, list.Any(c => c.Date == today));
        var done = list.Count(c => c.Date >= from && c.Date <= to && c.Status == CheckInStatus.Done);
        return new Consistency(from, to, done, ExpectedCount(type, frequencyDays, timesPerWeek, from, to));
    }

    /// <param name="levelReachedOn">Local date the current level was reached: the whole window must be at this level.</param>
    /// <param name="snoozedUntil">Dismissed suggestions stay hidden until this date (exclusive).</param>
    public static bool ShouldSuggest(Consistency consistency, DateOnly levelReachedOn, DateOnly? snoozedUntil,
        DateOnly today, bool archived) =>
        !archived
        && levelReachedOn <= consistency.From
        && (snoozedUntil is not { } until || today >= until)
        && consistency.Expected > 0
        && consistency.Rate >= Threshold;

    /// <summary>Weeks rule: suggest once <paramref name="weeksDone"/> reaches the required weeks.</summary>
    public static bool ShouldSuggestWeeks(int weeksDone, int requiredWeeks, DateOnly? snoozedUntil, DateOnly today,
        bool archived) =>
        !archived
        && (snoozedUntil is not { } until || today >= until)
        && weeksDone >= requiredWeeks;
}
