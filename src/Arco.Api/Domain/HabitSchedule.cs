namespace Arco.Api.Domain;

public static class HabitSchedule
{
    /// <summary>Monday of the week containing <paramref name="date"/>.</summary>
    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>Sunday of the week containing <paramref name="date"/>.</summary>
    public static DateOnly WeekEnd(DateOnly date) => WeekStart(date).AddDays(6);

    public static bool CountsTowardWeek(CheckInStatus status) =>
        status is CheckInStatus.Done or CheckInStatus.Minimum;

    /// <summary>Done/Minimum check-ins within the Monday–Sunday week containing <paramref name="today"/>.</summary>
    public static int WeekCount(IEnumerable<(DateOnly Date, CheckInStatus Status)> checkIns, DateOnly today)
    {
        var start = WeekStart(today);
        var end = start.AddDays(6);
        return checkIns.Count(c => c.Date >= start && c.Date <= end && CountsTowardWeek(c.Status));
    }

    /// <summary>
    /// Daily: always. SpecificDays: today's weekday (0 = Sunday) is in the list.
    /// WeeklyTarget: weekCount &lt; timesPerWeek, not counting today's own check-in,
    /// so a habit checked in today stays "due today" instead of leaving the list.
    /// </summary>
    public static bool IsDueToday(
        FrequencyType type,
        IReadOnlyCollection<int> frequencyDays,
        int? timesPerWeek,
        DateOnly today,
        int weekCount,
        CheckInStatus? todayStatus = null) => type switch
    {
        FrequencyType.Daily => true,
        FrequencyType.SpecificDays => frequencyDays.Contains((int)today.DayOfWeek),
        FrequencyType.WeeklyTarget =>
            weekCount - (todayStatus is { } s && CountsTowardWeek(s) ? 1 : 0) < (timesPerWeek ?? 1),
        _ => false,
    };
}
