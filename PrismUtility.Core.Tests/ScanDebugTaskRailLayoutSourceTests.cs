using System.Xml.Linq;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "ScanDebugLayout")]
public sealed class ScanDebugTaskRailLayoutSourceTests
{
    [Fact]
    public void BlackWhiteDetail_UsesExistingFullWidthConfigurationRouteAndTaskReturn()
    {
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var detail = Named(xaml, "ChannelCalibrationDetailsExpander");

        Assert.Contains("_isConfigurationWorkspaceOpen = true;\n        SetActiveWorkbenchSection(2);",
            page.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("_isConfigurationWorkspaceOpen = false;", page, StringComparison.Ordinal);
        Assert.Contains("SetActiveWorkbenchSection(_lastWorkbenchTaskSectionIndex);", page, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationDetailsExpander.Visibility = ToVisibility(showChannelDetail);", page, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationPrimaryActions.Visibility = ToVisibility(index == 2 && !showChannelDetail);", page, StringComparison.Ordinal);
        Assert.Contains("inspectionOpen, _isConfigurationWorkspaceOpen", page, StringComparison.Ordinal);
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
        Assert.Equal("3", (string?)footer.Attribute("Grid.Row"));
        Assert.Contains(footer, Named(xaml, "ChannelCalibrationRail").Elements());
        Assert.Equal(4, Named(xaml, "WorkbenchTaskSelectorBar").Elements().Count());
        Assert.Equal(4, Named(xaml, "WorkbenchTaskComboBox").Elements().Count());
    }

    [Fact]
    public void BlackWhiteTask_ReusesIlluminationAndSampleControlsWithoutDuplicatingCommands()
    {
        var page = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var xaml = ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml");

        Assert.Contains("ChannelCalibrationPrimaryActions.Children.Insert(0, ChannelCalibrationIlluminationCard);", page, StringComparison.Ordinal);
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
                && (string?)element.Attribute("Text") == $"{{Binding Header, ElementName={first}}}");
            Assert.Contains(grid.Elements(), element => (int?)element.Attribute("Grid.Row") == inputRow - 1
                && (string?)element.Attribute("Text") == $"{{Binding {(second.Contains("UnitComboBox", StringComparison.Ordinal) ? "Tag" : "Header")}, ElementName={second}}}");
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
    public void Sampling_TransportFieldsPrecedeChannelsAndDetailsShowOnlyExecutionOrder()
    {
        var xaml = XDocument.Parse(ReadSource("PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var sampling = Named(xaml, "AcquisitionPlanSection");
        var children = Named(xaml, "AcquisitionRowsComboBox").Parent!.Elements().ToList();
        var transport = Named(xaml, "AcquisitionTransportDetailsExpander");
        var channels = Named(xaml, "AcquisitionChannelAssignmentList");
        var preview = Assert.Single(sampling.Descendants(), element => (string?)element.Attribute(XName.Get("Uid", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ScanDebug_PreviewToggleSwitch");

        Assert.True(children.IndexOf(Named(xaml, "AcquisitionRowsComboBox")) < children.IndexOf(transport));
        Assert.True(children.IndexOf(transport) < children.IndexOf(channels));
        Assert.True(children.IndexOf(channels) < children.IndexOf(preview));
        Assert.Equal("{x:Bind ViewModel.IsScanMotorTransportEnabled, Mode=OneWay}", (string?)transport.Attribute("IsExpanded"));
        Assert.Contains(Named(xaml, "AcquisitionScanMotorComboBox"), transport.Descendants());
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
        Assert.Contains("availableWidth >= taskSelectorWidth + toolbarWidth + spacing", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("var isTaskSelectorBarAvailable = availableWidth >= ScanWorkbenchPreviewLayout.WideThreshold;", layout, StringComparison.Ordinal);
        Assert.True(layout.IndexOf("FindWorkbenchFocusReplacement(focusedElement", StringComparison.Ordinal)
            < layout.IndexOf("WorkbenchTaskSelectorBar.Visibility =", StringComparison.Ordinal));
        Assert.Equal("Left", (string?)Named(xaml, "RunStatusPanel").Attribute("HorizontalAlignment"));
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

        Assert.Contains("var modeWidth = MeasureNaturalWidth(RunCaptureModePanel);", layout, StringComparison.Ordinal);
        Assert.Contains("var stopWidth = MeasureNaturalWidth(RunActionPanel);", layout, StringComparison.Ordinal);
        Assert.Contains("var wrapRunActions = availableWidth < modeWidth + stopWidth + spacing;", layout, StringComparison.Ordinal);
        Assert.Contains("var wrapRunStatus = wrapRunActions || availableWidth < modeWidth + statusWidth + recoveryWidth + stopWidth + spacing * 3;", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(RunActionPanel, wrapRunActions ? 1 : 0);", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(RunActionPanel, wrapRunActions ? 0 : 2);", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(RunStatusPanel, wrapRunActions ? 2 : wrapRunStatus ? 1 : 0);", layout, StringComparison.Ordinal);
        Assert.Contains("var statusWidth = MeasureNaturalWidth(RunStatusTextPanel);", layout, StringComparison.Ordinal);
        Assert.Contains("var recoveryWidth = MeasureNaturalWidth(RunRecoveryActionsPanel);", layout, StringComparison.Ordinal);
        Assert.Contains("var stackRunStatusActions = statusAvailableWidth < statusWidth + recoveryWidth + spacing;", layout, StringComparison.Ordinal);
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
    public void CompactPane_HeaderShowsPreviewToolsOnlyWithPreviewAndPreservesNavigation(
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
        Assert.Contains("InspectionToggleButton.Visibility = _isConfigurationWorkspaceOpen ? Visibility.Collapsed : Visibility.Visible;", layout, StringComparison.Ordinal);
        Assert.Contains("OpenPreviewButton.Visibility = ToVisibility(layout.IsPreviewOpenButtonVisible);", layout, StringComparison.Ordinal);
        Assert.Contains("BackToEditorButton.Visibility = ToVisibility(layout.IsBackToEditorButtonVisible);", layout, StringComparison.Ordinal);
        Assert.Contains("WorkbenchTaskComboBox.Visibility = ToVisibility(!_isConfigurationWorkspaceOpen && !isTaskSelectorBarAvailable);", layout, StringComparison.Ordinal);
        Assert.Contains("var previewEntryWidth = layout.IsPreviewOpenButtonVisible ? MeasureNaturalWidth(OpenPreviewButton) + spacing : 0;", layout, StringComparison.Ordinal);
        Assert.Contains("taskSelectorWidth + toolbarWidth + spacing + previewEntryWidth", layout, StringComparison.Ordinal);
        Assert.Contains("? MeasureNaturalWidth(WorkspacePreviewToolbar) - backExtraWidth", layout, StringComparison.Ordinal);
        Assert.Contains(": MeasureNaturalWidth(InspectionToggleButton);", layout, StringComparison.Ordinal);
        Assert.Contains("InspectionToggleButton.Visibility = inspectionVisibility;", layout, StringComparison.Ordinal);
        Assert.Contains("(IsFocusedWithin(focusedElement, RawSignalModeSelector) || IsFocusedWithin(focusedElement, PreviewToolbar))", focus, StringComparison.Ordinal);
        Assert.Contains("return layout.IsPreviewOpenButtonVisible ? OpenPreviewButton : InspectionToggleButton;", focus, StringComparison.Ordinal);
        Assert.Contains("focusHandoffVersion != _workbenchFocusHandoffVersion", layout, StringComparison.Ordinal);
        Assert.Contains("!IsCurrentActivation(activationEpoch)", layout, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(InspectionToggleButton, inspectionActionLabel", layout, StringComparison.Ordinal);
        Assert.Contains("if (!Equals(InspectionToggleButton.Content, inspectionActionLabel))", layout, StringComparison.Ordinal);
        Assert.Contains("? (_isNarrowPreviewOpen ? OpenPreviewButton.Content : BackToEditorButton.Content)", layout, StringComparison.Ordinal);
        Assert.Same(toolbar, Named(xaml, "InspectionToggleButton").Parent);
        Assert.Same(Named(xaml, "PreviewHeaderGrid"), Named(xaml, "RawSignalModeSelector").Parent);
        Assert.Same(Named(xaml, "PreviewHeaderGrid"), Named(xaml, "PreviewToolbar").Parent);
        Assert.Contains("WorkspacePreviewToolbar.Children.Insert(0, RawSignalModeSelector);", page, StringComparison.Ordinal);
        Assert.Contains("WorkspacePreviewToolbar.Children.Insert(1, PreviewToolbar);", page, StringComparison.Ordinal);
        Assert.Same(workspace, Named(xaml, "OpenPreviewButton").Parent);
        Assert.Equal("ScanDebug_WorkbenchInspectionButton", (string?)Named(xaml, "InspectionToggleButton").Attribute(XName.Get("Uid", "http://schemas.microsoft.com/winfx/2006/xaml")));
    }

    [Theory]
    [InlineData("en-us", "Signal inspection", "Open preview", "Back to editor")]
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
        Assert.Contains("? (_isNarrowPreviewOpen ? OpenPreviewButton.Content : BackToEditorButton.Content)\n            : _inspectionActionLabel;",
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
        Assert.Contains("下一次采集", Resource("zh-CN", "ScanDebug_CurrentIlluminationHelpText.Text"), StringComparison.Ordinal);
        Assert.Contains("local input only", Resource("en-us", "ScanDebug_ImageReferenceRoiEditorScopeText.Text"), StringComparison.Ordinal);
        Assert.Contains("仅更改本地输入框", Resource("zh-CN", "ScanDebug_ImageReferenceRoiEditorScopeText.Text"), StringComparison.Ordinal);
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
