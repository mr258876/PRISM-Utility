#if PRISM_VISUAL_QA
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Views;

public sealed partial class ScanDebugPage
{
    private bool _visualQaDirtyRowsRoundTripPassed;
    private double? _visualQaRequestedRootWidth;
    private double? _visualQaRequestedRootHeight;
    private string? _visualQaConfigurationMenuCapturePath;
    private string? _visualQaScannerConnectionCapturePath;

    internal async Task ApplyWorkbenchVisualQaAsync(string? scenario, double? rootWidth, double? rootHeight, string? theme)
    {
        if (ViewModel.IsConnected)
            throw new InvalidOperationException("Workbench visual QA requires an offline session.");

        ((Flyout)((Button)FindVisualQaElement(this, "OperationFeedbackButton")!).Flyout).Hide();
        ObservationSelectionFlyout.Hide();
        ObservationCompareFlyout.Hide();
        ((Flyout)PreviewAuxiliaryToolsButton.Flyout).Hide();
        if (scenario?.StartsWith("fluent", StringComparison.Ordinal) == true)
        {
            ConfigurationFileFlyout.Hide();
            FilmProfileWorkflowFlyout.Hide();
            GetVisualQaScannerConnectionFlyout()?.Hide();
        }
        if (Enum.TryParse<ElementTheme>(theme, true, out var requestedTheme)
            && App.MainWindow.Content is FrameworkElement content)
            content.RequestedTheme = requestedTheme;

        if (rootWidth is > 0 && rootHeight is > 0)
        {
            _visualQaRequestedRootWidth = rootWidth;
            _visualQaRequestedRootHeight = rootHeight;
            await ResizeWorkbenchVisualQaRootAsync(rootWidth.Value, rootHeight.Value);
        }

        FrameworkElement? alignmentTarget = null;
        switch (scenario)
        {
            case "fluentIncomplete":
                GetWorkbenchVisualQaWorkspace().RestoreEditingDraft(ScanFilmProfileDraft.CreateDefault());
                ViewModel.PreviewFrame = null;
                ViewModel.PendingCalibrationResult = null;
                FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
                _isAuxiliaryPanelOpen = false;
                _isNarrowPreviewOpen = false;
                _previewCollapsedOverride = false;
                SelectWorkbenchTask(0);
                break;
            case "fluentStatusFixture":
                // Synthetic editor data only: no repository save, file handoff, or device command.
                // Valid parameters with only one configured reference pair exercise the distinction
                // between parameter validity, reference coverage, and offline device readiness.
                var parameters = new ScanParameterSnapshot(1200, -4, 21, 3, 22, 52000);
                var profiles = new[] { "Red", "Green", "Blue", "White" }.ToDictionary(
                    role => role,
                    role => new ScanChannelCalibrationProfile(parameters, ScanCalibrationRoiSettings.CreateDefault(),
                        role == "Red" ? (ushort)512 : null, role == "Red" ? (ushort)60120 : null));
                var acquisition = ScanFilmAcquisitionSettings.CreateDefault() with
                {
                    TargetLinePitchMicrometers = 5,
                    AcquisitionChannelAssignment = new("Red", "Green", "Blue", "White", false, false, false, false)
                };
                GetWorkbenchVisualQaWorkspace().RestoreEditingDraft(new ScanFilmProfileDraft(
                    "Fluent QA · Partial references", DateTimeOffset.UtcNow, profiles, "Red", acquisition, null));
                ViewModel.PreviewFrame = null;
                ViewModel.PendingCalibrationResult = null;
                ViewModel.ValidateFilmProfileCommand.Execute(null);
                await Task.Delay(100);
                FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
                _isAuxiliaryPanelOpen = false;
                _isNarrowPreviewOpen = false;
                _previewCollapsedOverride = false;
                SelectWorkbenchTask(0);
                break;
            case "fluentSampling": SelectWorkbenchTask(0); break;
            case "fluentBlackWhite": SelectWorkbenchTask(1); break;
            case "fluentFocus": SelectWorkbenchTask(2); break;
            case "fluentMotion": SelectWorkbenchTask(3); break;
            case "fluentConfigurationMenu":
                ConfigurationFileFlyout.ShowAt(ConfigurationFileMenuButton);
                break;
            case "fluentValidation":
                ViewModel.ValidateFilmProfileCommand.Execute(null);
                break;
            case "fluentPreview":
                FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
                OpenPreviewButton_Click(this, new RoutedEventArgs());
                break;
            case "fluentConnection":
                var connectionAction = FindVisualQaElement(this, "PreviewConnectionAction") as Button
                    ?? throw new InvalidOperationException("The empty preview has no connection action.");
                if (!connectionAction.IsEnabled || !IsVisibleInTree(connectionAction))
                    throw new InvalidOperationException("The empty-preview connection action is not available.");
                // Exercise the button's actual Click wiring; this opens connection details only.
                var peer = FrameworkElementAutomationPeer.CreatePeerForElement(connectionAction)
                    ?? new ButtonAutomationPeer(connectionAction);
                if (peer.GetPattern(PatternInterface.Invoke) is not IInvokeProvider invoke)
                    throw new InvalidOperationException("The connection action does not expose Invoke.");
                invoke.Invoke();
                break;
            case "dirtyRowsRoundTrip":
                // Run once against the untouched startup editor. Do not reset an explicit
                // new-profile pending-save flag to make this check pass.
                var startupWorkspace = GetWorkbenchVisualQaWorkspace();
                var newProfilePending = (bool)typeof(ViewModels.ScanDebugViewModel).GetField("_isNewFilmProfilePendingExport",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(ViewModel)!;
                if (newProfilePending || ViewModel.HasUnsavedProfileChanges || startupWorkspace.Snapshot.IsDirty
                    || startupWorkspace.Snapshot.CurrentDraft.ChannelProfiles.Count != 0 || ViewModel.SelectedRows != "128")
                    throw new InvalidOperationException("Dirty-state native QA needs the untouched incomplete startup editor, with no explicit new-profile save pending.");
                ViewModel.SelectedRows = "333";
                if (!ViewModel.HasUnsavedProfileChanges)
                    throw new InvalidOperationException("An edited row count did not mark the incomplete editor as modified.");
                ViewModel.SelectedRows = "128";
                ViewModel.ValidateFilmProfileCommand.Execute(null);
                if (ViewModel.HasUnsavedProfileChanges || startupWorkspace.Snapshot.IsDirty
                    || ViewModel.CurrentFilmProfileValidationIssues.Count == 0)
                    throw new InvalidOperationException("Returning rows to 128 did not restore a clean incomplete editor.");
                _visualQaDirtyRowsRoundTripPassed = true;
                SelectWorkbenchTask(0);
                SynchronizeAcquisitionRowsEditor();
                break;
            case "manageChannelLibrary":
                BwManageChannelProfilesButton_Click(this, new RoutedEventArgs());
                break;
            case "rowsPreset":
                SelectWorkbenchTask(0);
                ViewModel.SelectedRows = "128";
                SynchronizeAcquisitionRowsEditor();
                break;
            case "clockDraft":
                ViewModel.SysClockMhz = "100";
                OpenDevicePropertiesButton_Click(this, new RoutedEventArgs());
                break;
            case "exposureRange":
                ViewModel.SysClockMhz = "100";
                ViewModel.ExposureMicroseconds = "1000";
                BwConfigureChannelButton_Click(this, new RoutedEventArgs());
                break;
            case "toolbarValidate":
                ViewModel.ValidateFilmProfileCommand.Execute(null);
                break;
            case "toolbarIssues":
            case "toolbarReview":
                ViewModel.ValidateFilmProfileCommand.Execute(null);
                break;
            case "toolbarNotice":
                typeof(ViewModels.ScanDebugViewModel).GetMethod("ReportOperationFailure",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(ViewModel, ["Offline visual QA", "Offline visual QA: sample operation failed."]);
                break;
            case "toolbarFeedback":
                var feedback = (Button)FindVisualQaElement(this, "OperationFeedbackButton")!;
                feedback.Flyout.ShowAt(feedback);
                break;
            case "toolbarAcknowledge":
                foreach (var notice in ViewModel.OperationNotices.ToArray())
                    ViewModel.AcknowledgeOperationNoticeCommand.Execute(notice.Id);
                break;
            case "alignmentGeneral":
                OpenConfigurationButton_Click(this, new RoutedEventArgs());
                BasicInfoScrollViewer.ChangeView(null, 0, null, true);
                break;
            case "alignmentColor":
                OpenConfigurationButton_Click(this, new RoutedEventArgs());
                alignmentTarget = ProfileTargetWhitePointComboBox;
                break;
            case "alignmentDevice":
                OpenDevicePropertiesButton_Click(this, new RoutedEventArgs());
                ((ScrollViewer)DeviceSettingsSection.Children[0]).ChangeView(null, 0, null, true);
                break;
            case "alignmentMapping":
                OpenDevicePropertiesButton_Click(this, new RoutedEventArgs());
                alignmentTarget = FocusMappingZPositiveDirectionComboBox;
                break;
            case "alignmentSampling":
                ViewModel.SelectedCaptureMode = ViewModel.CaptureModeOptions[2];
                SelectWorkbenchTask(0);
                AcquisitionTransportDetailsExpander.IsExpanded = true;
                alignmentTarget = AcquisitionMotorDistancePerLineTextBox;
                break;
            case "alignmentIllumination":
                PrismVisualQaPendingCalibrationHook.Seed(ViewModel);
                ViewModel.PendingCalibrationResult = null;
                SelectWorkbenchTask(1);
                alignmentTarget = CurrentCalibrationIlluminationLevelTextBox;
                break;
            case "alignmentSession":
                OpenAdvancedButton_Click(this, new RoutedEventArgs());
                await Task.Delay(100);
                UpdateLayout();
                ((Expander)FindVisualQaElement(this, "SessionIlluminationTestExpander")!).IsExpanded = true;
                alignmentTarget = SessionTestLedIndexTextBox;
                break;
            case "alignmentMotion1":
            case "alignmentMotion2":
            case "alignmentMotion3":
                SelectWorkbenchTask(3);
                MotorAxisComboBox.SelectedIndex = scenario[^1] - '1';
                alignmentTarget = scenario[^1] == '1' ? Motor1SpeedValueTextBox : scenario[^1] == '2' ? Motor2SpeedValueTextBox : Motor3SpeedValueTextBox;
                break;
            case "alignmentFocus":
                SelectWorkbenchTask(2);
                await Task.Delay(100);
                UpdateLayout();
                ((Expander)FindVisualQaElement(this, "AdvancedAutofocusExpander")!).IsExpanded = true;
                alignmentTarget = AutofocusZProbeStepsTextBox;
                break;
            case "alignmentAdc":
                BwOpenChannelDetailsButton_Click(this, new RoutedEventArgs());
                await Task.Delay(100);
                UpdateLayout();
                ((Expander)FindVisualQaElement(this, "AdvancedAdcAdjustmentExpander")!).IsExpanded = true;
                alignmentTarget = Adc1OffsetTextBox;
                break;
            case "transportMotorIssue":
            case "transportDistanceIssue":
                ViewModel.SelectedCaptureMode = ViewModel.CaptureModeOptions[0];
                OnCurrentFilmProfileIssueNavigationRequested(new(
                    ScanFilmProfileIssueNavigationSection.AcquisitionPlan,
                    scenario == "transportMotorIssue" ? ScanFilmProfileIssueEditorTarget.ScanMotor : ScanFilmProfileIssueEditorTarget.MotorDistancePerLine,
                    scenario == "transportMotorIssue" ? "acquisitionSettings.scanMotorId" : "acquisitionSettings.targetLinePitchMicrometers", null));
                break;
            case "transportIssueReturn":
                SetActiveWorkbenchSection(3);
                SetActiveWorkbenchSection(1);
                break;
            case "empty":
                FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Collapsed;
                ViewModel.FilmProfileOperationIsOpen = false;
                _isAuxiliaryPanelOpen = false;
                CloseCandidateReview(returnFocus: false);
                ViewModel.ClearObservationBaselineCommand.Execute(null);
                if (ViewModel.IsObservationSnapshotVisible)
                    ViewModel.ToggleObservationFreezeCommand.Execute(null);
                ViewModel.PreviewFrame = null;
                ViewModel.PendingCalibrationResult = null;
                SetActiveWorkbenchSection(1);
                RawSignalModeSelector.SelectedItem = ImagePreviewModeItem;
                break;
            case "sample":
                CloseCandidateReview(returnFocus: false);
                PrismVisualQaPendingCalibrationHook.Seed(ViewModel);
                ViewModel.PendingCalibrationResult = null;
                SetActiveWorkbenchSection(2);
                RawSignalModeSelector.SelectedItem = ImagePreviewModeItem;
                break;
            case "blackWhiteEmpty":
                CloseCandidateReview(returnFocus: false);
                ViewModel.PendingCalibrationResult = null;
                ViewModel.PreviewFrame = null;
                SetActiveWorkbenchSection(2);
                break;
            case "channelGreen":
            case "channelRed":
                var role = scenario == "channelGreen" ? "Green" : "Red";
                CalibrationChannelSelectorBar.SelectedItem = CalibrationChannelSelectorBar.Items.Single(item => Equals(item.Tag, role));
                break;
            case "engineering": OpenAdvancedButton_Click(this, new RoutedEventArgs()); break;
            case "engineeringFocusTest":
                OpenAdvancedButton_Click(this, new RoutedEventArgs());
                ((Expander)FindVisualQaElement(this, "FocusMappingEngineeringExpander")!).IsExpanded = true;
                EngineeringToolsScrollViewer.ChangeView(null, 0, null, true);
                break;
            case "engineeringRaw":
                OpenAdvancedButton_Click(this, new RoutedEventArgs());
                var rawExpander = (Expander)FindVisualQaElement(this, "RawIlluminationExpander")!;
                rawExpander.IsExpanded = true;
                await Task.Delay(100);
                rawExpander.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false, VerticalAlignmentRatio = 0 });
                break;
            case "sameTaskReturn": WorkbenchTaskButton_Tapped(WorkbenchTaskSelectorBar.Items[Array.IndexOf(WorkbenchTaskSectionIndices, _lastWorkbenchTaskSectionIndex)], null!); break;
            case "configuration": OpenConfigurationButton_Click(this, new RoutedEventArgs()); break;
            case "deviceProperties": OpenDevicePropertiesButton_Click(this, new RoutedEventArgs()); break;
            case "channelDetails": BwOpenChannelDetailsButton_Click(this, new RoutedEventArgs()); break;
            case "workspaceReturn": ReturnToWorkbenchTaskButton_Click(this, new RoutedEventArgs()); break;
            case "pending":
                CloseCandidateReview(returnFocus: false);
                PrismVisualQaPendingCalibrationHook.Seed(ViewModel);
                SetActiveWorkbenchSection(2);
                break;
            case "candidate":
                OpenCandidateReview();
                break;
            case "return":
                CloseCandidateReview(returnFocus: true);
                break;
            case "focus": SetActiveWorkbenchSection(3); break;
            case "motion": SetActiveWorkbenchSection(5); break;
            case "sampling": SetActiveWorkbenchSection(1); break;
            case "preview": OpenPreviewButton_Click(this, new RoutedEventArgs()); break;
            case "previewToggle": PreviewToggleButton_Click(this, new RoutedEventArgs()); break;
            case "previewHidden": HidePreviewButton_Click(this, new RoutedEventArgs()); break;
            case "editorDraft":
                CurrentCalibrationIlluminationLevelTextBox.Text = "17";
                (_bwCompactMode ? ChannelCalibrationCompactScrollViewer : ChannelCalibrationScrollViewer).ChangeView(null, 140, null, true);
                break;
            case "rawInteractive":
                PrismVisualQaPendingCalibrationHook.SeedInteractiveRaw(ViewModel);
                RawSignalModeSelector.SelectedItem = RawSignalProfileModeItem;
                break;
            case "raw":
                PrismVisualQaPendingCalibrationHook.SeedRawDisplay(ViewModel);
                RawSignalModeSelector.SelectedItem = RawSignalProfileModeItem;
                break;
            case "freeze": ViewModel.ToggleObservationFreezeCommand.Execute(null); break;
            case "baseline": ViewModel.SetObservationBaselineCommand.Execute(null); break;
            case "showBaseline": ViewModel.ToggleObservationBaselineCommand.Execute(null); break;
            case "clearBaseline": ViewModel.ClearObservationBaselineCommand.Execute(null); break;
            case "range":
                ((Flyout)PreviewAuxiliaryToolsButton.Flyout).ShowAt(PreviewAuxiliaryToolsButton);
                await Task.Delay(150);
                ObservationSelectionFlyout.ShowAt(ObservationSelectionButton);
                break;
            case "compare":
                ((Flyout)PreviewAuxiliaryToolsButton.Flyout).ShowAt(PreviewAuxiliaryToolsButton);
                await Task.Delay(150);
                ObservationCompareFlyout.ShowAt(ObservationCompareButton);
                break;
        }
        await Task.Delay(350);
        UpdateLayout();
        if (alignmentTarget is not null)
        {
            alignmentTarget.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
            await Task.Delay(150);
            UpdateLayout();
        }
        // Scenario changes can alter the shell or page's measured layout after resizing.
        // A requested size is a contract, never merely the size we attempted to set.
        if (_visualQaRequestedRootWidth is { } width && _visualQaRequestedRootHeight is { } height)
            await ResizeWorkbenchVisualQaRootAsync(width, height);

        if (scenario == "fluentConfigurationMenu")
        {
            if (GetVisualQaConfigurationMenuPresenter() is not { IsLoaded: true } presenter)
                throw new InvalidOperationException("The configuration menu presenter is not open.");
            _visualQaConfigurationMenuCapturePath = await CaptureWorkbenchVisualQaPopupAsync(presenter, "configuration-menu");
        }
        if (scenario == "fluentConnection")
        {
            if (GetVisualQaScannerConnectionFlyout()?.Content is not FrameworkElement scannerContent
                || !scannerContent.IsLoaded)
                throw new InvalidOperationException("The scanner connection details did not open.");
            _visualQaScannerConnectionCapturePath = await CaptureWorkbenchVisualQaPopupAsync(scannerContent, "connection-details");
        }
    }

    private async Task<string> CaptureWorkbenchVisualQaPopupAsync(FrameworkElement target, string name)
    {
        var directory = Environment.GetEnvironmentVariable("PRISM_VISUAL_QA_REQUEST_DIRECTORY")
            ?? throw new InvalidOperationException("Visual QA capture requires an isolated request directory.");
        var path = Path.Combine(Path.GetFullPath(directory), $"{Guid.NewGuid():N}-{name}.png");
        var renderTarget = new RenderTargetBitmap();
        await renderTarget.RenderAsync(target);
        var pixels = await renderTarget.GetPixelsAsync();
        using (File.Create(path)) { }
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
            (uint)renderTarget.PixelWidth, (uint)renderTarget.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
        return path;
    }

    private async Task ResizeWorkbenchVisualQaRootAsync(double width, double height)
    {
        await WaitForWorkbenchVisualQaLayoutAsync();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var widthError = width - ScanDebugRootGrid.ActualWidth;
            var heightError = height - ScanDebugRootGrid.ActualHeight;
            if (Math.Abs(widthError) <= 0.5 && Math.Abs(heightError) <= 0.5)
                return;

            var size = App.MainWindow.AppWindow.Size;
            var scale = XamlRoot.RasterizationScale;
            App.MainWindow.AppWindow.Resize(new SizeInt32(
                size.Width + (int)Math.Round(widthError * scale),
                size.Height + (int)Math.Round(heightError * scale)));
            await WaitForWorkbenchVisualQaLayoutAsync();
        }

        if (Math.Abs(width - ScanDebugRootGrid.ActualWidth) <= 0.5
            && Math.Abs(height - ScanDebugRootGrid.ActualHeight) <= 0.5)
            return;

        var finalSize = App.MainWindow.AppWindow.Size;
        throw new InvalidOperationException(
            $"Visual QA root size did not converge: requested {width}x{height}, "
            + $"actual {ScanDebugRootGrid.ActualWidth}x{ScanDebugRootGrid.ActualHeight}, "
            + $"window pixels {finalSize.Width}x{finalSize.Height}, scale {XamlRoot.RasterizationScale}, "
            + $"window minimum DIP {App.MainWindow.MinWidth}x{App.MainWindow.MinHeight}.");
    }

    private async Task WaitForWorkbenchVisualQaLayoutAsync()
    {
        // Wait for both the native window and XAML layout to settle. The first page
        // measurement can precede shell margins and the NavigationView display mode.
        (double Width, double Height, int WindowWidth, int WindowHeight)? previous = null;
        var stableSamples = 0;
        for (var sample = 0; sample < 16; sample++)
        {
            await Task.Delay(75);
            UpdateLayout();
            var size = App.MainWindow.AppWindow.Size;
            var current = (ScanDebugRootGrid.ActualWidth, ScanDebugRootGrid.ActualHeight, size.Width, size.Height);
            if (current.Item1 > 0 && current.Item2 > 0 && previous == current)
            {
                if (++stableSamples >= 3)
                    return;
            }
            else
            {
                stableSamples = 0;
            }
            previous = current;
        }
        throw new InvalidOperationException("Visual QA could not obtain a stable native window and page layout.");
    }

    internal object GetWorkbenchVisualQaState()
    {
        var windowRoot = (FrameworkElement)App.MainWindow.Content;
        var rootOrigin = ScanDebugRootGrid.TransformToVisual(windowRoot).TransformPoint(default);
        var windowScreenBounds = GetVisualQaScreenBounds(windowRoot);
        var menuPresenter = GetVisualQaConfigurationMenuPresenter();
        string[] names = ["RunStatusPanel", "RunStatusTextPanel", "RunRecoveryActionsPanel", "FilmProfileLifecycleHeader", "FilmProfileLifecycleActionsPanel", "WorkbenchWorkspaceHeader", "WorkspacePreviewToolbar", "PreviewToggleButton", "PreviewToolbar", "RawSignalModeSelector", "ConfigurationFileMenuButton", "SaveConfigurationFileButton", "WorkbenchConfigurationButton", "WorkbenchValidationButton", "ObservationCompareContent", "OperationFeedbackButton", "FreezeObservationButton", "ZoomScaleComboBox", "PreviewAuxiliaryToolsButton", "FilmProfileLifecycleDetailsScrollViewer", "ScanDebugRootGrid", "WorkbenchRunBar", "StartScanButton", "StopScanButton", "StopAllMotorsButton",
            "WorkbenchPreviewColumnContent", "PreviewScrollViewer", "ObservationContextPanel", "ObservationSelectionContent",
            "ChannelCalibrationRail", "CalibrationCandidateReviewPanel", "CandidateReturnToParametersButton",
            "PendingCalibrationAcceptDeviceOnlyButton", "PendingCalibrationAcceptAndSaveButton",
            "PendingCalibrationRestoreOriginalButton", "PendingCalibrationCancelButton", "CalibrationChannelFallbackComboBox",
            "CalibrationChannelSelectorBar", "ChannelCalibrationSection", "ChannelCalibrationScrollViewer", "ChannelCalibrationSaveFooter",
            "WorkbenchAuxiliaryHeader", "ReturnToWorkbenchTaskButton", "BwRoiSummary",
            "WorkbenchSectionSelectorBar", "WorkbenchTaskSelectorBar", "WorkbenchTaskComboBox", "PropertiesEngineeringTab",
            "BasicInfoSection", "DeviceSettingsSection", "EngineeringToolsSection", "BasicInfoScrollViewer",
            "EngineeringToolsScrollViewer", "FocusMappingEngineeringExpander", "SessionIlluminationTestExpander", "RawIlluminationExpander",
            "RawIlluminationLed1Card", "RawLed1LevelTextBox", "RawLed1SteadyToggle",
            "AcquisitionTransportDetailsExpander", "AcquisitionDeviceMappingButton", "AcquisitionMotorDistancePerLineTextBox"];
        string[] alignmentNames = ["WorkbenchProfileSummaryGrid", "CurrentProfileFileNameText", "ProfileSaveStateText", "WorkbenchTitleTextBlock", "RunPresentationText", "RunContextText", "RunScannerConnectionButton", "WorkbenchParameterSurface", "WorkbenchAuxiliaryTitle", "ProfileAlignmentModeComboBox", "ProfileDngExportModeComboBox", "ProfileRedWavelengthTextBox", "ProfileGreenWavelengthTextBox", "ProfileBlueWavelengthTextBox", "ProfileOutputGammaTextBox", "ProfileTargetWhitePointComboBox", "ProfileManualWhitePointColorTemperatureTextBox", "AcquisitionMotorDistancePerLineTextBox", "AcquisitionMotorDistancePerLineUnitComboBox", "CurrentCalibrationIlluminationLevelTextBox", "CurrentCalibrationIlluminationPulseClockTextBox", "DeviceClockMhzTextBox", "ApplyDeviceClockButton", "FocusMappingLeftMotorComboBox", "FocusMappingRightMotorComboBox", "FocusMappingZPositiveDirectionComboBox", "FocusMappingTiltPositiveDirectionComboBox", "SessionTestLedIndexTextBox", "SessionTestLedLevelTextBox", "AutofocusZProbeStepsTextBox", "AutofocusMotorIntervalUsTextBox", "Adc1OffsetTextBox", "Adc1GainTextBox", "Motor1MoveValueTextBox", "Motor1MoveUnitComboBox", "Motor1SpeedValueTextBox", "Motor1SpeedUnitComboBox", "Motor2MoveValueTextBox", "Motor2MoveUnitComboBox", "Motor2SpeedValueTextBox", "Motor2SpeedUnitComboBox", "Motor3MoveValueTextBox", "Motor3MoveUnitComboBox", "Motor3SpeedValueTextBox", "Motor3SpeedUnitComboBox"];
        string[] improvementNames = ["AcquisitionRowsComboBox", "AcquisitionChannelAssignmentList", "SaveAndOpenScanButton",
            "FilmProfileSaveHintTextBlock", "ChannelCalibrationDetailsGrid", "ChannelCalibrationParametersCard",
            "ChannelCalibrationLibraryCard", "ChannelCalibrationStatusCard", "ExposureTimingDetailsExpander",
            "CalibrationCopySourceComboBox", "ChannelCalibrationCompactScrollViewer", "FilmProfileWorkflowGuide",
            "FilmProfileWorkflowTitle", "FilmProfileWorkflowNextButton", "WorkbenchCalibrationStatusButton",
            "SaveFilmProfileDraftButton", "LoadFilmProfileDraftButton", "PreviewConnectionAction",
            "PreviewStartSamplingAction", "RunActionPanel", "CurrentFilmProfileValidationInspector",
            "CloseValidationReviewButton", "PreviewEmptyStateGrid"];
        var elements = names.Concat(alignmentNames).Concat(improvementNames).Concat(alignmentNames.Select(name => "FieldHeader_" + name)).Distinct().Select(name =>
        {
            var target = FindName(name) as FrameworkElement ?? FindVisualQaElement(this, name)
                ?? VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot)
                    .Select(popup => FindVisualQaElement(popup.Child, name)).FirstOrDefault(element => element is not null);
            if (target is null) return new { name, present = false, visible = false, x = 0d, y = 0d, width = 0d, height = 0d };
            var point = target.IsLoaded ? target.TransformToVisual(ScanDebugRootGrid).TransformPoint(default) : default;
            return new { name, present = true, visible = target.IsLoaded && IsVisibleInTree(target), x = point.X, y = point.Y, width = target.ActualWidth, height = target.ActualHeight };
        }).ToArray();
        return new
        {
            windowRootWidth = windowRoot.ActualWidth, windowRootHeight = windowRoot.ActualHeight,
            windowWidthPixels = App.MainWindow.AppWindow.Size.Width,
            windowHeightPixels = App.MainWindow.AppWindow.Size.Height,
            windowMinWidth = App.MainWindow.MinWidth, windowMinHeight = App.MainWindow.MinHeight,
            requestedRootWidth = _visualQaRequestedRootWidth, requestedRootHeight = _visualQaRequestedRootHeight,
            windowScreenWidthPixels = windowScreenBounds.Width, windowScreenHeightPixels = windowScreenBounds.Height,
            rootOriginX = rootOrigin.X, rootOriginY = rootOrigin.Y,
            validationActionText = ViewModel.WorkbenchValidationActionText,
            parameterStatusText = ViewModel.WorkbenchParameterStatusText,
            parameterDetailText = ViewModel.WorkbenchParameterDetailText,
            calibrationStatusText = ViewModel.WorkbenchCalibrationStatusText,
            calibrationDetailText = ViewModel.WorkbenchCalibrationDetailText,
            deviceStatusText = ViewModel.WorkbenchDeviceStatusText,
            offlineGuidanceVisible = ViewModel.WorkbenchOfflineGuidanceVisibility == Visibility.Visible,
            selectedChannelCount = ViewModel.WorkbenchSelectedChannelCount,
            configuredReferenceCount = ViewModel.WorkbenchConfiguredReferenceCount,
            isConnected = ViewModel.IsConnected,
            canStartSampling = ViewModel.StartScanCommand.CanExecute(null),
            motorStatusText = ViewModel.Motor1DisplayStatusText,
            autofocusStatusText = ViewModel.AutofocusDisplayStatusText,
            profileDraftSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(GetWorkbenchVisualQaWorkspace().Snapshot.CurrentDraft))),
            scannerConnectionOpen = GetVisualQaScannerConnectionFlyout()?.Content is FrameworkElement scannerContent
                && scannerContent.IsLoaded && IsVisibleInTree(scannerContent),
            configurationMenuCapturePath = _visualQaConfigurationMenuCapturePath,
            scannerConnectionCapturePath = _visualQaScannerConnectionCapturePath,
            configurationMenuWidth = menuPresenter?.ActualWidth ?? 0,
            configurationMenuHeight = menuPresenter?.ActualHeight ?? 0,
            configurationMenuItems = ConfigurationFileFlyout.Items.OfType<MenuFlyoutItem>().Select(item =>
            {
                // Popup and page visual roots differ. UI Automation supplies physical screen
                // bounds for placement; TransformToVisual is valid only inside the presenter.
                var screenBounds = item.IsLoaded ? GetVisualQaScreenBounds(item) : default;
                var localPoint = item.IsLoaded && menuPresenter is not null
                    ? item.TransformToVisual(menuPresenter).TransformPoint(default) : default;
                return new
                {
                    text = item.Text, enabled = item.IsEnabled, visible = item.IsLoaded && IsVisibleInTree(item),
                    x = (screenBounds.X - windowScreenBounds.X) / XamlRoot.RasterizationScale,
                    y = (screenBounds.Y - windowScreenBounds.Y) / XamlRoot.RasterizationScale,
                    width = screenBounds.Width / XamlRoot.RasterizationScale,
                    height = screenBounds.Height / XamlRoot.RasterizationScale,
                    localX = localPoint.X, localY = localPoint.Y,
                    localWidth = item.ActualWidth, localHeight = item.ActualHeight
                };
            }).ToArray(),
            validationStateText = ViewModel.CurrentFilmProfileValidationStateText,
            validationIssueCount = ViewModel.CurrentFilmProfileValidationIssues.Count,
            operationNoticeCount = ViewModel.OperationNotices.Count,
            operationFeedbackActionText = ViewModel.OperationFeedbackActionText,
            rootWidth = ScanDebugRootGrid.ActualWidth, rootHeight = ScanDebugRootGrid.ActualHeight,
            scale = XamlRoot.RasterizationScale, theme = ActualTheme.ToString(),
            candidateReviewOpen = _isCandidateReviewOpen, selectedChannel = ViewModel.SelectedCalibrationChannel,
            auxiliaryPanelOpen = _isAuxiliaryPanelOpen,
            previewVisible = WorkbenchPreviewColumnContent.Visibility == Visibility.Visible,
            previewToggleActionText = PreviewToggleButton.Content?.ToString(),
            previewCollapsedOverride = _previewCollapsedOverride,
            narrowPreviewOpen = _isNarrowPreviewOpen,
            transportIssueOpen = _isAcquisitionTransportIssueOpen,
            captureMode = ViewModel.SelectedCaptureMode.ToString(),
            runContextVisibility = RunContextVisibility(ViewModel.RunContextText).ToString(),
            propertySectionCount = WorkbenchSectionSelectorBar.Items.Count,
            lastTaskSection = _lastWorkbenchTaskSectionIndex,
            profileFileName = ViewModel.FilmProfileFileName,
            profileDisplayName = ViewModel.CurrentProfileNameText,
            hasProfileNameEditor = FindName("ProfileNameTextBox") is not null, activeSection = _activeWorkbenchSectionIndex,
            channelOptions = CalibrationChannelSelectorBar.Items.Select(item => new { role = item.Tag, item.Text, item.IsSelected }).ToArray(),
            hasPageBackground = ScanDebugRootGrid.ReadLocalValue(Panel.BackgroundProperty) != DependencyProperty.UnsetValue,
            shellContentMargin = (App.MainWindow.Content as ShellPage)?.GetVisualQaContentMargin(),
            illuminationDraft = ViewModel.CurrentCalibrationIlluminationLevel,
            editorScrollOffset = (_bwCompactMode ? ChannelCalibrationCompactScrollViewer : ChannelCalibrationScrollViewer).VerticalOffset,
            focusedControl = (FocusManager.GetFocusedElement(XamlRoot) as FrameworkElement)?.Name,
            selectedRows = ViewModel.SelectedRows,
            rowsEditorText = AcquisitionRowsComboBox.Text,
            rowsEditorSelection = AcquisitionRowsComboBox.SelectedItem,
            issueFocusPending = _issueTargetFocusControl?.Name,
            issueBringRequested = _issueTargetBringIntoViewRequested,
            transportExpanded = AcquisitionTransportDetailsExpander.IsExpanded,
            canSaveProfile = ViewModel.CanSaveFilmProfileFile,
            hasUnsavedProfileChanges = ViewModel.HasUnsavedProfileChanges,
            workspaceIsDirty = GetWorkbenchVisualQaWorkspace().Snapshot.IsDirty,
            workspaceChannelCount = GetWorkbenchVisualQaWorkspace().Snapshot.CurrentDraft.ChannelProfiles.Count,
            dirtyRowsRoundTripPassed = _visualQaDirtyRowsRoundTripPassed,
            workflowGuideStep = ViewModel.WorkflowGuideStep.ToString(),
            workflowGuideTitle = ViewModel.WorkflowGuideTitle,
            workflowGuideDetail = ViewModel.WorkflowGuideDetail,
            workflowGuideActionText = ViewModel.WorkflowGuideActionText,
            workflowGuideActionEnabled = ViewModel.WorkflowGuideActionEnabled,
            saveHint = ViewModel.FilmProfileSaveHintText,
            deviceClock = ViewModel.DeviceClockDisplay,
            clockDraftStatus = ViewModel.ClockDraftStatusText,
            exposureRange = ViewModel.ExposureInputRangeText,
            ViewModel.HasObservationSample, ViewModel.HasObservationBaseline, ViewModel.IsObservationSnapshotVisible,
            ViewModel.ObservationModeText,
            summaryVisibility = ViewModel.ObservationSummaryVisibility.ToString(),
            warningVisibility = ViewModel.ObservationWarningVisibility.ToString(),
            ViewModel.ObservationSourceSummaryText, ViewModel.ObservationComparisonText,
            elements
        };
    }

    private IScanFilmProfileWorkspace GetWorkbenchVisualQaWorkspace()
        => (IScanFilmProfileWorkspace)typeof(ViewModels.ScanDebugViewModel).GetField("_filmProfileWorkspace",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(ViewModel)!;

    private static Flyout? GetVisualQaScannerConnectionFlyout()
        => (App.MainWindow.Content as ShellPage)?.FindName("ScannerConnectionFlyout") as Flyout;

    private MenuFlyoutPresenter? GetVisualQaConfigurationMenuPresenter()
    {
        DependencyObject? element = ConfigurationFileFlyout.Items.FirstOrDefault();
        while (element is not null && element is not MenuFlyoutPresenter)
            element = VisualTreeHelper.GetParent(element);
        return element as MenuFlyoutPresenter;
    }

    private static Windows.Foundation.Rect GetVisualQaScreenBounds(FrameworkElement element)
        => (FrameworkElementAutomationPeer.CreatePeerForElement(element)
            ?? new FrameworkElementAutomationPeer(element)).GetBoundingRectangle();

    private static FrameworkElement? FindVisualQaElement(DependencyObject root, string id)
    {
        if (root is FrameworkElement element && AutomationProperties.GetAutomationId(element) == id)
            return element;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            if (FindVisualQaElement(VisualTreeHelper.GetChild(root, index), id) is { } found)
                return found;
        return null;
    }
}
#endif
