using Arco.Api.Features.Achievements;
using Arco.Api.Features.CheckIns;

namespace Arco.Api.Features.Habits;

/// <summary>
/// Fills the "clean" days of negative (quit) habits: a day that passed with no check-in and no pause is clean, so it
/// becomes a Done check-in (with XP). Today is never filled (it stays pending until the day turns or the user
/// confirms), and only the last <see cref="MaxCatchUpDays"/> days are looked at, so a long absence does not mint XP.
/// Idempotent: a day with any check-in (Done, Missed, Skipped) is left alone.
/// </summary>
public sealed class CleanDaysService(IServiceScopeFactory scopes, ILogger<CleanDaysService> logger)
{
    /// <summary>How far back (from today) the automatic days are filled.</summary>
    public const int MaxCatchUpDays = 7;

    /// <summary>Runs for every user with an active negative habit, each in its own scope acting as that user.</summary>
    /// <returns>Number of clean days created.</returns>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        List<int> userIds;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userIds = await db.Habits.IgnoreQueryFilters().AsNoTracking()
                .Where(h => h.Kind == HabitKind.Negative && h.ArchivedAt == null)
                .Select(h => h.UserId).Distinct().ToListAsync(ct);
        }

        var created = 0;
        foreach (var userId in userIds)
        {
            try
            {
                using var scope = scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<CurrentUser>().Impersonate(userId);
                created += await RunForCurrentUserAsync(
                    scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                    scope.ServiceProvider.GetRequiredService<AppClock>(), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Clean days failed for user {UserId}", userId);
            }
        }
        return created;
    }

    private static async Task<int> RunForCurrentUserAsync(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var timezone = await clock.GetTimezoneAsync(ct);
        var today = await clock.GetTodayAsync(ct);
        var yesterday = today.AddDays(-1);
        var now = clock.UtcNow;

        var habits = await db.Habits.Include(h => h.Levels).Include(h => h.Pauses)
            .Where(h => h.Kind == HabitKind.Negative && h.ArchivedAt == null)
            .ToListAsync(ct);

        var created = 0;
        foreach (var habit in habits)
        {
            var from = TimeZones.ToLocalDate(habit.CreatedAt, timezone);
            var earliest = today.AddDays(-MaxCatchUpDays);
            if (from < earliest) from = earliest;
            if (from > yesterday) continue;

            var taken = (await db.CheckIns.AsNoTracking()
                    .Where(c => c.HabitId == habit.Id && c.Date >= from && c.Date <= yesterday)
                    .Select(c => c.Date).ToListAsync(ct))
                .ToHashSet();
            var calendar = PauseCalendar.From(habit.Pauses.Select(p => (p.StartDate, p.EndDate)), []);

            for (var day = from; day <= yesterday; day = day.AddDays(1))
            {
                if (taken.Contains(day) || calendar.IsPaused(day)) continue;
                await CheckInEndpoints.ApplyStatusAsync(db, habit, day, CheckInStatus.Done, now, false, null, ct);
                created++;
            }
        }

        if (created == 0) return 0;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            // Another run (or the user) filled the same day at the same time: the next run picks up what is missing.
            db.ChangeTracker.Clear();
            return 0;
        }
        await AchievementService.UnlockAsync(db, clock, ct);
        return created;
    }
}

/// <summary>Runs <see cref="CleanDaysService"/> once at startup and then every hour.</summary>
public sealed class CleanDaysHostedService(CleanDaysService service, TimeProvider time, ILogger<CleanDaysHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), time);
        do
        {
            try
            {
                var created = await service.RunAsync(stoppingToken);
                if (created > 0) logger.LogInformation("Created {Count} clean day(s).", created);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Clean days job failed."); }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
