namespace Arco.Api.Features.Reviews;

public sealed record HabitPeriodStatsDto(
    int HabitId,
    string Name,
    string? Emoji,
    int Level,
    int LevelsGained,
    int Done,
    int Minimum,
    int Missed,
    int Expected,
    double Rate,
    bool LevelUpSuggested);

public sealed record CheckInTotalsDto(int Done, int Minimum, int Missed);

/// <summary>Per-habit numbers for a date range, shared by the weekly and the cycle reviews.</summary>
public static class ReviewStats
{
    /// <summary>
    /// Habits that existed in [from, to] and are either active or have check-ins in the range.
    /// Rate = (Done + Minimum) / expected, limited to 0..1; expected uses the current frequency,
    /// counted from the later of <paramref name="from"/> and the habit's creation date.
    /// </summary>
    public static async Task<List<HabitPeriodStatsDto>> ForRangeAsync(
        AppDbContext db, DateOnly from, DateOnly to, DateOnly today, string timezone, CancellationToken ct)
    {
        var suggestionFrom = today.AddDays(-LevelUpSuggestion.WindowDays);
        var queryFrom = from < suggestionFrom ? from : suggestionFrom;
        var checkIns = (await db.CheckIns.AsNoTracking()
                .Where(c => c.Date >= queryFrom && c.Date <= (to > today ? to : today))
                .ToListAsync(ct))
            .ToLookup(c => c.HabitId);

        var habits = await db.Habits.AsNoTracking().Include(h => h.Levels).ToListAsync(ct);

        return habits
            .Select(h => (Habit: h, InRange: checkIns[h.Id].Where(c => c.Date >= from && c.Date <= to).ToList()))
            .Where(x => TimeZones.ToLocalDate(x.Habit.CreatedAt, timezone) <= to
                        && (x.Habit.ArchivedAt is null || x.InRange.Count > 0))
            .Select(x =>
            {
                var h = x.Habit;
                var created = TimeZones.ToLocalDate(h.CreatedAt, timezone);
                var start = created > from ? created : from;
                var expected = LevelUpSuggestion.ExpectedCount(h.FrequencyType, h.FrequencyDays, h.TimesPerWeek, start, to);
                var done = x.InRange.Count(c => c.Status == CheckInStatus.Done);
                var minimum = x.InRange.Count(c => c.Status == CheckInStatus.Minimum);
                var missed = x.InRange.Count(c => c.Status == CheckInStatus.Missed);
                var rate = expected == 0 ? 0 : Math.Min(1, (double)(done + minimum) / expected);
                var levelsGained = h.Levels.Count(l => l.Level > 1
                    && TimeZones.ToLocalDate(l.ReachedAt, timezone) is var d && d >= from && d <= to);
                var (_, suggested) = Queries.LevelUp(h, checkIns[h.Id], today, timezone);
                return new HabitPeriodStatsDto(h.Id, h.Name, h.Emoji, h.CurrentLevelNumber, levelsGained,
                    done, minimum, missed, expected, Math.Round(rate, 4), suggested);
            })
            .OrderByDescending(s => s.Rate)
            .ThenBy(s => s.Name, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }

    public static CheckInTotalsDto Totals(IEnumerable<HabitPeriodStatsDto> stats)
    {
        var list = stats.ToList();
        return new CheckInTotalsDto(list.Sum(s => s.Done), list.Sum(s => s.Minimum), list.Sum(s => s.Missed));
    }

    public static Task<int> XpInRangeAsync(AppDbContext db, DateOnly from, DateOnly to, CancellationToken ct) =>
        db.XpEvents.Where(e => e.Date >= from && e.Date <= to).SumAsync(e => e.Amount, ct);
}
