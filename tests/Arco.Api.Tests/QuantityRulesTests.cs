using Arco.Api.Domain;

namespace Arco.Api.Tests;

public class QuantityRulesTests
{
    [Theory]
    [InlineData(8, 4, 8, CheckInStatus.Done)]
    [InlineData(10, 4, 8, CheckInStatus.Done)]
    [InlineData(7, 4, 8, CheckInStatus.Minimum)]
    [InlineData(4, 4, 8, CheckInStatus.Minimum)]
    [InlineData(3, 4, 8, null)]
    [InlineData(0, 4, 8, null)]
    [InlineData(4, 8, 8, null)]
    [InlineData(8, 8, 8, CheckInStatus.Done)]
    public void StatusFor_uses_the_goal_and_the_minimum_as_thresholds(int total, int minimum, int target, CheckInStatus? expected)
    {
        // Same numbers in tenths: the thresholds do not depend on the scale.
        Assert.Equal(expected, QuantityRules.StatusFor(total * 10, target * 10, minimum * 10));
    }

    [Fact]
    public void StatusFor_is_exact_on_the_tenth_below_the_thresholds()
    {
        Assert.Equal(CheckInStatus.Done, QuantityRules.StatusFor(25, 25, 10));
        Assert.Equal(CheckInStatus.Minimum, QuantityRules.StatusFor(24, 25, 10));
        Assert.Equal(CheckInStatus.Minimum, QuantityRules.StatusFor(10, 25, 10));
        Assert.Null(QuantityRules.StatusFor(9, 25, 10));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("0.1", 1)]
    [InlineData("2.5", 25)]
    [InlineData("2.50", 25)]
    [InlineData("8", 80)]
    [InlineData("100000", 1_000_000)]
    [InlineData("100000.0", 1_000_000)]
    public void TryToTenths_accepts_at_most_one_decimal_place(string value, int expected)
    {
        Assert.True(QuantityRules.TryToTenths(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), out var tenths));
        Assert.Equal(expected, tenths);
    }

    [Theory]
    [InlineData("2.55")]
    [InlineData("0.01")]
    [InlineData("1.999")]
    [InlineData("-1")]
    [InlineData("-0.1")]
    [InlineData("100000.1")]
    [InlineData("100001")]
    public void TryToTenths_refuses_rounding_negatives_and_values_above_the_limit(string value)
    {
        Assert.False(QuantityRules.TryToTenths(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), out _));
    }

    [Theory]
    [InlineData(25, "km", "2,5 km")]
    [InlineData(80, "copos", "8 copos")]
    [InlineData(1, "g", "0,1 g")]
    [InlineData(1_000_000, "páginas", "100000 páginas")]
    [InlineData(105, "páginas", "10,5 páginas")]
    public void Describe_uses_a_decimal_comma_and_drops_a_zero_fraction(int tenths, string unit, string expected)
    {
        Assert.Equal(expected, QuantityRules.Describe(tenths, unit));
    }

    [Fact]
    public void FromTenths_returns_a_decimal_with_one_place()
    {
        Assert.Equal(2.5m, QuantityRules.FromTenths(25));
        Assert.Equal(8m, QuantityRules.FromTenths(80));
        Assert.Equal(0m, QuantityRules.FromTenths(0));
    }
}
