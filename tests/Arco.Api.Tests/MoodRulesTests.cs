using Arco.Api.Domain;

namespace Arco.Api.Tests;

public class MoodRulesTests
{
    // Monday 2030-03-04 .. Sunday 2030-03-10.
    private static readonly DateOnly Mon = new(2030, 3, 4);

    private static MoodRules.HabitDays Habit(
        FrequencyType type, int[]? days = null, PauseCalendar? calendar = null,
        params (DateOnly Date, CheckInStatus Status)[] checkIns) =>
        new(type, days ?? [], calendar ?? PauseCalendar.None,
            checkIns.ToDictionary(c => c.Date, c => c.Status), Mon.AddDays(-30));

    [Fact]
    public void Good_and_bad_thresholds()
    {
        Assert.True(MoodRules.IsGood(4));
        Assert.True(MoodRules.IsGood(5));
        Assert.False(MoodRules.IsGood(3));
        Assert.True(MoodRules.IsBad(2));
        Assert.True(MoodRules.IsBad(1));
        Assert.False(MoodRules.IsBad(3));
    }

    [Fact]
    public void No_moods_gives_zeros_and_nulls()
    {
        var s = MoodRules.Summarize(new Dictionary<DateOnly, int>(), [Habit(FrequencyType.Daily)]);
        Assert.Equal(new MoodRules.MoodSummary(0, null, 0, 0, null, null), s);
    }

    [Fact]
    public void Average_has_one_decimal_and_groups_are_counted()
    {
        var moods = new Dictionary<DateOnly, int> { [Mon] = 5, [Mon.AddDays(1)] = 4, [Mon.AddDays(2)] = 3, [Mon.AddDays(3)] = 1 };
        var s = MoodRules.Summarize(moods, []);
        Assert.Equal(4, s.Days);
        Assert.Equal(3.3, s.Average);
        Assert.Equal(2, s.GoodDays);
        Assert.Equal(1, s.BadDays);
        Assert.Null(s.GoodConsistency); // no habits = nothing expected
        Assert.Null(s.BadConsistency);
    }

    [Fact]
    public void Consistency_compares_good_and_bad_days()
    {
        var good1 = Mon; var good2 = Mon.AddDays(1); var bad1 = Mon.AddDays(2); var bad2 = Mon.AddDays(3);
        var moods = new Dictionary<DateOnly, int> { [good1] = 5, [good2] = 4, [bad1] = 2, [bad2] = 1 };
        var a = Habit(FrequencyType.Daily, null, null,
            (good1, CheckInStatus.Done), (good2, CheckInStatus.Minimum), (bad1, CheckInStatus.Done), (bad2, CheckInStatus.Missed));
        var b = Habit(FrequencyType.Daily, null, null,
            (good1, CheckInStatus.Done), (good2, CheckInStatus.Done));

        var s = MoodRules.Summarize(moods, [a, b]);
        Assert.Equal(1.0, s.GoodConsistency); // 4 of 4
        Assert.Equal(0.25, s.BadConsistency); // 1 of 4
    }

    [Fact]
    public void Paused_or_skipped_days_are_not_expected_but_a_check_in_still_counts()
    {
        var good = Mon; var bad = Mon.AddDays(1);
        var moods = new Dictionary<DateOnly, int> { [good] = 5, [bad] = 1 };
        var paused = Habit(FrequencyType.Daily,
            calendar: PauseCalendar.From([(good, good)], []));
        var skipped = Habit(FrequencyType.Daily,
            calendar: PauseCalendar.From([], [(bad, CheckInStatus.Skipped)]),
            checkIns: (bad, CheckInStatus.Skipped));
        var pausedButDone = Habit(FrequencyType.Daily,
            calendar: PauseCalendar.From([(good, good)], [(good, CheckInStatus.Done)]),
            checkIns: (good, CheckInStatus.Done));

        var s = MoodRules.Summarize(moods, [paused, skipped, pausedButDone]);
        Assert.Equal(0.5, s.GoodConsistency); // paused habit excused; skipped habit 0 of 1; done-while-paused 1 of 1
        Assert.Equal(0.0, s.BadConsistency);  // skipped habit excused; paused and third habit expected, none done (0 of 2)
    }

    [Fact]
    public void Specific_days_only_count_on_due_days_and_weekly_target_is_left_out()
    {
        var mon = Mon; var tue = Mon.AddDays(1);
        var moods = new Dictionary<DateOnly, int> { [mon] = 5, [tue] = 5 };
        var specific = Habit(FrequencyType.SpecificDays, [1], null, (mon, CheckInStatus.Done)); // Mondays only
        var weekly = Habit(FrequencyType.WeeklyTarget, null, null);

        var s = MoodRules.Summarize(moods, [specific, weekly]);
        Assert.Equal(1.0, s.GoodConsistency); // Monday only, done

        var onlyWeekly = MoodRules.Summarize(moods, [weekly]);
        Assert.Null(onlyWeekly.GoodConsistency);
    }

    [Fact]
    public void Days_before_the_habit_started_are_not_expected()
    {
        var moods = new Dictionary<DateOnly, int> { [Mon] = 5 };
        var future = new MoodRules.HabitDays(FrequencyType.Daily, [], PauseCalendar.None,
            new Dictionary<DateOnly, CheckInStatus>(), Mon.AddDays(1));
        Assert.Null(MoodRules.Summarize(moods, [future]).GoodConsistency);
    }
}
