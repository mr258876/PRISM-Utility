using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class FilmProfileSourceContractTests
{
    private static readonly string[] ExpectedNamedElements =
    [
        "ActualSizePreviewButton",
        "FitPreviewButton",
        "LineProfileChartRow",
        "LineProfileImageRow",
        "RawSignalFollowLatestToggleSwitch",
        "PreviewZoomStepPanel",
        "PreviewZoomContainer",
        "SaveConfigurationFileButton",
        "SaveAndOpenScanButton",
        "WorkbenchCalibrationStatusButton",
        "SaveFilmProfileDraftButton",
        "LoadFilmProfileDraftButton",
        "FilmProfileWorkflowGuide",
        "FilmProfileWorkflowNextButton",
        "FilmProfileWorkflowFlyout",
        "FilmProfileWorkflowConfigurationStepButton",
        "FilmProfileWorkflowCalibrationStepButton",
        "FilmProfileWorkflowReviewStepButton",
        "FilmProfileWorkflowScanStepButton",
        "PreviewToggleButton",
        "WorkbenchWorkspaceGrid",
        "AutoCalibrationActions",
        "AdvancedAdcAdjustmentExpander",
        "DeviceSettingsScrollViewer",
        "CalibrationGuideNextButton",
        "CalibrationGuideOverviewExpander",
        "CalibrationGuideOverviewFlyout",
        "ConfigurationFileMenuButton",
        "ConfigurationFileFlyout",
        "AutoCalibrateButton",
        "BwRoiEditButton",
        "WorkbenchValidationButton",
        "CloseValidationReviewButton",
        "DiscardStagedFilmProfileImportButton",
        "StartScanActionButton",
        "StopScanActionButton",
        "PropertiesEngineeringTab",
        "ChannelCalibrationSaveFooter",
        "AcquisitionMotorDistancePerLineUnitComboBox",
        "DeviceClockMhzTextBox",
        "DeviceClockInputsGrid",
        "FocusMappingLeftMotorComboBox",
        "FocusMappingRightMotorComboBox",
        "FocusMappingZPositiveDirectionComboBox",
        "FocusMappingTiltPositiveDirectionComboBox",
        "SessionTestLedIndexTextBox",
        "SessionTestLedLevelTextBox",
        "CalibrationChannelSelectorBar",
        "WorkbenchAuxiliaryHeader",
        "WorkbenchAuxiliaryTitle",
        "PendingCalibrationCandidateReviewCard",
        "ObservationContextPanel",
        "ObservationFeedbackPanel",
        "PendingCalibrationCandidateReviewEntry",
        "CalibrationCandidateReviewPanel",
        "CandidateReturnToParametersButton",
        "CalibrationCandidateReviewScrollViewer",
        "CalibrationCandidateActions",
        "ObservationCompareButton",
        "ObservationCompareFlyout",
        "ObservationCompareScrollViewer",
        "ObservationCompareContent",
        "ObservationSelectionButton",
        "ObservationSelectionFlyout",
        "ObservationSelectionScrollViewer",
        "ObservationSelectionContent",
        "FreezeObservationButton",
        "CurrentCalibrationIlluminationEditorGrid",
        "CurrentCalibrationIlluminationLevelTextBox",
        "CurrentCalibrationIlluminationPulseClockTextBox",
        "CurrentCalibrationIlluminationWorkModeComboBox",
        "LiveFocusCard",
        "AdvancedAutofocusContent",
        "AdvancedAutofocusResults",
        "DeviceClockContent",
        "AdvancedAdcAdjustmentContent",
        "FocusMappingContent",
        "ManualFocusNegativeButton",
        "ManualFocusPositiveButton",
        "IlluminationContent",
        "MotionContent",
        "MotionExpander",
        "MotorActionGrid",
        "MotorAxisComboBox",
        "Motor1StatusTextBlock",
        "Motor2StatusTextBlock",
        "Motor3StatusTextBlock",
        "Motor1DetailsExpander",
        "Motor2DetailsExpander",
        "Motor3DetailsExpander",
        "Motor1SettingsExpander",
        "Motor2SettingsExpander",
        "Motor3SettingsExpander",
        "Motor1MoveValueTextBox",
        "Motor1MoveUnitComboBox",
        "Motor1SpeedValueTextBox",
        "Motor1SpeedUnitComboBox",
        "Motor2MoveValueTextBox",
        "Motor2MoveUnitComboBox",
        "Motor2SpeedValueTextBox",
        "Motor2SpeedUnitComboBox",
        "Motor3MoveValueTextBox",
        "Motor3MoveUnitComboBox",
        "Motor3SpeedValueTextBox",
        "Motor3SpeedUnitComboBox",
        "Motor1MoveButton",
        "Motor2MoveButton",
        "Motor3MoveButton",
        "Motor1StopButton",
        "Motor2StopButton",
        "Motor3StopButton",
        "ZoomScaleComboBox",
        "ZoomOutButton",
        "ZoomInButton",
        "RawSignalModeSelector",
        "ImagePreviewModeItem",
        "RawSignalProfileModeItem",
        "PreviewDisplayToolsButton",
        "PreviewAuxiliaryToolsButton",
        "PreviewHeaderGrid",
        "PreviewToolbar",
        "PreviewDisplayToolsContent",
        "PreviewDisplayToolsFlyout",
        "PreviewDisplayToolsScrollViewer",
        "OverlayToolsContent",
        "OverlayToolsButton",
        "OverlayToolsFlyout",
        "OverlayToolsScrollViewer",
        "AdcRoiEditorCard",
        "AdcRoiDetailsExpander",
        "AdcRoiTargetComboBox",
        "AdcRoiStartTextBox",
        "AdcRoiEndTextBox",
        "Adc1OffsetTextBox",
        "Adc1GainTextBox",
        "Adc2OffsetTextBox",
        "Adc2GainTextBox",
        "ImageReferenceRoiEditorCard",
        "ReferenceColumnSampleStartTextBox",
        "ReferenceColumnSampleEndTextBox",
        "ManualReferenceLevelsCard",
        "ManualReferenceStatusTextBlock",
        "MissingCalibrationProfileEntry",
        "BwRoiSummary",
        "BwValidationActionPanel",
        "BwValidationSummaryTextBlock",
        "BwAutoActionGrid",
        "BwAutoBlackButton",
        "BwAutoWhiteButton",
        "BwReturnToParametersButton",
        "BwViewIssuesButton",
        "BwViewingIssuesText",
        "ChannelCalibrationRail",
        "ChannelCalibrationBodyRow",
        "ChannelCalibrationIdentityHeader",
        "ChannelCalibrationIssuePanel",
        "ChannelCalibrationCompactScrollViewer",
        "ChannelCalibrationDetailsExpander",
        "ChannelCalibrationDetailsGrid",
        "ChannelCalibrationParametersCard",
        "ChannelCalibrationLibraryCard",
        "ChannelCalibrationStatusCard",
        "ExposureTimingDetailsExpander",
        "ChannelCalibrationContentWidthHost",
        "ChannelCalibrationTaskStack",
        "ChannelCalibrationPrimaryActions",
        "ChannelCalibrationActionsCard",
        "ChannelCalibrationActionsStack",
        "ChannelCalibrationIlluminationCard",
        "ColumnSampleGroupTitle",
        "ColumnSampleUseStack",
        "ColumnSampleSaveActions",
        "UseSampleAsManualBlackLevelButton",
        "ClearManualReferenceLevelsButton",
        "CurrentIlluminationDraftLabel",
        "WhiteLightComponentsEditor",
        "WhiteLightSourceComboBox",
        "ChannelCalibrationIssueItemsControl",
        "ManualBlackLevelTextBox",
        "ManualWhiteLevelTextBox",
        "LiveCalibrationScrollViewer",
        "CaptureModeComboBox",
        "FocusRoiEditorCard",
        "FocusRoiDetailsExpander",
        "FocusRoiTargetComboBox",
        "FocusRoiStartTextBox",
        "FocusRoiEndTextBox",
        "AutofocusSampleRowsTextBox",
        "AutofocusTiltProbeStepsTextBox",
        "AutofocusZProbeStepsTextBox",
        "AutofocusMotorIntervalUsTextBox",
        "AutofocusMaxTiltIterationsTextBox",
        "AutofocusMaxZIterationsTextBox",
        "PreviewScrollViewer",
        "PreviewCanvas",
        "PreviewCanvasControl",
        "CursorReadoutBorder",
        "RoiCanvas",
        "AxisCanvas",
        "RawSignalProfileScrollViewer",
        "RawSignalProfileGrid",
        "RawSignalStatusTextBlock",
        "RawSignalRowTextBox",
        "RawSignalResultDetails",
        "RawSignalCanvasControl",
        "RawSignalAxisSwatch",
        "RawSignalProfileSwatch",
        "RawSignalHistogramSwatch",
        "RawSignalGridSwatch",
            "CalibrationChannelFallbackComboBox",
            "CalibrationChannelStatusListView",
            "ChannelCalibrationScrollViewer",
            "ChannelCalibrationLibraryActionsGrid",
            "PreviewEmptyStateGrid",
            "CursorPositionTextBlock",
            "CursorIntensityTextBlock",
            "CalibrationCopySourceComboBox",
            "CopyCalibrationProfileButton",
            "FilmProfileLifecycleHeader",
            "FilmProfileLifecycleActionsPanel",
        "FilmProfileOperationInfoBar",
        "FilmProfileLifecycleDetailsScrollViewer",
        "ScanDebugRootGrid",
        "StagedFilmProfileImportReview",
        "StagedFilmProfileImportValidationIssues",
        "WorkbenchSectionSelectorBar",
        "WorkbenchTaskSelectorBar",
        "WorkbenchTaskComboBox",
        "WorkbenchWorkspaceHeader",
        "WorkspacePreviewToolbar",
        "ReturnToWorkbenchTaskButton",
        "BasicInfoSection",
        "AcquisitionPlanSection",
        "AcquisitionPlanDetailsExpander",
        "AcquisitionTransportDetailsExpander",
        "AcquisitionTransportStrategyExpander",
        "ChannelCalibrationSection",
        "LiveCalibrationSection",
        "DeviceSettingsSection",
        "EngineeringToolsSection",
        "WorkbenchContentSplitGrid",
        "WorkbenchEditorColumn",
        "WorkbenchEditorColumnContent",
        "WorkbenchEditorRow",
        "WorkbenchPreviewSeparatorColumn",
        "WorkbenchPreviewColumn",
        "WorkbenchPreviewColumnContent",
        "WorkbenchPreviewInteractionHost",
        "BackToEditorButton",
        "PreviewSplitter",
        "PreviewGammaToggleSwitch",
        "PreviewGammaTextBox",
        "WaterfallPreviewOptionsPanel",
        "CurrentFilmProfileValidationInspector",
        "CurrentFilmProfileValidationIssues",
        "BasicInfoScrollViewer",
        "BasicCurrentFilmProfileValidationIssueScrollViewer",
        "AcquisitionPlanScrollViewer",
        "ProfileAlignmentModeComboBox",
        "ProfileDngExportModeComboBox",
        "ProfileColorManagementToggleSwitch",
        "ProfileRedWavelengthTextBox",
        "ProfileGreenWavelengthTextBox",
        "ProfileBlueWavelengthTextBox",
        "ProfileOutputGammaTextBox",
        "ProfileTargetWhitePointComboBox",
        "ProfileManualWhitePointColorTemperatureTextBox",
        "AcquisitionRowsComboBox",
        "AcquisitionDeviceMappingButton",
        "AcquisitionMotorDistancePerLineTextBox",
        "AcquisitionChannelAssignmentList",
        "AcquisitionTransportStrategyToggleSwitch",
        "ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer",
        "ExposureMicrosecondsTextBox",
        "WorkbenchRunBar",
        "WorkbenchRunContentGrid",
        "RunCaptureModePanel",
        "RunActionPanel",
        "RunStatusPanel",
        "RunStatusTextPanel",
        "RunRecoveryActionsPanel",
        "RunProgressBar",
        "EngineeringToolsScrollViewer",
        "MotionTaskSection",
        "MotionScrollViewer",
        "OperationLatestMessageLabel",
        "SamplingTaskButton",
        "BlackWhiteTaskButton",
        "FocusTaskButton",
        "MotionTaskButton",
        "InspectionToggleButton",
        "InspectionCloseButton",
        "InspectionEvidenceContent",
        "WorkbenchInspectionSeparatorColumn",
        "WorkbenchInspectionColumn",
        "WorkbenchInspectionRail",
        "WorkbenchValidationReviewColumn",
        "WorkbenchDataExportFlyout",
        "WorkbenchDataExportScrollViewer",
        "WorkbenchDataExportContent",
        "WorkbenchIdentityGrid",
        "WorkbenchTitleTextBlock",
        "WorkbenchProfileSummaryGrid"
    ];

    private static readonly IReadOnlyDictionary<string, int> ExpectedCommandBindingCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["StartScanCommand"] = 2,
            ["StopScanCommand"] = 1,
            ["ExportDngCommand"] = 1,
            ["NewFilmProfileCommand"] = 1,
            ["ValidateFilmProfileCommand"] = 1,
            ["SaveFilmProfileJsonCommand"] = 1,
            ["LoadFilmProfileJsonCommand"] = 2,
            ["ApplyStagedFilmProfileImportCommand"] = 1,
            ["DiscardStagedFilmProfileImportCommand"] = 1,
            ["CopyCalibrationProfileFromChannelCommand"] = 1,
            ["SaveChannelProfileCommand"] = 1,
            ["ClearChannelProfileCommand"] = 1,
            ["ApplyDeviceClockCommand"] = 1,
            ["ApplyParametersCommand"] = 1,
            ["AutoBlackAdjustCommand"] = 1,
            ["AutoWhiteAdjustCommand"] = 1,
            ["AutoCalibrateCommand"] = 1,
            ["AcceptPendingCalibrationCommand"] = 1,
            ["AcceptAndSavePendingCalibrationCommand"] = 1,
            ["RestorePendingCalibrationCommand"] = 1,
            ["CancelPendingCalibrationCommand"] = 1,
            ["SaveColumnSampleAsBlackLevelCommand"] = 1,
            ["SaveColumnSampleAsWhiteLevelCommand"] = 1,
            ["ApplyManualReferenceLevelsCommand"] = 1,
            ["RevertManualReferenceLevelsCommand"] = 1,
            ["ClearManualReferenceLevelsCommand"] = 1,
            ["UseColumnSampleAsManualBlackLevelCommand"] = 1,
            ["UseColumnSampleAsManualWhiteLevelCommand"] = 1,
            ["SaveFocusMappingCommand"] = 1,
            ["TestLeftFocusMappingCommand"] = 1,
            ["TestRightFocusMappingCommand"] = 1,
            ["ToggleObservationFreezeCommand"] = 1,
            ["SetObservationBaselineCommand"] = 1,
            ["ToggleObservationBaselineCommand"] = 1,
            ["ClearObservationBaselineCommand"] = 1,
            ["UseObservationRangeForCalibrationCommand"] = 1,
            ["QuickFocusCommand"] = 1,
            ["FineFocusCommand"] = 1,
            ["StopAllFocusCommand"] = 1,
            ["StopAllMotorsCommand"] = 1,
            ["RefreshIlluminationCommand"] = 1,
            ["ApplyIlluminationCommand"] = 1,
            ["CopyRawIlluminationToDraftCommand"] = 1,
            ["TestIlluminationOffCommand"] = 1,
            ["TestIlluminationSteadyCommand"] = 1,
            ["TestIlluminationAcquisitionSyncCommand"] = 1,
            ["RefreshMotionCommand"] = 2,
            ["EnableMotorCommand"] = 3,
            ["DisableMotorCommand"] = 3,
            ["MoveMotorCommand"] = 3,
            ["StopMotorCommand"] = 3,
            ["ApplyMotorConfigCommand"] = 3,
            ["ResetAllRoisCommand"] = 2,
            ["ApplyAdcRoiInputsCommand"] = 1,
            ["ResetAdcRoiCommand"] = 1,
            ["ReloadAdcRoiInputsCommand"] = 1,
            ["ApplyFocusRoiInputsCommand"] = 1,
            ["ResetFocusRoiCommand"] = 1,
            ["ReloadFocusRoiInputsCommand"] = 1
        };

    private static readonly IReadOnlyDictionary<string, int> ExpectedEventHandlerBindingCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Loaded=ChannelCalibrationSection_Loaded"] = 1,
            ["Loaded=AcquisitionRowsComboBox_Loaded"] = 1,
            ["SelectionChanged=AcquisitionRowsComboBox_SelectionChanged"] = 1,
            ["DropDownClosed=AcquisitionRowsComboBox_DropDownClosed"] = 1,
            ["PreviewKeyDown=AcquisitionRowsComboBox_PreviewKeyDown"] = 1,
            ["TextSubmitted=AcquisitionRowsComboBox_TextSubmitted"] = 1,
            ["Click=SaveAndOpenScanButton_Click"] = 1,
            ["Click=SaveCurrentConfigurationButton_Click"] = 1,
            ["Click=LoadFilmProfileDraftButton_Click"] = 1,
            ["Click=FilmProfileWorkflowNextButton_Click"] = 1,
            ["Click=FilmProfileWorkflowStepButton_Click"] = 4,
            ["Loaded=PairedField_Loaded"] = 41,
            ["TextChanged=CurrentCalibrationIlluminationLevelTextBox_TextChanged"] = 1,
            ["TextChanged=CurrentCalibrationIlluminationPulseClockTextBox_TextChanged"] = 1,
            ["SelectionChanged=CurrentCalibrationIlluminationWorkModeComboBox_SelectionChanged"] = 1,
            ["SelectionChanged=WhiteLightSourceComboBox_SelectionChanged"] = 1,
            ["Expanding=DeferredExpander_Expanding"] = 1,
            ["PointerPressed=ManualFocusNegativeButton_PointerPressed"] = 1,
            ["PointerPressed=ManualFocusPositiveButton_PointerPressed"] = 1,
            ["PointerReleased=ManualFocusButton_PointerReleased"] = 2,
            ["PointerCanceled=ManualFocusButton_PointerCanceled"] = 2,
            ["PointerCaptureLost=ManualFocusButton_PointerCaptureLost"] = 2,
            ["Click=AutoFocusButton_Click"] = 1,
            ["Click=ZoomOutButton_Click"] = 1,
            ["Click=ZoomInButton_Click"] = 1,
            ["Click=FitPreviewButton_Click"] = 1,
            ["Click=ActualSizePreviewButton_Click"] = 1,
            ["PreviewKeyDown=LineProfilePreview_KeyDown"] = 1,
            ["ItemClick=CalibrationChannelStatusListView_ItemClick"] = 1,
            ["SelectionChanged=ZoomScaleComboBox_SelectionChanged"] = 1,
            ["SelectionChanged=RawSignalModeSelector_SelectionChanged"] = 1,
            ["SelectionChanged=WorkbenchSectionSelectorBar_SelectionChanged"] = 1,
            ["Tapped=WorkbenchTaskButton_Tapped"] = 4,
            ["Click=OpenDevicePropertiesButton_Click"] = 2,
            ["Click=OpenDeviceChannelsSettings_Click"] = 2,
            ["SizeChanged=ScanDebugRootGrid_SizeChanged"] = 1,
            ["Click=PreviewToggleButton_Click"] = 1,
            ["Click=CalibrationGuideNextButton_Click"] = 1,
            ["Click=CalibrationGuideStepButton_Click"] = 6,
            ["Click=BackToEditorButton_Click"] = 1,
            ["Click=RunScannerConnectionButton_Click"] = 2,
            ["SelectionChanged=WorkbenchTaskSelectorBar_SelectionChanged"] = 1,
            ["SelectionChanged=WorkbenchTaskComboBox_SelectionChanged"] = 1,
            ["SelectionChanged=MotorAxisComboBox_SelectionChanged"] = 1,
            ["Click=OpenBlackWhiteTaskButton_Click"] = 2,
            ["Click=InspectionEditRoiButton_Click"] = 1,
            ["Click=OpenConfigurationButton_Click"] = 1,
            ["Click=ReturnToWorkbenchTaskButton_Click"] = 1,
            ["Click=CloseValidationReviewButton_Click"] = 1,
            ["Click=InspectionToggleButton_Click"] = 2,
            ["KeyDown=InspectionRail_KeyDown"] = 1,
            ["Click=BwRoiEditButton_Click"] = 1,
            ["Click=BwViewIssuesButton_Click"] = 1,
            ["Click=BwReturnToParametersButton_Click"] = 1,
            ["Click=BwIssueButton_Click"] = 1,
            ["Click=BwConfigureChannelButton_Click"] = 1,
            ["Click=BwOpenChannelDetailsButton_Click"] = 1,
            ["Click=BwManageChannelProfilesButton_Click"] = 1,
            ["Click=BwReviewCandidateButton_Click"] = 1,
            ["Click=CandidateReturnToParametersButton_Click"] = 1,
            ["Opening=ObservationCompareFlyout_Opening"] = 1,
            ["Opening=ObservationSelectionFlyout_Opening"] = 1,
            ["SelectionChanged=CalibrationChannelFallbackComboBox_SelectionChanged"] = 1,
            ["SelectionChanged=CalibrationChannelSelectorBar_SelectionChanged"] = 1,
            ["SizeChanged=BwAutoActionGrid_SizeChanged"] = 1,
            ["SizeChanged=ChannelCalibrationSection_SizeChanged"] = 1,
            ["SizeChanged=ChannelCalibrationSummary_SizeChanged"] = 3,
            ["SizeChanged=ChannelCalibrationContentWidthHost_SizeChanged"] = 1,
            ["SizeChanged=ChannelCalibrationDetailsGrid_SizeChanged"] = 1,
            ["DragDelta=PreviewSplitter_DragDelta"] = 1,
            ["KeyDown=PreviewSplitter_KeyDown"] = 1,
            ["Opening=PreviewDisplayToolsFlyout_Opening"] = 1,
            ["Opening=OverlayToolsFlyout_Opening"] = 1,
            ["Opening=WorkbenchDataExportFlyout_Opening"] = 1,
            ["SizeChanged=PreviewScrollViewer_SizeChanged"] = 1,
            ["ViewChanged=PreviewScrollViewer_ViewChanged"] = 1,
            ["PointerPressed=PreviewScrollViewer_PointerPressed"] = 1,
            ["PointerMoved=PreviewScrollViewer_PointerMoved"] = 1,
            ["PointerReleased=PreviewScrollViewer_PointerReleased"] = 1,
            ["PointerCanceled=PreviewScrollViewer_PointerCanceled"] = 1,
            ["PointerCaptureLost=PreviewScrollViewer_PointerCaptureLost"] = 1,
            ["PointerWheelChanged=PreviewScrollViewer_PointerWheelChanged"] = 1,
            ["CreateResources=PreviewCanvasControl_CreateResources"] = 1,
            ["Draw=PreviewCanvasControl_Draw"] = 1,
            ["CreateResources=RawSignalCanvasControl_CreateResources"] = 1,
            ["Draw=RawSignalCanvasControl_Draw"] = 1,
            ["SizeChanged=RawSignalCanvasControl_SizeChanged"] = 1,
            ["SizeChanged=RawSignalProfileScrollViewer_SizeChanged"] = 1,
            ["ActualThemeChanged=RawSignalCanvasControl_ActualThemeChanged"] = 1,
            ["PointerPressed=PreviewCanvasControl_PointerPressed"] = 1,
            ["PointerMoved=PreviewCanvasControl_PointerMoved"] = 1,
            ["PointerReleased=PreviewCanvasControl_PointerReleased"] = 1,
            ["PointerCanceled=PreviewCanvasControl_PointerCanceled"] = 1,
            ["PointerCaptureLost=PreviewCanvasControl_PointerCaptureLost"] = 1,
            ["PointerExited=PreviewCanvasControl_PointerExited"] = 1
        };

    private static readonly IReadOnlyDictionary<string, string> ExpectedPackageReferences =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CommunityToolkit.Mvvm"] = "8.4.2",
            ["CommunityToolkit.WinUI.Animations"] = "8.2.251219",
            ["CommunityToolkit.WinUI.Controls.Primitives"] = "8.2.251219",
            ["CommunityToolkit.WinUI.Extensions"] = "8.2.251219",
            ["Microsoft.Extensions.Hosting"] = "10.0.7",
            ["Microsoft.WindowsAppSDK"] = "2.0.1",
            ["Microsoft.Xaml.Behaviors.WinUI.Managed"] = "3.0.1",
            ["Microsoft.Graphics.Win2D"] = "1.4.0",
            ["WinUIEx"] = "2.9.0"
        };

    private static readonly (string Binding, string Method, string CanExecute, string RuntimeCommand)[] ExpectedProfileLifecycleBindings =
    [
        ("Command=\"{x:Bind ViewModel.NewFilmProfileCommand}\"", "NewFilmProfile", "CanRunFilmProfileLifecycleOperation", "NewFilmProfile"),
        ("Command=\"{x:Bind ViewModel.LoadFilmProfileJsonCommand}\"", "LoadFilmProfileJson", "CanLoadFilmProfile", "LoadFilmProfileJson"),
        ("Command=\"{x:Bind ViewModel.ValidateFilmProfileCommand}\"", "ValidateFilmProfile", "CanValidateFilmProfile", "ValidateFilmProfile"),
        ("Command=\"{x:Bind ViewModel.SaveFilmProfileJsonCommand}\"", "SaveFilmProfileJson", "CanSaveFilmProfile", "SaveFilmProfileJson"),
        ("Command=\"{x:Bind ViewModel.ApplyStagedFilmProfileImportCommand}\"", "ApplyStagedFilmProfileImport", "CanApplyStagedFilmProfileImportCommand", "ApplyStagedFilmProfileImport"),
        ("Command=\"{x:Bind ViewModel.DiscardStagedFilmProfileImportCommand}\"", "DiscardStagedFilmProfileImport", "CanDiscardStagedFilmProfileImport", "DiscardStagedFilmProfileImport")
    ];

    private static readonly string[] Todo10OperatorAcquisitionBindings =
    [
        "x:Uid=\"ScanDebug_FilmProfileWorkbenchAcquisitionSetupTitle\"",
        "x:Uid=\"ScanDebug_FilmProfileWorkbenchAcquisitionSetupDescription\"",
        "x:Uid=\"ScanDebug_RowsComboBox\"",
        "IsEnabled=\"{x:Bind ViewModel.AreScanAcquisitionSettingsEditable, Mode=OneWay}\"",
        "IsEditable=\"True\"",
        "ItemsSource=\"{x:Bind ViewModel.RowOptions, Mode=OneWay}\"",
        "Text=\"{x:Bind ViewModel.SelectedRows, Mode=OneWay}\"",
        "SelectedItem=\"{x:Bind ViewModel.SelectedRows, Mode=OneWay}\"",
        "SelectionChangedTrigger=\"Committed\"",
        "SelectionChanged=\"AcquisitionRowsComboBox_SelectionChanged\"",
        "DropDownClosed=\"AcquisitionRowsComboBox_DropDownClosed\"",
        "PreviewKeyDown=\"AcquisitionRowsComboBox_PreviewKeyDown\"",
        "Loaded=\"AcquisitionRowsComboBox_Loaded\"",
        "TextSubmitted=\"AcquisitionRowsComboBox_TextSubmitted\"",
        "x:Name=\"AcquisitionDeviceMappingButton\"",
        "x:Uid=\"ScanDebug_DeviceChannelsEntry\"",
        "Click=\"OpenDeviceChannelsSettings_Click\"",
        "x:Uid=\"Scan_MotorIntervalTextBox\"",
        "Text=\"{x:Bind ViewModel.MotorDistancePerLineValue, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
        "x:Uid=\"Scan_MotorIntervalUnitComboBox\"",
        "ItemsSource=\"{x:Bind ViewModel.MotorDistancePerLineUnitOptions, Mode=OneWay}\"",
        "SelectedItem=\"{x:Bind ViewModel.MotorDistancePerLineUnit, Mode=TwoWay}\"",
        "x:Uid=\"ScanDebug_WarmUpToggleSwitch\"",
        "IsOn=\"{x:Bind ViewModel.IsWarmUpEnabled, Mode=TwoWay}\"",
        "x:Uid=\"ScanDebug_PreviewToggleSwitch\"",
        "IsEnabled=\"{x:Bind ViewModel.IsPreviewToggleEnabled, Mode=OneWay}\"",
        "IsOn=\"{x:Bind ViewModel.IsPreviewEnabled, Mode=TwoWay}\"",
        "x:Uid=\"Scan_StartingDirectionComboBox\"",
        "ItemsSource=\"{x:Bind ViewModel.DirectionOptions, Mode=OneWay}\"",
        "SelectedItem=\"{x:Bind ViewModel.SelectedStartingDirection, Mode=TwoWay}\"",
        "Text=\"{x:Bind ViewModel.ComputedMotorSummaryText, Mode=OneWay}\"",
        "ItemsSource=\"{x:Bind ViewModel.AcquisitionChannelsInDisplayOrder, Mode=OneWay}\"",
        "AutomationProperties.Name=\"{Binding ChannelLedBindingText}\"",
        "IsChecked=\"{Binding IsSelected, Mode=TwoWay}\"",
        "IsEnabled=\"{Binding IsSelectionEditable}\"",
        "Text=\"{Binding ChannelLedBindingText}\"",
        "Text=\"{Binding CalibrationStatusText}\"",
        "x:Uid=\"ScanDebug_AcquisitionChannelOrderTitle\"",
        "Text=\"{x:Bind ViewModel.AcquisitionChannelOrderText, Mode=OneWay}\""
    ];

    private static readonly string[] Todo10DebugOnlyAcquisitionSwitchBindings =
    [
        "x:Uid=\"ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugDescription\"",
        "x:Uid=\"ScanDebug_WaterfallToggleSwitch\"",
        "IsEnabled=\"{x:Bind ViewModel.CanEditWaterfall, Mode=OneWay}\"",
        "IsOn=\"{x:Bind ViewModel.IsWaterfallEnabled, Mode=TwoWay}\"",
        "Visibility=\"{x:Bind ViewModel.WaterfallPreviewOptionsVisibility, Mode=OneWay}\""
    ];

    private static readonly string[] Todo21TransportStrategyBindings =
    [
        "AutomationProperties.AutomationId=\"AcquisitionTransportStrategyCard\"",
        "x:Uid=\"ScanDebug_AcquisitionTransportStrategyTitle\"",
        "x:Uid=\"ScanDebug_AcquisitionTransportStrategyHelpText\"",
        "x:Uid=\"Scan_AlternateDirectionToggleSwitch\"",
        "IsEnabled=\"{x:Bind ViewModel.AreScanAcquisitionSettingsEditable, Mode=OneWay}\"",
        "IsOn=\"{x:Bind ViewModel.IsAlternateMotorDirectionEnabled, Mode=TwoWay}\"",
        "x:Uid=\"ScanDebug_AcquisitionTransportStrategySafetyText\""
    ];

    private static readonly string[] Todo21CaptureModeBindings =
    [
        "x:Name=\"CaptureModeComboBox\"",
        "AutomationProperties.AutomationId=\"CaptureModeComboBox\"",
        "x:Uid=\"ScanDebug_CaptureModeComboBox\"",
        "ItemsSource=\"{x:Bind ViewModel.CaptureModeOptions, Mode=OneWay}\"",
        "ItemTemplate=\"{StaticResource ScanCaptureModeOptionTemplate}\"",
        "SelectedItem=\"{x:Bind ViewModel.SelectedCaptureMode, Mode=TwoWay}\"",
        "IsEnabled=\"{x:Bind ViewModel.CanEditCaptureMode, Mode=OneWay}\"",
        "AutomationProperties.HelpText=\"{x:Bind ViewModel.CaptureModeUnavailableReasonText, Mode=OneWay}\"",
        "AutomationProperties.FullDescription=\"{x:Bind ViewModel.CaptureModeScopeText, Mode=OneWay}\""
    ];

    [Fact]
    public void ShellNavigation_HasExistingMainAndScanDebugItems()
    {
        var shell = ReadAppSource("Views", "ShellPage.xaml");
        var code = ReadAppSource("Views", "ShellPage.xaml.cs");

        Assert.Contains("x:Uid=\"Shell_Main\" helpers:NavigationHelper.NavigateTo=\"Main\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"Shell_ScanDebug\" helpers:NavigationHelper.NavigateTo=\"ScanDebug\"", shell, StringComparison.Ordinal);
        Assert.Contains("<NavigationView.MenuItems>", shell, StringComparison.Ordinal);
        Assert.Contains("Header=\"{x:Bind ViewModel.HeaderText, Mode=OneWay}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("FilmProfileEditor", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PageContentHost\" Margin=\"{StaticResource NavigationViewPageContentMargin}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("ScanDebugShellContentMargin", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdatePageContentMargin", code, StringComparison.Ordinal);
        var page = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        Assert.Null(page.Descendants().Single(element => (string?)element.Attribute("AutomationProperties.AutomationId") == "ScanDebugRootGrid").Attribute("Background"));
        Assert.Contains("NavigationCacheMode=\"Enabled\"", ReadAppSource("Views", "ScanDebugPage.xaml"), StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugHardwareControls_ArePresent()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");

        var requiredControls = new[]
        {
            "ScanDebug_StartButton",
            "ScanDebug_StopButton",
            "ScanDebug_PreviewToggleSwitch",
            "ScanDebug_SaveChannelButton",
            "ScanDebug_ClearChannelButton",
            "ScanDebug_ApplyDeviceClockButton",
            "ScanDebug_ApplyParamsButton",
            "ScanDebug_AutoBlackButton",
            "ScanDebug_AutoWhiteButton",
            "ScanDebug_AutoCalibrateButton",
            "ScanDebug_RunAutofocusButton",
            "ScanDebug_ApplyIlluminationButton",
            "ScanDebug_Motor1MoveButton",
            "ScanDebug_Motor1StopButton",
            "ScanDebug_Motor2MoveButton",
            "ScanDebug_Motor2StopButton",
            "ScanDebug_Motor3MoveButton",
            "ScanDebug_Motor3StopButton",
            "ScanDebug_ExportDngButton",
            "PreviewScrollViewer",
            "PreviewCanvasControl"
        };

        foreach (var control in requiredControls)
            Assert.Contains(control, xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugPage_UsesWorkspaceSummaryAndLifecycleHeaderJsonActions()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");

        Assert.Contains("ScanDebug_BasicInformationValidationTitle", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.CurrentProfileNameText, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Message=\"{x:Bind ViewModel.CurrentFilmProfileValidationIssueCountText, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_ConfigurationFileSaveItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.SaveFilmProfileJsonCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_ConfigurationFileOpenItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.LoadFilmProfileJsonCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationSave_UnifiesSavingAndKeepsScanHandoffValidationExplicit()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var document = XDocument.Parse(xaml);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
        var save = Named("SaveConfigurationFileButton");

        Assert.Equal("ScanDebug_ConfigurationFileSaveItem", (string?)save.Attribute(x + "Uid"));
        Assert.Equal("MenuFlyoutItem", save.Name.LocalName);
        Assert.Equal("MenuFlyout", Named("ConfigurationFileFlyout").Name.LocalName);
        Assert.Contains(save, Named("ConfigurationFileFlyout").Descendants());
        Assert.Null(save.Attribute("TabIndex"));
        Assert.Equal("{x:Bind ViewModel.SaveFilmProfileJsonCommand}", (string?)save.Attribute("Command"));
        Assert.Null(save.Attribute("Width"));
        var saveIcon = Assert.Single(save.Descendants(), element => element.Name.LocalName == "SymbolIcon");
        Assert.Equal("Save", (string?)saveIcon.Attribute("Symbol"));
        var draft = Named("SaveFilmProfileDraftButton");
        Assert.Null(draft.Attribute("Style"));
        Assert.DoesNotContain(draft.Ancestors(), element => element.Name.LocalName == "Flyout");
        Assert.Equal("{x:Bind ViewModel.CanUseEditingDraftFiles, Mode=OneWay}", (string?)draft.Attribute("IsEnabled"));
        Assert.Equal("SaveCurrentConfigurationButton_Click", (string?)draft.Attribute("Click"));
        var engineering = Named("PropertiesEngineeringTab");
        Assert.Equal("SelectorBarItem", engineering.Name.LocalName);
        Assert.Contains(engineering, Named("WorkbenchSectionSelectorBar").Elements());
        Assert.DoesNotContain(engineering, Named("ConfigurationFileFlyout").Descendants());
        Assert.DoesNotContain(document.Descendants(), element =>
            (string?)element.Attribute("Click") == "OpenAdvancedButton_Click");
        var page = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var saveCurrent = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(
            ReadAppSource("Views", "ScanDebugPage.UiRefinement.cs"), "SaveCurrentConfigurationButton_Click");
        Assert.Contains("ViewModel.SaveFilmProfileJsonCommand.CanExecute(null)", saveCurrent, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.SaveFilmProfileJsonCommand.ExecuteAsync(null);", saveCurrent, StringComparison.Ordinal);
        Assert.Contains("else if (ViewModel.CanUseEditingDraftFiles)", saveCurrent, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.SaveEditingDraftAsync", saveCurrent, StringComparison.Ordinal);
        Assert.Contains("private static readonly int[] WorkbenchPropertySectionIndices = [0, 4, 5];", page, StringComparison.Ordinal);
        Assert.Equal("Save full configuration…", FilmProfileContractSource.ReadResources("en-us")["ScanDebug_ConfigurationFileSaveItem.Text"]);
        Assert.Equal("保存完整配置…", FilmProfileContractSource.ReadResources("zh-CN")["ScanDebug_ConfigurationFileSaveItem.Text"]);
        var saveAndScan = Named("SaveAndOpenScanButton");
        Assert.Equal("ScanDebug_SaveAndOpenScanButton", (string?)saveAndScan.Attribute(x + "Uid"));
        Assert.Equal("{StaticResource AccentButtonStyle}", (string?)saveAndScan.Attribute("Style"));
        Assert.Equal("{x:Bind ViewModel.CanSaveFilmProfileFile, Mode=OneWay}", (string?)saveAndScan.Attribute("IsEnabled"));
        Assert.Equal("SaveAndOpenScanButton_Click", (string?)saveAndScan.Attribute("Click"));
        Assert.Null(saveAndScan.Attribute("Command"));
        var handoff = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "SaveAndOpenScanButton_Click");
        Assert.Contains("if (!ViewModel.SaveFilmProfileJsonCommand.CanExecute(null))", handoff, StringComparison.Ordinal);
        Assert.Contains("ViewModel.ValidateFilmProfileCommand.Execute(null);", handoff, StringComparison.Ordinal);
        Assert.Contains("ShowFilmProfileReview();", handoff, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.EnsureSavedFilmProfileForScanAsync()", handoff, StringComparison.Ordinal);
        Assert.Contains("IsCurrentActivation(activationEpoch) && isSaved", handoff, StringComparison.Ordinal);
        Assert.Contains("ScanViewModel.UseSavedFilmProfileNavigationParameter", handoff, StringComparison.Ordinal);
        foreach (var (key, english, chinese) in new[]
        {
            ("ScanDebug_SaveFilmProfileDraftButton.Content", "Save configuration…", "保存配置…"),
            ("ScanDebug_SaveAndOpenScanButton.Content", "Save and use for scanning", "保存并用于扫描"),
            ("ScanDebug_ReviewBeforeScanAction", "Check configuration…", "检查配置…"),
            ("ScanDebug_SaveChannelButton.Content", "Save channel preset", "另存为通道预设"),
            ("ScanDebug_ApplyParamsButton.Content", "Apply current channel to device", "应用当前通道到设备"),
            ("ScanDebug_ApplyDeviceClockButton.Content", "Apply clock to device", "应用时钟到设备")
        })
        {
            Assert.Equal(english, FilmProfileContractSource.ReadResources("en-us")[key]);
            Assert.Equal(chinese, FilmProfileContractSource.ReadResources("zh-CN")[key]);
        }
    }

    [Fact]
    public void Todo7SourceContract_LifecycleHeaderOwnsProfileActionsAndSeparatedStatusSurfaces()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var header = ExtractNamedRegion(xaml, "FilmProfileLifecycleHeader", "WorkbenchRunBar");

        foreach (var uid in new[]
        {
            "ScanDebug_ConfigurationFileNewItem",
            "ScanDebug_ConfigurationFileOpenItem",
            "ScanDebug_ConfigurationFileSaveItem"
        })
        {
            Assert.Contains($"x:Uid=\"{uid}\"", header, StringComparison.Ordinal);
        }
        var validation = GetNamedElement(xaml, "WorkbenchValidationButton");
        Assert.Equal("{x:Bind ViewModel.WorkbenchParameterStatusText, Mode=OneWay}",
            (string?)validation.Attribute("AutomationProperties.Name"));
        Assert.Equal("{x:Bind ViewModel.WorkbenchParameterDetailText, Mode=OneWay}",
            (string?)validation.Attribute("AutomationProperties.HelpText"));
        Assert.Equal("{x:Bind ViewModel.ValidateFilmProfileCommand}", (string?)validation.Attribute("Command"));

        Assert.Contains("x:Name=\"FilmProfileLifecycleHeader\"", header, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.CurrentProfileNameText, Mode=OneWay}\"", header, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.ProfileSaveStateText, Mode=OneWay}\"", header, StringComparison.Ordinal);
        var inspection = xaml[xaml.IndexOf("x:Name=\"WorkbenchInspectionRail\"", StringComparison.Ordinal)..];
        Assert.Contains("Text=\"{x:Bind ViewModel.FilmProfileDeviceStatusText, Mode=OneWay}\"", inspection, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.DeviceEvidenceText, Mode=OneWay}\"", inspection, StringComparison.Ordinal);
        var feedback = GetNamedElement(xaml, "FilmProfileOperationInfoBar").ToString();
        Assert.DoesNotContain("x:Name=\"FilmProfileOperationInfoBar\"", header, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.FilmProfileOperationMessage, Mode=OneWay}\"", feedback, StringComparison.Ordinal);
        var operationMessage = XDocument.Parse(xaml).Descendants().Single(element =>
            (string?)element.Attribute("Text") == "{x:Bind ViewModel.FilmProfileOperationMessage, Mode=OneWay}");
        Assert.Equal("WrapWholeWords", (string?)operationMessage.Attribute("TextWrapping"));
        Assert.Equal("2", (string?)operationMessage.Attribute("MaxLines"));
        Assert.Equal("CharacterEllipsis", (string?)operationMessage.Attribute("TextTrimming"));
        Assert.Equal("{x:Bind ViewModel.FilmProfileOperationMessage, Mode=OneWay}", (string?)operationMessage.Attribute("ToolTipService.ToolTip"));
        Assert.DoesNotContain("Message=\"{x:Bind ViewModel.FilmProfileOperationMessage, Mode=OneWay}\"", header, StringComparison.Ordinal);
        Assert.Contains("Severity=\"{x:Bind ViewModel.FilmProfileOperationSeverity, Mode=OneWay}\"", feedback, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.FilmProfileOperationVisibility, Mode=OneWay}\"", feedback, StringComparison.Ordinal);
        Assert.Contains("IsOpen=\"{x:Bind ViewModel.FilmProfileOperationIsOpen, Mode=TwoWay}\"", feedback, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"1\"", header, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", header, StringComparison.Ordinal);

        Assert.DoesNotContain("ViewModel.StatusText", header, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.StartScanCommand", header, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.StopScanCommand", header, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.ExportDngCommand", header, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedDebugDngExportMode", header, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo5SourceContract_OperationInfoBarCloseStateIsBoundToViewModel()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var header = ExtractNamedRegion(xaml, "FilmProfileLifecycleHeader", "WorkbenchSectionSelectorBar");
        var operationInfoBar = GetOpeningTag(xaml, "FilmProfileOperationInfoBar");

        Assert.DoesNotContain("x:Name=\"FilmProfileOperationInfoBar\"", header, StringComparison.Ordinal);
        Assert.Contains("IsOpen=\"{x:Bind ViewModel.FilmProfileOperationIsOpen, Mode=TwoWay}\"", operationInfoBar, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.FilmProfileOperationVisibility, Mode=OneWay}\"", operationInfoBar, StringComparison.Ordinal);
        Assert.DoesNotContain("IsOpen=\"True\"", operationInfoBar, StringComparison.Ordinal);
        Assert.Contains("partial void OnFilmProfileOperationIsOpenChanged(bool value)", source, StringComparison.Ordinal);
        Assert.Contains("FilmProfileOperationVisibility = value ? Visibility.Visible : Visibility.Collapsed;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage01OperationStatus_OverlaysBodyWithoutResizingHeaderAndKeepsValidationInReview()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var document = XDocument.Parse(xaml);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);

        var header = Named("FilmProfileLifecycleHeader");
        var status = Named("FilmProfileOperationInfoBar");
        var details = Named("FilmProfileLifecycleDetailsScrollViewer");
        var workspace = Named("WorkbenchWorkspaceGrid");
        Assert.Same(workspace, status.Parent);
        Assert.Same(Named("WorkbenchContentSplitGrid"), details.Parent);
        Assert.DoesNotContain(status, header.Descendants());
        Assert.DoesNotContain(details, header.Descendants());
        Assert.Equal("Collapsed", (string?)details.Attribute("Visibility"));
        Assert.Null(details.Attribute("MaxHeight"));
        Assert.Equal("320", (string?)details.Attribute("Width"));
        Assert.Equal("Right", (string?)details.Attribute("HorizontalAlignment"));
        Assert.Equal("Stretch", (string?)details.Attribute("VerticalAlignment"));
        Assert.Equal((string?)Named("WorkbenchContentSplitGrid").Attribute("Grid.Row"), (string?)status.Attribute("Grid.Row"));
        Assert.Equal("0", (string?)details.Attribute("Grid.Row"));
        Assert.Equal("5", (string?)details.Attribute("Grid.Column"));
        Assert.Equal("Right", (string?)status.Attribute("HorizontalAlignment"));
        Assert.Equal("Top", (string?)status.Attribute("VerticalAlignment"));
        Assert.Equal("420", (string?)status.Attribute("MaxWidth"));
        Assert.True((int?)status.Attribute("Canvas.ZIndex") > (int?)details.Attribute("Canvas.ZIndex"));
        Assert.Equal("2", (string?)Named("WorkbenchContentSplitGrid").Attribute("Grid.Row"));
        Assert.Contains(Named("FilmProfileWorkflowGuide"), Named("WorkbenchProfileSummaryGrid").Descendants());
        Assert.DoesNotContain(Named("FilmProfileWorkflowGuide"), Named("ConfigurationFileFlyout").Descendants());
        Assert.Contains(Named("FilmProfileWorkflowFlyout"), Named("FilmProfileWorkflowGuide").Descendants());
        Assert.Equal("0", (string?)Named("WorkbenchWorkspaceHeader").Attribute("Grid.Row"));
        var workRows = workspace.Element(workspace.Name.Namespace + "Grid.RowDefinitions")!.Elements().ToArray();
        Assert.Equal(3, workRows.Length);
        Assert.Equal("Auto", (string?)workRows[0].Attribute("Height"));
        Assert.Equal("Auto", (string?)workRows[1].Attribute("Height"));
        Assert.Equal("*", (string?)workRows[2].Attribute("Height"));
        Assert.Same(workspace, Named("WorkbenchRunBar").Parent);
        Assert.Equal("1", (string?)Named("WorkbenchRunBar").Attribute("Grid.Row"));
        Assert.Same(status, workspace.Elements().Last());
        Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == "FilmProfileOperationInfoBar");
        Assert.Equal(1, CountOccurrences(xaml, "Text=\"{x:Bind ViewModel.FilmProfileOperationMessage, Mode=OneWay}\""));
        var message = Assert.Single(status.Elements(), element => element.Name.LocalName == "TextBlock");
        Assert.Equal("{x:Bind ViewModel.FilmProfileOperationMessage, Mode=OneWay}", (string?)message.Attribute("Text"));
        Assert.Equal("WrapWholeWords", (string?)message.Attribute("TextWrapping"));
        Assert.Equal("2", (string?)message.Attribute("MaxLines"));
        Assert.Equal("CharacterEllipsis", (string?)message.Attribute("TextTrimming"));
        Assert.Equal("{x:Bind ViewModel.FilmProfileOperationMessage, Mode=OneWay}", (string?)message.Attribute("ToolTipService.ToolTip"));
        Assert.Equal("88", (string?)status.Attribute("MaxHeight"));
        Assert.Null(header.Attribute("MaxHeight"));
        Assert.Equal("*", (string?)status.Parent!.Element(status.Name.Namespace + "Grid.RowDefinitions")!
            .Elements().ElementAt(2).Attribute("Height"));

        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var save = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "SaveFilmProfileJson");
        var validate = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "ValidateFilmProfile");
        Assert.Contains("ScanDebug_Runtime_StatusSaveFilmProfileFailed", save, StringComparison.Ordinal);
        Assert.Contains("FilmProfileValidationReviewRequested?.Invoke(this, EventArgs.Empty);", validate, StringComparison.Ordinal);
        Assert.Contains("InfoBarSeverity.Error", save, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishFilmProfileOperation", validate, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo7SourceContract_StagedImportReviewIsExplicitAndValidationDoesNotReuseCurrentSummary()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var review = GetNamedElement(xaml, "StagedFilmProfileImportReview").ToString();

        Assert.Contains("x:Name=\"StagedFilmProfileImportReview\"", review, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.FilmProfileImportResultReviewVisibility, Mode=OneWay}\"", review, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchStagedImportTitle\"", review, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchStagedImportHelpText\"", review, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.StagedFilmProfileImportDisplayNameText, Mode=OneWay}\"", review, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.StagedFilmProfileImportChannelCountText, Mode=OneWay}\"", review, StringComparison.Ordinal);
        Assert.Contains("Message=\"{x:Bind ViewModel.StagedFilmProfileImportValidationIssueCountText, Mode=OneWay}\"", review, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"StagedFilmProfileImportValidationIssues\"", review, StringComparison.Ordinal);
        var document = XDocument.Parse(xaml);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var stagedReview = Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == "StagedFilmProfileImportReview");
        Assert.DoesNotContain(stagedReview.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Contains("Text=\"{x:Bind ViewModel.StagedFilmProfileDirtyReplacementWarningText, Mode=OneWay}\"", review, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.DiscardStagedFilmProfileImportCommand}\"", review, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.ApplyStagedFilmProfileImportCommand}\"", review, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource AccentButtonStyle}\"", review, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"WrapWholeWords\"", review, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"560\"", review, StringComparison.Ordinal);
        Assert.DoesNotContain("FilmProfileValidationSummary", review, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentFilmProfileValidationSummary", review, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo4SourceContract_ImportErrorReviewRemainsActionableWithoutStagedDraft()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var review = GetNamedElement(xaml, "StagedFilmProfileImportReview").ToString();

        Assert.Contains("FilmProfileImportResultReviewVisibility", review, StringComparison.Ordinal);
        Assert.Contains("StagedFilmProfileImportDisplayNameText", review, StringComparison.Ordinal);
        Assert.Contains("StagedFilmProfileImportValidationIssueCountText", review, StringComparison.Ordinal);
        Assert.Contains("StagedFilmProfileImportValidationIssueDisplays", review, StringComparison.Ordinal);
        Assert.Contains("DiscardStagedFilmProfileImportCommand", review, StringComparison.Ordinal);
        Assert.Contains("LoadFilmProfileJsonCommand", review, StringComparison.Ordinal);
        Assert.DoesNotContain("Visibility=", GetOpeningTag(xaml, "WorkbenchContentSplitGrid"), StringComparison.Ordinal);
        var stagedReview = GetNamedElement(xaml, "StagedFilmProfileImportReview");
        Assert.DoesNotContain(stagedReview.Ancestors(), element =>
            (string?)element.Attribute("Visibility") == "{x:Bind ViewModel.FilmProfileWorkbenchContentVisibility, Mode=OneWay}");
        Assert.Equal("{x:Bind ViewModel.FilmProfileWorkbenchContentVisibility, Mode=OneWay}",
            (string?)GetNamedElement(xaml, "CurrentFilmProfileValidationInspector").Attribute("Visibility"));
        Assert.Contains("HasPendingFilmProfileImportResult", source, StringComparison.Ordinal);
        Assert.Contains("public Visibility FilmProfileWorkbenchContentVisibility => HasPendingFilmProfileImportResult", source, StringComparison.Ordinal);
        Assert.Contains("OnPropertyChanged(nameof(FilmProfileWorkbenchContentVisibility));", source, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_FilmProfileWorkbenchImportError", source, StringComparison.Ordinal);
        Assert.Contains("CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.DiscardStagedFilmProfileImport)", source, StringComparison.Ordinal);
        Assert.Contains("HasPendingFilmProfileImportResult", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FilmProfileEditorSurface_IsNotReintroduced()
    {
        var appRoute = ReadAppSource("Contracts", "Navigation", "AppRoute.cs");
        var pageService = ReadAppSource("Services", "PageService.cs");
        var app = ReadAppSource("App.xaml.cs");
        var scanDebug = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var shell = ReadAppSource("Views", "ShellPage.xaml");
        var scanDebugPage = ReadAppSource("Views", "ScanDebugPage.xaml");

        Assert.DoesNotContain("FilmProfileEditor", appRoute, StringComparison.Ordinal);
        Assert.DoesNotContain("FilmProfileEditorPage", pageService + app, StringComparison.Ordinal);
        Assert.DoesNotContain("FilmProfileEditorViewModel", pageService + app, StringComparison.Ordinal);
        Assert.DoesNotContain("AppRoute.FilmProfileEditor", scanDebug, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenFilmProfileEditor", scanDebug + scanDebugPage + shell, StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureCurrentFilmProfileAndOpenEditor", scanDebug + scanDebugPage, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "Views", "FilmProfileEditorPage.xaml")));
        Assert.False(File.Exists(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "Views", "FilmProfileEditorPage.xaml.cs")));
        Assert.False(File.Exists(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", "FilmProfileEditorViewModel.cs")));
        Assert.False(File.Exists(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", "FilmProfileEditorModels.cs")));
    }

    [Fact]
    public void Stage01SourceContract_RunBarOwnsScanWhileLiveCalibrationKeepsDistinctDngExport()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var acquisition = ExtractNamedRegion(xaml, "AcquisitionPlanSection", "ChannelCalibrationSection");
        var live = ExtractNamedRegion(xaml, "LiveCalibrationSection", "DeviceSettingsSection");
        var run = GetNamedElement(xaml, "WorkbenchRunBar").ToString();
        var header = ExtractNamedRegion(xaml, "FilmProfileLifecycleHeader", "WorkbenchRunBar");
        var preview = ExtractNamedRegion(xaml, "WorkbenchPreviewColumnContent", "PreviewCanvasControl");

        Assert.Contains("x:Uid=\"ScanDebug_WorkbenchDataExportButton\"", preview, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchDataExportScrollViewer\"", preview, StringComparison.Ordinal);
        Assert.Contains("ConfigureLocalFlyoutBounds(WorkbenchDataExportFlyout, WorkbenchDataExportScrollViewer, WorkbenchDataExportContent);", ReadAppSource("Views", "ScanDebugPage.xaml.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.StatusText", run, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.StartScanCommand}\"", run, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.StopScanCommand}\"", run, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.StopAllMotorsCommand}\"", run, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{x:Bind ViewModel.SelectedDebugDngExportMode, Mode=TwoWay}\"", preview, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.ExportDngCommand}\"", preview, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.ScanReadProgressVisibility, Mode=OneWay}\"", run, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.DngAlignmentWarningVisibility, Mode=OneWay}\"", preview, StringComparison.Ordinal);
        foreach (var binding in Todo21CaptureModeBindings)
            Assert.Contains(binding, run, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"180\"", preview, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"320\"", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("StartScanCommand", header, StringComparison.Ordinal);
        Assert.DoesNotContain("StartScanCommand", live, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportDngCommand", live, StringComparison.Ordinal);
        var engineering = ExtractNamedRegion(xaml, "EngineeringToolsSection", "MotionTaskSection");
        Assert.Contains("AutomationProperties.AutomationId=\"SessionIlluminationTestCard\"", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("SessionIlluminationTestCard", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("SessionIlluminationTestCard", live, StringComparison.Ordinal);
        Assert.DoesNotContain("StopAllMotorsCommand", header, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedDebugDngExportMode", header, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage01RunConnectionButton_OpensShellFlyoutWithoutConnectingOrNavigatingDirectly()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var document = XDocument.Parse(xaml);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var run = Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == "WorkbenchRunBar");
        var button = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "Button" && (string?)element.Attribute(x + "Uid") == "ScanDebug_RunScannerConnectionButton");

        Assert.Contains(button, run.Descendants());
        Assert.Equal("RunScannerConnectionButton", (string?)button.Attribute("AutomationProperties.AutomationId"));
        Assert.Equal("RunScannerConnectionButton_Click", (string?)button.Attribute("Click"));
        Assert.Equal(2, CountOccurrences(xaml, "Click=\"RunScannerConnectionButton_Click\""));
        Assert.Null(button.Attribute("Command"));
        var emptyState = GetNamedElement(xaml, "PreviewEmptyStateGrid");
        var emptyStateConnection = Assert.Single(emptyState.Descendants(), element =>
            (string?)element.Attribute(x + "Uid") == "ScanDebug_OfflineConnectionAction");
        Assert.Equal("RunScannerConnectionButton_Click", (string?)emptyStateConnection.Attribute("Click"));
        Assert.Equal("{x:Bind ViewModel.WorkbenchOfflineGuidanceVisibility, Mode=OneWay}",
            (string?)emptyStateConnection.Attribute("Visibility"));
        Assert.Null(emptyStateConnection.Attribute("Command"));

        var page = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var click = Regex.Match(page, @"private void RunScannerConnectionButton_Click\(object sender, RoutedEventArgs e\)\s*=>\s*[^;]+;").Value;
        Assert.NotEmpty(click);
        Assert.Contains("OpenScannerConnectionFlyout(sender as FrameworkElement);", click, StringComparison.Ordinal);
        var open = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "OpenScannerConnectionFlyout");
        Assert.Contains("if (App.MainWindow.Content is ShellPage shellPage)", open, StringComparison.Ordinal);
        Assert.Contains("shellPage.ShowScannerConnectionFlyout(origin);", open, StringComparison.Ordinal);
        var connectionPath = click + open;
        Assert.DoesNotContain("ConnectDevicesCommand", connectionPath, StringComparison.Ordinal);
        Assert.DoesNotContain("DeviceConfiguration", connectionPath, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateTo(", connectionPath, StringComparison.Ordinal);

        var shell = XDocument.Parse(ReadAppSource("Views", "ShellPage.xaml"));
        var shellItem = Assert.Single(shell.Descendants(), element => (string?)element.Attribute(x + "Name") == "ScannerConnectionItem");
        Assert.Equal("ScannerConnectionItem_Tapped", (string?)shellItem.Attribute("Tapped"));
        Assert.Equal("ScannerConnectionItem_KeyDown", (string?)shellItem.Attribute("KeyDown"));
        var flyout = Assert.Single(shellItem.Descendants(), element =>
            element.Name.LocalName == "Flyout" && (string?)element.Attribute(x + "Name") == "ScannerConnectionFlyout");
        Assert.Equal("FlyoutBase.AttachedFlyout", flyout.Parent?.Name.LocalName);
        Assert.Contains(flyout.Descendants(), element => (string?)element.Attribute("Command") == "{x:Bind ViewModel.ConnectScannerCommand}");

        var shellCode = ReadAppSource("Views", "ShellPage.xaml.cs");
        Assert.Contains("ShowScannerConnectionFlyout();", ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(shellCode, "ScannerConnectionItem_Tapped"), StringComparison.Ordinal);
        var keyDown = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(shellCode, "ScannerConnectionItem_KeyDown");
        Assert.Contains("e.Key is VirtualKey.Enter or VirtualKey.Space", keyDown, StringComparison.Ordinal);
        Assert.Contains("ShowScannerConnectionFlyout();", keyDown, StringComparison.Ordinal);
        Assert.Matches(@"internal void ShowScannerConnectionFlyout\(FrameworkElement\? anchor = null\)\s*=>\s*ScannerConnectionFlyout\.ShowAt\(anchor \?\? ScannerConnectionItem\);", shellCode);
    }

    [Fact]
    public void Stage01SourceContract_FixedRowsOwnRunControlsAndSinglePreviewWithIndependentInspection()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var document = XDocument.Parse(xaml);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);

        var root = Named("ScanDebugRootGrid");
        var header = Named("FilmProfileLifecycleHeader");
        var run = Named("WorkbenchRunBar");
        var work = Named("WorkbenchContentSplitGrid");
        var preview = Named("WorkbenchPreviewColumnContent");
        var inspection = Named("WorkbenchInspectionRail");
        var workspace = Named("WorkbenchWorkspaceGrid");
        var footer = Assert.Single(root.Elements(), element => element.Name.LocalName == "Border" && (string?)element.Attribute("Grid.Row") == "2");

        Assert.Equal(new[] { "Auto", "*", "Auto" }, root.Elements().Single(element => element.Name.LocalName == "Grid.RowDefinitions")
            .Elements().Select(element => (string?)element.Attribute("Height")));
        Assert.Same(root, header.Parent);
        Assert.Same(root, workspace.Parent);
        Assert.Same(workspace, run.Parent);
        Assert.Same(workspace, work.Parent);
        Assert.Equal("1", (string?)run.Attribute("Grid.Row"));
        Assert.DoesNotContain(run.Ancestors(), ancestor => ancestor.Name.LocalName == "ScrollViewer");
        Assert.Equal("{x:Bind ViewModel.ObservationFooterVisibility, Mode=OneWay}", (string?)footer.Attribute("Visibility"));
        var observation = Assert.Single(footer.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.ObservationFooterText, Mode=OneWay}");
        Assert.Equal("{x:Bind ViewModel.ObservationFooterText, Mode=OneWay}", (string?)observation.Attribute("Text"));
        Assert.Equal("{x:Bind ViewModel.ObservationFooterText, Mode=OneWay}", (string?)observation.Attribute("ToolTipService.ToolTip"));
        Assert.DoesNotContain(footer.Descendants(), element => ((string?)element.Attribute("Text"))?.Contains("ViewModel.StatusText", StringComparison.Ordinal) == true);
        var runStatus = Named("RunStatusPanel");
        Assert.Contains(runStatus, run.Descendants());
        var runPresentation = Assert.Single(runStatus.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.WorkbenchDeviceStatusText, Mode=OneWay}");
        Assert.Equal("Polite", (string?)runPresentation.Attribute("AutomationProperties.LiveSetting"));
        Assert.Single(runStatus.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.RunContextText, Mode=OneWay}");
        var notices = Assert.Single(runStatus.Descendants(), element => (string?)element.Attribute("ItemsSource") == "{x:Bind ViewModel.OperationNotices, Mode=OneWay}");
        var acknowledge = Assert.Single(notices.Descendants(), element => (string?)element.Attribute("Command") == "{Binding DataContext.AcknowledgeOperationNoticeCommand, ElementName=ScanDebugRootGrid}");
        Assert.Equal("{Binding Id}", (string?)acknowledge.Attribute("CommandParameter"));
        Assert.Contains(notices.Descendants(), element => (string?)element.Attribute("Text") == "{Binding Message}");
        var diagnostic = Assert.Single(runStatus.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.CurrentDiagnosticText, Mode=OneWay}");
        Assert.Equal("{x:Bind OperationLatestMessageLabel}", (string?)diagnostic.Attribute("AutomationProperties.LabeledBy"));
        Assert.Equal("2", (string?)work.Attribute("Grid.Row"));
        Assert.Equal("4", (string?)inspection.Attribute("Grid.Column"));
        foreach (var (section, scrollOwner) in new[]
        {
            ("AcquisitionPlanSection", "AcquisitionPlanScrollViewer"),
            ("ChannelCalibrationSection", "ChannelCalibrationScrollViewer"),
            ("LiveCalibrationSection", "LiveCalibrationScrollViewer"),
            ("MotionTaskSection", "MotionScrollViewer")
        })
        {
            Assert.Contains(Named(scrollOwner), Named(section).Descendants());
            Assert.Contains(Named(section), Named("WorkbenchEditorColumnContent").Descendants());
        }
        Assert.Contains(Named("EngineeringToolsScrollViewer"), Named("EngineeringToolsSection").Descendants());
        Assert.DoesNotContain(Named("EngineeringToolsScrollViewer"), Named("MotionTaskSection").Descendants());

        foreach (var uid in new[] { "ScanDebug_CaptureModeComboBox", "ScanDebug_StartButton", "ScanDebug_StopButton", "ScanDebug_StopAllMotorsButton" })
        {
            var control = Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Uid") == uid);
            Assert.Contains(control, run.Descendants());
            Assert.DoesNotContain(control, header.Descendants());
        }

        var imageViewport = Named("PreviewScrollViewer");
        var rawViewport = Named("RawSignalProfileScrollViewer");
        var previewInteraction = Named("WorkbenchPreviewInteractionHost");
        Assert.Equal("ContentControl", previewInteraction.Name.LocalName);
        Assert.Equal("False", (string?)previewInteraction.Attribute("IsTabStop"));
        Assert.Same(preview, previewInteraction.Parent);
        Assert.Contains(imageViewport, previewInteraction.Descendants());
        Assert.Contains(rawViewport, previewInteraction.Descendants());
        Assert.Contains(imageViewport, preview.Descendants());
        Assert.Contains(rawViewport, preview.Descendants());
        Assert.Single(imageViewport.Descendants(), element => element.Name.LocalName == "CanvasControl");
        Assert.Single(rawViewport.Descendants(), element => element.Name.LocalName == "CanvasControl");
        Assert.Equal(2, document.Descendants().Count(element => element.Name.LocalName == "CanvasControl"));
        Assert.Contains(Named("PreviewCanvasControl"), imageViewport.Descendants());
        Assert.Contains(Named("RawSignalCanvasControl"), rawViewport.Descendants());
        Assert.DoesNotContain(inspection.Descendants(), element => element.Name.LocalName is "TextBox" or "CanvasControl");
        Assert.Contains(inspection.Descendants(), element => ((string?)element.Attribute("Text"))?.Contains("ViewModel.ColumnSampleStatusText", StringComparison.Ordinal) == true);
        Assert.Contains(inspection.Descendants(), element => ((string?)element.Attribute("Text"))?.Contains("ViewModel.RoiStatusText", StringComparison.Ordinal) == true);
        var roiEntry = Assert.Single(inspection.Descendants(), element => (string?)element.Attribute(x + "Uid") == "ScanDebug_WorkbenchInspectionEditButton");
        Assert.Equal("InspectionEditRoiButton_Click", (string?)roiEntry.Attribute("Click"));
    }

    [Fact]
    public void Stage01V_PreviewHeaderKeepsImageToolsBesideCanvasAndSecondaryToolsInAuxiliaryMenu()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
        var workspace = Named("WorkbenchWorkspaceHeader");
        var toolbar = Named("WorkspacePreviewToolbar");
        var preview = Named("WorkbenchPreviewColumnContent");
        var display = Named("PreviewDisplayToolsButton");
        var auxiliary = Named("PreviewAuxiliaryToolsButton");
        var code = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var compose = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "ComposeWorkspaceHeader");
        var layout = code[code.IndexOf("private void UpdateWorkbenchPreviewLayout(double availableWidth)", StringComparison.Ordinal)
            ..code.IndexOf("private Control? FindWorkbenchFocusReplacement(", StringComparison.Ordinal)];

        Assert.Same(Named("WorkbenchContentSplitGrid").Parent, workspace.Parent);
        Assert.Equal("0", (string?)workspace.Attribute("Grid.Row"));
        Assert.Contains(Named("WorkbenchTaskSelectorBar"), workspace.Descendants());
        Assert.Contains(Named("WorkbenchTaskComboBox"), workspace.Descendants());
        Assert.Contains(Named("InspectionToggleButton"), toolbar.Descendants());
        Assert.Contains(Named("ReturnToWorkbenchTaskButton"), Named("WorkbenchAuxiliaryHeader").Descendants());
        Assert.Contains(Named("RawSignalModeSelector"), preview.Descendants());
        Assert.Contains(Named("PreviewToolbar"), preview.Descendants());
        Assert.Equal("Visible", (string?)Named("PreviewHeaderGrid").Attribute("Visibility"));
        Assert.Same(Named("PreviewHeaderGrid"), Named("RawSignalModeSelector").Parent);
        Assert.Same(Named("PreviewHeaderGrid"), Named("PreviewToolbar").Parent);
        Assert.Contains(Named("FitPreviewButton"), Named("PreviewToolbar").Descendants());
        Assert.Contains(Named("ActualSizePreviewButton"), Named("PreviewToolbar").Descendants());
        Assert.Equal("ToggleButton", Named("FitPreviewButton").Name.LocalName);
        Assert.Contains(display, auxiliary.Descendants());
        Assert.Contains(Named("PreviewDisplayToolsFlyout"), display.Descendants());
        Assert.Contains(Named("OverlayToolsButton"), auxiliary.Descendants());
        Assert.Contains(Named("WorkbenchDataExportFlyout"), auxiliary.Descendants());
        Assert.Contains("CaptureModeComboBox.Header = null;", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewHeaderGrid.Children.Remove(RawSignalModeSelector);", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewHeaderGrid.Children.Remove(PreviewToolbar);", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("auxiliaryTools.Children.Remove(PreviewDisplayToolsButton);", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewToolbar.Children.Insert(PreviewToolbar.Children.IndexOf(PreviewAuxiliaryToolsButton), PreviewDisplayToolsButton);", compose, StringComparison.Ordinal);
        Assert.Contains(Named("ObservationCompareButton"), auxiliary.Descendants());
        Assert.DoesNotContain("WorkspacePreviewToolbar.Children.Insert(0, RawSignalModeSelector);", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkspacePreviewToolbar.Children.Insert(1, PreviewToolbar);", compose, StringComparison.Ordinal);
        Assert.Contains("PreviewToolbar.Children.Insert(PreviewToolbar.Children.IndexOf(ZoomScaleComboBox), ZoomOutButton);", compose, StringComparison.Ordinal);
        Assert.Contains("PreviewToolbar.Children.Insert(PreviewToolbar.Children.IndexOf(ZoomScaleComboBox) + 1, ZoomInButton);", compose, StringComparison.Ordinal);
        Assert.True(code.IndexOf("InitializeComponent();", StringComparison.Ordinal) < code.IndexOf("ComposeWorkspaceHeader();", StringComparison.Ordinal));
        Assert.True(code.IndexOf("ComposeWorkspaceHeader();", StringComparison.Ordinal) < code.IndexOf("SetActiveWorkbenchSection(_activeWorkbenchSectionIndex);", StringComparison.Ordinal));
        Assert.Equal("0", (string?)toolbar.Attribute("Grid.Row"));
        Assert.Equal("2", (string?)toolbar.Attribute("Grid.Column"));
        Assert.Equal("Right", (string?)toolbar.Attribute("HorizontalAlignment"));
        Assert.Same(Named("PreviewToggleButton"), toolbar.Elements().Last());
        Assert.DoesNotContain("Grid.SetRow(WorkspacePreviewToolbar", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.SetColumnSpan(WorkspacePreviewToolbar", layout, StringComparison.Ordinal);
        Assert.Contains("var taskNavigationWidth = Math.Max(0, availableWidth - toolbarWidth - spacing * 2);", layout, StringComparison.Ordinal);
        Assert.Contains("var isTaskSelectorBarAvailable = !_isAuxiliaryPanelOpen && taskNavigationWidth >= taskSelectorWidth;", layout, StringComparison.Ordinal);
        Assert.Contains("WorkspacePreviewToolbar.Visibility = ToVisibility(!isPropertiesPanelOpen);", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage01SourceContract_ProfileIdentityStaysBelowTitleWhileRunReasonsAndReviewStayAvailable()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var page = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var ownerStart = page.IndexOf("private void UpdateWorkbenchPreviewLayout(double availableWidth)", StringComparison.Ordinal);
        Assert.True(ownerStart >= 0);
        var ownerEnd = page.IndexOf("private bool HasValidPreviewFrame()", ownerStart, StringComparison.Ordinal);
        Assert.True(ownerEnd > ownerStart);
        var layout = page[ownerStart..ownerEnd];
        var header = ExtractNamedRegion(xaml, "FilmProfileLifecycleHeader", "WorkbenchRunBar");
        var run = GetNamedElement(xaml, "WorkbenchRunBar").ToString();

        Assert.Contains("availableWidth < MeasureNaturalWidth(WorkbenchTitleTextBlock)", layout, StringComparison.Ordinal);
        Assert.Contains("+ MeasureNaturalWidth(FilmProfileLifecycleActionsPanel) + spacing", layout, StringComparison.Ordinal);
        Assert.Contains("WorkbenchIdentityGrid.MaxWidth = wrapIdentity ? availableWidth", layout, StringComparison.Ordinal);
        Assert.Contains("Math.Max(0, availableWidth - MeasureNaturalWidth(FilmProfileLifecycleActionsPanel) - spacing)", layout, StringComparison.Ordinal);
        Assert.Contains("ScanDebugRootGrid.RowSpacing = (double)Resources[\"ScanDebugInlineSpacing\"]", layout, StringComparison.Ordinal);
        Assert.Contains("Resources[\"ScanDebugInlineSpacing\"]", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(FilmProfileLifecycleActionsPanel, wrapIdentity ? 1 : 0);", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(FilmProfileLifecycleActionsPanel, wrapIdentity ? 0 : 1);", layout, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"1\"", GetOpeningTag(xaml, "WorkbenchProfileSummaryGrid"), StringComparison.Ordinal);
        Assert.Contains("Grid.ColumnSpan=\"2\"", GetOpeningTag(xaml, "WorkbenchProfileSummaryGrid"), StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(WorkbenchProfileSummaryGrid, wrapIdentity ? 2 : 1);", layout, StringComparison.Ordinal);
        var identity = GetNamedElement(xaml, "WorkbenchIdentityGrid");
        var summary = Assert.Single(identity.Parent!.Elements(), element =>
            (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "WorkbenchProfileSummaryGrid");
        Assert.Same(identity.Parent, summary.Parent);
        Assert.DoesNotContain(summary, identity.Descendants());
        Assert.Equal(["Auto", "Auto", "Auto"], identity.Parent!.Elements()
            .Single(element => element.Name.LocalName == "Grid.RowDefinitions").Elements()
            .Select(element => (string?)element.Attribute("Height")));
        Assert.DoesNotContain("MaxHeight=\"{StaticResource ScanDebugHeaderMaximumHeight}\"", GetOpeningTag(xaml, "FilmProfileLifecycleHeader"), StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_WorkstationTitle\"", header, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.CurrentProfileNameText, Mode=OneWay}\"", header, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.ProfileSaveStateText, Mode=OneWay}\"", header, StringComparison.Ordinal);
        var validationDetails = GetNamedElement(xaml, "CurrentFilmProfileValidationInspector");
        var parameterScope = Assert.Single(validationDetails.Descendants(), element =>
            (string?)element.Attribute("Text") == "{x:Bind ViewModel.WorkbenchParameterDetailText, Mode=OneWay}");
        Assert.Null(parameterScope.Attribute("MaxLines"));
        Assert.Null(parameterScope.Attribute("TextTrimming"));
        Assert.Equal("{x:Bind ViewModel.WorkbenchParameterDetailText, Mode=OneWay}",
            (string?)GetNamedElement(xaml, "WorkbenchValidationButton").Attribute("AutomationProperties.HelpText"));
        Assert.DoesNotContain("FilmProfileSaveHintText", summary.ToString(), StringComparison.Ordinal);
        Assert.Equal("{x:Bind ViewModel.FilmProfileSaveHintText, Mode=OneWay}",
            (string?)GetNamedElement(xaml, "SaveAndOpenScanButton").Attribute("ToolTipService.ToolTip"));
        Assert.DoesNotContain("x:Name=\"FilmProfileLifecycleDetailsScrollViewer\"", header, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"Collapsed\"", GetOpeningTag(xaml, "FilmProfileLifecycleDetailsScrollViewer"), StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.WorkbenchDeviceStatusText, Mode=OneWay}\"", run, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{x:Bind ViewModel.StartDisabledReasonText, Mode=OneWay}\"", run, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{x:Bind ViewModel.CaptureModeUnavailableReasonText, Mode=OneWay}\"", run, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{x:Bind ViewModel.StopDisabledReasonText, Mode=OneWay}\"", run, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.StopScanCommand}\"", run, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.StopAllMotorsCommand}\"", run, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage01StopReason_IsAccessibleOnFixedStopActionAndUsesExistingCanExecuteGate()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var run = Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == "WorkbenchRunBar");
        var stop = Assert.Single(run.Descendants(), element => (string?)element.Attribute(x + "Uid") == "ScanDebug_StopButton");
        Assert.DoesNotContain(stop.Ancestors(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Equal("{x:Bind ViewModel.StopScanCommand}", (string?)stop.Attribute("Command"));
        Assert.Equal("{x:Bind ViewModel.StopDisabledReasonText, Mode=OneWay}", (string?)stop.Attribute("AutomationProperties.HelpText"));
        Assert.Equal("{x:Bind ViewModel.StopDisabledReasonText, Mode=OneWay}", (string?)stop.Attribute("ToolTipService.ToolTip"));
        Assert.DoesNotContain(run.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.StopDisabledReasonText, Mode=OneWay}");

        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var projection = source[source.IndexOf("public string StopDisabledReasonText =>", StringComparison.Ordinal)
            ..source.IndexOf("public string ExportDngDisabledReasonText =>", StringComparison.Ordinal)];
        var notification = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "NotifyRuntimeOperationAvailabilityChanged");
        Assert.Contains("CanStopScan()", projection, StringComparison.Ordinal);
        Assert.Contains("BuildRuntimeCommandDisabledReason(ScanDebugRuntimeCommandKind.StopScan)", projection, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_DisabledReasonNoActiveScan", projection, StringComparison.Ordinal);
        Assert.Contains("private bool CanStopScan() => IsRunning && CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.StopScan);", source, StringComparison.Ordinal);
        Assert.True(notification.IndexOf("OnPropertyChanged(nameof(StopDisabledReasonText));", StringComparison.Ordinal)
            < notification.IndexOf("StopScanCommand.NotifyCanExecuteChanged();", StringComparison.Ordinal));
    }

    [Fact]
    public void Stage01StopButtons_UseCriticalThemeBrushWithNativeFocusAndDisabledState()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var run = Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == "WorkbenchRunBar");

        foreach (var (uid, command) in new[]
        {
            ("ScanDebug_StopButton", "StopScanCommand"),
            ("ScanDebug_StopAllMotorsButton", "StopAllMotorsCommand")
        })
        {
            var button = Assert.Single(document.Descendants(), element =>
                element.Name.LocalName == "Button" && (string?)element.Attribute(x + "Uid") == uid);

            Assert.Contains(button, run.Descendants());
            Assert.Equal($"{{x:Bind ViewModel.{command}}}", (string?)button.Attribute("Command"));
            Assert.Equal("{ThemeResource SystemFillColorCriticalBrush}", (string?)button.Attribute("BorderBrush"));
            Assert.Equal("{ThemeResource SystemFillColorCriticalBrush}", (string?)button.Attribute("Foreground"));
            Assert.Null(button.Attribute("Style"));
            Assert.Null(button.Attribute("IsEnabled"));
        }
    }

    [Fact]
    public void Stage01ZoomButtons_KeepSymbolContentAndSingleHandlersWithAccessibleUids()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var document = XDocument.Parse(xaml);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var preview = Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == "WorkbenchPreviewColumnContent");

        foreach (var (uid, automationId, content, handler) in new[]
        {
            ("ScanDebug_ZoomOutButton", "ZoomOutButton", "-", "ZoomOutButton_Click"),
            ("ScanDebug_ZoomInButton", "ZoomInButton", "+", "ZoomInButton_Click")
        })
        {
            var button = Assert.Single(document.Descendants(), element =>
                element.Name.LocalName == "Button" && (string?)element.Attribute(x + "Uid") == uid);

            Assert.Contains(button, preview.Descendants());
            Assert.Equal(automationId, (string?)button.Attribute("AutomationProperties.AutomationId"));
            Assert.Equal(content, (string?)button.Attribute("Content"));
            Assert.Equal(handler, (string?)button.Attribute("Click"));
            Assert.Equal(1, CountOccurrences(xaml, $"Click=\"{handler}\""));
        }
    }

    [Fact]
    public void Stage01ResponsiveFocus_CapturesBeforeHidingAndTargetsVisibleSelectorOrPaneEntry()
    {
        var page = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var responsiveLayout = page[page.IndexOf("private void UpdateWorkbenchPreviewLayout(double availableWidth)", StringComparison.Ordinal)
            ..page.IndexOf("private Control? FindWorkbenchFocusReplacement(", StringComparison.Ordinal)];
        var handoff = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "FindWorkbenchFocusReplacement");
        var containment = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "IsFocusedWithin");

        Assert.Contains("FocusManager.GetFocusedElement(root)", responsiveLayout, StringComparison.Ordinal);
        Assert.True(responsiveLayout.IndexOf("FindWorkbenchFocusReplacement(focusedElement", StringComparison.Ordinal)
            < responsiveLayout.IndexOf("WorkbenchSectionSelectorBar.Visibility =", StringComparison.Ordinal));
        Assert.True(responsiveLayout.IndexOf("ManualFocusNegativeButton.ReleasePointerCaptures();", StringComparison.Ordinal)
            < responsiveLayout.IndexOf("WorkbenchEditorColumnContent.Visibility = ToVisibility", StringComparison.Ordinal));
        Assert.True(responsiveLayout.IndexOf("WorkbenchInspectionRail.Visibility =", StringComparison.Ordinal)
            < responsiveLayout.IndexOf("DispatcherQueue.TryEnqueue(() =>", StringComparison.Ordinal));
        Assert.Contains("var focusHandoffVersion = ++_workbenchFocusHandoffVersion;", responsiveLayout, StringComparison.Ordinal);
        Assert.Contains("focusHandoffVersion != _workbenchFocusHandoffVersion", responsiveLayout, StringComparison.Ordinal);
        Assert.Contains("!IsVisibleInTree(focusReplacement) || !focusReplacement.IsEnabled", responsiveLayout, StringComparison.Ordinal);
        Assert.Contains("!ReferenceEquals(currentFocus, focusedElement) && IsVisibleInTree(control)", responsiveLayout, StringComparison.Ordinal);
        Assert.Contains("focusReplacement.Focus(FocusState.Programmatic)", responsiveLayout, StringComparison.Ordinal);
        Assert.Contains("IsFocusedWithin(focusedElement, WorkbenchTaskSelectorBar)", handoff, StringComparison.Ordinal);
        Assert.Contains("return WorkbenchTaskComboBox;", handoff, StringComparison.Ordinal);
        Assert.Contains("IsFocusedWithin(focusedElement, WorkbenchTaskComboBox)", handoff, StringComparison.Ordinal);
        Assert.Contains("IsFocusedWithin(focusedElement, WorkbenchAuxiliaryHeader)", handoff, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(handoff, "return GetActiveWorkbenchTaskButton();"));
        Assert.Contains("IsFocusedWithin(focusedElement, WorkbenchEditorColumnContent)", handoff, StringComparison.Ordinal);
        Assert.Contains("IsFocusedWithin(focusedElement, RawSignalModeSelector)", handoff, StringComparison.Ordinal);
        Assert.Contains("IsFocusedWithin(focusedElement, PreviewToolbar)", handoff, StringComparison.Ordinal);
        Assert.Contains("layout.IsBackToEditorButtonVisible ? BackToEditorButton : InspectionToggleButton", handoff, StringComparison.Ordinal);
        Assert.Contains("IsFocusedWithin(focusedElement, WorkbenchPreviewColumnContent)", handoff, StringComparison.Ordinal);
        Assert.Contains("layout.IsInspectionOverlay && IsFocusedWithin(focusedElement, WorkbenchPreviewColumnContent)", handoff, StringComparison.Ordinal);
        Assert.Contains("return InspectionCloseButton;", handoff, StringComparison.Ordinal);
        Assert.Contains("WorkbenchPreviewInteractionHost.IsEnabled = !layout.IsInspectionOverlay;", responsiveLayout, StringComparison.Ordinal);
        Assert.Contains("return PreviewToggleButton;", handoff, StringComparison.Ordinal);
        Assert.Contains("IsFocusedWithin(focusedElement, WorkbenchInspectionRail)", handoff, StringComparison.Ordinal);
        Assert.Contains("IsFocusedWithin(focusedElement, PreviewSplitter)", handoff, StringComparison.Ordinal);
        Assert.Contains("VisualTreeHelper.GetParent(current)", containment, StringComparison.Ordinal);
        var visibleFocus = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "IsVisibleInTree");
        Assert.Contains("Visibility: Visibility.Collapsed", visibleFocus, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", responsiveLayout + handoff, StringComparison.Ordinal);
        Assert.DoesNotContain("StartScanCommand", responsiveLayout + handoff, StringComparison.Ordinal);

        var openPreview = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "OpenPreviewButton_Click");
        Assert.Contains("WorkbenchInspectionRail.Visibility == Visibility.Visible", openPreview, StringComparison.Ordinal);
        Assert.Contains("WorkbenchPreviewColumnContent.Visibility != Visibility.Visible", openPreview, StringComparison.Ordinal);
        Assert.Contains("_inspectionOpenOverride = false;", openPreview, StringComparison.Ordinal);
        Assert.Contains("PreviewToggleButton.Focus(FocusState.Programmatic)", openPreview, StringComparison.Ordinal);
        var review = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "UpdateWorkbenchReviewEntry");
        Assert.Contains("if (hasPendingReview)", review, StringComparison.Ordinal);
        Assert.Contains("ShowFilmProfileReview();", review, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage01SourceContract_TaskNavigationHasNoImplicitRuntimeActionAndFocusIsFirst()
    {
        var page = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var task = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "SelectWorkbenchTask");
        var currentIssue = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "OnCurrentFilmProfileIssueNavigationRequested");
        var roiIssue = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "OnRoiIssueNavigationRequested");

        Assert.Contains("SetActiveWorkbenchSection(WorkbenchTaskSectionIndices[taskIndex]);", task, StringComparison.Ordinal);
        Assert.DoesNotContain("StartBringIntoView", task, StringComparison.Ordinal);
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var live = ExtractNamedRegion(xaml, "LiveCalibrationSection", "DeviceSettingsSection");
        var engineering = ExtractNamedRegion(xaml, "EngineeringToolsSection", "WorkbenchPreviewColumnContent");
        Assert.True(live.IndexOf("x:Name=\"LiveFocusCard\"", StringComparison.Ordinal) < live.IndexOf("x:Name=\"AdvancedAutofocusContent\"", StringComparison.Ordinal));
        Assert.DoesNotContain("SessionIlluminationTestCard", live, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchTaskSelectorBar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"WorkbenchTaskSelectorBar_SelectionChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"WorkbenchTaskComboBox_SelectionChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsSelected=\"True\"", GetOpeningTag(xaml, "SamplingTaskButton"), StringComparison.Ordinal);
        Assert.Contains("Tag=\"MotionContent\"", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.", task, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", task, StringComparison.Ordinal);
        Assert.Contains("_isAuxiliaryPanelOpen = sectionIndex is 0 or 4;", currentIssue, StringComparison.Ordinal);
        Assert.Contains("SetActiveWorkbenchSection(sectionIndex);", currentIssue, StringComparison.Ordinal);
        Assert.Contains("_inspectionOpenOverride = false;", roiIssue, StringComparison.Ordinal);
        Assert.Contains("SetActiveWorkbenchSection(sectionIndex);", roiIssue, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage01V_TaskSelectorsShareOneGuardedSectionIndexAndClearSelectionInAuxiliaryProperties()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
        var taskSelector = Named("WorkbenchTaskSelectorBar");
        var compactSelector = Named("WorkbenchTaskComboBox");
        var code = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var select = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "SelectWorkbenchTask");
        var synchronize = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "SynchronizeWorkbenchSectionSelectors");

        Assert.Equal(4, taskSelector.Elements().Count(element => element.Name.LocalName == "SelectorBarItem"));
        Assert.Equal(4, compactSelector.Elements().Count(element => element.Name.LocalName == "ComboBoxItem"));
        Assert.Equal("True", (string?)Named("SamplingTaskButton").Attribute("IsSelected"));
        Assert.Equal("0", (string?)compactSelector.Attribute("SelectedIndex"));
        Assert.Contains("[1, 2, 3, 5]", code, StringComparison.Ordinal);
        Assert.Contains("SetActiveWorkbenchSection(WorkbenchTaskSectionIndices[taskIndex]);", select, StringComparison.Ordinal);
        Assert.Contains("_lastWorkbenchTaskSectionIndex", synchronize, StringComparison.Ordinal);
        Assert.Contains("var selectedTaskIndex = _isAuxiliaryPanelOpen && index != 2 ? -1 : taskIndex;", synchronize, StringComparison.Ordinal);
        Assert.Contains("var selectedTask = selectedTaskIndex >= 0 ? WorkbenchTaskSelectorBar.Items[selectedTaskIndex] : null;", synchronize, StringComparison.Ordinal);
        Assert.Contains("WorkbenchTaskSelectorBar.SelectedItem = selectedTask;", synchronize, StringComparison.Ordinal);
        Assert.Contains("WorkbenchTaskComboBox.SelectedIndex = selectedTaskIndex;", synchronize, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingWorkbenchSection = true;", code, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.", select, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", select, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage01V_ChannelEditorAndIssueDetailUseBoundedSiblingScrollOwnersWithCompactFixedActions()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
        var channel = Named("ChannelCalibrationSection");
        var rail = Named("ChannelCalibrationRail");
        var identity = Named("ChannelCalibrationIdentityHeader");
        var editScroll = Named("ChannelCalibrationScrollViewer");
        var issuePanel = Named("ChannelCalibrationIssuePanel");
        var issueScroll = Named("ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer");
        var issueItems = Named("ChannelCalibrationIssueItemsControl");
        var editor = Named("ManualReferenceLevelsCard");
        var footer = Named("BwRoiSummary");

        Assert.Equal("Grid", channel.Name.LocalName);
        Assert.Equal("ChannelCalibrationSection_Loaded", (string?)channel.Attribute("Loaded"));
        Assert.Equal("ChannelCalibrationSection_SizeChanged", (string?)channel.Attribute("SizeChanged"));
        Assert.Contains(rail, channel.Descendants());
        Assert.Equal(["Auto", "*", "Auto"], rail.Elements().Single(element => element.Name.LocalName == "Grid.RowDefinitions")
            .Elements().Select(element => (string?)element.Attribute("Height")));
        Assert.Same(rail, identity.Parent);
        Assert.Contains(Named("CalibrationChannelFallbackComboBox"), identity.Descendants());
        Assert.Equal("{x:Bind ViewModel.SelectedCalibrationChannel, Mode=OneWay}",
            (string?)Named("CalibrationChannelFallbackComboBox").Attribute("SelectedItem"));
        Assert.Same(rail, editScroll.Parent);
        Assert.Same(rail, issuePanel.Parent);
        Assert.Contains(footer, editScroll.Descendants());
        Assert.Same(rail, Named("ChannelCalibrationSaveFooter").Parent);
        Assert.Equal("1", (string?)editScroll.Attribute("Grid.Row"));
        Assert.Equal("1", (string?)issuePanel.Attribute("Grid.Row"));
        Assert.Equal("Collapsed", (string?)issuePanel.Attribute("Visibility"));
        Assert.Equal("1", (string?)issueScroll.Attribute("Grid.Row"));
        Assert.Null(footer.Attribute("Grid.Row"));
        Assert.Equal("2", (string?)Named("ChannelCalibrationSaveFooter").Attribute("Grid.Row"));
        Assert.Contains(editor, editScroll.Descendants());
        Assert.Contains(issueItems, issueScroll.Descendants());
        Assert.Equal("{x:Bind ViewModel.ChannelCalibrationCurrentFilmProfileValidationIssueDisplays, Mode=OneWay}",
            (string?)issueItems.Attribute("ItemsSource"));
        Assert.Equal("{StaticResource FilmProfileBwIssueTemplateSelector}",
            (string?)issueItems.Attribute("ItemTemplateSelector"));
        Assert.DoesNotContain(issueScroll, editScroll.Descendants());
        Assert.DoesNotContain(editScroll, issueScroll.Descendants());
        Assert.DoesNotContain(issuePanel, editScroll.Descendants());
        Assert.DoesNotContain(issueScroll.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Null(issueScroll.Attribute("MaxHeight"));
        Assert.DoesNotContain(Named("ChannelCalibrationSaveFooter"), editScroll.Descendants());
        Assert.DoesNotContain(footer, issueScroll.Descendants());
        Assert.Contains(footer.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.BwValidationSummaryText, Mode=OneWay}");
        Assert.Contains(footer.Descendants(), element => (string?)element.Attribute("Click") == "BwViewIssuesButton_Click");
        Assert.Contains(Named("BwValidationActionPanel"), footer.Descendants());
        Assert.Contains(issuePanel.Descendants(), element => (string?)element.Attribute("Click") == "BwReturnToParametersButton_Click");
        Assert.Contains(issueScroll.Descendants(), element => (string?)element.Attribute("Visibility") == "{x:Bind ViewModel.ChannelCalibrationIssueEmptyVisibility, Mode=OneWay}");
        Assert.Equal("Collapsed", (string?)Named("ChannelCalibrationCompactScrollViewer").Attribute("Visibility"));
    }

    [Fact]
    public void Stage01V_ChannelIssueDetailRendersActualMessagesAndOnlyNavigableIssuesOfferLocate()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
        XElement Template(string key) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Key") == key);
        var issuePanel = Named("ChannelCalibrationIssuePanel");
        var issueItems = Named("ChannelCalibrationIssueItemsControl");
        var navigable = Template("FilmProfileBwNavigableValidationIssueTemplate");
        var passive = Template("FilmProfilePassiveValidationIssueTemplate");
        var bwPassive = Template("FilmProfileBwPassiveValidationIssueTemplate");
        var selector = Template("FilmProfileBwIssueTemplateSelector");

        Assert.Contains(issueItems, issuePanel.Descendants());
        Assert.DoesNotContain(issueItems, Named("ChannelCalibrationDetailsExpander").Descendants());
        Assert.Equal("{StaticResource FilmProfileBwNavigableValidationIssueTemplate}", (string?)selector.Attribute("NavigableTemplate"));
        Assert.Equal("{StaticResource FilmProfileBwPassiveValidationIssueTemplate}", (string?)selector.Attribute("PassiveTemplate"));
        var locate = Assert.Single(navigable.Descendants(), element => element.Name.LocalName == "Button");
        Assert.Equal("BwIssueButton_Click", (string?)locate.Attribute("Click"));
        Assert.Equal("{Binding AutomationId}", (string?)locate.Attribute("AutomationProperties.AutomationId"));
        Assert.Equal("{Binding AutomationName}", (string?)locate.Attribute("AutomationProperties.Name"));
        Assert.Contains(locate.Descendants(), element => (string?)element.Attribute("Text") == "{Binding Message}");
        Assert.Contains(locate.Descendants(), element => (string?)element.Attribute("Text") == "{Binding FieldDisplayName}");
        Assert.Contains(locate.Descendants(), element => (string?)element.Attribute("ToolTipService.ToolTip") == "{Binding FieldPath}");
        Assert.Contains(locate.Descendants(), element => (string?)element.Attribute("Text") == "{Binding SourceSeverityText}");
        Assert.Contains(locate.Descendants(), element => (string?)element.Attribute(x + "Uid") == "ScanDebug_BwLocateIssueAction");
        Assert.DoesNotContain(passive.Descendants(), element => element.Name.LocalName == "Button");
        Assert.Contains(passive.Descendants(), element => (string?)element.Attribute("Text") == "{Binding Message}");
        Assert.Contains(passive.Descendants(), element => (string?)element.Attribute("Text") == "{Binding FieldDisplayName}");
        Assert.Contains(passive.Descendants(), element => (string?)element.Attribute("ToolTipService.ToolTip") == "{Binding FieldPath}");
        Assert.DoesNotContain(bwPassive.Descendants(), element => element.Name.LocalName == "Button");
        Assert.Contains(bwPassive.Descendants(), element => (string?)element.Attribute("ContentTemplate") == "{StaticResource FilmProfilePassiveValidationIssueTemplate}");
        Assert.Contains(bwPassive.Descendants(), element => (string?)element.Attribute("Text") == "{Binding SourceSeverityText}");
        Assert.DoesNotContain(issuePanel.Descendants(), element => element.Name.LocalName == "InfoBar");
    }

    [Fact]
    public void Stage01V_ChannelIssueViewShowsMessagesDirectlyAndReturnPreservesTheEditor()
    {
        var code = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var show = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "BwViewIssuesButton_Click");
        var returnToEditor = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "BwReturnToParametersButton_Click");
        var exit = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "ExitBwIssueView");
        var locate = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "BwIssueButton_Click");
        var configure = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "BwConfigureChannelButton_Click");
        var focus = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "TryFocusBwIssue");
        var cancelFocus = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "CancelBwIssueFocus");

        Assert.Contains("ChannelCalibrationIssuePanel.Visibility = Visibility.Visible;", show, StringComparison.Ordinal);
        Assert.Contains("BwValidationActionPanel.MinHeight = BwViewIssuesButton.ActualHeight;", show, StringComparison.Ordinal);
        Assert.Contains("BwValidationSummaryTextBlock.Margin = new Thickness(0, inset, 0, 0);", show, StringComparison.Ordinal);
        Assert.Contains("BwValidationSummaryTextBlock.Margin = new Thickness(0);", exit, StringComparison.Ordinal);
        Assert.True(show.IndexOf("BwValidationActionPanel.MinHeight = BwViewIssuesButton.ActualHeight;", StringComparison.Ordinal)
            < show.IndexOf("BwViewIssuesButton.Visibility = Visibility.Collapsed;", StringComparison.Ordinal));
        Assert.Contains("ChannelCalibrationScrollViewer.Visibility = Visibility.Collapsed;", show, StringComparison.Ordinal);
        Assert.Contains("BwReturnToParametersButton.Focus(FocusState.Programmatic)", show, StringComparison.Ordinal);
        Assert.DoesNotContain("ChannelCalibrationDetailsExpander.IsExpanded", show, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateLayout()", show, StringComparison.Ordinal);
        Assert.Contains("ExitBwIssueView(returnFocus: true)", returnToEditor, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationIssuePanel.Visibility = Visibility.Collapsed;", exit, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationScrollViewer.Visibility = Visibility.Visible;", exit, StringComparison.Ordinal);
        Assert.Contains("BwViewIssuesButton.Focus(FocusState.Programmatic)", exit, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadManualReferenceFromCurrentTarget", show + exit, StringComparison.Ordinal);
        Assert.DoesNotContain("ChannelCalibrationScrollViewer.ChangeView", show + exit, StringComparison.Ordinal);
        Assert.Contains("TryRequestCurrentFilmProfileIssueNavigationAsync(display)", locate, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationIssuePanel.Visibility != Visibility.Visible", locate, StringComparison.Ordinal);
        Assert.Contains("ExitBwIssueView(returnFocus: false);", configure, StringComparison.Ordinal);
        Assert.Contains("BeginIssueTargetFocus(2, ChannelCalibrationScrollViewer, ExposureMicrosecondsTextBox", configure, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", show + exit + locate + configure, StringComparison.Ordinal);

        Assert.Contains("IsCurrentActivation(activationEpoch)", focus, StringComparison.Ordinal);
        Assert.Contains("requestVersion != _bwFocusRequestVersion", focus, StringComparison.Ordinal);
        Assert.Contains("channelRole, ViewModel.SelectedCalibrationChannel", focus, StringComparison.Ordinal);
        Assert.Contains("IsControlFullyVisible(target", focus, StringComparison.Ordinal);
        Assert.Contains("FocusManager.GetFocusedElement(root)", focus, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationIssueItemsControl.LayoutUpdated -= _bwIssueLayoutHandler", cancelFocus, StringComparison.Ordinal);
        Assert.Contains("CancelBwIssueFocus();", exit, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage01V_ChannelSwitchAndIssueNavigationInvalidateDeferredFocusWithoutCommands()
    {
        var code = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var select = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "SelectCalibrationChannelAsync");
        var synchronize = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "SynchronizeCalibrationChannelSelectors");
        var navigate = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "OnCurrentFilmProfileIssueNavigationRequested");
        var beginFocus = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "BeginIssueTargetFocus");
        var completeFocus = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "TryCompleteIssueTargetFocus");
        var cancelFocus = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "CancelIssueTargetFocus");
        var switchTask = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "SetActiveWorkbenchSection");
        var unload = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "OnUnloaded");
        var resize = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "ChannelCalibrationSection_SizeChanged");

        Assert.Contains("CalibrationChannelFallbackComboBox.SelectedItem = ViewModel.SelectedCalibrationChannel;", synchronize, StringComparison.Ordinal);
        Assert.Contains("if (_calibrationChannelSelectionPending)", select, StringComparison.Ordinal);
        Assert.Contains("await ViewModel.TrySelectCalibrationChannelAsync(nextChannel)", select, StringComparison.Ordinal);
        Assert.Contains("IsCurrentActivation(activationEpoch)", select, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.SelectedCalibrationChannel = nextChannel", select, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", select, StringComparison.Ordinal);

        Assert.Contains("ExitBwIssueView(returnFocus: false);", navigate, StringComparison.Ordinal);
        Assert.Contains("request.EditorTarget is ScanFilmProfileIssueEditorTarget.ChannelStatus", navigate, StringComparison.Ordinal);
        Assert.Contains("request.EditorTarget == ScanFilmProfileIssueEditorTarget.ChannelRoiSettings", navigate, StringComparison.Ordinal);
        Assert.Contains("BeginIssueTargetFocus(sectionIndex, scroller, target, request.ChannelRole", navigate, StringComparison.Ordinal);
        Assert.Contains("ScanFilmProfileIssueEditorTarget.ChannelStatus => ExposureMicrosecondsTextBox", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", navigate, StringComparison.Ordinal);

        Assert.Contains("CancelIssueTargetFocus();", beginFocus, StringComparison.Ordinal);
        Assert.Contains("target.LayoutUpdated += _issueTargetLayoutHandler", beginFocus, StringComparison.Ordinal);
        Assert.Contains("_issueTargetViewport.ViewChanged += _issueTargetViewChangedHandler", beginFocus, StringComparison.Ordinal);
        Assert.Contains("requestVersion != _issueTargetFocusRequestVersion", completeFocus, StringComparison.Ordinal);
        Assert.Contains("IsCurrentActivation(activationEpoch)", completeFocus, StringComparison.Ordinal);
        Assert.Contains("!IsCurrentFilmProfileNavigationStillValid()", completeFocus, StringComparison.Ordinal);
        Assert.Contains("channelRole, ViewModel.SelectedCalibrationChannel", completeFocus, StringComparison.Ordinal);
        Assert.Contains("IsControlFullyVisible(target, viewport)", completeFocus, StringComparison.Ordinal);
        Assert.Contains("FocusManager.GetFocusedElement(root)", completeFocus, StringComparison.Ordinal);
        Assert.Contains("target.Focus(FocusState.Programmatic)", completeFocus, StringComparison.Ordinal);
        Assert.Contains("_issueTargetFocusControl.LayoutUpdated -= _issueTargetLayoutHandler", cancelFocus, StringComparison.Ordinal);
        Assert.Contains("_issueTargetViewport.ViewChanged -= _issueTargetViewChangedHandler", cancelFocus, StringComparison.Ordinal);
        Assert.Contains("CancelIssueTargetFocus();", switchTask, StringComparison.Ordinal);
        Assert.Contains("ViewModel.CancelPendingCalibrationChannelSelection();", switchTask, StringComparison.Ordinal);
        Assert.Contains("CancelBwIssueFocus();", unload, StringComparison.Ordinal);
        Assert.Contains("CancelIssueTargetFocus();", unload, StringComparison.Ordinal);

        Assert.Contains("ChannelCalibrationCompactScrollViewer.Content = ChannelCalibrationRail;", resize, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationScrollViewer.VerticalScrollMode = ScrollMode.Disabled;", resize, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer.VerticalScrollMode = ScrollMode.Disabled;", resize, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationCompactScrollViewer.Content = null;", resize, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationBodyRow.Height = new GridLength(1, GridUnitType.Star);", resize, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage01V_TaskFirstEditorsAndDisclosurePreserveSingleInputOwners()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
        var acquisition = Named("AcquisitionPlanSection");
        var channel = Named("ChannelCalibrationSection");
        var focus = Named("LiveCalibrationSection");
        var motion = Named("MotionTaskSection");
        var engineering = Named("EngineeringToolsSection");
        var code = ReadAppSource("Views", "ScanDebugPage.xaml.cs");

        Assert.Contains(Named("AcquisitionRowsComboBox"), acquisition.Descendants());
        Assert.Contains(Named("AcquisitionChannelAssignmentList"), acquisition.Descendants());
        var mappingEntry = Named("AcquisitionDeviceMappingButton");
        Assert.Contains(mappingEntry, Named("AcquisitionTransportDetailsExpander").Descendants());
        Assert.Equal("HyperlinkButton", mappingEntry.Name.LocalName);
        Assert.Equal("OpenDeviceChannelsSettings_Click", (string?)mappingEntry.Attribute("Click"));
        Assert.Null(mappingEntry.Attribute("SelectedItem"));
        Assert.DoesNotContain(acquisition.Descendants(), element =>
            ((string?)element.Attribute("SelectedItem"))?.Contains("SelectedScanMotor", StringComparison.Ordinal) == true);
        Assert.Contains(Named("AcquisitionTransportStrategyToggleSwitch"), acquisition.Descendants());
        Assert.Contains(Named("AcquisitionTransportStrategyToggleSwitch"), Named("AcquisitionTransportStrategyExpander").Descendants());
        Assert.Contains(Named("CalibrationChannelStatusListView"), Named("ChannelCalibrationDetailsExpander").Descendants());
        Assert.Equal("None", (string?)Named("CalibrationChannelStatusListView").Attribute("SelectionMode"));
        Assert.Contains(Named("AdcRoiEditorCard"), Named("AdcRoiDetailsExpander").Descendants());
        Assert.Contains(Named("FocusRoiEditorCard"), Named("FocusRoiDetailsExpander").Descendants());
        Assert.Contains(Named("AdcRoiEditorCard"), channel.Descendants());
        Assert.Contains(Named("FocusRoiEditorCard"), focus.Descendants());
        Assert.DoesNotContain(Named("FocusRoiEditorCard"), channel.Descendants());
        Assert.DoesNotContain(Named("AdcRoiEditorCard"), focus.Descendants());
        Assert.Single(focus.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.FocusRoiSummaryText, Mode=OneWay}");
        Assert.Single(channel.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.BwActiveRoiRangeText, Mode=OneWay}");
        Assert.Single(channel.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.BwShieldRoiRangeText, Mode=OneWay}");
        foreach (var owner in new[] { "Adc", "Focus" })
        {
            var card = Named($"{owner}RoiEditorCard");
            var selector = Named($"{owner}RoiTargetComboBox");
            Assert.Contains(selector, card.Descendants());
            Assert.Equal($"{{x:Bind ViewModel.{owner}RoiSelectionOptions, Mode=OneWay}}", (string?)selector.Attribute("ItemsSource"));
            Assert.Equal($"{{x:Bind ViewModel.Selected{owner}RoiSelection, Mode=TwoWay}}", (string?)selector.Attribute("SelectedItem"));
            foreach (var (suffix, input) in new[] { ("Start", "Start"), ("End", "End") })
            {
                var textBox = Named($"{owner}Roi{suffix}TextBox");
                Assert.Contains(textBox, card.Descendants());
                Assert.Equal($"{{x:Bind ViewModel.{owner}Roi{input}Input, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}", (string?)textBox.Attribute("Text"));
            }
            Assert.Single(card.Descendants(), element => (string?)element.Attribute("Text") == $"{{x:Bind ViewModel.{owner}RoiTargetText, Mode=OneWay}}");
            Assert.Single(card.Descendants(), element => (string?)element.Attribute("Text") == $"{{x:Bind ViewModel.{owner}RoiInputStatusText, Mode=OneWay}}");
            var issues = Assert.Single(card.Descendants(), element => (string?)element.Attribute("ItemsSource") == $"{{x:Bind ViewModel.{owner}RoiValidationIssueDisplays, Mode=OneWay}}");
            Assert.Single(issues.Descendants(), element => (string?)element.Attribute("Command") == "{Binding DataContext.NavigateToRoiIssueCommand, ElementName=ScanDebugRootGrid}");
            foreach (var (action, command) in new[]
            {
                ($"Apply{owner}Roi", $"Apply{owner}RoiInputsCommand"),
                ($"Reset{owner}Roi", $"Reset{owner}RoiCommand"),
                ($"Reload{owner}RoiInputs", $"Reload{owner}RoiInputsCommand")
            })
            {
                var button = Assert.Single(card.Descendants(), element => (string?)element.Attribute(x + "Uid") == $"ScanDebug_{action}Button");
                Assert.Equal($"{{x:Bind ViewModel.{command}}}", (string?)button.Attribute("Command"));
                Assert.Equal("{StaticResource ScanDebugWrappingButtonContentTemplate}", (string?)button.Attribute("ContentTemplate"));
            }
            Assert.Single(card.Descendants(), element => (string?)element.Attribute("Command") == "{x:Bind ViewModel.ResetAllRoisCommand}");
            Assert.DoesNotContain(card.Descendants(), element => ((string?)element.Attribute("Text"))?.Contains("ViewModel.RoiStartInput", StringComparison.Ordinal) == true
                || ((string?)element.Attribute("Text"))?.Contains("ViewModel.RoiEndInput", StringComparison.Ordinal) == true);
        }
        Assert.DoesNotContain(Named("ChannelCalibrationIssueItemsControl"), Named("ChannelCalibrationDetailsExpander").Descendants());
        Assert.Contains(Named("ChannelCalibrationIssuePanel"), channel.Descendants());
        Assert.Contains(Named("ManualReferenceLevelsCard"), channel.Descendants());
        var navigation = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "OnCurrentFilmProfileIssueNavigationRequested");
        Assert.Contains("request.EditorTarget is ScanFilmProfileIssueEditorTarget.ChannelStatus", navigation, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationDetailsExpander.IsExpanded = true;", navigation, StringComparison.Ordinal);
        Assert.Contains("ScanFilmProfileIssueEditorTarget.ChannelStatus => ExposureMicrosecondsTextBox", code, StringComparison.Ordinal);
        Assert.Contains("FocusRoiDetailsExpander.IsExpanded = true;", code, StringComparison.Ordinal);
        Assert.Contains("AdcRoiDetailsExpander.IsExpanded = true;", code, StringComparison.Ordinal);
        Assert.Contains("AcquisitionTransportStrategyExpander.IsExpanded = true;", code, StringComparison.Ordinal);

        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var sampling = ExtractNamedRegion(xaml, "AcquisitionPlanSection", "ChannelCalibrationSection");
        var blackWhite = ExtractNamedRegion(xaml, "ChannelCalibrationSection", "LiveCalibrationSection");
        var focusing = ExtractNamedRegion(xaml, "LiveCalibrationSection", "DeviceSettingsSection");
        Assert.True(sampling.IndexOf("AcquisitionRowsComboBox", StringComparison.Ordinal) < sampling.IndexOf("AcquisitionChannelAssignmentList", StringComparison.Ordinal));
        Assert.True(blackWhite.IndexOf("CalibrationChannelFallbackComboBox", StringComparison.Ordinal) < blackWhite.IndexOf("ManualReferenceLevelsCard", StringComparison.Ordinal));
        Assert.True(blackWhite.IndexOf("ManualReferenceLevelsCard", StringComparison.Ordinal) < blackWhite.IndexOf("ChannelCalibrationDetailsExpander", StringComparison.Ordinal));
        Assert.True(focusing.IndexOf("ScanDebug_RunAutofocusButton", StringComparison.Ordinal) < focusing.IndexOf("FocusRoiDetailsExpander", StringComparison.Ordinal));
        Assert.True(focusing.IndexOf("ManualFocusNegativeButton", StringComparison.Ordinal) < focusing.IndexOf("FocusRoiDetailsExpander", StringComparison.Ordinal));
        Assert.Equal("0", (string?)Named("MotionExpander").Attribute("Grid.Row"));
        Assert.Contains(Named("MotionExpander"), motion.Descendants());
        Assert.Contains("FindName(\"MotionContent\")", code, StringComparison.Ordinal);
        Assert.Contains(Named("MotionScrollViewer"), motion.Descendants());
        Assert.Contains(Named("EngineeringToolsScrollViewer"), engineering.Descendants());
        Assert.DoesNotContain(Named("MotionScrollViewer"), engineering.Descendants());
        Assert.DoesNotContain(Named("EngineeringToolsScrollViewer"), motion.Descendants());
        Assert.DoesNotContain("Command=\"{x:Bind ViewModel.ApplySelectedRoiInputsCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{x:Bind ViewModel.ResetSelectedRoiCommand}\"", xaml, StringComparison.Ordinal);
        var viewModel = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        foreach (var (owner, selection, canExecute) in new[]
        {
            ("Adc", "SelectedAdcRoiSelection", "CanEditAdcRoiDraft"),
            ("Focus", "SelectedFocusRoiSelection", "CanEditFocusRoiDraft")
        })
        {
            foreach (var (method, operation) in new[]
            {
                ($"Apply{owner}RoiInputs", "ApplyRoiNumericDraft"),
                ($"Reset{owner}Roi", "ResetRoiNumericDraft"),
                ($"Reload{owner}RoiInputs", "ReloadRoiNumericDraft")
            })
            {
                Assert.Matches($@"\[RelayCommand\(CanExecute = nameof\({canExecute}\)\)\]\s+private void {method}\(\)\s*=> {operation}\({selection}\);", viewModel);
            }
            Assert.Contains($"get => GetRoiNumericDraft({selection})?.Start", viewModel, StringComparison.Ordinal);
            Assert.Contains($"get => GetRoiNumericDraft({selection})?.End", viewModel, StringComparison.Ordinal);
        }
        var numericDraft = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(viewModel, "GetRoiNumericDraft");
        Assert.Contains("(SelectedCalibrationChannel.ToUpperInvariant(), selection)", numericDraft, StringComparison.Ordinal);
        Assert.Contains("GetRoiRange(_roiSettings, selection)", numericDraft, StringComparison.Ordinal);
        var applyDraft = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(viewModel, "ApplyRoiNumericDraft");
        Assert.Contains("GetRoiRange(_roiSettings, selection) != draft.AppliedRange", applyDraft, StringComparison.Ordinal);
        Assert.Contains("TryBuildRoiEditCandidate(selection, new ScanColumnRange(start, end)", applyDraft, StringComparison.Ordinal);
        Assert.Contains("TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ApplySelectedRoiInputs", applyDraft, StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs();", applyDraft, StringComparison.Ordinal);
        var resetDraft = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(viewModel, "ResetRoiNumericDraft");
        Assert.Contains("var defaults = ScanCalibrationRoiSettings.CreateDefault();", resetDraft, StringComparison.Ordinal);
        Assert.Contains("GetRoiRange(defaults, selection)", resetDraft, StringComparison.Ordinal);
        Assert.Contains("TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ResetSelectedRoi", resetDraft, StringComparison.Ordinal);
        var reloadDraft = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(viewModel, "ReloadRoiNumericDraft");
        Assert.Contains("GetRoiRange(_roiSettings, selection)", reloadDraft, StringComparison.Ordinal);
        Assert.DoesNotContain("SynchronizeFilmProfileDraftFromInputs", reloadDraft, StringComparison.Ordinal);
        var previewSelection = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(viewModel, "OnSelectedRoiSelectionChanged");
        Assert.DoesNotContain("SelectedAdcRoiSelection =", previewSelection, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedFocusRoiSelection =", previewSelection, StringComparison.Ordinal);
        Assert.DoesNotContain("_roiNumericDrafts", previewSelection, StringComparison.Ordinal);
        Assert.Contains("SelectedRoiSelection = value;", ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(viewModel, "OnSelectedFocusRoiSelectionChanged"), StringComparison.Ordinal);
        Assert.Contains("SelectedRoiSelection = value;", ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(viewModel, "OnSelectedAdcRoiSelectionChanged"), StringComparison.Ordinal);
        foreach (var (method, command) in new[] { ("ApplySelectedRoiInputs", "ApplySelectedRoiInputs"), ("ResetSelectedRoi", "ResetSelectedRoi") })
            Assert.Contains($"TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.{command}", ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(viewModel, method), StringComparison.Ordinal);
        foreach (var motor in Enumerable.Range(1, 3))
        {
            var settings = Assert.Single(motion.Descendants(), element => element.Name.LocalName == "Expander"
                && (string?)element.Attribute(x + "Uid") == "ScanDebug_MotorSettingsExpander"
                && element.Descendants().Any(child => (string?)child.Attribute(x + "Uid") == $"ScanDebug_Motor{motor}ApplyConfigButton"));
            Assert.Contains(settings.Descendants(), element => (string?)element.Attribute(x + "Uid") == $"ScanDebug_Motor{motor}EnableButton");
            Assert.DoesNotContain(settings.Descendants(), element => (string?)element.Attribute(x + "Uid") == $"ScanDebug_Motor{motor}StopButton");
            Assert.Contains(motion.Descendants(), element => (string?)element.Attribute(x + "Uid") == $"ScanDebug_Motor{motor}StopButton");
        }
    }

    [Fact]
    public void Stage01V_MotionMoveRequiresVisibleInputsAndSummaryWhileAxisStopsSurviveCollapsedSettings()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
        var motion = Named("MotionContent");
        var selector = Named("MotorAxisComboBox");
        var code = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var selectAxis = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "MotorAxisComboBox_SelectionChanged");
        var actionGrid = Named("MotorActionGrid");
        var motionTask = Named("MotionTaskSection");
        var motionScroll = Named("MotionScrollViewer");
        var effectiveSummary = Assert.Single(motion.Elements(), element =>
            (string?)element.Attribute("Text") == "{x:Bind ViewModel.MotionSummaryText, Mode=OneWay}");
        var motionChildren = motion.Elements().ToList();

        Assert.Equal("False", (string?)motion.Attribute(x + "Load"));
        Assert.Equal("0", (string?)selector.Attribute("SelectedIndex"));
        Assert.Equal(3, selector.Elements().Count(element => element.Name.LocalName == "ComboBoxItem"));
        Assert.Equal("MotorAxisComboBox_SelectionChanged", (string?)selector.Attribute("SelectionChanged"));
        Assert.Same(motionTask, actionGrid.Parent);
        Assert.Equal("1", (string?)actionGrid.Attribute("Grid.Row"));
        Assert.Same(motionTask, motionScroll.Parent);
        Assert.Contains(motion, motionScroll.Descendants());
        Assert.DoesNotContain(actionGrid.Ancestors(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Equal(["*", "Auto"], motionTask.Elements()
            .Single(element => element.Name.LocalName == "Grid.RowDefinitions").Elements()
            .Select(element => (string?)element.Attribute("Height")));

        foreach (var motor in Enumerable.Range(1, 3))
        {
            var axis = Named($"Motor{motor}DetailsExpander");
            Assert.Equal("Border", axis.Name.LocalName);
            Assert.Same(motion, axis.Parent);
            Assert.Equal(motor == 1 ? null : "Collapsed", (string?)axis.Attribute("Visibility"));
            var elements = axis.Descendants().ToList();
            XElement Uid(string uid) => Assert.Single(elements, element => (string?)element.Attribute(x + "Uid") == uid);
            var direction = Assert.Single(elements, element =>
                (string?)element.Attribute("AutomationProperties.AutomationId") == $"Motor{motor}MoveDirectionComboBox");
            var moveValue = Uid($"ScanDebug_Motor{motor}MoveValueTextBox");
            var moveUnit = Uid($"ScanDebug_Motor{motor}MoveUnitComboBox");
            var speedValue = Uid($"ScanDebug_Motor{motor}SpeedValueTextBox");
            var speedUnit = Uid($"ScanDebug_Motor{motor}SpeedUnitComboBox");
            var summary = Assert.Single(elements, element =>
                (string?)element.Attribute("Text") == $"{{x:Bind ViewModel.Motor{motor}MovePreviewText, Mode=OneWay}}");
            Assert.Equal($"{{x:Bind ViewModel.Motor{motor}MoveSummaryText, Mode=OneWay}}", (string?)summary.Attribute("ToolTipService.ToolTip"));
            var move = Named($"Motor{motor}MoveButton");
            var stop = Named($"Motor{motor}StopButton");
            Assert.Equal($"ScanDebug_Motor{motor}MoveButton", (string?)move.Attribute(x + "Uid"));
            Assert.Equal($"ScanDebug_Motor{motor}StopButton", (string?)stop.Attribute(x + "Uid"));
            Assert.Same(actionGrid, move.Parent);
            Assert.Same(actionGrid, stop.Parent);
            Assert.Equal($"Motor{motor}StopButton", (string?)stop.Attribute("AutomationProperties.AutomationId"));
            var settings = Named($"Motor{motor}SettingsExpander");
            Assert.Equal("Expander", settings.Name.LocalName);
            Assert.Same(motion, settings.Parent);
            Assert.Equal("ScanDebug_MotorSettingsExpander", (string?)settings.Attribute(x + "Uid"));
            Assert.Equal(motor == 1 ? null : "Collapsed", (string?)settings.Attribute("Visibility"));

            Assert.Equal("ScanDebug_MotorMoveDirectionComboBox", (string?)direction.Attribute(x + "Uid"));
            Assert.Equal($"{{x:Bind ViewModel.Motor{motor}MoveDirectionOptions, Mode=OneWay}}", (string?)direction.Attribute("ItemsSource"));
            Assert.Equal("{StaticResource ScanMappedMotorDirectionOptionTemplate}", (string?)direction.Attribute("ItemTemplate"));
            Assert.Equal("Token", (string?)direction.Attribute("SelectedValuePath"));
            Assert.Equal($"{{x:Bind ViewModel.Motor{motor}MoveDirection, Mode=TwoWay}}", (string?)direction.Attribute("SelectedValue"));
            Assert.Equal($"{{x:Bind ViewModel.Motor{motor}MoveValue, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}", (string?)moveValue.Attribute("Text"));
            Assert.Equal($"{{x:Bind ViewModel.Motor{motor}MoveUnit, Mode=TwoWay}}", (string?)moveUnit.Attribute("SelectedItem"));
            Assert.Equal($"{{x:Bind ViewModel.Motor{motor}SpeedValue, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}", (string?)speedValue.Attribute("Text"));
            Assert.Equal($"{{x:Bind ViewModel.Motor{motor}SpeedUnit, Mode=TwoWay}}", (string?)speedUnit.Attribute("SelectedItem"));
            Assert.Equal("{x:Bind ViewModel.StopMotorCommand}", (string?)stop.Attribute("Command"));
            Assert.Equal(motor.ToString(), (string?)stop.Attribute("CommandParameter"));
            Assert.Null(stop.Attribute("Visibility"));
            Assert.Equal("{x:Bind ViewModel.MoveMotorCommand}", (string?)move.Attribute("Command"));
            Assert.Equal(motor.ToString(), (string?)move.Attribute("CommandParameter"));
            Assert.Equal(motor == 1 ? null : "Collapsed", (string?)move.Attribute("Visibility"));
            Assert.Equal("False", (string?)settings.Attribute("IsExpanded"));
            Assert.Equal(motor - 1, motionChildren.IndexOf(settings) - motionChildren.IndexOf(Named("Motor1SettingsExpander")));
            Assert.True(motionChildren.IndexOf(settings) < motionChildren.IndexOf(effectiveSummary));
            Assert.Single(selector.Elements(), element =>
                (string?)element.Attribute(x + "Uid") == $"ScanDebug_Motor{motor}AxisComboBoxItem");
            Assert.Single(settings.Descendants(), element =>
                (string?)element.Attribute("ToolTipService.ToolTip") == $"{{x:Bind ViewModel.Motor{motor}ApplyConfigEffectText, Mode=OneWay}}"
                && (string?)element.Attribute("AutomationProperties.HelpText") == $"{{x:Bind ViewModel.Motor{motor}ApplyConfigEffectText, Mode=OneWay}}");
            Assert.True(elements.IndexOf(direction) < elements.IndexOf(moveValue));
            Assert.True(elements.IndexOf(moveValue) < elements.IndexOf(moveUnit));
            Assert.True(elements.IndexOf(moveUnit) < elements.IndexOf(speedValue));
            Assert.True(elements.IndexOf(speedValue) < elements.IndexOf(speedUnit));
            Assert.True(elements.IndexOf(speedUnit) < elements.IndexOf(summary));
            Assert.Equal("StackPanel", summary.Parent!.Name.LocalName);
            Assert.DoesNotContain(stop, axis.Descendants());
            Assert.DoesNotContain(stop, settings.Descendants());
            Assert.DoesNotContain(move, settings.Descendants());
            Assert.True(motionChildren.IndexOf(axis) < motionChildren.IndexOf(settings));
            foreach (var (action, command) in new[]
            {
                ("EnableButton", "EnableMotorCommand"),
                ("DisableButton", "DisableMotorCommand"),
                ("ApplyConfigButton", "ApplyMotorConfigCommand")
            })
            {
                var button = Assert.Single(settings.Descendants(), element =>
                    (string?)element.Attribute(x + "Uid") == $"ScanDebug_Motor{motor}{action}");
                Assert.Equal("Button", button.Name.LocalName);
                Assert.Equal($"{{x:Bind ViewModel.{command}}}", (string?)button.Attribute("Command"));
                Assert.Equal(motor.ToString(), (string?)button.Attribute("CommandParameter"));
            }
            Assert.Contains(axis, motion.Descendants());
            Assert.Contains($"Motor{motor}DetailsExpander.Visibility = ToVisibility(MotorAxisComboBox.SelectedIndex == {motor - 1});", selectAxis, StringComparison.Ordinal);
            Assert.Contains($"Motor{motor}SettingsExpander.Visibility = Motor{motor}DetailsExpander.Visibility;", selectAxis, StringComparison.Ordinal);
            Assert.Contains($"Motor{motor}StatusTextBlock.Visibility = Motor{motor}DetailsExpander.Visibility;", selectAxis, StringComparison.Ordinal);
            Assert.Contains($"Motor{motor}MoveButton.Visibility = Motor{motor}DetailsExpander.Visibility;", selectAxis, StringComparison.Ordinal);
        }

        Assert.Contains("Grid.SetRow(stopButtons[index], isCurrentAxis ? 0 : 1);", selectAxis, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(stopButtons[index], isCurrentAxis ? 1 : nextOtherStopColumn++);", selectAxis, StringComparison.Ordinal);
        Assert.DoesNotContain("ChangeView(", selectAxis, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.", selectAxis, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", selectAxis, StringComparison.Ordinal);
        Assert.DoesNotContain("MoveMotorCommand", selectAxis, StringComparison.Ordinal);
        Assert.DoesNotContain("StopMotorCommand", selectAxis, StringComparison.Ordinal);
        Assert.DoesNotContain("Motor1MoveValue", selectAxis, StringComparison.Ordinal);
        Assert.Equal(6, actionGrid.Elements().Count(element => element.Name.LocalName == "Button"));
        Assert.Equal("1", (string?)Named("Motor1StopButton").Attribute("Grid.Column"));
        Assert.Equal("1", (string?)Named("Motor2StopButton").Attribute("Grid.Row"));
        Assert.Equal("1", (string?)Named("Motor3StopButton").Attribute("Grid.Row"));
        Assert.Equal("1", (string?)Named("Motor3StopButton").Attribute("Grid.Column"));
        Assert.Equal("TextBlock", effectiveSummary.Name.LocalName);
        Assert.Contains(effectiveSummary, motionScroll.Descendants());
        Assert.True(motionTask.Elements().ToList().IndexOf(motionScroll) < motionTask.Elements().ToList().IndexOf(actionGrid));
        var updateAxisLabels = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "UpdateMotorAxisLabels");
        Assert.Contains("if (MotorAxisComboBox is null)", updateAxisLabels, StringComparison.Ordinal);
        Assert.Contains("ViewModel.Motor1RoleText, ViewModel.Motor2RoleText, ViewModel.Motor3RoleText", updateAxisLabels, StringComparison.Ordinal);
        Assert.Contains("item.Content = string.IsNullOrWhiteSpace(roles[index]) ? name : $\"{name} · {roles[index]}\";", updateAxisLabels, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(item, item.Content.ToString());", updateAxisLabels, StringComparison.Ordinal);
        Assert.Contains("EnqueueForCurrentActivation(UpdateMotorAxisLabels);", code, StringComparison.Ordinal);

        var run = Named("WorkbenchRunBar");
        Assert.Contains(run.Descendants(), element =>
            (string?)element.Attribute("Command") == "{x:Bind ViewModel.StopAllMotorsCommand}");
    }

    [Fact]
    public void Stage01V_ConfigurationReturnRestoresPriorTaskEvenIfItsSelectionHasNotChanged()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var returnButton = Assert.Single(document.Descendants(), element =>
            (string?)element.Attribute(x + "Name") == "ReturnToWorkbenchTaskButton");
        var code = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var returnAction = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "ReturnToWorkbenchTaskButton_Click");
        var changeSection = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(code, "SetActiveWorkbenchSection");

        Assert.Equal("Button", returnButton.Name.LocalName);
        Assert.Contains(returnButton, document.Descendants().Single(element => (string?)element.Attribute(x + "Name") == "WorkbenchAuxiliaryHeader").Descendants());
        Assert.Null(returnButton.Attribute("Style"));
        Assert.Equal("ReturnToWorkbenchTaskButton", (string?)returnButton.Attribute("AutomationProperties.AutomationId"));
        Assert.Equal("ScanDebug_ReturnToWorkbenchTaskButton", (string?)returnButton.Attribute(x + "Uid"));
        Assert.Equal("ReturnToWorkbenchTaskButton_Click", (string?)returnButton.Attribute("Click"));
        Assert.Contains("ReturnToWorkbenchTaskButton.Visibility = ToVisibility(_isAuxiliaryPanelOpen);", code, StringComparison.Ordinal);
        Assert.Contains("if ((!_isAuxiliaryPanelOpen || index == 2) && Array.IndexOf(WorkbenchTaskSectionIndices, index) >= 0)", changeSection, StringComparison.Ordinal);
        Assert.Contains("_isAuxiliaryPanelOpen = false;", returnAction, StringComparison.Ordinal);
        Assert.Contains("SetActiveWorkbenchSection(_lastWorkbenchTaskSectionIndex);", returnAction, StringComparison.Ordinal);
        Assert.Contains("GetActiveWorkbenchTaskButton().Focus(FocusState.Programmatic)", returnAction, StringComparison.Ordinal);
        Assert.Contains("? WorkbenchTaskComboBox : WorkbenchTaskSelectorBar.SelectedItem ?? (Control)WorkbenchTaskSelectorBar", code, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.", returnAction, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", returnAction, StringComparison.Ordinal);
        var english = FilmProfileContractSource.ReadResources("en-us");
        var chinese = FilmProfileContractSource.ReadResources("zh-CN");
        Assert.Equal("← Back to task", english["ScanDebug_ReturnToWorkbenchTaskButton.Content"]);
        Assert.Equal("← 返回任务", chinese["ScanDebug_ReturnToWorkbenchTaskButton.Content"]);
    }

    [Fact]
    public void ObservationEvidenceStaysInsidePreviewAndInspectionKeepsDeviceEvidence()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var inspection = Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == "WorkbenchInspectionRail");
        var evidence = Assert.Single(inspection.Descendants(), element =>
            (string?)element.Attribute("AutomationProperties.AutomationId") == "InspectionDiagnosticsPanel");
        Assert.Single(inspection.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.DoesNotContain(evidence.Ancestors(), element => element.Name.LocalName is "Flyout" or "Expander" or "Button");
        Assert.DoesNotContain(inspection.Descendants(), element =>
            (string?)element.Attribute("Text") == "{x:Bind ViewModel.FilmProfileHardwareUnavailableReasonText, Mode=OneWay}"
            || (string?)element.Attribute("Text") == "{x:Bind ViewModel.ObservationSourceSummaryText, Mode=OneWay}");
        var comparison = Assert.Single(document.Descendants(), element =>
            (string?)element.Attribute("AutomationProperties.AutomationId") == "CaptureComparisonText");

        var context = document.Descendants().Single(element => (string?)element.Attribute(x + "Name") == "ObservationContextPanel");
        Assert.Contains(comparison, context.Descendants());
        Assert.Equal("0", (string?)context.Attribute("Grid.Row"));
        Assert.Contains(context.Parent!.Elements(), element => (string?)element.Attribute(x + "Name") == "PreviewScrollViewer"
            && (string?)element.Attribute("Grid.Row") == "1");
        Assert.Contains(context.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.ObservationEvidenceStatusText, Mode=OneWay}");
        Assert.Contains(context.Descendants(), element => (string?)element.Attribute("Visibility") == "{x:Bind ViewModel.ObservationEvidenceSourceVisibility, Mode=OneWay}");
        Assert.DoesNotContain(comparison, inspection.Descendants());
        Assert.Equal("{x:Bind ViewModel.ObservationComparisonText, Mode=OneWay}", (string?)context.Attribute("ToolTipService.ToolTip"));
        Assert.Equal("{x:Bind ViewModel.ObservationEvidenceHintText, Mode=OneWay}", (string?)comparison.Attribute("Text"));
        Assert.Equal((string?)comparison.Attribute("Text"), (string?)comparison.Attribute("AutomationProperties.Name"));
        Assert.Equal("WrapWholeWords", (string?)comparison.Attribute("TextWrapping"));
        Assert.Null(comparison.Attribute("MaxLines"));
        Assert.Null(comparison.Attribute("TextTrimming"));
        Assert.Contains(evidence.Descendants(), element =>
            (string?)element.Attribute("AutomationProperties.AutomationId") == "DeviceEvidenceText");
    }

    [Fact]
    public void Stage01V_ChannelStatusListOffersDirectSelectionWithoutIssuingHardwareCommands()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var list = GetOpeningTag(xaml, "CalibrationChannelStatusListView");
        var selector = GetOpeningTag(xaml, "CalibrationChannelFallbackComboBox");
        var english = FilmProfileContractSource.ReadResources("en-us");
        var chinese = FilmProfileContractSource.ReadResources("zh-CN");

        Assert.Contains("SelectionMode=\"None\"", list, StringComparison.Ordinal);
        Assert.Contains("IsTabStop=\"True\"", list, StringComparison.Ordinal);
        Assert.Contains("IsItemClickEnabled=\"True\"", list, StringComparison.Ordinal);
        Assert.Contains("ItemClick=\"CalibrationChannelStatusListView_ItemClick\"", list, StringComparison.Ordinal);
        var select = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(
            ReadAppSource("Views", "ScanDebugPage.UiRefinement.cs"), "CalibrationChannelStatusListView_ItemClick");
        Assert.Contains("await ViewModel.TrySelectCalibrationChannelAsync(channel.Role);", select, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", select, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyParameters", select, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{x:Bind ViewModel.SelectedCalibrationChannel, Mode=OneWay}\"", selector, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"CalibrationChannelFallbackComboBox_SelectionChanged\"", selector, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"Collapsed\"", selector, StringComparison.Ordinal);
        var directSelector = GetOpeningTag(xaml, "CalibrationChannelSelectorBar");
        Assert.DoesNotContain("Visibility=\"Collapsed\"", directSelector, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"CalibrationChannelSelectorBar_SelectionChanged\"", directSelector, StringComparison.Ordinal);
        Assert.Equal("Select a channel to edit its parameters.",
            english["ScanDebug_ChannelCalibrationStatusListHelpText.Text"]);
        Assert.Equal("选择通道以编辑参数。",
            chinese["ScanDebug_ChannelCalibrationStatusListHelpText.Text"]);
        Assert.Equal("Move direction", english["ScanDebug_MotorMoveDirectionComboBox.Header"]);
        Assert.Equal("移动方向", chinese["ScanDebug_MotorMoveDirectionComboBox.Header"]);
    }

    [Fact]
    public void ScanDebugViewModel_JsonCommandsUseWorkspaceAndFileCoordinator()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");

        Assert.Contains("private async Task SaveFilmProfileJson()", source, StringComparison.Ordinal);
        Assert.Contains("private async Task LoadFilmProfileJson()", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.BuildExportDocument", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.MarkExported", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.StageImport", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.ApplyStagedImportAsync", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileFiles.ExportAsync", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileFiles.ImportAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugViewModel_InputValidationUsesReviewWhileFileFailuresKeepOperationFeedback()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var validate = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "ValidateFilmProfile");
        var save = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "SaveFilmProfileJson");
        Assert.Contains("public event EventHandler? FilmProfileValidationReviewRequested;", source, StringComparison.Ordinal);
        Assert.Contains("SetCurrentFilmProfileValidation(validation);", validate, StringComparison.Ordinal);
        Assert.Contains("FilmProfileValidationReviewRequested?.Invoke(this, EventArgs.Empty);", validate, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishFilmProfileOperation", validate, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_Runtime_StatusSaveFilmProfileFailed", save, StringComparison.Ordinal);
        Assert.Contains("InfoBarSeverity.Error", save, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugChannelParameterInputs_UpdateBeforeCalibrationActions()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");

        var requiredBindings = new[]
        {
            "Text=\"{x:Bind ViewModel.SysClockMhz, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.ExposureMicroseconds, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.ExposureTicks, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.Adc1Offset, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.Adc1Gain, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.Adc2Offset, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.Adc2Gain, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\""
        };

        foreach (var binding in requiredBindings)
            Assert.Contains(binding, xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo8Baseline_ScanDebugHostLifecyclePreviewCalibrationAndCommandsRemainCharacterizedBeforeShellMigration()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs") + ReadAppSource("Views", "ScanDebugPage.CandidateReview.cs");
        var viewModel = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");

        Assert.Contains("public sealed partial class ScanDebugPage : Page, IPageViewModelHost<ScanDebugViewModel>", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ViewModel = App.GetService<ScanDebugViewModel>();", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Loaded += OnLoaded;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Unloaded += OnUnloaded;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ViewModel.PropertyChanged += OnViewModelPropertyChanged;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ViewModel.PropertyChanged -= OnViewModelPropertyChanged;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ViewModel.CalibrationPromptRequested += OnCalibrationPromptRequested;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ViewModel.NoticeRequested += OnNoticeRequested;", codeBehind, StringComparison.Ordinal);

        foreach (var name in new[]
        {
            "CurrentCalibrationIlluminationEditorGrid",
            "CurrentCalibrationIlluminationLevelTextBox",
            "CurrentCalibrationIlluminationPulseClockTextBox",
            "CurrentCalibrationIlluminationWorkModeComboBox",
            "PreviewScrollViewer",
            "PreviewCanvas",
            "PreviewCanvasControl",
            "RoiCanvas",
            "AxisCanvas",
            "PreviewEmptyStateGrid",
            "CursorPositionTextBlock",
            "CursorIntensityTextBlock"
        })
        {
            Assert.Contains($"x:Name=\"{name}\"", xaml, StringComparison.Ordinal);
        }

        foreach (var command in ExpectedCommandBindingCounts.Keys)
        {
            Assert.Contains($"ViewModel.{command}", xaml, StringComparison.Ordinal);
        }

        Assert.Contains("IsEnabled=\"{x:Bind ViewModel.CanRunAutoFocusAction, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"AutoFocusButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private void AutoFocusButton_Click", codeBehind, StringComparison.Ordinal);
        AssertCurrentRuntimeGatedCommandInventory(xaml, codeBehind, viewModel);

        Assert.Contains("public void AttachRuntimeBindings()", viewModel, StringComparison.Ordinal);
        Assert.Contains("public async Task DeactivateAsync()", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void PropertiesSelector_ContainsOutputDeviceAndEngineeringInsideTheEditor()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
        var selector = Named("WorkbenchSectionSelectorBar");
        Assert.Contains(selector, Named("WorkbenchAuxiliaryHeader").Descendants());
        Assert.Equal(3, selector.Elements().Count());
        Assert.Equal(4, Named("WorkbenchTaskSelectorBar").Elements().Count());
        Assert.DoesNotContain(document.Descendants(), element => (string?)element.Attribute(x + "Name") == "WorkbenchSectionComboBox");
        var english = FilmProfileContractSource.ReadResources("en-us");
        var chinese = FilmProfileContractSource.ReadResources("zh-CN");
        foreach (var (uid, en, zh) in new[]
        {
            ("ScanDebug_PropertiesGeneralTab", "Output & color", "输出与色彩"),
            ("ScanDebug_PropertiesDeviceTab", "Device", "设备设置"),
            ("ScanDebug_PropertiesEngineeringTab", "Engineering tools", "工程工具")
        })
        {
            Assert.Single(selector.Elements(), element => (string?)element.Attribute(x + "Uid") == uid);
            Assert.Equal(en, english[$"{uid}.Text"]);
            Assert.Equal(zh, chinese[$"{uid}.Text"]);
        }
    }

    [Fact]
    public void Todo8SourceContract_WorkbenchSectionRootsAreExclusiveAndEngineeringToolsStaySeparated()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");

        foreach (var root in new[]
        {
            "BasicInfoSection",
            "AcquisitionPlanSection",
            "ChannelCalibrationSection",
            "LiveCalibrationSection",
            "DeviceSettingsSection",
            "EngineeringToolsSection"
        })
        {
            var openingTag = GetOpeningTag(xaml, root);
            Assert.Contains($"x:Name=\"{root}\"", openingTag, StringComparison.Ordinal);
            Assert.Contains($"AutomationProperties.AutomationId=\"{root}\"", openingTag, StringComparison.Ordinal);
        }

        Assert.True(xaml.IndexOf("x:Name=\"BasicInfoSection\"", StringComparison.Ordinal) < xaml.IndexOf("AutomationProperties.AutomationId=\"BasicInfoSection\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("AutomationProperties.AutomationId=\"BasicInfoSection\"", StringComparison.Ordinal) < xaml.IndexOf("Visibility=\"Collapsed\"", xaml.IndexOf("x:Name=\"BasicInfoSection\"", StringComparison.Ordinal), StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"AcquisitionPlanSection\"", StringComparison.Ordinal) < xaml.IndexOf("AutomationProperties.AutomationId=\"AcquisitionPlanSection\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("AutomationProperties.AutomationId=\"AcquisitionPlanSection\"", StringComparison.Ordinal) < xaml.IndexOf("Visibility=\"Collapsed\"", xaml.IndexOf("x:Name=\"AcquisitionPlanSection\"", StringComparison.Ordinal), StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"ChannelCalibrationSection\"", StringComparison.Ordinal) < xaml.IndexOf("AutomationProperties.AutomationId=\"ChannelCalibrationSection\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("AutomationProperties.AutomationId=\"ChannelCalibrationSection\"", StringComparison.Ordinal) < xaml.IndexOf("Visibility=\"Collapsed\"", xaml.IndexOf("x:Name=\"ChannelCalibrationSection\"", StringComparison.Ordinal), StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"LiveCalibrationSection\"", StringComparison.Ordinal) < xaml.IndexOf("AutomationProperties.AutomationId=\"LiveCalibrationSection\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("AutomationProperties.AutomationId=\"LiveCalibrationSection\"", StringComparison.Ordinal) < xaml.IndexOf("Visibility=\"Collapsed\"", xaml.IndexOf("x:Name=\"LiveCalibrationSection\"", StringComparison.Ordinal), StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"DeviceSettingsSection\"", StringComparison.Ordinal) < xaml.IndexOf("AutomationProperties.AutomationId=\"DeviceSettingsSection\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("AutomationProperties.AutomationId=\"DeviceSettingsSection\"", StringComparison.Ordinal) < xaml.IndexOf("Visibility=\"Collapsed\"", xaml.IndexOf("x:Name=\"DeviceSettingsSection\"", StringComparison.Ordinal), StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"AcquisitionPlanSection\"", StringComparison.Ordinal) < xaml.IndexOf("x:Uid=\"ScanDebug_FilmProfileWorkbenchAcquisitionSetupTitle\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"DeviceSettingsSection\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"FocusMappingContent\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"FocusMappingContent\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"EngineeringToolsSection\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"EngineeringToolsSection\"", StringComparison.Ordinal) < xaml.IndexOf("x:Uid=\"ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugDescription\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"EngineeringToolsSection\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"MotionContent\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"PreviewScrollViewer\"", StringComparison.Ordinal) > xaml.IndexOf("</ScrollViewer>", xaml.IndexOf("x:Name=\"EngineeringToolsSection\"", StringComparison.Ordinal), StringComparison.Ordinal));
    }

    [Fact]
    public void Stage01SourceContract_TaskSelectionAndLegacySectionsUseOneGuardedLayoutOwner()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs") + ReadAppSource("Views", "ScanDebugPage.CandidateReview.cs");
        var mainWindow = ReadAppSource("MainWindow.xaml");

        Assert.Contains("private bool _isSynchronizingWorkbenchSection;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_isAuxiliaryPanelOpen = false;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_isAuxiliaryPanelOpen = true;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"WorkbenchTaskSelectorBar_SelectionChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"WorkbenchTaskComboBox_SelectionChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OpenConfigurationButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PropertiesEngineeringTab\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"OpenAdvancedButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchPreviewSplitMinimumWidth", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private int _activeWorkbenchSectionIndex = 1;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private bool _isNarrowPreviewOpen;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private double _workbenchPreviewEditorRatio = double.NaN;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SizeChanged=\"ScanDebugRootGrid_SizeChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private void ScanDebugRootGrid_SizeChanged", codeBehind, StringComparison.Ordinal);
        Assert.Contains("EnqueueForCurrentActivation(UpdateWorkbenchPreviewLayout);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private void SetActiveWorkbenchSection(int index)", codeBehind, StringComparison.Ordinal);
        Assert.True(CountOccurrences(codeBehind, "SetActiveWorkbenchSection(") >= 5);
        Assert.Contains("if (_isSynchronizingWorkbenchSection || !IsWorkbenchSectionUiReady())", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingWorkbenchSection = true;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingWorkbenchSection = false;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("if ((uint)index >= 6)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SynchronizeWorkbenchSectionSelectors(_activeWorkbenchSectionIndex);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSectionSelectorBar.SelectedItem", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private static readonly int[] WorkbenchPropertySectionIndices = [0, 4, 5];", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchTaskSelectorBar.SelectedItem", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchTaskComboBox.SelectedIndex", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_lastWorkbenchTaskSectionIndex = index;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("DeviceSettingsSection.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("EngineeringToolsSection.Visibility = index == 5 && _isAuxiliaryPanelOpen ? Visibility.Visible : Visibility.Collapsed;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("MotionTaskSection.Visibility = index == 5 && !_isAuxiliaryPanelOpen ? Visibility.Visible : Visibility.Collapsed;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SetActiveWorkbenchSection(_lastWorkbenchTaskSectionIndex);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("DeviceSettingsSection is not null", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ManualFocusNegativeButton.ReleasePointerCaptures();", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_inspectionOpenOverride = false;", codeBehind, StringComparison.Ordinal);

        Assert.DoesNotContain("<AdaptiveTrigger", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<VisualStateManager.VisualStateGroups>", xaml, StringComparison.Ordinal);
        Assert.Contains("ScanWorkbenchPreviewLayout.Calculate", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ScanWorkbenchPreviewLayout.ResizeWideImageSplit", codeBehind, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"900\"", mainWindow, StringComparison.Ordinal);
        Assert.Contains("<RowDefinition Height=\"*\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"3\"", xaml, StringComparison.Ordinal);
        var normalizedXaml = xaml.Replace("\r\n", "\n", StringComparison.Ordinal);
        var splitGridStart = normalizedXaml.IndexOf("x:Name=\"WorkbenchContentSplitGrid\"", StringComparison.Ordinal);
        Assert.True(splitGridStart >= 0, "The task/preview/inspection split must be hosted by the bounded work row.");
        var categories = GetNamedElement(xaml, "WorkbenchSectionSelectorBar");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        Assert.Contains(categories.Ancestors(), element => (string?)element.Attribute(x + "Name") == "WorkbenchAuxiliaryHeader");
        Assert.Contains(categories.Ancestors(), element => (string?)element.Attribute(x + "Name") == "WorkbenchWorkspaceHeader");
        Assert.DoesNotContain(categories.Ancestors(), element => element.Name.LocalName is "ScrollViewer" or "Flyout");
        Assert.Contains("WorkbenchAuxiliaryHeader.Visibility = ToVisibility(_isAuxiliaryPanelOpen);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSectionSelectorBar.Visibility = ToVisibility(isPropertiesPanelOpen);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchTaskSelectorBar.Visibility = ToVisibility(isTaskSelectorBarAvailable);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchTaskComboBox.Visibility = ToVisibility(!_isAuxiliaryPanelOpen && !isTaskSelectorBarAvailable);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchInspectionRail.Visibility = ToVisibility(layout.IsInspectionVisible);", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("<NavigationView", xaml[xaml.IndexOf("Grid.Row=\"1\"", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.DoesNotContain("<TabView", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo22SourceContract_OneLayoutOwnerKeepsPreviewReachableAndResizable()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs") + ReadAppSource("Views", "ScanDebugPage.CandidateReview.cs");

        Assert.Contains("x:Name=\"WorkbenchContentSplitGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchEditorColumn\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchPreviewSeparatorColumn\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchPreviewColumn\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchPreviewColumnContent\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"PreviewToggleButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"BackToEditorButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"PreviewSplitter\"", xaml, StringComparison.Ordinal);
        Assert.Contains("DragDelta=\"PreviewSplitter_DragDelta\"", xaml, StringComparison.Ordinal);
        Assert.Contains("KeyDown=\"PreviewSplitter_KeyDown\"", xaml, StringComparison.Ordinal);

        Assert.Contains("private void UpdateWorkbenchPreviewLayout(double availableWidth)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("new ScanWorkbenchPreviewLayoutInput(", codeBehind, StringComparison.Ordinal);
        Assert.Contains("var hasImage = HasValidPreviewFrame();", codeBehind, StringComparison.Ordinal);
        Assert.Contains("availableWidth, hasImage, _isNarrowPreviewOpen,", codeBehind, StringComparison.Ordinal);
        Assert.Contains("IsReviewOpen: reviewVisible", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchValidationReviewColumn.Width = new GridLength(layout.ReviewWidth + layout.ReviewGapWidth);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("FilmProfileLifecycleDetailsScrollViewer.Width = layout.ReviewWidth;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("FilmProfileLifecycleDetailsScrollViewer.Margin = new Thickness(layout.ReviewGapWidth, 0, 0, 0);", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("reviewReservedWidth", codeBehind, StringComparison.Ordinal);
        Assert.Contains("IsPreviewCollapsed: _previewCollapsedOverride ?? false", codeBehind, StringComparison.Ordinal);
        Assert.Contains("HasImage: HasValidPreviewFrame()", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_isAuxiliaryPanelOpen ? _auxiliaryPanelEditorRatio : _workbenchPreviewEditorRatio,", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("inspectionOpen, _isAuxiliaryPanelOpen", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchEditorRow.Height = new GridLength(1, GridUnitType.Star);", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchPreviewRow", xaml + codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("SetWorkbenchContentSplitMode", codeBehind, StringComparison.Ordinal);
        Assert.Contains("var previewActionLabel = layout.IsPreviewVisible ? _hidePreviewActionLabel : _openPreviewActionLabel;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("PreviewToggleButton.Content = previewActionLabel;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(PreviewToggleButton, previewActionLabel);", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewToggleButton.Visibility = ToVisibility", codeBehind, StringComparison.Ordinal);
        var openPreview = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(codeBehind, "OpenPreviewButton_Click");
        Assert.Contains("_isNarrowPreviewOpen = true;", openPreview, StringComparison.Ordinal);
        Assert.Contains("_previewCollapsedOverride = false;", openPreview, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", openPreview, StringComparison.Ordinal);
        Assert.Contains("BackToEditorButton.Visibility = ToVisibility(layout.IsBackToEditorButtonVisible);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private static readonly int[] WorkbenchTaskSectionIndices = [1, 2, 3, 5];", codeBehind, StringComparison.Ordinal);
        Assert.Contains("? WorkbenchTaskComboBox : WorkbenchTaskSelectorBar.SelectedItem ?? (Control)WorkbenchTaskSelectorBar", codeBehind, StringComparison.Ordinal);
        Assert.Contains("PreviewSplitter.Visibility = ToVisibility(layout.IsSplitterVisible);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchInspectionColumn.Width = new GridLength(layout.InspectionWidth);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchInspectionSeparatorColumn.Width = new GridLength(layout.InspectionGapWidth);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchInspectionRail\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"InspectionToggleButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchAcquisitionSetupDescription\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugDescription\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.ComputedMotorSummaryText, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo8RuntimeContract_WorkbenchSelectionEventsCannotTouchSectionRootsDuringXamlInitialization()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs") + ReadAppSource("Views", "ScanDebugPage.CandidateReview.cs");

        Assert.True(
            xaml.IndexOf("x:Name=\"WorkbenchSectionSelectorBar\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"BasicInfoSection\"", StringComparison.Ordinal),
            "Selector controls are declared before section roots, so initialization-time selection events must be guarded.");
        Assert.Contains("private bool IsWorkbenchSectionUiReady()", codeBehind, StringComparison.Ordinal);
        Assert.Contains("if (_isSynchronizingWorkbenchSection || !IsWorkbenchSectionUiReady())", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SetActiveWorkbenchSection(_activeWorkbenchSectionIndex);", codeBehind, StringComparison.Ordinal);
        Assert.True(
            codeBehind.IndexOf("SetActiveWorkbenchSection(_activeWorkbenchSectionIndex);", StringComparison.Ordinal) < codeBehind.IndexOf("InitializeCurrentCalibrationIlluminationEditor();", StringComparison.Ordinal),
            "The post-InitializeComponent synchronization must run before legacy editor initialization.");
    }

    [Fact]
    public void Todo9Baseline_CurrentProfileBindingsAndInventoryRemainCharacterizedBeforeBasicEditorMigration()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");

        foreach (var binding in new[]
        {
            "public partial string FilmProfileName",
            "public partial bool HasUnsavedProfileChanges",
            "public partial IReadOnlyList<ScanFilmProfileValidationIssue> CurrentFilmProfileValidationIssues",
            "public string CurrentFilmProfileValidationSummary",
            "public ObservableCollection<ScanDngExportMode> DngExportModeOptions",
            "public ObservableCollection<ScanChannelAlignmentMode> AlignmentModeOptions",
            "public partial bool IsScanRecipeColorManagementEnabled",
            "public partial string ScanRecipeRedWavelengthNm",
            "public partial string ScanRecipeGreenWavelengthNm",
            "public partial string ScanRecipeBlueWavelengthNm",
            "public partial string ScanRecipeOutputGamma",
            "public partial string SelectedScanRecipeTargetWhitePointMode",
            "public partial string ScanRecipeManualWhitePointColorTemperatureK",
            "public partial ScanChannelAlignmentMode SelectedProfileAlignmentMode",
            "public partial ScanDngExportMode SelectedProfileDngExportMode"
        })
        {
            Assert.Contains(binding, source, StringComparison.Ordinal);
        }

        Assert.Equal(ExpectedNamedElements.Order(StringComparer.Ordinal), FilmProfileContractSource.GetXamlNames(xaml));
        Assert.Contains("x:Name=\"BasicInfoSection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{x:Bind ViewModel.SelectedDebugDngExportMode, Mode=TwoWay}\"", ExtractNamedRegion(xaml, "WorkbenchPreviewColumnContent", "PreviewScrollViewer"), StringComparison.Ordinal);
    }

    [Fact]
    public void Todo9SourceContract_BasicInfoSectionOwnsOnlyProfileAndRecipeEditingSurface()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var basic = ExtractNamedRegion(xaml, "BasicInfoSection", "AcquisitionPlanSection");

        foreach (var uid in new[]
        {
            "ScanDebug_FilmProfileWorkbenchBasicMetadataTitle",
            "ScanDebug_FilmProfileWorkbenchBasicRecipeTitle",
            "ScanDebug_FilmProfileWorkbenchBasicValidationTitle",
            "ScanDebug_FilmProfileWorkbenchBasicValidationDescription",
            "ScanDebug_ProfileAlignmentModeComboBox",
            "ScanDebug_ProfileDngExportModeComboBox",
            "ScanDebug_FilmProfileWorkbenchBasicColorManagementToggleSwitch",
            "ScanDebug_FilmProfileWorkbenchBasicRedWavelengthTextBox",
            "ScanDebug_FilmProfileWorkbenchBasicGreenWavelengthTextBox",
            "ScanDebug_FilmProfileWorkbenchBasicBlueWavelengthTextBox",
            "ScanDebug_FilmProfileWorkbenchBasicOutputGammaTextBox",
            "ScanDebug_FilmProfileWorkbenchBasicTargetWhitePointComboBox",
            "ScanDebug_FilmProfileWorkbenchBasicManualWhitePointColorTemperatureTextBox"
        })
        {
            Assert.Contains($"x:Uid=\"{uid}\"", basic, StringComparison.Ordinal);
        }

        foreach (var binding in new[]
        {
            "ItemsSource=\"{x:Bind ViewModel.AlignmentModeOptions, Mode=OneWay}\"",
            "SelectedItem=\"{x:Bind ViewModel.SelectedProfileAlignmentMode, Mode=TwoWay}\"",
            "ItemsSource=\"{x:Bind ViewModel.DngExportModeOptions, Mode=OneWay}\"",
            "SelectedItem=\"{x:Bind ViewModel.SelectedProfileDngExportMode, Mode=TwoWay}\"",
            "ItemTemplate=\"{StaticResource ProfileDngExportModeOptionTemplate}\"",
            "ToolTipService.ToolTip=\"{x:Bind ViewModel.SelectedProfileDngExportModeAccessibleText, Mode=OneWay}\"",
            "IsOn=\"{x:Bind ViewModel.IsScanRecipeColorManagementEnabled, Mode=TwoWay}\"",
            "Text=\"{x:Bind ViewModel.ScanRecipeRedWavelengthNm, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.ScanRecipeGreenWavelengthNm, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.ScanRecipeBlueWavelengthNm, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.ScanRecipeOutputGamma, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "SelectedValue=\"{x:Bind ViewModel.SelectedScanRecipeTargetWhitePointMode, Mode=TwoWay}\"",
            "Text=\"{x:Bind ViewModel.ScanRecipeManualWhitePointColorTemperatureK, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Message=\"{x:Bind ViewModel.BasicCurrentFilmProfileValidationIssueCountText, Mode=OneWay}\"",
            "ItemsSource=\"{x:Bind ViewModel.BasicCurrentFilmProfileValidationIssueDisplays, Mode=OneWay}\"",
            "Severity=\"{x:Bind ViewModel.BasicCurrentFilmProfileValidationSeverity, Mode=OneWay}\""
        })
        {
            Assert.Contains(binding, basic, StringComparison.Ordinal);
        }

        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", basic, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource ScanDebugSectionCardStyle}\"", basic, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"WrapWholeWords\"", basic, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"Wrap\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Margin=\"0,0,0,96\"", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("ProfileNameTextBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.FilmProfileName, Mode=TwoWay", xaml, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{x:Bind ViewModel.IsScanRecipeColorManagementEnabled, Mode=OneWay}\"", basic, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{x:Bind ViewModel.IsScanRecipeManualWhitePointColorTemperatureEnabled, Mode=OneWay}\"", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedDebugDngExportMode", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("DebugDngExportModes", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportDngCommand", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("StartScanCommand", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("StopScanCommand", basic, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo23SourceContract_ValidationInspectorUsesCurrentAndStagedIssueDisplaysAndNativeFocusBridge()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs") + ReadAppSource("Views", "ScanDebugPage.CandidateReview.cs");
        var viewModel = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var currentInspector = GetNamedElement(xaml, "CurrentFilmProfileValidationInspector").ToString();
        var stagedReview = GetNamedElement(xaml, "StagedFilmProfileImportReview").ToString();
        var basic = ExtractNamedRegion(xaml, "BasicInfoSection", "AcquisitionPlanSection");
        var acquisition = ExtractNamedRegion(xaml, "AcquisitionPlanSection", "ChannelCalibrationSection");
        var channel = ExtractNamedRegion(xaml, "ChannelCalibrationSection", "LiveCalibrationSection");
        var engineering = ExtractNamedRegion(xaml, "EngineeringToolsSection", "WorkbenchPreviewColumnContent");

        Assert.DoesNotContain("MaxHeight=", GetOpeningTag(xaml, "FilmProfileLifecycleHeader"), StringComparison.Ordinal);
        Assert.Null(GetNamedElement(xaml, "FilmProfileLifecycleDetailsScrollViewer").Attribute("MaxHeight"));

        foreach (var required in new[]
        {
            "x:Name=\"CurrentFilmProfileValidationInspector\"",
            "AutomationProperties.AutomationId=\"CurrentFilmProfileValidationInspector\"",
            "x:Uid=\"ScanDebug_FilmProfileWorkbenchCurrentValidationTextTitle\"",
            "Message=\"{x:Bind ViewModel.CurrentFilmProfileValidationIssueCountText, Mode=OneWay}\"",
            "Severity=\"{x:Bind ViewModel.CurrentFilmProfileValidationSeverity, Mode=OneWay}\"",
            "x:Name=\"CurrentFilmProfileValidationIssues\"",
            "ItemTemplateSelector=\"{StaticResource FilmProfileValidationIssueTemplateSelector}\"",
            "ItemsSource=\"{x:Bind ViewModel.CurrentFilmProfileValidationIssueDisplays, Mode=OneWay}\""
        })
        {
            Assert.Contains(required, currentInspector, StringComparison.Ordinal);
        }

        foreach (var required in new[]
        {
            "Command=\"{Binding DataContext.NavigateToCurrentFilmProfileValidationIssueCommand, ElementName=ScanDebugRootGrid}\"",
            "CommandParameter=\"{Binding}\"",
            "AutomationProperties.AutomationId=\"{Binding AutomationId}\"",
            "AutomationProperties.Name=\"{Binding AutomationName}\"",
            "Text=\"{Binding Message}\"",
            "Text=\"{Binding FieldDisplayName}\"",
            "ToolTipService.ToolTip=\"{Binding FieldPath}\"",
            "x:Uid=\"ScanDebug_ValidationLocateAction\""
        })
        {
            Assert.Contains(required, xaml, StringComparison.Ordinal);
        }

        foreach (var required in new[]
        {
            "Message=\"{x:Bind ViewModel.StagedFilmProfileImportValidationIssueCountText, Mode=OneWay}\"",
            "x:Name=\"StagedFilmProfileImportValidationIssues\"",
            "ItemsSource=\"{x:Bind ViewModel.StagedFilmProfileImportValidationIssueDisplays, Mode=OneWay}\"",
            "ItemTemplate=\"{StaticResource FilmProfilePassiveValidationIssueTemplate}\""
        })
        {
            Assert.Contains(required, stagedReview, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("StagedFilmProfileImportValidationSummary", stagedReview, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigateToCurrentFilmProfileValidationIssueCommand", stagedReview, StringComparison.Ordinal);

        Assert.Contains("x:Name=\"BasicCurrentFilmProfileValidationIssueScrollViewer\"", basic, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.BasicCurrentFilmProfileValidationIssueDisplays, Mode=OneWay}\"", basic, StringComparison.Ordinal);
        Assert.Contains("Message=\"{x:Bind ViewModel.BasicCurrentFilmProfileValidationIssueCountText, Mode=OneWay}\"", basic, StringComparison.Ordinal);
        Assert.Contains("Severity=\"{x:Bind ViewModel.BasicCurrentFilmProfileValidationSeverity, Mode=OneWay}\"", basic, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_BasicInformationValidationTitle\"", basic, StringComparison.Ordinal);
        Assert.Contains("MaxHeight=\"160\"", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{x:Bind ViewModel.BasicCurrentFilmProfileValidationHeadline, Mode=OneWay}\"", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{x:Bind ViewModel.CurrentFilmProfileValidationSummary, Mode=OneWay}\"", basic, StringComparison.Ordinal);

        Assert.DoesNotContain("AcquisitionCurrentFilmProfileValidationIssueScrollViewer", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentFilmProfileValidationIssueDisplays", acquisition, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.CurrentFilmProfileValidationIssueDisplays, Mode=OneWay}\"", currentInspector, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{x:Bind ViewModel.CurrentFilmProfileValidationSummary, Mode=OneWay}\"", acquisition, StringComparison.Ordinal);

        Assert.Contains("x:Name=\"ChannelCalibrationIssuePanel\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer\"", channel, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.ChannelCalibrationCurrentFilmProfileValidationIssueDisplays, Mode=OneWay}\"", channel, StringComparison.Ordinal);
        Assert.Contains("ItemTemplateSelector=\"{StaticResource FilmProfileBwIssueTemplateSelector}\"", channel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.BwValidationSummaryText, Mode=OneWay}\"", channel, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.ChannelCalibrationIssueEmptyVisibility, Mode=OneWay}\"", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"160\"", GetOpeningTag(channel, "ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer"), StringComparison.Ordinal);
        Assert.DoesNotContain("ChannelCalibrationCurrentFilmProfileValidationCard", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{x:Bind ViewModel.ChannelCalibrationCurrentFilmProfileValidationHeadline, Mode=OneWay}\"", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentFilmProfileValidationHeadline", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("FilmProfileValidationSummary", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"176\"", currentInspector, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"96\"", currentInspector, StringComparison.Ordinal);

        Assert.Contains("ViewModel.CurrentFilmProfileIssueNavigationRequested += OnCurrentFilmProfileIssueNavigationRequested;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ViewModel.CurrentFilmProfileIssueNavigationRequested -= OnCurrentFilmProfileIssueNavigationRequested;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private void OnCurrentFilmProfileIssueNavigationRequested(ScanFilmProfileIssueNavigationRequest request)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("TryResolveCurrentFilmProfileNavigationTarget(request, allowDeferredRealization: false, out var sectionIndex, out var scroller, out var target)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_isNarrowPreviewOpen = false;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SetActiveWorkbenchSection(sectionIndex);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ExitBwIssueView(returnFocus: false);", ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(codeBehind, "OnCurrentFilmProfileIssueNavigationRequested"), StringComparison.Ordinal);
        Assert.Contains("BeginIssueTargetFocus(sectionIndex, scroller, target, request.ChannelRole", codeBehind, StringComparison.Ordinal);
        Assert.Contains("IsCurrentFilmProfileNavigationStillValid()", codeBehind, StringComparison.Ordinal);
        Assert.Contains("target.Focus(FocusState.Programmatic)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("textBox.SelectAll();", codeBehind, StringComparison.Ordinal);
        Assert.Contains("FindFirstEligibleAssignmentCheckBox()", codeBehind, StringComparison.Ordinal);
        var deferredFocus = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(codeBehind, "TryCompleteIssueTargetFocus");
        Assert.True(deferredFocus.IndexOf("!IsCurrentFilmProfileNavigationStillValid()", StringComparison.Ordinal)
            < deferredFocus.IndexOf("target.StartBringIntoView(new BringIntoViewOptions", StringComparison.Ordinal)
            && deferredFocus.Contains("!IsCurrentFilmProfileNavigationStillValid()", StringComparison.Ordinal),
            "The deferred current-navigation lifecycle/import guard must run before revealing the target.");
        Assert.DoesNotContain("fallback ??= checkBox", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("target.ActualOffset.Y", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("target = AcquisitionChannelAssignmentList", codeBehind, StringComparison.Ordinal);

        var selectorStart = codeBehind.IndexOf("public sealed class FilmProfileValidationIssueTemplateSelector : DataTemplateSelector", StringComparison.Ordinal);
        Assert.True(selectorStart >= 0, "Missing FilmProfileValidationIssueTemplateSelector.");
        var selector = codeBehind[selectorStart..];
        Assert.Contains("protected override DataTemplate? SelectTemplateCore(object item)", selector, StringComparison.Ordinal);
        Assert.Contains("protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)", selector, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(selector, "=> SelectFilmProfileValidationIssueTemplate(item);"));
        Assert.Contains("private DataTemplate? SelectFilmProfileValidationIssueTemplate(object item)", selector, StringComparison.Ordinal);
        Assert.Contains("private static bool CanNavigateFilmProfileValidationIssue(object item)", selector, StringComparison.Ordinal);
        Assert.Contains("item is ScanFilmProfileValidationIssueDisplay { CanNavigate: true }", selector, StringComparison.Ordinal);
        Assert.DoesNotContain("XamlReader", selector, StringComparison.Ordinal);
        Assert.DoesNotContain("ToString", selector, StringComparison.Ordinal);

        foreach (var property in new[]
        {
            "public string CurrentFilmProfileValidationHeadline",
            "public string CurrentFilmProfileValidationIssueCountText",
            "public IReadOnlyList<ScanFilmProfileValidationIssueDisplay> CurrentFilmProfileValidationIssueDisplays",
            "public IReadOnlyList<ScanFilmProfileValidationIssueDisplay> StagedFilmProfileImportValidationIssueDisplays",
            "private async Task NavigateToCurrentFilmProfileValidationIssue(ScanFilmProfileValidationIssueDisplay display)"
        })
        {
            Assert.Contains(property, viewModel, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Stage01SourceContract_ConfigurationActionsAndMotorStopStayOutsideScrollableReview()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var document = XDocument.Parse(xaml);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
        var header = Named("FilmProfileLifecycleHeader");
        var runElement = Named("WorkbenchRunBar");
        var actions = Named("FilmProfileLifecycleActionsPanel");
        var details = Named("FilmProfileLifecycleDetailsScrollViewer");
        Assert.Contains(actions, header.Descendants());
        Assert.DoesNotContain(details, header.Descendants());
        Assert.DoesNotContain(actions.Ancestors(), element => element.Name.LocalName == "ScrollViewer");
        Assert.DoesNotContain(runElement.Ancestors(), element => element.Name.LocalName == "ScrollViewer");
        Assert.DoesNotContain("StopAllMotorsCommand", header.ToString(), StringComparison.Ordinal);

        var actionsRegion = actions.ToString();
        Assert.Contains("Command=\"{x:Bind ViewModel.NewFilmProfileCommand}\"", actionsRegion, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.LoadFilmProfileJsonCommand}\"", actionsRegion, StringComparison.Ordinal);
        var validation = Named("WorkbenchValidationButton");
        Assert.Equal("{x:Bind ViewModel.ValidateFilmProfileCommand}", (string?)validation.Attribute("Command"));
        Assert.Contains(validation, Named("WorkbenchProfileSummaryGrid").Descendants());
        Assert.Contains(validation, header.Descendants());
        Assert.DoesNotContain(validation, actions.Descendants());
        Assert.DoesNotContain(validation.Ancestors(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Contains("Command=\"{x:Bind ViewModel.SaveFilmProfileJsonCommand}\"", actionsRegion, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchReviewButton", actionsRegion, StringComparison.Ordinal);
        Assert.DoesNotContain("<ScrollViewer", actionsRegion, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(runElement.ToString(), "Command=\"{x:Bind ViewModel.StopAllMotorsCommand}\""));
        Assert.Equal(1, CountOccurrences(runElement.ToString(), "x:Uid=\"ScanDebug_StopAllMotorsButton\""));
        foreach (var command in new[] { "StopScanCommand", "StopAllMotorsCommand" })
        {
            var action = Assert.Single(document.Descendants(), element => (string?)element.Attribute("Command") == $"{{x:Bind ViewModel.{command}}}");
            Assert.Contains(action, runElement.Descendants());
            Assert.DoesNotContain(action.Ancestors(), element => element.Name.LocalName == "ScrollViewer");
        }
        var startActions = document.Descendants().Where(element =>
            (string?)element.Attribute("Command") == "{x:Bind ViewModel.StartScanCommand}").ToArray();
        Assert.Equal(2, startActions.Length);
        Assert.Contains(Named("StartScanActionButton"), startActions);
        Assert.Contains(Named("StartScanActionButton"), runElement.Descendants());
        var emptyStateStart = Assert.Single(Named("PreviewEmptyStateGrid").Descendants(), element =>
            (string?)element.Attribute("Command") == "{x:Bind ViewModel.StartScanCommand}");
        Assert.Contains(emptyStateStart, startActions);
        Assert.Equal("{x:Bind ViewModel.StartDisabledReasonText, Mode=OneWay}",
            (string?)emptyStateStart.Attribute("ToolTipService.ToolTip"));
        Assert.Null(emptyStateStart.Attribute("Click"));
        foreach (var startAction in startActions)
            Assert.DoesNotContain(startAction.Ancestors(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Contains(runElement.Descendants(), element => element.Name.LocalName == "ScrollViewer"
            && element.Descendants().Any(child => (string?)child.Attribute("ItemsSource") == "{x:Bind ViewModel.OperationNotices, Mode=OneWay}"));
    }

    [Fact]
    public void CurrentValidationViewport_UsesOneReadableScrollerAndOpensFromPrimaryCheck()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var header = ExtractNamedRegion(xaml, "FilmProfileLifecycleHeader", "WorkbenchSectionSelectorBar");
        var detailsScroll = GetOpeningTag(xaml, "FilmProfileLifecycleDetailsScrollViewer");
        var currentInspector = GetNamedElement(xaml, "CurrentFilmProfileValidationInspector").ToString();
        var document = XDocument.Parse(xaml);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
        var details = Named("FilmProfileLifecycleDetailsScrollViewer");
        var currentIssues = Named("CurrentFilmProfileValidationIssues");
        var stagedIssues = Named("StagedFilmProfileImportValidationIssues");

        Assert.Null(details.Attribute("MaxHeight"));
        Assert.Same(Named("WorkbenchContentSplitGrid"), details.Parent);
        Assert.Equal("0", (string?)details.Attribute("Grid.Row"));
        Assert.Equal("5", (string?)details.Attribute("Grid.Column"));
        Assert.Contains("Visibility=\"Collapsed\"", detailsScroll, StringComparison.Ordinal);
        Assert.Equal("240", Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Key") == "ScanDebugReviewMaximumHeight").Value);
        Assert.Equal("ItemsControl", currentIssues.Name.LocalName);
        Assert.Equal("ItemsControl", stagedIssues.Name.LocalName);
        Assert.DoesNotContain(details.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Same(details, Assert.Single(currentIssues.Ancestors(), element => element.Name.LocalName == "ScrollViewer"));
        Assert.Same(details, Assert.Single(stagedIssues.Ancestors(), element => element.Name.LocalName == "ScrollViewer"));
        Assert.DoesNotContain("ScanDebug_FilmProfileWorkbenchCurrentValidationDescription", currentInspector, StringComparison.Ordinal);
        var page = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        Assert.Contains("UpdateWorkbenchReviewEntry();", page, StringComparison.Ordinal);
        Assert.Contains("FilmProfileLifecycleDetailsScrollViewer.Visibility = Visibility.Visible;", page, StringComparison.Ordinal);
        var importReview = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "UpdateWorkbenchReviewEntry");
        Assert.Contains("ViewModel.FilmProfileImportResultReviewVisibility == Visibility.Visible", importReview, StringComparison.Ordinal);
        Assert.Contains("ShowFilmProfileReview();", importReview, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", detailsScroll, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", detailsScroll, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"WrapWholeWords\"", currentInspector, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding FieldDisplayName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"Wrap\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemTemplateSelector=\"{StaticResource FilmProfileValidationIssueTemplateSelector}\"", currentInspector, StringComparison.Ordinal);

        var validationButton = Named("WorkbenchValidationButton");
        Assert.Equal("Button", validationButton.Name.LocalName);
        Assert.Equal("{x:Bind ViewModel.ValidateFilmProfileCommand}", (string?)validationButton.Attribute("Command"));
        Assert.Null(validationButton.Attribute("Click"));
        Assert.Empty(validationButton.Elements());
        Assert.DoesNotContain(document.Descendants(), element =>
            (string?)element.Attribute(x + "Name") is "WorkbenchValidationFlyout" or "WorkbenchValidationMenu" or "WorkbenchReviewButton");
        var reviewRequestedStart = page.IndexOf("private void OnFilmProfileValidationReviewRequested(", StringComparison.Ordinal);
        Assert.True(reviewRequestedStart >= 0);
        var reviewRequestedEnd = page.IndexOf("private void OnCurrentFilmProfileIssueNavigationRequested(", reviewRequestedStart, StringComparison.Ordinal);
        Assert.True(reviewRequestedEnd > reviewRequestedStart);
        var reviewRequested = page[reviewRequestedStart..reviewRequestedEnd];
        Assert.Contains("EnqueueForCurrentActivation(ShowFilmProfileReview)", reviewRequested, StringComparison.Ordinal);
        Assert.Contains("ViewModel.FilmProfileValidationReviewRequested += OnFilmProfileValidationReviewRequested;", page, StringComparison.Ordinal);
        Assert.Contains("ViewModel.FilmProfileValidationReviewRequested -= OnFilmProfileValidationReviewRequested;", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateFilmProfileCommand.Execute", reviewRequested, StringComparison.Ordinal);

        Assert.DoesNotContain("MaxHeight=\"176\"", currentInspector, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=\"96\"", currentInspector, StringComparison.Ordinal);
        Assert.DoesNotContain("ScrollViewer MaxHeight=\"360\"", header, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo9VerifierFix_SaveCommandCanExecuteAndCurrentValidationSeverityAreBoundToCurrentValidity()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var save = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "SaveFilmProfileJson");
        var canSave = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "CanSaveFilmProfile");
        var synchronize = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "SynchronizeFilmProfileDraftFromInputs");
        var setCurrentValidation = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "SetCurrentFilmProfileValidation");

        Assert.Contains("[RelayCommand(CanExecute = nameof(CanSaveFilmProfile))]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("[RelayCommand(CanExecute = nameof(CanRunFilmProfileLifecycleOperation))]\r\n    private async Task SaveFilmProfileJson()", source, StringComparison.Ordinal);
        Assert.Contains("CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.SaveFilmProfileJson)", canSave, StringComparison.Ordinal);
        Assert.DoesNotContain("HasUnsavedProfileChanges", canSave, StringComparison.Ordinal);
        Assert.Contains("IsCurrentFilmProfileValidationValid", canSave, StringComparison.Ordinal);
        Assert.Contains("!_hasInvalidFilmProfileInput", canSave, StringComparison.Ordinal);
        Assert.Contains("SaveFilmProfileJsonCommand.NotifyCanExecuteChanged();", synchronize, StringComparison.Ordinal);
        Assert.Contains("SaveFilmProfileJsonCommand.NotifyCanExecuteChanged();", setCurrentValidation, StringComparison.Ordinal);
        Assert.True(save.IndexOf("SynchronizeFilmProfileDraftFromInputs", StringComparison.Ordinal) < save.IndexOf("BuildExportDocument", StringComparison.Ordinal));

        Assert.Contains("[NotifyCanExecuteChangedFor(nameof(SaveFilmProfileJsonCommand))]", source, StringComparison.Ordinal);
        Assert.Contains("[NotifyPropertyChangedFor(nameof(CurrentFilmProfileValidationSeverity))]", source, StringComparison.Ordinal);
        Assert.Contains("SetCurrentFilmProfileValidation(validation);", synchronize, StringComparison.Ordinal);
        Assert.Contains("public InfoBarSeverity CurrentFilmProfileValidationSeverity", source, StringComparison.Ordinal);
        Assert.Contains("GetFilmProfileValidationPresentationSeverity(CurrentFilmProfileValidationIssues)", source, StringComparison.Ordinal);
        Assert.Contains("issues.Any(issue => issue.Severity == ScanFilmProfileValidationSeverity.Error && !FilmProfileValidationTextPresenter.IsPendingInput(issue))", source, StringComparison.Ordinal);
        Assert.Matches(@"issues\.Count > 0\s*\? InfoBarSeverity\.Informational", source);
        Assert.Contains("public bool IsCurrentFilmProfileValidationValid => new ScanFilmProfileValidationResult(CurrentFilmProfileValidationIssues).IsValid;", source, StringComparison.Ordinal);

        // Pending input has informational presentation, but remains a validation
        // error used by the existing save gate and the final export check above.
        var presenter = ReadAppSource("Helpers", "FilmProfileValidationTextPresenter.cs");
        var inputIssue = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(presenter, "CreateChannelParameterInputIssue");
        Assert.Contains("FilmProfile.Validation.SystemClockInputMissing", inputIssue, StringComparison.Ordinal);
        Assert.Contains("ScanFilmProfileValidationSeverity.Error", inputIssue, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo9VerifierFix_ProfileDngSelectionUsesConciseReadableTextWithFullAccessibleValue()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var converter = ReadAppSource("Helpers", "ScanSelectorDisplayNameConverter.cs");
        var basic = ExtractNamedRegion(xaml, "BasicInfoSection", "AcquisitionPlanSection");
        var english = FilmProfileContractSource.ReadResources("en-us");
        var chinese = FilmProfileContractSource.ReadResources("zh-CN");

        Assert.Contains("ProfileDngExportModeOptionTemplate", xaml, StringComparison.Ordinal);
        Assert.Contains("ConverterParameter=ProfileDngExportMode", xaml, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedProfileDngExportModeAccessibleText", basic, StringComparison.Ordinal);
        Assert.Contains("Scan_Runtime_ProfileDngExportModeLinearRaw4", converter, StringComparison.Ordinal);
        Assert.Contains("Scan_Runtime_ProfileDngExportModeLinearRgbIrw", converter, StringComparison.Ordinal);
        // A selected W capture can add an auxiliary file. The mode names describe
        // the layout; the scan page reports the actual file count from its plan.
        Assert.Equal("Raw channel DNG", english["Scan_Runtime_ProfileDngExportModeLinearRaw4"]);
        Assert.Equal("RGB composite and auxiliary DNG", english["Scan_Runtime_ProfileDngExportModeLinearRgbIrw"]);
        Assert.Equal("原始通道 DNG", chinese["Scan_Runtime_ProfileDngExportModeLinearRaw4"]);
        Assert.Equal("RGB 合成及辅助通道 DNG", chinese["Scan_Runtime_ProfileDngExportModeLinearRgbIrw"]);
        foreach (var resources in new[] { english, chinese })
        {
            Assert.Equal(resources["Scan_Runtime_ProfileDngExportModeLinearRaw4"], resources["Scan_Runtime_DngExportModeLinearRaw4"]);
            Assert.Equal(resources["Scan_Runtime_ProfileDngExportModeLinearRgbIrw"], resources["Scan_Runtime_DngExportModeLinearRgbIrw"]);
            Assert.Contains("{0}", resources["Scan_Runtime_DngOutputPlan"], StringComparison.Ordinal);
            Assert.Contains("{1}", resources["Scan_Runtime_DngOutputPlan"], StringComparison.Ordinal);
        }
        Assert.Contains("ViewModel.OutputFormatDescriptionText", ReadAppSource("Views", "ScanPage.xaml"), StringComparison.Ordinal);
        var scanSource = ReadAppSource("ViewModels", "ScanViewModel.cs");
        var outputDescription = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(
            scanSource[scanSource.IndexOf("private string BuildOutputFormatDescription()", StringComparison.Ordinal)..], "BuildOutputFormatDescription");
        Assert.Contains("ScanDngOutputPlan.Build", outputDescription, StringComparison.Ordinal);
        Assert.Contains("plan.Count", outputDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo9ValidationContract_BasicInvalidIntermediateInputsUseFieldSpecificCurrentIssues()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var build = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "TryBuildCurrentFilmProfileDraft");
        var recipe = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "TryBuildScanRecipeSettings");

        foreach (var path in new[]
        {
            "ScanRecipeSettings.ColorManagement.RedWavelengthNm",
            "ScanRecipeSettings.ColorManagement.GreenWavelengthNm",
            "ScanRecipeSettings.ColorManagement.BlueWavelengthNm",
            "ScanRecipeSettings.ColorManagement.OutputGamma",
            "ScanRecipeSettings.ColorManagement.ManualWhitePointColorTemperatureK"
        })
        {
            Assert.Contains(path, recipe, StringComparison.Ordinal);
        }

        Assert.Contains("TryBuildScanRecipeSettings(out var recipeSettings, issues)", build, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidColorManagement, \"ScanRecipeSettings\"", build, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo10Baseline_CurrentAcquisitionAndDebugInventoryIsExplicitBeforeMigration()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");

        Assert.Equal(ExpectedNamedElements.Order(StringComparer.Ordinal), FilmProfileContractSource.GetXamlNames(xaml));
        foreach (var binding in Todo10OperatorAcquisitionBindings.Where(binding => !binding.StartsWith("x:Uid=\"ScanDebug_FilmProfileWorkbench", StringComparison.Ordinal)))
            Assert.Contains(binding, xaml, StringComparison.Ordinal);

        foreach (var binding in Todo10DebugOnlyAcquisitionSwitchBindings.Where(binding => !binding.StartsWith("x:Uid=\"ScanDebug_FilmProfileWorkbench", StringComparison.Ordinal)))
            Assert.Contains(binding, xaml, StringComparison.Ordinal);

        foreach (var property in new[]
        {
            "public partial string SelectedRows",
            "public partial string SelectedScanMotor",
            "public partial string MotorDistancePerLineValue",
            "public partial string MotorDistancePerLineUnit",
            "public partial string SelectedStartingDirection",
            "public partial bool IsWarmUpEnabled",
            "public partial bool IsPreviewEnabled",
            "public ObservableCollection<ScanDebugAcquisitionChannelViewModel> AcquisitionChannels",
            "public string AcquisitionPlanSummaryText",
            "public string AcquisitionChannelOrderText",
            "public string ChannelLedBindingSummaryText",
            "public ObservableCollection<ScanDebugCaptureMode> CaptureModeOptions",
            "public partial ScanDebugCaptureMode SelectedCaptureMode",
            "public bool IsContinuousScanEnabled",
            "public partial bool IsWaterfallEnabled",
            "public bool IsScanMotorTransportEnabled",
            "public partial bool IsAlternateMotorDirectionEnabled"
        })
        {
            Assert.Contains(property, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Todo10SourceContract_AcquisitionPlanOwnsOperatorFieldsAndStatusExactlyOnce()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var acquisition = ExtractNamedRegion(xaml, "AcquisitionPlanSection", "ChannelCalibrationSection");
        var live = ExtractNamedRegion(xaml, "LiveCalibrationSection", "DeviceSettingsSection");
        var engineering = ExtractNamedRegion(xaml, "EngineeringToolsSection", "WorkbenchPreviewColumnContent");
        var preview = ExtractNamedRegion(xaml, "WorkbenchPreviewColumnContent", "PreviewCanvasControl");

        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", acquisition, StringComparison.Ordinal);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var setupTitle = Assert.Single(XDocument.Parse(xaml).Descendants(),
            element => (string?)element.Attribute(x + "Uid") == "ScanDebug_FilmProfileWorkbenchAcquisitionSetupTitle");
        var details = GetNamedElement(xaml, "AcquisitionPlanDetailsExpander");
        Assert.Contains(details.Descendants(), element =>
            (string?)element.Attribute(x + "Uid") == "ScanDebug_FilmProfileWorkbenchAcquisitionSetupTitle");
        Assert.Equal("{StaticResource ScanDebugGroupTitleStyle}", (string?)setupTitle.Attribute("Style"));
        Assert.Equal("False", (string?)details.Attribute("IsExpanded"));
        Assert.Contains("TextWrapping=\"WrapWholeWords\"", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"ScanDebug_FilmProfileWorkbenchAcquisitionStatusTitle\"", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"ScanDebug_FilmProfileWorkbenchAcquisitionStatusDescription\"", acquisition, StringComparison.Ordinal);

        foreach (var binding in Todo10OperatorAcquisitionBindings)
        {
            Assert.Contains(binding, acquisition, StringComparison.Ordinal);
        }

        foreach (var uid in new[]
        {
            "ScanDebug_RowsComboBox",
            "ScanDebug_DeviceChannelsEntry",
            "Scan_MotorIntervalTextBox",
            "Scan_MotorIntervalUnitComboBox",
            "ScanDebug_WarmUpToggleSwitch",
            "ScanDebug_PreviewToggleSwitch",
            "Scan_StartingDirectionComboBox",
            "ScanDebug_AcquisitionChannelsTitle",
            "ScanDebug_AcquisitionChannelsHelpText"
        })
        {
            Assert.Equal(1, CountOccurrences(acquisition, $"x:Uid=\"{uid}\""));
            Assert.DoesNotContain($"x:Uid=\"{uid}\"", engineering, StringComparison.Ordinal);
        }

        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugDescription\"", engineering, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugDescription\"", engineering, StringComparison.Ordinal);
        foreach (var debugBinding in Todo10DebugOnlyAcquisitionSwitchBindings.Where(binding => !binding.StartsWith("x:Uid=\"ScanDebug_FilmProfileWorkbench", StringComparison.Ordinal)))
        {
            Assert.Contains(debugBinding, preview, StringComparison.Ordinal);
            Assert.DoesNotContain(debugBinding, engineering, StringComparison.Ordinal);
        }

        foreach (var transportBinding in Todo21TransportStrategyBindings)
        {
            Assert.Contains(transportBinding, acquisition, StringComparison.Ordinal);
            Assert.DoesNotContain(transportBinding, engineering, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("x:Uid=\"ScanDebug_WaterfallToggleSwitch\"", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"ScanDebug_WaterfallToggleSwitch\"", engineering, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(preview, "x:Uid=\"ScanDebug_WaterfallToggleSwitch\""));
        Assert.Equal(1, CountOccurrences(xaml, "x:Uid=\"Scan_AlternateDirectionToggleSwitch\""));
        Assert.DoesNotContain("x:Uid=\"Scan_AlternateDirectionToggleSwitch\"", engineering, StringComparison.Ordinal);

        var run = GetNamedElement(xaml, "WorkbenchRunBar").ToString();
        foreach (var binding in Todo21CaptureModeBindings)
        {
            Assert.Contains(binding, run, StringComparison.Ordinal);
            Assert.DoesNotContain(binding, live, StringComparison.Ordinal);
            Assert.DoesNotContain(binding, engineering, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("ScanDebug_ContinuousToggleSwitch", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ScanDebug_ScanMotorTransportToggleSwitch", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsOn=\"{x:Bind ViewModel.IsContinuousScanEnabled, Mode=TwoWay}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsOn=\"{x:Bind ViewModel.IsScanMotorTransportEnabled, Mode=TwoWay}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo22SourceContract_PreviewToolbarOwnsPreviewOnlyDisplayStateAndBoundedFlyouts()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs") + ReadAppSource("Views", "ScanDebugPage.CandidateReview.cs");
        var preview = ExtractNamedRegion(xaml, "WorkbenchPreviewColumnContent", "PreviewCanvasControl");
        var displayStart = preview.IndexOf("PreviewDisplayToolsButton", StringComparison.Ordinal);
        var overlayStart = preview.IndexOf("OverlayToolsButton", StringComparison.Ordinal);
        var scrollStart = preview.IndexOf("PreviewScrollViewer", StringComparison.Ordinal);
        Assert.True(displayStart >= 0, "Preview display tools trigger should remain present.");
        Assert.True(overlayStart > displayStart, "Overlay tools trigger should follow display tools.");
        Assert.True(scrollStart > overlayStart, "Preview scroll viewer should follow local flyouts.");
        var displayFlyout = preview[displayStart..overlayStart];
        var overlayFlyout = preview[overlayStart..scrollStart];

        foreach (var required in new[]
        {
            "x:Name=\"PreviewGammaToggleSwitch\"",
            "AutomationProperties.AutomationId=\"PreviewGammaToggleSwitch\"",
            "x:Uid=\"ScanDebug_PreviewGammaToggleSwitch\"",
            "IsOn=\"{x:Bind ViewModel.IsGammaCorrectionEnabled, Mode=TwoWay}\"",
            "x:Name=\"PreviewGammaTextBox\"",
            "AutomationProperties.AutomationId=\"PreviewGammaTextBox\"",
            "x:Uid=\"ScanDebug_PreviewGammaTextBox\"",
            "Text=\"{x:Bind ViewModel.PreviewGamma, Mode=TwoWay}\"",
            "x:Uid=\"ScanDebug_WaterfallToggleSwitch\"",
            "IsEnabled=\"{x:Bind ViewModel.CanEditWaterfall, Mode=OneWay}\"",
            "IsOn=\"{x:Bind ViewModel.IsWaterfallEnabled, Mode=TwoWay}\"",
            "x:Name=\"WaterfallPreviewOptionsPanel\"",
            "Visibility=\"{x:Bind ViewModel.WaterfallPreviewOptionsVisibility, Mode=OneWay}\"",
            "x:Uid=\"ScanDebug_WaterfallCompressionToggleSwitch\"",
            "x:Uid=\"ScanDebug_WhiteLevelPreviewToggleSwitch\""
        })
        {
            Assert.Contains(required, preview, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("x:Uid=\"ScanDebug_GammaCorrectionToggleSwitch\"", displayFlyout, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"ScanDebug_GammaTextBox\"", displayFlyout, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEnabled=\"{x:Bind ViewModel.IsWaterfallEnabled, Mode=OneWay}\"", displayFlyout, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PreviewGammaToggleSwitch\"", displayFlyout, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PreviewGammaTextBox\"", displayFlyout, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_WaterfallToggleSwitch\"", displayFlyout, StringComparison.Ordinal);
        Assert.Contains("IsOn=\"{x:Bind ViewModel.IsWaterfallEnabled, Mode=TwoWay}\"", displayFlyout, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PreviewDisplayToolsScrollViewer\"", displayFlyout, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OverlayToolsScrollViewer\"", overlayFlyout, StringComparison.Ordinal);
        Assert.DoesNotContain("MinWidth=\"760\"", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=\"1040\"", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("MinWidth=\"860\"", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=\"1120\"", preview, StringComparison.Ordinal);
        Assert.Contains("ConfigureLocalFlyoutBounds(PreviewDisplayToolsFlyout, PreviewDisplayToolsScrollViewer, PreviewDisplayToolsContent);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ConfigureLocalFlyoutBounds(OverlayToolsFlyout, OverlayToolsScrollViewer, OverlayToolsContent);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("const double targetFlyoutWidth = 420;", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void ObservationDisplayTools_GammaEnablementIsOwnedByControls()
    {
        var page = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var elements = page.Descendants().ToArray();
        Assert.All(elements.Where(element => element.Name.LocalName is "StackPanel" or "Grid" or "Border" or "RelativePanel"),
            element => Assert.Null(element.Attribute("IsEnabled")));
        var toggle = elements.Single(element => (string?)element.Attribute(x + "Name") == "PreviewGammaToggleSwitch");
        var input = elements.Single(element => (string?)element.Attribute(x + "Name") == "PreviewGammaTextBox");
        Assert.Equal("ToggleSwitch", toggle.Name.LocalName);
        Assert.Equal("TextBox", input.Name.LocalName);
        Assert.Equal("{x:Bind ViewModel.IsObservationLive, Mode=OneWay}", (string?)toggle.Attribute("IsEnabled"));
        Assert.Equal("{x:Bind ViewModel.CanEditObservationGamma, Mode=OneWay}", (string?)input.Attribute("IsEnabled"));
    }

    [Fact]
    public void Todo22Red_OuterFlyoutPresenterIsBoundedWithoutInnerWidthInflation()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs") + ReadAppSource("Views", "ScanDebugPage.CandidateReview.cs");
        var preview = ExtractNamedRegion(xaml, "WorkbenchPreviewColumnContent", "PreviewCanvasControl");
        var displayFlyout = preview[preview.IndexOf("PreviewDisplayToolsButton", StringComparison.Ordinal)..preview.IndexOf("OverlayToolsButton", StringComparison.Ordinal)];
        var overlayFlyout = preview[preview.IndexOf("OverlayToolsButton", StringComparison.Ordinal)..preview.IndexOf("PreviewScrollViewer", StringComparison.Ordinal)];
        var owner = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(codeBehind, "ConfigureLocalFlyoutBounds");
        var presenterBaseStyleStart = xaml.IndexOf("<Style x:Key=\"ScanDebugLocalFlyoutPresenterBaseStyle\"", StringComparison.Ordinal);
        Assert.True(presenterBaseStyleStart >= 0, "The bounded local flyouts must share a page-local FlyoutPresenter base style.");
        var presenterBaseStyleEnd = xaml.IndexOf("</Style>", presenterBaseStyleStart, StringComparison.Ordinal);
        Assert.True(presenterBaseStyleEnd > presenterBaseStyleStart, "The local FlyoutPresenter base style must be closed before flyout usage.");
        var presenterBaseStyle = xaml[presenterBaseStyleStart..(presenterBaseStyleEnd + "</Style>".Length)];

        Assert.Contains("TargetType=\"FlyoutPresenter\"", presenterBaseStyle, StringComparison.Ordinal);
        Assert.Contains("BasedOn=\"{StaticResource DefaultFlyoutPresenterStyle}\"", presenterBaseStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("Property=\"Padding\"", presenterBaseStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("Property=\"BorderThickness\"", presenterBaseStyle, StringComparison.Ordinal);
        Assert.DoesNotContain("Property=\"Template\"", presenterBaseStyle, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PreviewDisplayToolsFlyout\"", displayFlyout, StringComparison.Ordinal);
        Assert.Contains("FlyoutPresenterStyle=\"{StaticResource ScanDebugLocalFlyoutPresenterBaseStyle}\"", displayFlyout, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OverlayToolsFlyout\"", overlayFlyout, StringComparison.Ordinal);
        Assert.Contains("FlyoutPresenterStyle=\"{StaticResource ScanDebugLocalFlyoutPresenterBaseStyle}\"", overlayFlyout, StringComparison.Ordinal);
        foreach (var innerDeclaration in new[]
        {
            GetOpeningTag(xaml, "PreviewDisplayToolsScrollViewer"),
            GetOpeningTag(xaml, "PreviewDisplayToolsContent"),
            GetOpeningTag(xaml, "OverlayToolsScrollViewer"),
            GetOpeningTag(xaml, "OverlayToolsContent")
        })
        {
            Assert.DoesNotContain("Width=", innerDeclaration, StringComparison.Ordinal);
            Assert.DoesNotContain("MaxWidth=", innerDeclaration, StringComparison.Ordinal);
        }

        Assert.Contains("private void ConfigureLocalFlyoutBounds(Flyout flyout, ScrollViewer scrollViewer, FrameworkElement content)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("new Style(typeof(FlyoutPresenter))", owner, StringComparison.Ordinal);
        Assert.Contains("BasedOn = (Style)Resources[\"ScanDebugLocalFlyoutPresenterBaseStyle\"]", owner, StringComparison.Ordinal);
        Assert.Contains("new Setter(FrameworkElement.WidthProperty, width)", owner, StringComparison.Ordinal);
        Assert.Contains("new Setter(FrameworkElement.MaxWidthProperty, width)", owner, StringComparison.Ordinal);
        Assert.Contains("new Setter(FrameworkElement.MaxHeightProperty, height)", owner, StringComparison.Ordinal);
        Assert.Contains("flyout.FlyoutPresenterStyle = boundedPresenterStyle;", owner, StringComparison.Ordinal);
        Assert.Contains("Math.Min(targetFlyoutWidth, rootSize.Width - edgePadding)", owner, StringComparison.Ordinal);
        Assert.DoesNotContain("scrollViewer.Width = width;", owner, StringComparison.Ordinal);
        Assert.DoesNotContain("content.Width = width;", owner, StringComparison.Ordinal);
        Assert.DoesNotContain("content.MaxWidth = width;", owner, StringComparison.Ordinal);
        Assert.DoesNotContain("BasedOn = flyout.FlyoutPresenterStyle", owner, StringComparison.Ordinal);
        Assert.DoesNotContain("320", owner, StringComparison.Ordinal);
        Assert.DoesNotContain("446", xaml + codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("26", owner, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo10SourceContract_AcquisitionEditsSynchronizeDraftAndInvalidInputsBlockSaveWithoutProjection()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var build = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "TryBuildCurrentFilmProfileDraft");
        var acquisition = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "TryBuildFilmAcquisitionSettings");
        var synchronization = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "SynchronizeFilmProfileDraftFromInputs");

        foreach (var callback in new[]
        {
            "OnSelectedRowsChanged",
            "OnIsWarmUpEnabledChanged",
            "OnSelectedScanMotorChanged",
            "OnMotorDistancePerLineValueChanged",
            "OnMotorDistancePerLineUnitChanged",
            "OnSelectedStartingDirectionChanged",
            "OnIsAlternateMotorDirectionEnabledChanged"
        })
        {
            Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, callback), StringComparison.Ordinal);
        }

        Assert.DoesNotContain("SynchronizeFilmProfileDraftFromInputs", ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "OnIsPreviewEnabledChanged"), StringComparison.Ordinal);

        Assert.Contains("TryBuildFilmAcquisitionSettings", build, StringComparison.Ordinal);
        Assert.Contains("TryParseRequestedRows(out var rows)", acquisition, StringComparison.Ordinal);
        Assert.Contains("rows,", acquisition, StringComparison.Ordinal);
        Assert.Contains("TryParseSelectedScanMotor(out var motorId", acquisition, StringComparison.Ordinal);
        Assert.Contains("BuildDebugChannelAssignment()", acquisition, StringComparison.Ordinal);
        Assert.Contains("TargetLinePitchMicrometers", acquisition, StringComparison.Ordinal);
        Assert.Contains("IsAlternateMotorDirectionEnabled ? ScanFilmTransportStrategy.AlternateDirection : ScanFilmTransportStrategy.ReturnToStart", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.Rows", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.ScanMotorId", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.TargetLinePitchMicrometers", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.StartingDirectionPositive", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.WarmUpEnabled", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.TransportStrategy", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.AcquisitionChannelAssignment", acquisition, StringComparison.Ordinal);
        ScanDebugFilmProfileOrchestrationSourceTests.AssertInvalidInputsOnlyRestoreOwnedEditorBaseline(synchronization);
    }

    [Fact]
    public void Todo10SourceContract_ProfileAcquisitionProjectionUsesSelectedMotor()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var applyAcquisition = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "ApplyProfileAcquisitionSettings");

        Assert.Contains("SelectedRows = normalized.Rows.ToString(CultureInfo.InvariantCulture);", applyAcquisition, StringComparison.Ordinal);
        Assert.Contains("SelectedScanMotor = MotorOptions[normalized.ScanMotorId];", applyAcquisition, StringComparison.Ordinal);
        Assert.Contains("SelectedStartingDirection = normalized.StartingDirectionPositive ? ForwardDirection : ReverseDirection;", applyAcquisition, StringComparison.Ordinal);
        Assert.Contains("IsWarmUpEnabled = normalized.WarmUpEnabled;", applyAcquisition, StringComparison.Ordinal);
        Assert.Contains("IsAlternateMotorDirectionEnabled = normalized.TransportStrategy == ScanFilmTransportStrategy.AlternateDirection;", applyAcquisition, StringComparison.Ordinal);
        Assert.Contains("MotorDistancePerLineValue = targetLinePitchDisplayValue;", applyAcquisition, StringComparison.Ordinal);
        Assert.Contains("NotifyAcquisitionPlanChanged();", applyAcquisition, StringComparison.Ordinal);
        Assert.Contains("TryParseSelectedScanMotor(out var motorId", applyAcquisition, StringComparison.Ordinal);
        Assert.Contains("ApplyMotorSpeedFromIntervalNs(motorId", applyAcquisition, StringComparison.Ordinal);
        Assert.Contains("TryParseSelectedScanMotor(out var selectedMotorId", ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "OnMotorSpeedInputChanged"), StringComparison.Ordinal);
        Assert.DoesNotContain("ClearMotorIntervalInput(selectedMotorId)", applyAcquisition, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo10SourceContract_AcquisitionStateProjectionUsesLiveUiStateAndResetsOnImport()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var build = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "TryBuildFilmAcquisitionSettings");
        var apply = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "ApplyProfileAcquisitionSettings");
        var selectedScanMotorChanged = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "OnSelectedScanMotorChanged");

        Assert.Contains("TryParseRequestedRows(out var rows)", build, StringComparison.Ordinal);
        Assert.Contains("rows,", build, StringComparison.Ordinal);
        Assert.Contains("TryParseSelectedScanMotor(out var motorId", build, StringComparison.Ordinal);
        Assert.Contains("BuildDebugChannelAssignment()", build, StringComparison.Ordinal);
        Assert.Contains("TargetLinePitchMicrometers", build, StringComparison.Ordinal);
        Assert.Contains("IsAlternateMotorDirectionEnabled ? ScanFilmTransportStrategy.AlternateDirection : ScanFilmTransportStrategy.ReturnToStart", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.Rows", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.ScanMotorId", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.TargetLinePitchMicrometers", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.StartingDirectionPositive", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.WarmUpEnabled", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.TransportStrategy", build, StringComparison.Ordinal);
        Assert.DoesNotContain("existing.AcquisitionChannelAssignment", build, StringComparison.Ordinal);

        Assert.Contains("SelectedRows = normalized.Rows.ToString(CultureInfo.InvariantCulture);", apply, StringComparison.Ordinal);
        Assert.Contains("SelectedScanMotor = MotorOptions[normalized.ScanMotorId];", apply, StringComparison.Ordinal);
        Assert.Contains("SelectedStartingDirection = normalized.StartingDirectionPositive ? ForwardDirection : ReverseDirection;", apply, StringComparison.Ordinal);
        Assert.Contains("IsWarmUpEnabled = normalized.WarmUpEnabled;", apply, StringComparison.Ordinal);
        Assert.Contains("IsAlternateMotorDirectionEnabled = normalized.TransportStrategy == ScanFilmTransportStrategy.AlternateDirection;", apply, StringComparison.Ordinal);
        Assert.Contains("MotorDistancePerLineValue = targetLinePitchDisplayValue;", apply, StringComparison.Ordinal);
        Assert.Contains("NotifyAcquisitionPlanChanged();", apply, StringComparison.Ordinal);
        Assert.Contains("partial void OnIsAlternateMotorDirectionEnabledChanged(bool value)", source, StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs();", selectedScanMotorChanged, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo16SourceContract_IlluminationRefreshUpdatesDeviceSnapshotOnly()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var load = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "LoadIlluminationStateAsync");
        var deviceSnapshot = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "ApplyDeviceIlluminationSnapshot");
        var draftProjection = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "ApplyDraftIlluminationStateToInputs");
        var acquisitionProjection = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "ApplyProfileAcquisitionSettings");

        Assert.Contains("ApplyDeviceIlluminationSnapshot(state);", load, StringComparison.Ordinal);
        Assert.DoesNotContain("Led1Level = state.Led1Level", deviceSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshFilmProfileWorkspaceProjection", deviceSnapshot, StringComparison.Ordinal);
        Assert.Contains("_deviceIlluminationSnapshot = state;", deviceSnapshot, StringComparison.Ordinal);
        Assert.Contains("DeviceLed1Level", deviceSnapshot, StringComparison.Ordinal);
        Assert.Contains("Led1Level = state.Led1Level.ToString", draftProjection, StringComparison.Ordinal);
        Assert.Contains("ApplyDraftIlluminationStateToInputs(new ScanIlluminationState", acquisitionProjection, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo16SourceContract_RawApplyAndSessionTestsDoNotUseDraftInputsOrPersistence()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var apply = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "ApplyIllumination");
        var rawBuilder = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "TryBuildRawIlluminationState");
        var sessionBuilder = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "TryBuildSessionIlluminationTestState");
        var sessionApply = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "ApplySessionIlluminationTestAsync");

        Assert.Contains("TryBuildRawIlluminationState(out var state, out var error)", apply, StringComparison.Ordinal);
        Assert.DoesNotContain("TryBuildIlluminationRequest", apply, StringComparison.Ordinal);
        Assert.Contains("RawLed1Level", rawBuilder, StringComparison.Ordinal);
        Assert.Contains("ScanIlluminationValidator.ValidateState(state, \"rawIllumination\")", rawBuilder, StringComparison.Ordinal);
        Assert.Contains("RawIlluminationValidationIssues = validation.Issues;", rawBuilder, StringComparison.Ordinal);
        Assert.DoesNotContain("SynchronizeFilmProfileDraftFromInputs", rawBuilder + sessionBuilder + sessionApply, StringComparison.Ordinal);
        Assert.DoesNotContain("_filmProfileWorkspace.SetCurrentDraft", rawBuilder + sessionBuilder + sessionApply, StringComparison.Ordinal);
        Assert.DoesNotContain("Led1Level =", rawBuilder, StringComparison.Ordinal);
        Assert.Contains("SessionTestLedIndex", sessionBuilder, StringComparison.Ordinal);
        Assert.Contains("ApplyDeviceIlluminationSnapshot(state);", ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "LoadIlluminationStateAsync"), StringComparison.Ordinal);
    }

    [Fact]
    public void Todo16SourceContract_RawMalformedNumericInputsExposeExactFieldPaths()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var validation = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility.Core", "Models", "ScanIlluminationValidation.cs"));
        var rawBuilder = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "TryBuildRawIlluminationState");
        var parseFailure = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "SetRawIlluminationParseFailure");

        foreach (var fieldPath in new[]
        {
            "rawIllumination.led1.level",
            "rawIllumination.led2.level",
            "rawIllumination.led3.level",
            "rawIllumination.led4.level",
            "rawIllumination.led1.pulseClock",
            "rawIllumination.led2.pulseClock",
            "rawIllumination.led3.pulseClock",
            "rawIllumination.led4.pulseClock"
        })
        {
            Assert.Contains(fieldPath, rawBuilder, StringComparison.Ordinal);
        }

        Assert.Contains("RawIlluminationValidationIssues = new[]", parseFailure, StringComparison.Ordinal);
        Assert.Contains("new ScanIlluminationValidationIssue(fieldPath", parseFailure, StringComparison.Ordinal);
        Assert.Contains("InvalidNumericInput,", validation, StringComparison.Ordinal);
        Assert.Contains("ScanIlluminationValidationCode.InvalidNumericInput", parseFailure, StringComparison.Ordinal);
        Assert.DoesNotContain("ScanIlluminationValidationCode.InvalidMaskBits", parseFailure, StringComparison.Ordinal);
        Assert.Contains("$\"{fieldPath}: ", parseFailure, StringComparison.Ordinal);
        Assert.DoesNotContain("Shared_Runtime_ErrorIntegerRange0To65535\".GetLocalizedFormat(fieldPath)", rawBuilder, StringComparison.Ordinal);
        Assert.DoesNotContain("ScanDebug_Runtime_ErrorNonNegativeInteger\".GetLocalizedFormat(fieldPath)", rawBuilder, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo16SourceContract_DeviceSnapshotValuesAndModesHaveTruthfulAccessibility()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var engineering = ExtractNamedRegion(xaml, "IlluminationContent", "MotionContent");

        foreach (var led in Enumerable.Range(1, 4))
        {
            var levelText = GetOpeningTagByAttribute(engineering, "AutomationProperties.AutomationId", $"DeviceLed{led}LevelText");
            var pulseClockText = GetOpeningTagByAttribute(engineering, "AutomationProperties.AutomationId", $"DeviceLed{led}PulseClockText");
            var steadyState = GetOpeningTagByAttribute(engineering, "AutomationProperties.AutomationId", $"DeviceLed{led}SteadyState");
            var syncState = GetOpeningTagByAttribute(engineering, "AutomationProperties.AutomationId", $"DeviceLed{led}SyncState");

            Assert.Contains($"x:Uid=\"ScanDebug_DeviceLed{led}LevelText\"", levelText, StringComparison.Ordinal);
            Assert.Contains($"x:Uid=\"ScanDebug_DeviceLed{led}PulseClockText\"", pulseClockText, StringComparison.Ordinal);

            foreach (var modeState in new[] { steadyState, syncState })
            {
                Assert.Contains("IsEnabled=\"False\"", modeState, StringComparison.Ordinal);
                Assert.Contains("IsHitTestVisible=\"False\"", modeState, StringComparison.Ordinal);
                Assert.Contains("IsTabStop=\"False\"", modeState, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Todo16SourceContract_DeviceModeStatesHaveLedSpecificAccessibleNamesAndRemainReadOnly()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var engineering = ExtractNamedRegion(xaml, "IlluminationContent", "MotionContent");

        foreach (var led in Enumerable.Range(1, 4))
        {
            var steadyState = GetOpeningTagByAttribute(engineering, "AutomationProperties.AutomationId", $"DeviceLed{led}SteadyState");
            var syncState = GetOpeningTagByAttribute(engineering, "AutomationProperties.AutomationId", $"DeviceLed{led}SyncState");

            Assert.Contains($"x:Uid=\"ScanDebug_DeviceLed{led}SteadyState\"", steadyState, StringComparison.Ordinal);
            Assert.Contains($"x:Uid=\"ScanDebug_DeviceLed{led}SyncState\"", syncState, StringComparison.Ordinal);

            foreach (var modeState in new[] { steadyState, syncState })
            {
                Assert.Contains("IsEnabled=\"False\"", modeState, StringComparison.Ordinal);
                Assert.Contains("IsHitTestVisible=\"False\"", modeState, StringComparison.Ordinal);
                Assert.Contains("IsTabStop=\"False\"", modeState, StringComparison.Ordinal);
                Assert.Contains($"IsOn=\"{{x:Bind ViewModel.IsDeviceLed{led}", modeState, StringComparison.Ordinal);
                Assert.Contains("Mode=OneWay}", modeState, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Todo16SourceContract_OnlyExplicitRawCopyMovesRawIlluminationIntoDraftInputs()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var copy = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "CopyRawIlluminationToDraft");
        var acquisition = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "TryBuildFilmAcquisitionSettings");

        Assert.Contains("TryBuildIlluminationRequest(out var illuminationRequest, out var error, clearUnusedInputs: false)", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("RawLed", acquisition, StringComparison.Ordinal);
        Assert.Contains("TryBuildRawIlluminationState(out var state, out var error)", copy, StringComparison.Ordinal);
        Assert.Contains("ApplyDraftIlluminationStateToInputs(state);", copy, StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs();", copy, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(source, "ApplyDraftIlluminationStateToInputs(state);"));
    }

    [Fact]
    public void Todo16SourceContract_EngineeringIlluminationExpanderSeparatesDeviceRawAndSessionScopes()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var channel = ExtractNamedRegion(xaml, "ChannelCalibrationSection", "LiveCalibrationSection");
        var live = ExtractNamedRegion(xaml, "LiveCalibrationSection", "DeviceSettingsSection");
        var engineering = ExtractNamedRegion(xaml, "IlluminationContent", "MotionContent");

        Assert.Contains("AutomationProperties.AutomationId=\"ChannelCalibrationIlluminationCard\"", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("ChannelCalibrationIlluminationCard", engineering, StringComparison.Ordinal);
        var acquisition = ExtractNamedRegion(xaml, "AcquisitionPlanSection", "ChannelCalibrationSection");
        var engineeringTools = ExtractNamedRegion(xaml, "EngineeringToolsSection", "MotionTaskSection");
        Assert.Contains("AutomationProperties.AutomationId=\"SessionIlluminationTestCard\"", engineeringTools, StringComparison.Ordinal);
        Assert.DoesNotContain("SessionIlluminationTestCard", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SessionIlluminationTestCard\"", live, StringComparison.Ordinal);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SessionIlluminationTestCard\"", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("ActiveIlluminationChannels", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("LevelHeader", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("PulseClockHeader", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkModeOptions", engineering, StringComparison.Ordinal);

        foreach (var uid in new[]
        {
            "ScanDebug_IlluminationDeviceSnapshotTitle",
            "ScanDebug_IlluminationRawEditTitle",
            "ScanDebug_RawIlluminationValidationTitle",
            "ScanDebug_IlluminationRawApplyScopeText",
            "ScanDebug_IlluminationRawCopyScopeText",
            "ScanDebug_CopyRawIlluminationToDraftButton"
        })
        {
            Assert.Contains($"x:Uid=\"{uid}\"", engineering, StringComparison.Ordinal);
        }

        foreach (var uid in new[]
        {
            "ScanDebug_IlluminationSessionTestTitle",
            "ScanDebug_IlluminationSessionTestHelpText",
            "ScanDebug_TestIlluminationOffButton",
            "ScanDebug_TestIlluminationSteadyButton",
            "ScanDebug_TestIlluminationAcquisitionSyncButton"
        })
        {
            Assert.Contains($"x:Uid=\"{uid}\"", engineeringTools, StringComparison.Ordinal);
            Assert.DoesNotContain($"x:Uid=\"{uid}\"", acquisition, StringComparison.Ordinal);
            Assert.DoesNotContain($"x:Uid=\"{uid}\"", live, StringComparison.Ordinal);
            Assert.DoesNotContain($"x:Uid=\"{uid}\"", engineering, StringComparison.Ordinal);
        }

        foreach (var led in Enumerable.Range(1, 4))
        {
            Assert.Contains($"x:Uid=\"ScanDebug_IlluminationLed{led}RowTitle\"", engineering, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(engineering, $"Text=\"{{x:Bind ViewModel.DeviceLed{led}Level, Mode=OneWay}}\""));
            Assert.Equal(1, CountOccurrences(engineering, $"Text=\"{{x:Bind ViewModel.DeviceLed{led}PulseClock, Mode=OneWay}}\""));
            Assert.Equal(1, CountOccurrences(engineering, $"IsOn=\"{{x:Bind ViewModel.IsDeviceLed{led}SteadyEnabled, Mode=OneWay}}\""));
            Assert.Equal(1, CountOccurrences(engineering, $"IsOn=\"{{x:Bind ViewModel.IsDeviceLed{led}SyncEnabled, Mode=OneWay}}\""));
            Assert.Equal(1, CountOccurrences(engineering, $"Text=\"{{x:Bind ViewModel.RawLed{led}Level, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}\""));
            Assert.Equal(1, CountOccurrences(engineering, $"Text=\"{{x:Bind ViewModel.RawLed{led}PulseClock, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}\""));
            Assert.Equal(1, CountOccurrences(engineering, $"IsOn=\"{{x:Bind ViewModel.IsRawLed{led}SteadyEnabled, Mode=TwoWay}}\""));
            Assert.Equal(1, CountOccurrences(engineering, $"IsOn=\"{{x:Bind ViewModel.IsRawLed{led}SyncEnabled, Mode=TwoWay}}\""));
        }

        foreach (var binding in new[]
        {
            "ItemsSource=\"{x:Bind ViewModel.RawIlluminationValidationIssues, Mode=OneWay}\"",
            "Text=\"{Binding FieldPath}\"",
            "Text=\"{Binding Code}\"",
            "Command=\"{x:Bind ViewModel.RefreshIlluminationCommand}\"",
            "Command=\"{x:Bind ViewModel.ApplyIlluminationCommand}\"",
            "Command=\"{x:Bind ViewModel.CopyRawIlluminationToDraftCommand}\""
        })
        {
            Assert.Equal(1, CountOccurrences(engineering, binding));
        }

        foreach (var binding in new[]
        {
            "Text=\"{x:Bind ViewModel.SessionTestLedIndex, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.SessionTestLedLevel, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.SessionTestPulseClock, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Command=\"{x:Bind ViewModel.TestIlluminationOffCommand}\"",
            "Command=\"{x:Bind ViewModel.TestIlluminationSteadyCommand}\"",
            "Command=\"{x:Bind ViewModel.TestIlluminationAcquisitionSyncCommand}\""
        })
        {
            Assert.Equal(1, CountOccurrences(engineeringTools, binding));
            Assert.DoesNotContain(binding, acquisition, StringComparison.Ordinal);
            Assert.DoesNotContain(binding, live, StringComparison.Ordinal);
            Assert.DoesNotContain(binding, engineering, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("RawLed1Level", live, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{x:Bind ViewModel.CopyRawIlluminationToDraftCommand}\"", live, StringComparison.Ordinal);

        Assert.DoesNotContain("ViewModel.Led1Level, Mode=TwoWay", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.Led2Level, Mode=TwoWay", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.Led3Level, Mode=TwoWay", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.Led4Level, Mode=TwoWay", engineering, StringComparison.Ordinal);
        Assert.Contains("IsTabStop=\"False\"", engineering, StringComparison.Ordinal);
        Assert.Contains("IsHitTestVisible=\"False\"", engineering, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"WrapWholeWords\"", engineering, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo16SourceContract_RawValidationSummaryStaysWithRawActionScope()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var engineering = ExtractNamedRegion(xaml, "IlluminationContent", "MotionContent");

        var validationCardIndex = engineering.IndexOf("AutomationProperties.AutomationId=\"RawIlluminationValidationCard\"", StringComparison.Ordinal);
        var rawApplyScopeIndex = engineering.IndexOf("x:Uid=\"ScanDebug_IlluminationRawApplyScopeText\"", StringComparison.Ordinal);
        var rawCopyButtonIndex = engineering.IndexOf("AutomationProperties.AutomationId=\"CopyRawIlluminationToDraftButton\"", StringComparison.Ordinal);

        Assert.DoesNotContain("AutomationProperties.AutomationId=\"SessionIlluminationTestCard\"", engineering, StringComparison.Ordinal);
        Assert.True(validationCardIndex >= 0);
        Assert.True(rawApplyScopeIndex >= 0);
        Assert.True(rawCopyButtonIndex >= 0);
        Assert.Equal(1, CountOccurrences(engineering, "AutomationProperties.AutomationId=\"RawIlluminationValidationCard\""));
        Assert.True(validationCardIndex < rawApplyScopeIndex, "Raw validation must sit immediately with the raw apply/copy action scope.");
        Assert.True(validationCardIndex < rawCopyButtonIndex, "Raw validation must be visible before the raw-to-Draft command that can trigger it.");
    }

    [Fact]
    public void Todo21SourceContract_NonPreviewControlsAreRehomedWithStableLocalReasonSurfaces()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var viewModel = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var acquisition = ExtractNamedRegion(xaml, "AcquisitionPlanSection", "ChannelCalibrationSection");
        var channel = ExtractNamedRegion(xaml, "ChannelCalibrationSection", "LiveCalibrationSection");
        var live = ExtractNamedRegion(xaml, "LiveCalibrationSection", "DeviceSettingsSection");
        var deviceSettings = ExtractNamedRegion(xaml, "DeviceSettingsSection", "EngineeringToolsSection");
        var engineering = ExtractNamedRegion(xaml, "EngineeringToolsSection", "WorkbenchPreviewColumnContent");

        foreach (var transportBinding in Todo21TransportStrategyBindings)
        {
            Assert.Contains(transportBinding, acquisition, StringComparison.Ordinal);
            Assert.DoesNotContain(transportBinding, engineering, StringComparison.Ordinal);
        }

        Assert.Equal(1, CountOccurrences(xaml, "AutomationProperties.AutomationId=\"AcquisitionTransportStrategyCard\""));
        Assert.Equal(1, CountOccurrences(xaml, "x:Uid=\"Scan_AlternateDirectionToggleSwitch\""));
        Assert.Equal(1, CountOccurrences(xaml, "IsOn=\"{x:Bind ViewModel.IsAlternateMotorDirectionEnabled, Mode=TwoWay}\""));
        Assert.DoesNotContain("ScanFilmTransportStrategy", xaml, StringComparison.Ordinal);

        Assert.Contains("AutomationProperties.AutomationId=\"AdvancedAdcAdjustmentExpander\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AdvancedAdcAdjustmentContent\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_AdvancedAdcAdjustmentScopeText\"", channel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.ChannelParametersDisabledReasonText, Mode=OneWay}\"", channel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"ChannelParametersDisabledReasonText\"", channel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.CompactClockStateText, Mode=OneWay}\"", deviceSettings, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.ClockInputErrorText, Mode=OneWay}\"", deviceSettings, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind RunContextVisibility(ViewModel.ClockInputErrorText), Mode=OneWay}\"", deviceSettings, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.ApplyDeviceClockCommand}\"", deviceSettings, StringComparison.Ordinal);
        Assert.Contains("[RelayCommand(CanExecute = nameof(CanApplyDeviceClock))]", viewModel, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.SessionIlluminationDisabledReasonText, Mode=OneWay}\"", engineering, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"SessionIlluminationDisabledReasonText\"", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("SessionIlluminationDisabledReasonText", acquisition, StringComparison.Ordinal);
        var run = GetNamedElement(xaml, "WorkbenchRunBar").ToString();
        Assert.Contains("Text=\"{x:Bind ViewModel.WorkbenchDeviceStatusText, Mode=OneWay}\"", run, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"{x:Bind ViewModel.StartDisabledReasonText, Mode=OneWay}\"", run, StringComparison.Ordinal);
        Assert.Contains("BuildStartDisabledReason()", viewModel[viewModel.IndexOf("public string RunPresentationText =>", StringComparison.Ordinal)
            ..viewModel.IndexOf("public string RunContextText", StringComparison.Ordinal)], StringComparison.Ordinal);
        var preview = ExtractNamedRegion(xaml, "WorkbenchPreviewColumnContent", "PreviewScrollViewer");
        Assert.Contains("Text=\"{x:Bind ViewModel.ExportDngDisabledReasonText, Mode=OneWay}\"", preview, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"ExportDngDisabledReasonText\"", preview, StringComparison.Ordinal);

        foreach (var property in new[]
        {
            "public string DeviceClockDisabledReasonText",
            "public string ChannelParametersDisabledReasonText",
            "public string SessionIlluminationDisabledReasonText"
        })
        {
            Assert.Contains(property, viewModel, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Todo21SourceContract_AdvancedAdcExpanderContainsOnlyAdcManualEditsAndPreservesBindings()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var channel = ExtractNamedRegion(xaml, "ChannelCalibrationSection", "LiveCalibrationSection");
        var document = XDocument.Parse(xaml);
        var expanderElement = Assert.Single(document.Descendants(), element =>
            (string?)element.Attribute("AutomationProperties.AutomationId") == "AdvancedAdcAdjustmentExpander");
        Assert.Equal("Expander", expanderElement.Name.LocalName);
        var expander = expanderElement.ToString();

        Assert.Contains("IsExpanded=\"False\"", expander, StringComparison.Ordinal);
        Assert.DoesNotContain("Expanding=\"DeferredExpander_Expanding\"", expander, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Load=\"False\"", expander, StringComparison.Ordinal);

        foreach (var binding in new[]
        {
            "Text=\"{x:Bind ViewModel.Adc1Offset, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.Adc1Gain, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.Adc2Offset, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.Adc2Gain, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.Adc1OffsetLimitText, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.Adc1GainLimitText, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.Adc2OffsetLimitText, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.Adc2GainLimitText, Mode=OneWay}\""
        })
        {
            Assert.Equal(1, CountOccurrences(expander, binding));
            Assert.Equal(1, CountOccurrences(channel, binding));
        }

        foreach (var normalBinding in new[]
        {
            "Text=\"{x:Bind ViewModel.ExposureMicroseconds, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.ExposureTicks, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.CompactClockStateText, Mode=OneWay}\"",
            "Command=\"{x:Bind ViewModel.ApplyParametersCommand}\""
        })
        {
            Assert.Contains(normalBinding, channel, StringComparison.Ordinal);
            Assert.DoesNotContain(normalBinding, expander, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Todo21LocalizationContract_RehomeGuidanceIsPairedPreciseAndNonPreviewScoped()
    {
        var english = FilmProfileContractSource.ReadResources("en-us");
        var chinese = FilmProfileContractSource.ReadResources("zh-CN");
        var expected = new (string Key, string English, string Chinese)[]
        {
            ("ScanDebug_AcquisitionTransportStrategyTitle.Text", "Transport strategy", "传送策略"),
            ("ScanDebug_AcquisitionTransportStrategyHelpText.Text", "Return to start scans every selected channel in the same physical direction, then returns before the next channel. Alternate direction scans adjacent selected channels in opposite directions and relies on matching geometry.", "返回起点会让每个所选通道按相同物理方向扫描，并在下一个通道前返回。交替方向会让相邻所选通道按相反方向扫描，并依赖匹配的几何。"),
            ("ScanDebug_AcquisitionTransportStrategySafetyText.Text", "Changes the current profile draft transport strategy only. Geometry and timing checks still block unsafe motion.", "仅修改当前草稿的传送策略。\n几何与时序预检仍会阻止不安全运动。"),
            ("ScanDebug_AdvancedAdcAdjustmentTitle.Text", "Advanced ADC manual adjustment", "高级 ADC 手动调整"),
            ("ScanDebug_AdvancedAdcAdjustmentHelpText.Text", "Manual ADC offset and gain overrides for the selected channel.", "手动调整 ADC 偏移与增益。\n仅影响所选通道。"),
            ("ScanDebug_AdvancedAdcAdjustmentScopeText.Text", "Apply current channel to device uses these parameters on the scanner. Save channel preset keeps them for reuse.", "“应用当前通道到设备”使参数在设备上生效；“另存为通道预设”保留参数供复用。"),
            ("ScanDebug_DeviceClockUnitRangeText.Text", "30–200 MHz, up to three decimal places. Reapply channel parameters after changing the clock.", "30–200 MHz，最多三位小数。更改时钟后需重新应用通道参数。"),
            ("ScanDebug_Runtime_DeviceClockReadyReason", "Ready to apply the device-global clock.", "可以应用设备全局时钟。"),
            ("ScanDebug_Runtime_ChannelParametersReadyReason", "Ready to apply the selected channel parameters to the device.", "可以把所选通道参数应用到设备。"),
            ("ScanDebug_Runtime_SessionIlluminationReadyReason", "Ready to send a session-only illumination test to the connected device.", "可以向已连接设备发送仅当前会话的照明测试。"),
            ("ScanDebug_Runtime_DeviceClockInvalidReason", "Enter a device clock from 30 to 200 MHz, with at most three decimal places.", "请输入 30 到 200 MHz 的设备时钟，最多三位小数。"),
            ("ScanDebug_Runtime_DeviceClockReadRequiredReason", "Read or re-apply the device clock before this command can safely touch channel timing or hardware motion.", "读取或重新应用设备时钟后，此命令才能安全触及时序或硬件运动。")
        };

        foreach (var (key, expectedEnglish, expectedChinese) in expected)
        {
            Assert.Equal(expectedEnglish, english[key]);
            Assert.Equal(expectedChinese, chinese[key]);
        }
    }

    [Fact]
    public void Todo16LocalizationContract_EngineeringIlluminationSurfaceIsBilingualAndScoped()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var engineering = ExtractNamedRegion(xaml, "IlluminationContent", "MotionContent");
        var english = FilmProfileContractSource.ReadResources("en-us");
        var chinese = FilmProfileContractSource.ReadResources("zh-CN");
        var expected = new (string Key, string English, string Chinese)[]
        {
            ("ScanDebug_IlluminationDeviceSnapshotTitle.Text", "Device snapshot", "设备快照"),
            ("ScanDebug_IlluminationRawEditTitle.Text", "Raw engineering edit", "工程原始编辑"),
            ("ScanDebug_IlluminationSessionTestTitle.Text", "Illumination test (this session)", "照明测试（本次会话）"),
            ("ScanDebug_IlluminationRawApplyScopeText.Text", "Apply raw edits sends the values to the device only; it does not update the current profile draft.", "应用原始编辑只会把数值发送到设备，不会更新当前配置 Draft。"),
            ("ScanDebug_IlluminationRawCopyScopeText.Text", "Copy raw edits to the current profile draft updates the Draft only; it sends no device command.", "复制原始编辑到当前配置 Draft 只会更新 Draft，不会发送设备命令。"),
            ("ScanDebug_IlluminationSessionTestHelpText.Text", "Tests send illumination commands to the device immediately, but do not change acquisition settings. Turn the lights off when done.", "测试会立即向设备发出照明命令；不会修改采集配置。结束测试时请关闭照明。"),
            ("ScanDebug_IlluminationPulseClockHardwareHint.Text", "Pulse clocks are raw hardware cycles/clocks; no physical-time conversion is applied here.", "脉冲时钟数是原始硬件 cycles/clocks；此处不进行物理时间换算。"),
            ("ScanDebug_RawIlluminationValidationTitle.Text", "Raw validation", "原始值验证"),
            ("ScanDebug_IlluminationDeviceLevelLabel.Text", "Current device level", "设备当前电平"),
            ("ScanDebug_IlluminationDevicePulseClockLabel.Text", "Current pulse clocks", "设备当前脉冲时钟数"),
            ("ScanDebug_DeviceLed1LevelText.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED1 current device level", "LED1 设备当前电平"),
            ("ScanDebug_DeviceLed1PulseClockText.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED1 current device pulse clocks", "LED1 设备当前脉冲时钟数"),
            ("ScanDebug_DeviceLed2LevelText.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED2 current device level", "LED2 设备当前电平"),
            ("ScanDebug_DeviceLed2PulseClockText.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED2 current device pulse clocks", "LED2 设备当前脉冲时钟数"),
            ("ScanDebug_DeviceLed3LevelText.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED3 current device level", "LED3 设备当前电平"),
            ("ScanDebug_DeviceLed3PulseClockText.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED3 current device pulse clocks", "LED3 设备当前脉冲时钟数"),
            ("ScanDebug_DeviceLed4LevelText.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED4 current device level", "LED4 设备当前电平"),
            ("ScanDebug_DeviceLed4PulseClockText.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED4 current device pulse clocks", "LED4 设备当前脉冲时钟数"),
            ("ScanDebug_DeviceLed1SteadyState.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED1 device steady mode", "LED1 设备常亮模式"),
            ("ScanDebug_DeviceLed1SyncState.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED1 device acquisition-sync mode", "LED1 设备采集同步模式"),
            ("ScanDebug_DeviceLed2SteadyState.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED2 device steady mode", "LED2 设备常亮模式"),
            ("ScanDebug_DeviceLed2SyncState.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED2 device acquisition-sync mode", "LED2 设备采集同步模式"),
            ("ScanDebug_DeviceLed3SteadyState.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED3 device steady mode", "LED3 设备常亮模式"),
            ("ScanDebug_DeviceLed3SyncState.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED3 device acquisition-sync mode", "LED3 设备采集同步模式"),
            ("ScanDebug_DeviceLed4SteadyState.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED4 device steady mode", "LED4 设备常亮模式"),
            ("ScanDebug_DeviceLed4SyncState.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", "LED4 device acquisition-sync mode", "LED4 设备采集同步模式"),
            ("ScanDebug_IlluminationRawLevelTextBox.Header", "Raw edit level", "原始编辑电平"),
            ("ScanDebug_IlluminationRawPulseClockTextBox.Header", "Raw edit pulse clocks", "原始编辑脉冲时钟数"),
            ("ScanDebug_TestIlluminationOffButton.Content", "All off (session)", "全部关闭（会话）"),
            ("ScanDebug_TestIlluminationSteadyButton.Content", "Steady test", "常亮测试"),
            ("ScanDebug_TestIlluminationAcquisitionSyncButton.Content", "Acquisition-sync test", "采集同步测试"),
            ("ScanDebug_ApplyIlluminationButton.Content", "Apply raw edits", "应用原始编辑"),
            ("ScanDebug_CopyRawIlluminationToDraftButton.Content", "Copy raw edits to current profile Draft", "复制原始编辑到当前配置 Draft")
        };

        foreach (var (key, expectedEnglish, expectedChinese) in expected)
        {
            Assert.Equal(expectedEnglish, english[key]);
            Assert.Equal(expectedChinese, chinese[key]);
        }

        var expectedDeviceModeNameKeys = expected
            .Where(item => item.Key.Contains("State.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name", StringComparison.Ordinal))
            .Select(item => item.Key)
            .ToArray();
        Assert.Equal(8, expectedDeviceModeNameKeys.Length);
        Assert.Equal(8, expectedDeviceModeNameKeys.Select(key => english[key]).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(8, expectedDeviceModeNameKeys.Select(key => chinese[key]).Distinct(StringComparer.Ordinal).Count());

        var engineeringUids = Regex.Matches(engineering, "x:Uid=\"(?<uid>[^\"]+)\"")
            .Select(match => match.Groups["uid"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var uid in engineeringUids)
        {
            Assert.Contains(english.Keys, key => key.StartsWith(uid + ".", StringComparison.Ordinal));
            Assert.Contains(chinese.Keys, key => key.StartsWith(uid + ".", StringComparison.Ordinal));
        }

        var hardCodedVisibleEnglish = Regex.Matches(engineering, "\\b(?:Text|Header|Content|PlaceholderText)=\"(?<value>[^\"]*[A-Za-z][^\"]*)\"")
            .Select(match => match.Groups["value"].Value)
            .Where(value => !value.StartsWith('{'))
            .ToArray();

        Assert.Empty(hardCodedVisibleEnglish);
    }

    [Fact]
    public void Todo10SourceContract_AcquisitionChannelSelectionResynchronizesDraft()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var selection = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "SetAcquisitionChannelSelection");

        Assert.Contains("NotifyAcquisitionPlanChanged();", selection, StringComparison.Ordinal);
        Assert.Contains("SynchronizeFilmProfileDraftFromInputs();", selection, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo10LocalizationContract_AcquisitionAndEngineeringCardsArePaired()
    {
        var english = FilmProfileContractSource.ReadResources("en-us");
        var chinese = FilmProfileContractSource.ReadResources("zh-CN");
        var expected = new (string Key, string English, string Chinese)[]
        {
            ("ScanDebug_FilmProfileWorkbenchAcquisitionSetupTitle.Text", "Acquisition setup", "采集设置"),
            ("ScanDebug_FilmProfileWorkbenchAcquisitionSetupDescription.Text", "Plan and channel settings belong to the draft; transport settings apply only to that capture mode.", "计划和通道设置属于配置草稿；传送设置仅用于对应采集方式。"),
            ("ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugTitle.Text", "Engineering tools", "工程工具"),
            ("ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugDescription.Text", "Illumination tests and raw-value application can act on the device immediately. Follow the hardware validation procedure.", "照明测试与原始值应用可立即作用于设备；仅按硬件验证流程操作。")
        };

        foreach (var (key, expectedEnglish, expectedChinese) in expected)
        {
            Assert.Equal(expectedEnglish, english[key]);
            Assert.Equal(expectedChinese, chinese[key]);
        }
    }

    [Fact]
    public void Todo11Baseline_CurrentCalibrationCommandBindingNameAndHandlerInventoryIsExplicitAfterMigration()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = FilmProfileContractSource.ReadScanDebugPageCodeBehindSource();
        var channel = ExtractNamedRegion(xaml, "ChannelCalibrationSection", "LiveCalibrationSection");
        var engineering = ExtractNamedRegion(xaml, "EngineeringToolsSection", "WorkbenchPreviewColumnContent");

        Assert.Equal(ExpectedNamedElements.Order(StringComparer.Ordinal), FilmProfileContractSource.GetXamlNames(xaml));
        Assert.Equal(ExpectedCommandBindingCounts.OrderBy(pair => pair.Key), FilmProfileContractSource.GetXamlCommandBindingCounts(xaml));
        Assert.Equal(ExpectedEventHandlerBindingCounts.OrderBy(pair => pair.Key), GetScanDebugEventHandlerBindingCounts(xaml).OrderBy(pair => pair.Key));

        Assert.Contains("Visibility=\"Collapsed\"", GetOpeningTag(xaml, "ChannelCalibrationSection"), StringComparison.Ordinal);
        Assert.Contains("SaveChannelProfileCommand", channel, StringComparison.Ordinal);
        Assert.Contains("ClearChannelProfileCommand", channel, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.CalibrationChannelOptions, Mode=OneWay}\"", channel, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{x:Bind ViewModel.SelectedCalibrationChannel, Mode=OneWay}\"", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChannelProfileCommand", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("ClearChannelProfileCommand", engineering, StringComparison.Ordinal);

        foreach (var required in new[]
        {
            "Command=\"{x:Bind ViewModel.SaveChannelProfileCommand}\"",
            "Command=\"{x:Bind ViewModel.ClearChannelProfileCommand}\"",
            "Command=\"{x:Bind ViewModel.ApplyDeviceClockCommand}\"",
            "Command=\"{x:Bind ViewModel.ApplyParametersCommand}\"",
            "Command=\"{x:Bind ViewModel.AutoBlackAdjustCommand}\"",
            "Command=\"{x:Bind ViewModel.AutoWhiteAdjustCommand}\"",
            "Command=\"{x:Bind ViewModel.AutoCalibrateCommand}\"",
            "IsEnabled=\"{x:Bind ViewModel.CanRunAutoFocusAction, Mode=OneWay}\"",
            "Click=\"AutoFocusButton_Click\"",
            "Command=\"{x:Bind ViewModel.SaveColumnSampleAsBlackLevelCommand}\"",
            "Command=\"{x:Bind ViewModel.SaveColumnSampleAsWhiteLevelCommand}\"",
            "x:Name=\"CurrentCalibrationIlluminationEditorGrid\"",
            "x:Name=\"CurrentCalibrationIlluminationLevelTextBox\"",
            "x:Name=\"CurrentCalibrationIlluminationPulseClockTextBox\"",
            "x:Name=\"CurrentCalibrationIlluminationWorkModeComboBox\"",
            "TextChanged=\"CurrentCalibrationIlluminationLevelTextBox_TextChanged\"",
            "TextChanged=\"CurrentCalibrationIlluminationPulseClockTextBox_TextChanged\"",
            "SelectionChanged=\"CurrentCalibrationIlluminationWorkModeComboBox_SelectionChanged\""
        })
        {
            Assert.Equal(1, CountOccurrences(xaml, required));
        }

        foreach (var handler in new[]
        {
            "private void CurrentCalibrationIlluminationLevelTextBox_TextChanged",
            "private void CurrentCalibrationIlluminationPulseClockTextBox_TextChanged",
            "private void CurrentCalibrationIlluminationWorkModeComboBox_SelectionChanged"
        })
        {
            Assert.Equal(1, CountOccurrences(codeBehind, handler));
        }

        var viewModel = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var clearLibrary = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(viewModel, "ClearChannelProfile");
        Assert.Contains("TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ClearChannelProfile", clearLibrary, StringComparison.Ordinal);
        Assert.Contains("RequestCalibrationPromptAsync(new ScanCalibrationPrompt(", clearLibrary, StringComparison.Ordinal);
        Assert.Contains("_calibrationProfiles.ClearProfileAsync(channelRole", clearLibrary, StringComparison.Ordinal);
        Assert.True(clearLibrary.IndexOf("RequestCalibrationPromptAsync", StringComparison.Ordinal)
            < clearLibrary.IndexOf("ClearProfileAsync", StringComparison.Ordinal));
        var clearManual = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(viewModel, "ClearManualReferenceLevels");
        Assert.Contains("RequestFilmProfileImportConfirmationAsync(new ScanFilmProfileDiscardConfirmationRequest(", clearManual, StringComparison.Ordinal);
        Assert.True(clearManual.IndexOf("RequestFilmProfileImportConfirmationAsync", StringComparison.Ordinal)
            < clearManual.IndexOf("CommitManualReferenceLevels(null, null);", StringComparison.Ordinal));
        AssertCurrentRuntimeGatedCommandInventory(xaml, codeBehind, viewModel);
    }

    [Fact]
    public void Todo11DesiredContract_ChannelCalibrationSectionOwnsVisibleStatusListStableEditorAndNoLossInventory()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var channel = ExtractNamedRegion(xaml, "ChannelCalibrationSection", "LiveCalibrationSection");
        var engineering = ExtractNamedRegion(xaml, "EngineeringToolsSection", "WorkbenchPreviewColumnContent");

        foreach (var name in new[]
        {
            "CalibrationChannelStatusListView",
            "CalibrationChannelFallbackComboBox",
            "ChannelCalibrationLibraryActionsGrid",
            "ChannelCalibrationPrimaryActions",
            "ChannelCalibrationActionsCard",
            "ColumnSampleUseStack",
            "ColumnSampleSaveActions",
            "ChannelCalibrationIlluminationCard",
            "CurrentIlluminationDraftLabel",
            "CurrentCalibrationIlluminationEditorGrid",
            "CurrentCalibrationIlluminationLevelTextBox",
            "CurrentCalibrationIlluminationPulseClockTextBox",
            "CurrentCalibrationIlluminationWorkModeComboBox"
        })
        {
            Assert.Equal(1, CountOccurrences(channel, $"x:Name=\"{name}\""));
        }

        foreach (var binding in new[]
        {
            "ItemsSource=\"{x:Bind ViewModel.CalibrationChannelItems, Mode=OneWay}\"",
            "AutomationProperties.Name=\"{Binding AccessibilityText}\"",
            "Text=\"{Binding DisplayName}\"",
            "Text=\"{Binding StatusText}\"",
            "Text=\"{Binding LedMappingText}\"",
            "Text=\"{Binding StatusIconGlyph}\"",
            "ItemsSource=\"{x:Bind ViewModel.CalibrationChannelOptions, Mode=OneWay}\"",
            "SelectedItem=\"{x:Bind ViewModel.SelectedCalibrationChannel, Mode=OneWay}\"",
            "Command=\"{x:Bind ViewModel.SaveChannelProfileCommand}\"",
            "Command=\"{x:Bind ViewModel.ClearChannelProfileCommand}\"",
            "ItemsSource=\"{x:Bind ViewModel.CalibrationCopySourceChannelOptions, Mode=OneWay}\"",
            "SelectedItem=\"{x:Bind ViewModel.SelectedCalibrationCopySourceChannel, Mode=TwoWay}\"",
            "Command=\"{x:Bind ViewModel.CopyCalibrationProfileFromChannelCommand}\"",
            "ColumnSpacing=\"{StaticResource ScanDebugInlineSpacing}\"",
            "RowSpacing=\"{StaticResource ScanDebugInlineSpacing}\"",
            "HorizontalScrollBarVisibility=\"Disabled\""
        })
        {
            Assert.Contains(binding, channel, StringComparison.Ordinal);
        }

        Assert.Contains("SelectionMode=\"None\"", GetOpeningTag(channel, "CalibrationChannelStatusListView"), StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedItem=\"{x:Bind ViewModel.SelectedCalibrationChannelItem, Mode=TwoWay}\"", channel, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{x:Bind ViewModel.SelectedCalibrationChannel, Mode=OneWay}\"", GetOpeningTag(channel, "CalibrationChannelFallbackComboBox"), StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_ChannelCalibrationLibraryHelpText\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CalibrationCopySourceComboBox\"", channel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"CalibrationCopySourceComboBox\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_CalibrationCopySourceComboBox\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CopyCalibrationProfileButton\"", channel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"CopyCalibrationProfileButton\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_CopyCalibrationProfileButton\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_CalibrationCopyHelperText\"", channel, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"WrapWholeWords\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_SaveChannelButton\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_ClearChannelButton\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"ScanDebugWrappingButtonContentTemplate\"", xaml, StringComparison.Ordinal);
        var document = XDocument.Parse(xaml);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var channelElement = Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == "ChannelCalibrationSection");
        var adcReload = Assert.Single(channelElement.Descendants(), element => (string?)element.Attribute(x + "Uid") == "ScanDebug_ReloadAdcRoiInputsButton");
        Assert.Equal("{x:Bind ViewModel.ReloadAdcRoiInputsCommand}", (string?)adcReload.Attribute("Command"));
        Assert.Contains(adcReload, channelElement.Descendants().Single(element => (string?)element.Attribute(x + "Name") == "AdcRoiEditorCard").Descendants());
        Assert.Equal("{StaticResource ScanDebugWrappingButtonContentTemplate}", (string?)adcReload.Attribute("ContentTemplate"));
        var copyCalibration = Assert.Single(channelElement.Descendants(), element => (string?)element.Attribute(x + "Name") == "CopyCalibrationProfileButton");
        Assert.Equal("{StaticResource ScanDebugWrappingButtonContentTemplate}", (string?)copyCalibration.Attribute("ContentTemplate"));
        Assert.Equal(19, channelElement.Descendants().Count(element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("ContentTemplate") == "{StaticResource ScanDebugWrappingButtonContentTemplate}"
            && element != adcReload));
        Assert.Contains("<DataTemplate x:Key=\"ScanDebugWrappingButtonContentTemplate\">", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"WrapWholeWords\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("<FontIcon", channel, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"{ThemeResource SymbolThemeFontFamily}\"", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChannelProfileCommand", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("ClearChannelProfileCommand", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentCalibrationIlluminationEditorGrid", engineering, StringComparison.Ordinal);
        var page = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var arrange = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "ArrangeBlackWhiteTaskControls");
        Assert.Contains("ChannelCalibrationTaskStack.Children.Remove(ChannelCalibrationIlluminationCard);", arrange, StringComparison.Ordinal);
        Assert.Matches(@"ChannelCalibrationTaskStack\.Children\.Insert\(\s*ChannelCalibrationTaskStack\.Children\.IndexOf\(ChannelCalibrationDetailsGrid\),\s*ChannelCalibrationIlluminationCard\)", arrange);
        Assert.DoesNotContain("ChannelCalibrationPrimaryActions.Children.Add(ChannelCalibrationIlluminationCard);", arrange, StringComparison.Ordinal);
        Assert.Contains("ColumnSampleUseStack.Children.Insert(", arrange, StringComparison.Ordinal);
        Assert.Contains("ColumnSampleSaveActions);", arrange, StringComparison.Ordinal);
        Assert.DoesNotContain("Command.Execute", arrange, StringComparison.Ordinal);
        var entry = Assert.Single(channelElement.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == "BwDetailsEntryButton");
        Assert.Equal("BwOpenChannelDetailsButton_Click", (string?)entry.Attribute("Click"));
        Assert.Null(entry.Attribute("Command"));
        var openDetails = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(page, "BwConfigureChannelButton_Click");
        Assert.Contains("_isAuxiliaryPanelOpen = true;", openDetails, StringComparison.Ordinal);
        Assert.Contains("ChannelCalibrationDetailsExpander.IsExpanded = true;", openDetails, StringComparison.Ordinal);
        Assert.Contains("BeginIssueTargetFocus(2, ChannelCalibrationScrollViewer, ExposureMicrosecondsTextBox", openDetails, StringComparison.Ordinal);
        Assert.Contains("BwConfigureChannelButton_Click(sender, e)", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage02SourceContract_ManualReferenceCardEditsChannelDraftWithoutReplacingLegacySampleSaves()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var channel = Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == "ChannelCalibrationSection");
        var card = Assert.Single(channel.Descendants(), element => (string?)element.Attribute(x + "Name") == "ManualReferenceLevelsCard");
        Assert.Equal("ManualReferenceLevelsCard", (string?)card.Attribute("AutomationProperties.AutomationId"));

        const string wrappingTemplate = "{StaticResource ScanDebugWrappingButtonContentTemplate}";
        var wrappedButtons = channel.Descendants().Where(element => element.Name.LocalName == "Button" && (string?)element.Attribute("ContentTemplate") == wrappingTemplate).ToArray();
        var supplement = Assert.Single(channel.Descendants(), element => (string?)element.Attribute(x + "Uid") == "ScanDebug_ManualReferenceSupplementExpander");
        var manualButtons = card.Descendants().Concat(supplement.Descendants())
            .Where(element => element.Name.LocalName == "Button" && element.Attribute("Command") is not null).ToArray();
        var adcReload = Assert.Single(channel.Descendants(), element => (string?)element.Attribute(x + "Uid") == "ScanDebug_ReloadAdcRoiInputsButton");
        Assert.Equal("{x:Bind ViewModel.ReloadAdcRoiInputsCommand}", (string?)adcReload.Attribute("Command"));
        Assert.Contains(adcReload, wrappedButtons);
        Assert.Equal(14, wrappedButtons.Except(manualButtons).Except([adcReload]).Count());
        Assert.Equal(5, manualButtons.Length);
        Assert.Equal(19, wrappedButtons.Except([adcReload]).Count());
        Assert.Equal(2, card.Descendants().Count(element => element.Name.LocalName == "Button"));
        Assert.Equal(3, supplement.Descendants().Count(element => element.Name.LocalName == "Button" && element.Attribute("Command") is not null));
        Assert.Contains(supplement.Descendants(), element => (string?)element.Attribute(x + "Uid") == "ScanDebug_ReferenceHelpButton"
            && element.Descendants().Any(child => child.Name.LocalName == "Flyout"));
        var automatic = Assert.Single(channel.Descendants(), element => (string?)element.Attribute(x + "Name") == "AutoCalibrationActions");
        var missingProfile = Assert.Single(automatic.Descendants(), element => (string?)element.Attribute(x + "Name") == "MissingCalibrationProfileEntry");
        var configure = Assert.Single(missingProfile.Descendants(), element => (string?)element.Attribute(x + "Uid") == "ScanDebug_BwConfigureChannelButton");
        Assert.Equal("BwConfigureChannelButton_Click", (string?)configure.Attribute("Click"));
        Assert.Null(configure.Attribute("Command"));
        Assert.Equal("{x:Bind ViewModel.MissingCalibrationProfileVisibility, Mode=OneWay}", (string?)missingProfile.Attribute("Visibility"));
        var inspection = Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == "WorkbenchInspectionRail");
        Assert.DoesNotContain(automatic.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{x:Bind ViewModel.FilmProfileHardwareUnavailableReasonText, Mode=OneWay}");
        Assert.Contains(inspection.Descendants(), element => element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{x:Bind ViewModel.DeviceEvidenceText, Mode=OneWay}");
        Assert.Contains(card.Parent!.Descendants(), element => (string?)element.Attribute("AutomationProperties.AutomationId") == "CalibrationGuideTitle"
            && (string?)element.Attribute("Text") == "{x:Bind ViewModel.CalibrationGuideTitle, Mode=OneWay}");
        Assert.Equal("False", (string?)supplement.Attribute("IsExpanded"));

        foreach (var (uid, automationId, command) in new[]
        {
            ("ScanDebug_UseSampleAsManualBlackLevelButton", "UseSampleAsManualBlackLevelButton", "UseColumnSampleAsManualBlackLevelCommand"),
            ("ScanDebug_UseSampleAsManualWhiteLevelButton", "UseSampleAsManualWhiteLevelButton", "UseColumnSampleAsManualWhiteLevelCommand"),
            ("ScanDebug_ApplyManualReferenceLevelsButton", "ApplyManualReferenceLevelsButton", "ApplyManualReferenceLevelsCommand"),
            ("ScanDebug_RevertManualReferenceLevelsButton", "RevertManualReferenceLevelsButton", "RevertManualReferenceLevelsCommand"),
            ("ScanDebug_ClearManualReferenceLevelsButton", "ClearManualReferenceLevelsButton", "ClearManualReferenceLevelsCommand")
        })
        {
            var button = Assert.Single(manualButtons, element => (string?)element.Attribute(x + "Uid") == uid);
            Assert.Equal(automationId, (string?)button.Attribute("AutomationProperties.AutomationId"));
            Assert.Equal($"{{x:Bind ViewModel.{command}}}", (string?)button.Attribute("Command"));
            Assert.Equal(wrappingTemplate, (string?)button.Attribute("ContentTemplate"));
            Assert.Equal(uid is "ScanDebug_ApplyManualReferenceLevelsButton" or "ScanDebug_RevertManualReferenceLevelsButton",
                card.Descendants().Contains(button));
        }

        Assert.Equal(2, card.Descendants().Count(element => element.Name.LocalName == "TextBox"));
        Assert.DoesNotContain(supplement.Descendants(), element => element.Name.LocalName == "TextBox");
        foreach (var (name, uid, input) in new[]
        {
            ("ManualBlackLevelTextBox", "ScanDebug_ManualBlackLevelTextBox", "ManualBlackLevelInput"),
            ("ManualWhiteLevelTextBox", "ScanDebug_ManualWhiteLevelTextBox", "ManualWhiteLevelInput")
        })
        {
            var textBox = Assert.Single(card.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
            Assert.Equal(uid, (string?)textBox.Attribute(x + "Uid"));
            Assert.Equal(name, (string?)textBox.Attribute("AutomationProperties.AutomationId"));
            Assert.Equal($"{{x:Bind ViewModel.{input}, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}", (string?)textBox.Attribute("Text"));
        }

        foreach (var (automationId, property) in new[]
        {
            ("ManualReferenceSampleDisabledReasonText", "ManualReferenceSampleDisabledReasonText"),
            ("ManualReferenceSourceText", "ManualReferenceSourceText"),
            ("ManualReferenceStatusText", "ManualReferenceStatusText"),
            ("ManualReferenceDisabledReasonText", "ManualReferenceDisabledReasonText")
        })
        {
            var status = Assert.Single(card.Descendants().Concat(supplement.Descendants()), element =>
                (string?)element.Attribute("AutomationProperties.AutomationId") == automationId);
            Assert.Equal("TextBlock", status.Name.LocalName);
            Assert.Equal($"{{x:Bind ViewModel.{property}, Mode=OneWay}}", (string?)status.Attribute("Text"));
        }
        var selector = Assert.Single(channel.Descendants(), element => (string?)element.Attribute(x + "Name") == "CalibrationChannelFallbackComboBox");
        Assert.Equal("{x:Bind ViewModel.ManualReferenceChannelText, Mode=OneWay}", (string?)selector.Attribute("AutomationProperties.HelpText"));
        Assert.Single(selector.Parent!.Elements(), element =>
            element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{x:Bind ViewModel.CurrentCalibrationLedName, Mode=OneWay}");

        var autoActions = new[]
        {
            ("ScanDebug_AutoBlackButton", "AutoBlackAdjustCommand"),
            ("ScanDebug_AutoWhiteButton", "AutoWhiteAdjustCommand"),
            ("ScanDebug_AutoCalibrateButton", "AutoCalibrateCommand")
        }.Select(action =>
        {
            var button = Assert.Single(channel.Descendants(), element => (string?)element.Attribute(x + "Uid") == action.Item1);
            Assert.Equal($"{{x:Bind ViewModel.{action.Item2}}}", (string?)button.Attribute("Command"));
            Assert.DoesNotContain(button, card.Descendants());
            Assert.DoesNotContain(button, supplement.Descendants());
            return button;
        }).ToArray();
        var autoActionGrid = Assert.Single(channel.Descendants(), element => (string?)element.Attribute(x + "Name") == "BwAutoActionGrid");
        Assert.Equal("Grid", autoActionGrid.Name.LocalName);
        Assert.Equal(2, autoActionGrid.Elements().Single(element => element.Name.LocalName == "Grid.ColumnDefinitions").Elements().Count());
        Assert.Contains(autoActions[0], autoActionGrid.Elements());
        Assert.Contains(autoActions[1], autoActionGrid.Elements());
        Assert.Equal("1", (string?)autoActions[1].Attribute("Grid.Column"));
        Assert.Same(autoActionGrid.Parent, autoActions[2].Parent);
        Assert.Same(card.Parent, autoActionGrid.Parent!.Parent);
        var editingOrder = channel.Descendants().ToList();
        Assert.True(editingOrder.IndexOf(autoActions[2]) < editingOrder.IndexOf(autoActions[0]));
        Assert.True(editingOrder.IndexOf(autoActions[1]) < editingOrder.IndexOf(card));
        Assert.True(editingOrder.IndexOf(card) < editingOrder.IndexOf(supplement));

        foreach (var (uid, command) in new[]
        {
            ("ScanDebug_SaveColumnBlackLevelButton", "SaveColumnSampleAsBlackLevelCommand"),
            ("ScanDebug_SaveColumnWhiteLevelButton", "SaveColumnSampleAsWhiteLevelCommand")
        })
        {
            var legacySave = Assert.Single(channel.Descendants(), element => element.Name.LocalName == "Button" && (string?)element.Attribute(x + "Uid") == uid);
            Assert.DoesNotContain(legacySave, card.Descendants());
            Assert.Equal($"{{x:Bind ViewModel.{command}}}", (string?)legacySave.Attribute("Command"));
        }
    }

    [Fact]
    public void Stage02LocalizationContract_ManualDraftActionsAndLegacySampleSaveScopeStayDistinctInBothLocales()
    {
        var document = XDocument.Parse(ReadAppSource("Views", "ScanDebugPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var card = Assert.Single(document.Descendants(), element => (string?)element.Attribute(x + "Name") == "ManualReferenceLevelsCard");
        var english = FilmProfileContractSource.ReadResources("en-us");
        var chinese = FilmProfileContractSource.ReadResources("zh-CN");

        foreach (var uid in card.Descendants().Attributes(x + "Uid").Select(attribute => attribute.Value).Distinct(StringComparer.Ordinal))
        {
            Assert.Contains(english.Keys, key => key.StartsWith(uid + ".", StringComparison.Ordinal));
            Assert.Contains(chinese.Keys, key => key.StartsWith(uid + ".", StringComparison.Ordinal));
        }

        foreach (var (key, expectedEnglish, expectedChinese) in new[]
        {
            ("ScanDebug_ManualReferenceLevelsTitle.Text", "Channel black/white reference (RAW 16-bit ADU)", "通道黑白参考值（RAW 16 位 ADU）"),
            ("ScanDebug_ManualReferenceLevelsHelpText.Text", "Black: 0–65535; white: 1–65535. Use integers with black below white. After editing or filling a sample, choose Update reference levels.", "黑值：0–65535；白值：1–65535，使用整数且黑值小于白值。编辑或填入采样值后，点击“更新参考值”。"),
            ("ScanDebug_UseSampleAsManualBlackLevelButton.Content", "Fill black input from sample", "使用采样填入黑值"),
            ("ScanDebug_UseSampleAsManualWhiteLevelButton.Content", "Fill white input from sample", "使用采样填入白值"),
            ("ScanDebug_ApplyManualReferenceLevelsButton.Content", "Update reference levels", "更新参考值"),
            ("ScanDebug_RevertManualReferenceLevelsButton.Content", "Revert this edit", "撤销本次编辑"),
            ("ScanDebug_ClearManualReferenceLevelsButton.Content", "Clear channel references...", "清除通道参考值…"),
            ("ScanDebug_ManualReferenceClearHelpText.Text", "Clear this channel's black and white reference levels to enter or sample new values.", "清除当前通道的黑白参考值，随后可重新输入或采样。"),
            ("ScanDebug_SaveColumnBlackLevelButton.Content", "Save black level to channel preset", "保存黑电平到通道预设"),
            ("ScanDebug_SaveColumnWhiteLevelButton.Content", "Save white level to channel preset", "保存白电平到通道预设")
        })
        {
            Assert.Equal(expectedEnglish, english[key]);
            Assert.Equal(expectedChinese, chinese[key]);
        }
    }

    [Fact]
    public void VisualQaCaptureContract_ChannelCalibrationExposesStableScrollCardsGutterAndRejectsHiddenFocusSinks()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var channel = ExtractNamedRegion(xaml, "ChannelCalibrationSection", "LiveCalibrationSection");

        Assert.Contains("x:Name=\"ChannelCalibrationRail\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ChannelCalibrationScrollViewer\"", channel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"ChannelCalibrationScrollViewer\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ChannelCalibrationIssuePanel\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_BwIssueList\"", GetOpeningTag(channel, "ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer"), StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BwRoiSummary\"", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxHeight=", GetOpeningTag(channel, "ChannelCalibrationCurrentFilmProfileValidationIssueScrollViewer"), StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", channel, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,0,12,0\"", channel, StringComparison.Ordinal);

        foreach (var automationId in new[]
        {
            "ChannelCalibrationStatusCard",
            "ChannelCalibrationLibraryCard",
            "ChannelCalibrationParametersCard",
            "ChannelCalibrationIlluminationCard",
            "ChannelCalibrationActionsCard",
            "PendingCalibrationCandidateReviewCard"
        })
        {
            Assert.Equal(1, CountOccurrences(channel, $"AutomationProperties.AutomationId=\"{automationId}\""));
        }

        Assert.DoesNotContain("x:Name=\"ScreenshotFocusSink\"", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ScreenshotFocusSink\"", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("AutomationProperties.Name=\"Screenshot neutral focus sink\"", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("ScreenshotFocusSink", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("Screenshot neutral focus sink", channel, StringComparison.Ordinal);
        Assert.Empty(GetHiddenFocusableArtifactOpeningTags(channel));
    }

    [Fact]
    public void VisualQaCaptureContract_HiddenFocusSinkDetectorRejectsCompoundArtifactsAndAllowsGenericAttributes()
    {
        var representativeRenamedSink = """
            <Button x:Name="RenamedFocusSinkProbe"
                    AutomationProperties.AutomationId="RenamedFocusSinkProbe"
                    AutomationProperties.Name="Renamed neutral focus sink"
                    IsTabStop="True"
                    Width="1"
                    Height="1"
                    Opacity="0"
                    FocusVisualPrimaryThickness="0"
                    FocusVisualSecondaryThickness="0"/>
            """;

        Assert.Single(GetHiddenFocusableArtifactOpeningTags(representativeRenamedSink));

        foreach (var permittedElement in new[]
        {
            "<Button IsTabStop=\"True\"/>",
            "<Rectangle Opacity=\"0\"/>",
            "<ColumnDefinition Width=\"1\"/>",
            "<RowDefinition Height=\"1\"/>",
            "<Button FocusVisualPrimaryThickness=\"0\"/>",
            "<Button FocusVisualSecondaryThickness=\"0\"/>"
        })
        {
            Assert.Empty(GetHiddenFocusableArtifactOpeningTags(permittedElement));
        }
    }

    [Fact]
    public void VisualQaCaptureContract_ChannelLibraryActionsUseSharedVisibleKeyboardFocusStyle()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var channel = ExtractNamedRegion(xaml, "ChannelCalibrationSection", "LiveCalibrationSection");

        Assert.Contains("x:Key=\"ScanDebugChannelLibraryActionButtonStyle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BasedOn=\"{StaticResource DefaultButtonStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FocusVisualPrimaryBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("FocusVisualPrimaryThickness", xaml, StringComparison.Ordinal);
        Assert.Contains("FocusVisualSecondaryThickness", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"SaveChannelLibraryActionButton\"", channel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"ClearChannelLibraryActionButton\"", channel, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(channel, "Style=\"{StaticResource ScanDebugChannelLibraryActionButtonStyle}\""));
    }

    [Fact]
    public void Todo15SourceContract_PendingCalibrationCandidateReviewCardIsLocalizedAccessibleAndUnambiguous()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var viewModel = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var channel = ExtractNamedRegion(xaml, "ChannelCalibrationSection", "LiveCalibrationSection");
        var review = XDocument.Parse(xaml).Descendants().Single(element =>
            (string?)element.Attribute("AutomationProperties.AutomationId") == "CalibrationCandidateReviewPanel").ToString();

        foreach (var required in new[]
        {
            "AutomationProperties.AutomationId=\"PendingCalibrationCandidateReviewCard\"",
            "AutomationProperties.AccessibilityView=\"Control\"",
            "Visibility=\"{x:Bind ViewModel.PendingCalibrationCandidateReviewVisibility, Mode=OneWay}\"",
            "x:Uid=\"ScanDebug_PendingCalibrationCandidateReviewTitle\"",
            "x:Uid=\"ScanDebug_PendingCalibrationCandidateReviewHelperText\"",
            "Text=\"{x:Bind ViewModel.PendingCalibrationSelectedChannelText, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.PendingCalibrationAdcDifferenceText, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.PendingCalibrationBlackDeviationText, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.PendingCalibrationSaturationText, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.PendingCalibrationNoiseText, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.PendingCalibrationValidationText, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.PendingCalibrationValidationIssuesText, Mode=OneWay}\"",
            "Command=\"{x:Bind ViewModel.AcceptPendingCalibrationCommand}\"",
            "Command=\"{x:Bind ViewModel.AcceptAndSavePendingCalibrationCommand}\"",
            "Command=\"{x:Bind ViewModel.RestorePendingCalibrationCommand}\"",
            "Command=\"{x:Bind ViewModel.CancelPendingCalibrationCommand}\"",
            "AutomationProperties.AutomationId=\"PendingCalibrationAcceptDeviceOnlyButton\"",
            "AutomationProperties.AutomationId=\"PendingCalibrationAcceptAndSaveButton\"",
            "AutomationProperties.AutomationId=\"PendingCalibrationRestoreOriginalButton\"",
            "AutomationProperties.AutomationId=\"PendingCalibrationCancelButton\"",
            "TextWrapping=\"WrapWholeWords\""
        })
        {
            Assert.Contains(required, review, StringComparison.Ordinal);
        }

        Assert.Contains("Style=\"{StaticResource AccentButtonStyle}\"", review, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{x:Bind ViewModel.SaveChannelProfileCommand}\"", review, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{x:Bind ViewModel.SaveFilmProfileJsonCommand}\"", review, StringComparison.Ordinal);
        Assert.DoesNotContain("#", review, StringComparison.Ordinal);
        Assert.DoesNotContain("Color=\"", review, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"#", review, StringComparison.Ordinal);
        Assert.Contains("NotifyPropertyChangedFor(nameof(PendingCalibrationCandidateReviewVisibility))", viewModel, StringComparison.Ordinal);
        Assert.Contains("partial void OnPendingCalibrationResultChanged(PendingCalibrationResult? value)", viewModel, StringComparison.Ordinal);
        Assert.Contains("AcceptPendingCalibrationCommand.NotifyCanExecuteChanged();", viewModel, StringComparison.Ordinal);
        Assert.Contains("AcceptAndSavePendingCalibrationCommand.NotifyCanExecuteChanged();", viewModel, StringComparison.Ordinal);
        Assert.Contains("RestorePendingCalibrationCommand.NotifyCanExecuteChanged();", viewModel, StringComparison.Ordinal);
        Assert.Contains("CancelPendingCalibrationCommand.NotifyCanExecuteChanged();", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo11LocalizationContract_ChannelStatusAndCalibrationLibraryScopeArePaired()
    {
        var english = FilmProfileContractSource.ReadResources("en-us");
        var chinese = FilmProfileContractSource.ReadResources("zh-CN");
        var expected = new (string Key, string English, string Chinese)[]
        {
            ("ScanDebug_ChannelCalibrationStatusListTitle.Text", "Channel status", "通道状态"),
            ("ScanDebug_ChannelCalibrationStatusListHelpText.Text", "Select a channel to edit its parameters.", "选择通道以编辑参数。"),
            ("ScanDebug_ChannelCalibrationLibraryTitle.Text", "Channel presets", "通道预设"),
            ("ScanDebug_ChannelCalibrationLibraryHelpText.Text", "Save channel parameters here to reuse them in other configurations.", "保存当前通道参数，供其他配置复用。"),
            ("ScanDebug_Runtime_ChannelStatusSaved", "Calibration saved", "校准已保存"),
            ("ScanDebug_Runtime_ChannelStatusModified", "Calibration has unsaved changes", "校准有未保存更改"),
            ("ScanDebug_Runtime_ChannelStatusInvalid", "Parameters need correction", "参数待修正"),
            ("ScanDebug_Runtime_ChannelStatusMissing", "Missing calibration", "缺少校准"),
            ("ScanDebug_Runtime_ChannelStatusCopiedUnverified", "Copied; review needed", "已复制待检查"),
            ("ScanDebug_Runtime_ChannelStatusUnconfigured", "Missing calibration", "缺少校准"),
            ("ScanDebug_Runtime_ChannelStatusAccessibility", "{0}, {1}, {2}, {3}", "{0}，{1}，{2}，{3}"),
            ("ScanDebug_Runtime_ChannelStatusSelected", "selected", "已选择"),
            ("ScanDebug_Runtime_ChannelStatusNotSelected", "not selected", "未选择")
        };

        foreach (var (key, expectedEnglish, expectedChinese) in expected)
        {
            Assert.Equal(expectedEnglish, english[key]);
            Assert.Equal(expectedChinese, chinese[key]);
        }
    }

    [Fact]
    public void ScanDebugSourceContract_BaselineInventoryPreservesCommandsCodeBehindDependenciesAndPackages()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = FilmProfileContractSource.ReadScanDebugPageCodeBehindSource();
        var viewModel = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var project = ReadAppSource("PrismUtility.csproj");

        Assert.Equal(ExpectedNamedElements.Order(StringComparer.Ordinal), FilmProfileContractSource.GetXamlNames(xaml));
        Assert.Equal(ExpectedCommandBindingCounts.OrderBy(pair => pair.Key), FilmProfileContractSource.GetXamlCommandBindingCounts(xaml));
        Assert.Equal(ExpectedEventHandlerBindingCounts.OrderBy(pair => pair.Key), GetScanDebugEventHandlerBindingCounts(xaml).OrderBy(pair => pair.Key));
        Assert.Empty(FilmProfileContractSource.FindMissingTokens(ExpectedEventHandlerBindingCounts.Keys.Select(binding => binding[(binding.IndexOf('=') + 1)..]), FilmProfileContractSource.GetDeclaredMethodNames(codeBehind)));
        Assert.Equal(ExpectedPackageReferences.OrderBy(pair => pair.Key), FilmProfileContractSource.GetPackageReferences(project));
        Assert.DoesNotContain("CommunityToolkit.WinUI.Controls.Sizers", project, StringComparison.Ordinal);
        Assert.DoesNotContain("GridSplitter", xaml, StringComparison.Ordinal);
        AssertCurrentRuntimeGatedCommandInventory(xaml, codeBehind, viewModel);
    }

    [Fact]
    public void Todo17SourceContract_LiveFocusIsVisibleAdvancedAutofocusIsNestedAndMappingIsDeviceSettingsOnly()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var live = ExtractNamedRegion(xaml, "LiveCalibrationSection", "DeviceSettingsSection");
        var deviceSettings = ExtractNamedRegion(xaml, "DeviceSettingsSection", "EngineeringToolsSection");
        var engineering = ExtractNamedRegion(xaml, "EngineeringToolsSection", "WorkbenchPreviewColumnContent");
        var liveFocus = ExtractNamedRegion(live, "LiveFocusCard", "AdvancedAutofocusContent");
        var advancedStart = xaml.IndexOf("x:Name=\"AdvancedAutofocusContent\"", StringComparison.Ordinal);
        var advancedExpanderStart = xaml.LastIndexOf("<Expander IsExpanded=\"False\"", advancedStart, StringComparison.Ordinal);
        Assert.True(advancedExpanderStart >= 0, "Advanced autofocus content must be hosted by a default-collapsed expander.");
        var advanced = xaml[advancedExpanderStart..xaml.IndexOf("x:Name=\"DeviceSettingsSection\"", StringComparison.Ordinal)];
        var mappingStart = deviceSettings.IndexOf("x:Name=\"FocusMappingContent\"", StringComparison.Ordinal);
        Assert.True(mappingStart >= 0, "Device settings must contain FocusMappingContent.");
        var mapping = deviceSettings[mappingStart..];

        Assert.Contains("x:Name=\"LiveFocusCard\"", live, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"LiveFocusCard\"\r\n                                      x:Load=\"False\"", live, StringComparison.Ordinal);
        foreach (var uid in new[]
        {
            "ScanDebug_AutofocusTitle",
            "ScanDebug_QuickFocusButton",
            "ScanDebug_RunAutofocusButton",
            "ScanDebug_FineFocusButton",
            "ScanDebug_ManualFocusNegativeButton",
            "ScanDebug_ManualFocusPositiveButton",
            "ScanDebug_StopAllFocusButton"
        })
        {
            Assert.Contains($"x:Uid=\"{uid}\"", liveFocus, StringComparison.Ordinal);
        }

        Assert.Contains("<Expander IsExpanded=\"False\"", advanced, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"AdvancedAutofocusExpander\"", advanced, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_AdvancedAutofocusExpander\"", advanced, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_AdvancedAutofocusTitle\"", advanced, StringComparison.Ordinal);
        Assert.DoesNotContain("Expanding=\"DeferredExpander_Expanding\"", advanced, StringComparison.Ordinal);
        Assert.DoesNotContain("Tag=\"AdvancedAutofocusContent\"", advanced, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AdvancedAutofocusContent\"", advanced, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(xaml, "x:Name=\"AdvancedAutofocusResults\""));
        Assert.DoesNotContain("x:Load=\"False\"", advanced, StringComparison.Ordinal);
        foreach (var automationId in new[]
        {
            "AdvancedAutofocusBoundsText",
            "AutofocusInputLimitsText",
            "AutofocusResolutionLimitsText",
            "AutofocusEstimatedCapturesText",
            "AutofocusCumulativeTravelText",
            "AutofocusEstimatedDurationText",
            "AutofocusEstimateDisclaimer",
            "AutofocusPresetComboBox",
            "AutofocusSampleRowsTextBox",
            "AutofocusTiltProbeStepsTextBox",
            "AutofocusZProbeStepsTextBox",
            "AutofocusMotorIntervalUsTextBox",
            "AutofocusMaxTiltIterationsTextBox",
            "AutofocusMaxZIterationsTextBox"
        })
        {
            Assert.Contains($"AutomationProperties.AutomationId=\"{automationId}\"", advanced, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(advanced, $"AutomationProperties.AutomationId=\"{automationId}\""));
        }

        Assert.Contains("x:Uid=\"ScanDebug_AutofocusPresetComboBox\"", advanced, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", advanced, StringComparison.Ordinal);
        Assert.Contains("ItemTemplate=\"{StaticResource ScanAutofocusPresetStringOptionTemplate}\"", advanced, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.AutofocusPresetOptions, Mode=OneWay}\"", advanced, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{x:Bind ViewModel.SelectedAutofocusPresetDisplayName, Mode=TwoWay}\"", advanced, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{x:Bind ViewModel.SelectedAutofocusPresetDisplayName, Mode=OneWay}\"", advanced, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.AutofocusBoundsText, Mode=OneWay}\"", advanced, StringComparison.Ordinal);
        Assert.DoesNotContain("ScanDebug_QuickFocusButton", advanced, StringComparison.Ordinal);
        Assert.DoesNotContain("ScanDebug_StopAllFocusButton", advanced, StringComparison.Ordinal);

        Assert.Contains("x:Name=\"DeviceSettingsSection\"", deviceSettings, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"DeviceSettingsSection\"", GetOpeningTag(xaml, "DeviceSettingsSection"), StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DeviceClockContent\"", deviceSettings, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"DeviceClockContent\"", deviceSettings, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_SysClockMhzTextBox\"", deviceSettings, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"DeviceClockMhzTextBox\"", deviceSettings, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.ApplyDeviceClockCommand}\"", deviceSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"ScanDebug_SysClockMhzTextBox\"", live, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FocusMappingContent\"", deviceSettings, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(xaml, "x:Name=\"FocusMappingContent\""));
        Assert.Contains("x:Uid=\"ScanDebug_FocusMappingTitle\"", mapping, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_SaveFocusMappingButton\"", mapping, StringComparison.Ordinal);
        Assert.DoesNotContain("TestLeftFocusMappingCommand", mapping, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_TestLeftFocusMappingButton\"", engineering, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_TestRightFocusMappingButton\"", engineering, StringComparison.Ordinal);
        Assert.Contains("Click=\"OpenDevicePropertiesButton_Click\"", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("FocusMappingLeftMotorComboBox", live, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveFocusMappingCommand", live, StringComparison.Ordinal);
        Assert.DoesNotContain("FocusMappingLeftMotorComboBox", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveFocusMappingCommand", engineering, StringComparison.Ordinal);
    }

    [Fact]
    public void DeviceClockContent_CompactStatusAndRangeUseDeclaredRowsWithSingleValidationSurface()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var document = System.Xml.Linq.XDocument.Parse(xaml, System.Xml.Linq.LoadOptions.PreserveWhitespace);
        var deviceClockContent = document.Descendants()
            .Single(element => GetAttributeValue(element, "Name") == "DeviceClockContent");
        var deviceClockGrid = deviceClockContent.Descendants()
            .First(element => element.Name.LocalName == "Grid"
                && element.Elements().Any(child => child.Name.LocalName == "Grid.ColumnDefinitions")
                && element.Elements().Any(child => child.Name.LocalName == "Grid.RowDefinitions"));
        var declaredRowCount = deviceClockGrid.Elements()
            .Single(element => element.Name.LocalName == "Grid.RowDefinitions")
            .Elements()
            .Count(element => element.Name.LocalName == "RowDefinition");
        var directChildRows = deviceClockGrid.Elements()
            .Where(element => element.Name.LocalName is not "Grid.ColumnDefinitions" and not "Grid.RowDefinitions")
            .Select(element => (ElementName: element.Name.LocalName, Row: GetGridRow(element)))
            .ToArray();

        Assert.All(directChildRows, child =>
            Assert.True(
                child.Row < declaredRowCount,
                $"DeviceClockContent direct child {child.ElementName} uses Grid.Row={child.Row}, but the grid declares {declaredRowCount} rows."));

        var statusRows = deviceClockGrid.Elements()
            .Where(element => element.DescendantsAndSelf().Any(IsDeviceClockStatusTextBlock))
            .Select(GetGridRow)
            .ToArray();

        Assert.Equal(2, statusRows.Length);
        Assert.Equal(statusRows.Length, statusRows.Distinct().Count());
        Assert.Single(deviceClockGrid.Descendants(), element => (string?)element.Attribute("Text") == "{x:Bind ViewModel.CompactClockStateText, Mode=OneWay}");
        Assert.Single(deviceClockGrid.Descendants(), element => GetAttributeValue(element, "Uid") == "ScanDebug_DeviceClockUnitRangeText");
        foreach (var display in new[] { "SysClockMhzDisplay", "DeviceClockDisplay", "ClockDraftStatusText", "DeviceClockDisabledReasonText" })
            Assert.DoesNotContain(deviceClockGrid.Descendants(), element => (string?)element.Attribute("Text") == $"{{x:Bind ViewModel.{display}, Mode=OneWay}}");

        var validation = Assert.Single(deviceClockContent.Descendants(), element =>
            (string?)element.Attribute("Text") == "{x:Bind ViewModel.ClockInputErrorText, Mode=OneWay}");
        Assert.Equal("{x:Bind RunContextVisibility(ViewModel.ClockInputErrorText), Mode=OneWay}", (string?)validation.Attribute("Visibility"));
        Assert.Equal("Polite", (string?)validation.Attribute("AutomationProperties.LiveSetting"));
        var presentation = ReadAppSource("ViewModels", "ScanDebugViewModel.UiRefinement.cs");
        Assert.Contains("public string ClockInputErrorText => string.IsNullOrWhiteSpace(SysClockMhz)", presentation, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_ClockInputFormatHint", presentation, StringComparison.Ordinal);
        Assert.Equal("{x:Bind ViewModel.ClockRangeHintVisibility, Mode=OneWay}",
            (string?)deviceClockGrid.Descendants().Single(element => GetAttributeValue(element, "Uid") == "ScanDebug_DeviceClockUnitRangeText").Attribute("Visibility"));
        Assert.Contains("ScanDebug_ClockNotSet", presentation, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_ClockPendingOffline", presentation, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_ClockInputInvalidState", presentation, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_ClockApplied", presentation, StringComparison.Ordinal);

        static string? GetAttributeValue(System.Xml.Linq.XElement element, string localName)
            => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

        static int GetGridRow(System.Xml.Linq.XElement element)
            => int.Parse(GetAttributeValue(element, "Grid.Row") ?? "0");

        static bool IsDeviceClockStatusTextBlock(System.Xml.Linq.XElement element)
        {
            var text = GetAttributeValue(element, "Text");
            var uid = GetAttributeValue(element, "Uid");

            return string.Equals(uid, "ScanDebug_DeviceClockUnitRangeText", StringComparison.Ordinal)
                || text?.Contains("ViewModel.CompactClockStateText", StringComparison.Ordinal) == true;
        }
    }

    [Fact]
    public void PrismVisualQaPendingCalibrationHook_IsCompileTimeGatedAndPresentationOnly()
    {
        var project = ReadAppSource("PrismUtility.csproj");
        var activation = ReadAppSource("Activation", "DefaultActivationHandler.cs");
        var page = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var hook = ReadAppSource("PrismVisualQaPendingCalibrationHook.cs");
        var captureService = ReadAppSource("PrismVisualQaCaptureService.cs");

        Assert.Contains("Condition=\"'$(PrismVisualQa)' == 'true'\"", project, StringComparison.Ordinal);
        Assert.Contains("<DefineConstants>$(DefineConstants);PRISM_VISUAL_QA</DefineConstants>", project, StringComparison.Ordinal);
        Assert.Contains("#if PRISM_VISUAL_QA", activation, StringComparison.Ordinal);
        Assert.Contains("PrismVisualQaPendingCalibrationHook.Activate(_navigationService);", activation, StringComparison.Ordinal);
        Assert.Contains("#if PRISM_VISUAL_QA", page, StringComparison.Ordinal);
        Assert.Contains("PrismVisualQaPendingCalibrationHook.ApplyPageState(this, SetActiveWorkbenchSection);", page, StringComparison.Ordinal);
        Assert.StartsWith("#if PRISM_VISUAL_QA", hook, StringComparison.Ordinal);
        Assert.Contains("PRISM_VISUAL_QA_PENDING_CALIBRATION_HOOK", hook, StringComparison.Ordinal);
        Assert.Contains("navigationService.NavigateTo(AppRoute.ScanDebug, clearNavigation: true);", hook, StringComparison.Ordinal);
        Assert.Contains("viewModel.PendingCalibrationResult = PendingCalibrationResult.Create(", hook, StringComparison.Ordinal);
        Assert.Contains("PRISM_VISUAL_QA_MOTION_READ_REQUIRED", hook, StringComparison.Ordinal);
        Assert.Contains("setActiveWorkbenchSection(forceMotionReadRequired ? 5 : 2);", hook, StringComparison.Ordinal);
        Assert.Contains("sectionIndex is >= 0 and <= 5", hook, StringComparison.Ordinal);
        Assert.Contains("MarkMotionReadRequired(page.ViewModel);", hook, StringComparison.Ordinal);
        Assert.Contains("PrismVisualQaCaptureService.Start(page);", hook, StringComparison.Ordinal);
        Assert.StartsWith("#if PRISM_VISUAL_QA", captureService, StringComparison.Ordinal);
        Assert.Contains("RenderTargetBitmap", captureService, StringComparison.Ordinal);
        Assert.Contains("BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId", captureService, StringComparison.Ordinal);
        Assert.Contains("FindByAutomationId", captureService, StringComparison.Ordinal);
        Assert.Contains("Focus(FocusState.Keyboard)", captureService, StringComparison.Ordinal);
        Assert.Contains("StartBringIntoView", captureService, StringComparison.Ordinal);
        Assert.Contains("dragPreviewColumnSample", captureService, StringComparison.Ordinal);
        Assert.Contains("page.ViewModel.UpdateColumnSampleRange(42, 86, previewWidth);", captureService, StringComparison.Ordinal);
        Assert.Contains("capturePreviewFrame", captureService, StringComparison.Ordinal);
        Assert.Contains("CapturePreviewFrameAsync(page, request)", captureService, StringComparison.Ordinal);
        Assert.DoesNotContain("CopyFromScreen", captureService, StringComparison.Ordinal);
        Assert.DoesNotContain("PrintWindow", captureService, StringComparison.Ordinal);

        var forbiddenTokens = new[]
        {
            "ILocalSettingsService",
            "ApplicationData",
            "SaveSettingAsync",
            "IScanSessionService",
            "RunConnectedSessionStateAsync",
            "IScanCalibrationProfileRepository",
            "SaveSelectedCalibrationProfileAsync",
            "AcceptPendingCalibrationCommand.Execute",
            "AcceptAndSavePendingCalibrationCommand.Execute",
            "RestorePendingCalibrationCommand.Execute",
            "CancelPendingCalibrationCommand.Execute"
        };

        foreach (var forbiddenToken in forbiddenTokens)
            Assert.DoesNotContain(forbiddenToken, hook, StringComparison.Ordinal);
    }

    [Fact]
    public void PrismVisualQa_BuildPropertyDefaultsLocallyBeforeConditionalSymbolGroup()
    {
        var project = ReadAppSource("PrismUtility.csproj");
        const string conditionalPropertyGroup = "<PropertyGroup Condition=\"'$(PrismVisualQa)' == 'true'\">";

        var propertyGroups = System.Xml.Linq.XDocument.Parse(project).Root!.Elements("PropertyGroup").ToArray();
        var defaultGroupIndex = Array.FindIndex(propertyGroups, group =>
            group.Attribute("Condition") is null
            && string.Equals(group.Element("PrismVisualQa")?.Value, "false", StringComparison.Ordinal));
        var conditionalGroupIndex = project.IndexOf(conditionalPropertyGroup, StringComparison.Ordinal);

        Assert.True(defaultGroupIndex >= 0, "PrismVisualQa must have an unconditional project-local false default.");
        Assert.True(conditionalGroupIndex >= 0, "The QA symbol PropertyGroup must retain its true condition.");
        Assert.True(project.IndexOf("<PrismVisualQa>false</PrismVisualQa>", StringComparison.Ordinal) < conditionalGroupIndex,
            "The local false default must precede the conditional QA symbol group.");
        Assert.DoesNotContain("TreatAsLocalProperty", project, StringComparison.Ordinal);
    }

    [Fact]
    public void PrismVisualQaCaptureService_StopAsyncIsCurrentPageAwareAndScanDebugUnloadWired()
    {
        var captureService = ReadAppSource("PrismVisualQaCaptureService.cs");
        var page = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        const string stopSignature = "internal static async Task StopAsync(Page page)";

        var stopStart = captureService.IndexOf(stopSignature, StringComparison.Ordinal);
        if (stopStart < 0)
        {
            Assert.Fail("The QA capture service must expose a page-specific awaitable StopAsync method.");
            return;
        }

        var nextMethod = captureService.IndexOf("    private static async void ProcessRequests", stopStart, StringComparison.Ordinal);
        Assert.True(nextMethod > stopStart, "The Stop method must be a complete implementation before ProcessRequests.");

        var stop = captureService[stopStart..nextMethod];
        Assert.Contains("private static Task? _processingTask;", captureService, StringComparison.Ordinal);
        Assert.Contains("private static bool _stopping;", captureService, StringComparison.Ordinal);
        Assert.Contains("if (!ReferenceEquals(_page, page))", stop, StringComparison.Ordinal);
        Assert.Contains("_stopping = true;", stop, StringComparison.Ordinal);
        Assert.Contains("_timer.Stop();", stop, StringComparison.Ordinal);
        Assert.Contains("_timer.Tick -= ProcessRequests;", stop, StringComparison.Ordinal);
        Assert.Contains("var processingTask = _processingTask;", stop, StringComparison.Ordinal);
        Assert.Contains("if (processingTask is not null)", stop, StringComparison.Ordinal);
        Assert.Contains("await processingTask;", stop, StringComparison.Ordinal);
        Assert.Contains("_timer = null;", stop, StringComparison.Ordinal);
        Assert.Contains("_page = null;", stop, StringComparison.Ordinal);
        var awaitProcessingTaskIndex = stop.IndexOf("await processingTask;", StringComparison.Ordinal);
        var clearPageIndex = stop.IndexOf("_page = null;", StringComparison.Ordinal);
        Assert.True(stop.IndexOf("_stopping = true;", StringComparison.Ordinal) < awaitProcessingTaskIndex);
        Assert.True(awaitProcessingTaskIndex < clearPageIndex);

        var postAwaitGuard = stop[awaitProcessingTaskIndex..clearPageIndex];
        Assert.Contains("finally", postAwaitGuard, StringComparison.Ordinal);
        Assert.Contains("processingTask is null || processingTask.IsCompleted", postAwaitGuard, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(_page, page)", postAwaitGuard, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(_processingTask, processingTask)", postAwaitGuard, StringComparison.Ordinal);
        Assert.Contains("&& _stopping", postAwaitGuard, StringComparison.Ordinal);

        var processEventStart = captureService.IndexOf("    private static async void ProcessRequests", StringComparison.Ordinal);
        Assert.True(processEventStart > 0, "The DispatcherTimer event must remain an async void shim.");
        var processCoreStart = captureService.IndexOf("    private static async Task ProcessRequestsAsync(Page page)", StringComparison.Ordinal);
        Assert.True(processCoreStart > processEventStart, "Substantive request processing must live in an awaitable Task core.");
        var eventShim = captureService[processEventStart..processCoreStart];
        Assert.Contains("if (_stopping)", eventShim, StringComparison.Ordinal);
        Assert.Contains("var page = _page;", eventShim, StringComparison.Ordinal);
        Assert.Contains("_processingTask is { IsCompleted: false }", eventShim, StringComparison.Ordinal);
        Assert.Contains("_processingTask = ProcessRequestsAsync(page);", eventShim, StringComparison.Ordinal);
        Assert.Contains("await _processingTask;", eventShim, StringComparison.Ordinal);

        var processCoreEnd = captureService.IndexOf("    private static async Task<CaptureResult> CaptureAsync", processCoreStart, StringComparison.Ordinal);
        Assert.True(processCoreEnd > processCoreStart, "The awaitable request core must be complete before CaptureAsync.");
        var processCore = captureService[processCoreStart..processCoreEnd];
        Assert.Contains("if (_stopping)", processCore, StringComparison.Ordinal);
        Assert.Contains("break;", processCore, StringComparison.Ordinal);
        Assert.Contains("CaptureAsync(page, request)", processCore, StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureAsync(_page", processCore, StringComparison.Ordinal);

        var unloadStart = page.IndexOf("private async void OnUnloaded", StringComparison.Ordinal);
        if (unloadStart < 0)
        {
            Assert.Fail("ScanDebugPage must retain its unload handler.");
            return;
        }

        var nextPageMethod = page.IndexOf("    private void SubscribeViewModelEvents", unloadStart, StringComparison.Ordinal);
        Assert.True(nextPageMethod > unloadStart, "The ScanDebugPage unload handler must be a complete method.");

        var unload = page[unloadStart..nextPageMethod];
        Assert.Contains("#if PRISM_VISUAL_QA", unload, StringComparison.Ordinal);
        Assert.Contains("await PrismVisualQaCaptureService.StopAsync(this);", unload, StringComparison.Ordinal);
        Assert.Contains("#endif", unload, StringComparison.Ordinal);
        Assert.True(unload.IndexOf("await PrismVisualQaCaptureService.StopAsync(this);", StringComparison.Ordinal) < unload.IndexOf("UnsubscribeViewModelEvents();", StringComparison.Ordinal));
        Assert.True(unload.IndexOf("await PrismVisualQaCaptureService.StopAsync(this);", StringComparison.Ordinal) < unload.IndexOf("DisposePreviewBitmap();", StringComparison.Ordinal));
        Assert.True(unload.IndexOf("await PrismVisualQaCaptureService.StopAsync(this);", StringComparison.Ordinal) < unload.IndexOf("await ViewModel.DeactivateForPageAsync(pageOwner);", StringComparison.Ordinal));
    }

    [Fact]
    public void ScanDebugSourceContract_RejectsSyntheticMissingCommandsNamesHandlersAndPackages()
    {
        const string xaml = """
            <Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Button x:Name="PreviewCanvas" Command="{x:Bind ViewModel.StartScanCommand}" PointerPressed="PreviewCanvasControl_PointerPressed" />
            </Page>
            """;
        const string codeBehind = "private void PreviewCanvasControl_PointerPressed(object sender, object args) { }";
        const string project = "<Project><ItemGroup><PackageReference Include=\"Microsoft.WindowsAppSDK\" Version=\"2.0.1\" /></ItemGroup></Project>";

        Assert.Equal(["StopScanCommand"], FilmProfileContractSource.FindMissingCommandBindings(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["StartScanCommand"] = 1,
                ["StopScanCommand"] = 1
            }, xaml));
        Assert.Equal(["RoiCanvas"], FilmProfileContractSource.FindMissingTokens(["PreviewCanvas", "RoiCanvas"], FilmProfileContractSource.GetXamlNames(xaml)));
        Assert.Equal(["PointerReleased=PreviewCanvasControl_PointerReleased"], FilmProfileContractSource.FindMissingEventHandlerBindings(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["PointerPressed=PreviewCanvasControl_PointerPressed"] = 1,
                ["PointerReleased=PreviewCanvasControl_PointerReleased"] = 1
            }, xaml));
        Assert.Equal(["PreviewCanvasControl_PointerReleased"], FilmProfileContractSource.FindMissingTokens(
            ["PreviewCanvasControl_PointerPressed", "PreviewCanvasControl_PointerReleased"], FilmProfileContractSource.GetDeclaredMethodNames(codeBehind)));
        Assert.Equal(["WinUIEx"], FilmProfileContractSource.FindMissingPackageReferences(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Microsoft.WindowsAppSDK"] = "2.0.1",
                ["WinUIEx"] = "2.9.0"
            }, project));
    }

    private static string ReadAppSource(params string[] relativePath)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", Path.Combine(relativePath)));

    internal static void AssertCurrentRuntimeGatedCommandInventory(string xaml, string codeBehind, string viewModel)
    {
        foreach (var (binding, method, canExecute, runtimeCommand) in ExpectedProfileLifecycleBindings)
        {
            Assert.Contains(binding, xaml, StringComparison.Ordinal);
            Assert.Contains($"[RelayCommand(CanExecute = nameof({canExecute}))]", viewModel, StringComparison.Ordinal);
            Assert.Contains($"private bool {canExecute}", viewModel, StringComparison.Ordinal);
            Assert.Contains($"CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.{runtimeCommand})", viewModel, StringComparison.Ordinal);
            Assert.Contains(
                $"if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.{runtimeCommand}, out var runtimeClaim))",
                ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(viewModel, method),
                StringComparison.Ordinal);
        }

        Assert.Contains("public bool CanRunAutoFocusAction => CanRunAutoFocus();", viewModel, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{x:Bind ViewModel.CanRunAutoFocusAction, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"AutoFocusButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{x:Bind ViewModel.AutoFocusCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("if (ViewModel.CanRunAutoFocusAction)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ViewModel.AutoFocusCommand.Execute(null);", codeBehind, StringComparison.Ordinal);

        var autofocus = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(viewModel, "AutoFocus");
        Assert.Contains("[RelayCommand(CanExecute = nameof(CanRunAutoFocus))]", viewModel, StringComparison.Ordinal);
        Assert.Contains("if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.AutoFocus, out var runtimeClaim))", autofocus, StringComparison.Ordinal);
        Assert.Contains("RunConnectedSessionStateAsync", autofocus, StringComparison.Ordinal);
        Assert.Contains("_autoFocus.AutoFocusAsync", autofocus, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
        => source.Split(value, StringSplitOptions.None).Length - 1;

    private static string[] GetHiddenFocusableArtifactOpeningTags(string xaml)
        => GetOpeningTags(xaml)
            .Where(IsHiddenFocusableArtifactOpeningTag)
            .ToArray();

    private static bool IsHiddenFocusableArtifactOpeningTag(string openingTag)
        => ContainsAttribute(openingTag, "IsTabStop", "True")
            && HasHiddenOrTinyGeometry(openingTag)
            && HasSuppressedOrInvisibleFocusPresentation(openingTag);

    private static bool HasHiddenOrTinyGeometry(string openingTag)
        => ContainsAttribute(openingTag, "Opacity", "0")
            || ContainsAttribute(openingTag, "Width", "1")
            || ContainsAttribute(openingTag, "Height", "1");

    private static bool HasSuppressedOrInvisibleFocusPresentation(string openingTag)
        => ContainsAttribute(openingTag, "Opacity", "0")
            || ContainsAttribute(openingTag, "FocusVisualPrimaryThickness", "0")
            || ContainsAttribute(openingTag, "FocusVisualSecondaryThickness", "0");

    private static bool ContainsAttribute(string openingTag, string name, string value)
        => openingTag.Contains($"{name}=\"{value}\"", StringComparison.Ordinal);

    private static string[] GetOpeningTags(string source)
    {
        var tags = new List<string>();
        var searchIndex = 0;
        while (searchIndex < source.Length)
        {
            var tagStart = source.IndexOf('<', searchIndex);
            if (tagStart < 0)
                break;

            if (tagStart + 1 >= source.Length || (!char.IsLetter(source[tagStart + 1]) && source[tagStart + 1] is not '/' and not '!'))
            {
                searchIndex = tagStart + 1;
                continue;
            }

            var tagEnd = source.IndexOf('>', tagStart + 1);
            if (tagEnd < 0)
                break;

            if (tagStart + 1 < source.Length && source[tagStart + 1] is not '/' and not '!')
                tags.Add(source[tagStart..(tagEnd + 1)]);

            searchIndex = tagEnd + 1;
        }

        return tags.ToArray();
    }

    private static XElement GetNamedElement(string xaml, string elementName)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return Assert.Single(XDocument.Parse(xaml).Descendants(), element => (string?)element.Attribute(x + "Name") == elementName);
    }

    private static string GetOpeningTag(string xaml, string elementName)
    {
        var start = xaml.IndexOf($"x:Name=\"{elementName}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing x:Name={elementName}.");
        var tagStart = xaml.LastIndexOf('<', start);
        var tagEnd = xaml.IndexOf('>', start);
        Assert.True(tagStart >= 0 && tagEnd > tagStart, $"Could not locate opening tag for {elementName}.");
        return xaml[tagStart..(tagEnd + 1)];
    }

    private static string GetOpeningTagByAttribute(string xaml, string attributeName, string attributeValue)
    {
        var start = xaml.IndexOf($"{attributeName}=\"{attributeValue}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing {attributeName}={attributeValue}.");
        var tagStart = xaml.LastIndexOf('<', start);
        var tagEnd = xaml.IndexOf('>', start);
        Assert.True(tagStart >= 0 && tagEnd > tagStart, $"Could not locate opening tag for {attributeName}={attributeValue}.");
        return xaml[tagStart..(tagEnd + 1)];
    }

    private static string ExtractNamedRegion(string source, string startName, string endName)
    {
        var start = source.IndexOf($"x:Name=\"{startName}\"", StringComparison.Ordinal);
        var end = source.IndexOf($"x:Name=\"{endName}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing x:Name={startName}.");
        Assert.True(end > start, $"Missing x:Name={endName} after {startName}.");
        return source[start..end];
    }

    private static IReadOnlyDictionary<string, int> GetScanDebugEventHandlerBindingCounts(string xaml)
    {
        var counts = new Dictionary<string, int>(FilmProfileContractSource.GetXamlEventHandlerBindingCounts(xaml), StringComparer.Ordinal);
        var supplementalHandlers = XDocument.Parse(xaml).Descendants()
            .Attributes()
            .Where(attribute => attribute.Name.LocalName is "ActualThemeChanged" or "DropDownClosed" or "PreviewKeyDown" or "TextSubmitted")
            .GroupBy(attribute => $"{attribute.Name.LocalName}={attribute.Value}", StringComparer.Ordinal);
        foreach (var handlers in supplementalHandlers)
            counts.Add(handlers.Key, handlers.Count());

        return counts;
    }

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate Host Software source root.");
    }
}
