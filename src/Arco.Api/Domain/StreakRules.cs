namespace Arco.Api.Domain;

public enum StreakUnit
{
    Days,
    Weeks,
}

/// <summary>
/// Habit streak with tolerance, computed from the check-in history (never stored).
/// Daily/SpecificDays count due days with Done or Minimum; WeeklyTarget counts weeks that hit the target.
/// One isolated miss per week is forgiven (it keeps the streak but does not add to it); two misses in a row,
/// or two in the same Monday–Sunday week, reset it. The current frequency applies to the whole history.
/// </summary>
public static class StreakRules
{
    public sealed record Streak(int Current, int Best, StreakUnit Unit);

    /// <param name="start">First local date the habit existed (creation date or its earliest check-in).</param>
    /// <param name="today">A due "today" without a check-in (or an unfinished week) is pending, not a miss.</param>
    public static Streak Compute(FrequencyType type, IReadOnlyCollection<int> frequencyDays, int? timesPerWeek,
        IEnumerable<(DateOnly Date, CheckInStatus Status)> checkIns, DateOnly start, DateOnly today)
    {
        var byDate = new Dictionary<DateOnly, CheckInStatus>();
        foreach (var (date, status) in checkIns)
            if (date >= start && date <= today) byDate[date] = status;

        return type == FrequencyType.WeeklyTarget
            ? Weeks(timesPerWeek ?? 1, byDate, start, today)
            : Days(type, frequencyDays, byDate, start, today);
    }

    private static Streak Days(FrequencyType type, IReadOnlyCollection<int> frequencyDays,
        Dictionary<DateOnly, CheckInStatus> byDate, DateOnly start, DateOnly today)
    {
        var tracker = new Tracker();
        for (var d = start; d <= today; d = d.AddDays(1))
        {
            if (type == FrequencyType.SpecificDays && !frequencyDays.Contains((int)d.DayOfWeek)) continue;
            var hasStatus = byDate.TryGetValue(d, out var status);
            if (d == today && !hasStatus) break;
            if (hasStatus && HabitSchedule.CountsTowardWeek(status)) tracker.Hit();
            else tracker.Miss(HabitSchedule.WeekStart(d), forgivable: true);
        }
        return new Streak(tracker.Current, tracker.Best, StreakUnit.Days);
    }

    private static Streak Weeks(int target, Dictionary<DateOnly, CheckInStatus> byDate, DateOnly start, DateOnly today)
    {
        var tracker = new Tracker();
        var firstWeek = HabitSchedule.WeekStart(start);
        var currentWeek = HabitSchedule.WeekStart(today);
        for (var week = firstWeek; week <= currentWeek; week = week.AddDays(7))
        {
            var count = 0;
            for (var d = week; d < week.AddDays(7); d = d.AddDays(1))
                if (byDate.TryGetValue(d, out var s) && HabitSchedule.CountsTowardWeek(s)) count++;

            if (count >= target) tracker.Hit();
            // The unfinished current week and the partial first week never count as a miss.
            else if (week == currentWeek || week == firstWeek) continue;
            // A week one short of the target is the forgivable miss (a target of 1 has no tolerance).
            else tracker.Miss(week, forgivable: target >= 2 && count >= target - 1);
        }
        return new Streak(tracker.Current, tracker.Best, StreakUnit.Weeks);
    }

    private sealed class Tracker
    {
        private bool _lastMissed;
        private DateOnly? _forgivenWeek;

        public int Current { get; private set; }
        public int Best { get; private set; }

        public void Hit()
        {
            Current++;
            Best = Math.Max(Best, Current);
            _lastMissed = false;
        }

        public void Miss(DateOnly week, bool forgivable)
        {
            if (forgivable && !_lastMissed && _forgivenWeek != week) _forgivenWeek = week;
            else Current = 0;
            _lastMissed = true;
        }
    }
}
