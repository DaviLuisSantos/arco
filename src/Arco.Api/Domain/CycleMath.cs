namespace Arco.Api.Domain;

public static class CycleMath
{
    public const int DefaultWeeks = 6;
    public const int MinWeeks = 1;
    public const int MaxWeeks = 12;

    /// <summary>endDate = start + weeks × 7 − 1.</summary>
    public static DateOnly EndDate(DateOnly start, int weeks) => start.AddDays(weeks * 7 - 1);

    public static int TotalDays(DateOnly start, DateOnly end) => end.DayNumber - start.DayNumber + 1;

    /// <summary>1-based day of the cycle relative to today, limited to 1..totalDays.</summary>
    public static int DayNumber(DateOnly start, DateOnly end, DateOnly today) =>
        Math.Clamp(today.DayNumber - start.DayNumber + 1, 1, Math.Max(1, TotalDays(start, end)));
}
