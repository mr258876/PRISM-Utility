using System.Xml.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static PrismUtility.Core.Tests.ScanDebugFilmProfileOrchestrationSourceTests;

namespace PrismUtility.Core.Tests;

[Trait("Category", "ScanPresentation")]
public sealed class ScanPresentationSourceContractTests
{
    [Fact]
    public void PreparationProgressAndTerminalFeedbackShareAnAlwaysVisibleStatusRegion()
    {
        var page = XDocument.Parse(ReadAppSource("Views", "ScanPage.xaml"));
        var region = Named(page, "ScanStatusStrip");
        foreach (var name in new[] { "ScanPreparationSummary", "ScanProgressPanel", "ScanRuntimeFeedbackBar" })
            Assert.Contains(region, Named(page, name).Ancestors());
        Assert.Null(region.Attribute("Visibility"));
        Assert.Contains("ViewModel.IsPreparationVisible", (string?)Named(page, "ScanPreparationSummary").Attribute("Visibility"));
        Assert.Contains("ViewModel.IsRunning", (string?)Named(page, "ScanProgressPanel").Attribute("Visibility"));
        Assert.Contains("ViewModel.HasRuntimeFeedback", (string?)Named(page, "ScanRuntimeFeedbackBar").Attribute("Visibility"));
        foreach (var name in new[] { "ScanWorkflowProgressBar", "ScanHeaderProgressRing" })
        {
            var progress = Named(page, name);
            Assert.Contains("ViewModel.ScanProgressValue", (string?)progress.Attribute("Value"));
            Assert.Contains("ViewModel.ScanProgressMaximum", (string?)progress.Attribute("Maximum"));
        }
    }

    [Fact]
    public void OutputFeedbackAndItsFolderActionRemainInsideTheScrollableResultRegion()
    {
        var page = XDocument.Parse(ReadAppSource("Views", "ScanPage.xaml"));
        var output = Named(page, "OutputPreparationCard");
        var feedback = Named(page, "ScanOutputFeedbackBar");
        var openFolder = Named(page, "ScanOpenOutputFolderButton");
        Assert.Contains(output, feedback.Ancestors());
        Assert.Contains(Named(page, "ScanResultScrollViewer"), output.Ancestors());
        Assert.Contains(feedback, openFolder.Ancestors());
        Assert.Contains("ViewModel.HasOutputFeedback", (string?)feedback.Attribute("Visibility"));
        Assert.Contains("ViewModel.OutputFeedbackText", (string?)feedback.Attribute("Message"));
        Assert.Equal("{x:Bind ViewModel.OpenLastOutputFolderCommand}", (string?)openFolder.Attribute("Command"));
        Assert.DoesNotContain(feedback.Ancestors(), element =>
            ((string?)element.Attribute("Visibility"))?.Contains("ViewModel.IsRunning", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ActionablePreparationItemsInvokeTheCorrespondingGuardedCommandOrDeviceMappingPage()
    {
        var page = XDocument.Parse(ReadAppSource("Views", "ScanPage.xaml"));
        var preparation = Named(page, "ScanPreparationSummary");
        var button = Assert.Single(preparation.Descendants().Where(element =>
            (string?)element.Attribute("Click") == "PreparationStep_Click"));
        Assert.Equal("{Binding IsEnabled}", (string?)button.Attribute("IsEnabled"));
        Assert.Contains("IsActionVisible", (string?)button.Attribute("Visibility"));

        var handler = ExtractMethod(ReadAppSource("Views", "ScanPage.xaml.cs"), "PreparationStep_Click");
        Assert.Contains("!step.IsEnabled", handler, StringComparison.Ordinal);
        Assert.Contains("ScanPreparationAction.UseSavedConfiguration", handler, StringComparison.Ordinal);
        Assert.Contains("ViewModel.UseSavedFilmProfileCommand.CanExecute(null)", handler, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null)", handler, StringComparison.Ordinal);
        Assert.Contains("ScanPreparationAction.LoadConfiguration", handler, StringComparison.Ordinal);
        Assert.Contains("ScanPreparationAction.ReplaceConfiguration", handler, StringComparison.Ordinal);
        Assert.Contains("ViewModel.LoadConfigProfileCommand.CanExecute(null)", handler, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.LoadConfigProfileCommand.ExecuteAsync(null)", handler, StringComparison.Ordinal);
        Assert.Contains("ScanPreparationAction.ReviewDeviceMappings", handler, StringComparison.Ordinal);
        Assert.Contains("OpenDeviceMappings_Click(sender, e)", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeCompletionAndSuccessfulOutputPathsPublishPersistentFeedback()
    {
        var source = ReadAppSource("ViewModels", "ScanViewModel.cs");
        var start = ExtractMethod(source, "StartScan");
        Assert.Contains("ResetScanPresentationForNewRun();", start, StringComparison.Ordinal);
        Assert.True(start.IndexOf("ResetScanPresentationForNewRun();", StringComparison.Ordinal)
                    < start.IndexOf("_workflow.ExecuteAsync", StringComparison.Ordinal));
        Assert.Equal(3, Regex.Matches(start, "RecordScanCompletion\\(").Count);
        Assert.Contains("catch (OperationCanceledException)", start, StringComparison.Ordinal);
        Assert.Contains("RecordScanCompletion(StatusText, InfoBarSeverity.Warning);", start, StringComparison.Ordinal);
        Assert.Contains("RecordScanCompletion(StatusText, InfoBarSeverity.Error);", start, StringComparison.Ordinal);

        var rgb = ExtractMethod(source, "SaveRgbImage");
        Assert.True(rgb.IndexOf("RecordOutputSuccess", StringComparison.Ordinal)
                    > rgb.IndexOf("await _channelImages.SaveRgbImageAsync", StringComparison.Ordinal));
        var dng = ExtractMethod(source, "ExportDngChannels");
        Assert.True(dng.IndexOf("RecordOutputSuccess", StringComparison.Ordinal)
                    > dng.IndexOf("await ExportCapturedDngChannelsAsync", StringComparison.Ordinal));
    }

    private static XElement Named(XDocument page, string name)
        => Assert.Single(page.Descendants().Where(element =>
            (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == name));

    [Fact]
    public void DngAlignmentFallbackPreservesItsWarningAndSeparatelyPublishesTheSuccessfulOutputLocation()
    {
        var export = ExtractMethod(ReadAppSource("ViewModels", "ScanViewModel.cs"), "ExportDngChannels");
        var warningStart = export.IndexOf("if (exportResult.HasAlignmentWarning)", StringComparison.Ordinal);
        Assert.True(warningStart >= 0);
        var warningEnd = export.IndexOf("else", warningStart, StringComparison.Ordinal);
        var warning = export[warningStart..warningEnd];
        Assert.Contains("Scan_Runtime_StatusDngExportedWithAlignmentWarning", warning, StringComparison.Ordinal);
        Assert.Contains("ShowTopRiskBanner(", warning, StringComparison.Ordinal);
        Assert.Contains("Scan_Runtime_DngAlignmentWarningText", warning, StringComparison.Ordinal);
        Assert.Matches("RecordOutputSuccess\\(folder\\.Path, false,\\s*\"Scan_Runtime_StatusDngExported\"\\.GetLocalizedFormat\\(folder\\.Path\\)\\)", warning);
        Assert.DoesNotContain("SetOutputFeedback(StatusText", warning, StringComparison.Ordinal);
    }

    private static string ReadAppSource(params string[] path)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var app = Path.Combine(directory.FullName, "PRISM Utility");
            if (Directory.Exists(app))
                return File.ReadAllText(Path.Combine(app, Path.Combine(path)));
        }
        throw new DirectoryNotFoundException("Could not locate Host Software source root.");
    }
}
