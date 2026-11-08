using System.Security.Claims;
using Dapper;
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

        // Diagnostic: an unhandled exception that must show up in Sentry. Regular users cannot tell it exists.
        secured.MapPost("/admin/sentry-test", async (ClaimsPrincipal principal, AppDbContext db, IConfiguration config,
                CancellationToken ct) =>
            {
                if (!await IsAdminAsync(principal, db, config, ct)) return ApiResults.NotFound();
                throw new InvalidOperationException("Teste do Sentry disparado pelo admin.");
            })
            .WithTags("Admin")
            .WithSummary("Força um erro 500 para testar o Sentry (somente admin; 404 para os demais)");
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

    private sealed record Totals(long Total, long Verified, long Demo, long Pro);
    private sealed record DayRow(string Day, long Count);
    private sealed record UserRow(long Id, string Day);
    private sealed record CheckInRow(long UserId, string Day);

    private static async Task<AdminMetrics> MetricsAsync(AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);

        static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");
        static DateOnly Parse(string s) => DateOnly.ParseExact(s[..10], "yyyy-MM-dd");

        var t = await conn.QuerySingleAsync<Totals>(new CommandDefinition(
            """
            SELECT COUNT(*) AS Total,
                   COALESCE(SUM(EmailVerifiedAt IS NOT NULL AND IsDemo = 0), 0) AS Verified,
                   COALESCE(SUM(IsDemo = 1), 0) AS Demo,
                   COALESCE(SUM(IsDemo = 0 AND Plan = 'Pro'), 0) AS Pro
            FROM Users
            """, cancellationToken: ct));
        var totals = new UsersTotals((int)t.Total, (int)t.Verified, (int)t.Demo, (int)t.Pro);

        // Retention needs 8 cohorts of weeks plus up to 4 weeks after the oldest one.
        var currentWeek = HabitSchedule.WeekStart(today);
        var firstCohort = currentWeek.AddDays(-7 * 7);
        var since = firstCohort.AddDays(-1);
        var from30 = today.AddDays(-29);

        // CreatedAt is stored as TEXT ("yyyy-MM-dd HH:mm:ss..."), so its first 10 chars are the UTC day.
        var real = (await conn.QueryAsync<UserRow>(new CommandDefinition(
            "SELECT Id, substr(CreatedAt, 1, 10) AS Day FROM Users WHERE IsDemo = 0", cancellationToken: ct)))
            .Select(u => (Id: (int)u.Id, Day: Parse(u.Day))).ToList();

        var signups = real.Select(u => u.Day).Where(d => d >= from30 && d <= today)
            .GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());

        var perDay = (await conn.QueryAsync<DayRow>(new CommandDefinition(
            """
            SELECT c.Date AS Day, COUNT(DISTINCT c.UserId) AS Count
            FROM CheckIns c JOIN Users u ON u.Id = c.UserId
            WHERE u.IsDemo = 0 AND c.Status <> 'Missed' AND c.Date >= @From AND c.Date <= @To
            GROUP BY c.Date
            """, new { From = Iso(from30), To = Iso(today) }, cancellationToken: ct)))
            .ToDictionary(r => Parse(r.Day), r => (int)r.Count);

        async Task<int> ActiveAsync(int d) => await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(DISTINCT c.UserId)
            FROM CheckIns c JOIN Users u ON u.Id = c.UserId
            WHERE u.IsDemo = 0 AND c.Status <> 'Missed' AND c.Date > @From AND c.Date <= @To
            """, new { From = Iso(today.AddDays(-d)), To = Iso(today) }, cancellationToken: ct));

        var checkIns = (await conn.QueryAsync<CheckInRow>(new CommandDefinition(
            """
            SELECT DISTINCT c.UserId AS UserId, c.Date AS Day
            FROM CheckIns c JOIN Users u ON u.Id = c.UserId
            WHERE u.IsDemo = 0 AND c.Status <> 'Missed' AND c.Date >= @Since
            """, new { Since = Iso(since) }, cancellationToken: ct)))
            .ToLookup(c => (int)c.UserId, c => Parse(c.Day));

        var days = Enumerable.Range(0, 30).Select(i => from30.AddDays(i)).ToList();
        var retention = new List<RetentionRow>();
        for (var i = 0; i < 8; i++)
        {
            var start = firstCohort.AddDays(7 * i);
            var cohort = real.Where(u => u.Day >= start && u.Day < start.AddDays(7)).ToList();
            double Rate(int week)
            {
                if (cohort.Count == 0) return 0;
                var ws = start.AddDays(7 * week);
                var we = ws.AddDays(6);
                var kept = cohort.Count(u => checkIns[u.Id].Any(d => d >= ws && d <= we));
                return Math.Round((double)kept / cohort.Count, 3);
            }
            retention.Add(new RetentionRow(Iso(start), cohort.Count, Rate(1), Rate(2), Rate(4)));
        }

        return new AdminMetrics(totals,
            days.Select(d => new DayCount(Iso(d), signups.GetValueOrDefault(d))).ToList(),
            days.Select(d => new DayCount(Iso(d), perDay.GetValueOrDefault(d))).ToList(),
            new ActiveUsers(await ActiveAsync(1), await ActiveAsync(7), await ActiveAsync(30)), retention);
    }
}
