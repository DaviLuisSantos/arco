using Arco.Api.Domain;

namespace Arco.Api.Tests;

public class StreakRulesTests
{
    // 2026-03-02 is a Monday.
    private static readonly DateOnly Monday = new(2026, 3, 2);

    private const CheckInStatus D = CheckInStatus.Done;
    private const CheckInStatus M = CheckInStatus.Minimum;
    private const CheckInStatus X = CheckInStatus.Missed;

    /// <summary>One entry per day from <paramref name="start"/>; null = no check-in.</summary>
    private static List<(DateOnly, CheckInStatus)> Seq(DateOnly start, params CheckInStatus?[] days) =>
        days.Select((s, i) => (start.AddDays(i), s))
            .Where(x => x.s is not null)
            .Select(x => (x.Item1, x.s!.Value))
            .ToList();

    private static StreakRules.Streak Daily(DateOnly today, params CheckInStatus?[] days) =>
        StreakRules.Compute(FrequencyType.Daily, [], null, Seq(Monday, days), Monday, today);

    [Fact]
    public void Done_and_minimum_both_count()
    {
        var s = Daily(Monday.AddDays(3), D, M, D, M);
        Assert.Equal(new StreakRules.Streak(4, 4, StreakUnit.Days), s);
    }

    [Fact]
    public void Unmarked_today_is_pending_but_a_missed_today_is_a_miss()
    {
        Assert.Equal(3, Daily(Monday.AddDays(3), D, D, D, null).Current);
        // The first miss of the week is forgiven: it keeps the streak but does not add.
        Assert.Equal(3, Daily(Monday.AddDays(3), D, D, D, X).Current);
    }

    [Fact]
    public void One_isolated_miss_per_week_is_forgiven()
    {
        // Week 1: one miss. Week 2: one miss. Never two in a row.
        var s = Daily(Monday.AddDays(13), D, D, X, D, D, D, D, D, D, D, D, X, D, D);
        Assert.Equal(12, s.Current);
        Assert.Equal(12, s.Best);
    }

    [Fact]
    public void Second_miss_in_the_same_week_resets()
    {
        var s = Daily(Monday.AddDays(6), D, X, D, D, X, D, D);
        Assert.Equal(2, s.Current);
        Assert.Equal(3, s.Best);
    }

    [Fact]
    public void Two_misses_in_a_row_reset_even_across_weeks()
    {
        // Sunday miss (week 1) then Monday miss (week 2).
        var s = Daily(Monday.AddDays(8), D, D, D, D, D, D, X, null, D);
        Assert.Equal(1, s.Current);
        Assert.Equal(6, s.Best);
    }

    [Fact]
    public void Days_without_check_in_are_misses()
    {
        var s = Daily(Monday.AddDays(4), D, D, null, null, D);
        Assert.Equal(1, s.Current);
        Assert.Equal(2, s.Best);
    }

    [Fact]
    public void Specific_days_only_count_due_days()
    {
        // Mon/Wed/Fri; Tuesday and Thursday have no check-in and do not matter.
        var checkIns = Seq(Monday, D, null, D, null, D);
        var s = StreakRules.Compute(FrequencyType.SpecificDays, [1, 3, 5], null, checkIns, Monday, Monday.AddDays(4));
        Assert.Equal(3, s.Current);
    }

    [Fact]
    public void Once_a_week_habit_breaks_after_two_missed_weeks()
    {
        // Sundays only: hit, miss (forgiven), miss (in a row) → reset.
        var sundays = new[] { Monday.AddDays(6), Monday.AddDays(13), Monday.AddDays(20) };
        var hitThenForgiven = StreakRules.Compute(FrequencyType.SpecificDays, [0], null,
            [(sundays[0], D), (sundays[1], X)], Monday, sundays[1]);
        Assert.Equal(1, hitThenForgiven.Current);
        var reset = StreakRules.Compute(FrequencyType.SpecificDays, [0], null,
            [(sundays[0], D), (sundays[1], X), (sundays[2], X)], Monday, sundays[2]);
        Assert.Equal(0, reset.Current);
        Assert.Equal(1, reset.Best);
    }

    [Fact]
    public void Weekly_target_counts_weeks_and_forgives_one_short_once()
    {
        // 3x/week. Week 1: 3 (hit). Week 2: 2 (one short, forgiven). Week 3: 3 (hit). Week 4 (current): 1 (pending).
        var checkIns = Seq(Monday, D, D, D, null, null, null, null,
            D, M, null, null, null, null, null,
            D, D, D, null, null, null, null,
            D);
        var s = StreakRules.Compute(FrequencyType.WeeklyTarget, [], 3, checkIns, Monday, Monday.AddDays(21));
        Assert.Equal(new StreakRules.Streak(2, 2, StreakUnit.Weeks), s);
    }

    [Fact]
    public void Weekly_target_resets_when_far_short_or_short_twice_in_a_row()
    {
        var farShort = Seq(Monday, D, D, D, null, null, null, null, D);
        Assert.Equal(0, StreakRules.Compute(FrequencyType.WeeklyTarget, [], 3, farShort, Monday, Monday.AddDays(14)).Current);

        var shortTwice = Seq(Monday, D, D, D, null, null, null, null, D, D, null, null, null, null, null, D, D);
        Assert.Equal(0, StreakRules.Compute(FrequencyType.WeeklyTarget, [], 3, shortTwice, Monday, Monday.AddDays(21)).Current);
    }

    [Fact]
    public void Weekly_target_counts_current_week_once_hit_and_skips_a_partial_first_week()
    {
        // Created on Thursday with only one check-in that week; the next week hits 2x.
        var thursday = Monday.AddDays(3);
        var checkIns = new List<(DateOnly, CheckInStatus)> { (thursday, D), (Monday.AddDays(7), D), (Monday.AddDays(8), D) };
        var s = StreakRules.Compute(FrequencyType.WeeklyTarget, [], 2, checkIns, thursday, Monday.AddDays(8));
        Assert.Equal(1, s.Current);
    }
}
