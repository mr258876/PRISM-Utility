using System.Globalization;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanAdcOffsetGuidanceTests
{
    [Theory]
    [InlineData(1, -256, false)]
    [InlineData(1, -255, true)]
    [InlineData(1, 255, true)]
    [InlineData(1, 256, false)]
    [InlineData(2, -256, false)]
    [InlineData(2, -255, true)]
    [InlineData(2, 255, true)]
    [InlineData(2, 256, false)]
    public void TryParseInput_ValidatesEachAdcOffsetBoundaryWithoutDeviceIo(int adc, int value, bool expectedValid)
    {
        var service = new ScanParameterService(new ScanProtocolService());
        var input = value.ToString(CultureInfo.InvariantCulture);
        var parsed = service.TryParseInput("1000", adc == 1 ? input : "0", "12", adc == 2 ? input : "0", "15", "125", out var snapshot, out var error);

        Assert.Equal(expectedValid, parsed);
        if (expectedValid)
        {
            Assert.Equal(adc == 1 ? value : 0, snapshot.Adc1Offset);
            Assert.Equal(adc == 2 ? value : 0, snapshot.Adc2Offset);
            Assert.Equal(string.Empty, error);
        }
        else
        {
            Assert.Contains($"ADC{adc} Offset", error, StringComparison.Ordinal);
            Assert.Contains("[-255, +255]", error, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("en-us", "e.g. -10")]
    [InlineData("zh-CN", "如 -10")]
    public void AdcOffsetPlaceholders_AreLocalizedShortExamplesWithinTheExistingLimit(string culture, string expectedPlaceholder)
    {
        var resources = FilmProfileContractSource.ReadResources(culture);
        var service = new ScanParameterService(new ScanProtocolService());

        foreach (var adc in new[] { 1, 2 })
        {
            var placeholder = resources[$"ScanDebug_Adc{adc}OffsetTextBox.PlaceholderText"];
            var limitLabel = resources[$"ScanDebug_Runtime_LimitLabelAdc{adc}Offset"];
            var hint = string.Format(CultureInfo.GetCultureInfo(culture), resources["ScanDebug_Runtime_LimitBounded"], limitLabel, -255, 255);

            Assert.Equal(expectedPlaceholder, placeholder);
            Assert.True(placeholder.Length <= 8);
            Assert.Contains("-255 ~ 255", hint, StringComparison.Ordinal);
            Assert.DoesNotContain("256", hint, StringComparison.Ordinal);
        }

        var example = expectedPlaceholder[(expectedPlaceholder.LastIndexOf(' ') + 1)..];
        Assert.True(service.TryParseInput("1000", example, "12", example, "15", "125", out var snapshot, out var error), error);
        Assert.Equal(-10, snapshot.Adc1Offset);
        Assert.Equal(-10, snapshot.Adc2Offset);
    }
}
