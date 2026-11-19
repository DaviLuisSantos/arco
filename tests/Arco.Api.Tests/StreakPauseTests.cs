using Arco.Api.Domain;

namespace Arco.Api.Tests;

public class StreakPauseTests
{
    // 2026-03-02 is a Monday.
    private static readonly DateOnly Monday = new(2026, 3, 2);

    private static List<(DateOnly, CheckInStatus)> Done(DateOnly from, int days, CheckInStatus status = CheckInStatus.Done) =>
        Enumerable.Range(0, days).Select(i => (from.AddDays(i), status)).ToList();

    private static StreakRules.Streak Daily(List<(DateOnly, CheckInStatus)> checkIns, DateOnly today,
        params (DateOnly Start, DateOnly? End)[] pauses) =>
        StreakRules.Compute(FrequencyType.Daily, [], null, checkIns, Monday, today, PauseCalendar.From(pauses, checkIns));

    [Fact]
    public void Ten_day_vacation_in_the_middle_keeps_the_daily_streak()
    {
        var checkIns = Done(Monday, 5);                       // days 0..4
        checkIns.AddRange(Done(Monday.AddDays(15), 3));       // days 15..17
        var vacation = (Monday.AddDays(5), (DateOnly?)Monday.AddDays(14)); // 10 days
        var s = Daily(checkIns, Monday.AddDays(17), vacation);
        Assert.Equal(new StreakRules.Streak(8, 8, StreakUnit.Days), s);

        // Without the pause the streak would have broken.
        Assert.True(Daily(checkIns, Monday.AddDays(17)).Current < 8);
    }

    [Fact]
    public void Skipped_does_not_add_nor_break()
    {
        var checkIns = new List<(DateOnly, CheckInStatus)>
        {
            (Monday, CheckInStatus.Done),
            (Monday.AddDays(1), CheckInStatus.Skipped),
            (Monday.AddDays(2), CheckInStatus.Done),
        };
        Assert.Equal(new StreakRules.Streak(2, 2, StreakUnit.Days), Daily(checkIns, Monday.AddDays(2)));

        // Also neutral when the caller passes no calendar (old overload).
        var legacy = StreakRules.Compute(FrequencyType.Daily, [], null, checkIns, Monday, Monday.AddDays(2));
        Assert.Equal(new StreakRules.Streak(2, 2, StreakUnit.Days), legacy);
    }

    [Fact]
    public void Done_on_a_paused_day_still_counts()
    {
        var checkIns = Done(Monday, 3);
        var s = Daily(checkIns, Monday.AddDays(2), (Monday.AddDays(1), Monday.AddDays(1)));
        Assert.Equal(3, s.Current);
    }

    [Fact]
    public void Open_ended_pause_up_to_today_keeps_the_streak()
    {
        var checkIns = Done(Monday, 3);
        var s = Daily(checkIns, Monday.AddDays(9), (Monday.AddDays(3), null));
        Assert.Equal(new StreakRules.Streak(3, 3, StreakUnit.Days), s);
    }

    [Fact]
    public void Weekly_target_fully_paused_week_is_neutral()
    {
        // Weeks: Mon 03-02 (3 done), 03-09 (paused), 03-16 (3 done), today 03-22.
        var checkIns = Done(Monday, 3);
        checkIns.AddRange(Done(Monday.AddDays(14), 3));
        var pauses = new[] { (Monday.AddDays(7), (DateOnly?)Monday.AddDays(13)) };
        var s = StreakRules.Compute(FrequencyType.WeeklyTarget, [], 3, checkIns, Monday, Monday.AddDays(20),
            PauseCalendar.From(pauses, checkIns));
        Assert.Equal(new StreakRules.Streak(2, 2, StreakUnit.Weeks), s);
    }

    [Fact]
    public void Weekly_target_is_prorated_in_partially_paused_week()
    {
        // Target 4, 3 days paused -> ceil(4 * 4 / 7) = 3. Two done days would break; three done hit.
        var week2 = Monday.AddDays(7);
        var pauses = new[] { (week2.AddDays(4), (DateOnly?)week2.AddDays(6)) };
        var checkIns = Done(Monday, 4);
        checkIns.AddRange(Done(week2, 3));
        var s = StreakRules.Compute(FrequencyType.WeeklyTarget, [], 4, checkIns, Monday, Monday.AddDays(14),
            PauseCalendar.From(pauses, checkIns));
        Assert.Equal(2, s.Current);
    }

    [Fact]
    public void Weekly_target_skipped_days_lower_the_target()
    {
        // Target 7 with one Skipped day -> 6 needed.
        var week2 = Monday.AddDays(7);
        var checkIns = Done(Monday, 7);
        checkIns.AddRange(Done(week2, 6));
        checkIns.Add((week2.AddDays(6), CheckInStatus.Skipped));
        var s = StreakRules.Compute(FrequencyType.WeeklyTarget, [], 7, checkIns, Monday, Monday.AddDays(14),
            PauseCalendar.From([], checkIns));
        Assert.Equal(2, s.Current);
    }
}
