using System.Security.Claims;
using Arco.Api.Domain;

namespace Arco.Api.Features.Admin;

public static class AdminEndpoints
{
    public sealed record DayCount(string Date, int Count);
    public sealed record UsersTotals(int Total, int Verified, int Demo, int Pro);
    public sealed record ActiveUsers(int D1, int D7, int D30);
    public sealed record RetentionRow(string CohortWeek, int Size, double Week1, double Week2, double Week4);
    public sealed record AdminMetrics(
        UsersTotals Users, List<DayCount> SignupsByDay, List<DayCount> CheckInsByDay,
        ActiveUsers ActiveUsers, List<RetentionRow> Retention);

    public static RouteGroupBuilder MapAdmin(this RouteGroupBuilder secured)
    {
        secured.MapGet("/admin/metrics", async (ClaimsPrincipal principal, AppDbContext db, IConfiguration config,
                TimeProvider time, CancellationToken ct) =>
            {
                if (!await IsAdminAsync(principal, db, config, ct)) return ApiResults.NotFound();
                return Results.Ok(await MetricsAsync(db, time, ct));
            })
            .WithTags("Admin")
            .WithSummary("Métricas do produto (somente admin; 404 para os demais)")
            .Produces<AdminMetrics>();
        return secured;
    }

    /// <summary>Admin = e-mail listed in <c>Admin:Emails</c> (comma-separated).</summary>
    private static async Task<bool> IsAdminAsync(ClaimsPrincipal principal, AppDbContext db, IConfiguration config, CancellationToken ct)
    {
        var raw = config["Admin:Emails"];
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id <= 0) return false;
        var email = await db.Users.Where(u => u.Id == id).Select(u => u.Email).FirstOrDefaultAsync(ct);
        if (email is null) return false;
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<AdminMetrics> MetricsAsync(AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

        var users = await db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.CreatedAt, u.IsDemo, u.Plan, Verified = u.EmailVerifiedAt != null })
            .ToListAsync(ct);
        var real = users.Where(u => !u.IsDemo).ToList();
        var realIds = real.Select(u => u.Id).ToHashSet();

        var totals = new UsersTotals(users.Count, users.Count(u => u.Verified && !u.IsDemo),
            users.Count(u => u.IsDemo), real.Count(u => u.Plan == UserPlan.Pro));

        // Retention needs 8 cohorts of weeks plus up to 4 weeks after the oldest one.
        var currentWeek = HabitSchedule.WeekStart(today);
        var firstCohort = currentWeek.AddDays(-7 * 7);
        var since = firstCohort.AddDays(-1);
        var checkIns = (await db.CheckIns.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.Status != CheckInStatus.Missed && c.Date >= since)
                .Select(c => new { c.UserId, c.Date })
                .ToListAsync(ct))
            .Where(c => realIds.Contains(c.UserId))
            .Distinct()
            .ToList();

        var from30 = today.AddDays(-29);
        var signups = real.Select(u => DateOnly.FromDateTime(u.CreatedAt)).Where(d => d >= from30 && d <= today)
            .GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());
        var perDay = checkIns.Where(c => c.Date >= from30 && c.Date <= today)
            .GroupBy(c => c.Date).ToDictionary(g => g.Key, g => g.Count());
        var days = Enumerable.Range(0, 30).Select(i => from30.AddDays(i)).ToList();

        int Active(int d) => checkIns.Where(c => c.Date > today.AddDays(-d) && c.Date <= today)
            .Select(c => c.UserId).Distinct().Count();

        var byUser = checkIns.ToLookup(c => c.UserId, c => c.Date);
        var retention = new List<RetentionRow>();
        for (var i = 0; i < 8; i++)
        {
            var start = firstCohort.AddDays(7 * i);
            var cohort = real.Where(u =>
            {
                var d = DateOnly.FromDateTime(u.CreatedAt);
                return d >= start && d < start.AddDays(7);
            }).ToList();
            double Rate(int week)
            {
                if (cohort.Count == 0) return 0;
                var ws = start.AddDays(7 * week);
                var we = ws.AddDays(6);
                var kept = cohort.Count(u => byUser[u.Id].Any(d => d >= ws && d <= we));
                return Math.Round((double)kept / cohort.Count, 3);
            }
            retention.Add(new RetentionRow(start.ToString("yyyy-MM-dd"), cohort.Count, Rate(1), Rate(2), Rate(4)));
        }

        return new AdminMetrics(totals,
            days.Select(d => new DayCount(d.ToString("yyyy-MM-dd"), signups.GetValueOrDefault(d))).ToList(),
            days.Select(d => new DayCount(d.ToString("yyyy-MM-dd"), perDay.GetValueOrDefault(d))).ToList(),
            new ActiveUsers(Active(1), Active(7), Active(30)), retention);
    }
}
