using Arco.Api.Domain;

namespace Arco.Api.Tests;

public class XpRulesTests
{
    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 12)]
    [InlineData(3, 14)]
    [InlineData(6, 20)]
    public void Done_is_10_plus_2_per_extra_level(int level, int expected) =>
        Assert.Equal(expected, XpRules.ForCheckIn(CheckInStatus.Done, level));

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 6)]
    [InlineData(4, 8)] // Done = 16 → 8
    [InlineData(3, 7)] // Done = 14 → 7
    public void Minimum_is_half_of_done_rounded_down(int level, int expected) =>
        Assert.Equal(expected, XpRules.ForCheckIn(CheckInStatus.Minimum, level));

    [Fact]
    public void Missed_gives_zero() => Assert.Equal(0, XpRules.ForCheckIn(CheckInStatus.Missed, 5));

    [Fact]
    public void Fixed_amounts_match_product_table()
    {
        Assert.Equal(50, XpRules.HabitLevelUp);
        Assert.Equal(25, XpRules.MilestoneDone);
        Assert.Equal(150, XpRules.GoalCompleted);
        Assert.Equal(100, XpRules.CycleCompleted);
    }
}

public class PlayerLevelTests
{
    [Theory]
    [InlineData(0, 1, 0, 100)]
    [InlineData(99, 1, 99, 100)]
    [InlineData(100, 2, 0, 150)]
    [InlineData(249, 2, 149, 150)]
    [InlineData(250, 3, 0, 200)]
    [InlineData(450, 4, 0, 250)]
    [InlineData(500, 4, 50, 250)]
    public void Level_math(int totalXp, int level, int into, int forNext)
    {
        var p = PlayerLevel.FromTotalXp(totalXp);
        Assert.Equal(level, p.Level);
        Assert.Equal(totalXp, p.TotalXp);
        Assert.Equal(into, p.XpIntoLevel);
        Assert.Equal(forNext, p.XpForNextLevel);
    }

    [Fact]
    public void Cost_sequence_is_100_150_200()
    {
        Assert.Equal(100, PlayerLevel.CostToAdvance(1));
        Assert.Equal(150, PlayerLevel.CostToAdvance(2));
        Assert.Equal(200, PlayerLevel.CostToAdvance(3));
    }
}

public class HabitScheduleTests
{
    private static readonly DateOnly Sunday = new(2026, 10, 18);
    private static readonly DateOnly Monday = new(2026, 10, 19);
    private static readonly DateOnly Wednesday = new(2026, 10, 21);

    [Fact]
    public void Daily_is_always_due() =>
        Assert.True(HabitSchedule.IsDueToday(FrequencyType.Daily, [], null, Wednesday, 7));

    [Fact]
    public void SpecificDays_uses_0_for_sunday()
    {
        Assert.Equal(DayOfWeek.Sunday, Sunday.DayOfWeek);
        Assert.True(HabitSchedule.IsDueToday(FrequencyType.SpecificDays, [0], null, Sunday, 0));
        Assert.False(HabitSchedule.IsDueToday(FrequencyType.SpecificDays, [0], null, Monday, 0));
        Assert.True(HabitSchedule.IsDueToday(FrequencyType.SpecificDays, [1, 3, 5], null, Wednesday, 0));
        Assert.False(HabitSchedule.IsDueToday(FrequencyType.SpecificDays, [1, 5], null, Wednesday, 0));
    }

    [Fact]
    public void WeeklyTarget_due_while_week_count_below_target()
    {
        Assert.True(HabitSchedule.IsDueToday(FrequencyType.WeeklyTarget, [], 3, Wednesday, 2));
        Assert.False(HabitSchedule.IsDueToday(FrequencyType.WeeklyTarget, [], 3, Wednesday, 3));
    }

    [Fact]
    public void WeeklyTarget_ignores_todays_own_checkin()
    {
        // Count reached 3 thanks to today's check-in: still shown as due today.
        Assert.True(HabitSchedule.IsDueToday(FrequencyType.WeeklyTarget, [], 3, Wednesday, 3, CheckInStatus.Done));
        Assert.False(HabitSchedule.IsDueToday(FrequencyType.WeeklyTarget, [], 3, Wednesday, 3, CheckInStatus.Missed));
    }

    [Fact]
    public void Week_starts_on_monday()
    {
        Assert.Equal(Monday, HabitSchedule.WeekStart(Wednesday));
        Assert.Equal(Monday, HabitSchedule.WeekStart(Monday));
        Assert.Equal(new DateOnly(2026, 10, 12), HabitSchedule.WeekStart(Sunday));
        Assert.Equal(Sunday, HabitSchedule.WeekEnd(Sunday));
    }

    [Fact]
    public void Week_count_counts_done_and_minimum_in_monday_to_sunday_week()
    {
        var checkIns = new (DateOnly, CheckInStatus)[]
        {
            (Sunday, CheckInStatus.Done),                 // previous week
            (Monday, CheckInStatus.Done),
            (Monday.AddDays(1), CheckInStatus.Minimum),
            (Wednesday, CheckInStatus.Missed),            // doesn't count
            (Monday.AddDays(6), CheckInStatus.Done),      // Sunday of this week
            (Monday.AddDays(7), CheckInStatus.Done),      // next week
        };
        Assert.Equal(3, HabitSchedule.WeekCount(checkIns, Wednesday));
        Assert.Equal(1, HabitSchedule.WeekCount(checkIns, Sunday));
    }
}

public class GoalProgressTests
{
    [Theory]
    [InlineData(3.0, 10.0, null, 0.0)]
    [InlineData(3.0, 10.0, 3.0, 0.0)]
    [InlineData(0.0, 10.0, 5.0, 0.5)]
    [InlineData(0.0, 10.0, 10.0, 1.0)]
    [InlineData(0.0, 10.0, 15.0, 1.0)]   // clamped
    [InlineData(5.0, 10.0, 2.0, 0.0)]    // clamped below
    [InlineData(90.0, 80.0, 85.0, 0.5)] // decreasing target
    [InlineData(90.0, 80.0, 78.0, 1.0)]
    [InlineData(90.0, 80.0, 95.0, 0.0)]
    public void Numeric_progress(double start, double target, double? current, double expected) =>
        Assert.Equal(expected, GoalProgress.Numeric(start, target, current), 6);

    [Fact]
    public void Milestones_progress()
    {
        Assert.Equal(0, GoalProgress.Milestones(0, 0));
        Assert.Equal(0.5, GoalProgress.Milestones(2, 4));
        Assert.Equal(1, GoalProgress.Milestones(4, 4));
    }

    [Fact]
    public void Target_reached_handles_both_directions()
    {
        Assert.True(GoalProgress.IsTargetReached(3, 10, 10));
        Assert.False(GoalProgress.IsTargetReached(3, 10, 9.9));
        Assert.True(GoalProgress.IsTargetReached(90, 80, 80));
        Assert.False(GoalProgress.IsTargetReached(90, 80, 81));
    }
}

public class CycleMathTests
{
    [Fact]
    public void End_date_and_day_number()
    {
        var start = new DateOnly(2026, 10, 1);
        var end = CycleMath.EndDate(start, 6);
        Assert.Equal(new DateOnly(2026, 11, 11), end);
        Assert.Equal(42, CycleMath.TotalDays(start, end));
        Assert.Equal(1, CycleMath.DayNumber(start, end, start));
        Assert.Equal(1, CycleMath.DayNumber(start, end, start.AddDays(-3)));
        Assert.Equal(21, CycleMath.DayNumber(start, end, start.AddDays(20)));
        Assert.Equal(42, CycleMath.DayNumber(start, end, end.AddDays(10)));
    }
}

public class TimeZonesTests
{
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void Today_uses_configured_timezone()
    {
        // 02:00 UTC on Oct 22 is still Oct 21 in São Paulo (UTC−3).
        var time = new FixedTime(new DateTimeOffset(2026, 10, 22, 2, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2026, 10, 21), TimeZones.Today(time, "America/Sao_Paulo"));
        Assert.Equal(new DateOnly(2026, 10, 22), TimeZones.Today(time, "Europe/Lisbon"));
        Assert.Equal(new DateOnly(2026, 10, 21), TimeZones.Today(time, "Not/AZone")); // fallback
    }

    [Fact]
    public void Validates_iana_ids()
    {
        Assert.True(TimeZones.IsValidIana("America/Sao_Paulo"));
        Assert.False(TimeZones.IsValidIana("Not/AZone"));
        Assert.False(TimeZones.IsValidIana(""));
    }
}
