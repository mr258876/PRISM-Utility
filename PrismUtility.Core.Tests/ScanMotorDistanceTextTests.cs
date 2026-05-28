using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanMotorDistanceTextTests
{
    private static readonly ScanMotorMechanicalSettings MotorSettings = new(200, 16, 8.0);

    [Theory]
    [InlineData("mm", 1.25)]
    [InlineData("um", 0.00125)]
    [InlineData("steps", 0.003125)]
    public void TryParseMillimeters_ConvertsSupportedUnits(string unit, double expectedMillimeters)
    {
        var ok = ScanMotorDistanceText.TryParseMillimeters("1.25", unit, MotorSettings, out var distanceMm);

        Assert.True(ok);
        Assert.Equal(expectedMillimeters, distanceMm, 6);
    }

    [Theory]
    [InlineData("mm", "1.25")]
    [InlineData("um", "1250")]
    [InlineData("steps", "500")]
    public void TryFormatDisplayValue_ConvertsSupportedUnits(string unit, string expectedText)
    {
        var ok = ScanMotorDistanceText.TryFormatDisplayValue(1.25, unit, MotorSettings, out var valueText);

        Assert.True(ok);
        Assert.Equal(expectedText, valueText);
    }

    [Theory]
    [InlineData(null, ScanMotorDistanceText.MillimetersUnit)]
    [InlineData("", ScanMotorDistanceText.MillimetersUnit)]
    [InlineData("STEPS", ScanMotorDistanceText.StepsUnit)]
    [InlineData(" um ", ScanMotorDistanceText.MicrometersUnit)]
    public void NormalizeUnit_ReturnsStableUnit(string? unit, string expected)
        => Assert.Equal(expected, ScanMotorDistanceText.NormalizeUnit(unit));

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("abc")]
    public void TryParseMillimeters_RejectsInvalidValues(string text)
        => Assert.False(ScanMotorDistanceText.TryParseMillimeters(text, "mm", MotorSettings, out _));
}
