using Arco.Api.Domain;
using Arco.Api.Infrastructure;

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

public class LevelUpWeeksRuleTests
{
    // 2026-03-02 is a Monday.
    private static readonly DateOnly Monday = new(2026, 3, 2);

    /// <summary>Check-ins on the first <paramref name="days"/> days (Mon…) of the week starting at <paramref name="week"/>.</summary>
    private static IEnumerable<(DateOnly, CheckInStatus)> Week(DateOnly week, int days, CheckInStatus status = CheckInStatus.Done) =>
        Enumerable.Range(0, days).Select(i => (week.AddDays(i), status));

    [Fact]
    public void Counts_consecutive_weeks_with_enough_days_and_minimum_counts()
    {
        var checkIns = Week(Monday, 5).Concat(Week(Monday.AddDays(7), 5, CheckInStatus.Minimum)).Concat(Week(Monday.AddDays(14), 6));
        // Today = Monday of week 4, nothing yet: the run ends at week 3.
        Assert.Equal(3, LevelUpSuggestion.QualifyingWeeks(checkIns, 5, Monday, Monday.AddDays(21)));
    }

    [Fact]
    public void A_short_week_breaks_the_run()
    {
        var checkIns = Week(Monday, 5).Concat(Week(Monday.AddDays(7), 4)).Concat(Week(Monday.AddDays(14), 5));
        Assert.Equal(1, LevelUpSuggestion.QualifyingWeeks(checkIns, 5, Monday, Monday.AddDays(21)));
    }

    [Fact]
    public void Current_week_counts_once_it_qualifies()
    {
        var checkIns = Week(Monday, 5).Concat(Week(Monday.AddDays(7), 5)).ToList();
        // Friday of week 2, with its 5th day done.
        Assert.Equal(2, LevelUpSuggestion.QualifyingWeeks(checkIns, 5, Monday, Monday.AddDays(11)));
        // Thursday of week 2: 4 days so far, still pending → only week 1.
        Assert.Equal(1, LevelUpSuggestion.QualifyingWeeks(checkIns, 5, Monday, Monday.AddDays(10)));
    }

    [Fact]
    public void Only_weeks_entirely_at_the_current_level_count()
    {
        var checkIns = Week(Monday, 5).Concat(Week(Monday.AddDays(7), 5)).Concat(Week(Monday.AddDays(14), 5));
        // Level reached on Tuesday of week 2: only week 3 counts.
        Assert.Equal(1, LevelUpSuggestion.QualifyingWeeks(checkIns, 5, Monday.AddDays(8), Monday.AddDays(21)));
    }

    [Fact]
    public void Suggests_when_weeks_are_reached_and_not_snoozed()
    {
        var today = Monday.AddDays(21);
        Assert.True(LevelUpSuggestion.ShouldSuggestWeeks(3, 3, null, today, archived: false));
        Assert.False(LevelUpSuggestion.ShouldSuggestWeeks(2, 3, null, today, archived: false));
        Assert.False(LevelUpSuggestion.ShouldSuggestWeeks(3, 3, today.AddDays(1), today, archived: false));
        Assert.False(LevelUpSuggestion.ShouldSuggestWeeks(3, 3, null, today, archived: true));
    }
}

public class AchievementCatalogTests
{
    private static readonly AchievementStats Empty = new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void Keys_are_unique_and_targets_positive()
    {
        Assert.Equal(AchievementCatalog.All.Count, AchievementCatalog.All.Select(a => a.Key).Distinct().Count());
        Assert.All(AchievementCatalog.All, a => Assert.True(a.Target > 0));
    }

    [Fact]
    public void Progress_is_capped_at_the_target()
    {
        var stats = Empty with { BestDayStreak = 12, CheckIns = 1, MaxHabitLevel = 1 };
        var progress = AchievementCatalog.Evaluate(stats).ToDictionary(x => x.Definition.Key, x => x.Progress);
        Assert.Equal(7, progress["streak-7"]);
        Assert.Equal(12, progress["streak-30"]);
        Assert.Equal(1, progress["checkins-1"]);
        Assert.Equal(1, progress["habit-level-2"]);
        Assert.Equal(0, progress["goal-1"]);
    }
}

public class DatabaseBackupRulesTests
{
    [Fact]
    public void Keeps_the_last_14_days_and_ignores_other_files()
    {
        var today = new DateOnly(2026, 10, 22);
        var files = new[]
        {
            DatabaseBackup.FileName(today),
            DatabaseBackup.FileName(today.AddDays(-13)), // 14th day: kept
            DatabaseBackup.FileName(today.AddDays(-14)), // expired
            DatabaseBackup.FileName(today.AddDays(-40)), // expired
            "arco.db", "arco-2026-10-22.db.tmp", "notes.txt",
        };
        Assert.Equal(["arco-2026-10-08.db", "arco-2026-09-12.db"], DatabaseBackup.Expired(files, today));
    }

    [Fact]
    public void Reads_the_date_from_the_file_name()
    {
        Assert.Equal(new DateOnly(2026, 10, 22), DatabaseBackup.DateOf("arco-2026-10-22.db"));
        Assert.Null(DatabaseBackup.DateOf("arco-2026-13-40.db"));
        Assert.Null(DatabaseBackup.DateOf("arco.db"));
    }
}

public class LevelDownSuggestionTests
{
    // 2026-03-02 is a Monday; "today" is Wednesday of the third week.
    private static readonly DateOnly Monday = new(2026, 3, 2);
    private static readonly DateOnly Today = Monday.AddDays(16);

    private static IEnumerable<(DateOnly, CheckInStatus)> Week(DateOnly week, int days) =>
        Enumerable.Range(0, days).Select(i => (week.AddDays(i), CheckInStatus.Minimum));

    private static bool Suggest(IEnumerable<(DateOnly, CheckInStatus)> checkIns, int level = 2, DateOnly? reachedOn = null,
        DateOnly? snoozed = null) =>
        LevelDownSuggestion.ShouldSuggest(checkIns, 5, level, reachedOn ?? Monday, snoozed, Today, archived: false);

    [Fact]
    public void Target_comes_from_the_weeks_rule_or_the_frequency()
    {
        Assert.Equal(5, LevelDownSuggestion.WeeklyTarget(FrequencyType.Daily, [], null, 5));
        Assert.Equal(7, LevelDownSuggestion.WeeklyTarget(FrequencyType.Daily, [], null, null));
        Assert.Equal(3, LevelDownSuggestion.WeeklyTarget(FrequencyType.SpecificDays, [1, 3, 5], null, null));
        Assert.Equal(4, LevelDownSuggestion.WeeklyTarget(FrequencyType.WeeklyTarget, [], 4, null));
    }

    [Fact]
    public void Two_finished_weeks_below_half_suggest_going_back()
    {
        // Target 5: 2 days is weak, 3 is not.
        Assert.True(Suggest(Week(Monday, 2).Concat(Week(Monday.AddDays(7), 1))));
        Assert.False(Suggest(Week(Monday, 2).Concat(Week(Monday.AddDays(7), 3))));
        // The current (unfinished) week does not count, even if it is empty.
        Assert.False(Suggest(Week(Monday, 5).Concat(Week(Monday.AddDays(7), 0))));
    }

    [Fact]
    public void Needs_level_2_both_weeks_at_the_current_level_and_no_snooze()
    {
        Assert.False(Suggest([], level: 1));
        Assert.False(Suggest([], reachedOn: Monday.AddDays(1)));
        Assert.False(Suggest([], snoozed: Today.AddDays(1)));
        Assert.True(Suggest([], snoozed: Today));
    }
}
