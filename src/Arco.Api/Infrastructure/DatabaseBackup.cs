using System.Globalization;
using System.Text.RegularExpressions;

namespace Arco.Api.Infrastructure;

/// <summary>
/// SQLite backups with <c>VACUUM INTO</c> (a consistent copy even while the app is running): one automatic copy per
/// day in <c>&lt;data dir&gt;/backups</c>, keeping the last <see cref="KeepDays"/> days, and on-demand snapshots for download.
/// </summary>
public sealed partial class DatabaseBackup(string directory)
{
    public const int KeepDays = 14;

    public string Directory { get; } = directory;

    public sealed record BackupFile(DateOnly Date, long SizeBytes);

    public static string FileName(DateOnly date) => $"arco-{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.db";

    [GeneratedRegex(@"^arco-(\d{4}-\d{2}-\d{2})\.db$")]
    private static partial Regex FilePattern();

    public static DateOnly? DateOf(string fileName) =>
        FilePattern().Match(fileName) is { Success: true } m
        && DateOnly.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    /// <summary>Backup files older than the last <see cref="KeepDays"/> days (other files are never touched).</summary>
    public static IReadOnlyList<string> Expired(IEnumerable<string> fileNames, DateOnly today) =>
        fileNames.Where(f => DateOf(f) is { } d && d <= today.AddDays(-KeepDays)).ToList();

    /// <summary>Creates today's copy unless it already exists, then removes expired ones.</summary>
    /// <returns>The path of the new copy, or null when today's copy already existed.</returns>
    public async Task<string?> CreateDailyAsync(AppDbContext db, DateOnly today, CancellationToken ct)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var path = Path.Combine(Directory, FileName(today));
        if (File.Exists(path)) return null;

        await VacuumIntoAsync(db, path, ct);
        foreach (var expired in Expired(System.IO.Directory.GetFiles(Directory).Select(Path.GetFileName)!, today))
            File.Delete(Path.Combine(Directory, expired));
        return path;
    }

    /// <summary>A fresh copy in a temporary file (the caller deletes it).</summary>
    public async Task<string> SnapshotAsync(AppDbContext db, CancellationToken ct)
    {
        var path = Path.Combine(Path.GetTempPath(), $"arco-snapshot-{Guid.NewGuid():N}.db");
        await VacuumIntoAsync(db, path, ct);
        return path;
    }

    /// <summary>Automatic copies, newest first.</summary>
    public IReadOnlyList<BackupFile> List()
    {
        if (!System.IO.Directory.Exists(Directory)) return [];
        return new DirectoryInfo(Directory).GetFiles("arco-*.db")
            .Select(f => DateOf(f.Name) is { } d ? new BackupFile(d, f.Length) : null)
            .OfType<BackupFile>()
            .OrderByDescending(b => b.Date)
            .ToList();
    }

    /// <summary>Writes to a temporary name first, so a half-written file never looks like a finished backup.</summary>
    private static async Task VacuumIntoAsync(AppDbContext db, string path, CancellationToken ct)
    {
        var temp = path + ".tmp";
        if (File.Exists(temp)) File.Delete(temp);
        await db.Database.ExecuteSqlRawAsync("VACUUM INTO {0}", [temp], ct);
        File.Move(temp, path, overwrite: true);
    }
}

/// <summary>Checks every hour (and right after startup) that today's automatic copy exists.</summary>
public sealed class DatabaseBackupService(
    IServiceScopeFactory scopes, DatabaseBackup backup, TimeProvider time, ILogger<DatabaseBackupService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), time);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var today = await scope.ServiceProvider.GetRequiredService<AppClock>().GetTodayAsync(stoppingToken);
                if (await backup.CreateDailyAsync(db, today, stoppingToken) is { } path)
                    log.LogInformation("Database backup created: {Path}", path);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Database backup failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
