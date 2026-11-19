namespace Arco.Api.Domain;

/// <summary>
/// Which days of one habit are "excused" (paused, or checked in as Skipped): they are neutral —
/// they never break a streak nor count as a failure. A Done/Minimum check-in on the day wins and counts normally.
/// </summary>
public sealed class PauseCalendar
{
    public static readonly PauseCalendar None = new([], [], []);

    private readonly (DateOnly Start, DateOnly? End)[] _pauses;
    private readonly HashSet<DateOnly> _skipped;
    private readonly HashSet<DateOnly> _succeeded;

    private PauseCalendar((DateOnly Start, DateOnly? End)[] pauses, HashSet<DateOnly> skipped, HashSet<DateOnly> succeeded)
    {
        _pauses = pauses;
        _skipped = skipped;
        _succeeded = succeeded;
    }

    public static PauseCalendar From(
        IEnumerable<(DateOnly Start, DateOnly? End)> pauses,
        IEnumerable<(DateOnly Date, CheckInStatus Status)> checkIns)
    {
        var p = pauses.ToArray();
        var list = checkIns as ICollection<(DateOnly Date, CheckInStatus Status)> ?? checkIns.ToList();
        var skipped = list.Where(c => c.Status == CheckInStatus.Skipped).Select(c => c.Date).ToHashSet();
        var succeeded = list.Where(c => HabitSchedule.CountsTowardWeek(c.Status)).Select(c => c.Date).ToHashSet();
        return p.Length == 0 && skipped.Count == 0 ? None : new PauseCalendar(p, skipped, succeeded);
    }

    public bool IsPaused(DateOnly day) => _pauses.Any(p => p.Start <= day && (p.End is null || day <= p.End));

    /// <summary>Paused or Skipped, with no Done/Minimum check-in on that day.</summary>
    public bool IsExcused(DateOnly day) =>
        !_succeeded.Contains(day) && (_skipped.Contains(day) || IsPaused(day));

    /// <summary>Excused days within the week starting on <paramref name="monday"/> (0..7).</summary>
    public int ExcusedInWeek(DateOnly monday)
    {
        var n = 0;
        for (var i = 0; i < 7; i++) if (IsExcused(monday.AddDays(i))) n++;
        return n;
    }

    /// <summary>Excused days within [from, to] (inclusive) that are also due days.</summary>
    public int ExcusedInRange(DateOnly from, DateOnly to, Func<DateOnly, bool> isDueDay)
    {
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1)) if (isDueDay(d) && IsExcused(d)) n++;
        return n;
    }

    /// <summary>End date of the pause covering <paramref name="today"/>; null when open-ended or not paused (use <see cref="IsPaused"/>).</summary>
    public DateOnly? PausedUntil(DateOnly today)
    {
        DateOnly? best = null;
        foreach (var p in _pauses)
        {
            if (p.Start > today || p.End is null || p.End < today) continue;
            if (best is null || p.End > best) best = p.End;
        }
        return IsPaused(today) && _pauses.Any(p => p.Start <= today && p.End is null) ? null : best;
    }
}
