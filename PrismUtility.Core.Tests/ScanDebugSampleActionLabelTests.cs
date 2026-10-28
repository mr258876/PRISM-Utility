using System.Xml.Linq;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "ScanDebugSampleActions")]
public sealed class ScanDebugSampleActionLabelTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("en-us", "Fill black input from sample", "Fill white input from sample",
        "Save black level to channel preset", "Save white level to channel preset", "channel preset", "save")]
    [InlineData("zh-CN", "使用采样填入黑值", "使用采样填入白值",
        "保存黑电平到通道预设", "保存白电平到通道预设", "通道预设", "保存")]
    public void LocalizedButtons_DistinguishInputOnlyFromLibraryPersistence(
        string culture, string expectedBlackFill, string expectedWhiteFill,
        string expectedBlackSave, string expectedWhiteSave, string libraryTerm, string saveVerb)
    {
        var resources = FilmProfileContractSource.ReadResources(culture);
        var blackFill = resources["ScanDebug_UseSampleAsManualBlackLevelButton.Content"];
        var whiteFill = resources["ScanDebug_UseSampleAsManualWhiteLevelButton.Content"];
        var blackSave = resources["ScanDebug_SaveColumnBlackLevelButton.Content"];
        var whiteSave = resources["ScanDebug_SaveColumnWhiteLevelButton.Content"];

        Assert.Equal(expectedBlackFill, blackFill);
        Assert.Equal(expectedWhiteFill, whiteFill);
        Assert.Equal(expectedBlackSave, blackSave);
        Assert.Equal(expectedWhiteSave, whiteSave);
        foreach (var saveLabel in new[] { blackSave, whiteSave })
            Assert.Contains(libraryTerm, saveLabel, StringComparison.OrdinalIgnoreCase);
        foreach (var fillLabel in new[] { blackFill, whiteFill })
        {
            Assert.DoesNotContain(libraryTerm, fillLabel, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(saveVerb, fillLabel, StringComparison.OrdinalIgnoreCase);
        }
        Assert.NotEqual(blackFill, blackSave);
        Assert.NotEqual(whiteFill, whiteSave);
    }

    [Fact]
    public void SampleButtons_KeepSeparateFillAndSaveCommands()
    {
        var page = XDocument.Parse(FilmProfileContractSource.ReadAppText("Views", "ScanDebugPage.xaml"));
        foreach (var (uid, command) in new[]
        {
            ("ScanDebug_UseSampleAsManualBlackLevelButton", "UseColumnSampleAsManualBlackLevelCommand"),
            ("ScanDebug_UseSampleAsManualWhiteLevelButton", "UseColumnSampleAsManualWhiteLevelCommand"),
            ("ScanDebug_SaveColumnBlackLevelButton", "SaveColumnSampleAsBlackLevelCommand"),
            ("ScanDebug_SaveColumnWhiteLevelButton", "SaveColumnSampleAsWhiteLevelCommand")
        })
        {
            var button = Assert.Single(page.Descendants(), element => element.Name.LocalName == "Button"
                && (string?)element.Attribute(XamlNamespace + "Uid") == uid);
            Assert.Equal($"{{x:Bind ViewModel.{command}}}", (string?)button.Attribute("Command"));
        }
    }
}
