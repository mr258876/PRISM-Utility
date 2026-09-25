using System.Globalization;
using System.Xml.Linq;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "ScanDebugRawSignalUI")]
public sealed class ScanDebugRawSignalUiSourceTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void Stage03A_ModeSelector_keepsImagePreviewWaterfallAndRunControls()
    {
        var page = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var preview = Named(page, "WorkbenchPreviewColumnContent");
        var runBar = Named(page, "WorkbenchRunBar");
        var selector = Named(page, "RawSignalModeSelector");

        Assert.Contains(selector, preview.Descendants());
        Assert.Equal("RawSignalModeSelector_SelectionChanged", (string?)selector.Attribute("SelectionChanged"));
        Assert.Equal("True", (string?)Named(page, "ImagePreviewModeItem").Attribute("IsSelected"));
        Assert.Contains(Named(page, "RawSignalProfileModeItem"), selector.Descendants());
        Assert.Contains(Named(page, "PreviewCanvasControl"), Named(page, "PreviewScrollViewer").Descendants());
        Assert.Contains(Named(page, "WaterfallPreviewOptionsPanel"), preview.Descendants());
        Assert.Contains(Named(page, "RawSignalCanvasControl"), Named(page, "RawSignalProfileScrollViewer").Descendants());
        Assert.Contains(Named(page, "RawSignalProfileScrollViewer"), preview.Descendants());
        Assert.Equal("Collapsed", (string?)Named(page, "RawSignalProfileScrollViewer").Attribute("Visibility"));
        foreach (var command in new[] { "StartScanCommand", "StopScanCommand", "StopAllMotorsCommand" })
            Assert.Contains(runBar.Descendants(), element =>
                (string?)element.Attribute("Command") == $"{{x:Bind ViewModel.{command}}}");
        Assert.Contains(Named(page, "WorkbenchInspectionRail"), Named(page, "WorkbenchContentSplitGrid").Descendants());
    }

    [Fact]
    public void Stage03A_ModeSelector_sharesPreviewTitleRowWithoutWrappingToolbar()
    {
        var page = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var preview = Named(page, "WorkbenchPreviewColumnContent");
        var content = Assert.Single(preview.Elements(), element => element.Name.LocalName == "Grid");
        var header = Assert.Single(content.Elements(), element => element.Name.LocalName == "Grid" && (string?)element.Attribute("Grid.Row") == "0");
        var titleRow = Assert.Single(header.Elements(), element => element.Name.LocalName == "Grid" && (string?)element.Attribute("Grid.Row") == "0");
        var toolbar = Assert.Single(header.Elements(), element => element.Name.LocalName == "WrapPanel" && (string?)element.Attribute("Grid.Row") == "1");
        var selector = Named(page, "RawSignalModeSelector");

        Assert.Equal(2, header.Element(header.Name.Namespace + "Grid.RowDefinitions")!.Elements().Count());
        Assert.Equal(new[] { "*", "Auto" }, titleRow.Element(titleRow.Name.Namespace + "Grid.ColumnDefinitions")!
            .Elements().Select(column => (string?)column.Attribute("Width")));
        Assert.Equal("{StaticResource ScanDebugInlineSpacing}", (string?)titleRow.Attribute("ColumnSpacing"));
        Assert.Single(titleRow.Elements(), element => element.Name.LocalName == "TextBlock" && (string?)element.Attribute(XamlNamespace + "Uid") == "ScanDebug_Preview");
        Assert.Same(titleRow, selector.Parent);
        Assert.Equal("1", (string?)selector.Attribute("Grid.Column"));
        Assert.DoesNotContain(toolbar.Descendants(), element => element.Name.LocalName == "SelectorBar");
        Assert.Same(toolbar, Named(page, "BackToEditorButton").Parent);
        Assert.Same(toolbar, Named(page, "ZoomScaleComboBox").Parent);
        foreach (var id in new[] { "ZoomOutButton", "ZoomInButton", "PreviewDisplayToolsButton", "OverlayToolsButton", "WorkbenchDataExportButton" })
        {
            var tool = Identified(toolbar, id);
            Assert.Same(toolbar, tool.Parent);
            Assert.Null(tool.Attribute("Visibility"));
        }
    }

    [Fact]
    public void Stage03A_ProfileBinding_publishesLiveStatusAndHidesStaleResultDetails()
    {
        var page = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var code = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var profile = Named(page, "RawSignalProfileScrollViewer");
        var chart = Named(page, "RawSignalCanvasControl");
        var rowInput = Named(page, "RawSignalRowTextBox");
        var details = Named(page, "RawSignalResultDetails");

        Assert.Equal("{x:Bind ViewModel.RawSignalStatusText, Mode=OneWay}",
            (string?)Named(page, "RawSignalStatusTextBlock").Attribute("Text"));
        Assert.Equal("Polite", (string?)Named(page, "RawSignalStatusTextBlock").Attribute("AutomationProperties.LiveSetting"));
        Assert.Equal("{x:Bind ViewModel.RawSignalRowInput, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
            (string?)rowInput.Attribute("Text"));
        Assert.Equal("{x:Bind ViewModel.RawSignalResult, Mode=OneWay}", (string?)chart.Attribute("Tag"));
        Assert.Equal("Collapsed", (string?)details.Attribute("Visibility"));
        Assert.Contains(details, profile.Descendants());
        Assert.Equal("{x:Bind ViewModel.RawSignalSourceText, Mode=OneWay}",
            (string?)Identified(details, "RawSignalSourceText").Attribute("Text"));
        Assert.Equal("{x:Bind ViewModel.RawSignalStatisticsText, Mode=OneWay}",
            (string?)Identified(details, "RawSignalStatisticsText").Attribute("Text"));
        Assert.Equal("Raw", (string?)chart.Attribute("AutomationProperties.AccessibilityView"));

        var mode = Between(code, "private void UpdateRawSignalMode()", "private void UpdateWorkbenchReviewEntry()");
        Assert.Contains("PreviewScrollViewer.Visibility = ToVisibility(!isProfile);", mode, StringComparison.Ordinal);
        Assert.Contains("RawSignalProfileScrollViewer.Visibility = ToVisibility(isProfile);", mode, StringComparison.Ordinal);
        Assert.Contains("CancelPreviewVisualInteraction();", mode, StringComparison.Ordinal);
        Assert.Contains("RawSignalResultDetails.Visibility = ToVisibility(hasResult);", mode, StringComparison.Ordinal);
        Assert.Contains("RawSignalInspectionDetails.Visibility = ToVisibility(hasResult", mode, StringComparison.Ordinal);
        Assert.Contains("RawSignalCanvasControl.Invalidate();", mode, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", mode, StringComparison.Ordinal);
        Assert.DoesNotContain("StartScanCommand", mode, StringComparison.Ordinal);
        Assert.Contains("nameof(ScanDebugViewModel.RawSignalResult)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage03A_Chart_reducesFullRowOnlyForDrawingAndPlotsPublishedHistogram()
    {
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var chart = Between(page, "private void RawSignalCanvasControl_Draw(", "private void PreviewCanvasControl_Draw(");

        Assert.Contains("var result = ViewModel.RawSignalResult;", chart, StringComparison.Ordinal);
        Assert.Contains("var profile = result.Profile;", chart, StringComparison.Ordinal);
        Assert.Contains("ScanRawSignalAnalyzer.ReduceProfile(profile,", chart, StringComparison.Ordinal);
        Assert.Contains("bucket.Minimum", chart, StringComparison.Ordinal);
        Assert.Contains("bucket.Maximum", chart, StringComparison.Ordinal);
        Assert.Contains("result.Histogram.Max()", chart, StringComparison.Ordinal);
        Assert.Contains("result.Histogram[bin]", chart, StringComparison.Ordinal);
        Assert.Contains("ushort.MaxValue", chart, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewFrame", chart, StringComparison.Ordinal);
        Assert.DoesNotContain("result.Mean", chart, StringComparison.Ordinal);
        Assert.DoesNotContain("FullScaleCount", chart, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage03A_Locales_includeMatchingCaptureContextStatesAndStatisticsFormats()
    {
        var english = XDocument.Parse(ReadSource("PRISM Utility", "Strings", "en-us", "Resources.resw"));
        var chinese = XDocument.Parse(ReadSource("PRISM Utility", "Strings", "zh-CN", "Resources.resw"));
        var resourceNames = new[]
        {
            "ScanDebug_ImagePreviewModeItem.Text", "ScanDebug_RawSignalProfileModeItem.Text",
            "ScanDebug_RawSignalRowTextBox.Header", "ScanDebug_RawSignalRowTextBox.PlaceholderText",
            "ScanDebug_RawSignalProfileAxis.Text", "ScanDebug_RawSignalHistogramAxis.Text",
            "ScanDebug_RawSignalSourceLabel.Text", "ScanDebug_RawSignalStatisticsLabel.Text",
            "ScanDebug_RawSignalUnavailable", "ScanDebug_RawSignalCalculating", "ScanDebug_RawSignalReady",
            "ScanDebug_RawSignalInvalidRow", "ScanDebug_RawSignalInvalidRoi",
            "ScanDebug_RawSignalProcessedUnavailable", "ScanDebug_RawSignalUnavailableSource",
            "ScanDebug_RawSignalUnavailableChannel", "ScanDebug_RawSignalUnavailableWorkflowStream",
            "ScanDebug_RawSignalUnavailableComposite",
            "ScanDebug_RawSignalUnavailablePreview", "ScanDebug_RawSignalUnavailableData",
            "ScanDebug_RawSignalSource", "ScanDebug_RawSignalCompositeCoordinates",
            "ScanDebug_RawSignalStatistics"
        };

        foreach (var name in resourceNames)
        {
            Assert.False(string.IsNullOrWhiteSpace(Resource(english, name)));
            Assert.False(string.IsNullOrWhiteSpace(Resource(chinese, name)));
        }

        foreach (var locale in new[] { english, chinese })
        {
            var sourceFormat = Resource(locale, "ScanDebug_RawSignalSource");
            for (var index = 0; index <= 7; index++)
                Assert.Contains($"{{{index}}}", sourceFormat, StringComparison.Ordinal);
            var source = string.Format(CultureInfo.InvariantCulture, sourceFormat,
                "capture-42", -1, "Red", Resource(locale, "ScanDebug_CaptureUnknown"), 12, 10, 19, 13);
            var workflowSource = string.Format(CultureInfo.InvariantCulture, sourceFormat,
                "capture-42", 1, "Green", 7, 12, 10, 19, 13);
            var statistics = string.Format(CultureInfo.InvariantCulture, Resource(locale, "ScanDebug_RawSignalStatistics"),
                10, 0, ushort.MaxValue, 32767.5, 1);

            Assert.Contains("capture-42", source, StringComparison.Ordinal);
            Assert.Contains("Red", source, StringComparison.Ordinal);
            Assert.Contains(Resource(locale, "ScanDebug_CaptureUnknown"), source, StringComparison.Ordinal);
            Assert.Contains("7", workflowSource, StringComparison.Ordinal);
            Assert.Contains("12", source, StringComparison.Ordinal);
            Assert.Contains("10", source, StringComparison.Ordinal);
            Assert.Contains("19", source, StringComparison.Ordinal);
            Assert.Contains("32767.50", statistics, StringComparison.Ordinal);
            Assert.Contains("65535", statistics, StringComparison.Ordinal);
            Assert.Contains("1/10", statistics, StringComparison.Ordinal);
        }

        Assert.Contains("not composite image pixels", Resource(english, "ScanDebug_RawSignalCompositeCoordinates"), StringComparison.Ordinal);
        Assert.Contains("非合成图像像素坐标", Resource(chinese, "ScanDebug_RawSignalCompositeCoordinates"), StringComparison.Ordinal);
        Assert.DoesNotContain("原始遍次坐标", Resource(english, "ScanDebug_RawSignalCompositeCoordinates"), StringComparison.Ordinal);
        Assert.DoesNotContain("not composite image pixels", Resource(chinese, "ScanDebug_RawSignalCompositeCoordinates"), StringComparison.Ordinal);

        var worker = ReadSource("PRISM Utility", "ViewModels", "ScanDebugViewModel.RawSignal.Worker.cs");
        Assert.Contains("\"ScanDebug_RawSignalCompositeCoordinates\".GetLocalized()", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("原始遍次坐标", worker, StringComparison.Ordinal);
    }

    private static XElement Named(XDocument document, string name)
        => Assert.Single(document.Descendants(), element => (string?)element.Attribute(XamlNamespace + "Name") == name);

    private static XElement Identified(XElement scope, string id)
        => Assert.Single(scope.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == id);

    private static string Resource(XDocument document, string name)
        => Assert.Single(document.Descendants("data"), element => (string?)element.Attribute("name") == name)
            .Element("value")!.Value;

    private static string Between(string source, string start, string end)
    {
        var first = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(first >= 0);
        var last = source.IndexOf(end, first, StringComparison.Ordinal);
        Assert.True(last > first);
        return source[first..last];
    }

    private static string ReadSource(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PRISM Utility.sln")))
                return File.ReadAllText(Path.Combine(directory.FullName, Path.Combine(parts)));
        }

        throw new DirectoryNotFoundException("PRISM Utility.sln was not found above the test output directory.");
    }
}
