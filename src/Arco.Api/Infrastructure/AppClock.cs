namespace Arco.Api.Infrastructure;

/// <summary>Request-scoped clock: UTC now plus "today" in the current user's Settings.Timezone.</summary>
public sealed class AppClock(TimeProvider time, AppDbContext db)
{
    private string? _timezone;

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public async Task<string> GetTimezoneAsync(CancellationToken ct = default) =>
        _timezone ??= await db.Settings.AsNoTracking()
            .Select(s => s.Timezone)
            .FirstOrDefaultAsync(ct) ?? TimeZones.Default;

    public async Task<DateOnly> GetTodayAsync(CancellationToken ct = default) =>
        TimeZones.Today(time, await GetTimezoneAsync(ct));

    public void Invalidate() => _timezone = null;
}
