using System.Globalization;

namespace Arco.Api.Domain;

/// <summary>
/// Rules of quantitative habits (a unit, plus a goal and a minimum per level). Amounts are stored in tenths of the
/// unit (integers); the API speaks decimals with at most one decimal place. Pure and testable.
/// </summary>
public static class QuantityRules
{
    /// <summary>100000,0 in tenths: the largest amount accepted.</summary>
    public const int MaxTenths = 1_000_000;

    public const int MaxUnitLength = 16;

    /// <summary>
    /// Converts an amount to tenths. Accepts 0 (a day's total may be zero; callers needing a positive goal check it)
    /// up to <see cref="MaxTenths"/>; refuses negatives, more than one decimal place (never rounds) and larger values.
    /// </summary>
    public static bool TryToTenths(decimal value, out int tenths)
    {
        tenths = 0;
        if (value < 0 || value > MaxTenths / 10m) return false;
        var scaled = value * 10m;
        if (scaled != decimal.Truncate(scaled)) return false;
        tenths = (int)scaled;
        return true;
    }

    public static decimal FromTenths(int tenths) => tenths / 10m;

    /// <summary>
    /// Status that a day's total gives: Done at or above the goal, Minimum at or above the minimum, otherwise null
    /// (nothing reached yet, so no check-in is derived).
    /// </summary>
    public static CheckInStatus? StatusFor(int totalTenths, int targetTenths, int minimumTenths)
    {
        if (totalTenths >= targetTenths) return CheckInStatus.Done;
        if (totalTenths >= minimumTenths) return CheckInStatus.Minimum;
        return null;
    }

    /// <summary>"2,5 km", "8 copos": decimal comma, no trailing ",0".</summary>
    public static string Describe(int tenths, string unit)
    {
        var whole = tenths / 10;
        var fraction = tenths % 10;
        var number = fraction == 0
            ? whole.ToString(CultureInfo.InvariantCulture)
            : $"{whole.ToString(CultureInfo.InvariantCulture)},{fraction.ToString(CultureInfo.InvariantCulture)}";
        return $"{number} {unit}";
    }
}
