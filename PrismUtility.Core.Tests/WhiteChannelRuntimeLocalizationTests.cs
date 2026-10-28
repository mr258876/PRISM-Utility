using PRISM_Utility.Helpers;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class WhiteChannelRuntimeLocalizationTests
{
    [Theory]
    [InlineData("DedicatedLed", "Dedicated white LED")]
    [InlineData("RgbComposite", "RGB combined light")]
    public void WhiteCaptureProgressUsesReadableSourceForBothSurfaces(string source, string expectedLabel)
    {
        var message = $"Pass 5/5: White ({source}) active, capturing 128 row(s)...";

        var scan = ScanRuntimeMessageLocalizer.LocalizeScanViewStatus(message);
        var workbench = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(message);

        Assert.Equal($"Pass 5/5: White ({expectedLabel}), capturing 128 rows...", scan);
        Assert.Equal(scan, workbench);
        Assert.DoesNotContain("LED5", scan);
    }

    [Theory]
    [InlineData("Red")]
    [InlineData("Green")]
    [InlineData("Blue")]
    [InlineData("White")]
    public void MissingWhiteSourceMappingUsesSharedLocalizedMessage(string role)
    {
        var message = $"The selected white illumination source requires exactly one mapped {role} LED.";

        var localized = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(message);

        Assert.Contains("The selected white light source requires exactly one", localized);
        Assert.DoesNotContain("illumination source", localized);
        Assert.Contains($"Scan_Runtime_ChannelRole{role}", localized);
    }
}
