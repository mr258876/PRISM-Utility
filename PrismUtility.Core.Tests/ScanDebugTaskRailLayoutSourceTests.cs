using System.Xml.Linq;
using System.Text.RegularExpressions;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "ScanDebugLayout")]
public sealed class ScanDebugTaskRailLayoutSourceTests
{
    [Fact]
    public void BlackWhiteDetail_KeepsPreviewTaskNavigationAndExplicitReturn()
    {
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var detail = Named(xaml, "ChannelCalibrationDetailsGrid");

        Assert.Contains("_isAuxiliaryPanelOpen = true;\n        SetActiveWorkbenchSection(2);",
            page.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("_isAuxiliaryPanelOpen = false;", page, StringComparison.Ordinal);
        Assert.Contains("SetActiveWorkbenchSection(_lastWorkbenchTaskSectionIndex);", page, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationDetailsExpander.Visibility = Visibility.Collapsed;", page, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationDetailsGrid.Visibility = ToVisibility(showChannelDetail);", page, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationDetailsExpander.Content = null;", page, StringComparison.Ordinal);
        var composition = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "ComposeWorkspaceHeader");
        Assert.Contains("ChannelCalibrationTaskStack", composition, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationDetailsGrid", composition, StringComparison.Ordinal);
        Assert.Matches(@"detailParent\.Children\.Insert\(\s*detailParent\.Children\.IndexOf\(ChannelCalibrationDetailsExpander\)\s*\+\s*1\s*,\s*details\s*\)", composition);
        Assert.Contains("ChannelCalibrationPrimaryActions.Visibility = ToVisibility(index == 2 && !showChannelDetail);", page, StringComparison.Ordinal);
        Assert.DoesNotContain("inspectionOpen, _isAuxiliaryPanelOpen", page, StringComparison.Ordinal);
        Assert.Contains("_isAuxiliaryPanelOpen ? _auxiliaryPanelEditorRatio : _workbenchPreviewEditorRatio,", page, StringComparison.Ordinal);
        var entry = xaml.Descendants().Single(element => (string?)element.Attribute("AutomationProperties.AutomationId") == "BwDetailsEntryButton");
        Assert.Contains(entry, Named(xaml, "ChannelCalibrationPrimaryActions").Descendants());
        Assert.Equal("{Binding Header, ElementName=ChannelCalibrationDetailsExpander}", (string?)entry.Attribute("Content"));
        Assert.Equal("BwOpenChannelDetailsButton_Click", (string?)entry.Attribute("Click"));
        Assert.Equal("0", (string?)xaml.Descendants().Single(element => (string?)element.Attribute("AutomationProperties.AutomationId") == "ChannelCalibrationParametersCard").Attribute("Grid.Row"));
        Assert.Equal("1", (string?)xaml.Descendants().Single(element => (string?)element.Attribute("AutomationProperties.AutomationId") == "ChannelCalibrationLibraryCard").Attribute("Grid.Row"));
        Assert.Equal("2", (string?)xaml.Descendants().Single(element => (string?)element.Attribute("AutomationProperties.AutomationId") == "ChannelCalibrationStatusCard").Attribute("Grid.Row"));
        Assert.Contains("ExposureMicrosecondsTextBox", detail.ToString(), StringComparison.Ordinal);
        Assert.Contains("Adc1OffsetTextBox", detail.ToString(), StringComparison.Ordinal);
        var footer = Named(xaml, "ChannelCalibrationSaveFooter");
        Assert.DoesNotContain("SaveChannelProfileCommand", detail.ToString(), StringComparison.Ordinal);
        Assert.Contains("SaveChannelProfileCommand", footer.ToString(), StringComparison.Ordinal);
        Assert.Equal("2", (string?)footer.Attribute("Grid.Row"));
        Assert.Contains(footer, Named(xaml, "ChannelCalibrationRail").Elements());
        Assert.Equal(4, Named(xaml, "WorkbenchTaskSelectorBar").Elements().Count());
        Assert.Equal(4, Named(xaml, "WorkbenchTaskComboBox").Elements().Count());

        var sectionChange = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "SetActiveWorkbenchSection");
        Assert.Contains("(_activeWorkbenchSectionIndex == 2 || index == 2)", sectionChange, StringComparison.Ordinal);
        Assert.Contains("(ChannelCalibrationDetailsGrid.Visibility == Visibility.Visible) != _isAuxiliaryPanelOpen", sectionChange, StringComparison.Ordinal);
        Assert.Contains("if (isChannelRouteChange)", sectionChange, StringComparison.Ordinal);
        Assert.Contains("RememberChannelRouteScrollPosition();", sectionChange, StringComparison.Ordinal);
        Assert.Contains("RestoreChannelRouteScrollPositionAfterLayout(_isAuxiliaryPanelOpen);", sectionChange, StringComparison.Ordinal);
        var refinement = ReadSource("PRISM Utility", "Views", "ScanDebugPage.UiRefinement.cs");
        Assert.Contains("_channelRouteScrollPositions[isDetails]", refinement, StringComparison.Ordinal);
        Assert.Contains("focusRequestVersion != _issueTargetFocusRequestVersion", refinement, StringComparison.Ordinal);
        Assert.Contains("_bwCompactMode ? offsets.Compact : offsets.Editor", refinement, StringComparison.Ordinal);
        var issueNavigation = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "OnCurrentFilmProfileIssueNavigationRequested");
        Assert.True(issueNavigation.IndexOf("SetActiveWorkbenchSection(sectionIndex);", StringComparison.Ordinal)
            < issueNavigation.IndexOf("BeginIssueTargetFocus(", StringComparison.Ordinal));
    }

    [Fact]
    public void BlackWhiteTask_ReusesIlluminationAndSampleControlsWithoutDuplicatingCommands()
    {
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var xaml = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml");

        Assert.Contains("ChannelCalibrationTaskStack.Children.Remove(ChannelCalibrationIlluminationCard);", page, StringComparison.Ordinal);
        Assert.Matches(@"ChannelCalibrationTaskStack\.Children\.Insert\(\s*ChannelCalibrationTaskStack\.Children\.IndexOf\(ChannelCalibrationDetailsGrid\),\s*ChannelCalibrationIlluminationCard\)", page);
        Assert.DoesNotContain("ChannelCalibrationPrimaryActions.Children.Add(ChannelCalibrationIlluminationCard);", page, StringComparison.Ordinal);
        Assert.Contains("ColumnSampleUseStack.Children.Insert(", page, StringComparison.Ordinal);
        Assert.Contains("ImageReferenceRoiEditorCard);", page, StringComparison.Ordinal);
        Assert.Contains("ClearManualReferenceLevelsButton), ColumnSampleSaveActions);", page, StringComparison.Ordinal);
        Assert.Contains("detailGrid.Children.OfType<Border>().OrderBy(Grid.GetRow).ToArray()", page, StringComparison.Ordinal);
        Assert.Contains("CurrentIlluminationDraftLabel.Text = \"ScanDebug_BwIssueCurrentDraft\".GetLocalized();", page, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{x:Bind ViewModel.CaptureSourceText, Mode=OneWay}\"", xaml, StringComparison.Ordinal);

        foreach (var command in new[]
        {
            "UseColumnSampleAsManualBlackLevelCommand", "UseColumnSampleAsManualWhiteLevelCommand",
            "SaveColumnSampleAsBlackLevelCommand", "SaveColumnSampleAsWhiteLevelCommand"
        })
            Assert.Equal(1, xaml.Split($"Command=\"{{x:Bind ViewModel.{command}}}\"", StringSplitOptions.None).Length - 1);

        Assert.Equal(1, xaml.Split("TextChanged=\"CurrentCalibrationIlluminationLevelTextBox_TextChanged\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, xaml.Split("TextChanged=\"CurrentCalibrationIlluminationPulseClockTextBox_TextChanged\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, xaml.Split("SelectionChanged=\"CurrentCalibrationIlluminationWorkModeComboBox_SelectionChanged\"", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void PreviewDisclosure_UsesLayoutAuthorityAndRestoresKeyboardFocus()
    {
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var collapse = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "HidePreviewButton_Click");
        var restore = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "OpenPreviewButton_Click");

        var toggle = Named(xaml, "PreviewToggleButton");
        var toolbar = Named(xaml, "WorkspacePreviewToolbar");
        Assert.Same(toolbar, toggle.Parent);
        Assert.Same(toggle, toolbar.Elements().Last());
        Assert.Equal("PreviewToggleButton_Click", (string?)toggle.Attribute("Click"));
        Assert.Null(toggle.Attribute("Visibility"));
        Assert.Equal("0", (string?)toolbar.Attribute("Grid.Row"));
        Assert.Equal("2", (string?)toolbar.Attribute("Grid.Column"));
        Assert.Equal("Right", (string?)toolbar.Attribute("HorizontalAlignment"));
        Assert.Contains("IsPreviewCollapsed: _previewCollapsedOverride ?? false", page, StringComparison.Ordinal);
        Assert.Contains("_previewCollapsedOverride = true;", collapse, StringComparison.Ordinal);
        Assert.Contains("_isNarrowPreviewOpen = false;", collapse, StringComparison.Ordinal);
        Assert.Contains("UpdateWorkbenchPreviewLayout();", collapse, StringComparison.Ordinal);
        Assert.Contains("PreviewToggleButton.Focus(FocusState.Programmatic)", collapse, StringComparison.Ordinal);
        Assert.Contains("_previewCollapsedOverride = false;", restore, StringComparison.Ordinal);
        Assert.Contains("UpdateWorkbenchPreviewLayout();", restore, StringComparison.Ordinal);
        Assert.Contains("PreviewToggleButton.Focus(FocusState.Programmatic)", restore, StringComparison.Ordinal);
        var toggleAction = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "PreviewToggleButton_Click");
        Assert.Contains("WorkbenchPreviewColumnContent.Visibility == Visibility.Visible", toggleAction, StringComparison.Ordinal);
        Assert.Contains("HidePreviewButton_Click(sender, e);", toggleAction, StringComparison.Ordinal);
        Assert.Contains("OpenPreviewButton_Click(sender, e);", toggleAction, StringComparison.Ordinal);
        var stabilize = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "StabilizePreviewToggleWidth");
        Assert.Contains("_openPreviewActionLabel", stabilize, StringComparison.Ordinal);
        Assert.Contains("_hidePreviewActionLabel", stabilize, StringComparison.Ordinal);
        Assert.Contains("Math.Max(openWidth, hideWidth)", stabilize, StringComparison.Ordinal);
        Assert.Contains("PreviewToggleButton.MinWidth", stabilize, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", collapse + restore, StringComparison.Ordinal);
    }

    [Fact]
    public void CalibrationGuide_ProvidesACompactRecommendationAndSixNavigationOnlySteps()
    {
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var primary = Named(xaml, "ChannelCalibrationPrimaryActions");
        var next = Named(xaml, "CalibrationGuideNextButton");
        var overview = Named(xaml, "CalibrationGuideOverviewExpander");
        var title = Assert.Single(primary.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == "CalibrationGuideTitle");

        Assert.Equal("{x:Bind ViewModel.CalibrationGuideTitle, Mode=OneWay}", (string?)title.Attribute("Text"));
        Assert.Equal("{x:Bind ViewModel.CalibrationGuideActionText, Mode=OneWay}", (string?)next.Attribute("Content"));
        Assert.Equal("{x:Bind ViewModel.CalibrationGuideActionEnabled, Mode=OneWay}", (string?)next.Attribute("IsEnabled"));
        Assert.Equal("CalibrationGuideNextButton_Click", (string?)next.Attribute("Click"));
        Assert.Null(next.Attribute("Command"));
        Assert.Contains(next, primary.Descendants());
        Assert.Contains(overview, primary.Descendants());
        Assert.Equal("Button", overview.Name.LocalName);
        Assert.Null(overview.Attribute("IsExpanded"));
        Assert.Null(overview.Attribute("Click"));
        Assert.Null(overview.Attribute("Command"));
        var overviewFlyout = Named(xaml, "CalibrationGuideOverviewFlyout");
        Assert.Same(overview.Element(overview.Name.Namespace + "Button.Flyout"), overviewFlyout.Parent);
        var stepsScroll = Assert.Single(overviewFlyout.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Equal("360", (string?)stepsScroll.Attribute("MaxHeight"));
        Assert.Equal("Auto", (string?)stepsScroll.Attribute("VerticalScrollBarVisibility"));
        Assert.DoesNotContain(next, overview.Descendants());
        Assert.Contains(overview.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.CalibrationGuideDetail, Mode=OneWay}");

        var steps = overview.Descendants().Where(element => element.Name.LocalName == "Button").ToArray();
        Assert.Equal(6, steps.Length);
        foreach (var (step, action, status) in new[]
        {
            ("Connection", "ConnectDevice", "Connection"),
            ("Parameters", "PrepareChannelParameters", "Parameters"),
            ("Roi", "ReviewAdcRoi", "Roi"),
            ("Calibration", "ShowCalibrationActions", "Calibration"),
            ("Review", "ReviewCandidate", "Review"),
            ("Save", "SaveConfiguration", "Save")
        })
        {
            var button = Assert.Single(steps, element => (string?)element.Attribute("AutomationProperties.AutomationId") == $"CalibrationGuide{step}StepButton");
            var uid = $"ScanDebug_CalibrationGuide{step}StepButton";
            Assert.Equal(uid, (string?)button.Attribute(x + "Uid"));
            Assert.Equal(action, (string?)button.Attribute("Tag"));
            Assert.Equal("CalibrationGuideStepButton_Click", (string?)button.Attribute("Click"));
            Assert.Null(button.Attribute("Command"));
            Assert.Equal($"{{x:Bind ViewModel.CalibrationGuide{status}Status, Mode=OneWay}}", (string?)button.Attribute("AutomationProperties.HelpText"));
            Assert.Contains(button.Descendants(), element => (string?)element.Attribute("Text") == $"{{x:Bind ViewModel.CalibrationGuide{status}Status, Mode=OneWay}}");
            var headingUid = (string?)Assert.Single(button.Descendants(), element => element.Attribute(x + "Uid") is not null).Attribute(x + "Uid");
            foreach (var locale in new[] { "en-us", "zh-CN" })
            {
                var accessibleName = Resource(locale, $"{uid}.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name");
                Assert.False(string.IsNullOrWhiteSpace(accessibleName));
                Assert.Equal(Resource(locale, $"{headingUid}.Text"), accessibleName);
            }
        }
    }

    [Fact]
    public void MotorUnitTemplates_LocalizeDisplayWhileKeepingUnderlyingSelections()
    {
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var (templateName, converterParameter, unitProperty) in new[]
        {
            ("ScanMotorUnitTemplate", "MotorUnit", "MoveUnit"),
            ("ScanMotorRateUnitTemplate", "MotorRateUnit", "SpeedUnit")
        })
        {
            var template = Assert.Single(xaml.Descendants(), element => (string?)element.Attribute(x + "Key") == templateName);
            var label = Assert.Single(template.Elements());
            Assert.Equal("TextBlock", label.Name.LocalName);
            Assert.Equal($"{{Binding Converter={{StaticResource ScanSelectorDisplayNameConverter}}, ConverterParameter={converterParameter}}}", (string?)label.Attribute("Text"));
            Assert.Empty(label.Elements());
            foreach (var motor in Enumerable.Range(1, 3))
            {
                var selector = Named(xaml, $"Motor{motor}{unitProperty}ComboBox");
                Assert.Equal($"{{StaticResource {templateName}}}", (string?)selector.Attribute("ItemTemplate"));
                Assert.Equal("{x:Bind ViewModel.MotorUnitOptions, Mode=OneWay}", (string?)selector.Attribute("ItemsSource"));
                Assert.Equal($"{{x:Bind ViewModel.Motor{motor}{unitProperty}, Mode=TwoWay}}", (string?)selector.Attribute("SelectedItem"));
            }
        }
    }

    [Fact]
    public void CalibrationGuide_NavigatesWithoutRunningCalibrationAcceptanceOrPersistence()
    {
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var next = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "CalibrationGuideNextButton_Click");
        var step = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "CalibrationGuideStepButton_Click");
        var route = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "NavigateCalibrationGuideStep");
        var calibration = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "ShowGuidedCalibrationActions");
        var connection = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "OpenScannerConnectionFlyout");
        var navigation = next + step + route + calibration + connection;

        Assert.Contains("ViewModel.CalibrationGuideActionEnabled", next, StringComparison.Ordinal);
        Assert.Contains("NavigateCalibrationGuideStep(ViewModel.CalibrationGuideStep, sender as FrameworkElement);", next, StringComparison.Ordinal);
        Assert.Contains("Enum.TryParse<ScanDebugCalibrationGuideStep>", step, StringComparison.Ordinal);
        Assert.Contains("NavigateCalibrationGuideStep(step, sender as FrameworkElement);", step, StringComparison.Ordinal);
        Assert.Contains("OpenScannerConnectionFlyout(origin);", route, StringComparison.Ordinal);
        Assert.Contains("OpenScannerConnectionFlyout(origin);", calibration, StringComparison.Ordinal);
        Assert.Contains("shellPage.ShowScannerConnectionFlyout(origin);", connection, StringComparison.Ordinal);
        Assert.Contains("DeviceSettingsScrollViewer, DeviceClockMhzTextBox", route, StringComparison.Ordinal);
        Assert.Contains("BwConfigureChannelButton_Click", route, StringComparison.Ordinal);
        Assert.Contains("BwRoiEditButton_Click", route, StringComparison.Ordinal);
        Assert.Contains("OpenCandidateReview();", route, StringComparison.Ordinal);
        Assert.Contains("CalibrationGuideOverviewFlyout.Hide();", route, StringComparison.Ordinal);
        Assert.Contains("ConfigurationFileFlyout.ShowAt(ConfigurationFileMenuButton);", route, StringComparison.Ordinal);
        Assert.Contains("SaveConfigurationFileButton.Focus(FocusState.Programmatic)", route, StringComparison.Ordinal);
        Assert.True(route.IndexOf("ConfigurationFileFlyout.ShowAt(", StringComparison.Ordinal)
            < route.IndexOf("SaveConfigurationFileButton.Focus(", StringComparison.Ordinal));
        Assert.Contains("goto case ScanDebugCalibrationGuideStep.ReviewConfiguration;", route, StringComparison.Ordinal);
        Assert.Contains("ShowFilmProfileReview();", route, StringComparison.Ordinal);
        Assert.Contains("AutoCalibrateButton.Focus(FocusState.Programmatic)", calibration, StringComparison.Ordinal);
        Assert.Contains("EnqueueForCurrentActivation", calibration, StringComparison.Ordinal);
        Assert.DoesNotContain("SynchronizeFilmProfileDraftFromInputs", navigation, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveSelectedCalibrationProfileAsync", navigation, StringComparison.Ordinal);
        Assert.DoesNotContain("_sessionCoordinator", navigation, StringComparison.Ordinal);

        var executedCommands = System.Text.RegularExpressions.Regex.Matches(navigation, @"\b([\w.]+Command)\.Execute(?:Async)?\(")
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(["ViewModel.ValidateFilmProfileCommand"], executedCommands);
    }

    [Fact]
    public void PairedFields_SeparateLocalizedHeaderRowsFromInputAndFullWidthLimits()
    {
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        foreach (var (first, second) in new[]
        {
            ("Motor1SpeedValueTextBox", "Motor1SpeedUnitComboBox"),
            ("Motor2SpeedValueTextBox", "Motor2SpeedUnitComboBox"),
            ("Motor3SpeedValueTextBox", "Motor3SpeedUnitComboBox"),
            ("AutofocusSampleRowsTextBox", "AutofocusTiltProbeStepsTextBox"),
            ("AutofocusZProbeStepsTextBox", "AutofocusMotorIntervalUsTextBox"),
            ("Adc1OffsetTextBox", "Adc1GainTextBox"),
            ("Adc2OffsetTextBox", "Adc2GainTextBox")
        })
        {
            var left = Named(xaml, first);
            var right = Named(xaml, second);
            var grid = left.Parent!;
            var inputRow = (int)left.Attribute("Grid.Row")!;
            Assert.Same(grid, right.Parent);
            Assert.Equal(inputRow, (int)right.Attribute("Grid.Row")!);
            Assert.Equal("{StaticResource ScanDebugExternalFieldHeaderTemplate}", (string?)left.Attribute("HeaderTemplate"));
            Assert.Equal("{StaticResource ScanDebugExternalFieldHeaderTemplate}", (string?)right.Attribute("HeaderTemplate"));
            Assert.Contains(grid.Elements(), element => (int?)element.Attribute("Grid.Row") == inputRow - 1
                && (string?)element.Attribute("Text") == $"{{Binding Tag, ElementName={first}}}");
            Assert.Contains(grid.Elements(), element => (int?)element.Attribute("Grid.Row") == inputRow - 1
                && (string?)element.Attribute("Text") == $"{{Binding Tag, ElementName={second}}}");
            Assert.Null(left.Attribute("Height"));
            Assert.Null(right.Attribute("Height"));
        }

        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        Assert.Contains("unit.Tag = localizedHeader;", page, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(unit, localizedHeader);", page, StringComparison.Ordinal);
        Assert.Contains("unit.Header = null;", page, StringComparison.Ordinal);
        foreach (var speedUnit in new[] { "Motor1SpeedUnitComboBox", "Motor2SpeedUnitComboBox", "Motor3SpeedUnitComboBox" })
        {
            var comboBox = Named(xaml, speedUnit);
            Assert.Equal("{StaticResource ScanMotorRateUnitTemplate}", (string?)comboBox.Attribute("ItemTemplate"));
            Assert.Equal("{x:Bind ViewModel.MotorUnitOptions, Mode=OneWay}", (string?)comboBox.Attribute("ItemsSource"));
        }

        foreach (var name in new[] { "AutofocusSampleRowsLimitText", "Adc1OffsetLimitText", "Adc2GainLimitText" })
        {
            var text = xaml.Descendants().Single(element => (string?)element.Attribute("Text") == $"{{x:Bind ViewModel.{name}, Mode=OneWay}}");
            Assert.Equal("2", (string?)text.Parent!.Attribute("Grid.ColumnSpan")
                ?? (string?)text.Parent!.Parent!.Attribute("Grid.ColumnSpan"));
        }
    }

    [Fact]
    public void TransportMotorMapping_IsOwnedBySettingsAndRemovedFromBothTaskPages()
    {
        var debug = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var scan = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanPage.xaml"));
        var settings = XDocument.Parse(ReadSource("PRISM Utility", "Views", "SettingsPage.xaml"));
        foreach (var taskPage in new[] { debug, scan })
            Assert.DoesNotContain(taskPage.Descendants(), element =>
                ((string?)element.Attribute("SelectedItem"))?.Contains("ViewModel.SelectedScanMotor", StringComparison.Ordinal) == true);
        var transportMotor = Assert.Single(settings.Descendants(), element =>
            (string?)element.Attribute("SelectedItem") == "{x:Bind ViewModel.DeviceDefaultScanMotor, Mode=TwoWay}");
        Assert.Equal("{x:Bind ViewModel.DeviceMotorOptions}", (string?)transportMotor.Attribute("ItemsSource"));
        Assert.Equal("{Binding ElementName=TransportMotorLabel}", (string?)transportMotor.Attribute("AutomationProperties.LabeledBy"));
        Assert.Equal("Settings_DeviceTransportMotorLabel",
            (string?)Named(settings, "TransportMotorLabel").Attribute(XName.Get("Uid", "http://schemas.microsoft.com/winfx/2006/xaml")));
        var rows = Assert.Single(scan.Descendants(), element =>
            (string?)element.Attribute(XName.Get("Uid", "http://schemas.microsoft.com/winfx/2006/xaml")) == "Scan_RowsComboBox");
        Assert.Equal("{x:Bind ViewModel.RowOptions, Mode=OneWay}", (string?)rows.Attribute("ItemsSource"));
        Assert.Equal("{x:Bind ViewModel.SelectedRows, Mode=TwoWay}", (string?)rows.Attribute("SelectedItem"));
        Assert.Equal("{x:Bind ViewModel.IsSetupEditingEnabled, Mode=OneWay}", (string?)rows.Attribute("IsEnabled"));
        var mappingEntry = Named(debug, "AcquisitionDeviceMappingButton");
        Assert.Equal("HyperlinkButton", mappingEntry.Name.LocalName);
        Assert.Equal("OpenDeviceChannelsSettings_Click", (string?)mappingEntry.Attribute("Click"));
    }

    [Fact]
    public void Sampling_PrioritizesChannelsAndExposesTransportInputs()
    {
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var sampling = Named(xaml, "AcquisitionPlanSection");
        var children = Named(xaml, "AcquisitionRowsComboBox").Parent!.Elements().ToList();
        var transport = Named(xaml, "AcquisitionTransportDetailsExpander");
        var channels = Named(xaml, "AcquisitionChannelAssignmentList");
        var preview = Assert.Single(sampling.Descendants(), element => (string?)element.Attribute(XName.Get("Uid", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ScanDebug_PreviewToggleSwitch");

        Assert.True(children.IndexOf(Named(xaml, "AcquisitionRowsComboBox")) < children.IndexOf(channels));
        Assert.True(children.IndexOf(channels) < children.IndexOf(preview));
        Assert.True(children.IndexOf(preview) < children.IndexOf(transport));
        Assert.Equal("True", (string?)transport.Attribute("IsExpanded"));
        var mappingEntry = Named(xaml, "AcquisitionDeviceMappingButton");
        Assert.Contains(mappingEntry, transport.Descendants());
        Assert.Equal("HyperlinkButton", mappingEntry.Name.LocalName);
        Assert.Equal("OpenDeviceChannelsSettings_Click", (string?)mappingEntry.Attribute("Click"));
        Assert.Null(mappingEntry.Attribute("SelectedItem"));
        Assert.Contains(Named(xaml, "AcquisitionMotorDistancePerLineTextBox"), transport.Descendants());
        var detail = Named(xaml, "AcquisitionPlanDetailsExpander");
        Assert.Contains(detail.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.AcquisitionChannelOrderText, Mode=OneWay}");
        Assert.False(string.IsNullOrWhiteSpace(Resource("en-us", "ScanDebug_AcquisitionChannelOrderTitle.Text")));
        Assert.False(string.IsNullOrWhiteSpace(Resource("zh-CN", "ScanDebug_AcquisitionChannelOrderTitle.Text")));
        Assert.DoesNotContain(detail.Descendants(), element => ((string?)element.Attribute("Text"))?.Contains("AcquisitionPlanSummaryText", StringComparison.Ordinal) == true
            || ((string?)element.Attribute("Text"))?.Contains("ChannelLedBindingSummaryText", StringComparison.Ordinal) == true);

        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        Assert.Contains("EnqueueForCurrentActivation(UpdateAcquisitionTransportVisibility);", page, StringComparison.Ordinal);
        Assert.Contains("AcquisitionTransportDetailsExpander.Visibility = ToVisibility(ViewModel.IsScanMotorTransportEnabled);", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Focus_PresentsActionsAndSelectedRangeBeforeSecondaryExplanationAndEstimates()
    {
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var focus = Named(xaml, "LiveCalibrationSection");
        var title = Assert.Single(focus.Descendants(), element => (string?)element.Attribute(XName.Get("Uid", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ScanDebug_AutofocusTitle");
        var status = Assert.Single(focus.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == "AutofocusSummaryText");
        var roiSummary = Named(xaml, "FocusRoiDetailsExpander");
        var explanation = Assert.Single(focus.Descendants(), element => (string?)element.Attribute(XName.Get("Uid", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ScanDebug_FocusEvaluationScope");
        var advanced = Named(xaml, "AdvancedAutofocusContent");
        var results = Named(xaml, "AdvancedAutofocusResults");

        Assert.Same(title.Parent, status.Parent);
        Assert.Contains(focus.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == "FocusRoiSummaryText"
            && (string?)element.Attribute("Text") == "{x:Bind ViewModel.FocusRoiSummaryText, Mode=OneWay}");
        Assert.Contains(explanation, roiSummary.Descendants());
        Assert.Contains(explanation.Ancestors(), element => element.Name.LocalName == "Flyout");
        Assert.Equal("ScanDebug_AutofocusPresetComboBox", (string?)advanced.Elements().First().Attribute(XName.Get("Uid", "http://schemas.microsoft.com/winfx/2006/xaml")));
        Assert.Contains(results, advanced.Descendants());
        Assert.Equal("{x:Bind ViewModel.AutofocusResolvedBoundsVisibility, Mode=OneWay}", (string?)results.Attribute("Visibility"));
        Assert.Contains(results.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == "AutofocusEstimateDisclaimer");
        foreach (var (id, property) in new[]
        {
            ("AutofocusResolutionLimitsText", "AutofocusResolutionLimitsText"),
            ("AutofocusEstimatedCapturesText", "AutofocusEstimatedCapturesText"),
            ("AutofocusCumulativeTravelText", "AutofocusCumulativeTravelText"),
            ("AutofocusEstimatedDurationText", "AutofocusEstimatedDurationText")
        })
        {
            var readout = Assert.Single(results.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == id);
            Assert.Equal($"{{x:Bind ViewModel.{property}, Mode=OneWay}}", (string?)readout.Attribute("Text"));
        }
        Assert.Equal("{x:Bind ViewModel.AutofocusBoundsText, Mode=OneWay}",
            (string?)advanced.Elements().Last().Attribute("Text"));
        Assert.Equal("{x:Bind ViewModel.AutofocusBoundsErrorVisibility, Mode=OneWay}",
            (string?)advanced.Elements().Last().Attribute("Visibility"));
        Assert.Contains("非实测", Resource("zh-CN", "ScanDebug_AutofocusEstimateDisclaimer.Text"), StringComparison.Ordinal);
        Assert.Contains("Not measured", Resource("en-us", "ScanDebug_AutofocusEstimateDisclaimer.Text"), StringComparison.Ordinal);
        foreach (var locale in new[] { "en-us", "zh-CN" })
        {
            foreach (var key in new[]
            {
                "ScanDebug_AutofocusResolutionLimitsLabel.Text", "ScanDebug_AutofocusEstimatedCapturesLabel.Text",
                "ScanDebug_AutofocusCumulativeTravelLabel.Text", "ScanDebug_AutofocusEstimatedDurationLabel.Text",
                "ScanDebug_Runtime_AutofocusInputLimits", "ScanDebug_Runtime_AutofocusResolutionLimits",
                "ScanDebug_Runtime_AutofocusEstimatedCaptures", "ScanDebug_Runtime_AutofocusCumulativeTravel",
                "ScanDebug_Runtime_AutofocusEstimatedDuration"
            })
                Assert.False(string.IsNullOrWhiteSpace(Resource(locale, key)));
            var inputLimits = Resource(locale, "ScanDebug_Runtime_AutofocusInputLimits");
            Assert.Contains("137", string.Format(inputLimits, 137, "0.001", 1), StringComparison.Ordinal);
            Assert.Contains("0.001", string.Format(inputLimits, 137, "0.001", 1), StringComparison.Ordinal);
            Assert.Contains("1", string.Format(inputLimits, 137, "0.001", 1), StringComparison.Ordinal);
            Assert.Contains("1600", string.Format(Resource(locale, "ScanDebug_Runtime_AutofocusResolutionLimits"), 200, 1600, 400, 10000), StringComparison.Ordinal);
            Assert.Contains("141.5", string.Format(Resource(locale, "ScanDebug_Runtime_AutofocusCumulativeTravel"), "141.5"), StringComparison.Ordinal);
        }

        foreach (var (field, visibility) in new[]
        {
            ("AutofocusSampleRowsTextBox", "AutofocusSampleRowsLimitVisibility"),
            ("AutofocusTiltProbeStepsTextBox", "AutofocusTiltProbeStepsLimitVisibility"),
            ("AutofocusZProbeStepsTextBox", "AutofocusZProbeStepsLimitVisibility"),
            ("AutofocusMotorIntervalUsTextBox", "AutofocusMotorIntervalLimitVisibility")
        })
        {
            var input = Named(xaml, field);
            Assert.Contains("UpdateSourceTrigger=PropertyChanged", (string?)input.Attribute("Text"));
            var warning = Assert.Single(input.Parent!.Descendants(), element => (string?)element.Attribute("Visibility") == $"{{x:Bind ViewModel.{visibility}, Mode=OneWay}}");
            Assert.True((int)warning.Parent!.Attribute("Grid.Row")! > (int)input.Attribute("Grid.Row")!);
        }
        foreach (var visibility in new[] { "AutofocusMaxTiltIterationsLimitVisibility", "AutofocusMaxZIterationsLimitVisibility" })
            Assert.Contains(advanced.Elements(), element => (string?)element.Attribute("Visibility") == $"{{x:Bind ViewModel.{visibility}, Mode=OneWay}}");
    }

    [Fact]
    public void ValidationReview_UsesAllocatedColumnAndKeepsFeedbackOutOfHeaderFlow()
    {
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var review = Named(xaml, "FilmProfileLifecycleDetailsScrollViewer");
        var split = Named(xaml, "WorkbenchContentSplitGrid");
        var status = Named(xaml, "FilmProfileOperationInfoBar");
        Assert.Same(split, review.Parent);
        Assert.Equal("0", (string?)review.Attribute("Grid.Row"));
        Assert.Equal("5", (string?)review.Attribute("Grid.Column"));
        Assert.Contains(Named(xaml, "WorkbenchValidationReviewColumn"), split.Descendants());
        Assert.Same(Named(xaml, "WorkbenchWorkspaceGrid"), status.Parent);
        Assert.DoesNotContain(status, Named(xaml, "FilmProfileLifecycleHeader").Descendants());
        Assert.Equal("Top", (string?)status.Attribute("VerticalAlignment"));
        Assert.Equal("Right", (string?)status.Attribute("HorizontalAlignment"));
        Assert.Contains("IsReviewOpen: reviewVisible", page, StringComparison.Ordinal);
        Assert.Contains("WorkbenchValidationReviewColumn.Width = new GridLength(layout.ReviewWidth + layout.ReviewGapWidth);", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.SetColumnSpan(FilmProfileLifecycleDetailsScrollViewer", page, StringComparison.Ordinal);
        foreach (var method in new[] { "ShowFilmProfileReview", "CloseFilmProfileReview" })
            Assert.Contains("UpdateWorkbenchPreviewLayout();", ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, method), StringComparison.Ordinal);
        Assert.Contains("ViewModel.FilmProfileValidationReviewRequested += OnFilmProfileValidationReviewRequested;", page, StringComparison.Ordinal);
        Assert.Contains("ViewModel.FilmProfileValidationReviewRequested -= OnFilmProfileValidationReviewRequested;", page, StringComparison.Ordinal);
        Assert.Contains("=> EnqueueForCurrentActivation(ShowFilmProfileReview);", page, StringComparison.Ordinal);
    }

    [Fact]
    public void RoiRecovery_KeepsApplyWithInputsAndGroupsRestoreActionsAndHelpInFlyout()
    {
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var owner in new[] { "Adc", "Focus" })
        {
            var card = Named(xaml, $"{owner}RoiEditorCard");
            var apply = Assert.Single(card.Descendants(), element =>
                (string?)element.Attribute("Command") == $"{{x:Bind ViewModel.Apply{owner}RoiInputsCommand}}");
            Assert.DoesNotContain(apply.Ancestors(), element => element.Name.LocalName == "Flyout");
            var recovery = Assert.Single(card.Descendants(), element =>
                (string?)element.Attribute("AutomationProperties.AutomationId") == $"{owner}RoiRecoveryButton");
            Assert.Equal("Button", recovery.Name.LocalName);
            Assert.Same(apply.Parent, recovery.Parent);
            var flyout = Assert.Single(recovery.Descendants(), element => element.Name.LocalName == "Flyout");
            Assert.Null(recovery.Attribute("Command"));
            foreach (var command in new[] { $"Reset{owner}RoiCommand", $"Reload{owner}RoiInputsCommand", "ResetAllRoisCommand" })
                Assert.Single(flyout.Descendants(), element => (string?)element.Attribute("Command") == $"{{x:Bind ViewModel.{command}}}");
            Assert.Contains(flyout.Descendants(), element => (string?)element.Attribute(x + "Uid") == $"ScanDebug_{owner}RoiEditorHelpText");
            Assert.Contains(flyout.Descendants(), element => (string?)element.Attribute(x + "Uid") == "ScanDebug_RoiDraftSourceText");
            Assert.DoesNotContain(Named(xaml, $"{owner}RoiStartTextBox").Ancestors(), element => element.Name.LocalName == "Flyout");
            Assert.DoesNotContain(Named(xaml, $"{owner}RoiEndTextBox").Ancestors(), element => element.Name.LocalName == "Flyout");
        }
    }

    [Fact]
    public void SharedHeader_MeasuresTaskAndToolbarFitAndKeepsFixedRunActions()
    {
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var layoutStart = page.IndexOf("private void UpdateWorkbenchPreviewLayout(double availableWidth)", StringComparison.Ordinal);
        var layoutEnd = page.IndexOf("private Control? FindWorkbenchFocusReplacement(", layoutStart, StringComparison.Ordinal);
        var layout = page[layoutStart..layoutEnd];
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var run = Named(xaml, "WorkbenchRunBar");

        Assert.Contains("MeasureNaturalWidth(WorkspacePreviewToolbar)", layout, StringComparison.Ordinal);
        Assert.Contains("MeasureNaturalWidth(WorkbenchTaskSelectorBar)", layout, StringComparison.Ordinal);
        Assert.Contains("var taskNavigationWidth = Math.Max(0, availableWidth - toolbarWidth - spacing * 2);", layout, StringComparison.Ordinal);
        Assert.Contains("var isTaskSelectorBarAvailable = !_isAuxiliaryPanelOpen && taskNavigationWidth >= taskSelectorWidth;", layout, StringComparison.Ordinal);
        Assert.Contains(Named(xaml, "WorkbenchAuxiliaryHeader"), Named(xaml, "WorkbenchWorkspaceHeader").Descendants());
        Assert.DoesNotContain(Named(xaml, "WorkbenchAuxiliaryHeader"), Named(xaml, "WorkbenchEditorColumnContent").Descendants());
        Assert.Contains("WorkbenchTaskComboBox.MaxWidth = Math.Max(WorkbenchTaskComboBox.MinWidth, taskNavigationWidth);", layout, StringComparison.Ordinal);
        var toolbar = Named(xaml, "WorkspacePreviewToolbar");
        Assert.Equal("0", (string?)toolbar.Attribute("Grid.Row"));
        Assert.Equal("2", (string?)toolbar.Attribute("Grid.Column"));
        Assert.Equal("Right", (string?)toolbar.Attribute("HorizontalAlignment"));
        Assert.Same(Named(xaml, "PreviewToggleButton"), toolbar.Elements().Last());
        Assert.DoesNotContain("Grid.SetRow(WorkspacePreviewToolbar", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.SetColumn(WorkspacePreviewToolbar", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("var isTaskSelectorBarAvailable = availableWidth >= ScanWorkbenchPreviewLayout.WideThreshold;", layout, StringComparison.Ordinal);
        Assert.True(layout.IndexOf("FindWorkbenchFocusReplacement(focusedElement", StringComparison.Ordinal)
            < layout.IndexOf("WorkbenchTaskSelectorBar.Visibility =", StringComparison.Ordinal));
        Assert.Equal("Stretch", (string?)Named(xaml, "RunStatusPanel").Attribute("HorizontalAlignment"));
        foreach (var (id, command) in new[]
        {
            ("StartScanButton", "StartScanCommand"), ("StopScanButton", "StopScanCommand"),
            ("StopAllMotorsButton", "StopAllMotorsCommand")
        })
        {
            var button = Assert.Single(run.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == id);
            Assert.Equal($"{{x:Bind ViewModel.{command}}}", (string?)button.Attribute("Command"));
        }
    }

    [Fact]
    public void CompactRunHeader_WrapsStopsOnlyWhenTheirMeasuredGroupDoesNotFitBesideMode()
    {
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var layoutStart = page.IndexOf("private void UpdateWorkbenchPreviewLayout(double availableWidth)", StringComparison.Ordinal);
        var layoutEnd = page.IndexOf("private Control? FindWorkbenchFocusReplacement(", layoutStart, StringComparison.Ordinal);
        var layout = page[layoutStart..layoutEnd];
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var run = Named(xaml, "WorkbenchRunBar");
        var actions = Named(xaml, "RunActionPanel");

        Assert.Contains("var modeWidth = RunCaptureModePanel.Visibility == Visibility.Visible ? MeasureNaturalWidth(RunCaptureModePanel) : 0;", layout, StringComparison.Ordinal);
        Assert.Contains("var stopWidth = MeasureNaturalWidth(RunActionPanel);", layout, StringComparison.Ordinal);
        Assert.Contains("var wrapRunActions = availableWidth < modeWidth + stopWidth + spacing;", layout, StringComparison.Ordinal);
        Assert.Contains("var wrapRunStatus = wrapRunActions || availableWidth < modeWidth + statusWidth + recoveryWidth + stopWidth + spacing * 3;", layout, StringComparison.Ordinal);
        Assert.Contains("RunStatusTextPanel.Visibility = Visibility.Visible;", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(RunActionPanel, wrapRunActions ? 1 : 0);", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(RunActionPanel, wrapRunActions ? 0 : 2);", layout, StringComparison.Ordinal);
        Assert.Contains("RunActionPanel.Margin = new Thickness(0, wrapRunActions ? spacing : 0, 0, 0);", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(RunStatusPanel, wrapRunActions ? 2 : wrapRunStatus ? 1 : 0);", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(RunStatusPanel, wrapRunStatus ? 0 : 1);", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumnSpan(RunStatusPanel, wrapRunStatus ? 3 : 1);", layout, StringComparison.Ordinal);
        Assert.Contains("RunStatusPanel.Margin = new Thickness(0, wrapRunStatus ? spacing : 0, 0, 0);", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(RunProgressBar, wrapRunActions ? 3 : wrapRunStatus ? 2 : 1);", layout, StringComparison.Ordinal);
        Assert.Contains("var statusWidth = RunStatusTextPanel.Visibility == Visibility.Visible ? MeasureNaturalWidth(RunStatusTextPanel) : 0;", layout, StringComparison.Ordinal);
        Assert.Contains("var recoveryWidth = MeasureNaturalWidth(RunRecoveryActionsPanel);", layout, StringComparison.Ordinal);
        Assert.Contains("var statusAvailableWidth = availableWidth - (wrapRunStatus ? 0 : modeWidth + stopWidth + spacing * 2);", layout, StringComparison.Ordinal);
        Assert.Contains("var stackRunStatusActions = statusAvailableWidth < statusWidth + recoveryWidth + spacing;", layout, StringComparison.Ordinal);
        Assert.Contains("RunStatusPanel.RowSpacing = stackRunStatusActions ? spacing : 0;", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(RunRecoveryActionsPanel, stackRunStatusActions ? 1 : 0);", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.SetRow(RunActionPanel, wrapIdentity", layout, StringComparison.Ordinal);
        Assert.Contains(Named(xaml, "RunCaptureModePanel"), run.Descendants());
        Assert.Contains(Named(xaml, "RunRecoveryActionsPanel"), Named(xaml, "RunStatusPanel").Descendants());
        foreach (var id in new[] { "StartScanButton", "StopScanButton", "StopAllMotorsButton" })
            Assert.Contains(actions.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == id);
    }

    [Theory]
    [InlineData(false, false, ScanWorkbenchPreviewLayoutMode.NarrowEditor, false, true, false)]
    [InlineData(true, false, ScanWorkbenchPreviewLayoutMode.NarrowPreview, true, false, true)]
    [InlineData(false, true, ScanWorkbenchPreviewLayoutMode.NarrowInspection, false, false, false)]
    [InlineData(true, true, ScanWorkbenchPreviewLayoutMode.NarrowInspection, false, false, false)]
    public void CompactPane_KeepsImageToolsBesideThePreviewAndPreservesTaskNavigation(
        bool previewOpen, bool inspectionOpen, ScanWorkbenchPreviewLayoutMode mode,
        bool showPreviewTools, bool showPreviewEntry, bool showBackButton)
    {
        var state = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            800, HasImage: true, IsNarrowPreviewOpen: previewOpen, IsInspectionOpen: inspectionOpen));
        Assert.Equal(mode, state.Mode);
        Assert.Equal(showPreviewTools, state.IsPreviewVisible);
        Assert.Equal(showPreviewEntry, state.IsPreviewOpenButtonVisible);
        Assert.Equal(showBackButton, state.IsBackToEditorButtonVisible);

        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var layoutStart = page.IndexOf("private void UpdateWorkbenchPreviewLayout(double availableWidth)", StringComparison.Ordinal);
        var focusStart = page.IndexOf("private Control? FindWorkbenchFocusReplacement(", layoutStart, StringComparison.Ordinal);
        var focusEnd = page.IndexOf("private static bool IsFocusedWithin(", focusStart, StringComparison.Ordinal);
        var layout = page[layoutStart..focusStart];
        var focus = page[focusStart..focusEnd];
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var workspace = Named(xaml, "WorkbenchWorkspaceHeader");
        var toolbar = Named(xaml, "WorkspacePreviewToolbar");

        Assert.Contains("PreviewToolbar.Visibility = ToVisibility(layout.IsPreviewVisible);", layout, StringComparison.Ordinal);
        Assert.Contains("RawSignalModeSelector.Visibility = ToVisibility(layout.IsPreviewVisible);", layout, StringComparison.Ordinal);
        Assert.Contains("InspectionToggleButton.Visibility = Visibility.Visible;", layout, StringComparison.Ordinal);
        Assert.Contains("PreviewToggleButton.Content = previewActionLabel;", layout, StringComparison.Ordinal);
        Assert.Contains("layout.IsPreviewVisible ? _hidePreviewActionLabel : _openPreviewActionLabel", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewToggleButton.Visibility = ToVisibility", layout, StringComparison.Ordinal);
        Assert.Contains("BackToEditorButton.Visibility = ToVisibility(layout.IsBackToEditorButtonVisible);", layout, StringComparison.Ordinal);
        Assert.Contains("WorkbenchTaskComboBox.Visibility = ToVisibility(!_isAuxiliaryPanelOpen && !isTaskSelectorBarAvailable);", layout, StringComparison.Ordinal);
        Assert.Contains("WorkbenchAuxiliaryHeader.Visibility = ToVisibility(_isAuxiliaryPanelOpen);", layout, StringComparison.Ordinal);
        Assert.Contains("var toolbarWidth = MeasureNaturalWidth(WorkspacePreviewToolbar);", layout, StringComparison.Ordinal);
        Assert.Contains("var taskNavigationWidth = Math.Max(0, availableWidth - toolbarWidth - spacing * 2);", layout, StringComparison.Ordinal);
        Assert.Contains("var isTaskSelectorBarAvailable = !_isAuxiliaryPanelOpen && taskNavigationWidth >= taskSelectorWidth;", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.SetRow(WorkspacePreviewToolbar", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.SetColumn(WorkspacePreviewToolbar", layout, StringComparison.Ordinal);
        Assert.Contains("(IsFocusedWithin(focusedElement, RawSignalModeSelector) || IsFocusedWithin(focusedElement, PreviewToolbar))", focus, StringComparison.Ordinal);
        Assert.Contains("return PreviewToggleButton;", focus, StringComparison.Ordinal);
        Assert.Contains("focusHandoffVersion != _workbenchFocusHandoffVersion", layout, StringComparison.Ordinal);
        Assert.Contains("!IsCurrentActivation(activationEpoch)", layout, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(InspectionToggleButton, inspectionActionLabel", layout, StringComparison.Ordinal);
        Assert.Contains("if (!Equals(InspectionToggleButton.Content, inspectionActionLabel))", layout, StringComparison.Ordinal);
        Assert.Contains("? (_isNarrowPreviewOpen ? _openPreviewActionLabel : BackToEditorButton.Content)", layout, StringComparison.Ordinal);
        Assert.Same(toolbar, Named(xaml, "InspectionToggleButton").Parent);
        Assert.Same(Named(xaml, "PreviewHeaderGrid"), Named(xaml, "RawSignalModeSelector").Parent);
        Assert.Same(Named(xaml, "PreviewHeaderGrid"), Named(xaml, "PreviewToolbar").Parent);
        Assert.Contains(Named(xaml, "RawSignalModeSelector"), Named(xaml, "WorkbenchPreviewColumnContent").Descendants());
        Assert.Contains(Named(xaml, "PreviewToolbar"), Named(xaml, "WorkbenchPreviewColumnContent").Descendants());
        Assert.DoesNotContain(Named(xaml, "RawSignalModeSelector"), workspace.Descendants());
        Assert.DoesNotContain(Named(xaml, "PreviewToolbar"), workspace.Descendants());
        Assert.DoesNotContain("WorkspacePreviewToolbar.Children.Insert(0, RawSignalModeSelector);", page, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkspacePreviewToolbar.Children.Insert(1, PreviewToolbar);", page, StringComparison.Ordinal);
        Assert.Same(toolbar, Named(xaml, "PreviewToggleButton").Parent);
        Assert.Same(Named(xaml, "PreviewToggleButton"), toolbar.Elements().Last());
        Assert.Equal("0", (string?)toolbar.Attribute("Grid.Row"));
        Assert.Equal("2", (string?)toolbar.Attribute("Grid.Column"));
        Assert.Equal("Right", (string?)toolbar.Attribute("HorizontalAlignment"));
        Assert.Equal("ScanDebug_WorkbenchInspectionButton", (string?)Named(xaml, "InspectionToggleButton").Attribute(XName.Get("Uid", "http://schemas.microsoft.com/winfx/2006/xaml")));
    }

    [Theory]
    [InlineData("en-us", "Open preview", "Hide preview")]
    [InlineData("zh-CN", "打开预览", "收起预览")]
    public void PreviewToggle_UsesPlainLocalizedActionKeysAndReadonlyLabels(string locale, string open, string hide)
    {
        Assert.Equal(open, Resource(locale, "ScanDebug_PreviewOpenAction"));
        Assert.Equal(hide, Resource(locale, "ScanDebug_PreviewHideAction"));
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        Assert.Contains("private readonly string _openPreviewActionLabel;", page, StringComparison.Ordinal);
        Assert.Contains("private readonly string _hidePreviewActionLabel;", page, StringComparison.Ordinal);
        Assert.Contains("_openPreviewActionLabel = \"ScanDebug_PreviewOpenAction\".GetLocalized();", page, StringComparison.Ordinal);
        Assert.Contains("_hidePreviewActionLabel = \"ScanDebug_PreviewHideAction\".GetLocalized();", page, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ScanDebug_OpenPreviewButton.Content\".GetLocalized()", page, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ScanDebug_HidePreviewButton.Content\".GetLocalized()", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-us", "Inspect", "Open preview", "Back to editor")]
    [InlineData("zh-CN", "信号检查", "打开预览", "返回编辑器")]
    public void InspectionToggle_RestoresItsLocalizedLabelAfterCompactInspection(
        string locale, string inspectionText, string previewText, string editorText)
    {
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var layoutStart = page.IndexOf("private void UpdateWorkbenchPreviewLayout(double availableWidth)", StringComparison.Ordinal);
        var layoutEnd = page.IndexOf("private Control? FindWorkbenchFocusReplacement(", layoutStart, StringComparison.Ordinal);
        var layout = page[layoutStart..layoutEnd];

        Assert.Equal(inspectionText, Resource(locale, "ScanDebug_WorkbenchInspectionButton.Content"));
        Assert.Equal(previewText, Resource(locale, "ScanDebug_OpenPreviewButton.Content"));
        Assert.Equal(editorText, Resource(locale, "ScanDebug_BackToEditorButton.Content"));
        Assert.Contains("InitializeComponent();\n        _inspectionActionLabel = (string)InspectionToggleButton.Content;",
            page.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("? (_isNarrowPreviewOpen ? _openPreviewActionLabel : BackToEditorButton.Content)\n            : _inspectionActionLabel;",
            layout.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("\"ScanDebug_WorkbenchInspectionButton.Content\".GetLocalized()", page, StringComparison.Ordinal);
        Assert.Contains("InspectionToggleButton.Content = inspectionActionLabel;", layout, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(InspectionToggleButton, inspectionActionLabel?.ToString() ?? string.Empty);", layout, StringComparison.Ordinal);

        string LabelFor(bool previewOpen, bool inspectionOpen)
        {
            var mode = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
                800, HasImage: true, IsNarrowPreviewOpen: previewOpen, IsInspectionOpen: inspectionOpen)).Mode;
            return mode == ScanWorkbenchPreviewLayoutMode.NarrowInspection
                ? (previewOpen ? previewText : editorText)
                : inspectionText;
        }

        Assert.Equal(new[] { inspectionText, editorText, inspectionText },
            new[] { LabelFor(false, false), LabelFor(false, true), LabelFor(false, false) });
        Assert.Equal(new[] { inspectionText, previewText, inspectionText },
            new[] { LabelFor(true, false), LabelFor(true, true), LabelFor(true, false) });
    }

    [Fact]
    public void CompletedBlackWhiteAndMotionCopy_IsPairedAcrossLocales()
    {
        Assert.Contains("next capture", Resource("en-us", "ScanDebug_CurrentIlluminationHelpText.Text"), StringComparison.Ordinal);
        Assert.Contains("下次采样", Resource("zh-CN", "ScanDebug_CurrentIlluminationHelpText.Text"), StringComparison.Ordinal);
        Assert.Contains("Fill copies the sample into the inputs", Resource("en-us", "ScanDebug_ImageReferenceRoiEditorScopeText.Text"), StringComparison.Ordinal);
        Assert.Contains("“填入”将采样值带入输入框", Resource("zh-CN", "ScanDebug_ImageReferenceRoiEditorScopeText.Text"), StringComparison.Ordinal);
        Assert.Contains("Save stores the reference level in this channel's preset", Resource("en-us", "ScanDebug_ImageReferenceRoiEditorScopeText.Text"), StringComparison.Ordinal);
        Assert.Contains("“保存”将参考值写入当前通道预设", Resource("zh-CN", "ScanDebug_ImageReferenceRoiEditorScopeText.Text"), StringComparison.Ordinal);
        for (var axis = 1; axis <= 3; axis++)
        {
            Assert.Equal("Speed", Resource("en-us", $"ScanDebug_Motor{axis}SpeedValueTextBox.Header"));
            Assert.Equal("速度", Resource("zh-CN", $"ScanDebug_Motor{axis}SpeedValueTextBox.Header"));
            Assert.Equal("Rate unit", Resource("en-us", $"ScanDebug_Motor{axis}SpeedUnitComboBox.Header"));
            Assert.Equal("速度单位", Resource("zh-CN", $"ScanDebug_Motor{axis}SpeedUnitComboBox.Header"));
        }
    }

    private static string Resource(string locale, string key)
    {
        var source = XDocument.Parse(ReadSource("PRISM Utility", "Strings", locale, "Resources.resw"));
        return Assert.Single(source.Descendants("data"), element => (string?)element.Attribute("name") == key).Element("value")!.Value;
    }

    private static XElement Named(XDocument document, string name)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
    }

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
