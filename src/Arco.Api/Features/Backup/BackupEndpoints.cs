namespace Arco.Api.Features.Backup;

public static class BackupEndpoints
{
    public sealed record BackupFileDto(DateOnly Date, long SizeBytes);

    public static IEndpointRouteBuilder MapBackup(this IEndpointRouteBuilder api)
    {
        api.MapGet("/backup", Download);
        api.MapGet("/backups", List);
        return api;
    }

    /// <summary>Downloads a fresh copy of the database (SQLite file).</summary>
    private static async Task<IResult> Download(AppDbContext db, DatabaseBackup backup, AppClock clock, CancellationToken ct)
    {
        var path = await backup.SnapshotAsync(db, ct);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 64 * 1024,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        var today = await clock.GetTodayAsync(ct);
        return Results.File(stream, "application/vnd.sqlite3", DatabaseBackup.FileName(today));
    }

    /// <summary>The automatic daily copies kept on the server, newest first.</summary>
    private static IReadOnlyList<BackupFileDto> List(DatabaseBackup backup) =>
        backup.List().Select(b => new BackupFileDto(b.Date, b.SizeBytes)).ToList();
}
