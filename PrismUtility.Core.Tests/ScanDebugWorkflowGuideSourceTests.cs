using System.Xml.Linq;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "ScanDebugLayout")]
public sealed class ScanDebugWorkflowGuideSourceTests
{
    [Fact]
    public void WorkflowGuide_StaysCompactAndKeepsRunActionsOutsideItsMenu()
    {
        var xaml = ReadXaml();
        var workspace = Named(xaml, "WorkbenchWorkspaceGrid");
        var guide = Named(xaml, "FilmProfileWorkflowGuide");
        var next = Named(xaml, "FilmProfileWorkflowNextButton");
        var content = Named(xaml, "WorkbenchContentSplitGrid");

        var summary = Named(xaml, "WorkbenchProfileSummaryGrid");
        Assert.DoesNotContain(guide, Named(xaml, "ConfigurationFileFlyout").Descendants());
        Assert.Contains(guide, summary.Descendants());
        Assert.DoesNotContain(guide, workspace.Descendants());
        Assert.DoesNotContain(summary, Named(xaml, "WorkbenchIdentityGrid").Descendants());
        Assert.Equal("WrapPanel", guide.Name.LocalName);
        Assert.Equal("0", (string?)Named(xaml, "WorkbenchWorkspaceHeader").Attribute("Grid.Row"));
        Assert.Equal("2", (string?)content.Attribute("Grid.Row"));
        Assert.Null(guide.Attribute("Height"));
        Assert.Null(guide.Attribute("MinHeight"));
        Assert.Equal("Button", next.Name.LocalName);
        Assert.Equal("{x:Bind ViewModel.WorkflowGuideTitle, Mode=OneWay}",
            (string?)next.Descendants().Single(element =>
                (string?)element.Attribute("AutomationProperties.AutomationId") == "FilmProfileWorkflowTitle").Attribute("Text"));
        Assert.Equal("{x:Bind ViewModel.WorkflowGuideDetail, Mode=OneWay}",
            (string?)next.Attribute("AutomationProperties.HelpText"));
        Assert.Equal("FilmProfileWorkflowNextButton_Click", (string?)next.Attribute("Click"));
        Assert.Null(next.Attribute("Command"));
        Assert.Null(next.Element(next.Name.Namespace + "Button.Flyout"));
        Assert.Same(next.Element(next.Name.Namespace + "FlyoutBase.AttachedFlyout"), Named(xaml, "FilmProfileWorkflowFlyout").Parent);

        var run = Named(xaml, "WorkbenchRunBar");
        foreach (var id in new[] { "StopScanButton", "StopAllMotorsButton" })
        {
            Assert.Contains(run.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == id);
            Assert.DoesNotContain(guide.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == id);
        }
    }

    [Fact]
    public void WorkflowOverview_ProvidesFourLocalizedNavigationOnlySteps()
    {
        var xaml = ReadXaml();
        var menu = Named(xaml, "FilmProfileWorkflowFlyout");
        var steps = menu.Descendants().Where(element => element.Name.LocalName == "Button").ToArray();
        Assert.Equal(4, steps.Length);
        Assert.Equal(new[] { "Configuration", "Calibration", "Review", "Scan" },
            steps.Select(element => (string?)element.Attribute("Tag")));
        foreach (var step in steps)
        {
            Assert.Equal("FilmProfileWorkflowStepButton_Click", (string?)step.Attribute("Click"));
            Assert.Null(step.Attribute("Command"));
            var uid = (string)step.Attribute(XName.Get("Uid", "http://schemas.microsoft.com/winfx/2006/xaml"))!;
            foreach (var locale in new[] { "en-us", "zh-CN" })
            {
                Assert.False(string.IsNullOrWhiteSpace(Resource(locale, $"{uid}.Content")));
                Assert.False(string.IsNullOrWhiteSpace(Resource(locale,
                    $"{uid}.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name")));
            }
        }
    }

    [Fact]
    public void SaveActions_KeepOneGeneralSaveAndAnExplicitScanHandoffWithDraftFallback()
    {
        var xaml = ReadXaml();
        var lifecycle = Named(xaml, "FilmProfileLifecycleActionsPanel");
        foreach (var (name, handler) in new[]
        {
            ("SaveFilmProfileDraftButton", "SaveCurrentConfigurationButton_Click"),
            ("LoadFilmProfileDraftButton", "LoadFilmProfileDraftButton_Click")
        })
        {
            var button = Named(xaml, name);
            Assert.Contains(button, lifecycle.Descendants());
            if (name == "SaveFilmProfileDraftButton")
            {
                Assert.DoesNotContain(button.Ancestors(), element => element.Name.LocalName == "Flyout");
                Assert.NotEqual("{StaticResource AccentButtonStyle}", (string?)button.Attribute("Style"));
            }
            else
            {
                Assert.Contains(button, Named(xaml, "ConfigurationFileFlyout").Descendants());
            }
            Assert.Equal(handler, (string?)button.Attribute("Click"));
            Assert.Equal("{x:Bind ViewModel.CanUseEditingDraftFiles, Mode=OneWay}",
                (string?)button.Attribute("IsEnabled"));
            Assert.Null(button.Attribute("Command"));
        }

        foreach (var locale in new[] { "en-us", "zh-CN" })
        {
            Assert.False(string.IsNullOrWhiteSpace(Resource(locale, "ScanDebug_FilmProfileDraftMenuScopeText.Text")));
            Assert.False(string.IsNullOrWhiteSpace(Resource(locale, "ScanDebug_LoadFilmProfileDraftButton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.HelpText")));
        }
        Assert.Contains("continue editing", Resource("en-us", "ScanDebug_FilmProfileDraftMenuScopeText.Text"), StringComparison.Ordinal);
        Assert.Contains("full configuration", Resource("en-us", "ScanDebug_FilmProfileDraftMenuScopeText.Text"), StringComparison.Ordinal);
        Assert.Contains("继续编辑", Resource("zh-CN", "ScanDebug_FilmProfileDraftMenuScopeText.Text"), StringComparison.Ordinal);
        Assert.Contains("完整配置", Resource("zh-CN", "ScanDebug_FilmProfileDraftMenuScopeText.Text"), StringComparison.Ordinal);
        Assert.Contains(Named(xaml, "SaveConfigurationFileButton"), Named(xaml, "ConfigurationFileFlyout").Descendants());
        var handoff = Named(xaml, "SaveAndOpenScanButton");
        Assert.DoesNotContain(handoff.Ancestors(), element => element.Name.LocalName == "Flyout");
        Assert.Equal("{StaticResource AccentButtonStyle}", (string?)handoff.Attribute("Style"));
        Assert.Equal("{x:Bind ViewModel.CanSaveFilmProfileFile, Mode=OneWay}", (string?)handoff.Attribute("IsEnabled"));
        Assert.Null(handoff.Attribute("Visibility"));
        Assert.Equal("SaveAndOpenScanButton_Click", (string?)handoff.Attribute("Click"));
        var presentation = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(
            ReadSource("PRISM Utility", "Views", "ScanDebugPage.Workflow.cs"), "UpdateConfigurationHandoffPresentation");
        Assert.Contains("var action = \"ScanDebug_SaveAndOpenScanButton/Content\".GetLocalized();", presentation, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveAndOpenScanButton.Visibility", presentation, StringComparison.Ordinal);
        var validation = Named(xaml, "WorkbenchValidationButton");
        Assert.Equal("{x:Bind ViewModel.ValidateFilmProfileCommand}", (string?)validation.Attribute("Command"));
        Assert.Contains(validation, Named(xaml, "WorkbenchProfileSummaryGrid").Descendants());
        Assert.Null(validation.Attribute("Visibility"));
        Assert.DoesNotContain("SaveAndOpenScanButton.Style", presentation, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(SaveAndOpenScanButton, action);", presentation, StringComparison.Ordinal);
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        Assert.Contains("UpdateConfigurationHandoffPresentation();", page, StringComparison.Ordinal);
        var onChanged = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "OnViewModelPropertyChanged");
        Assert.Contains("nameof(ScanDebugViewModel.CanSaveFilmProfileFile)", onChanged, StringComparison.Ordinal);
        Assert.Contains("nameof(ScanDebugViewModel.FilmProfileSaveHintText)", onChanged, StringComparison.Ordinal);
        var viewModel = ReadSource("PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        Assert.Contains("\"ScanDebug_FilmProfileWorkbenchLifecycleValidateButton/Content\".GetLocalized()", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ScanDebug_FilmProfileWorkbenchLifecycleValidateButton.Content\".GetLocalized()", viewModel, StringComparison.Ordinal);
        var saveSource = ReadSource("PRISM Utility", "Views", "ScanDebugPage.UiRefinement.cs");
        Assert.Contains("ViewModel.SaveFilmProfileJsonCommand.CanExecute(null)", saveSource, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.SaveFilmProfileJsonCommand.ExecuteAsync(null);", saveSource, StringComparison.Ordinal);
        Assert.Contains("else if (ViewModel.CanUseEditingDraftFiles)", saveSource, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.SaveEditingDraftAsync(App.GetService<IScanFilmProfileFileGateway>());", saveSource, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowRecommendation_CopyIsPairedAndRetainsTheIssueCount()
    {
        foreach (var step in new[] { "Setup", "Calibration", "Review", "Save" })
        foreach (var field in new[] { "Title", "Detail", "Action" })
        foreach (var locale in new[] { "en-us", "zh-CN" })
            Assert.False(string.IsNullOrWhiteSpace(Resource(locale, $"ScanDebug_Workflow{step}{field}")));

        foreach (var locale in new[] { "en-us", "zh-CN" })
            Assert.Contains("{0}", Resource(locale, "ScanDebug_WorkflowReviewDetail"), StringComparison.Ordinal);
    }

    [Fact]
    public void LongDraftNames_AreBoundedAndCannotMoveRunActionsIntoTheWorkflowPane()
    {
        var xaml = ReadXaml();
        var root = Named(xaml, "ScanDebugRootGrid");
        var run = Named(xaml, "WorkbenchRunBar");
        var workspace = Named(xaml, "WorkbenchWorkspaceGrid");
        var fileName = Assert.Single(xaml.Descendants(),
            element => (string?)element.Attribute("AutomationProperties.AutomationId") == "CurrentProfileFileNameText");

        Assert.Equal("{StaticResource ScanDebugProfileNameMaximum}", (string?)fileName.Attribute("MaxWidth"));
        Assert.Equal("NoWrap", (string?)fileName.Attribute("TextWrapping"));
        Assert.Equal("CharacterEllipsis", (string?)fileName.Attribute("TextTrimming"));
        Assert.Equal((string?)fileName.Attribute("Text"), (string?)fileName.Attribute("ToolTipService.ToolTip"));
        Assert.Same(workspace, run.Parent);
        Assert.Same(root, workspace.Parent);
        Assert.True((int?)run.Attribute("Grid.Row") < (int?)Named(xaml, "WorkbenchContentSplitGrid").Attribute("Grid.Row"));
        Assert.DoesNotContain(run, Named(xaml, "FilmProfileWorkflowGuide").Descendants());
        Assert.DoesNotContain(run.Ancestors(), element => element.Name.LocalName is "ScrollViewer" or "Flyout");

        foreach (var id in new[] { "StopScanButton", "StopAllMotorsButton" })
        {
            var stop = Assert.Single(run.Descendants(),
                element => (string?)element.Attribute("AutomationProperties.AutomationId") == id);
            Assert.Null(stop.Attribute("Visibility"));
            Assert.DoesNotContain(stop.Ancestors(), element => element.Name.LocalName is "ScrollViewer" or "Flyout");
        }
    }

    [Fact]
    public void WorkflowRecommendation_PrioritizesSetupAndBlockingErrorsBeforeOptionalReferences()
    {
        var source = ReadSource("PRISM Utility", "ViewModels", "ScanDebugViewModel.Workflow.cs");
        var configuration = source.IndexOf("selectedChannels.Length == 0 || draft.ChannelProfiles.Count == 0", StringComparison.Ordinal);
        var errors = source.IndexOf("issue => issue.Severity == ScanFilmProfileValidationSeverity.Error", StringComparison.Ordinal);
        var references = source.IndexOf("profile.BlackLevel is not ushort black", StringComparison.Ordinal);
        Assert.True(configuration >= 0 && configuration < errors && errors < references);
        Assert.Contains("_hasInvalidFilmProfileInput", source, StringComparison.Ordinal);
        Assert.Contains("profile.WhiteLevel is not ushort white", source, StringComparison.Ordinal);
        Assert.Contains("white <= black", source, StringComparison.Ordinal);
        Assert.Contains("public bool WorkflowGuideActionEnabled => true;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanSaveFilmProfile", source, StringComparison.Ordinal);
        foreach (var name in new[] { "WorkflowGuideStep", "WorkflowGuideTitle", "WorkflowGuideDetail", "WorkflowGuideActionText", "WorkflowGuideActionEnabled" })
            Assert.Contains($"OnPropertyChanged(nameof({name}));", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowNavigation_OnlyLocatesEditorsReviewAndExplicitSaveAction()
    {
        var source = ReadSource("PRISM Utility", "Views", "ScanDebugPage.Workflow.cs");
        var xaml = ReadXaml();
        var overview = Named(xaml, "FilmProfileWorkflowNextButton");
        Assert.Equal("Button", overview.Name.LocalName);
        Assert.Equal("FilmProfileWorkflowNextButton_Click", (string?)overview.Attribute("Click"));
        Assert.Null(overview.Attribute("Command"));
        Assert.Contains("Enum.TryParse<ScanDebugWorkflowGuideStep>", source, StringComparison.Ordinal);
        Assert.Contains("NavigateFilmProfileWorkflowStep(step);", source, StringComparison.Ordinal);
        Assert.Contains("FilmProfileWorkflowFlyout.Hide();", source, StringComparison.Ordinal);
        Assert.Contains("SelectWorkbenchTask(0);", source, StringComparison.Ordinal);
        Assert.Contains("BeginIssueTargetFocus(1, AcquisitionPlanScrollViewer, AcquisitionRowsComboBox);", source, StringComparison.Ordinal);
        Assert.Contains("OpenBlackWhiteTaskButton_Click(this, new RoutedEventArgs());", source, StringComparison.Ordinal);
        Assert.Contains("NavigateCalibrationGuideStep(ScanDebugCalibrationGuideStep.ReviewConfiguration);", source, StringComparison.Ordinal);
        Assert.Contains("SaveAndOpenScanButton.Focus(FocusState.Programmatic)", source, StringComparison.Ordinal);
        var steps = Named(xaml, "FilmProfileWorkflowFlyout").Descendants()
            .Where(element => element.Name.LocalName == "Button").ToArray();
        Assert.Equal(4, steps.Length);
        foreach (var (step, action) in new[]
        {
            ("Configuration", "BeginIssueTargetFocus(1, AcquisitionPlanScrollViewer, AcquisitionRowsComboBox);"),
            ("Calibration", "OpenBlackWhiteTaskButton_Click(this, new RoutedEventArgs());"),
            ("Review", "NavigateCalibrationGuideStep(ScanDebugCalibrationGuideStep.ReviewConfiguration);"),
            ("Scan", "SaveAndOpenScanButton.Focus(FocusState.Programmatic)")
        })
        {
            var button = Assert.Single(steps, element => (string?)element.Attribute("Tag") == step);
            Assert.Equal("FilmProfileWorkflowStepButton_Click", (string?)button.Attribute("Click"));
            Assert.Null(button.Attribute("Command"));
            var caseStart = source.IndexOf($"case ScanDebugWorkflowGuideStep.{step}:", StringComparison.Ordinal);
            Assert.True(caseStart >= 0);
            var caseEnd = source.IndexOf("break;", caseStart, StringComparison.Ordinal);
            Assert.True(caseEnd > caseStart);
            Assert.Contains(action, source[caseStart..caseEnd], StringComparison.Ordinal);
        }
        foreach (var forbidden in new[] { "Command.Execute", "ExecuteAsync", "SaveAndOpenScanButton_Click", "_session", "SelectedCalibrationChannel =" })
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
    }

    private static XDocument ReadXaml() => XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));

    private static XElement Named(XDocument document, string name) => Assert.Single(document.Descendants(),
        element => (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == name);

    private static string Resource(string locale, string key) => Assert.Single(
        XDocument.Parse(ReadSource("PRISM Utility", "Strings", locale, "Resources.resw")).Descendants("data"),
        element => (string?)element.Attribute("name") == key).Element("value")!.Value;

    private static string ReadSource(params string[] path)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var sourcePath = Path.Combine(directory.FullName, Path.Combine(path));
            if (File.Exists(sourcePath))
                return File.ReadAllText(sourcePath);
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(path)}");
    }
}
