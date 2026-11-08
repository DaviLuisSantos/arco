using System.Globalization;

namespace Arco.Api.Features.Backup;

public static class BackupEndpoints
{
    public static IEndpointRouteBuilder MapBackup(this IEndpointRouteBuilder api)
    {
        api.MapGet("/backup", Export).WithTags("Backup").WithSummary("Exporta os dados do usuário");
        return api;
    }

    private static readonly System.Text.Json.JsonSerializerOptions ExportOptions = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public static string ExportFileName(DateOnly date) =>
        $"arco-export-{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.json";

    /// <summary>
    /// Downloads everything the current user owns as JSON (query filters scope every set to them). Secrets are left out:
    /// API key hashes and the push subscription keys; the user id is implied.
    /// </summary>
    private static async Task<IResult> Export(AppDbContext db, AppClock clock, CurrentUser currentUser, CancellationToken ct)
    {
        var today = await clock.GetTodayAsync(ct);

        var export = new
        {
            exportedAt = clock.UtcNow,
            user = await db.Users.AsNoTracking().Where(u => u.Id == currentUser.Id)
                .Select(u => new { u.Email, u.DisplayName, u.CreatedAt }).FirstOrDefaultAsync(ct),
            settings = await db.Settings.AsNoTracking()
                .Select(s => new { s.Timezone, s.WeeklyReviewReminderEnabled, s.WeeklyReviewReminderDay, s.WeeklyReviewReminderTime })
                .ToListAsync(ct),
            cycles = await db.Cycles.AsNoTracking().OrderBy(c => c.Id)
                .Select(c => new
                {
                    c.Id, c.Name, c.StartDate, c.EndDate, c.Status, c.ReviewNotes, c.ReviewWins, c.ReviewObstacles,
                    c.ReviewNext, c.CompletedAt, c.CreatedAt, c.WeeklyFocusMinutes,
                }).ToListAsync(ct),
            goals = await db.Goals.AsNoTracking().OrderBy(g => g.Id)
                .Select(g => new
                {
                    g.Id, g.CycleId, g.Title, g.Description, g.Type, g.Unit, g.StartValue, g.TargetValue, g.CurrentValue,
                    g.Deadline, g.CompletedAt, g.CreatedAt,
                }).ToListAsync(ct),
            milestones = await db.Milestones.AsNoTracking().OrderBy(m => m.Id)
                .Select(m => new { m.Id, m.GoalId, m.Title, m.Order, m.DoneAt }).ToListAsync(ct),
            goalUpdates = await db.GoalUpdates.AsNoTracking().OrderBy(u => u.Id)
                .Select(u => new { u.Id, u.GoalId, u.Value, u.Note, u.CreatedAt }).ToListAsync(ct),
            habits = await db.Habits.AsNoTracking().OrderBy(h => h.Id)
                .Select(h => new
                {
                    h.Id, h.Name, h.Emoji, h.GoalId, h.FrequencyType, h.FrequencyDays, h.TimesPerWeek, h.ReminderTime,
                    h.LevelUpSnoozedUntil, h.LevelDownSnoozedUntil, h.LevelUpWeeks, h.LevelUpDaysPerWeek, h.ArchivedAt,
                    h.CreatedAt,
                }).ToListAsync(ct),
            habitLevels = await db.HabitLevels.AsNoTracking().OrderBy(l => l.Id)
                .Select(l => new { l.Id, l.HabitId, l.Level, l.Description, l.MinimumDescription, l.ReachedAt })
                .ToListAsync(ct),
            habitPlannedLevels = await db.HabitPlannedLevels.AsNoTracking().OrderBy(l => l.Id)
                .Select(l => new { l.Id, l.HabitId, l.Position, l.Description, l.MinimumDescription }).ToListAsync(ct),
            checkIns = await db.CheckIns.AsNoTracking().OrderBy(c => c.Id)
                .Select(c => new { c.Id, c.HabitId, c.Date, c.Status, c.Xp, c.CreatedAt, c.UpdatedAt }).ToListAsync(ct),
            focusSessions = await db.FocusSessions.AsNoTracking().OrderBy(f => f.Id)
                .Select(f => new { f.Id, f.HabitId, f.Date, f.Minutes, f.Source, f.StartedAt, f.CreatedAt }).ToListAsync(ct),
            xpEvents = await db.XpEvents.AsNoTracking().OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id, x.Date, x.Amount, x.Source, x.Description, x.CreatedAt, x.CheckInId, x.HabitId, x.GoalId,
                    x.MilestoneId, x.CycleId, x.WeeklyReviewId,
                }).ToListAsync(ct),
            weeklyReviews = await db.WeeklyReviews.AsNoTracking().OrderBy(r => r.Id)
                .Select(r => new { r.Id, r.WeekStart, r.Wins, r.Obstacles, r.Next, r.CreatedAt }).ToListAsync(ct),
            achievements = await db.Achievements.AsNoTracking().OrderBy(a => a.Id)
                .Select(a => new { a.Key, a.UnlockedAt }).ToListAsync(ct),
            apiKeys = await db.ApiKeys.AsNoTracking().OrderBy(k => k.Id)
                .Select(k => new { k.Id, k.Name, k.Prefix, k.CreatedAt, k.LastUsedAt }).ToListAsync(ct),
        };

        return Results.File(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(export, ExportOptions),
            "application/json", ExportFileName(today));
    }
}
