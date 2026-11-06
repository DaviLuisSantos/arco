namespace Arco.Api.Domain;

public static class CycleMath
{
    public const int DefaultWeeks = 6;
    public const int MinWeeks = 2;
    public const int MaxWeeks = 12;

    /// <summary>endDate = start + weeks × 7 − 1.</summary>
    public static DateOnly EndDate(DateOnly start, int weeks) => start.AddDays(weeks * 7 - 1);

    public static int TotalDays(DateOnly start, DateOnly end) => end.DayNumber - start.DayNumber + 1;

    /// <summary>1-based day of the cycle relative to today, limited to 1..totalDays.</summary>
    public static int DayNumber(DateOnly start, DateOnly end, DateOnly today) =>
        Math.Clamp(today.DayNumber - start.DayNumber + 1, 1, Math.Max(1, TotalDays(start, end)));

    public static bool IsValidWeeks(int weeks) => weeks is >= MinWeeks and <= MaxWeeks && weeks % 2 == 0;

    /// <summary>1-based week of the cycle (7-day blocks from start); may be below 1 or above the cycle length.</summary>
    public static int WeekOfCycle(DateOnly start, DateOnly date) =>
        (int)Math.Floor((date.DayNumber - start.DayNumber) / 7.0) + 1;

    public static int WeekCount(DateOnly start, DateOnly end) => (TotalDays(start, end) + 6) / 7;

    /// <summary>"Arch" for an even number of weeks (&gt;= 2), otherwise "Row".</summary>
    public static string ArchLayout(int weeks) => weeks >= 2 && weeks % 2 == 0 ? "Arch" : "Row";
}
