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
    public void PreviewToolbar_ExposesZoomControlsAndKeepsSecondaryToolsInTheFlyout()
    {
        var page = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var preview = Named(page, "WorkbenchPreviewColumnContent");
        var interactionHost = Named(page, "WorkbenchPreviewInteractionHost");
        Assert.Same(preview, interactionHost.Parent);
        var content = Assert.Single(interactionHost.Elements(), element => element.Name.LocalName == "Grid");
        var header = Assert.Single(content.Elements(), element => element.Name.LocalName == "Grid" && (string?)element.Attribute("Grid.Row") == "0");
        var toolbar = Named(page, "PreviewToolbar");
        var selector = Named(page, "RawSignalModeSelector");
        var auxiliary = Named(page, "PreviewAuxiliaryToolsButton");

        Assert.Same(header, selector.Parent);
        foreach (var name in new[] { "FitPreviewButton", "ActualSizePreviewButton", "ZoomOutButton", "ZoomInButton", "ZoomScaleComboBox" })
        {
            var tool = Named(page, name);
            Assert.Contains(tool, toolbar.Descendants());
            Assert.DoesNotContain(tool, auxiliary.Descendants());
            Assert.NotEqual("Collapsed", (string?)tool.Attribute("Visibility"));
        }
        foreach (var id in new[] { "PreviewAuxiliaryToolsButton" })
        {
            var tool = Identified(toolbar, id);
            Assert.Same(toolbar, tool.Parent);
            Assert.Null(tool.Attribute("Visibility"));
        }
        foreach (var id in new[] { "PreviewDisplayToolsButton", "OverlayToolsButton", "WorkbenchDataExportButton", "ObservationSelectionButton" })
        {
            var tool = Identified(auxiliary, id);
            Assert.Contains(tool, auxiliary.Descendants());
        }
        Assert.Equal("Flyout", Assert.Single(auxiliary.Descendants(), element => element.Name.LocalName == "Flyout" &&
            element.Descendants().Any(descendant => (string?)descendant.Attribute("AutomationProperties.AutomationId") == "WorkbenchDataExportButton")).Name.LocalName);
    }

    [Fact]
    public void ProfileMode_KeepsImageVisibleWhileShowingTheSamplingChart()
    {
        var page = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var code = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var profile = Named(page, "RawSignalProfileScrollViewer");
        var chart = Named(page, "RawSignalCanvasControl");
        var rowInput = Named(page, "RawSignalRowTextBox");
        var details = Named(page, "RawSignalResultDetails");

        Assert.Equal("{x:Bind ViewModel.RawSignalPresentationText, Mode=OneWay}",
            (string?)Named(page, "RawSignalStatusTextBlock").Attribute("Text"));
        Assert.Equal("{x:Bind ViewModel.RawSignalSelectionHintText, Mode=OneWay}",
            (string?)Named(page, "RawSignalStatusTextBlock").Attribute("ToolTipService.ToolTip"));
        Assert.Equal("Polite", (string?)Named(page, "RawSignalStatusTextBlock").Attribute("AutomationProperties.LiveSetting"));
        Assert.Equal("{x:Bind ViewModel.RawSignalDisplayRowInput, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
            (string?)rowInput.Attribute("Text"));
        Assert.Equal("{x:Bind ViewModel.ObservationRawSignalResult, Mode=OneWay}", (string?)chart.Attribute("Tag"));
        Assert.Equal("Collapsed", (string?)details.Attribute("Visibility"));
        Assert.Contains(details, profile.Descendants());
        Assert.Equal("{x:Bind ViewModel.ObservationRawSourceText, Mode=OneWay}",
            (string?)Identified(Named(page, "WorkbenchInspectionRail"), "RawSignalSourceText").Attribute("Text"));
        Assert.Equal("{x:Bind ViewModel.ObservationRawStatisticsText, Mode=OneWay}",
            (string?)Identified(details, "RawSignalStatisticsText").Attribute("Text"));
        Assert.Equal("Raw", (string?)chart.Attribute("AutomationProperties.AccessibilityView"));

        var mode = Between(code, "private void UpdateRawSignalMode()", "private void UpdateWorkbenchReviewEntry()");
        var image = Named(page, "PreviewScrollViewer");
        Assert.Same(image.Parent, profile.Parent);
        Assert.NotEqual((string?)image.Attribute("Grid.Row"), (string?)profile.Attribute("Grid.Row"));
        Assert.DoesNotContain("PreviewScrollViewer.Visibility = ToVisibility(!isProfile);", mode, StringComparison.Ordinal);
        Assert.Contains("RawSignalProfileScrollViewer.Visibility = ToVisibility(isProfile);", mode, StringComparison.Ordinal);
        Assert.Contains("CancelPreviewVisualInteraction();", mode, StringComparison.Ordinal);
        Assert.Contains("RawSignalResultDetails.Visibility = ToVisibility(hasResult);", mode, StringComparison.Ordinal);
        Assert.Contains("RawSignalCanvasControl.Invalidate();", mode, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", mode, StringComparison.Ordinal);
        Assert.DoesNotContain("StartScanCommand", mode, StringComparison.Ordinal);
        Assert.Contains("nameof(ScanDebugViewModel.ObservationRawSignalResult)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void LineSelection_ExposesPositionAndFollowModeAndDisablesUnsampledPrecisionInput()
    {
        var page = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var profile = Named(page, "RawSignalProfileScrollViewer");
        var precision = Named(page, "RawSignalRowTextBox");
        var follow = Named(page, "RawSignalFollowLatestToggleSwitch");
        var image = Named(page, "PreviewScrollViewer");

        Assert.Contains(profile.Descendants(), element =>
            (string?)element.Attribute("Text") == "{x:Bind ViewModel.RawSignalPositionText, Mode=OneWay}");
        Assert.Equal("{x:Bind ViewModel.IsRawSignalFollowingLatest, Mode=TwoWay}", (string?)follow.Attribute("IsOn"));
        foreach (var control in new[] { follow, precision })
            Assert.Equal("{x:Bind ViewModel.CanSelectRawSignalRow, Mode=OneWay}", (string?)control.Attribute("IsEnabled"));
        Assert.Contains(precision.Ancestors(), element => element.Name.LocalName == "Flyout");
        Assert.Equal("True", (string?)image.Attribute("IsTabStop"));
        Assert.Equal("LineProfilePreview_KeyDown", (string?)image.Attribute("PreviewKeyDown"));
    }

    [Fact]
    public void RawChartKeepsStatisticsInItsDetailsWithoutInspectionDuplicates()
    {
        var page = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var code = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var chart = Named(page, "RawSignalCanvasControl");
        var details = Named(page, "RawSignalResultDetails");
        var chartRows = details.Element(details.Name.Namespace + "Grid.RowDefinitions")!;
        var raw = Named(page, "RawSignalProfileScrollViewer");
        var inspection = Named(page, "WorkbenchInspectionRail");
        var update = Between(code, "private void UpdateRawSignalResultSurface()", "private void RawSignalProfileScrollViewer_SizeChanged(");

        Assert.Null(chart.Attribute("Height"));
        Assert.Equal(new[] { "Auto", "*", "Auto" }, chartRows.Elements().Select(row => (string?)row.Attribute("Height")));
        Assert.Equal("Auto", (string?)raw.Attribute("VerticalScrollMode"));
        Assert.Contains(Named(page, "RawSignalProfileGrid"), raw.Descendants());
        Assert.Contains(Identified(details, "RawSignalStatisticsText"), Identified(details, "RawSignalDetailsExpander").Descendants());
        Assert.Single(page.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == "RawSignalStatisticsText");



        Assert.Contains("var result = ViewModel.ObservationRawSignalResult;", update, StringComparison.Ordinal);
        Assert.DoesNotContain(inspection.Descendants(), element => ((string?)element.Attribute(XamlNamespace + "Name"))?.StartsWith("RawSignalInspection") == true);
        Assert.Equal("{x:Bind ViewModel.ObservationRawStatisticsText, Mode=OneWay}", (string?)Identified(details, "RawSignalStatisticsText").Attribute("Text"));
        Assert.DoesNotContain("RawSignalStatisticsText.Split", update, StringComparison.Ordinal);
        Assert.DoesNotContain("RawSignalSourceText.Split", update, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage03A_Chart_reducesFullRowOnlyForDrawingAndPlotsPublishedHistogram()
    {
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var chart = Between(page, "private void RawSignalCanvasControl_Draw(", "private void PreviewCanvasControl_Draw(");

        Assert.Contains("var result = ViewModel.ObservationRawSignalResult;", chart, StringComparison.Ordinal);
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
            "ScanDebug_RawSignalStatistics", "ScanDebug_RawSignalPosition",
            "ScanDebug_RawSignalSelectEmpty", "ScanDebug_RawSignalSelectSnapshot",
            "ScanDebug_RawSignalSelectAveragedWaterfall", "ScanDebug_RawSignalSelectComposite",
            "ScanDebug_RawSignalSelectWaterfall", "ScanDebug_RawSignalSelectImage",
            "ScanDebug_RawSignalSamplingLine"
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
