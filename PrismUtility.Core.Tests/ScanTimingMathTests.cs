using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanTimingMathTests
{
    private static readonly ScanMotorMechanicalSettings MotorSettings = new(200, 16, 8.0);

    [Fact]
    public void DeviceSettingsNormalize_UsesDefaultsForMissingValues()
    {
        var settings = new ScanDeviceSettings(
            new ScanMotorMechanicalSettings(0, 0, 0),
            null,
            new ScanMotorMechanicalSettings(400, 8, 2.0)).Normalize();

        Assert.Equal(200u, settings.GetMotorSettings(0).StepsPerRevolution);
        Assert.Equal(16u, settings.GetMotorSettings(1).Microsteps);
        Assert.Equal(400u, settings.GetMotorSettings(2).StepsPerRevolution);
        Assert.Equal(2.0, settings.GetMotorSettings(2).LeadLengthMm, 3);
    }

    [Fact]
    public void ConvertMillimetersToMotorSteps_ReturnsExpectedSteps()
    {
        var ok = ScanTimingMath.TryConvertMillimetersToMotorSteps(1.0, MotorSettings, out var steps);

        Assert.True(ok);
        Assert.Equal(400u, steps);
    }

    [Fact]
    public void ConvertMotorStepsToMillimeters_RoundTripsWholeMillimeter()
    {
        var distanceMm = ScanTimingMath.ConvertMotorStepsToMillimeters(400, MotorSettings);

        Assert.Equal(1.0, distanceMm, 6);
    }

    [Fact]
    public void ConvertMotorIntervalToMillimetersPerSecond_ReturnsExpectedSpeed()
    {
        var speedMmPerSecond = ScanTimingMath.ConvertMotorIntervalToMillimetersPerSecond(500000, MotorSettings);

        Assert.Equal(5.0, speedMmPerSecond, 6);
    }

    [Fact]
    public void ConvertMillimetersPerSecondToMotorIntervalNs_ReturnsExpectedInterval()
    {
        var ok = ScanTimingMath.TryConvertMillimetersPerSecondToMotorIntervalNs(5.0, MotorSettings, ScanDebugConstants.MotionMinIntervalNs, out var intervalNs);

        Assert.True(ok);
        Assert.Equal(500000u, intervalNs);
    }

    [Fact]
    public void ConvertMillimetersToMotorSteps_FailsBelowHalfStep()
    {
        var ok = ScanTimingMath.TryConvertMillimetersToMotorSteps(0.001, MotorSettings, out _);

        Assert.False(ok);
    }

    [Fact]
    public void ConvertMillimetersPerSecondToMotorIntervalNs_ClampsHighSpeedToMinimumInterval()
    {
        var ok = ScanTimingMath.TryConvertMillimetersPerSecondToMotorIntervalNs(4_000_000.0, MotorSettings, ScanDebugConstants.MotionMinIntervalNs, out var intervalNs);

        Assert.False(ok);
        Assert.Equal(0u, intervalNs);
    }

    [Fact]
    public void ConvertMillimetersToMotorSteps_FailsOnOverflow()
    {
        var ok = ScanTimingMath.TryConvertMillimetersToMotorSteps(100_000_000.0, MotorSettings, out _);

        Assert.False(ok);
    }

    [Fact]
    public void ConvertMotorIntervalToStepsPerSecond_PreservesLegacyStepsMode()
    {
        var stepsPerSecond = ScanTimingMath.ConvertMotorIntervalToStepsPerSecond(500000);

        Assert.Equal(2000.0, stepsPerSecond, 6);
    }

    [Fact]
    public void ConvertMotorIntervalToStepsPerSecond_RetainsArbitraryIntervalWhenFormattedUpward()
    {
        const uint intervalNs = 333000;
        var exactStepsPerSecond = 1_000_000_000m / intervalNs;
        var roundedUpStepsPerSecond = decimal.Round(exactStepsPerSecond + 0.0000000005m, 9, MidpointRounding.ToPositiveInfinity);
        var parsed = double.Parse(roundedUpStepsPerSecond.ToString("0.#########"), System.Globalization.CultureInfo.InvariantCulture);
        var recoveredIntervalNs = (uint)Math.Ceiling(1_000_000_000.0 / parsed);

        Assert.Equal(intervalNs, recoveredIntervalNs);
    }

    [Fact]
    public void ConvertMotorIntervalToLineDistanceMillimeters_RoundTripsThroughLineDistance()
    {
        const ushort exposureTicks = 1000;
        const uint sysClockKhz = 100_000;
        const uint intervalNs = 500000;

        var lineDistanceMm = ScanTimingMath.ConvertMotorIntervalToLineDistanceMillimeters(intervalNs, exposureTicks, sysClockKhz, MotorSettings);
        var ok = ScanTimingMath.TryConvertLineDistanceMillimetersToMotorIntervalNs(lineDistanceMm, exposureTicks, sysClockKhz, MotorSettings, ScanDebugConstants.MotionMinIntervalNs, out var recoveredIntervalNs);

        Assert.True(ok);
        Assert.Equal(intervalNs, recoveredIntervalNs);
    }
}
