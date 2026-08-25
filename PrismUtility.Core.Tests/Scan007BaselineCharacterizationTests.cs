using System.Globalization;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class Scan007BaselineCharacterizationTests
{
    [Fact]
    public void Scan007Baseline_ProtocolModelsAndTimingMath_PreserveNanoseconds()
    {
        const uint intervalNs = 1_000;
        var request = new ScanWorkflowRequest(1, false, [], [], [], 0, intervalNs, true, false, 0, 0);
        var result = new ScanWorkflowResult(1, [], 0, intervalNs, 0, 0);
        var acquisition = new ScanFilmAcquisitionSettings(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, intervalNs);
        var state = new ScanMotorState(0, false, false, false, 0, intervalNs, 0);
        var autofocus = new ScanAutofocusRequest(1, 1, 1, intervalNs, true, true, 1, 1, ScanCalibrationRoiSettings.CreateDefault());

        Assert.Equal(intervalNs, request.MotorIntervalNs);
        Assert.Equal(intervalNs, result.MotorIntervalNs);
        Assert.Equal(intervalNs, acquisition.MotorIntervalNs);
        Assert.Equal(intervalNs, state.IntervalNs);
        Assert.Equal(intervalNs, autofocus.MotorIntervalNs);
        Assert.Equal(1_000_000d, ScanTimingMath.ConvertMotorIntervalToStepsPerSecond(intervalNs));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", true)]
    [InlineData("1.5", false)]
    [InlineData("-1", false)]
    [InlineData("4294967296", false)]
    [InlineData("not-a-number", false)]
    public void Scan007Baseline_UiMicrosecondInputConvention_AcceptsOnlyInvariantUnsignedWholeNumbers(string text, bool expected)
    {
        var accepted = uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

        Assert.Equal(expected, accepted);
    }
}
