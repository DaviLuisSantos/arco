using Arco.Api.Domain;

namespace Arco.Api.Tests;

public class CleanMilestonesTests
{
    // 2026-03-02 is a Monday.
    private static readonly DateOnly Monday = new(2026, 3, 2);

    [Theory]
    [InlineData(1, 7)]
    [InlineData(2, 14)]
    [InlineData(3, 30)]
    [InlineData(4, 60)]
    [InlineData(5, 90)]
    [InlineData(6, 180)]
    [InlineData(7, 365)]
    public void Each_level_has_its_milestone(int level, int days) => Assert.Equal(days, CleanMilestones.ForLevel(level));

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(20)]
    public void There_is_no_milestone_outside_the_ladder(int level) => Assert.Null(CleanMilestones.ForLevel(level));

    [Theory]
    [InlineData(6, 1, false)]
    [InlineData(7, 1, true)]
    [InlineData(40, 1, true)]
    [InlineData(13, 2, false)]
    [InlineData(14, 2, true)]
    [InlineData(29, 3, false)]
    [InlineData(30, 3, true)]
    [InlineData(364, 7, false)]
    [InlineData(365, 7, true)]
    [InlineData(1000, 8, false)]
    public void Suggests_the_next_level_from_the_milestone_on(int cleanDays, int level, bool expected) =>
        Assert.Equal(expected, CleanMilestones.ShouldSuggest(cleanDays, level));

    private static List<(DateOnly, CheckInStatus)> Days(DateOnly from, int count, CheckInStatus status = CheckInStatus.Done) =>
        Enumerable.Range(0, count).Select(i => (from.AddDays(i), status)).ToList();

    private static StreakRules.Streak Strict(List<(DateOnly, CheckInStatus)> checkIns, DateOnly today,
        params (DateOnly Start, DateOnly? End)[] pauses) =>
        StreakRules.Compute(FrequencyType.Daily, [], null, checkIns, Monday, today, PauseCalendar.From(pauses, checkIns),
            forgiveMisses: false);

    [Fact]
    public void A_relapse_resets_the_streak_without_tolerance()
    {
        var checkIns = Days(Monday, 5);
        checkIns.Add((Monday.AddDays(5), CheckInStatus.Missed));
        checkIns.AddRange(Days(Monday.AddDays(6), 2));
        var today = Monday.AddDays(7);

        Assert.Equal(new StreakRules.Streak(2, 5, StreakUnit.Days), Strict(checkIns, today));
        // With tolerance, the isolated miss would have been forgiven.
        var forgiving = StreakRules.Compute(FrequencyType.Daily, [], null, checkIns, Monday, today,
            PauseCalendar.From([], checkIns));
        Assert.Equal(7, forgiving.Current);
    }

    [Fact]
    public void Skipped_is_neutral_without_tolerance()
    {
        var checkIns = new List<(DateOnly, CheckInStatus)>
        {
            (Monday, CheckInStatus.Done),
            (Monday.AddDays(1), CheckInStatus.Skipped),
            (Monday.AddDays(2), CheckInStatus.Done),
        };
        Assert.Equal(new StreakRules.Streak(2, 2, StreakUnit.Days), Strict(checkIns, Monday.AddDays(2)));
    }

    [Fact]
    public void A_pause_is_neutral_without_tolerance()
    {
        var checkIns = Days(Monday, 3);
        checkIns.AddRange(Days(Monday.AddDays(6), 2));
        var pause = (Monday.AddDays(3), (DateOnly?)Monday.AddDays(5));

        Assert.Equal(new StreakRules.Streak(5, 5, StreakUnit.Days), Strict(checkIns, Monday.AddDays(7), pause));
    }

    [Fact]
    public void Today_without_a_check_in_is_pending_not_a_relapse()
    {
        var checkIns = Days(Monday, 4);
        Assert.Equal(new StreakRules.Streak(4, 4, StreakUnit.Days), Strict(checkIns, Monday.AddDays(4)));
    }

    [Fact]
    public void Best_survives_a_relapse()
    {
        var checkIns = Days(Monday, 10);
        checkIns.Add((Monday.AddDays(10), CheckInStatus.Missed));
        Assert.Equal(new StreakRules.Streak(0, 10, StreakUnit.Days), Strict(checkIns, Monday.AddDays(10)));
    }
}
