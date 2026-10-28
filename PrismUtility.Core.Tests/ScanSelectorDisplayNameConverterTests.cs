using PRISM_Utility.Helpers;
using PRISM_Utility.Core.Models;
using Xunit;
using static PrismUtility.Core.Tests.FilmProfileContractSource;

namespace PrismUtility.Core.Tests;

public sealed class ScanSelectorDisplayNameConverterTests
{
    public ScanSelectorDisplayNameConverterTests()
        => AppContext.SetSwitch("PRISM.Utility.UseResourceFallbacks", true);

    [Theory]
    [InlineData("Motor1", "Motor", "Motor 1 (M1)")]
    [InlineData("Motor2", "Motor", "Motor 2 (M2)")]
    [InlineData("Motor3", "Motor", "Motor 3 (M3)")]
    [InlineData("Dir0", "MotorDirection", "Direction 0 (Dir0)")]
    [InlineData("Dir1", "MotorDirection", "Direction 1 (Dir1)")]
    public void MotorAndDirectionDisplayKeepsTheActualIdentifierWithoutAssigningAPhysicalRole(
        string token,
        string parameter,
        string expectedDisplay)
    {
        var converter = new ScanSelectorDisplayNameConverter();

        var display = converter.Convert(token, typeof(string), parameter, "en-us");

        Assert.Equal(expectedDisplay, display);
        Assert.NotEqual(token, display);
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(display, typeof(string), parameter, "en-us"));
    }

    [Theory]
    [InlineData("steps", "MotorUnit", "steps")]
    [InlineData("um", "MotorUnit", "μm")]
    [InlineData("mm", "MotorUnit", "mm")]
    [InlineData("steps", "MotorRateUnit", "steps/s")]
    [InlineData("um", "MotorRateUnit", "μm/s")]
    [InlineData("mm", "MotorRateUnit", "mm/s")]
    [InlineData("steps/s", "MotorUnit", "steps/s")]
    [InlineData("um/s", "MotorUnit", "μm/s")]
    [InlineData("mm/s", "MotorUnit", "mm/s")]
    public void UnitTemplatesFormatDisplayWithoutReplacingTheUnderlyingSelection(
        string selectedToken,
        string parameter,
        string expectedDisplay)
    {
        var converter = new ScanSelectorDisplayNameConverter();
        var selection = new[] { selectedToken };

        var display = converter.Convert(selection[0], typeof(string), parameter, "en-us");

        Assert.Equal(expectedDisplay, display);
        Assert.Same(selectedToken, selection[0]);
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(display, typeof(string), parameter, "en-us"));
    }

    [Theory]
    [InlineData("Motor4", "Motor")]
    [InlineData("Dir2", "MotorDirection")]
    [InlineData("custom", "MotorUnit")]
    [InlineData("custom/s", "MotorRateUnit")]
    [InlineData("steps", "UnrecognizedParameter")]
    public void UnrecognizedValuesRemainIntact(string token, string parameter)
    {
        var converter = new ScanSelectorDisplayNameConverter();

        Assert.Same(token, converter.Convert(token, typeof(string), parameter, "en-us"));
    }

    [Theory]
    [InlineData("Dir0", 0, false, "Focus positive (Z+)")]
    [InlineData("Dir1", 0, false, "Focus negative (Z−)")]
    [InlineData("Dir0", 2, false, "Focus positive (Z+)")]
    [InlineData("Dir1", 2, true, "Focus positive (Z+)")]
    [InlineData("Dir0", 2, true, "Focus negative (Z−)")]
    public void ManualFocusDirectionsFollowTheSavedZMapping(
        string token, byte motorId, bool zPositiveDirection, string expected)
    {
        var mapping = new ScanFocusMotorMapping(ZPositiveDirection: zPositiveDirection);

        Assert.Equal(expected, ScanSelectorDisplayNameConverter.GetMappedMotorDirectionDisplayName(
            token, motorId, mapping, transportMotorId: null));
    }

    [Theory]
    [InlineData("Dir0", "Film reverse")]
    [InlineData("Dir1", "Film forward")]
    public void ManualTransportDirectionsUseTheProtocolDirectionIndependentlyOfScanStartChoice(
        string token, string expected)
        => Assert.Equal(expected, ScanSelectorDisplayNameConverter.GetMappedMotorDirectionDisplayName(
            token, motorId: 1, new ScanFocusMotorMapping(), transportMotorId: 1));

    [Fact]
    public void SharedMotorDisplaysBothRolesAndInvalidMappingsDoNotInventFocusDirections()
    {
        Assert.Equal("Focus positive (Z+) · Film reverse",
            ScanSelectorDisplayNameConverter.GetMappedMotorDirectionDisplayName(
                "Dir0", motorId: 0, new ScanFocusMotorMapping(), transportMotorId: 0));
        Assert.Equal("Direction 0",
            ScanSelectorDisplayNameConverter.GetMappedMotorDirectionDisplayName(
                "Dir0", motorId: 0, new ScanFocusMotorMapping(LeftMotorId: 0, RightMotorId: 0), transportMotorId: 1));
        Assert.Equal("Direction 1",
            ScanSelectorDisplayNameConverter.GetMappedMotorDirectionDisplayName(
                "Dir1", motorId: 2, focusMapping: null, transportMotorId: 1));
    }

    [Fact]
    public void UpdatingDirectionDisplayPreservesTheProtocolTokenAndNotifiesTheExistingItem()
    {
        var option = new ScanMotorDirectionOption("Dir0");
        var changedProperties = new List<string?>();
        option.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        option.DisplayName = "Focus positive (Z+)";
        option.DisplayName = "Focus negative (Z−)";

        Assert.Equal("Dir0", option.Token);
        Assert.Equal("Focus negative (Z−)", option.DisplayName);
        Assert.Equal([nameof(ScanMotorDirectionOption.DisplayName), nameof(ScanMotorDirectionOption.DisplayName)], changedProperties);
    }

    [Fact]
    public void BothLocalesKeepMotorNumbersAndRawDirectionIdentifiersInTheLabels()
    {
        foreach (var (culture, motorPrefix, direction0, direction1) in new[]
        {
            ("en-us", "Motor", "Direction 0 (Dir0)", "Direction 1 (Dir1)"),
            ("zh-CN", "电机", "方向 0（Dir0）", "方向 1（Dir1）")
        })
        {
            var resources = ReadResources(culture);
            for (var motorId = 1; motorId <= 3; motorId++)
            {
                var expected = $"{motorPrefix} {motorId} (M{motorId})";
                Assert.Equal(expected, resources[$"Scan_Runtime_MotorOption{motorId}"]);
                Assert.Equal(expected, resources[$"ScanDebug_Motor{motorId}AxisComboBoxItem.Content"]);
            }

            Assert.Equal(direction0, resources["ScanDebug_Runtime_MotorDirectionDir0"]);
            Assert.Equal(direction1, resources["ScanDebug_Runtime_MotorDirectionDir1"]);
        }
    }
}
