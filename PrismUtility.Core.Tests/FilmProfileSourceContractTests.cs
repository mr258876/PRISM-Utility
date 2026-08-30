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
        "AutofocusContent",
        "ManualFocusNegativeButton",
        "ManualFocusPositiveButton",
        "IlluminationContent",
        "MotionContent",
        "ZoomScaleComboBox",
        "PreviewDisplayToolsContent",
        "OverlayToolsContent",
        "PreviewScrollViewer",
        "PreviewCanvas",
        "PreviewCanvasControl",
        "RoiCanvas",
        "AxisCanvas",
        "CalibrationChannelFallbackComboBox",
        "CalibrationChannelStatusListView",
        "ChannelCalibrationLibraryActionsGrid",
        "PreviewEmptyStateGrid",
        "CursorPositionTextBlock",
        "CursorIntensityTextBlock",
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
        "EngineeringToolsSection",
        "WorkbenchContentSplitGrid",
        "WorkbenchEditorColumn",
        "WorkbenchPreviewColumn",
        "WorkbenchPreviewColumnContent",
        "NarrowWorkbenchSectionSelectorState",
        "WideWorkbenchSectionSelectorState"
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
            ["LoadFilmProfileJsonCommand"] = 1,
            ["ApplyStagedFilmProfileImportCommand"] = 1,
            ["DiscardStagedFilmProfileImportCommand"] = 1,
            ["SaveChannelProfileCommand"] = 1,
            ["ClearChannelProfileCommand"] = 1,
            ["ApplyParametersCommand"] = 1,
            ["AutoBlackAdjustCommand"] = 1,
            ["AutoWhiteAdjustCommand"] = 1,
            ["AutoCalibrateCommand"] = 1,
            ["SaveColumnSampleAsBlackLevelCommand"] = 1,
            ["SaveColumnSampleAsWhiteLevelCommand"] = 1,
            ["AutoFocusCommand"] = 1,
            ["RefreshIlluminationCommand"] = 1,
            ["ApplyIlluminationCommand"] = 1,
            ["RefreshMotionCommand"] = 1,
            ["EnableMotorCommand"] = 3,
            ["DisableMotorCommand"] = 3,
            ["MoveMotorCommand"] = 3,
            ["StopMotorCommand"] = 3,
            ["ApplyMotorConfigCommand"] = 3,
            ["ResetSelectedRoiCommand"] = 1,
            ["ResetAllRoisCommand"] = 1,
            ["ApplySelectedRoiInputsCommand"] = 1
        };

    private static readonly IReadOnlyDictionary<string, int> ExpectedEventHandlerBindingCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["TextChanged=CurrentCalibrationIlluminationLevelTextBox_TextChanged"] = 1,
            ["TextChanged=CurrentCalibrationIlluminationPulseClockTextBox_TextChanged"] = 1,
            ["SelectionChanged=CurrentCalibrationIlluminationWorkModeComboBox_SelectionChanged"] = 1,
            ["Expanding=DeferredExpander_Expanding"] = 3,
            ["PointerPressed=ManualFocusNegativeButton_PointerPressed"] = 1,
            ["PointerPressed=ManualFocusPositiveButton_PointerPressed"] = 1,
            ["PointerReleased=ManualFocusButton_PointerReleased"] = 2,
            ["PointerCanceled=ManualFocusButton_PointerCanceled"] = 2,
            ["PointerCaptureLost=ManualFocusButton_PointerCaptureLost"] = 2,
            ["Click=ZoomOutButton_Click"] = 1,
            ["Click=ZoomInButton_Click"] = 1,
            ["SelectionChanged=ZoomScaleComboBox_SelectionChanged"] = 1,
            ["SelectionChanged=WorkbenchSectionSelectorBar_SelectionChanged"] = 1,
            ["SelectionChanged=WorkbenchSectionComboBox_SelectionChanged"] = 1,
            ["SizeChanged=ScanDebugRootGrid_SizeChanged"] = 1,
            ["Opening=PreviewDisplayToolsFlyout_Opening"] = 1,
            ["Opening=OverlayToolsFlyout_Opening"] = 1,
            ["SizeChanged=PreviewScrollViewer_SizeChanged"] = 1,
            ["ViewChanged=PreviewScrollViewer_ViewChanged"] = 1,
            ["PointerPressed=PreviewScrollViewer_PointerPressed"] = 1,
            ["PointerMoved=PreviewScrollViewer_PointerMoved"] = 1,
            ["PointerReleased=PreviewScrollViewer_PointerReleased"] = 1,
            ["PointerCanceled=PreviewScrollViewer_PointerCanceled"] = 1,
            ["PointerWheelChanged=PreviewScrollViewer_PointerWheelChanged"] = 1,
            ["CreateResources=PreviewCanvasControl_CreateResources"] = 1,
            ["Draw=PreviewCanvasControl_Draw"] = 1,
            ["PointerPressed=PreviewCanvasControl_PointerPressed"] = 1,
            ["PointerMoved=PreviewCanvasControl_PointerMoved"] = 1,
            ["PointerReleased=PreviewCanvasControl_PointerReleased"] = 1,
            ["PointerCanceled=PreviewCanvasControl_PointerCanceled"] = 1,
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
        "Text=\"{x:Bind ViewModel.StartDisabledReasonText, Mode=OneWay}\"",
        "Text=\"{x:Bind ViewModel.ProfileSaveStateText, Mode=OneWay}\"",
        "Text=\"{x:Bind ViewModel.CurrentFilmProfileValidationSummary, Mode=OneWay}\"",
        "Severity=\"{x:Bind ViewModel.CurrentFilmProfileValidationSeverity, Mode=OneWay}\""
    ];

    private static readonly string[] Todo10DebugOnlyAcquisitionSwitchBindings =
    [
        "x:Uid=\"ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugTitle\"",
        "x:Uid=\"ScanDebug_FilmProfileWorkbenchEngineeringAcquisitionDebugDescription\"",
        "x:Uid=\"ScanDebug_ContinuousToggleSwitch\"",
        "IsEnabled=\"{x:Bind ViewModel.CanEditContinuousScan, Mode=OneWay}\"",
        "IsOn=\"{x:Bind ViewModel.IsContinuousScanEnabled, Mode=TwoWay}\"",
        "x:Uid=\"ScanDebug_WaterfallToggleSwitch\"",
        "IsEnabled=\"{x:Bind ViewModel.CanEditWaterfall, Mode=OneWay}\"",
        "IsOn=\"{x:Bind ViewModel.IsWaterfallEnabled, Mode=TwoWay}\"",
        "x:Uid=\"ScanDebug_ScanMotorTransportToggleSwitch\"",
        "IsEnabled=\"{x:Bind ViewModel.CanEditScanMotorTransport, Mode=OneWay}\"",
        "IsOn=\"{x:Bind ViewModel.IsScanMotorTransportEnabled, Mode=TwoWay}\"",
        "x:Uid=\"Scan_AlternateDirectionToggleSwitch\"",
        "IsEnabled=\"{x:Bind ViewModel.AreScanAcquisitionSettingsEditable, Mode=OneWay}\"",
        "IsOn=\"{x:Bind ViewModel.IsAlternateMotorDirectionEnabled, Mode=TwoWay}\""
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
        Assert.Contains("Grid.Row=\"1\"", header, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", header, StringComparison.Ordinal);

        Assert.DoesNotContain("ViewModel.StatusText", header, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.StartScanCommand", header, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.StopScanCommand", header, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.ExportDngCommand", header, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedDebugDngExportMode", header, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo7SourceContract_StagedImportReviewIsExplicitAndValidationDoesNotReuseCurrentSummary()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var review = ExtractNamedRegion(xaml, "StagedFilmProfileImportReview", "WorkbenchSectionSelectorBar");

        Assert.Contains("x:Name=\"StagedFilmProfileImportReview\"", review, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{x:Bind ViewModel.StagedFilmProfileImportReviewVisibility, Mode=OneWay}\"", review, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchStagedImportTitle\"", review, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_FilmProfileWorkbenchStagedImportHelpText\"", review, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.StagedFilmProfileImportSummary, Mode=OneWay}\"", review, StringComparison.Ordinal);
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
        var live = ExtractNamedRegion(xaml, "LiveCalibrationSection", "EngineeringToolsSection");
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
            "Text=\"{x:Bind ViewModel.SysClockKhz, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.ExposureTicks, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
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

        Assert.Contains("public void AttachRuntimeBindings()", viewModel, StringComparison.Ordinal);
        Assert.Contains("public async Task DeactivateAsync()", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo8SourceContract_WorkbenchSelectorHasFiveLocalizedPeerItems()
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
            ("ScanDebug_FilmProfileWorkbenchSectionEngineeringTools", "ScanDebug_FilmProfileWorkbenchSectionEngineeringToolsComboBoxItem", "5. Engineering tools", "5. 工程工具")
        };

        Assert.Contains("<SelectorBar", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchSectionSelectorBar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchSectionComboBox\"", xaml, StringComparison.Ordinal);
        Assert.Equal(5, CountOccurrences(xaml, "<SelectorBarItem"));

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
        Assert.True(xaml.IndexOf("x:Name=\"AcquisitionPlanSection\"", StringComparison.Ordinal) < xaml.IndexOf("x:Uid=\"ScanDebug_FilmProfileWorkbenchAcquisitionSetupTitle\"", StringComparison.Ordinal));
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
        Assert.Contains("private const double WorkbenchSectionWideSelectorMinimumWidth = 960;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private int _activeWorkbenchSectionIndex = 4;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SizeChanged=\"ScanDebugRootGrid_SizeChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private void ScanDebugRootGrid_SizeChanged", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SetWorkbenchSectionSelectorMode(e.NewSize.Width)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private void SetActiveWorkbenchSection(int index)", codeBehind, StringComparison.Ordinal);
        Assert.Equal(4, CountOccurrences(codeBehind, "SetActiveWorkbenchSection("));
        Assert.Contains("if (_isSynchronizingWorkbenchSection || !IsWorkbenchSectionUiReady())", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingWorkbenchSection = true;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("_isSynchronizingWorkbenchSection = false;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("if ((uint)index >= 5)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("SynchronizeWorkbenchSectionSelectors(_activeWorkbenchSectionIndex);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSectionSelectorBar.SelectedItem", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchSectionComboBox.SelectedIndex", codeBehind, StringComparison.Ordinal);

        Assert.Contains("<AdaptiveTrigger MinWindowWidth=\"960\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("Target=\"WorkbenchSectionSelectorBar.Visibility\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Target=\"WorkbenchSectionComboBox.Visibility\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"360\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"900\"", mainWindow, StringComparison.Ordinal);
        var normalizedXaml = xaml.Replace("\r\n", "\n", StringComparison.Ordinal);
        var splitGridStart = normalizedXaml.IndexOf("x:Name=\"WorkbenchContentSplitGrid\"", StringComparison.Ordinal);
        Assert.True(splitGridStart >= 0, "The editor/preview split must be hosted by the root row 2 grid.");
        Assert.True(
            normalizedXaml.IndexOf("x:Name=\"WorkbenchSectionSelectorBar\"", StringComparison.Ordinal) < splitGridStart,
            "The wide selector must sit in a full-width row above the editor/preview split so all five labels remain visible.");
        Assert.True(
            normalizedXaml.IndexOf("x:Name=\"WorkbenchSectionComboBox\"", StringComparison.Ordinal) < splitGridStart,
            "The narrow ComboBox must be the full-width navigation control below the 960px breakpoint.");
        Assert.DoesNotContain("<NavigationView", xaml[xaml.IndexOf("Grid.Row=\"1\"", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.DoesNotContain("<TabView", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo8SourceContract_ConstrainedWorkbenchGivesEditorFullWidthUntilTodo14StacksPreview()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var codeBehind = ReadAppSource("Views", "ScanDebugPage.xaml.cs");

        Assert.Contains("Todo 14 owns final constrained preview stacking/expand behavior", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchContentSplitGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchEditorColumn\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchPreviewColumn\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WorkbenchPreviewColumnContent\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"5*\"", GetOpeningTag(xaml, "WorkbenchEditorColumn"), StringComparison.Ordinal);
        Assert.Contains("Width=\"7*\"", GetOpeningTag(xaml, "WorkbenchPreviewColumn"), StringComparison.Ordinal);

        Assert.Contains("SetWorkbenchContentSplitMode(isWideSelectorAvailable);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("private void SetWorkbenchContentSplitMode(bool isWideSelectorAvailable)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchEditorColumn.Width = isWideSelectorAvailable ? new GridLength(5, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchPreviewColumn.Width = isWideSelectorAvailable ? new GridLength(7, GridUnitType.Star) : new GridLength(0);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("WorkbenchPreviewColumnContent.Visibility = isWideSelectorAvailable ? Visibility.Visible : Visibility.Collapsed;", codeBehind, StringComparison.Ordinal);
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
        Assert.Contains("SelectedItem=\"{x:Bind ViewModel.SelectedDebugDngExportMode, Mode=TwoWay}\"", ExtractNamedRegion(xaml, "LiveCalibrationSection", "EngineeringToolsSection"), StringComparison.Ordinal);
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
        Assert.Contains("CanRunFilmProfileLifecycleOperation()", canSave, StringComparison.Ordinal);
        Assert.Contains("HasUnsavedProfileChanges", canSave, StringComparison.Ordinal);
        Assert.Contains("IsCurrentFilmProfileValidationValid", canSave, StringComparison.Ordinal);
        Assert.Contains("!_hasInvalidFilmProfileInput", canSave, StringComparison.Ordinal);
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
            "public partial bool IsContinuousScanEnabled",
            "public partial bool IsWaterfallEnabled",
            "public partial bool IsScanMotorTransportEnabled",
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
        var engineering = ExtractNamedRegion(xaml, "EngineeringToolsSection", "WorkbenchPreviewColumnContent");

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

        foreach (var debugBinding in Todo10DebugOnlyAcquisitionSwitchBindings)
        {
            Assert.Contains(debugBinding, engineering, StringComparison.Ordinal);
        }

        foreach (var uid in new[]
        {
            "ScanDebug_ContinuousToggleSwitch",
            "ScanDebug_WaterfallToggleSwitch",
            "ScanDebug_ScanMotorTransportToggleSwitch",
            "Scan_AlternateDirectionToggleSwitch"
        })
        {
            Assert.DoesNotContain($"x:Uid=\"{uid}\"", acquisition, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(engineering, $"x:Uid=\"{uid}\""));
        }
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
            "OnIsPreviewEnabledChanged",
            "OnSelectedScanMotorChanged",
            "OnMotorDistancePerLineValueChanged",
            "OnMotorDistancePerLineUnitChanged",
            "OnSelectedStartingDirectionChanged"
        })
        {
            Assert.Contains("SynchronizeFilmProfileDraftFromInputs", ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, callback), StringComparison.Ordinal);
        }

        Assert.Contains("TryBuildFilmAcquisitionSettings", build, StringComparison.Ordinal);
        Assert.Contains("TryParseSelectedScanMotor(out var motorId", acquisition, StringComparison.Ordinal);
        Assert.Contains("!_isMotorDistanceDerivedFromInterval", acquisition, StringComparison.Ordinal);
        Assert.Contains("_deviceSettings.Settings.GetMotorSettings(motorId)", acquisition, StringComparison.Ordinal);
        Assert.Contains("ScanTimingMath.TryConvertLineDistanceMillimetersToMotorIntervalNs", acquisition, StringComparison.Ordinal);
        Assert.Contains("out motorIntervalNs", acquisition, StringComparison.Ordinal);
        Assert.Contains("GetMotorMoveInputs(motorId)", acquisition, StringComparison.Ordinal);
        Assert.Contains("TryBuildMotorIntervalFromInputs(motorId", acquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("TryBuildMotorIntervalFromInputs(1", acquisition, StringComparison.Ordinal);
        Assert.Contains("\"AcquisitionSettings.Rows\"", acquisition, StringComparison.Ordinal);
        Assert.Contains("\"AcquisitionSettings.MotorDistancePerLine\"", acquisition, StringComparison.Ordinal);
        Assert.Contains("\"AcquisitionSettings.MotorIntervalNs\"", acquisition, StringComparison.Ordinal);
        Assert.Contains("\"AcquisitionSettings.ChannelAssignment\"", build, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidAcquisitionInput, \"AcquisitionSettings\"", build, StringComparison.Ordinal);
        Assert.Contains("_hasInvalidFilmProfileInput = true", synchronization, StringComparison.Ordinal);
        Assert.DoesNotContain("_filmProfileWorkspace.SetCurrentDraft", synchronization[synchronization.IndexOf("else", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    [Fact]
    public void Todo10SourceContract_ProfileAcquisitionProjectionUsesSelectedMotor()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var applyAcquisition = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "ApplyProfileAcquisitionSettings");

        Assert.Contains("TryParseSelectedScanMotor(out var motorId", applyAcquisition, StringComparison.Ordinal);
        Assert.Contains("ApplyMotorSpeedFromIntervalNs(motorId", applyAcquisition, StringComparison.Ordinal);
        Assert.Contains("ClearMotorIntervalInput(selectedMotorId)", applyAcquisition, StringComparison.Ordinal);
        Assert.Contains("TryParseSelectedScanMotor(out var selectedMotorId", ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(source, "OnMotorSpeedInputChanged"), StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyMotorSpeedFromIntervalNs(1", applyAcquisition, StringComparison.Ordinal);
        Assert.DoesNotContain("SetMotorSpeedDerivedFromInterval(1", applyAcquisition, StringComparison.Ordinal);
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
            "Command=\"{x:Bind ViewModel.ApplyParametersCommand}\"",
            "Command=\"{x:Bind ViewModel.AutoBlackAdjustCommand}\"",
            "Command=\"{x:Bind ViewModel.AutoWhiteAdjustCommand}\"",
            "Command=\"{x:Bind ViewModel.AutoCalibrateCommand}\"",
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
        Assert.Contains("x:Uid=\"ScanDebug_SaveChannelButton\"", channel, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_ClearChannelButton\"", channel, StringComparison.Ordinal);
        Assert.Contains("<FontIcon", channel, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"{ThemeResource SymbolThemeFontFamily}\"", channel, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChannelProfileCommand", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("ClearChannelProfileCommand", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentCalibrationIlluminationEditorGrid", engineering, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy", channel, StringComparison.OrdinalIgnoreCase);
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
            ("ScanDebug_Runtime_ChannelStatusUnconfigured", "Unconfigured", "未配置"),
            ("ScanDebug_Runtime_ChannelStatusInvalid", "Invalid current edits", "当前编辑无效"),
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
        var project = ReadAppSource("PrismUtility.csproj");

        Assert.Equal(ExpectedNamedElements.Order(StringComparer.Ordinal), FilmProfileContractSource.GetXamlNames(xaml));
        Assert.Equal(ExpectedCommandBindingCounts.OrderBy(pair => pair.Key), FilmProfileContractSource.GetXamlCommandBindingCounts(xaml));
        Assert.Equal(ExpectedEventHandlerBindingCounts.OrderBy(pair => pair.Key), FilmProfileContractSource.GetXamlEventHandlerBindingCounts(xaml));
        Assert.Empty(FilmProfileContractSource.FindMissingTokens(ExpectedEventHandlerBindingCounts.Keys.Select(binding => binding[(binding.IndexOf('=') + 1)..]), FilmProfileContractSource.GetDeclaredMethodNames(codeBehind)));
        Assert.Equal(ExpectedPackageReferences.OrderBy(pair => pair.Key), FilmProfileContractSource.GetPackageReferences(project));
        Assert.DoesNotContain("CommunityToolkit.WinUI.Controls.Sizers", project, StringComparison.Ordinal);
        Assert.DoesNotContain("GridSplitter", xaml, StringComparison.Ordinal);
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

    private static int CountOccurrences(string source, string value)
        => source.Split(value, StringSplitOptions.None).Length - 1;

    private static string GetOpeningTag(string xaml, string elementName)
    {
        var start = xaml.IndexOf($"x:Name=\"{elementName}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing x:Name={elementName}.");
        var tagStart = xaml.LastIndexOf('<', start);
        var tagEnd = xaml.IndexOf('>', start);
        Assert.True(tagStart >= 0 && tagEnd > tagStart, $"Could not locate opening tag for {elementName}.");
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
