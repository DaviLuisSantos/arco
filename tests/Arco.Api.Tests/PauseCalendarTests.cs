using Arco.Api.Domain;
using Xunit;

namespace Arco.Api.Tests;

public class PauseCalendarTests
{
    private static readonly DateOnly Mon = new(2026, 6, 1); // segunda-feira

    private static PauseCalendar Cal(
        (DateOnly, DateOnly?)[]? pauses = null,
        (DateOnly, CheckInStatus)[]? checkIns = null) =>
        PauseCalendar.From(pauses ?? [], checkIns ?? []);

    [Fact]
    public void None_excuses_nothing()
    {
        Assert.False(PauseCalendar.None.IsPaused(Mon));
        Assert.False(PauseCalendar.None.IsExcused(Mon));
        Assert.Equal(0, PauseCalendar.None.ExcusedInWeek(Mon));
        Assert.Null(PauseCalendar.None.PausedUntil(Mon));
    }

    [Fact]
    public void Open_pause_covers_every_day_from_start()
    {
        var c = Cal(pauses: [(Mon, null)]);
        Assert.False(c.IsPaused(Mon.AddDays(-1)));
        Assert.True(c.IsPaused(Mon));
        Assert.True(c.IsPaused(Mon.AddDays(400)));
        Assert.True(c.IsExcused(Mon.AddDays(10)));
        Assert.Null(c.PausedUntil(Mon.AddDays(3)));
    }

    [Fact]
    public void Closed_pause_is_inclusive_on_both_ends()
    {
        var c = Cal(pauses: [(Mon, Mon.AddDays(2))]);
        Assert.False(c.IsPaused(Mon.AddDays(-1)));
        Assert.True(c.IsPaused(Mon));
        Assert.True(c.IsPaused(Mon.AddDays(2)));
        Assert.False(c.IsPaused(Mon.AddDays(3)));
        Assert.Equal(Mon.AddDays(2), c.PausedUntil(Mon.AddDays(1)));
        Assert.Null(c.PausedUntil(Mon.AddDays(3)));
    }

    [Fact]
    public void Skipped_day_is_excused_even_without_pause()
    {
        var c = Cal(checkIns: [(Mon, CheckInStatus.Skipped)]);
        Assert.False(c.IsPaused(Mon));
        Assert.True(c.IsExcused(Mon));
        Assert.False(c.IsExcused(Mon.AddDays(1)));
    }

    [Fact]
    public void Missed_day_is_not_excused()
    {
        var c = Cal(checkIns: [(Mon, CheckInStatus.Missed)]);
        Assert.False(c.IsExcused(Mon));
    }

    [Theory]
    [InlineData(CheckInStatus.Done)]
    [InlineData(CheckInStatus.Minimum)]
    public void Success_on_a_paused_or_skipped_day_is_not_excused(CheckInStatus status)
    {
        var c = Cal(pauses: [(Mon, Mon.AddDays(6))], checkIns: [(Mon, status), (Mon.AddDays(1), CheckInStatus.Skipped)]);
        Assert.True(c.IsPaused(Mon));
        Assert.False(c.IsExcused(Mon));
        Assert.True(c.IsExcused(Mon.AddDays(1)));
        Assert.Equal(6, c.ExcusedInWeek(Mon));
    }

    [Fact]
    public void ExcusedInWeek_counts_pause_and_skipped_without_double_counting()
    {
        var c = Cal(
            pauses: [(Mon.AddDays(2), Mon.AddDays(3))],
            checkIns: [(Mon.AddDays(3), CheckInStatus.Skipped), (Mon.AddDays(5), CheckInStatus.Skipped)]);
        Assert.Equal(3, c.ExcusedInWeek(Mon));
        Assert.Equal(0, c.ExcusedInWeek(Mon.AddDays(7)));
    }

    [Fact]
    public void ExcusedInWeek_is_seven_for_a_fully_paused_week()
    {
        var c = Cal(pauses: [(Mon.AddDays(-3), Mon.AddDays(20))]);
        Assert.Equal(7, c.ExcusedInWeek(Mon));
    }

    [Fact]
    public void ExcusedInRange_only_counts_due_days()
    {
        var c = Cal(pauses: [(Mon, Mon.AddDays(6))]);
        Assert.Equal(7, c.ExcusedInRange(Mon, Mon.AddDays(6), _ => true));
        Assert.Equal(5, c.ExcusedInRange(Mon, Mon.AddDays(6), d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)));
        Assert.Equal(2, c.ExcusedInRange(Mon.AddDays(5), Mon.AddDays(9), _ => true));
    }

    [Theory]
    [InlineData(3, 0, 3)]
    [InlineData(3, 1, 3)]  // ceil(3 * 6 / 7) = 3
    [InlineData(3, 3, 2)]  // ceil(3 * 4 / 7) = 2
    [InlineData(5, 2, 4)]  // ceil(5 * 5 / 7) = 4
    [InlineData(7, 6, 1)]
    [InlineData(3, 7, 0)]
    [InlineData(3, 9, 0)]
    public void AdjustedWeekTarget_scales_the_target(int target, int excused, int expected) =>
        Assert.Equal(expected, HabitSchedule.AdjustedWeekTarget(target, excused));

    [Fact]
    public void Skipped_gives_no_xp()
    {
        Assert.Equal(0, XpRules.ForCheckIn(CheckInStatus.Skipped, 5));
        Assert.False(HabitSchedule.CountsTowardWeek(CheckInStatus.Skipped));
    }
}
