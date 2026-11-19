using Arco.Api.Domain;

namespace Arco.Api.Tests;

/// <summary>Level up / level down suggestions with excused (paused or Skipped) days.</summary>
public class LevelSuggestionPauseTests
{
    private static DateOnly D(string s) => DateOnly.Parse(s);

    private static List<(DateOnly, CheckInStatus)> Days(DateOnly from, int count, CheckInStatus status) =>
        Enumerable.Range(0, count).Select(i => (from.AddDays(i), status)).ToList();

    private static PauseCalendar Calendar(
        IEnumerable<(DateOnly, DateOnly?)> pauses, IEnumerable<(DateOnly, CheckInStatus)> checkIns) =>
        PauseCalendar.From(pauses, checkIns);

    // ---- Level up (80% / 14 days) ----

    [Fact]
    public void LevelUp_14_days_with_5_paused_and_9_done_suggests_level_up()
    {
        var today = D("2026-03-15"); // Sunday; window 03-02..03-15
        var from = D("2026-03-02");
        // Paused 03-02..03-06 (5 days), Done 03-07..03-15 (9 days).
        var checkIns = Days(D("2026-03-07"), 9, CheckInStatus.Done);
        var calendar = Calendar([(from, D("2026-03-06"))], checkIns);

        var without = LevelUpSuggestion.Measure(FrequencyType.Daily, [], null, checkIns, today);
        Assert.False(LevelUpSuggestion.ShouldSuggest(without, from, null, today, false)); // 9/14 = 64%

        var with = LevelUpSuggestion.Measure(FrequencyType.Daily, [], null, checkIns, today, calendar);
        Assert.Equal(9, with.Expected);
        Assert.Equal(9, with.Done);
        Assert.True(LevelUpSuggestion.ShouldSuggest(with, from, null, today, false));
    }

    [Fact]
    public void LevelUp_fully_excused_window_has_nothing_expected_and_no_suggestion()
    {
        var today = D("2026-03-15");
        var calendar = Calendar([(D("2026-02-01"), null)], []);

        var c = LevelUpSuggestion.Measure(FrequencyType.Daily, [], null, [], today, calendar);

        Assert.Equal(0, c.Expected);
        Assert.False(LevelUpSuggestion.ShouldSuggest(c, D("2026-01-01"), null, today, false));
    }

    [Fact]
    public void LevelUp_expected_discounts_excused_days_per_frequency()
    {
        var from = D("2026-03-02"); // Monday
        var to = D("2026-03-15");
        // Skipped on Mon 03-02 and Wed 03-04; paused Fri 03-06 through Sun 03-08.
        var checkIns = new List<(DateOnly, CheckInStatus)>
        {
            (D("2026-03-02"), CheckInStatus.Skipped), (D("2026-03-04"), CheckInStatus.Skipped),
        };
        var calendar = Calendar([(D("2026-03-06"), D("2026-03-08"))], checkIns);

        Assert.Equal(14 - 5, LevelUpSuggestion.ExpectedCount(FrequencyType.Daily, [], null, from, to, calendar));
        // Mon/Wed/Fri over two weeks = 6; Mon, Wed and Fri (03-06) are excused → 3 left.
        Assert.Equal(3, LevelUpSuggestion.ExpectedCount(FrequencyType.SpecificDays, [1, 3, 5], null, from, to, calendar));
        // 3x/week: ceil(3 * 9 / 7) = 4.
        Assert.Equal(4, LevelUpSuggestion.ExpectedCount(FrequencyType.WeeklyTarget, [], 3, from, to, calendar));
    }

    [Fact]
    public void LevelUp_done_on_a_paused_day_counts_and_is_not_discounted()
    {
        var from = D("2026-03-02");
        var to = D("2026-03-15");
        var checkIns = new List<(DateOnly, CheckInStatus)> { (D("2026-03-03"), CheckInStatus.Done) };
        var calendar = Calendar([(D("2026-03-03"), D("2026-03-04"))], checkIns);

        // Only 03-04 is excused: 03-03 has a Done.
        Assert.Equal(13, LevelUpSuggestion.ExpectedCount(FrequencyType.Daily, [], null, from, to, calendar));
    }

    [Fact]
    public void LevelUp_without_calendar_behaves_as_before()
    {
        var from = D("2026-03-02");
        var to = D("2026-03-15");
        Assert.Equal(14, LevelUpSuggestion.ExpectedCount(FrequencyType.Daily, [], null, from, to, PauseCalendar.None));
        Assert.Equal(6, LevelUpSuggestion.ExpectedCount(FrequencyType.WeeklyTarget, [], 3, from, to));
    }

    // ---- Level up: consecutive weeks rule ----

    [Fact]
    public void QualifyingWeeks_neutral_week_is_skipped_without_breaking_or_adding()
    {
        var today = D("2026-03-22"); // Sunday
        var levelReachedOn = D("2026-02-23");
        var checkIns = Days(D("2026-03-02"), 4, CheckInStatus.Done)   // week of 03-02
            .Concat(Days(D("2026-03-16"), 4, CheckInStatus.Done))     // week of 03-16 (current)
            .ToList();
        var calendar = Calendar([(D("2026-03-09"), D("2026-03-15"))], checkIns); // week of 03-09 fully paused

        Assert.Equal(1, LevelUpSuggestion.QualifyingWeeks(checkIns, 4, levelReachedOn, today));
        Assert.Equal(2, LevelUpSuggestion.QualifyingWeeks(checkIns, 4, levelReachedOn, today, calendar));
    }

    [Fact]
    public void QualifyingWeeks_partial_week_uses_proportional_target()
    {
        var today = D("2026-03-22");
        var levelReachedOn = D("2026-03-02");
        // Week of 03-09: paused Mon..Fri (5 days) with 2 Done on the weekend → target 4 becomes ceil(4*2/7) = 2.
        var checkIns = Days(D("2026-03-02"), 4, CheckInStatus.Done)
            .Concat(Days(D("2026-03-14"), 2, CheckInStatus.Done)) // Sat + Sun of the paused week
            .Concat(Days(D("2026-03-16"), 4, CheckInStatus.Done))
            .ToList();
        var calendar = Calendar([(D("2026-03-09"), D("2026-03-13"))], checkIns);

        Assert.Equal(3, LevelUpSuggestion.QualifyingWeeks(checkIns, 4, levelReachedOn, today, calendar));
        Assert.Equal(1, LevelUpSuggestion.QualifyingWeeks(checkIns, 4, levelReachedOn, today));
    }

    // ---- Level down ----

    [Fact]
    public void LevelDown_two_weeks_of_vacation_do_not_suggest_going_back()
    {
        var today = D("2026-03-16"); // Monday; last finished weeks: 03-09 and 03-02
        var levelReachedOn = D("2026-03-02");

        // No pause and no check-ins: two weak weeks → suggests.
        Assert.True(LevelDownSuggestion.ShouldSuggest([], 7, 3, levelReachedOn, null, today, false));

        var calendar = Calendar([(D("2026-03-02"), D("2026-03-15"))], []);
        Assert.False(LevelDownSuggestion.ShouldSuggest([], 7, 3, levelReachedOn, null, today, false, calendar));
    }

    [Fact]
    public void LevelDown_neutral_weeks_are_ignored_and_the_last_non_neutral_weeks_decide()
    {
        var today = D("2026-03-30"); // last finished: 03-23 (vacation), 03-16 (vacation), 03-09, 03-02
        var levelReachedOn = D("2026-03-02");
        var calendar = Calendar([(D("2026-03-16"), D("2026-03-29"))], []);

        // 03-09 and 03-02 are weak (no check-ins) → suggests, skipping the 2 vacation weeks.
        Assert.True(LevelDownSuggestion.ShouldSuggest([], 7, 3, levelReachedOn, null, today, false, calendar));

        // A strong week (6 Done) before the vacation breaks it.
        var checkIns = Days(D("2026-03-09"), 6, CheckInStatus.Done);
        var calendarWithCheckIns = Calendar([(D("2026-03-16"), D("2026-03-29"))], checkIns);
        Assert.False(LevelDownSuggestion.ShouldSuggest(checkIns, 7, 3, levelReachedOn, null, today, false,
            calendarWithCheckIns));
    }

    [Fact]
    public void LevelDown_partial_week_uses_proportional_target()
    {
        var today = D("2026-03-16");
        var levelReachedOn = D("2026-03-02");
        // Target 4/week. Week 03-02 is paused Mon..Fri (5 excused) → target ceil(4*2/7) = 2.
        // 1 Done on Saturday is enough there (1*2 < 2 is false); week 03-09 has 0 days (weak).
        var checkIns = new List<(DateOnly, CheckInStatus)> { (D("2026-03-07"), CheckInStatus.Done) };
        var calendar = Calendar([(D("2026-03-02"), D("2026-03-06"))], checkIns);

        Assert.True(LevelDownSuggestion.ShouldSuggest(checkIns, 4, 3, levelReachedOn, null, today, false));
        Assert.False(LevelDownSuggestion.ShouldSuggest(checkIns, 4, 3, levelReachedOn, null, today, false, calendar));
    }
}
