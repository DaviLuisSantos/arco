namespace Arco.Api.Features.Achievements;

/// <param name="UnlockedAt">null while locked.</param>
public sealed record AchievementDto(
    string Key,
    string Emoji,
    string Title,
    string Description,
    int Target,
    int Progress,
    DateTime? UnlockedAt);

public static class AchievementEndpoints
{
    public static IEndpointRouteBuilder MapAchievements(this IEndpointRouteBuilder api)
    {
        api.MapGet("/achievements", List).WithTags("Conquistas").WithSummary("Lista as conquistas");
        return api;
    }

    /// <summary>The whole catalog, in order, with progress and unlock date.</summary>
    private static async Task<List<AchievementDto>> List(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var stats = await AchievementService.StatsAsync(db, clock, ct);
        var unlocked = await db.Achievements.AsNoTracking().ToDictionaryAsync(a => a.Key, a => (DateTime?)a.UnlockedAt, ct);
        return AchievementCatalog.Evaluate(stats)
            .Select(x => AchievementService.ToDto(x.Definition, x.Progress, unlocked.GetValueOrDefault(x.Definition.Key)))
            .ToList();
    }
}

public static class AchievementService
{
    /// <summary>
    /// Unlocks every achievement whose target was reached and returns the new ones. Called after each action that
    /// gives XP (its response carries them, so the app can celebrate) and at startup (silently, for old data).
    /// </summary>
    public static async Task<List<AchievementDto>> UnlockAsync(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var stats = await StatsAsync(db, clock, ct);
        var known = (await db.Achievements.Select(a => a.Key).ToListAsync(ct)).ToHashSet();
        var reached = AchievementCatalog.Evaluate(stats)
            .Where(x => x.Progress >= x.Definition.Target && !known.Contains(x.Definition.Key))
            .ToList();
        if (reached.Count == 0) return [];

        var now = clock.UtcNow;
        db.Achievements.AddRange(reached.Select(x => new UnlockedAchievement { Key = x.Definition.Key, UnlockedAt = now }));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            // Unlocked at the same time by another request: nothing new to celebrate here.
            db.ChangeTracker.Clear();
            return [];
        }
        return reached.Select(x => ToDto(x.Definition, x.Progress, now)).ToList();
    }

    /// <summary>
    /// Takes back achievements that no longer hold. Only called when data is deleted (a cycle or a goal): undoing a
    /// check-in never revokes an achievement.
    /// </summary>
    public static async Task RevokeUnmetAsync(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var stats = await StatsAsync(db, clock, ct);
        var unmet = AchievementCatalog.Evaluate(stats)
            .Where(x => x.Progress < x.Definition.Target)
            .Select(x => x.Definition.Key)
            .ToList();
        await db.Achievements.Where(a => unmet.Contains(a.Key)).ExecuteDeleteAsync(ct);
    }

    public static async Task<AchievementStats> StatsAsync(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var today = await clock.GetTodayAsync(ct);
        var timezone = await clock.GetTimezoneAsync(ct);
        var habits = await db.Habits.AsNoTracking().Include(h => h.Levels).ToListAsync(ct);
        var checkIns = (await db.CheckIns.AsNoTracking().Where(c => c.Date <= today).ToListAsync(ct))
            .ToLookup(c => c.HabitId);

        var streaks = habits.Select(h => Queries.Streak(h, checkIns[h.Id].ToList(), today, timezone)).ToList();
        int Best(StreakUnit unit) => streaks.Where(s => s.Unit == unit).Select(s => s.Best).DefaultIfEmpty(0).Max();
        var counted = checkIns.SelectMany(g => g).ToList();

        return new AchievementStats(
            BestDayStreak: Best(StreakUnit.Days),
            BestWeekStreak: Best(StreakUnit.Weeks),
            CheckIns: counted.Count(c => HabitSchedule.CountsTowardWeek(c.Status)),
            Minimums: counted.Count(c => c.Status == CheckInStatus.Minimum),
            MaxHabitLevel: habits.Select(h => h.CurrentLevelNumber).DefaultIfEmpty(0).Max(),
            PlayerLevel: PlayerLevel.FromTotalXp(await db.XpEvents.SumAsync(e => e.Amount, ct)).Level,
            GoalsCompleted: await db.Goals.CountAsync(g => g.CompletedAt != null, ct),
            CyclesCompleted: await db.Cycles.CountAsync(c => c.Status == CycleStatus.Completed, ct),
            WeeklyReviews: await db.WeeklyReviews.CountAsync(ct));
    }

    public static AchievementDto ToDto(AchievementDefinition d, int progress, DateTime? unlockedAt) =>
        new(d.Key, d.Emoji, d.Title, d.Description, d.Target, unlockedAt is null ? progress : d.Target, unlockedAt);
}
