using System.Text.RegularExpressions;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class FilmProfileSourceContractTests
{
    private static readonly string[] ExpectedNamedElements =
    [
        "CurrentCalibrationIlluminationEditorGrid",
        "CurrentCalibrationIlluminationLevelTextBox",
        "CurrentCalibrationIlluminationPulseClockTextBox",
        "CurrentCalibrationIlluminationWorkModeComboBox",
        "LiveFocusCard",
        "AdvancedAutofocusContent",
        "DeviceClockContent",
        "AdvancedAdcAdjustmentContent",
        "FocusMappingContent",
        "ManualFocusNegativeButton",
        "ManualFocusPositiveButton",
        "IlluminationContent",
        "MotionContent",
        "ZoomScaleComboBox",
        "PreviewDisplayToolsContent",
        "PreviewDisplayToolsFlyout",
        "PreviewDisplayToolsScrollViewer",
        "OverlayToolsContent",
        "OverlayToolsFlyout",
        "OverlayToolsScrollViewer",
        "AdcRoiEditorCard",
        "AdcRoiTargetComboBox",
        "AdcRoiStartTextBox",
        "AdcRoiEndTextBox",
        "ImageReferenceRoiEditorCard",
        "ReferenceColumnSampleStartTextBox",
        "ReferenceColumnSampleEndTextBox",
        "LiveCalibrationScrollViewer",
        "CaptureModeComboBox",
        "FocusRoiEditorCard",
        "FocusRoiTargetComboBox",
        "FocusRoiStartTextBox",
        "FocusRoiEndTextBox",
        "PreviewScrollViewer",
        "PreviewCanvas",
        "PreviewCanvasControl",
        "RoiCanvas",
        "AxisCanvas",
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
        "FilmProfileOperationInfoBar",
        "ScanDebugRootGrid",
        "StagedFilmProfileImportReview",
        "WorkbenchSectionSelectorBar",
        "WorkbenchSectionComboBox",
        "BasicInfoSection",
        "AcquisitionPlanSection",
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
        "OpenPreviewButton",
        "BackToEditorButton",
        "PreviewSplitter",
        "PreviewGammaToggleSwitch",
        "PreviewGammaTextBox",
        "WaterfallPreviewOptionsPanel"
    ];

    private static readonly IReadOnlyDictionary<string, int> ExpectedCommandBindingCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["StartScanCommand"] = 1,
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
            ["SaveFocusMappingCommand"] = 1,
            ["TestLeftFocusMappingCommand"] = 1,
            ["TestRightFocusMappingCommand"] = 1,
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
            ["ResetSelectedRoiCommand"] = 2,
            ["ResetAllRoisCommand"] = 2,
            ["ApplySelectedRoiInputsCommand"] = 2
        };

    private static readonly IReadOnlyDictionary<string, int> ExpectedEventHandlerBindingCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["TextChanged=CurrentCalibrationIlluminationLevelTextBox_TextChanged"] = 1,
            ["TextChanged=CurrentCalibrationIlluminationPulseClockTextBox_TextChanged"] = 1,
            ["SelectionChanged=CurrentCalibrationIlluminationWorkModeComboBox_SelectionChanged"] = 1,
            ["Expanding=DeferredExpander_Expanding"] = 2,
            ["PointerPressed=ManualFocusNegativeButton_PointerPressed"] = 1,
            ["PointerPressed=ManualFocusPositiveButton_PointerPressed"] = 1,
            ["PointerReleased=ManualFocusButton_PointerReleased"] = 2,
            ["PointerCanceled=ManualFocusButton_PointerCanceled"] = 2,
            ["PointerCaptureLost=ManualFocusButton_PointerCaptureLost"] = 2,
            ["Click=AutoFocusButton_Click"] = 1,
            ["Click=ZoomOutButton_Click"] = 1,
            ["Click=ZoomInButton_Click"] = 1,
            ["SelectionChanged=ZoomScaleComboBox_SelectionChanged"] = 1,
            ["SelectionChanged=WorkbenchSectionSelectorBar_SelectionChanged"] = 1,
            ["SelectionChanged=WorkbenchSectionComboBox_SelectionChanged"] = 1,
            ["SizeChanged=ScanDebugRootGrid_SizeChanged"] = 1,
            ["Click=OpenPreviewButton_Click"] = 1,
            ["Click=BackToEditorButton_Click"] = 1,
            ["DragDelta=PreviewSplitter_DragDelta"] = 1,
            ["KeyDown=PreviewSplitter_KeyDown"] = 1,
            ["Opening=PreviewDisplayToolsFlyout_Opening"] = 1,
            ["Opening=OverlayToolsFlyout_Opening"] = 1,
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
        "Text=\"{x:Bind ViewModel.SelectedRows, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
        "x:Uid=\"Scan_ScanMotorComboBox\"",
        "ItemsSource=\"{x:Bind ViewModel.MotorOptions, Mode=OneWay}\"",
        "ItemTemplate=\"{StaticResource ScanMotorOptionTemplate}\"",
        "SelectedItem=\"{x:Bind ViewModel.SelectedScanMotor, Mode=TwoWay}\"",
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
        "ItemsSource=\"{x:Bind ViewModel.AcquisitionChannels}\"",
        "AutomationProperties.Name=\"{Binding ChannelLedBindingText}\"",
        "IsChecked=\"{Binding IsSelected, Mode=TwoWay}\"",
        "IsEnabled=\"{Binding IsSelectionEditable}\"",
        "Text=\"{Binding ChannelLedBindingText}\"",
        "Text=\"{Binding CalibrationStatusText}\"",
        "Text=\"{x:Bind ViewModel.AcquisitionPlanSummaryText, Mode=OneWay}\"",
        "Text=\"{x:Bind ViewModel.ChannelLedBindingSummaryText, Mode=OneWay}\"",
        "Text=\"{x:Bind ViewModel.ProfileSaveStateText, Mode=OneWay}\"",
        "Text=\"{x:Bind ViewModel.CurrentFilmProfileValidationSummary, Mode=OneWay}\"",
        "Severity=\"{x:Bind ViewModel.CurrentFilmProfileValidationSeverity, Mode=OneWay}\""
    ];

    private static readonly string[] Todo10DebugOnlyAcquisitionSwitchBindings =
    [
        "x:Uid=\"ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugTitle\"",
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
        "Text=\"{x:Bind ViewModel.CaptureModeScopeText, Mode=OneWay}\"",
        "Text=\"{x:Bind ViewModel.CaptureModeUnavailableReasonText, Mode=OneWay}\""
    ];

    [Fact]
    public void ShellNavigation_HasExistingMainAndScanDebugItems()
    {
        var shell = ReadAppSource("Views", "ShellPage.xaml");

        Assert.Contains("x:Uid=\"Shell_Main\" helpers:NavigationHelper.NavigateTo=\"Main\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"Shell_ScanDebug\" helpers:NavigationHelper.NavigateTo=\"ScanDebug\"", shell, StringComparison.Ordinal);
        Assert.Contains("<NavigationView.MenuItems>", shell, StringComparison.Ordinal);
        Assert.Contains("Header=\"{x:Bind ViewModel.HeaderText, Mode=OneWay}\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("FilmProfileEditor", shell, StringComparison.Ordinal);
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

        Assert.Contains("ScanDebug_FilmProfileWorkspaceSummaryTitle", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.CurrentProfileNameText, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.FilmProfileValidationSummary, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchLifecycleSaveJsonButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.SaveFilmProfileJsonCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchLifecycleOpenJsonButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.LoadFilmProfileJsonCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo7SourceContract_LifecycleHeaderOwnsProfileActionsAndSeparatedStatusSurfaces()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var header = ExtractNamedRegion(xaml, "FilmProfileLifecycleHeader", "WorkbenchSectionSelectorBar");

        foreach (var uid in new[]
        {
            "ScanDebug_FilmProfileWorkbenchLifecycleNewButton",
            "ScanDebug_FilmProfileWorkbenchLifecycleOpenJsonButton",
            "ScanDebug_FilmProfileWorkbenchLifecycleValidateButton",
            "ScanDebug_FilmProfileWorkbenchLifecycleSaveJsonButton"
        })
        {
            Assert.Contains($"x:Uid=\"{uid}\"", header, StringComparison.Ordinal);
        }

        Assert.Contains("x:Name=\"FilmProfileLifecycleHeader\"", header, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.CurrentProfileNameText, Mode=OneWay}\"", header, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.ProfileSaveStateText, Mode=OneWay}\"", header, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.FilmProfileDeviceStatusText, Mode=OneWay}\"", header, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.FilmProfileHardwareUnavailableReasonText, Mode=OneWay}\"", header, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FilmProfileOperationInfoBar\"", header, StringComparison.Ordinal);
        Assert.Contains("Message=\"{x:Bind ViewModel.FilmProfileOperationMessage, Mode=OneWay}\"", header, StringComparison.Ordinal);
        Assert.Contains("Severity=\"{x:Bind ViewModel.FilmProfileOperationSeverity, Mode=OneWay}\"", header, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.FilmProfileOperationVisibility, Mode=OneWay}\"", header, StringComparison.Ordinal);
        Assert.Contains("IsOpen=\"{x:Bind ViewModel.FilmProfileOperationIsOpen, Mode=TwoWay}\"", header, StringComparison.Ordinal);
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

        Assert.Contains("x:Name=\"FilmProfileOperationInfoBar\"", header, StringComparison.Ordinal);
        Assert.Contains("IsOpen=\"{x:Bind ViewModel.FilmProfileOperationIsOpen, Mode=TwoWay}\"", operationInfoBar, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.FilmProfileOperationVisibility, Mode=OneWay}\"", operationInfoBar, StringComparison.Ordinal);
        Assert.DoesNotContain("IsOpen=\"True\"", operationInfoBar, StringComparison.Ordinal);
        Assert.Contains("partial void OnFilmProfileOperationIsOpenChanged(bool value)", source, StringComparison.Ordinal);
        Assert.Contains("FilmProfileOperationVisibility = value ? Visibility.Visible : Visibility.Collapsed;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo7SourceContract_StagedImportReviewIsExplicitAndValidationDoesNotReuseCurrentSummary()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var review = ExtractNamedRegion(xaml, "StagedFilmProfileImportReview", "WorkbenchSectionSelectorBar");

        Assert.Contains("x:Name=\"StagedFilmProfileImportReview\"", review, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.FilmProfileImportResultReviewVisibility, Mode=OneWay}\"", review, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchStagedImportTitle\"", review, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchStagedImportHelpText\"", review, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.StagedFilmProfileImportDisplayNameText, Mode=OneWay}\"", review, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.StagedFilmProfileImportChannelCountText, Mode=OneWay}\"", review, StringComparison.Ordinal);
        Assert.Contains("Message=\"{x:Bind ViewModel.StagedFilmProfileImportValidationSummary, Mode=OneWay}\"", review, StringComparison.Ordinal);
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
        var review = ExtractNamedRegion(xaml, "StagedFilmProfileImportReview", "WorkbenchSectionSelectorBar");

        Assert.Contains("FilmProfileImportResultReviewVisibility", review, StringComparison.Ordinal);
        Assert.Contains("StagedFilmProfileImportDisplayNameText", review, StringComparison.Ordinal);
        Assert.Contains("StagedFilmProfileImportValidationSummary", review, StringComparison.Ordinal);
        Assert.Contains("DiscardStagedFilmProfileImportCommand", review, StringComparison.Ordinal);
        Assert.Contains("LoadFilmProfileJsonCommand", review, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.FilmProfileWorkbenchContentVisibility, Mode=OneWay}\"", GetOpeningTag(xaml, "WorkbenchContentSplitGrid"), StringComparison.Ordinal);
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
    public void Todo7SourceContract_LiveCalibrationOwnsScanRunAndDebugDngControlsOnlyAsInterimGroup()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var live = ExtractNamedRegion(xaml, "LiveCalibrationSection", "DeviceSettingsSection");
        var header = ExtractNamedRegion(xaml, "FilmProfileLifecycleHeader", "WorkbenchSectionSelectorBar");

        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchLiveRuntimeTitle\"", live, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.StatusText, Mode=OneWay}\"", live, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.StartScanCommand}\"", live, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.StopScanCommand}\"", live, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{x:Bind ViewModel.SelectedDebugDngExportMode, Mode=TwoWay}\"", live, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.ExportDngCommand}\"", live, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchLiveProgressLabel\"", live, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.ScanReadProgressVisibility, Mode=OneWay}\"", live, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.DngAlignmentWarningVisibility, Mode=OneWay}\"", live, StringComparison.Ordinal);
        foreach (var binding in Todo21CaptureModeBindings)
            Assert.Contains(binding, live, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"180\"", live, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"320\"", live, StringComparison.Ordinal);
        Assert.DoesNotContain("StartScanCommand", header, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedDebugDngExportMode", header, StringComparison.Ordinal);
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
    public void ScanDebugViewModel_InvalidJsonStatusesSeparateSummaryStates()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");

        Assert.Contains("ScanDebug_Runtime_StatusFilmProfileInvalid\".GetLocalizedOrFallback", source, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_Runtime_StatusFilmProfileInvalidWithSummary\".GetLocalizedFormatOrFallback", source, StringComparison.Ordinal);
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
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
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
    public void Todo8SourceContract_WorkbenchSelectorHasSixLocalizedPeerItems()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var english = FilmProfileContractSource.ReadResources("en-us");
        var chinese = FilmProfileContractSource.ReadResources("zh-CN");
        var expected = new (string SelectorUid, string ComboBoxUid, string EnglishText, string ChineseText)[]
        {
            ("ScanDebug_FilmProfileWorkbenchSectionBasicInfo", "ScanDebug_FilmProfileWorkbenchSectionBasicInfoComboBoxItem", "1. Basic info", "1. 基本信息"),
            ("ScanDebug_FilmProfileWorkbenchSectionAcquisitionPlan", "ScanDebug_FilmProfileWorkbenchSectionAcquisitionPlanComboBoxItem", "2. Acquisition plan", "2. 采集计划"),
            ("ScanDebug_FilmProfileWorkbenchSectionChannelCalibrationSelector", "ScanDebug_FilmProfileWorkbenchSectionChannelCalibrationComboBoxItem", "3. Channel calibration", "3. 通道校准"),
            ("ScanDebug_FilmProfileWorkbenchSectionLiveCalibration", "ScanDebug_FilmProfileWorkbenchSectionLiveCalibrationComboBoxItem", "4. Live calibration", "4. 实时校准"),
            ("ScanDebug_FilmProfileWorkbenchSectionDeviceSettings", "ScanDebug_FilmProfileWorkbenchSectionDeviceSettingsComboBoxItem", "5. Device settings", "5. 设备设置"),
            ("ScanDebug_FilmProfileWorkbenchSectionEngineeringTools", "ScanDebug_FilmProfileWorkbenchSectionEngineeringToolsComboBoxItem", "6. Engineering tools", "6. 工程工具")
        };

        Assert.Contains("<SelectorBar", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchSectionSelectorBar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchSectionComboBox\"", xaml, StringComparison.Ordinal);
        Assert.Equal(6, CountOccurrences(xaml, "<SelectorBarItem"));

        foreach (var (selectorUid, comboBoxUid, englishText, chineseText) in expected)
        {
            Assert.Equal(1, CountOccurrences(xaml, $"x:Uid=\"{selectorUid}\""));
            Assert.Equal(1, CountOccurrences(xaml, $"x:Uid=\"{comboBoxUid}\""));
            Assert.Equal(englishText, english[$"{selectorUid}.Text"]);
            Assert.Equal(chineseText, chinese[$"{selectorUid}.Text"]);
            Assert.Equal(englishText, english[$"{comboBoxUid}.Content"]);
            Assert.Equal(chineseText, chinese[$"{comboBoxUid}.Content"]);
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
        Assert.True(xaml.IndexOf("x:Name=\"EngineeringToolsSection\"", StringComparison.Ordinal) < xaml.IndexOf("x:Uid=\"ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugTitle\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"EngineeringToolsSection\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"MotionContent\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"PreviewScrollViewer\"", StringComparison.Ordinal) > xaml.IndexOf("</ScrollViewer>", xaml.IndexOf("x:Name=\"EngineeringToolsSection\"", StringComparison.Ordinal), StringComparison.Ordinal));
    }

    [Fact]
    public void Todo8SourceContract_WorkbenchSectionStateUsesOneGuardedSelectionPathAndNativeResponsiveShell()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var mainWindow = ReadAppSource("MainWindow.xaml");

        Assert.Contains("private bool _isSynchronizingWorkbenchSection;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ScanWorkbenchPreviewLayoutMode.WideCompact", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchPreviewSplitMinimumWidth", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private int _activeWorkbenchSectionIndex = 0;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private bool _isNarrowPreviewOpen;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private double _workbenchPreviewEditorRatio = ScanWorkbenchPreviewLayout.DefaultEditorRatio;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SizeChanged=\"ScanDebugRootGrid_SizeChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private void ScanDebugRootGrid_SizeChanged", codeBehind, StringComparison.Ordinal);
        Assert.Contains("UpdateWorkbenchPreviewLayout(e.NewSize.Width)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private void SetActiveWorkbenchSection(int index)", codeBehind, StringComparison.Ordinal);
        Assert.True(CountOccurrences(codeBehind, "SetActiveWorkbenchSection(") >= 5);
        Assert.Contains("if (_isSynchronizingWorkbenchSection || !IsWorkbenchSectionUiReady())", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingWorkbenchSection = true;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingWorkbenchSection = false;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("if ((uint)index >= 6)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SynchronizeWorkbenchSectionSelectors(_activeWorkbenchSectionIndex);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSectionSelectorBar.SelectedItem", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSectionComboBox.SelectedIndex", codeBehind, StringComparison.Ordinal);
        Assert.Contains("DeviceSettingsSection.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("EngineeringToolsSection.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("DeviceSettingsSection is not null", codeBehind, StringComparison.Ordinal);

        Assert.DoesNotContain("<AdaptiveTrigger", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<VisualStateManager.VisualStateGroups>", xaml, StringComparison.Ordinal);
        Assert.Contains("ScanWorkbenchPreviewLayout.Calculate", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ScanWorkbenchPreviewLayout.ResizeWideImageSplit", codeBehind, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"360\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"900\"", mainWindow, StringComparison.Ordinal);
        var normalizedXaml = xaml.Replace("\r\n", "\n", StringComparison.Ordinal);
        var splitGridStart = normalizedXaml.IndexOf("x:Name=\"WorkbenchContentSplitGrid\"", StringComparison.Ordinal);
        Assert.True(splitGridStart >= 0, "The editor/preview split must be hosted by the root row 2 grid.");
        Assert.True(
            normalizedXaml.IndexOf("x:Name=\"WorkbenchSectionSelectorBar\"", StringComparison.Ordinal) < splitGridStart,
            "The wide selector must sit in a full-width row above the editor/preview split so all six labels remain visible.");
        Assert.True(
            normalizedXaml.IndexOf("x:Name=\"WorkbenchSectionComboBox\"", StringComparison.Ordinal) < splitGridStart,
            "The narrow ComboBox must be the full-width navigation control below the 960px breakpoint.");
        Assert.DoesNotContain("<NavigationView", xaml[xaml.IndexOf("Grid.Row=\"1\"", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.DoesNotContain("<TabView", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo22SourceContract_OneLayoutOwnerKeepsPreviewReachableAndResizable()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs");

        Assert.Contains("x:Name=\"WorkbenchContentSplitGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchEditorColumn\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchPreviewSeparatorColumn\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchPreviewColumn\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchPreviewColumnContent\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"OpenPreviewButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"BackToEditorButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"PreviewSplitter\"", xaml, StringComparison.Ordinal);
        Assert.Contains("DragDelta=\"PreviewSplitter_DragDelta\"", xaml, StringComparison.Ordinal);
        Assert.Contains("KeyDown=\"PreviewSplitter_KeyDown\"", xaml, StringComparison.Ordinal);

        Assert.Contains("private void UpdateWorkbenchPreviewLayout(double availableWidth)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("new ScanWorkbenchPreviewLayoutInput(availableWidth, HasValidPreviewFrame(), _isNarrowPreviewOpen, _workbenchPreviewEditorRatio)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchEditorRow.Height = new GridLength(1, GridUnitType.Star);", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchPreviewRow", xaml + codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("SetWorkbenchContentSplitMode", codeBehind, StringComparison.Ordinal);
        Assert.Contains("OpenPreviewButton.Visibility = ToVisibility(layout.IsPreviewOpenButtonVisible);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("BackToEditorButton.Visibility = ToVisibility(layout.IsBackToEditorButtonVisible);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("PreviewSplitter.Visibility = ToVisibility(layout.IsSplitterVisible);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchAcquisitionSetupDescription\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugDescription\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.ComputedMotorSummaryText, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo8RuntimeContract_WorkbenchSelectionEventsCannotTouchSectionRootsDuringXamlInitialization()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs");

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
        Assert.Contains("SelectedItem=\"{x:Bind ViewModel.SelectedDebugDngExportMode, Mode=TwoWay}\"", ExtractNamedRegion(xaml, "LiveCalibrationSection", "DeviceSettingsSection"), StringComparison.Ordinal);
    }

    [Fact]
    public void Todo9SourceContract_BasicInfoSectionOwnsOnlyProfileAndRecipeEditingSurface()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var basic = ExtractNamedRegion(xaml, "BasicInfoSection", "AcquisitionPlanSection");

        foreach (var uid in new[]
        {
            "ScanDebug_FilmProfileWorkbenchBasicMetadataTitle",
            "ScanDebug_FilmProfileWorkbenchBasicMetadataDescription",
            "ScanDebug_FilmProfileWorkbenchBasicRecipeTitle",
            "ScanDebug_FilmProfileWorkbenchBasicRecipeDescription",
            "ScanDebug_FilmProfileWorkbenchBasicValidationTitle",
            "ScanDebug_FilmProfileWorkbenchBasicValidationDescription",
            "ScanDebug_FilmProfileWorkbenchBasicProfileNameTextBox",
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
            "Text=\"{x:Bind ViewModel.FilmProfileName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
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
            "Text=\"{x:Bind ViewModel.CurrentFilmProfileValidationSummary, Mode=OneWay}\"",
            "Severity=\"{x:Bind ViewModel.CurrentFilmProfileValidationSeverity, Mode=OneWay}\"",
            "Text=\"{x:Bind ViewModel.ProfileSaveStateText, Mode=OneWay}\""
        })
        {
            Assert.Contains(binding, basic, StringComparison.Ordinal);
        }

        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", basic, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource ScanDebugSectionCardStyle}\"", basic, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"WrapWholeWords\"", basic, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"Wrap\"", basic, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,0,0,96\"", basic, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{x:Bind ViewModel.IsScanRecipeColorManagementEnabled, Mode=OneWay}\"", basic, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{x:Bind ViewModel.IsScanRecipeManualWhitePointColorTemperatureEnabled, Mode=OneWay}\"", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedDebugDngExportMode", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("DebugDngExportModes", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("ExportDngCommand", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("StartScanCommand", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("StopScanCommand", basic, StringComparison.Ordinal);
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
        Assert.Contains("CurrentFilmProfileValidationIssues.Any(issue => issue.Severity == ScanFilmProfileValidationSeverity.Error)", source, StringComparison.Ordinal);
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
        Assert.Equal("4-channel raw DNG", english["Scan_Runtime_ProfileDngExportModeLinearRaw4"]);
        Assert.Equal("RGB + IR/W DNG", english["Scan_Runtime_ProfileDngExportModeLinearRgbIrw"]);
        Assert.Equal("4 通道原始 DNG", chinese["Scan_Runtime_ProfileDngExportModeLinearRaw4"]);
        Assert.Equal("RGB + IR/W DNG", chinese["Scan_Runtime_ProfileDngExportModeLinearRgbIrw"]);
        Assert.Equal("Single file: 4-channel raw DNG", english["Scan_Runtime_DngExportModeLinearRaw4"]);
        Assert.Equal("Two files: RGB plus IR/W DNG", english["Scan_Runtime_DngExportModeLinearRgbIrw"]);
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
        var preview = ExtractNamedRegion(xaml, "WorkbenchPreviewColumnContent", "CursorPositionTextBlock");

        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", acquisition, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource ScanDebugSectionCardStyle}\"", acquisition, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"WrapWholeWords\"", acquisition, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,0,0,96\"", acquisition, StringComparison.Ordinal);

        foreach (var binding in Todo10OperatorAcquisitionBindings)
        {
            Assert.Contains(binding, acquisition, StringComparison.Ordinal);
        }

        foreach (var uid in new[]
        {
            "ScanDebug_RowsComboBox",
            "Scan_ScanMotorComboBox",
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

        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugTitle\"", engineering, StringComparison.Ordinal);
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

        foreach (var binding in Todo21CaptureModeBindings)
        {
            Assert.Contains(binding, live, StringComparison.Ordinal);
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
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var preview = ExtractNamedRegion(xaml, "WorkbenchPreviewColumnContent", "CursorPositionTextBlock");
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
    public void Todo22Red_OuterFlyoutPresenterIsBoundedWithoutInnerWidthInflation()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var preview = ExtractNamedRegion(xaml, "WorkbenchPreviewColumnContent", "CursorPositionTextBlock");
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
        Assert.Contains("_hasInvalidFilmProfileInput = true", synchronization, StringComparison.Ordinal);
        Assert.DoesNotContain("_filmProfileWorkspace.SetCurrentDraft", synchronization[synchronization.IndexOf("else", StringComparison.Ordinal)..], StringComparison.Ordinal);
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
        Assert.Contains("AutomationProperties.AutomationId=\"SessionIlluminationTestCard\"", live, StringComparison.Ordinal);
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
            Assert.Contains($"x:Uid=\"{uid}\"", live, StringComparison.Ordinal);
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
            Assert.Equal(1, CountOccurrences(live, binding));
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
        Assert.Contains("Text=\"{x:Bind ViewModel.DeviceClockDisabledReasonText, Mode=OneWay}\"", deviceSettings, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"DeviceClockDisabledReasonText\"", deviceSettings, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.SessionIlluminationDisabledReasonText, Mode=OneWay}\"", live, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"SessionIlluminationDisabledReasonText\"", live, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.StartDisabledReasonText, Mode=OneWay}\"", live, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"StartDisabledReasonText\"", live, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.ExportDngDisabledReasonText, Mode=OneWay}\"", live, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"ExportDngDisabledReasonText\"", live, StringComparison.Ordinal);

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
        var automationStart = channel.IndexOf("AutomationProperties.AutomationId=\"AdvancedAdcAdjustmentExpander\"", StringComparison.Ordinal);
        Assert.True(automationStart >= 0, "Channel calibration parameters must expose AdvancedAdcAdjustmentExpander.");
        var expanderStart = channel.LastIndexOf("<Expander", automationStart, StringComparison.Ordinal);
        Assert.True(expanderStart >= 0, "AdvancedAdcAdjustmentExpander must be hosted by a native Expander.");
        var expander = channel[expanderStart..channel.IndexOf("AutomationProperties.AutomationId=\"ChannelCalibrationActionsCard\"", expanderStart, StringComparison.Ordinal)];

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
            "Text=\"{x:Bind ViewModel.SysClockMhzDisplay, Mode=OneWay}\"",
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
            ("ScanDebug_AdvancedAdcAdjustmentScopeText.Text", "Edits stay in the current channel draft. Apply Parameters writes the selected channel values to the device only. Save channel calibration persists only the selected library profile.", "编辑仅更新当前通道草稿。\n应用参数时，仅写入设备。\n保存校准才写入标定库。\n仅更新所选通道，其余通道不变。"),
            ("ScanDebug_DeviceClockUnitRangeText.Text", "Legal range: 30-200 MHz. Apply sends the device-global clock command and does not save a channel profile. Device Flash persistence is unverified.", "合法范围：30-200 MHz。\n应用发送设备全局时钟命令，不保存通道配置。\n设备 Flash 持久化行为尚未验证。"),
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
            ("ScanDebug_IlluminationSessionTestTitle.Text", "Session-only illumination test", "仅当前会话的照明测试"),
            ("ScanDebug_IlluminationRawApplyScopeText.Text", "Apply raw edits sends the values to the device only; it does not update the current profile draft.", "应用原始编辑只会把数值发送到设备，不会更新当前配置 Draft。"),
            ("ScanDebug_IlluminationRawCopyScopeText.Text", "Copy raw edits to the current profile draft updates the Draft only; it sends no device command.", "复制原始编辑到当前配置 Draft 只会更新 Draft，不会发送设备命令。"),
            ("ScanDebug_IlluminationSessionTestHelpText.Text", "These test actions immediately affect only the current device session and do not modify acquisition configuration.", "这些测试动作只会立即影响当前设备会话，不会修改采集配置。"),
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
    public void Todo16EvidenceContract_CleanupDoesNotRetainPrivateBackupOrCroppedScreenshot()
    {
        var evidenceRoot = Path.Combine(FindHostSoftwareRoot(), ".omo", "evidence", "task-16-prism-utility-second-round-rework-ui");
        var receipt = File.ReadAllText(Path.Combine(
            FindHostSoftwareRoot(),
            ".omo",
            "evidence",
            "task-16-prism-utility-second-round-rework-ui.txt"));

        Assert.False(File.Exists(Path.Combine(evidenceRoot, "LocalSettings.before-task16-zh.json")));
        Assert.False(File.Exists(Path.Combine(evidenceRoot, "15-printwindow-engineering-illumination-zh-cn.png")));
        Assert.DoesNotContain("LocalSettings.before-task16-zh.json", receipt, StringComparison.Ordinal);
        Assert.DoesNotContain("15-printwindow-engineering-illumination-zh-cn.png", receipt, StringComparison.Ordinal);
        Assert.Contains("InvalidNumericInput", receipt, StringComparison.Ordinal);
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
            ("ScanDebug_FilmProfileWorkbenchAcquisitionSetupDescription.Text", "Edit offline acquisition defaults for the authored film profile. Hardware-only live actions remain in Live calibration or Engineering tools.", "编辑所编写胶片配置的离线采集默认值。仅硬件实时操作仍位于实时校准或工程工具中。"),
            ("ScanDebug_FilmProfileWorkbenchAcquisitionStatusTitle.Text", "Save and validation state", "保存与验证状态"),
            ("ScanDebug_FilmProfileWorkbenchAcquisitionStatusDescription.Text", "Rows, distance, channel enablement, and order changes update summaries immediately and block Save when invalid.", "行数、距离、通道启用和顺序更改会立即更新摘要，输入无效时会阻止保存。"),
            ("ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugTitle.Text", "Acquisition debug switches", "采集调试开关"),
            ("ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugDescription.Text", "Low-level scan flow toggles for engineering validation. Leave these off unless a hardware bring-up procedure explicitly asks for them.", "用于工程验证的底层扫描流程开关。除非硬件调试流程明确要求，否则请保持关闭。")
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
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var channel = ExtractNamedRegion(xaml, "ChannelCalibrationSection", "LiveCalibrationSection");
        var engineering = ExtractNamedRegion(xaml, "EngineeringToolsSection", "WorkbenchPreviewColumnContent");

        Assert.Equal(ExpectedNamedElements.Order(StringComparer.Ordinal), FilmProfileContractSource.GetXamlNames(xaml));
        Assert.Equal(ExpectedCommandBindingCounts.OrderBy(pair => pair.Key), FilmProfileContractSource.GetXamlCommandBindingCounts(xaml));
        Assert.Equal(ExpectedEventHandlerBindingCounts.OrderBy(pair => pair.Key), FilmProfileContractSource.GetXamlEventHandlerBindingCounts(xaml));

        Assert.Contains("Visibility=\"Collapsed\"", GetOpeningTag(xaml, "ChannelCalibrationSection"), StringComparison.Ordinal);
        Assert.Contains("SaveChannelProfileCommand", channel, StringComparison.Ordinal);
        Assert.Contains("ClearChannelProfileCommand", channel, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.CalibrationChannelOptions, Mode=OneWay}\"", channel, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{x:Bind ViewModel.SelectedCalibrationChannel, Mode=TwoWay}\"", channel, StringComparison.Ordinal);
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

        AssertCurrentRuntimeGatedCommandInventory(xaml, codeBehind, ReadAppSource("ViewModels", "ScanDebugViewModel.cs"));
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
            "SelectedItem=\"{x:Bind ViewModel.SelectedCalibrationChannelItem, Mode=TwoWay}\"",
            "AutomationProperties.Name=\"{Binding AccessibilityText}\"",
            "Text=\"{Binding DisplayName}\"",
            "Text=\"{Binding StatusText}\"",
            "Text=\"{Binding LedMappingText}\"",
            "Text=\"{Binding StatusIconGlyph}\"",
            "ItemsSource=\"{x:Bind ViewModel.CalibrationChannelOptions, Mode=OneWay}\"",
            "SelectedItem=\"{x:Bind ViewModel.SelectedCalibrationChannel, Mode=TwoWay}\"",
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

        Assert.Contains("SelectionMode=\"Single\"", channel, StringComparison.Ordinal);
        Assert.Contains("TabNavigation=\"Once\"", channel, StringComparison.Ordinal);
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
        Assert.Equal(11, CountOccurrences(channel, "ContentTemplate=\"{StaticResource ScanDebugWrappingButtonContentTemplate}\""));
        Assert.Contains("<DataTemplate x:Key=\"ScanDebugWrappingButtonContentTemplate\">", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"WrapWholeWords\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("<FontIcon", channel, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"{ThemeResource SymbolThemeFontFamily}\"", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChannelProfileCommand", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("ClearChannelProfileCommand", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentCalibrationIlluminationEditorGrid", engineering, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualQaCaptureContract_ChannelCalibrationExposesStableScrollCardsGutterAndRejectsHiddenFocusSinks()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var channel = ExtractNamedRegion(xaml, "ChannelCalibrationSection", "LiveCalibrationSection");

        Assert.Contains("x:Name=\"ChannelCalibrationScrollViewer\"", channel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"ChannelCalibrationScrollViewer\"", channel, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Channel calibration content scroller\"", channel, StringComparison.Ordinal);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", channel, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,0,12,96\"", channel, StringComparison.Ordinal);

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
        var review = channel[channel.IndexOf("AutomationProperties.AutomationId=\"PendingCalibrationCandidateReviewCard\"", StringComparison.Ordinal)..];
        review = review[..review.IndexOf("x:Uid=\"ScanDebug_CalibrationTitle\"", StringComparison.Ordinal)];

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
            ("ScanDebug_ChannelCalibrationStatusListHelpText.Text", "All calibration channels are visible here. Select one to edit only that channel's calibration-library entry.", "此处显示所有校准通道。选择一个通道，只编辑该通道在校准库中的条目。"),
            ("ScanDebug_ChannelCalibrationLibraryTitle.Text", "Selected channel calibration library", "所选通道校准库"),
            ("ScanDebug_ChannelCalibrationLibraryHelpText.Text", "Save and Remove affect only the selected channel in the calibration library. They do not save the top film-profile JSON or any other channel.", "保存和移除只影响所选通道在校准库中的条目，不会保存顶层胶片配置 JSON，也不会影响其他通道。"),
            ("ScanDebug_Runtime_ChannelStatusSaved", "Saved", "已保存"),
            ("ScanDebug_Runtime_ChannelStatusModified", "Modified", "已修改"),
            ("ScanDebug_Runtime_ChannelStatusInvalid", "Invalid", "无效"),
            ("ScanDebug_Runtime_ChannelStatusMissing", "Missing", "缺少"),
            ("ScanDebug_Runtime_ChannelStatusCopiedUnverified", "Copied-Unverified", "已复制待验证"),
            ("ScanDebug_Runtime_ChannelStatusUnconfigured", "Missing", "缺少"),
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
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs");
        var viewModel = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var project = ReadAppSource("PrismUtility.csproj");

        Assert.Equal(ExpectedNamedElements.Order(StringComparer.Ordinal), FilmProfileContractSource.GetXamlNames(xaml));
        Assert.Equal(ExpectedCommandBindingCounts.OrderBy(pair => pair.Key), FilmProfileContractSource.GetXamlCommandBindingCounts(xaml));
        Assert.Equal(ExpectedEventHandlerBindingCounts.OrderBy(pair => pair.Key), FilmProfileContractSource.GetXamlEventHandlerBindingCounts(xaml));
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
        Assert.DoesNotContain("x:Load=\"False\"", advanced, StringComparison.Ordinal);
        foreach (var automationId in new[]
        {
            "AdvancedAutofocusBoundsText",
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
        Assert.Contains("x:Uid=\"ScanDebug_TestLeftFocusMappingButton\"", mapping, StringComparison.Ordinal);
        Assert.DoesNotContain("FocusMappingLeftMotorComboBox", live, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveFocusMappingCommand", live, StringComparison.Ordinal);
        Assert.DoesNotContain("FocusMappingLeftMotorComboBox", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveFocusMappingCommand", engineering, StringComparison.Ordinal);
    }

    [Fact]
    public void DeviceClockContent_GridDeclaresRowsForEachDirectChildStatusLine()
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
            .Where(element => element.Name.LocalName == "TextBlock")
            .Where(IsDeviceClockStatusTextBlock)
            .Select(GetGridRow)
            .ToArray();

        Assert.Equal(3, statusRows.Length);
        Assert.Equal(statusRows.Length, statusRows.Distinct().Count());

        static string? GetAttributeValue(System.Xml.Linq.XElement element, string localName)
            => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

        static int GetGridRow(System.Xml.Linq.XElement element)
            => int.Parse(GetAttributeValue(element, "Grid.Row") ?? "0");

        static bool IsDeviceClockStatusTextBlock(System.Xml.Linq.XElement element)
        {
            var text = GetAttributeValue(element, "Text");
            var uid = GetAttributeValue(element, "Uid");

            return string.Equals(uid, "ScanDebug_DeviceClockUnitRangeText", StringComparison.Ordinal)
                || text?.Contains("ViewModel.SysClockMhzDisplay", StringComparison.Ordinal) == true
                || text?.Contains("ViewModel.DeviceClockDisabledReasonText", StringComparison.Ordinal) == true;
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
        const string stopSignature = "internal static async Task StopAsync(ScanDebugPage page)";

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
        Assert.Contains("!ReferenceEquals(_page, page)", postAwaitGuard, StringComparison.Ordinal);
        Assert.Contains("!ReferenceEquals(_processingTask, processingTask)", postAwaitGuard, StringComparison.Ordinal);
        Assert.Contains("|| !_stopping", postAwaitGuard, StringComparison.Ordinal);
        Assert.Contains("return;", postAwaitGuard, StringComparison.Ordinal);

        var processEventStart = captureService.IndexOf("    private static async void ProcessRequests", StringComparison.Ordinal);
        Assert.True(processEventStart > 0, "The DispatcherTimer event must remain an async void shim.");
        var processCoreStart = captureService.IndexOf("    private static async Task ProcessRequestsAsync(ScanDebugPage page)", StringComparison.Ordinal);
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
        Assert.True(unload.IndexOf("await PrismVisualQaCaptureService.StopAsync(this);", StringComparison.Ordinal) < unload.IndexOf("await ViewModel.DeactivateAsync();", StringComparison.Ordinal));
    }

    [Fact]
    public void PrismVisualQaTask17CaptureScript_ResetsValidFocusMappingBeforeEngineeringStateAndKeepsDuplicateStateSeparate()
    {
        var script = File.ReadAllText(Path.Combine(
            FindHostSoftwareRoot(),
            ".omo",
            "evidence",
            "task-17-visual-qa",
            "run-capture.ps1"));

        Assert.Contains("function Set-ValidFocusMapping($root)", script, StringComparison.Ordinal);
        Assert.Contains("Select-ComboByAutomationIdIndex $root \"FocusMappingLeftMotorComboBox\" 0", script, StringComparison.Ordinal);
        Assert.Contains("Select-ComboByAutomationIdIndex $root \"FocusMappingRightMotorComboBox\" 2", script, StringComparison.Ordinal);
        Assert.Contains("validFocusMapping = [ordered]@{ leftMotorId = 0; rightMotorId = 2 }", script, StringComparison.Ordinal);
        Assert.Contains("Set-ValidFocusMapping $root", script, StringComparison.Ordinal);
        Assert.Contains("Set-InvalidDuplicateMapping $root", script, StringComparison.Ordinal);
        Assert.True(
            script.IndexOf("Set-ValidFocusMapping $root", StringComparison.Ordinal)
                < script.IndexOf("$locale-$($widthSpec.label)-live-focus-default", StringComparison.Ordinal),
            "Each width iteration must reset to the valid 0/2 mapping before live and advanced captures so they cannot inherit a prior duplicate-invalid state.");
        Assert.True(
            script.IndexOf("Set-ValidFocusMapping $root", StringComparison.Ordinal)
                < script.IndexOf("$locale-$($widthSpec.label)-advanced-autofocus-expanded", StringComparison.Ordinal),
            "Advanced autofocus captures must not inherit duplicate focus mapping validation from the preceding invalid state.");
        Assert.True(
            script.IndexOf("Set-ValidFocusMapping $root", StringComparison.Ordinal)
                < script.IndexOf("$locale-$($widthSpec.label)-engineering-focus-mapping", StringComparison.Ordinal),
            "The visible engineering mapping state must be captured after resetting to the valid default 0/2 mapping.");
        Assert.True(
            script.IndexOf("$locale-$($widthSpec.label)-engineering-focus-mapping", StringComparison.Ordinal)
                < script.IndexOf("Set-InvalidDuplicateMapping $root", StringComparison.Ordinal),
            "The invalid duplicate state must remain a separate state after the valid engineering capture.");
    }

    [Fact]
    public void PrismVisualQaTask17CaptureScript_SelectsAndAssertsTrueAdvancedCustomPreset()
    {
        var script = File.ReadAllText(Path.Combine(
            FindHostSoftwareRoot(),
            ".omo",
            "evidence",
            "task-17-visual-qa",
            "run-capture.ps1"));

        Assert.Contains("Select-ComboByAutomationIdIndex $root \"AutofocusPresetComboBox\" 3", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Select-ComboByAutomationIdIndex $root \"AutofocusPresetComboBox\" 2", script, StringComparison.Ordinal);
        Assert.Contains("$zhCustom = ConvertFrom-CodePoints", script, StringComparison.Ordinal);
        Assert.Contains("Select-ComboByAutomationIdName $root \"AutofocusPresetComboBox\" (New-AnyNameRegex @(\"Custom\", $zhCustom))", script, StringComparison.Ordinal);
        Assert.Contains("selectedItems = @()", script, StringComparison.Ordinal);
        Assert.Contains("function Assert-AdvancedCustomSnapshot", script, StringComparison.Ordinal);
        Assert.Contains("function Test-AdvancedCustomSnapshot", script, StringComparison.Ordinal);
        Assert.Contains("if (-not $selectedPresetText -or $selectedPresetText -notmatch", script, StringComparison.Ordinal);
        Assert.DoesNotContain("if ($selectedPresetText -and $selectedPresetText -notmatch", script, StringComparison.Ordinal);
        Assert.Contains("advanced preset must be Custom", script, StringComparison.Ordinal);
        Assert.Contains("custom autofocus field sampleRows", script, StringComparison.Ordinal);
        Assert.Contains("custom autofocus field tiltProbe", script, StringComparison.Ordinal);
        Assert.Contains("custom autofocus field zProbe", script, StringComparison.Ordinal);
        Assert.Contains("custom autofocus field motorInterval", script, StringComparison.Ordinal);
        Assert.Contains("custom autofocus field maxTiltIterations", script, StringComparison.Ordinal);
        Assert.Contains("custom autofocus field maxZIterations", script, StringComparison.Ordinal);
        Assert.True(
            script.IndexOf("Assert-AdvancedCustomSnapshot", StringComparison.Ordinal)
                < script.IndexOf("return $failures.ToArray()", StringComparison.Ordinal),
            "Advanced captures must assert the selected preset is true Custom and custom text inputs are enabled before metadata can pass.");
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

    [Fact]
    public void Todo4NativeVerifier_CapturesClientSafeHighDpiViewportEvidence()
    {
        var script = File.ReadAllText(Path.Combine(
            FindHostSoftwareRoot(),
            ".omo",
            "evidence",
            "task-4-adversarial-verify-native-ui",
            "verify-task4-import-lifecycle.ps1"));

        Assert.DoesNotContain("effectiveWidth * $dpi", script, StringComparison.Ordinal);
        Assert.DoesNotContain("effectiveHeight * $dpi", script, StringComparison.Ordinal);
        Assert.Contains("GetClientRect", script, StringComparison.Ordinal);
        Assert.Contains("ClientToScreen", script, StringComparison.Ordinal);
        Assert.Contains("workingArea", script, StringComparison.Ordinal);
        Assert.Contains("clientRect", script, StringComparison.Ordinal);
        Assert.Contains("bottom-edge-crop", script, StringComparison.Ordinal);
        Assert.Contains("ScrollPattern", script, StringComparison.Ordinal);
        Assert.Contains("invalid-review-hides-lower-workbench", script, StringComparison.Ordinal);
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
