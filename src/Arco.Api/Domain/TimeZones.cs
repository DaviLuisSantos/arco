namespace Arco.Api.Domain;

public static class TimeZones
{
    public const string Default = "America/Sao_Paulo";

    /// <summary>True when <paramref name="id"/> is a known IANA time zone id.</summary>
    public static bool IsValidIana(string? id) =>
        !string.IsNullOrWhiteSpace(id)
        && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)
        && tz.HasIanaId;

    /// <summary>Resolves an IANA id, falling back to America/Sao_Paulo (then UTC).</summary>
    public static TimeZoneInfo Resolve(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.TryFindSystemTimeZoneById(Default, out var fallback) ? fallback : TimeZoneInfo.Utc;
    }

    /// <summary>Calendar date "today" in the given time zone.</summary>
    public static DateOnly Today(TimeProvider time, string? timezoneId) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), Resolve(timezoneId)).DateTime);
}
