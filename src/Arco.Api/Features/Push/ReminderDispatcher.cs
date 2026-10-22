using Arco.Api.Infrastructure.Push;

namespace Arco.Api.Features.Push;

/// <summary>
/// Sends the reminders that are due right now: one per habit (at its ReminderTime, on days it is due
/// and still has no check-in) and the weekly review (at the configured day/time while the review is pending).
/// Each reminder is claimed in PushDispatches first, so it goes out at most once.
/// </summary>
public sealed class ReminderDispatcher(AppDbContext db, AppClock clock, PushNotifier notifier, TimeProvider time)
{
    /// <summary>Reminders later than this (e.g. the server was down) are skipped, not sent late.</summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromHours(2);

    private static readonly TimeSpan DispatchRetention = TimeSpan.FromDays(60);

    /// <returns>Number of reminders sent.</returns>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        if (!await db.PushSubscriptions.AnyAsync(ct)) return 0;

        var timezone = await clock.GetTimezoneAsync(ct);
        var localNow = TimeZones.LocalNow(time, timezone);
        var today = DateOnly.FromDateTime(localNow);
        var now = TimeOnly.FromDateTime(localNow);

        var sent = await SendHabitRemindersAsync(today, now, ct) + await SendWeeklyReviewReminderAsync(today, now, ct);

        var cutoff = clock.UtcNow - DispatchRetention;
        await db.PushDispatches.Where(d => d.SentAt < cutoff).ExecuteDeleteAsync(ct);
        return sent;
    }

    internal static bool IsTime(TimeOnly at, TimeOnly now) => now >= at && now - at <= MaxDelay;

    private async Task<int> SendHabitRemindersAsync(DateOnly today, TimeOnly now, CancellationToken ct)
    {
        var habits = (await db.Habits.AsNoTracking()
                .Include(h => h.Levels)
                .Where(h => h.ArchivedAt == null && h.ReminderTime != null)
                .ToListAsync(ct))
            .Where(h => IsTime(h.ReminderTime!.Value, now))
            .ToList();
        if (habits.Count == 0) return 0;

        var ids = habits.Select(h => h.Id).ToList();
        var weekStart = HabitSchedule.WeekStart(today);
        var weekEnd = HabitSchedule.WeekEnd(today);
        var week = (await db.CheckIns.AsNoTracking()
                .Where(c => ids.Contains(c.HabitId) && c.Date >= weekStart && c.Date <= weekEnd)
                .ToListAsync(ct))
            .ToLookup(c => c.HabitId);

        var sent = 0;
        foreach (var habit in habits)
        {
            var checkIns = week[habit.Id].ToList();
            if (checkIns.Any(c => c.Date == today)) continue;
            var weekCount = HabitSchedule.WeekCount(checkIns.Select(c => (c.Date, c.Status)), today);
            if (!HabitSchedule.IsDueToday(habit.FrequencyType, habit.FrequencyDays, habit.TimesPerWeek, today, weekCount))
                continue;
            if (!await TryClaimAsync($"habit:{habit.Id}:{today:yyyy-MM-dd}", ct)) continue;

            var title = string.IsNullOrEmpty(habit.Emoji) ? habit.Name : $"{habit.Emoji} {habit.Name}";
            var current = habit.CurrentLevel;
            var body = current is null
                ? "Hora do seu hábito."
                : $"Hoje: {current.Description}. Sem tempo? O mínimo já vale: {current.MinimumDescription}.";
            await notifier.SendToAllAsync(new PushMessage(title, body, "/", $"habit-{habit.Id}"), ct);
            sent++;
        }
        return sent;
    }

    private async Task<int> SendWeeklyReviewReminderAsync(DateOnly today, TimeOnly now, CancellationToken ct)
    {
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == Domain.Settings.SingletonId, ct);
        if (settings is not { WeeklyReviewReminderEnabled: true }) return 0;
        if ((int)today.DayOfWeek != settings.WeeklyReviewReminderDay || !IsTime(settings.WeeklyReviewReminderTime, now))
            return 0;

        var weekStart = WeeklyReviewRules.ReviewableWeekStart(today);
        if (await db.WeeklyReviews.AnyAsync(r => r.WeekStart == weekStart, ct)) return 0;
        if (!await TryClaimAsync($"weekly-review:{weekStart:yyyy-MM-dd}", ct)) return 0;

        await notifier.SendToAllAsync(new PushMessage("Revisão da semana",
            $"Como foi a semana? Três perguntas rápidas e +{XpRules.WeeklyReview} XP.", "/review", "weekly-review"), ct);
        return 1;
    }

    private async Task<bool> TryClaimAsync(string key, CancellationToken ct)
    {
        if (await db.PushDispatches.AnyAsync(d => d.Key == key, ct)) return false;
        var dispatch = new PushDispatch { Key = key, SentAt = clock.UtcNow };
        db.PushDispatches.Add(dispatch);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            db.Entry(dispatch).State = EntityState.Detached;
            return false;
        }
    }
}

/// <summary>Runs <see cref="ReminderDispatcher"/> once a minute.</summary>
public sealed class ReminderService(IServiceScopeFactory scopes, TimeProvider time, ILogger<ReminderService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ReminderDispatcher>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Reminder dispatch failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
