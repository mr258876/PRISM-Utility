using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;
using PRISM_Utility.Models;
using Windows.UI;

namespace PRISM_Utility.ViewModels;

public static class ScanWorkflowBlockerIds
{
    public const string None = nameof(None);
    public const string Device = nameof(Device);
    public const string Configuration = nameof(Configuration);
    public const string Execution = nameof(Execution);
    public const string Output = nameof(Output);
}

public enum ScanPreparationAction
{
    ShowConnection,
    LoadConfiguration,
    UseSavedConfiguration,
    ReplaceConfiguration,
    ReviewDeviceMappings,
    ReviewColorSettings,
    ReviewMotionSettings,
    ShowOutputStatus
}

public sealed record ScanPreparationStep(string CardId, string Label, string Message, bool IsEnabled, ScanPreparationAction Action)
{
    public bool IsStatusOnly => false;
    public bool IsActionVisible => true;
}

public partial class ScanViewModel : ObservableRecipient
{
    public const string UseSavedFilmProfileNavigationParameter = nameof(UseSavedFilmProfile);
    private const string ForwardDirection = "Forward";
    private const string ReverseDirection = "Reverse";
    private const string MotorDistanceUnitSteps = ScanMotorDistanceText.StepsUnit;
    private const string MotorDistanceUnitMicrometers = ScanMotorDistanceText.MicrometersUnit;
    private const string MotorDistanceUnitMillimeters = ScanMotorDistanceText.MillimetersUnit;
    private static readonly string[] MotorDistanceUnitLabels = { MotorDistanceUnitSteps, MotorDistanceUnitMicrometers, MotorDistanceUnitMillimeters };
    private const double DefaultRedWavelengthNm = 680.0;
    private const double DefaultGreenWavelengthNm = 525.0;
    private const double DefaultBlueWavelengthNm = 450.0;
    private const double DefaultOutputGamma = 2.2;
    private const double DefaultManualWhitePointColorTemperatureK = 6504.0;
    private static readonly Brush ScanCardNormalBorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush", new Color { A = 255, R = 128, G = 128, B = 128 });
    private static readonly Brush ScanCardBlockerBorderBrush = GetThemeBrush("SystemFillColorCautionBrush", new Color { A = 255, R = 218, G = 165, B = 32 });

    private sealed record ScanStartBlocker(string CardId, string Message);
    private sealed record PreparedScanRequest(ScanWorkflowRequest Request,
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile> ChannelProfiles);

    private readonly IScannerDeviceSessionManager _sessionManager;
    private readonly IScanWorkflowSessionCoordinator _scanSessionCoordinator;
    private readonly IScanParameterService _parameters;
    private readonly IScanTransferSettingsService _transferSettings;
    private readonly IScanWorkflowService _workflow;
    private readonly IDebugOutputMirrorService _debugOutputMirror;
    private readonly IScanChannelImageService _channelImages;
    private readonly IScanOutputLocationService? _outputLocationService;
    private readonly IScanDeviceSettingsService _deviceSettings;
    private readonly IScanColorManagementSettingsService _colorManagementSettings;
    private readonly IScanCalibrationProfileRepository _calibrationProfiles;
    private readonly IScanFilmProfileFileCoordinator _filmProfileFiles;
    private readonly IScanFilmProfileWorkspace _filmProfileWorkspace;
    private readonly IUsbUsageCoordinator _usbUsageCoordinator;
    private readonly IUiDispatcher _dispatcher;
    private readonly CancellationTokenSource _uiLifetimeCts = new();

    private CancellationTokenSource? _scanCts;
    private ScanWorkflowResult? _lastResult;
    private ScanChannelAssignment? _lastResultChannelAssignment;
    private WhiteLightSource? _lastResultWhiteLightSource;
    private IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? _lastResultChannelProfiles;
    private ScanParameterSnapshot? _loadedSnapshot;
    private ushort _loadedExposureTicks;
    private uint _loadedSysClockKhz;
    private ScanFilmAcquisitionSettings? _selectedConfigAcquisitionSettings;
    private string[]? _loadedConfigChannelRoles;
    private bool _loadedChannel5Reversed;
    private readonly Task _deviceSettingsInitializationTask;
    private bool _isConfigProfileLoaded;
    private bool _hasEditableFilmProfileSession;
    private ScanFilmProfileDraft? _loadedConfigProfileDraft;
    public ScanFilmProfileEditRequest? LoadedFilmProfileEditRequest { get; private set; }
    private bool _isApplyingDerivedMotorDistance;
    private bool _isMotorDistanceDerivedFromInterval = true;
    private string _lastMotorDistancePerLineUnit = MotorDistanceUnitMillimeters;
    private bool _isDisposed;
    private bool _areSessionEventsSubscribed;
    private bool _isLoadingConnectedSessionState;
    private bool _loadedProfileHasColorSettings;
    private bool _isConfigProfileLoading;
    private CancellationTokenSource? _configurationActivationCts;
    private int _configurationActivationEpoch;

    public ObservableCollection<string> RowOptions { get; } = new() { "64", "128", "256", "512", "1024", "2048", "4096", "8192" };
    public ObservableCollection<string> DirectionOptions { get; } = new() { ForwardDirection, ReverseDirection };
    public ObservableCollection<string> PreviewModes { get; } = new(ScanPreviewModeOption.CreateKeys(4));
    public ObservableCollection<ScanPreviewModeOption> PreviewModeOptions { get; } = new();
    public ObservableCollection<string> MotorOptions { get; } = new() { "Motor1", "Motor2", "Motor3" };
    public ObservableCollection<string> MotorDistancePerLineUnitOptions { get; } = new(MotorDistanceUnitLabels);
    public ObservableCollection<ScanDngExportMode> DngExportModeOptions { get; } = new() { ScanDngExportMode.LinearRaw4, ScanDngExportMode.LinearRgbIrw };
    public ObservableCollection<ScanChannelAlignmentMode> AlignmentModeOptions { get; } = new() { ScanChannelAlignmentMode.Ecc, ScanChannelAlignmentMode.MutualInformation, ScanChannelAlignmentMode.EccThenMutualInformation };

    [ObservableProperty]
    public partial string SelectedRows { get; set; }

    [ObservableProperty]
    public partial bool IsWarmUpEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsPreviewEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsAlternateMotorDirectionEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsColorManagementEnabled { get; set; }

    [ObservableProperty]
    public partial string SelectedStartingDirection { get; set; }

    [ObservableProperty]
    public partial string SelectedPreviewMode { get; set; }

    [ObservableProperty]
    public partial string RedWavelengthNm { get; set; }

    [ObservableProperty]
    public partial string GreenWavelengthNm { get; set; }

    [ObservableProperty]
    public partial string BlueWavelengthNm { get; set; }

    [ObservableProperty]
    public partial string OutputGamma { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManualWhitePointColorTemperatureEditingEnabled))]
    public partial string SelectedTargetWhitePointMode { get; set; }

    [ObservableProperty]
    public partial string ManualWhitePointColorTemperatureK { get; set; }

    [ObservableProperty]
    public partial ScanDngExportMode SelectedDngExportMode { get; set; }

    [ObservableProperty]
    public partial ScanChannelAlignmentMode SelectedAlignmentMode { get; set; }

    [ObservableProperty]
    public partial bool IsDualFileDngExportAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsSingleFileDngExportForced { get; set; }

    [ObservableProperty]
    public partial bool IsChannel1Reversed { get; set; }

    [ObservableProperty]
    public partial bool IsChannel2Reversed { get; set; }

    [ObservableProperty]
    public partial bool IsChannel3Reversed { get; set; }

    [ObservableProperty]
    public partial bool IsChannel4Reversed { get; set; }

    [ObservableProperty]
    public partial string SelectedScanMotor { get; set; }

    [ObservableProperty]
    public partial string SelectedConfigProfileName { get; set; }

    [ObservableProperty]
    public partial string LoadedScanRecipeSummaryText { get; set; }

    [ObservableProperty]
    public partial string ExecutionConfigSummaryText { get; set; }

    [ObservableProperty]
    public partial string MotorDistancePerLineValue { get; set; }

    [ObservableProperty]
    public partial string MotorDistancePerLineUnit { get; set; }

    [ObservableProperty]
    public partial string MotorIntervalUs { get; set; }

    [ObservableProperty]
    public partial string Pass1DirectionText { get; set; }

    [ObservableProperty]
    public partial string Pass2DirectionText { get; set; }

    [ObservableProperty]
    public partial string Pass3DirectionText { get; set; }

    [ObservableProperty]
    public partial string Pass4DirectionText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    public partial bool IsDevicesPresent { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    public partial bool IsConnecting { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveRgbImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportDngChannelsCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveRgbImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportDngChannelsCommand))]
    public partial bool IsOutputAvailable { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveRgbImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportDngChannelsCommand))]
    public partial bool IsOutputOperationRunning { get; set; }

    [ObservableProperty]
    public partial WriteableBitmap? PreviewImage { get; set; }

    [ObservableProperty]
    public partial bool IsPreviewPlaceholderVisible { get; set; }

    [ObservableProperty]
    public partial double ScanProgressValue { get; set; }

    [ObservableProperty]
    public partial double ScanProgressMaximum { get; set; }

    [ObservableProperty]
    public partial bool IsScanProgressIndeterminate { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string CurrentPassText { get; set; }

    [ObservableProperty]
    public partial string CurrentLedText { get; set; }

    [ObservableProperty]
    public partial string CurrentDirectionText { get; set; }

    public string HeaderScanStatusText => IsRunning
        ? _scanCts?.IsCancellationRequested == true
            ? "Scan_Runtime_StatusStopRequested".GetLocalized()
            : string.Join(" | ", new[] { CurrentPassText, CurrentLedText, CurrentDirectionText }.Where(text => !string.IsNullOrWhiteSpace(text)))
        : string.Empty;

    [ObservableProperty]
    public partial string PreviewPlaceholderText { get; set; }

    [ObservableProperty]
    public partial string PreviewDescriptionText { get; set; }

    [ObservableProperty]
    public partial string ChannelMappingSummaryText { get; set; }

    [ObservableProperty]
    public partial string OutputSummaryText { get; set; }

    [ObservableProperty]
    public partial string DeviceStatusSummaryText { get; set; }

    [ObservableProperty]
    public partial string ConfigurationStatusSummaryText { get; set; }

    [ObservableProperty]
    public partial string ScanStatusSummaryText { get; set; }

    [ObservableProperty]
    public partial string OutputStatusSummaryText { get; set; }

    [ObservableProperty]
    public partial string ReadinessReasonText { get; set; }

    [ObservableProperty]
    public partial string PreparationTitle { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity PreparationSeverity { get; set; }

    public bool IsPreparationVisible => !IsRunning;
    public IReadOnlyList<ScanPreparationStep> PreparationSteps { get; private set; } = [];
    public bool NeedsDeviceConnection => !IsConnected;
    public bool NeedsConfigProfile => !_isConfigProfileLoaded
        || GetEffectiveDeviceChannelRoles().Any(role => ScanChannelRoleHelper.IsActiveRole(role) && !TryGetConfiguredProfile(role, out _))
        || !TryValidateChannelMapping(out _);
    public bool IsConfigProfileLoaded => _isConfigProfileLoaded;
    public string FilmProfileEditorActionText => IsConfigProfileLoaded
        ? "Scan_EditFilmProfileButton/Content".GetLocalizedOrFallback("Edit configuration")
        : _hasEditableFilmProfileSession || HasSavedFilmProfile
            || !_filmProfileWorkspace.Snapshot.CurrentDraft.HasSameContentAs(ScanFilmProfileDraft.CreateDefault())
            || _filmProfileWorkspace.Snapshot.StagedImport is not null
            ? "Scan_ContinueFilmProfileButton/Content".GetLocalizedOrFallback("Continue editing configuration")
            : "Scan_NewFilmProfileButton/Content".GetLocalizedOrFallback("New configuration");

    public void UpdateFilmProfileEditorSession(bool hasEditableDraft)
    {
        if (_hasEditableFilmProfileSession == hasEditableDraft)
            return;

        // Incomplete raw inputs stay in the editor until they can form a valid workspace draft.
        _hasEditableFilmProfileSession = hasEditableDraft;
        OnPropertyChanged(nameof(FilmProfileEditorActionText));
    }

    public string OutputFormatDescriptionText => BuildOutputFormatDescription();
    public string ConfigurationSourceText => !_isConfigProfileLoaded
        ? "Scan_Runtime_ConfigSourceNotLoaded".GetLocalized()
        : "Scan_Runtime_ConfigSourceLoaded".GetLocalizedFormat(SelectedConfigProfileName);
    public string ColorSettingsSourceText => (_loadedProfileHasColorSettings
        ? "Scan_Runtime_ColorSourceProfile"
        : "Scan_Runtime_ColorSourceDefaults").GetLocalized();

    [ObservableProperty]
    public partial string OutputActionReasonText { get; set; }

    [ObservableProperty]
    public partial string ComputedMotorSummaryText { get; set; }

    [ObservableProperty]
    public partial string TopRiskBannerTitle { get; set; }

    [ObservableProperty]
    public partial string TopRiskBannerText { get; set; }

    [ObservableProperty]
    public partial bool IsTopRiskBannerVisible { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity TopRiskBannerSeverity { get; set; }

    [ObservableProperty]
    public partial string StartValidationPromptText { get; set; }

    [ObservableProperty]
    public partial bool IsStartValidationPromptVisible { get; set; }

    [ObservableProperty]
    public partial string DeviceCardBlockerText { get; set; }

    [ObservableProperty]
    public partial bool IsDeviceCardBlocked { get; set; }

    [ObservableProperty]
    public partial Brush DeviceCardBorderBrush { get; set; }

    [ObservableProperty]
    public partial string ConfigurationCardBlockerText { get; set; }

    [ObservableProperty]
    public partial bool IsConfigurationCardBlocked { get; set; }

    [ObservableProperty]
    public partial Brush ConfigurationCardBorderBrush { get; set; }

    [ObservableProperty]
    public partial string ExecutionCardBlockerText { get; set; }

    [ObservableProperty]
    public partial bool IsExecutionCardBlocked { get; set; }

    [ObservableProperty]
    public partial Brush ExecutionCardBorderBrush { get; set; }

    [ObservableProperty]
    public partial string OutputCardBlockerText { get; set; }

    [ObservableProperty]
    public partial bool IsOutputCardBlocked { get; set; }

    [ObservableProperty]
    public partial Brush OutputCardBorderBrush { get; set; }

    [ObservableProperty]
    public partial string FirstBlockingCardId { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManualWhitePointColorTemperatureEditingEnabled))]
    public partial bool IsSetupEditingEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsOutputSettingsEnabled { get; set; }

    public bool IsManualWhitePointColorTemperatureEditingEnabled =>
        IsSetupEditingEnabled
        && string.Equals(
            SelectedTargetWhitePointMode,
            nameof(ScanTargetWhitePointMode.ManualColorTemperature),
            StringComparison.Ordinal);

    public bool HasSavedFilmProfile => _filmProfileWorkspace.LastSavedDocument is not null;

    public bool ShowSavedFilmProfileAction => _filmProfileWorkspace.LastSavedDocument is { } saved
        && (_loadedConfigProfileDraft is null
            || !_loadedConfigProfileDraft.HasSameContentAs(ScanFilmProfileDraft.FromDocument(saved).Draft));

    public string SavedFilmProfileAvailabilityText => _filmProfileWorkspace.LastSavedDocument is { } saved
        ? "Scan_Runtime_SavedFilmProfileAvailable".GetLocalizedFormat(saved.ProfileName)
        : "Scan_Runtime_SavedFilmProfileUnavailable".GetLocalized();

    public ScanViewModel(
        IScannerDeviceSessionManager sessionManager,
        IScanWorkflowSessionCoordinator scanSessionCoordinator,
        IScanParameterService parameters,
        IScanTransferSettingsService transferSettings,
        IScanWorkflowService workflow,
        IDebugOutputMirrorService debugOutputMirror,
        IScanChannelImageService channelImages,
        IScanDeviceSettingsService deviceSettings,
        IScanColorManagementSettingsService colorManagementSettings,
        IScanCalibrationProfileRepository calibrationProfiles,
        IScanFilmProfileFileCoordinator filmProfileFiles,
        IScanFilmProfileWorkspace filmProfileWorkspace,
        IUsbUsageCoordinator usbUsageCoordinator,
        IUiDispatcher dispatcher,
        IScanOutputLocationService? outputLocationService = null)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stepStopwatch = Stopwatch.StartNew();
        _sessionManager = sessionManager;
        _scanSessionCoordinator = scanSessionCoordinator;
        _parameters = parameters;
        _transferSettings = transferSettings;
        _workflow = workflow;
        _debugOutputMirror = debugOutputMirror;
        _channelImages = channelImages;
        _outputLocationService = outputLocationService;
        _deviceSettings = deviceSettings;
        _colorManagementSettings = colorManagementSettings;
        _calibrationProfiles = calibrationProfiles;
        _filmProfileFiles = filmProfileFiles;
        _filmProfileWorkspace = filmProfileWorkspace;
        _usbUsageCoordinator = usbUsageCoordinator;
        _dispatcher = dispatcher;
        _deviceSettingsInitializationTask = _deviceSettings.InitializeAsync();
        NavigationTimingLogger.Write($"ScanViewModel.ctor dependencies={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        SelectedRows = RowOptions[1];
        IsWarmUpEnabled = false;
        IsPreviewEnabled = true;
        IsAlternateMotorDirectionEnabled = true;
        IsColorManagementEnabled = true;
        SelectedStartingDirection = DirectionOptions[0];
        SelectedPreviewMode = PreviewModes[0];
        RedWavelengthNm = DefaultRedWavelengthNm.ToString("0");
        GreenWavelengthNm = DefaultGreenWavelengthNm.ToString("0");
        BlueWavelengthNm = DefaultBlueWavelengthNm.ToString("0");
        OutputGamma = DefaultOutputGamma.ToString("0.0");
        SelectedTargetWhitePointMode = nameof(ScanTargetWhitePointMode.D65);
        ManualWhitePointColorTemperatureK = DefaultManualWhitePointColorTemperatureK.ToString("0");
        SelectedDngExportMode = DngExportModeOptions[0];
        SelectedAlignmentMode = AlignmentModeOptions[0];
        UpdateDngExportModeAvailability();
        IsChannel1Reversed = false;
        IsChannel2Reversed = false;
        IsChannel3Reversed = false;
        IsChannel4Reversed = false;
        SelectedScanMotor = MotorOptions[Math.Min(1, MotorOptions.Count - 1)];
        SelectedConfigProfileName = "Scan_Runtime_ConfigProfileNotSelected".GetLocalized();
        LoadedScanRecipeSummaryText = string.Empty;
        ExecutionConfigSummaryText = string.Empty;
        MotorDistancePerLineValue = string.Empty;
        MotorDistancePerLineUnit = MotorDistanceUnitMillimeters;
        MotorIntervalUs = FormatMotorIntervalInput(ScanDebugConstants.MotionDefaultIntervalNs);
        Pass1DirectionText = ForwardDirection;
        Pass2DirectionText = ReverseDirection;
        Pass3DirectionText = ForwardDirection;
        Pass4DirectionText = ReverseDirection;
        StatusText = "Scan_Runtime_StatusWaitingForDevices".GetLocalized();
        CurrentPassText = "Scan_Runtime_CurrentPassIdle".GetLocalized();
        CurrentLedText = "Scan_Runtime_CurrentLedIdle".GetLocalized();
        CurrentDirectionText = "Scan_Runtime_CurrentDirectionIdle".GetLocalized();
        PreviewPlaceholderText = "Scan_Runtime_PreviewPlaceholderInitial".GetLocalized();
        PreviewDescriptionText = "Scan_Runtime_PreviewModeDescription".GetLocalizedFormat(GetPreviewModeDisplayName(PreviewModes[0]));
        IsPreviewPlaceholderVisible = true;
        ScanProgressValue = 0;
        ScanProgressMaximum = 1;
        IsScanProgressIndeterminate = true;
        OutputSummaryText = "Scan_Runtime_OutputSummaryEmpty".GetLocalized();
        ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorUnavailableUntilParametersLoaded".GetLocalized();
        DeviceStatusSummaryText = string.Empty;
        ConfigurationStatusSummaryText = string.Empty;
        ScanStatusSummaryText = string.Empty;
        OutputStatusSummaryText = string.Empty;
        ReadinessReasonText = string.Empty;
        PreparationTitle = string.Empty;
        OutputActionReasonText = string.Empty;
        TopRiskBannerTitle = string.Empty;
        TopRiskBannerText = string.Empty;
        TopRiskBannerSeverity = InfoBarSeverity.Warning;
        StartValidationPromptText = string.Empty;
        DeviceCardBlockerText = string.Empty;
        ConfigurationCardBlockerText = string.Empty;
        ExecutionCardBlockerText = string.Empty;
        OutputCardBlockerText = string.Empty;
        FirstBlockingCardId = ScanWorkflowBlockerIds.None;
        DeviceCardBorderBrush = ScanCardNormalBorderBrush;
        ConfigurationCardBorderBrush = ScanCardNormalBorderBrush;
        ExecutionCardBorderBrush = ScanCardNormalBorderBrush;
        OutputCardBorderBrush = ScanCardNormalBorderBrush;
        IsSetupEditingEnabled = true;
        IsOutputSettingsEnabled = true;
        NavigationTimingLogger.Write($"ScanViewModel.ctor defaultProperties={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        Activate();
        NavigationTimingLogger.Write($"ScanViewModel.ctor Activate={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        UpdatePassPlan();
        UpdateChannelMappingSummary();
        RefreshLoadedScanRecipeSummary();
        UpdateExecutionConfigSummary();
        UpdatePreviewState();
        UpdateReadinessSummaries();
        NavigationTimingLogger.Write($"ScanViewModel.ctor summaries={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        _ = LoadColorManagementSettingsAsync();
        NavigationTimingLogger.Write($"ScanViewModel.ctor start LoadColorManagementSettingsAsync={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        totalStopwatch.Stop();
        NavigationTimingLogger.Write($"ScanViewModel.ctor total={totalStopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    public void Activate()
    {
        if (_isDisposed)
            return;

        if (!_areSessionEventsSubscribed)
        {
            _configurationActivationCts = CancellationTokenSource.CreateLinkedTokenSource(_uiLifetimeCts.Token);
            _configurationActivationEpoch++;
            _sessionManager.TargetsChanged += OnSessionTargetsChanged;
            _sessionManager.SnapshotChanged += OnSessionSnapshotChanged;
            _usbUsageCoordinator.ActiveLeaseChanged += OnUsbActiveLeaseChanged;
            _filmProfileWorkspace.SnapshotChanged += OnFilmProfileWorkspaceSnapshotChanged;
            _areSessionEventsSubscribed = true;
        }

        ApplyManagerSnapshot(_sessionManager.Snapshot);
        RefreshTargets();
        UpdateReadinessSummaries();
        RefreshSavedFilmProfileAvailability();
        _ = RefreshDeviceSettingsBindingsAsync();

        if (IsConnected && !HasLoadedScannerParameters())
            _ = LoadConnectedSessionStateIfNeededAsync();
    }

    public void Deactivate()
    {
        if (!_areSessionEventsSubscribed)
            return;

        _sessionManager.TargetsChanged -= OnSessionTargetsChanged;
        _sessionManager.SnapshotChanged -= OnSessionSnapshotChanged;
        _usbUsageCoordinator.ActiveLeaseChanged -= OnUsbActiveLeaseChanged;
        _filmProfileWorkspace.SnapshotChanged -= OnFilmProfileWorkspaceSnapshotChanged;
        _areSessionEventsSubscribed = false;
        _configurationActivationEpoch++;
        _configurationActivationCts?.Cancel();
        _configurationActivationCts?.Dispose();
        _configurationActivationCts = null;
        _isConfigProfileLoading = false;
        RefreshInteractionState();
    }

    partial void OnIsAlternateMotorDirectionEnabledChanged(bool value)
        => UpdatePassPlan();

    partial void OnSelectedStartingDirectionChanged(string value)
        => UpdatePassPlan();

    partial void OnSelectedRowsChanged(string value)
    {
        UpdateComputedMotorSummary();
        UpdateExecutionConfigSummary();
    }

    partial void OnMotorDistancePerLineValueChanged(string value)
    {
        if (_isApplyingDerivedMotorDistance)
        {
            UpdateComputedMotorSummary();
            UpdateExecutionConfigSummary();
            return;
        }

        _isMotorDistanceDerivedFromInterval = false;
        UpdateComputedMotorSummary();
        UpdateExecutionConfigSummary();
    }

    partial void OnMotorDistancePerLineUnitChanged(string value)
    {
        var normalizedUnit = ScanMotorDistanceText.NormalizeUnit(value);
        var previousUnit = _lastMotorDistancePerLineUnit;
        _lastMotorDistancePerLineUnit = normalizedUnit;

        if (_isApplyingDerivedMotorDistance)
        {
            UpdateComputedMotorSummary();
            return;
        }

        if (_isMotorDistanceDerivedFromInterval)
        {
            RefreshDerivedMotorDistanceFromCurrentInterval();
            UpdateComputedMotorSummary();
            UpdateExecutionConfigSummary();
            return;
        }

        if (!string.Equals(previousUnit, normalizedUnit, StringComparison.Ordinal)
            && ScanMotorDistanceText.TryParseMillimeters(MotorDistancePerLineValue, previousUnit, GetCurrentMotorSettings(), out var lineDistanceMm)
            && ScanMotorDistanceText.TryFormatDisplayValue(lineDistanceMm, normalizedUnit, GetCurrentMotorSettings(), out var convertedValue))
        {
            ApplyDerivedMotorDistance(convertedValue);
        }

        UpdateComputedMotorSummary();
        UpdateExecutionConfigSummary();
    }

    partial void OnMotorIntervalUsChanged(string value)
    {
        if (_isMotorDistanceDerivedFromInterval)
            RefreshDerivedMotorDistanceFromCurrentInterval();

        UpdateComputedMotorSummary();
        UpdateExecutionConfigSummary();
    }

    partial void OnSelectedScanMotorChanged(string value)
    {
        var configuredMotor = _deviceSettings.Settings.Normalize().DefaultScanMotorId!.Value;
        var mappedSelection = configuredMotor < MotorOptions.Count ? MotorOptions[configuredMotor] : string.Empty;
        if (!string.Equals(value, mappedSelection, StringComparison.Ordinal))
        {
            SelectedScanMotor = mappedSelection;
            return;
        }

        if (_isMotorDistanceDerivedFromInterval)
            RefreshDerivedMotorDistanceFromCurrentInterval();

        UpdateComputedMotorSummary();
        UpdateExecutionConfigSummary();
    }

    partial void OnIsPreviewEnabledChanged(bool value)
        => UpdatePreviewState();

    partial void OnPreviewImageChanged(WriteableBitmap? value)
        => IsPreviewPlaceholderVisible = value is null;

    partial void OnStatusTextChanged(string value)
    {
        MirrorOutput("Scan.Status", value);
        UpdateReadinessSummaries();
    }

    partial void OnCurrentPassTextChanged(string value)
    {
        MirrorOutput("Scan.Pass", value);
        OnPropertyChanged(nameof(HeaderScanStatusText));
        UpdateReadinessSummaries();
    }

    partial void OnCurrentLedTextChanged(string value)
    {
        MirrorOutput("Scan.Led", value);
        OnPropertyChanged(nameof(HeaderScanStatusText));
        UpdateReadinessSummaries();
    }

    partial void OnCurrentDirectionTextChanged(string value)
    {
        MirrorOutput("Scan.Direction", value);
        OnPropertyChanged(nameof(HeaderScanStatusText));
        UpdateReadinessSummaries();
    }

    partial void OnOutputSummaryTextChanged(string value)
    {
        MirrorOutput("Scan.Output", value);
        UpdateReadinessSummaries();
    }

    partial void OnSelectedConfigProfileNameChanged(string value)
        => UpdateReadinessSummaries();

    partial void OnLoadedScanRecipeSummaryTextChanged(string value)
        => UpdateReadinessSummaries();

    partial void OnIsDevicesPresentChanged(bool value)
        => UpdateReadinessSummaries();

    partial void OnIsConnectedChanged(bool value)
        => UpdateReadinessSummaries();

    partial void OnIsConnectingChanged(bool value)
        => UpdateReadinessSummaries();

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(HeaderScanStatusText));
        UpdateReadinessSummaries();
    }

    partial void OnIsOutputAvailableChanged(bool value)
        => UpdateReadinessSummaries();

    partial void OnIsOutputOperationRunningChanged(bool value)
        => UpdateReadinessSummaries();

    partial void OnIsColorManagementEnabledChanged(bool value)
    {
        UpdatePreviewState();
        RefreshLoadedScanRecipeSummary();
    }

    partial void OnSelectedPreviewModeChanged(string value)
        => UpdatePreviewState();

    partial void OnSelectedDngExportModeChanged(ScanDngExportMode value)
    {
        if (value == ScanDngExportMode.LinearRgbIrw && !IsDualFileDngExportAvailable)
            SelectedDngExportMode = ScanDngExportMode.LinearRaw4;

        RefreshLoadedScanRecipeSummary();
        UpdateExecutionConfigSummary();
        OnPropertyChanged(nameof(OutputFormatDescriptionText));
    }

    partial void OnSelectedAlignmentModeChanged(ScanChannelAlignmentMode value)
    {
        RefreshLoadedScanRecipeSummary();
        UpdatePreviewState();
    }

    partial void OnRedWavelengthNmChanged(string value)
    {
        UpdatePreviewState();
        RefreshLoadedScanRecipeSummary();
    }

    partial void OnGreenWavelengthNmChanged(string value)
    {
        UpdatePreviewState();
        RefreshLoadedScanRecipeSummary();
    }

    partial void OnBlueWavelengthNmChanged(string value)
    {
        UpdatePreviewState();
        RefreshLoadedScanRecipeSummary();
    }

    partial void OnOutputGammaChanged(string value)
    {
        UpdatePreviewState();
        RefreshLoadedScanRecipeSummary();
    }

    partial void OnSelectedTargetWhitePointModeChanged(string value)
    {
        UpdatePreviewState();
        RefreshLoadedScanRecipeSummary();
    }

    partial void OnManualWhitePointColorTemperatureKChanged(string value)
    {
        UpdatePreviewState();
        RefreshLoadedScanRecipeSummary();
    }

    partial void OnIsChannel1ReversedChanged(bool value)
        => OnChannelAssignmentChanged();

    partial void OnIsChannel2ReversedChanged(bool value)
        => OnChannelAssignmentChanged();

    partial void OnIsChannel3ReversedChanged(bool value)
        => OnChannelAssignmentChanged();

    partial void OnIsChannel4ReversedChanged(bool value)
        => OnChannelAssignmentChanged();

    private void OnChannelAssignmentChanged()
    {
        UpdateDngExportModeAvailability();
        UpdateChannelMappingSummary();
        UpdatePreviewState();
    }

    private void UpdateDngExportModeAvailability()
    {
        var roles = GetPreviewChannelSlots().Select(slot => GetOutputChannelAssignment().Roles[slot]).ToArray();
        IsDualFileDngExportAvailable = HasRgbChannels(roles)
            && ScanChannelRoleHelper.CountIrOrWhiteRoles(roles) is 1 or 2
            && roles.Length == 3 + ScanChannelRoleHelper.CountIrOrWhiteRoles(roles)
            && roles.Distinct(StringComparer.OrdinalIgnoreCase).Count() == roles.Length;
        IsSingleFileDngExportForced = !IsDualFileDngExportAvailable;

        if (!IsDualFileDngExportAvailable && SelectedDngExportMode == ScanDngExportMode.LinearRgbIrw)
            SelectedDngExportMode = ScanDngExportMode.LinearRaw4;
        OnPropertyChanged(nameof(OutputFormatDescriptionText));
    }

    private void OnSessionTargetsChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(RefreshTargets);

    private void OnSessionSnapshotChanged(object? sender, ScannerDeviceSessionSnapshot snapshot)
        => _dispatcher.TryEnqueue(() => ApplyManagerSnapshot(snapshot));

    private void OnUsbActiveLeaseChanged(object? sender, UsbUsageLeaseSnapshot? snapshot)
        => _dispatcher.TryEnqueue(UpdateReadinessSummaries);

    private void ApplyManagerSnapshot(ScannerDeviceSessionSnapshot snapshot)
    {
        var isOwnedByThisPage = _scanSessionCoordinator.OwnsSnapshot(snapshot);

        IsConnecting = snapshot.State == ScannerSessionState.Connecting && isOwnedByThisPage;
        IsConnected = snapshot.State is ScannerSessionState.Connected or ScannerSessionState.Running;

        if (isOwnedByThisPage && snapshot.State == ScannerSessionState.Running)
        {
            IsRunning = true;
            return;
        }

        if (!isOwnedByThisPage || snapshot.State is ScannerSessionState.Connected or ScannerSessionState.Disconnected or ScannerSessionState.Faulted or ScannerSessionState.ReconnectPrompt)
            IsRunning = false;
    }

    private void RefreshTargets()
    {
        IsDevicesPresent = _sessionManager.Targets.IsDevicesPresent;

        if (!IsConnected && !IsConnecting)
        {
            StatusText = IsDevicesPresent
                ? "Scan_Runtime_StatusDevicesDetected".GetLocalized()
                : "Scan_Runtime_StatusWaitingForDevices".GetLocalized();
        }
    }

    private bool CanConnectDevices() => IsDevicesPresent && !IsConnected && !IsConnecting && !IsRunning;
    private bool CanDisconnectDevices() => IsConnected && !IsConnecting && !IsRunning && !IsOutputOperationRunning;
    private bool CanStartScan() => !IsConnecting && !IsRunning && !IsOutputOperationRunning && !_isConfigProfileLoading
        && GetPreparationBlockers().Count == 0;
    private bool CanStopScan() => IsRunning;
    private bool HasLoadedScannerParameters() => _loadedSnapshot is not null && _loadedSysClockKhz >= ScanDebugConstants.MinSysClockKhz;
    private bool CanSaveRgbImage() => IsOutputAvailable && !IsRunning && !IsOutputOperationRunning
        && HasRgbChannels(GetPreviewChannelSlots().Select(slot => GetOutputChannelAssignment().Roles[slot]));
    private bool CanExportDngChannels() => IsOutputAvailable && !IsRunning && !IsOutputOperationRunning;

    private void UpdateReadinessSummaries()
    {
        DeviceStatusSummaryText = BuildDeviceStatusSummary();
        ConfigurationStatusSummaryText = BuildConfigurationStatusSummary();
        ScanStatusSummaryText = BuildScanStatusSummary();
        OutputStatusSummaryText = "Scan_Runtime_OutputStatusSummary".GetLocalizedFormatOrFallback("{0}", OutputSummaryText ?? string.Empty);
        var blockers = GetPreparationBlockers();
        ReadinessReasonText = blockers.Count > 0
            ? string.Join("\n", blockers.Select(blocker => blocker.Message).Distinct())
            : BuildReadinessReason();
        PreparationTitle = blockers.Count > 0
            ? "Scan_Runtime_PreparationRemaining".GetLocalizedFormat(blockers.Select(blocker => blocker.CardId).Distinct().Count())
            : "Scan_Runtime_PreparationReady".GetLocalized();
        PreparationSeverity = blockers.Count > 0 ? InfoBarSeverity.Informational : InfoBarSeverity.Success;
        OutputActionReasonText = BuildOutputActionReason();
        RefreshInteractionState();
        OnPropertyChanged(nameof(IsPreparationVisible));
        OnPropertyChanged(nameof(NeedsDeviceConnection));
        OnPropertyChanged(nameof(NeedsConfigProfile));
        OnPropertyChanged(nameof(IsConfigProfileLoaded));
        OnPropertyChanged(nameof(FilmProfileEditorActionText));
        OnPropertyChanged(nameof(ConfigurationSourceText));
        OnPropertyChanged(nameof(ColorSettingsSourceText));
        OnPropertyChanged(nameof(OutputFormatDescriptionText));
        StartScanCommand.NotifyCanExecuteChanged();

        if (PreviewImage is null && IsPreviewEnabled && _lastResult is null)
            PreviewPlaceholderText = BuildPreviewPlaceholderText();
    }

    private ScanPreparationAction GetPreparationAction(string cardId) => cardId switch
    {
        ScanWorkflowBlockerIds.Device => ScanPreparationAction.ShowConnection,
        ScanWorkflowBlockerIds.Configuration when !IsConfigProfileLoaded && HasSavedFilmProfile => ScanPreparationAction.UseSavedConfiguration,
        ScanWorkflowBlockerIds.Configuration when !IsConfigProfileLoaded => ScanPreparationAction.LoadConfiguration,
        ScanWorkflowBlockerIds.Configuration when GetEffectiveDeviceChannelRoles().Any(role =>
            ScanChannelRoleHelper.IsActiveRole(role) && !TryGetConfiguredProfile(role, out _)) => ScanPreparationAction.ReplaceConfiguration,
        ScanWorkflowBlockerIds.Configuration when !TryValidateChannelMapping(out _) => ScanPreparationAction.ReviewDeviceMappings,
        ScanWorkflowBlockerIds.Configuration => ScanPreparationAction.ReviewColorSettings,
        ScanWorkflowBlockerIds.Execution => ScanPreparationAction.ReviewMotionSettings,
        _ => ScanPreparationAction.ShowOutputStatus
    };

    private static string GetPreparationStepLabel(ScanPreparationAction action) => action switch
    {
        ScanPreparationAction.ShowConnection => "Scan_PreparationDeviceAction".GetLocalized(),
        ScanPreparationAction.LoadConfiguration => "Scan_PreparationLoadAction".GetLocalized(),
        ScanPreparationAction.UseSavedConfiguration => "Scan_PreparationUseSavedAction".GetLocalized(),
        ScanPreparationAction.ReplaceConfiguration => "Scan_PreparationParametersAction".GetLocalizedOrFallback("Channel parameters missing · Choose configuration…"),
        ScanPreparationAction.ReviewDeviceMappings => "Scan_PreparationMappingAction".GetLocalizedOrFallback("Channel mapping needs attention · Review mapping →"),
        ScanPreparationAction.ReviewColorSettings => "Scan_PreparationColorAction".GetLocalized(),
        ScanPreparationAction.ReviewMotionSettings => "Scan_PreparationExecutionAction".GetLocalized(),
        _ => "Scan_PreparationOutputAction".GetLocalized()
    };

    private string BuildDeviceStatusSummary()
    {
        if (IsConnecting)
            return "Scan_Runtime_DeviceStatusConnecting".GetLocalizedOrFallback("Connecting scanner hardware...");

        if (IsConnected)
            return "Scan_Runtime_DeviceStatusConnected".GetLocalizedOrFallback("Scanner connected.");

        return IsDevicesPresent
            ? "Scan_Runtime_DeviceStatusDetected".GetLocalizedOrFallback("Scanner detected and ready to connect.")
            : "Scan_Runtime_DeviceStatusMissing".GetLocalizedOrFallback("No scanner detected.");
    }

    private List<ScanStartBlocker> GetPreparationBlockers()
    {
        var blockers = new List<ScanStartBlocker>();
        if (_usbUsageCoordinator.IsUsbDebugInUse)
            blockers.Add(new(ScanWorkflowBlockerIds.Device, "Scan_Runtime_UsbDebugActive".GetLocalized()));
        else if (IsConnecting)
            blockers.Add(new(ScanWorkflowBlockerIds.Device, "Scan_Runtime_ReadinessConnecting".GetLocalized()));
        else if (!IsDevicesPresent || !IsConnected)
            blockers.Add(new(ScanWorkflowBlockerIds.Device, (IsDevicesPresent
                ? "Scan_Runtime_ReadinessConnectFirst"
                : "Scan_Runtime_ReadinessNoDevices").GetLocalized()));
        else if (!HasLoadedScannerParameters())
            blockers.Add(new(ScanWorkflowBlockerIds.Device, "Scan_Runtime_ReadinessParametersMissing".GetLocalized()));

        if (!_isConfigProfileLoaded)
            blockers.Add(new(ScanWorkflowBlockerIds.Configuration, "Scan_Runtime_ReadinessConfigRequired".GetLocalized()));
        else if (GetEffectiveDeviceChannelRoles().Any(role => ScanChannelRoleHelper.IsActiveRole(role) && !TryGetConfiguredProfile(role, out _)))
            blockers.Add(new(ScanWorkflowBlockerIds.Configuration, "Scan_Runtime_ConfigChannelProfilesMissing".GetLocalized()));

        if (_isConfigProfileLoaded && !TryValidateChannelMapping(out var mappingError))
            blockers.Add(new(ScanWorkflowBlockerIds.Configuration, mappingError));

        if (_isConfigProfileLoaded && !TryBuildColorManagementOptions(out _, out var colorError))
            blockers.Add(new(ScanWorkflowBlockerIds.Configuration, colorError));

        if (IsOutputOperationRunning)
            blockers.Add(new(ScanWorkflowBlockerIds.Output, "Scan_Runtime_ReadinessOutputBusy".GetLocalized()));

        if (!int.TryParse(SelectedRows, out var rows) || rows <= 0)
            blockers.Add(new(ScanWorkflowBlockerIds.Execution, "Scan_Runtime_ErrorRowsPositiveInteger".GetLocalized()));

        if (!TryParseSelectedMotor(out _, out var motorError))
            blockers.Add(new(ScanWorkflowBlockerIds.Execution, motorError));
        else if (HasLoadedScannerParameters() && !TryGetEffectiveMotorIntervalNs(GetCurrentMotorSettings(), out _, updateInput: false))
            blockers.Add(new(ScanWorkflowBlockerIds.Execution, "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs))));

        return blockers;
    }

    private string BuildConfigurationStatusSummary()
        => _isConfigProfileLoaded
            ? "Scan_Runtime_ConfigStatusLoaded".GetLocalizedFormatOrFallback("{0}", SelectedConfigProfileName ?? string.Empty)
            : "Scan_Runtime_ConfigStatusMissing".GetLocalizedOrFallback("No scan configuration loaded.");

    private void UpdateExecutionConfigSummary()
    {
        if (_isRestoringLoadedSettings)
            return;
        var rows = string.IsNullOrWhiteSpace(SelectedRows)
            ? "Scan_Runtime_ExecutionConfigUnavailable".GetLocalizedOrFallback("Unavailable")
            : SelectedRows;
        var motor = string.IsNullOrWhiteSpace(SelectedScanMotor)
            ? "Scan_Runtime_ExecutionConfigUnavailable".GetLocalizedOrFallback("Unavailable")
            : SelectedScanMotor;
        var lineMove = BuildLineMoveSummary();
        var dngMode = GetDngExportModeDisplayName(SelectedDngExportMode);

        ExecutionConfigSummaryText = "Scan_Runtime_ExecutionConfigSummary".GetLocalizedFormatOrFallback("Rows: {0} | Motor: {1} | Line move: {2} | DNG: {3}", rows, motor, lineMove, dngMode);
        if (GetEffectiveDeviceChannelRoles().Any(role => role == "White"))
            ExecutionConfigSummaryText += " | " + BuildWhiteSourceSummary(GetConfiguredWhiteLightSource());
        UpdateReadinessSummaries();
    }

    private string BuildLineMoveSummary()
    {
        var interval = string.IsNullOrWhiteSpace(MotorIntervalUs)
            ? "Scan_Runtime_ExecutionConfigUnavailable".GetLocalizedOrFallback("Unavailable")
            : MotorIntervalUs;

        return string.IsNullOrWhiteSpace(MotorDistancePerLineValue)
            ? "Scan_Runtime_ExecutionConfigIntervalOnly".GetLocalizedFormatOrFallback("Not set (step interval {0} μs)", interval)
            : "Scan_Runtime_ExecutionConfigLineMove".GetLocalizedFormatOrFallback("{0} {1} @ {2} us", MotorDistancePerLineValue, MotorDistancePerLineUnit, interval);
    }

    private string BuildScanStatusSummary()
    {
        if (IsRunning)
            return "Scan_Runtime_ScanStatusRunning".GetLocalizedOrFallback("Scan running.");

        if (IsOutputAvailable)
            return "Scan_Runtime_ScanStatusComplete".GetLocalizedOrFallback("Latest scan complete.");

        return "Scan_Runtime_ScanStatusWaiting".GetLocalizedOrFallback("Waiting to start a scan.");
    }

    private string BuildReadinessReason()
    {
        if (IsRunning)
            return "Scan_Runtime_ReadinessStopAvailable".GetLocalizedOrFallback("Scan is running. Stop the current scan before changing setup.");

        if (IsOutputOperationRunning)
            return "Scan_Runtime_ReadinessOutputBusy".GetLocalizedOrFallback("An output operation is still running. Wait for it to finish before starting another scan.");

        if (IsConnecting)
            return "Scan_Runtime_ReadinessConnecting".GetLocalizedOrFallback("Connecting to scanner hardware...");

        if (!IsDevicesPresent)
            return "Scan_Runtime_ReadinessNoDevices".GetLocalizedOrFallback("Connect a PRISM scanner before starting a scan.");

        if (!IsConnected)
            return "Scan_Runtime_ReadinessConnectFirst".GetLocalizedOrFallback("Connect to the scanner before starting a scan.");

        if (!_isConfigProfileLoaded)
            return "Scan_Runtime_ReadinessConfigRequired".GetLocalizedOrFallback("Load a scan configuration before starting.");

        if (!HasLoadedScannerParameters())
            return "Scan_Runtime_ReadinessParametersMissing".GetLocalizedOrFallback("Scanner parameters are still missing. Reconnect before starting.");

        return "Scan_Runtime_ReadinessReady".GetLocalizedOrFallback("Ready to start scanning.");
    }

    private string BuildOutputActionReason()
    {
        if (IsOutputOperationRunning)
            return "Scan_Runtime_OutputActionBusy".GetLocalizedOrFallback("Output operation in progress.");

        if (IsRunning)
            return "Scan_Runtime_OutputActionScanRunning".GetLocalizedOrFallback("Scan is running.");

        return IsOutputAvailable
            ? "Scan_Runtime_OutputActionReady".GetLocalizedOrFallback("Save or export the latest scan result.")
            : "Scan_Runtime_OutputActionNoResult".GetLocalizedOrFallback("No scan result is available yet.");
    }

    private void RefreshInteractionState()
    {
        var canEdit = !IsConnecting && !IsRunning && !IsOutputOperationRunning && !_isConfigProfileLoading;
        IsSetupEditingEnabled = canEdit;
        IsOutputSettingsEnabled = canEdit;
        UseSavedFilmProfileCommand.NotifyCanExecuteChanged();
        LoadConfigProfileCommand.NotifyCanExecuteChanged();
        StartScanCommand.NotifyCanExecuteChanged();
        RestoreLoadedSettingsCommand.NotifyCanExecuteChanged();
        RefreshPreparationSteps();
    }

    private void RefreshPreparationSteps()
    {
        var preparationSteps = GetPreparationBlockers().GroupBy(blocker => blocker.CardId)
            .Select(group => new ScanPreparationStep(group.Key, GetPreparationStepLabel(GetPreparationAction(group.Key)),
                string.Join("\n", group.Select(blocker => blocker.Message).Distinct()),
                group.Key is ScanWorkflowBlockerIds.Device or ScanWorkflowBlockerIds.Output || IsSetupEditingEnabled,
                GetPreparationAction(group.Key)))
            .ToArray();
        if (!PreparationSteps.SequenceEqual(preparationSteps))
        {
            PreparationSteps = preparationSteps;
            OnPropertyChanged(nameof(PreparationSteps));
        }
    }

    private void OnFilmProfileWorkspaceSnapshotChanged(ScanFilmProfileWorkspaceSnapshot snapshot)
        => _dispatcher.TryEnqueue(() =>
        {
            if (!_isDisposed && _areSessionEventsSubscribed)
                RefreshSavedFilmProfileAvailability();
        });

    private void RefreshSavedFilmProfileAvailability()
    {
        OnPropertyChanged(nameof(HasSavedFilmProfile));
        OnPropertyChanged(nameof(ShowSavedFilmProfileAction));
        OnPropertyChanged(nameof(SavedFilmProfileAvailabilityText));
        OnPropertyChanged(nameof(FilmProfileEditorActionText));
        UseSavedFilmProfileCommand.NotifyCanExecuteChanged();
        RefreshPreparationSteps();
    }

    private async Task<PreparedScanRequest?> ValidateStartScanAsync()
    {
        ClearStartValidationState();

        var blockers = new List<ScanStartBlocker>();
        if (_usbUsageCoordinator.IsUsbDebugInUse)
        {
            var message = "Scan_Runtime_UsbDebugActive".GetLocalized();
            blockers.Add(new(ScanWorkflowBlockerIds.Device, message));
            ShowTopRiskBanner("Scan_Runtime_TopRiskUsbDebugTitle".GetLocalizedOrFallback("USB debug owns the scanner"), message);
        }
        else
        {
            ClearTopRiskBanner();
        }

        if (IsConnecting)
            blockers.Add(new(ScanWorkflowBlockerIds.Device, "Scan_Runtime_ReadinessConnecting".GetLocalizedOrFallback("Connecting to scanner hardware...")));

        if (IsRunning)
            blockers.Add(new(ScanWorkflowBlockerIds.Execution, "Scan_Runtime_ReadinessStopAvailable".GetLocalizedOrFallback("Scan is running. Stop the current scan before changing setup.")));

        if (IsOutputOperationRunning)
            blockers.Add(new(ScanWorkflowBlockerIds.Output, "Scan_Runtime_ReadinessOutputBusy".GetLocalizedOrFallback("An output operation is still running. Wait for it to finish before starting another scan.")));

        if (!IsDevicesPresent)
            blockers.Add(new(ScanWorkflowBlockerIds.Device, "Scan_Runtime_ReadinessNoDevices".GetLocalizedOrFallback("Connect a PRISM scanner before starting a scan.")));
        else if (!IsConnected)
            blockers.Add(new(ScanWorkflowBlockerIds.Device, "Scan_Runtime_ReadinessConnectFirst".GetLocalizedOrFallback("Connect to the scanner before starting a scan.")));

        if (!_isConfigProfileLoaded)
            blockers.Add(new(ScanWorkflowBlockerIds.Configuration, "Scan_Runtime_ReadinessConfigRequired".GetLocalizedOrFallback("Load a scan configuration before starting.")));

        if (!HasLoadedScannerParameters())
            blockers.Add(new(ScanWorkflowBlockerIds.Device, "Scan_Runtime_ReadinessParametersMissing".GetLocalizedOrFallback("Scanner parameters are still missing. Reconnect before starting.")));

        if (!int.TryParse(SelectedRows, out var rows) || rows <= 0)
            blockers.Add(new(ScanWorkflowBlockerIds.Execution, "Scan_Runtime_ErrorRowsPositiveInteger".GetLocalized()));

        if (!TryParseSelectedMotor(out var motorId, out var motorError))
        {
            blockers.Add(new(ScanWorkflowBlockerIds.Execution, motorError));
        }
        else if (HasLoadedScannerParameters())
        {
            await _deviceSettingsInitializationTask;
            var motorSettings = _deviceSettings.Settings.GetMotorSettings(motorId);
            if (!TryGetEffectiveMotorIntervalNs(motorSettings, out _))
                blockers.Add(new(ScanWorkflowBlockerIds.Execution, "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs))));
        }

        if (blockers.Count > 0)
        {
            ApplyStartValidationBlockers(blockers);
            StatusText = _usbUsageCoordinator.IsUsbDebugInUse
                ? "Scan_Runtime_UsbDebugActive".GetLocalized()
                : "Scan_Runtime_StartValidationFailed".GetLocalizedOrFallback("Scan prerequisites need attention before the workflow can start.");
            return null;
        }

        if (!TryBuildWorkflowRequest(out var request, out var error))
        {
            ApplyStartValidationBlockers(new[] { new ScanStartBlocker(ScanWorkflowBlockerIds.Execution, error) });
            StatusText = "Scan_Runtime_StartValidationFailed".GetLocalizedOrFallback("Scan prerequisites need attention before the workflow can start.");
            return null;
        }

        // Keep references paired with the submitted parameters before validation yields.
        var preparedRequest = new PreparedScanRequest(request, CaptureChannelProfiles(request));
        var singleTransferMaxRows = await _scanSessionCoordinator.UseConnectedSessionAsync(
            (session, _) => Task.FromResult(session.SingleTransferMaxRows),
            CancellationToken.None);

        if (request.Rows > singleTransferMaxRows && !request.WarmUpEnabled && !CanRunExtendedScan())
        {
            var message = "Scan_Runtime_StatusRowsLimitExceeded".GetLocalizedFormat(singleTransferMaxRows);
            ApplyStartValidationBlockers(new[] { new ScanStartBlocker(ScanWorkflowBlockerIds.Execution, message) });
            StatusText = message;
            return null;
        }

        return preparedRequest;
    }

    private void ApplyStartValidationBlockers(IEnumerable<ScanStartBlocker> blockers)
    {
        var blockerList = blockers.ToList();
        ResetCardBlockers();
        IsStartValidationPromptVisible = true;
        StartValidationPromptText = "Scan_Runtime_StartValidationPrompt".GetLocalizedOrFallback("Review the highlighted preparation cards, correct the blockers, then start the scan again.");
        FirstBlockingCardId = blockerList.FirstOrDefault()?.CardId ?? ScanWorkflowBlockerIds.None;

        foreach (var blocker in blockerList)
            AppendCardBlocker(blocker.CardId, blocker.Message);
    }

    private void ClearStartValidationState()
    {
        IsStartValidationPromptVisible = false;
        StartValidationPromptText = string.Empty;
        FirstBlockingCardId = ScanWorkflowBlockerIds.None;
        ResetCardBlockers();
    }

    private void ResetCardBlockers()
    {
        DeviceCardBlockerText = string.Empty;
        ConfigurationCardBlockerText = string.Empty;
        ExecutionCardBlockerText = string.Empty;
        OutputCardBlockerText = string.Empty;
        IsDeviceCardBlocked = false;
        IsConfigurationCardBlocked = false;
        IsExecutionCardBlocked = false;
        IsOutputCardBlocked = false;
        DeviceCardBorderBrush = ScanCardNormalBorderBrush;
        ConfigurationCardBorderBrush = ScanCardNormalBorderBrush;
        ExecutionCardBorderBrush = ScanCardNormalBorderBrush;
        OutputCardBorderBrush = ScanCardNormalBorderBrush;
    }

    private void AppendCardBlocker(string cardId, string message)
    {
        switch (cardId)
        {
            case ScanWorkflowBlockerIds.Device:
                DeviceCardBlockerText = AppendBlockerText(DeviceCardBlockerText, message);
                IsDeviceCardBlocked = true;
                DeviceCardBorderBrush = ScanCardBlockerBorderBrush;
                break;
            case ScanWorkflowBlockerIds.Configuration:
                ConfigurationCardBlockerText = AppendBlockerText(ConfigurationCardBlockerText, message);
                IsConfigurationCardBlocked = true;
                ConfigurationCardBorderBrush = ScanCardBlockerBorderBrush;
                break;
            case ScanWorkflowBlockerIds.Output:
                OutputCardBlockerText = AppendBlockerText(OutputCardBlockerText, message);
                IsOutputCardBlocked = true;
                OutputCardBorderBrush = ScanCardBlockerBorderBrush;
                break;
            default:
                ExecutionCardBlockerText = AppendBlockerText(ExecutionCardBlockerText, message);
                IsExecutionCardBlocked = true;
                ExecutionCardBorderBrush = ScanCardBlockerBorderBrush;
                break;
        }
    }

    private static string AppendBlockerText(string current, string message)
        => string.IsNullOrWhiteSpace(current) ? message : $"{current}\n{message}";

    private void ShowTopRiskBanner(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Warning)
    {
        TopRiskBannerTitle = title;
        TopRiskBannerText = message;
        TopRiskBannerSeverity = severity;
        IsTopRiskBannerVisible = true;
    }

    private void ClearTopRiskBanner()
    {
        TopRiskBannerTitle = string.Empty;
        TopRiskBannerText = string.Empty;
        TopRiskBannerSeverity = InfoBarSeverity.Warning;
        IsTopRiskBannerVisible = false;
    }

    private async Task LoadConnectedSessionStateIfNeededAsync()
    {
        if (_isLoadingConnectedSessionState || !IsConnected || HasLoadedScannerParameters())
            return;

        _isLoadingConnectedSessionState = true;
        var statusNotes = new List<string>();

        try
        {
            await _transferSettings.InitializeAsync();
            await _calibrationProfiles.InitializeAsync(CancellationToken.None);
            await _deviceSettings.InitializeAsync();
            OnChannelAssignmentChanged();

            var snapshot = await _scanSessionCoordinator.UseConnectedSessionAsync(
                (session, token) => _parameters.LoadAsync(session, token),
                CancellationToken.None);

            _loadedExposureTicks = snapshot.ExposureTicks;
            _loadedSysClockKhz = snapshot.SysClockKhz;
            _loadedSnapshot = snapshot;
            StartScanCommand.NotifyCanExecuteChanged();
            RefreshDerivedMotorDistanceFromCurrentInterval();
            statusNotes.Add("Scan_Runtime_StatusParametersLoaded".GetLocalizedFormat(snapshot.ExposureTicks, snapshot.SysClockKhz));
        }
        catch (Exception ex)
        {
            _loadedSnapshot = null;
            _loadedExposureTicks = 0;
            _loadedSysClockKhz = 0;
            StartScanCommand.NotifyCanExecuteChanged();
            statusNotes.Add("Scan_Runtime_StatusParameterLoadUnavailable".GetLocalizedFormat(ex.Message));
        }
        finally
        {
            _isLoadingConnectedSessionState = false;
        }

        if (_selectedConfigAcquisitionSettings is not null)
            ApplyAcquisitionSettingsToInputs(_selectedConfigAcquisitionSettings);

        UpdateComputedMotorSummary();
        StatusText = statusNotes.Count > 0
            ? "Scan_Runtime_StatusConnectedWithNotes".GetLocalizedFormat(string.Join(". ", statusNotes))
            : "Scan_Runtime_StatusConnected".GetLocalized();
        ClearTopRiskBanner();
    }

    [RelayCommand(CanExecute = nameof(CanConnectDevices))]
    private async Task ConnectDevices()
    {
        if (_usbUsageCoordinator.IsUsbDebugInUse)
        {
            StatusText = "Scan_Runtime_UsbDebugActive".GetLocalized();
            ShowTopRiskBanner("Scan_Runtime_TopRiskUsbDebugTitle".GetLocalizedOrFallback("USB debug owns the scanner"), StatusText);
            return;
        }

        IsConnecting = true;
        try
        {
            await _transferSettings.InitializeAsync();
            await _calibrationProfiles.InitializeAsync(CancellationToken.None);
            await _deviceSettings.InitializeAsync();
            OnChannelAssignmentChanged();

            var result = await _scanSessionCoordinator.ConnectAsync(CancellationToken.None);
            if (!result.Success)
            {
                StatusText = ScanRuntimeMessageLocalizer.LocalizeScanViewStatus(result.Message);
                ShowTopRiskBanner("Scan_Runtime_TopRiskConnectFailedTitle".GetLocalizedOrFallback("Scanner connection needs attention"), StatusText);
                return;
            }

            IsConnected = true;
            StatusText = "Scan_Runtime_StatusLoadingState".GetLocalized();
            await LoadConnectedSessionStateIfNeededAsync();
        }
        catch (Exception ex)
        {
            await _scanSessionCoordinator.DisconnectAsync(CancellationToken.None);
            IsConnected = false;
            StatusText = "Scan_Runtime_StatusConnectFailed".GetLocalizedFormat(ex.Message);
            ShowTopRiskBanner("Scan_Runtime_TopRiskConnectFailedTitle".GetLocalizedOrFallback("Scanner connection needs attention"), StatusText);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnectDevices))]
    private async Task DisconnectDevices()
    {
        IsConnecting = true;
        try
        {
            await _scanSessionCoordinator.DisconnectAsync(CancellationToken.None);
            IsConnected = false;
            _loadedSnapshot = null;
            _loadedExposureTicks = 0;
            _loadedSysClockKhz = 0;
            IsOutputAvailable = false;
            ScanProgressValue = 0;
            _lastResult = null;
            _lastResultChannelAssignment = null;
            _lastResultWhiteLightSource = null;
            _lastResultChannelProfiles = null;
            UpdateDngExportModeAvailability();
            UpdatePreviewModeOptions();
            PreviewImage = null;
            PreviewPlaceholderText = "Scan_Runtime_PreviewPlaceholderInitial".GetLocalized();
            OutputSummaryText = "Scan_Runtime_OutputSummaryEmpty".GetLocalized();
            StatusText = IsDevicesPresent ? "Scan_Runtime_StatusDisconnectedReconnect".GetLocalized() : "Scan_Runtime_StatusDisconnected".GetLocalized();
            CurrentPassText = "Scan_Runtime_CurrentPassIdle".GetLocalized();
            CurrentLedText = "Scan_Runtime_CurrentLedIdle".GetLocalized();
            CurrentDirectionText = "Scan_Runtime_CurrentDirectionIdle".GetLocalized();
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartScan))]
    private async Task StartScan()
    {
        var preparedRequest = await ValidateStartScanAsync();
        if (_isDisposed || preparedRequest is null)
            return;

        var request = preparedRequest.Request;
        ClearStartValidationState();
        ClearTopRiskBanner();

        _scanCts = new CancellationTokenSource();
        var uiToken = _uiLifetimeCts.Token;
        ResetScanPresentationForNewRun();
        IsRunning = true;
        ScanProgressValue = 0;
        ScanProgressMaximum = 1;
        IsScanProgressIndeterminate = true;
        IsOutputAvailable = false;
        _lastResult = null;
        _lastResultChannelAssignment = null;
        _lastResultWhiteLightSource = null;
        _lastResultChannelProfiles = null;
        var captureAssignment = request.AcquisitionSettings?.AcquisitionChannelAssignment ?? BuildChannelAssignment();
        var captureWhiteSource = request.AcquisitionSettings?.WhiteChannelSettings?.Source ?? GetConfiguredWhiteLightSource();
        UpdatePreviewModeOptions();
        UpdatePreviewState();
        OutputSummaryText = "Scan_Runtime_OutputSummaryInProgress".GetLocalized();

        try
        {
            var result = await _scanSessionCoordinator.RunConnectedSessionStateAsync(
                ScannerSessionState.Running,
                (session, _) => _workflow.ExecuteAsync(
                    session,
                    request,
                    _scanCts.Token,
                    progress =>
                    {
                        if (!uiToken.IsCancellationRequested)
                            _dispatcher.TryEnqueue(() =>
                            {
                                if (!uiToken.IsCancellationRequested)
                                    ApplyProgress(progress);
                            });
                    },
                    status =>
                    {
                        if (!uiToken.IsCancellationRequested)
                            _dispatcher.TryEnqueue(() =>
                            {
                                if (!uiToken.IsCancellationRequested)
                                    StatusText = ScanRuntimeMessageLocalizer.LocalizeScanViewStatus(status);
                            });
                    },
                    diagnostic => _debugOutputMirror.Mirror("Scan.Diagnostic", diagnostic)),
                CancellationToken.None,
                waitForAvailability: false);

            if (uiToken.IsCancellationRequested)
                return;

            _lastResult = result;
            _lastResultChannelAssignment = captureAssignment;
            _lastResultWhiteLightSource = captureWhiteSource;
            _lastResultChannelProfiles = preparedRequest.ChannelProfiles;
            UpdateDngExportModeAvailability();
            UpdatePreviewModeOptions();
            IsOutputAvailable = true;
            ScanProgressMaximum = Math.Max(1, result.Passes.Count);
            ScanProgressValue = ScanProgressMaximum;
            IsScanProgressIndeterminate = false;
            OutputSummaryText = "Scan_Runtime_OutputSummaryCaptured".GetLocalizedFormat(result.Passes.Count, result.Rows, result.ComputedMotorStepsPerPass);
            StatusText = "Scan_Runtime_StatusCompleted".GetLocalized();
            RecordScanCompletion("Scan_CompletedFeedback".GetLocalizedFormatOrFallback(
                "Scan complete · {0} passes · {1} rows. Ready to save or export.", result.Passes.Count, result.Rows), InfoBarSeverity.Success);
            ClearTopRiskBanner();
            UpdatePreviewState();
        }
        catch (OperationCanceledException)
        {
            if (uiToken.IsCancellationRequested)
                return;

            StatusText = "Scan_Runtime_StatusCanceled".GetLocalized();
            RecordScanCompletion(StatusText, InfoBarSeverity.Warning);
            CurrentPassText = "Scan_Runtime_CurrentPassCanceled".GetLocalized();
            CurrentLedText = "Scan_Runtime_CurrentLedStopped".GetLocalized();
            CurrentDirectionText = "Scan_Runtime_CurrentDirectionStopped".GetLocalized();
            ShowTopRiskBanner("Scan_Runtime_TopRiskInterruptedTitle".GetLocalizedOrFallback("Scan interrupted"), "Scan_Runtime_TopRiskInterruptedText".GetLocalizedOrFallback("The workflow was interrupted. Confirm film transport and scanner state before restarting."));
        }
        catch (Exception ex)
        {
            if (uiToken.IsCancellationRequested)
                return;

            StatusText = "Scan_Runtime_StatusFailed".GetLocalizedFormat(ScanRuntimeMessageLocalizer.LocalizeScanViewStatus(ex.Message));
            RecordScanCompletion(StatusText, InfoBarSeverity.Error);
            OutputSummaryText = "Scan_Runtime_OutputSummaryFailed".GetLocalized();
            ShowTopRiskBanner("Scan_Runtime_TopRiskScanFailedTitle".GetLocalizedOrFallback("Scan failed"), StatusText, InfoBarSeverity.Error);
        }
        finally
        {
            if (!uiToken.IsCancellationRequested)
                IsRunning = false;

            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopScan))]
    private async Task StopScan()
    {
        if (!IsRunning)
            return;

        _scanCts?.Cancel();
        OnPropertyChanged(nameof(HeaderScanStatusText));

        if (TryParseSelectedMotor(out var motorId, out _))
        {
            try
            {
                await _scanSessionCoordinator.UseConnectedSessionAsync(
                    async (session, _) =>
                    {
                        await session.StopMotorAsync(motorId, CancellationToken.None);
                        return true;
                    },
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                MirrorOutput("Scan.StopMotor", $"Stop motor command failed: {ex.Message}");
            }
        }

        var result = await _scanSessionCoordinator.StopAsync(CancellationToken.None);
        StatusText = result.Success ? "Scan_Runtime_StatusStopRequested".GetLocalized() : ScanRuntimeMessageLocalizer.LocalizeScanViewStatus(result.Message);
        if (result.Success)
            ShowTopRiskBanner("Scan_Runtime_TopRiskInterruptedTitle".GetLocalizedOrFallback("Scan interrupted"), "Scan_Runtime_TopRiskInterruptedText".GetLocalizedOrFallback("The workflow was interrupted. Confirm film transport and scanner state before restarting."));
    }

    private void MirrorOutput(string source, string message)
        => _debugOutputMirror.Mirror(source, message);

    [RelayCommand(CanExecute = nameof(CanSaveRgbImage), IncludeCancelCommand = true)]
    private async Task SaveRgbImage(CancellationToken cancellationToken)
    {
        if (_lastResult is null)
        {
            StatusText = "Scan_Runtime_StatusNoRgbResult".GetLocalized();
            return;
        }

        if (!TryBuildColorManagementOptions(out var colorManagement, out var colorError))
        {
            StatusText = colorError;
            SetOutputFeedback(colorError, InfoBarSeverity.Error);
            return;
        }

        try
        {
            var file = await _channelImages.PickRgbImageFileAsync($"scan_rgb_{DateTimeOffset.Now:yyyyMMdd_HHmmss}");
            if (file is null)
            {
                StatusText = "Scan_Runtime_StatusRgbSaveCanceled".GetLocalized();
                SetOutputFeedback(StatusText, InfoBarSeverity.Informational);
                return;
            }

            IsOutputOperationRunning = true;
            StatusText = "Scan_Runtime_StatusRgbSaving".GetLocalized();
            SetOutputFeedback(StatusText, InfoBarSeverity.Informational);
            var buffer = await _channelImages.BuildRgbCompositeBufferAsync(_lastResult, GetOutputChannelAssignment(), colorManagement, SelectedAlignmentMode, _lastResultChannelProfiles, true, cancellationToken);
            await _channelImages.SaveRgbImageAsync(file, buffer, cancellationToken);
            StatusText = "Scan_Runtime_StatusRgbSaved".GetLocalizedFormat(file.Path);
            RecordOutputSuccess(file.Path, true, StatusText);
            ClearTopRiskBanner();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Scan_Runtime_StatusRgbSaveCanceled".GetLocalized();
            SetOutputFeedback(StatusText, InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            StatusText = "Scan_Runtime_StatusRgbSaveFailed".GetLocalizedFormat(ex.Message);
            SetOutputFeedback(StatusText, InfoBarSeverity.Error);
            ShowTopRiskBanner("Scan_Runtime_TopRiskOutputFailedTitle".GetLocalizedOrFallback("Output failed"), StatusText, InfoBarSeverity.Error);
        }
        finally
        {
            IsOutputOperationRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportDngChannels), IncludeCancelCommand = true)]
    private async Task ExportDngChannels(CancellationToken cancellationToken)
    {
        if (_lastResult is null)
        {
            StatusText = "Scan_Runtime_StatusNoDngResult".GetLocalized();
            return;
        }

        try
        {
            ClearTopRiskBanner();
            var folder = await _channelImages.PickDngExportFolderAsync();
            if (folder is null)
            {
                StatusText = "Scan_Runtime_StatusDngExportCanceled".GetLocalized();
                SetOutputFeedback(StatusText, InfoBarSeverity.Informational);
                return;
            }

            IsOutputOperationRunning = true;
            SetOutputFeedback("Scan_Runtime_OutputActionBusy".GetLocalizedOrFallback("Output operation in progress."), InfoBarSeverity.Informational);
            var exportResult = await ExportCapturedDngChannelsAsync(folder, cancellationToken);
            if (exportResult.HasAlignmentWarning)
            {
                var affectedChannels = FormatDngAffectedChannelRoles(exportResult);
                StatusText = "Scan_Runtime_StatusDngExportedWithAlignmentWarning".GetLocalizedFormat(folder.Path, affectedChannels);
                RecordOutputSuccess(folder.Path, false,
                    "Scan_Runtime_StatusDngExported".GetLocalizedFormat(folder.Path));
                ShowTopRiskBanner(
                    "Scan_Runtime_DngAlignmentWarningTitle".GetLocalized(),
                    "Scan_Runtime_DngAlignmentWarningText".GetLocalizedFormat(affectedChannels),
                    InfoBarSeverity.Warning);
            }
            else
            {
                StatusText = "Scan_Runtime_StatusDngExported".GetLocalizedFormat(folder.Path);
                RecordOutputSuccess(folder.Path, false, StatusText);
                ClearTopRiskBanner();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Scan_Runtime_StatusDngExportCanceled".GetLocalized();
            SetOutputFeedback(StatusText, InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            StatusText = "Scan_Runtime_StatusDngExportFailed".GetLocalizedFormat(ex.Message);
            SetOutputFeedback(StatusText, InfoBarSeverity.Error);
            ShowTopRiskBanner("Scan_Runtime_TopRiskOutputFailedTitle".GetLocalizedOrFallback("Output failed"), StatusText, InfoBarSeverity.Error);
        }
        finally
        {
            IsOutputOperationRunning = false;
        }
    }

    private bool CanChangeConfigProfile() => !_isDisposed && _areSessionEventsSubscribed
        && _configurationActivationCts is { IsCancellationRequested: false }
        && IsSetupEditingEnabled && !_isConfigProfileLoading;

    private bool CanUseSavedFilmProfile() => CanChangeConfigProfile() && HasSavedFilmProfile;

    [RelayCommand(CanExecute = nameof(CanUseSavedFilmProfile))]
    private async Task UseSavedFilmProfile()
    {
        if (!CanChangeConfigProfile())
            return;

        var saved = _filmProfileWorkspace.LastSavedDocument;
        if (saved is null)
        {
            StatusText = "Scan_Runtime_SavedFilmProfileMissing".GetLocalized();
            return;
        }

        _isConfigProfileLoading = true;
        var activationEpoch = _configurationActivationEpoch;
        var ct = _configurationActivationCts!.Token;
        RefreshInteractionState();
        try
        {
            await ApplyConfigProfileAsync(saved, activationEpoch, ct);
            StatusText = "Scan_Runtime_StatusSavedFilmProfileLoaded".GetLocalizedFormat(saved.ProfileName);
        }
        catch (OperationCanceledException) when (_isDisposed || ct.IsCancellationRequested || activationEpoch != _configurationActivationEpoch)
        {
        }
        catch (Exception ex)
        {
            if (!_isDisposed && !ct.IsCancellationRequested && activationEpoch == _configurationActivationEpoch)
                ShowConfigProfileLoadFailure(ex.Message);
        }
        finally
        {
            FinishConfigProfileLoad(activationEpoch);
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeConfigProfile))]
    private async Task LoadConfigProfile()
    {
        if (!CanChangeConfigProfile())
            return;

        _isConfigProfileLoading = true;
        var activationEpoch = _configurationActivationEpoch;
        var ct = _configurationActivationCts!.Token;
        RefreshInteractionState();
        try
        {
            await _calibrationProfiles.InitializeAsync(ct);
            EnsureCurrentConfigurationActivation(activationEpoch, ct);

            var imported = await _filmProfileFiles.ImportAsync(ct);
            EnsureCurrentConfigurationActivation(activationEpoch, ct);
            if (imported.WasCanceled)
            {
                StatusText = "Scan_Runtime_StatusLoadConfigProfileCanceled".GetLocalized();
                return;
            }

            if (imported.Profile is null)
            {
                var validationIssue = imported.Validation?.Issues.FirstOrDefault();
                var validationMessage = validationIssue is null
                    ? "Invalid configuration profile."
                    : FilmProfileValidationTextPresenter.GetValidationIssueText(validationIssue);
                StatusText = "Scan_Runtime_StatusLoadConfigProfileFailed".GetLocalizedFormat(validationMessage);
                ShowTopRiskBanner("Scan_Runtime_ConfigLoadFailedTitle".GetLocalized(), StatusText);
                return;
            }

            await ApplyConfigProfileAsync(imported.Profile, activationEpoch, ct);
            LoadedFilmProfileEditRequest = new(imported.Profile, imported.FileName ?? imported.Profile.ProfileName);
        }
        catch (OperationCanceledException) when (_isDisposed || ct.IsCancellationRequested || activationEpoch != _configurationActivationEpoch)
        {
        }
        catch (Exception ex)
        {
            if (!_isDisposed && !ct.IsCancellationRequested && activationEpoch == _configurationActivationEpoch)
                ShowConfigProfileLoadFailure(ex.Message);
        }
        finally
        {
            FinishConfigProfileLoad(activationEpoch);
        }
    }

    private void EnsureCurrentConfigurationActivation(int activationEpoch, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_isDisposed || !_areSessionEventsSubscribed || activationEpoch != _configurationActivationEpoch)
            throw new OperationCanceledException(ct);
    }

    private void FinishConfigProfileLoad(int activationEpoch)
    {
        if (activationEpoch != _configurationActivationEpoch)
            return;

        _isConfigProfileLoading = false;
        RefreshInteractionState();
        RestoreLoadedSettingsCommand.NotifyCanExecuteChanged();
    }

    private async Task ApplyConfigProfileAsync(ScanFilmParameterProfileSet profile, int activationEpoch, CancellationToken ct)
    {
        EnsureCurrentConfigurationActivation(activationEpoch, ct);
        await _deviceSettingsInitializationTask;
        EnsureCurrentConfigurationActivation(activationEpoch, ct);
        await _colorManagementSettings.InitializeAsync();
        EnsureCurrentConfigurationActivation(activationEpoch, ct);
        await _calibrationProfiles.InitializeAsync(ct);
        EnsureCurrentConfigurationActivation(activationEpoch, ct);
        // Storage may already be writing when navigation cancels this operation.
        // It cannot always be rolled back; the activation check prevents stale UI projection.
        await _calibrationProfiles.ReplaceAsync(
            new ScanCalibrationProfileRepositorySnapshot(profile.ChannelProfiles, profile.SelectedCalibrationChannel,
                profile.AcquisitionSettings?.WhiteChannelSettings), ct);
        EnsureCurrentConfigurationActivation(activationEpoch, ct);

        _isConfigProfileLoaded = true;
        _loadedProfileHasColorSettings = profile.ScanRecipeSettings?.ColorManagement is not null;
        SelectedConfigProfileName = profile.ProfileName;
        _selectedConfigAcquisitionSettings = profile.AcquisitionSettings?.Normalize();
        ApplyAcquisitionSettingsToInputs(_selectedConfigAcquisitionSettings ?? (ScanFilmAcquisitionSettings.CreateDefault() with
        {
            ScanMotorId = _deviceSettings.Settings.Normalize().DefaultScanMotorId!.Value
        }));
        ApplyColorManagementSettings(_colorManagementSettings.Settings);
        ApplyScanRecipeSettings(profile.ScanRecipeSettings);
        RefreshLoadedScanRecipeSummary();
        UpdateExecutionConfigSummary();
        UpdateReadinessSummaries();
        ClearStartValidationState();
        ClearTopRiskBanner();

        StatusText = (_selectedConfigAcquisitionSettings is null
                ? "Scan_Runtime_StatusConfigProfileLoadedLegacy"
                : "Scan_Runtime_StatusConfigProfileLoaded")
            .GetLocalizedFormat(profile.ProfileName);
        _loadedConfigProfileDraft = ScanFilmProfileDraft.FromDocument(profile).Draft;
        LoadedFilmProfileEditRequest = new(profile, profile.ProfileName);
        CaptureLoadedSettingsBaseline();
        RefreshSavedFilmProfileAvailability();
    }

    private void ShowConfigProfileLoadFailure(string error)
    {
        StatusText = "Scan_Runtime_StatusLoadConfigProfileFailed".GetLocalizedFormat(error);
        ShowTopRiskBanner("Scan_Runtime_ConfigLoadFailedTitle".GetLocalized(), StatusText);
    }

    private void ApplyProgress(ScanWorkflowProgress progress)
    {
        UpdateScanProgress(progress);
        CurrentPassText = "Scan_Runtime_CurrentPassProgress".GetLocalizedFormat(progress.CurrentPass, progress.TotalPasses, ScanRuntimeMessageLocalizer.LocalizeScanWorkflowStage(progress.Stage));
        var roles = GetEffectiveDeviceChannelRoles();
        var role = progress.ChannelRole ?? (progress.LedChannelIndex < roles.Length ? roles[progress.LedChannelIndex] : null);
        CurrentLedText = string.Equals(role, "White", StringComparison.OrdinalIgnoreCase)
            ? "Scan_Runtime_CurrentWhiteProgress".GetLocalizedFormatOrFallback("White W · {0}",
                GetWhiteSourceDisplayName(progress.WhiteLightSource ?? GetConfiguredWhiteLightSource()))
            : "Scan_Runtime_CurrentLedProgress".GetLocalizedFormat(progress.LedChannelIndex + 1);
        CurrentDirectionText = "Scan_Runtime_CurrentDirectionProgress".GetLocalizedFormat(GetDirectionDisplayName(progress.DirectionPositive ? ForwardDirection : ReverseDirection));
    }

    private void UpdateScanProgress(ScanWorkflowProgress progress)
    {
        if (progress.CurrentPass <= 0 || progress.TotalPasses <= 0)
        {
            ScanProgressValue = 0;
            ScanProgressMaximum = 1;
            IsScanProgressIndeterminate = true;
            return;
        }

        ScanProgressMaximum = progress.TotalPasses;
        var passBase = Math.Clamp(progress.CurrentPass - 1, 0, progress.TotalPasses);
        var progressValue = progress.Stage switch
        {
            "Completed" => progress.CurrentPass,
            "Preparing" => passBase,
            _ => passBase + 0.5,
        };

        ScanProgressValue = Math.Clamp(progressValue, 0, ScanProgressMaximum);
        IsScanProgressIndeterminate = false;
    }
    private void UpdatePassPlan()
    {
        if (_isRestoringLoadedSettings)
            return;
        Pass1DirectionText = GetDirectionForPass(0);
        Pass2DirectionText = GetDirectionForPass(1);
        Pass3DirectionText = GetDirectionForPass(2);
        Pass4DirectionText = GetDirectionForPass(3);

        if (!IsRunning && IsConnected)
            CurrentDirectionText = "Scan_Runtime_CurrentDirectionProgress".GetLocalizedFormat(GetDirectionDisplayName(Pass1DirectionText));
    }

    private string GetDirectionForPass(int passIndex)
    {
        if (!IsAlternateMotorDirectionEnabled)
            return SelectedStartingDirection;

        var startPositive = string.Equals(SelectedStartingDirection, ForwardDirection, StringComparison.OrdinalIgnoreCase);
        return (passIndex % 2) == 0
            ? (startPositive ? ForwardDirection : ReverseDirection)
            : (startPositive ? ReverseDirection : ForwardDirection);
    }

    private void UpdatePreviewState()
    {
        if (_isRestoringLoadedSettings)
            return;
        if (!IsPreviewEnabled)
        {
            PreviewImage = null;
            PreviewDescriptionText = "Scan_Runtime_PreviewDisabledDescription".GetLocalized();
            PreviewPlaceholderText = "Scan_Runtime_PreviewDisabledPlaceholder".GetLocalized();
            return;
        }

        if (_lastResult is null)
        {
            PreviewImage = null;
            PreviewDescriptionText = "Scan_Runtime_PreviewModeDescription".GetLocalizedFormat(GetPreviewModeDisplayName(SelectedPreviewMode));
            PreviewPlaceholderText = BuildPreviewPlaceholderText();
            return;
        }

        if (string.Equals(SelectedPreviewMode, ScanPreviewModeOption.RgbCompositeKey, StringComparison.Ordinal))
        {
            if (!TryBuildColorManagementOptions(out var colorManagement, out var colorError))
            {
                PreviewImage = null;
                PreviewDescriptionText = colorError;
                PreviewPlaceholderText = colorError;
                return;
            }

            if (_channelImages.TryBuildRgbComposite(_lastResult, GetOutputChannelAssignment(), colorManagement, SelectedAlignmentMode, PreviewImage, out var frame, out var error, _lastResultChannelProfiles, true) && frame is not null)
            {
                PreviewImage = frame.Bitmap;
                PreviewDescriptionText = "Scan_Runtime_PreviewCompositeDescription".GetLocalizedFormat(GetPreviewModeDisplayName(SelectedPreviewMode), GetAlignmentModeDisplayName(SelectedAlignmentMode), IsColorManagementEnabled ? "Scan_Runtime_PreviewCompositeModeSpectral".GetLocalized() : "Scan_Runtime_PreviewCompositeModeGamma".GetLocalized());
                PreviewPlaceholderText = string.Empty;
            }
            else
            {
                PreviewImage = null;
                PreviewDescriptionText = error;
                PreviewPlaceholderText = error;
            }

            return;
        }

        if (!TryParsePreviewChannelIndex(SelectedPreviewMode, out var channelIndex) || _lastResult.Passes.Count <= channelIndex)
        {
            PreviewImage = null;
            PreviewDescriptionText = "Scan_Runtime_PreviewRawUnavailable".GetLocalized();
            PreviewPlaceholderText = PreviewDescriptionText;
            return;
        }

        var assignment = GetOutputChannelAssignment();
        if (_channelImages.TryBuildRawPreview(_lastResult, assignment, SelectedAlignmentMode, channelIndex, PreviewImage, out var bitmap, out var rawError))
        {
            PreviewImage = bitmap;
            PreviewDescriptionText = "Scan_Runtime_PreviewRawDescription".GetLocalizedFormat(GetPreviewModeDisplayName(SelectedPreviewMode), GetAlignmentModeDisplayName(SelectedAlignmentMode), _lastResult.Passes[channelIndex].PassIndex);
            PreviewPlaceholderText = string.Empty;
        }
        else
        {
            PreviewImage = null;
            PreviewDescriptionText = rawError;
            PreviewPlaceholderText = rawError;
        }
    }

    private void UpdateChannelMappingSummary()
    {
        var roles = GetEffectiveDeviceChannelRoles();
        UpdatePreviewModeOptions();
        ChannelMappingSummaryText = "Scan_Runtime_ChannelMappingSummary".GetLocalizedFormat(GetChannelRoleDisplayName(roles[0]), GetChannelRoleDisplayName(roles[1]), GetChannelRoleDisplayName(roles[2]), GetChannelRoleDisplayName(roles[3]));
        if (roles.Any(role => string.Equals(role, "White", StringComparison.OrdinalIgnoreCase)))
            ChannelMappingSummaryText += " · " + BuildWhiteSourceSummary(GetConfiguredWhiteLightSource());
        RefreshLoadedScanRecipeSummary();
    }

    private string BuildPreviewPlaceholderText()
    {
        if (IsRunning)
            return "Scan_Runtime_PreviewPlaceholderScanning".GetLocalizedOrFallback("Scanning. The preview will appear when the scan finishes.");

        if (!IsConnected && !IsConnecting)
            return OfflineGuidanceText;

        if (!IsConnected || !_isConfigProfileLoaded || !HasLoadedScannerParameters())
            return "Scan_Runtime_PreviewPlaceholderPrepareFirst".GetLocalizedOrFallback("No preview yet. Connect the scanner, load a configuration, then start a scan.");

        return "Scan_Runtime_PreviewPlaceholderNextScan".GetLocalizedFormat(GetPreviewModeDisplayName(SelectedPreviewMode));
    }

    private void UpdatePreviewModeOptions()
    {
        var selectedMode = SelectedPreviewMode;
        var slots = GetPreviewChannelSlots();
        var assignment = GetOutputChannelAssignment();
        var modes = ScanPreviewModeOption.CreateKeys(slots, HasRgbChannels(slots.Select(slot => assignment.Roles[slot])));
        if (modes.Count == 0)
            modes = new[] { ScanPreviewModeOption.RgbCompositeKey };
        for (var index = 0; index < modes.Count; index++)
        {
            if (index >= PreviewModes.Count)
                PreviewModes.Add(modes[index]);
            else if (!string.Equals(PreviewModes[index], modes[index], StringComparison.Ordinal))
                PreviewModes[index] = modes[index];
        }
        while (PreviewModes.Count > modes.Count)
            PreviewModes.RemoveAt(PreviewModes.Count - 1);
        ScanPreviewModeOption.Synchronize(PreviewModeOptions, PreviewModes, GetPreviewModeDisplayName);
        SelectedPreviewMode = PreviewModes.Contains(selectedMode) ? selectedMode : PreviewModes[0];
    }

    private void UpdateComputedMotorSummary()
    {
        if (_isRestoringLoadedSettings)
            return;
        if (!int.TryParse(SelectedRows, out var rows) || rows <= 0)
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorUnavailableUntilRowsValid".GetLocalized();
            return;
        }

        if (_loadedSysClockKhz < ScanDebugConstants.MinSysClockKhz)
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorUnavailableUntilParametersLoaded".GetLocalized();
            return;
        }

        if (!TryParseSelectedMotor(out var motorId, out _))
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorUnavailableUntilParametersLoaded".GetLocalized();
            return;
        }

        var motorSettings = _deviceSettings.Settings.GetMotorSettings(motorId);
        if (!TryGetEffectiveMotorIntervalNs(motorSettings, out var intervalNs))
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
            return;
        }

        var computedSteps = ScanTimingMath.ComputeMotorStepsPerPass(rows, _loadedExposureTicks, _loadedSysClockKhz, intervalNs);
        var distanceMm = ScanTimingMath.ConvertMotorStepsToMillimeters(computedSteps, motorSettings);
        var speedMmPerSecond = ScanTimingMath.ConvertMotorIntervalToMillimetersPerSecond(intervalNs, motorSettings);
        ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorSummary".GetLocalizedFormat(GetMotorDisplayIndex(), computedSteps, FormatMotorIntervalInput(intervalNs), rows, distanceMm.ToString("0.###", CultureInfo.InvariantCulture), speedMmPerSecond.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private bool TryBuildWorkflowRequest(out ScanWorkflowRequest request, out string error)
    {
        request = new ScanWorkflowRequest(0, false, Array.Empty<ushort>(), Array.Empty<string>(), Array.Empty<ScanParameterSnapshot>(), 0, 0, false, false, 0, 0, null);
        if (!_isConfigProfileLoaded)
        {
            error = "Scan_Runtime_ErrorConfigProfileRequired".GetLocalizedOrFallback("Load a scan configuration before starting.");
            return false;
        }

        if (!int.TryParse(SelectedRows, out var rows) || rows <= 0)
        {
            error = "Scan_Runtime_ErrorRowsPositiveInteger".GetLocalized();
            return false;
        }

        _deviceSettingsInitializationTask.GetAwaiter().GetResult();

        if (!TryParseSelectedMotor(out var motorId, out error))
            return false;

        var motorSettings = _deviceSettings.Settings.GetMotorSettings(motorId);
        if (!TryGetEffectiveMotorIntervalNs(motorSettings, out var intervalNs))
        {
            error = "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
            return false;
        }

        if (!HasLoadedScannerParameters() || _loadedSnapshot is null)
        {
            error = "Scan_Runtime_ErrorParametersNotLoaded".GetLocalized();
            return false;
        }

        var passRoles = GetEffectiveDeviceChannelRoles();
        if (!TryValidateChannelMapping(out error))
            return false;
        var passProfiles = new ScanParameterSnapshot[passRoles.Length];
        for (var passIndex = 0; passIndex < passRoles.Length; passIndex++)
        {
            if (!ScanChannelRoleHelper.IsActiveRole(passRoles[passIndex]))
            {
                passProfiles[passIndex] = _loadedSnapshot;
                continue;
            }

            if (!TryGetConfiguredProfile(passRoles[passIndex], out var profile))
            {
                error = "Scan_Runtime_ErrorConfigProfileRequired".GetLocalizedOrFallback("Load a scan configuration with profiles for every configured scanner channel before starting.");
                return false;
            }

            passProfiles[passIndex] = profile.Parameters;
        }

        double targetLinePitchMillimeters;
        if (_isMotorDistanceDerivedFromInterval)
        {
            targetLinePitchMillimeters = ScanTimingMath.ConvertMotorIntervalToLineDistanceMillimeters(
                intervalNs,
                _loadedExposureTicks,
                _loadedSysClockKhz,
                motorSettings);
        }
        else if (!ScanMotorDistanceText.TryParseMillimeters(
            MotorDistancePerLineValue,
            MotorDistancePerLineUnit,
            motorSettings,
            out targetLinePitchMillimeters))
        {
            error = "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
            return false;
        }

        var linePitchInput = new ScanWorkflowLinePitchInput(
            targetLinePitchMillimeters * 1_000.0,
            motorSettings,
            ScanDebugConstants.MotionMinIntervalNs,
            new uint?[passRoles.Length]);
        var linePitchPlan = ScanTimingMath.BuildLinePitchPlan(new ScanLinePitchPlanRequest(
            rows,
            linePitchInput.TargetLinePitchMicrometers,
            linePitchInput.MotorMechanics,
            linePitchInput.MinimumMotorIntervalNanoseconds,
            passRoles.Select((channelRole, passIndex) => new ScanLinePitchPassInput(
                passIndex,
                channelRole,
                ScanChannelRoleHelper.IsActiveRole(channelRole),
                passProfiles[passIndex].ExposureTicks,
                _loadedSysClockKhz,
                ParameterProfile: passProfiles[passIndex])).ToArray()));
        if (!linePitchPlan.CanScan
            || (IsAlternateMotorDirectionEnabled && !linePitchPlan.CanUseAlternateDirection))
        {
            error = linePitchPlan.Issues.FirstOrDefault(issue => issue.Severity != ScanLinePitchIssueSeverity.Warning
                && (issue.Scope == ScanLinePitchIssueScope.Plan
                    || (IsAlternateMotorDirectionEnabled && issue.Scope == ScanLinePitchIssueScope.AlternateDirection)))?.Message
                ?? "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
            return false;
        }

        var acquisitionSettings = BuildWorkflowAcquisitionSettings(intervalNs, motorId);

        request = new ScanWorkflowRequest(
            rows,
            IsWarmUpEnabled,
            new[] { acquisitionSettings.Led1Level, acquisitionSettings.Led2Level, acquisitionSettings.Led3Level, acquisitionSettings.Led4Level },
            passRoles,
            passProfiles,
            motorId,
            intervalNs,
            string.Equals(SelectedStartingDirection, ForwardDirection, StringComparison.OrdinalIgnoreCase),
            IsAlternateMotorDirectionEnabled,
            _loadedExposureTicks,
            _loadedSysClockKhz,
            acquisitionSettings,
            LinePitchInput: linePitchInput);

        error = string.Empty;
        return true;
    }

    private bool TryParseSelectedMotor(out byte motorId, out string error)
    {
        // Transport wiring belongs to the device, including when loading a legacy film file.
        // The returned value is captured in a new request; existing requests stay unchanged.
        motorId = _deviceSettings.Settings.Normalize().DefaultScanMotorId!.Value;
        if (motorId >= ScanDebugConstants.MotionMotorCount)
        {
            error = "Scan_Runtime_ErrorScanMotorRange".GetLocalizedFormat(ScanDebugConstants.MotionMotorCount);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private int GetMotorDisplayIndex()
        => TryParseSelectedMotor(out var motorId, out _) ? motorId + 1 : 1;

    private void ApplyAcquisitionSettingsToInputs(ScanFilmAcquisitionSettings settings)
    {
        var normalized = settings.Normalize() with
        {
            ScanMotorId = _deviceSettings.Settings.Normalize().DefaultScanMotorId!.Value
        };
        SelectedRows = normalized.Rows.ToString(CultureInfo.InvariantCulture);
        SelectedScanMotor = MotorOptions[normalized.ScanMotorId];
        SelectedStartingDirection = normalized.StartingDirectionPositive ? ForwardDirection : ReverseDirection;
        IsWarmUpEnabled = normalized.WarmUpEnabled;
        IsAlternateMotorDirectionEnabled = normalized.TransportStrategy == ScanFilmTransportStrategy.AlternateDirection;
        _isMotorDistanceDerivedFromInterval = true;
        MotorIntervalUs = FormatMotorIntervalInput(normalized.MotorIntervalNs);
        if (normalized.TargetLinePitchMicrometers is double targetLinePitch
            && ScanMotorDistanceText.TryFormatDisplayValue(targetLinePitch / 1_000.0,
                MotorDistancePerLineUnit, GetCurrentMotorSettings(), out var distance))
        {
            _isMotorDistanceDerivedFromInterval = false;
            ApplyDerivedMotorDistance(distance);
        }
        else
        {
            RefreshDerivedMotorDistanceFromCurrentInterval();
        }
    }

    private void ApplyScanRecipeSettings(ScanFilmScanRecipeSettings? settings)
    {
        // Retain the selected roles and their orientation, then project them onto
        // the device's current wiring when building a new acquisition request.
        var loadedAssignment = settings?.ChannelAssignment ?? _selectedConfigAcquisitionSettings?.AcquisitionChannelAssignment;
        _loadedConfigChannelRoles = loadedAssignment?.Roles.ToArray()
            ?? (_selectedConfigAcquisitionSettings is { } acquisition
                ? new[] { acquisition.Led1ChannelColor, acquisition.Led2ChannelColor, acquisition.Led3ChannelColor, acquisition.Led4ChannelColor }
                : _deviceSettings.Settings.Normalize().ChannelRoles.ToArray());
        IsChannel1Reversed = false;
        IsChannel2Reversed = false;
        IsChannel3Reversed = false;
        IsChannel4Reversed = false;
        _loadedChannel5Reversed = false;
        SelectedAlignmentMode = AlignmentModeOptions[0];
        SelectedDngExportMode = DngExportModeOptions[0];

        if (loadedAssignment is { } assignment)
        {
            IsChannel1Reversed = assignment.Channel1Reversed;
            IsChannel2Reversed = assignment.Channel2Reversed;
            IsChannel3Reversed = assignment.Channel3Reversed;
            IsChannel4Reversed = assignment.Channel4Reversed;
            _loadedChannel5Reversed = assignment.Channel5Reversed;
        }

        if (settings?.ColorManagement is { } colorManagement)
        {
            IsColorManagementEnabled = colorManagement.IsEnabled;
            RedWavelengthNm = FormatColorDouble(colorManagement.RedWavelengthNm);
            GreenWavelengthNm = FormatColorDouble(colorManagement.GreenWavelengthNm);
            BlueWavelengthNm = FormatColorDouble(colorManagement.BlueWavelengthNm);
            OutputGamma = FormatColorDouble(colorManagement.OutputGamma);
            SelectedTargetWhitePointMode = colorManagement.TargetWhitePointMode.ToString();
            ManualWhitePointColorTemperatureK = FormatColorDouble(colorManagement.ManualWhitePointColorTemperatureK);
        }

        if (settings?.AlignmentMode is { } alignmentMode && AlignmentModeOptions.Contains(alignmentMode))
            SelectedAlignmentMode = alignmentMode;

        if (settings?.DngExportMode is { } dngExportMode && DngExportModeOptions.Contains(dngExportMode))
            SelectedDngExportMode = dngExportMode;

        OnChannelAssignmentChanged();
    }

    private void RefreshLoadedScanRecipeSummary()
    {
        if (_isRestoringLoadedSettings)
            return;
        if (!_isConfigProfileLoaded)
        {
            LoadedScanRecipeSummaryText = string.Empty;
            return;
        }

        var assignment = BuildChannelAssignment();
        var channelSummary = string.Join(" / ", assignment.Roles.Select(GetChannelRoleDisplayName));
        var reversedCount = assignment.ReversedFlags.Count(reversed => reversed);
        var colorMode = IsColorManagementEnabled
            ? "Scan_Runtime_RecipeSummaryColorManaged".GetLocalizedFormat(RedWavelengthNm, GreenWavelengthNm, BlueWavelengthNm, OutputGamma)
            : "Scan_Runtime_RecipeSummaryGammaOnly".GetLocalizedFormat(OutputGamma);

        LoadedScanRecipeSummaryText = "Scan_Runtime_RecipeSummary".GetLocalizedFormat(
            channelSummary,
            reversedCount,
            GetAlignmentModeDisplayName(SelectedAlignmentMode),
            GetDngExportModeDisplayName(SelectedDngExportMode),
            colorMode);
        if (assignment.Roles.Contains("White"))
            LoadedScanRecipeSummaryText += " · " + BuildWhiteSourceSummary(GetConfiguredWhiteLightSource());
        UpdateReadinessSummaries();
    }

    private bool TryGetEffectiveMotorIntervalNs(ScanMotorMechanicalSettings motorSettings, out uint intervalNs, bool updateInput = true)
    {
        intervalNs = 0;

        if (_isMotorDistanceDerivedFromInterval)
            return ScanMotorIntervalText.TryParseMicroseconds(MotorIntervalUs, out intervalNs)
                && intervalNs >= ScanDebugConstants.MotionMinIntervalNs;

        if (_loadedSysClockKhz < ScanDebugConstants.MinSysClockKhz)
            return false;

        if (!ScanMotorDistanceText.TryParseMillimeters(MotorDistancePerLineValue, MotorDistancePerLineUnit, motorSettings, out var lineDistanceMm))
            return false;

        if (!ScanTimingMath.TryConvertLineDistanceMillimetersToMotorIntervalNs(lineDistanceMm, _loadedExposureTicks, _loadedSysClockKhz, motorSettings, ScanDebugConstants.MotionMinIntervalNs, out intervalNs))
            return false;

        var intervalInput = FormatMotorIntervalInput(intervalNs);
        if (string.IsNullOrEmpty(intervalInput))
            return false;

        if (updateInput)
            MotorIntervalUs = intervalInput;

        return true;
    }

    private void RefreshDerivedMotorDistanceFromCurrentInterval()
    {
        if (!_isMotorDistanceDerivedFromInterval)
            return;

        if (!ScanMotorIntervalText.TryParseMicroseconds(MotorIntervalUs, out var intervalNs)
            || intervalNs < ScanDebugConstants.MotionMinIntervalNs
            || _loadedSysClockKhz < ScanDebugConstants.MinSysClockKhz
            || !TryParseSelectedMotor(out var motorId, out _))
        {
            ApplyDerivedMotorDistance(string.Empty);
            return;
        }

        var lineDistanceMm = ScanTimingMath.ConvertMotorIntervalToLineDistanceMillimeters(intervalNs, _loadedExposureTicks, _loadedSysClockKhz, _deviceSettings.Settings.GetMotorSettings(motorId));
        if (!ScanMotorDistanceText.TryFormatDisplayValue(lineDistanceMm, MotorDistancePerLineUnit, _deviceSettings.Settings.GetMotorSettings(motorId), out var displayValue))
        {
            ApplyDerivedMotorDistance(string.Empty);
            return;
        }

        ApplyDerivedMotorDistance(displayValue);
    }

    private void ApplyDerivedMotorDistance(string value)
    {
        _isApplyingDerivedMotorDistance = true;
        try
        {
            MotorDistancePerLineValue = value;
        }
        finally
        {
            _isApplyingDerivedMotorDistance = false;
        }
    }

    private ScanMotorMechanicalSettings GetCurrentMotorSettings()
        => TryParseSelectedMotor(out var motorId, out _) ? _deviceSettings.Settings.GetMotorSettings(motorId) : ScanMotorMechanicalSettings.CreateDefault();

    private ScanFilmAcquisitionSettings BuildWorkflowAcquisitionSettings(uint motorIntervalNs, byte motorId)
    {
        var source = (_selectedConfigAcquisitionSettings ?? ScanFilmAcquisitionSettings.CreateDefault()).Normalize();
        var physicalRoles = _deviceSettings.Settings.Normalize().ChannelRoles;
        var lighting = physicalRoles.Select(role => WhiteChannelSettings.FindComponent(source, role) ?? new WhiteLightComponentSettings()).ToArray();
        var steadyMask = (byte)lighting.Select((component, index) => component.Mode == WhiteLightDriveMode.Steady ? 1 << index : 0).Sum();
        var syncMask = (byte)lighting.Select((component, index) => component.Mode == WhiteLightDriveMode.Synchronized ? 1 << index : 0).Sum();
        var white = source.WhiteChannelSettings;
        if (white is null && GetEffectiveDeviceChannelRoles().Contains("White"))
        {
            _calibrationProfiles.TryGetProfile("White", out var whiteProfile);
            white = WhiteChannelSettings.FromLegacy(whiteProfile, source);
        }
        return (source with
        {
            MotorIntervalNs = motorIntervalNs,
            ScanMotorId = motorId,
            Led1ChannelColor = physicalRoles[0],
            Led2ChannelColor = physicalRoles[1],
            Led3ChannelColor = physicalRoles[2],
            Led4ChannelColor = physicalRoles[3],
            Led1Level = lighting[0].Level,
            Led2Level = lighting[1].Level,
            Led3Level = lighting[2].Level,
            Led4Level = lighting[3].Level,
            Led1PulseClock = lighting[0].PulseClock,
            Led2PulseClock = lighting[1].PulseClock,
            Led3PulseClock = lighting[2].PulseClock,
            Led4PulseClock = lighting[3].PulseClock,
            SteadyMask = steadyMask,
            SyncMask = syncMask,
            AcquisitionChannelAssignment = BuildChannelAssignment(),
            WhiteChannelSettings = white
        }).Normalize();
    }

    private static string FormatMotorIntervalInput(uint intervalNs)
        => ScanMotorIntervalText.TryFormatMicroseconds(intervalNs, out var intervalUs) ? intervalUs : string.Empty;

    private bool CanRunExtendedScan()
        => _transferSettings.Settings.ReadMode == ScanBulkInReadMode.MultiBuffered && _transferSettings.Settings.RawIoEnabled;

    private static Brush GetThemeBrush(string resourceKey, Color fallbackColor)
    {
        try
        {
            return Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true && resource is Brush brush
                ? brush
                : new SolidColorBrush(fallbackColor);
        }
        catch (Exception exception) when (IsXamlRuntimeUnavailable(exception))
        {
            // A headless view-model host has no registered XAML runtime or visual consumers.
            return null!;
        }
    }

    private static bool IsXamlRuntimeUnavailable(Exception exception)
        => exception is COMException { HResult: unchecked((int)0x80040154) }
            || (exception is TypeInitializationException { InnerException: { } inner } && IsXamlRuntimeUnavailable(inner));

    private ScanChannelAssignment BuildChannelAssignment()
    {
        var roles = GetEffectiveDeviceChannelRoles();
        var originalRoles = GetConfiguredChannelRoles();
        var originalReversed = new[] { IsChannel1Reversed, IsChannel2Reversed, IsChannel3Reversed, IsChannel4Reversed, _loadedChannel5Reversed };
        bool IsReversed(string role)
        {
            var index = Array.FindIndex(originalRoles, candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));
            return ScanChannelRoleHelper.IsActiveRole(role) && index >= 0 && originalReversed[index];
        }
        return new(roles[0], roles[1], roles[2], roles[3],
            IsReversed(roles[0]), IsReversed(roles[1]), IsReversed(roles[2]), IsReversed(roles[3]),
            roles.Length > 4 ? roles[4] : "Unused", roles.Length > 4 && IsReversed(roles[4]));
    }

    private string[] GetConfiguredChannelRoles()
        => _isConfigProfileLoaded && _loadedConfigChannelRoles is { } roles
            ? roles.Select(ScanDeviceSettings.NormalizeProjectedChannelRole).ToArray()
            : _deviceSettings.Settings.Normalize().ChannelRoles.ToArray();

    private string[] GetEffectiveDeviceChannelRoles()
    {
        var selectedRoles = GetConfiguredChannelRoles();
        var physicalRoles = _deviceSettings.Settings.Normalize().ChannelRoles;
        var projected = physicalRoles.Select(role => selectedRoles.Contains(role, StringComparer.OrdinalIgnoreCase) ? role : "Unused").ToList();
        if (selectedRoles.Contains("White", StringComparer.OrdinalIgnoreCase) && !projected.Contains("White"))
            projected.Add("White");
        return projected.ToArray();
    }

    private bool TryValidateChannelMapping(out string error)
    {
        var selectedRoles = GetConfiguredChannelRoles();
        var physicalRoles = _deviceSettings.Settings.Normalize().ChannelRoles;
        foreach (var role in selectedRoles.Where(role => ScanChannelRoleHelper.IsActiveRole(role) && role != "White").Distinct())
        {
            if (physicalRoles.Count(candidate => candidate == role) == 1)
                continue;
            error = "Scan_Runtime_ChannelMappingRequired".GetLocalizedFormatOrFallback("Configure exactly one {0} LED mapping for the selected capture channel.", GetChannelRoleDisplayName(role));
            return false;
        }

        if (selectedRoles.Contains("White"))
        {
            var required = GetConfiguredWhiteLightSource() == WhiteLightSource.RgbComposite
                ? new[] { "Red", "Green", "Blue" }
                : new[] { "White" };
            foreach (var role in required)
            {
                if (physicalRoles.Count(candidate => candidate == role) == 1)
                    continue;
                error = "Scan_Runtime_WhiteSourceMappingRequired".GetLocalizedFormatOrFallback("The selected white light source requires exactly one {0} LED mapping.", GetChannelRoleDisplayName(role));
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    public async Task RefreshDeviceSettingsBindingsAsync()
    {
        var activationEpoch = _configurationActivationEpoch;
        try
        {
            await _deviceSettingsInitializationTask;
            _dispatcher.TryEnqueue(() =>
            {
                if (_isDisposed || !_areSessionEventsSubscribed || activationEpoch != _configurationActivationEpoch)
                    return;

                var motorId = _deviceSettings.Settings.Normalize().DefaultScanMotorId!.Value;
                if (motorId < MotorOptions.Count)
                    SelectedScanMotor = MotorOptions[motorId];

                OnChannelAssignmentChanged();
                RefreshLoadedScanRecipeSummary();
                UpdateReadinessSummaries();
            });
        }
        catch (Exception ex)
        {
            _debugOutputMirror.Mirror("Scan.DeviceSettings", $"Refresh failed: {ex.Message}");
        }
    }

    private async Task LoadColorManagementSettingsAsync()
    {
        await _colorManagementSettings.InitializeAsync();
        if (!_isConfigProfileLoaded)
            ApplyColorManagementSettings(_colorManagementSettings.Settings);

        UpdatePreviewState();
    }

    private void ApplyColorManagementSettings(ScanColorManagementOptions settings)
    {
        IsColorManagementEnabled = settings.IsEnabled;
        RedWavelengthNm = FormatColorDouble(settings.RedWavelengthNm);
        GreenWavelengthNm = FormatColorDouble(settings.GreenWavelengthNm);
        BlueWavelengthNm = FormatColorDouble(settings.BlueWavelengthNm);
        OutputGamma = FormatColorDouble(settings.OutputGamma);
        SelectedTargetWhitePointMode = settings.TargetWhitePointMode.ToString();
        ManualWhitePointColorTemperatureK = FormatColorDouble(settings.ManualWhitePointColorTemperatureK);
    }

    private bool TryBuildColorManagementOptions(out ScanColorManagementOptions options, out string error)
    {
        options = ScanColorManagementOptions.CreateDefault();

        if (!TryParseColorDouble(RedWavelengthNm, "Scan_Runtime_FieldRedWavelengthNm".GetLocalized(), out var redWavelength, out error)
            || !TryParseColorDouble(GreenWavelengthNm, "Scan_Runtime_FieldGreenWavelengthNm".GetLocalized(), out var greenWavelength, out error)
            || !TryParseColorDouble(BlueWavelengthNm, "Scan_Runtime_FieldBlueWavelengthNm".GetLocalized(), out var blueWavelength, out error)
            || !TryParseColorDouble(OutputGamma, "Scan_Runtime_FieldOutputGamma".GetLocalized(), out var outputGamma, out error)
            || !TryParseColorDouble(ManualWhitePointColorTemperatureK, "Scan_Runtime_FieldManualWhitePointColorTemperatureK".GetLocalized(), out var manualWhitePointColorTemperature, out error))
        {
            return false;
        }

        if (!TryParseTargetWhitePointMode(SelectedTargetWhitePointMode, out var targetWhitePointMode))
        {
            error = "Scan_Runtime_FieldTargetWhitePoint".GetLocalizedOrFallback("Target white point");
            return false;
        }

        options = new ScanColorManagementOptions(IsColorManagementEnabled, redWavelength, greenWavelength, blueWavelength, outputGamma, targetWhitePointMode, manualWhitePointColorTemperature);
        error = string.Empty;
        return true;
    }

    private static bool TryParseColorDouble(string text, string fieldName, out double value, out string error)
    {
        if (!InvariantNumericText.TryParseDouble(text, out value))
        {
            error = "Shared_Runtime_ErrorNumber".GetLocalizedFormat(fieldName);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string FormatColorDouble(double value)
        => InvariantNumericText.FormatCompactDouble(value);

    private static bool TryParseTargetWhitePointMode(string value, out ScanTargetWhitePointMode mode)
        => Enum.TryParse(value, out mode)
            && mode is ScanTargetWhitePointMode.D65 or ScanTargetWhitePointMode.D50 or ScanTargetWhitePointMode.ManualColorTemperature;

    private static string GetDirectionDisplayName(string direction)
        => string.Equals(direction, ForwardDirection, StringComparison.OrdinalIgnoreCase)
            ? "Scan_Runtime_DirectionForward".GetLocalized()
            : "Scan_Runtime_DirectionReverse".GetLocalized();

    private string GetPreviewModeDisplayName(string? mode)
    {
        mode = NormalizePreviewMode(mode);

        if (string.Equals(mode, ScanPreviewModeOption.RgbCompositeKey, StringComparison.Ordinal))
            return "Scan_Runtime_PreviewModeRgbComposite".GetLocalized();

        var assignment = GetOutputChannelAssignment();
        if (ScanPreviewModeOption.TryGetRawChannelIndex(mode, assignment.Roles.Count, out var slot))
        {
            var role = assignment.Roles[slot];
            if (role == "White")
                return "Scan_Runtime_PreviewModeRawWhite".GetLocalizedFormatOrFallback("Raw White W ({0})", GetWhiteSourceDisplayName(GetOutputWhiteLightSource(slot)));
            return "Scan_Runtime_PreviewModeRawRole".GetLocalizedFormatOrFallback("Raw ({0})", GetChannelRoleDisplayName(role));
        }

        return mode;
    }

    private static string GetDngExportModeDisplayName(ScanDngExportMode mode)
        => ScanSelectorDisplayNameConverter.GetDngExportModeDisplayName(mode);

    private ScanChannelAssignment GetOutputChannelAssignment()
        => _lastResultChannelAssignment ?? BuildChannelAssignment();

    private Task<ScanDngExportResult> ExportCapturedDngChannelsAsync(Windows.Storage.StorageFolder folder, CancellationToken cancellationToken)
        => _channelImages.ExportDngChannelsAsync(folder, _lastResult!, GetOutputChannelAssignment(), SelectedAlignmentMode,
            SelectedDngExportMode, _lastResultChannelProfiles, cancellationToken);

    private bool TryGetConfiguredProfile(string role, out ScanChannelCalibrationProfile profile)
    {
        if (string.Equals(role, "White", StringComparison.OrdinalIgnoreCase)
            && _selectedConfigAcquisitionSettings?.WhiteChannelSettings is { } white)
        {
            profile = white.ActiveSettings?.Profile!;
            return profile is not null;
        }

        return _calibrationProfiles.TryGetProfile(role, out profile!);
    }

    private IReadOnlyDictionary<string, ScanChannelCalibrationProfile> CaptureChannelProfiles(ScanWorkflowRequest request)
    {
        var profiles = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
        var activeProfiles = _calibrationProfiles.Snapshot.Profiles;
        for (var index = 0; index < request.PassChannelRoles.Length; index++)
        {
            var role = request.PassChannelRoles[index];
            if (!ScanChannelRoleHelper.IsActiveRole(role))
                continue;

            var profile = string.Equals(role, "White", StringComparison.OrdinalIgnoreCase)
                ? request.AcquisitionSettings?.WhiteChannelSettings?.ActiveSettings?.Profile
                : null;
            if (profile is null && !activeProfiles.TryGetValue(role, out profile))
                continue;

            profiles[role] = profile with { Parameters = request.PassParameterProfiles[index] };
        }

        return new ReadOnlyDictionary<string, ScanChannelCalibrationProfile>(profiles);
    }

    private IReadOnlyList<int> GetPreviewChannelSlots()
        => _lastResult is { } result
            ? result.Passes.Select(capture => (int)capture.LedChannelIndex).ToArray()
            : BuildChannelAssignment().Roles.Select((role, slot) => (role, slot))
                .Where(item => ScanChannelRoleHelper.IsActiveRole(item.role)).Select(item => item.slot).ToArray();

    private static bool HasRgbChannels(IEnumerable<string> roles)
    {
        var capturedRoles = roles.ToArray();
        return new[] { "Red", "Green", "Blue" }.All(role => capturedRoles.Count(candidate => candidate == role) == 1);
    }

    private WhiteLightSource GetConfiguredWhiteLightSource()
        => _selectedConfigAcquisitionSettings?.WhiteChannelSettings?.Source ?? WhiteLightSource.DedicatedLed;

    private WhiteLightSource GetOutputWhiteLightSource(int slot)
        => _lastResult?.Passes.FirstOrDefault(capture => capture.LedChannelIndex == slot)?.Provenance?.WhiteLightSource
            ?? _lastResultWhiteLightSource ?? GetConfiguredWhiteLightSource();

    private static string GetWhiteSourceDisplayName(WhiteLightSource source)
        => source == WhiteLightSource.RgbComposite
            ? "ScanDebug_WhiteSourceRgb".GetLocalizedOrFallback("RGB combined light")
            : "ScanDebug_WhiteSourceDedicated".GetLocalizedOrFallback("Dedicated white LED");

    private static string BuildWhiteSourceSummary(WhiteLightSource source)
        => "Scan_Runtime_WhiteSourceSummary".GetLocalizedFormatOrFallback("White W: {0}", GetWhiteSourceDisplayName(source));

    private string BuildOutputFormatDescription()
    {
        var assignment = GetOutputChannelAssignment();
        var slots = GetPreviewChannelSlots();
        if (slots.Count == 0)
            return GetDngExportModeDisplayName(SelectedDngExportMode);
        try
        {
            var plan = ScanDngOutputPlan.Build(assignment, slots, SelectedDngExportMode);
            var descriptions = plan.Select(part => part.PixelLayout switch
            {
                DngPixelLayout.LinearRawMultiChannel => "Scan_Runtime_DngOutputRaw4".GetLocalizedOrFallback("4-channel raw data"),
                DngPixelLayout.LinearRgb => "Scan_Runtime_DngOutputRgb".GetLocalizedOrFallback("RGB color composite"),
                _ => "Scan_Runtime_DngOutputMono".GetLocalizedFormatOrFallback("{0} raw grayscale", GetChannelRoleDisplayName(assignment.Roles[slots[part.PassIndexes[0]]]))
            });
            return "Scan_Runtime_DngOutputPlan".GetLocalizedFormatOrFallback("Exports {0} DNG file(s): {1}", plan.Count, string.Join(" + ", descriptions));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return GetDngExportModeDisplayName(SelectedDngExportMode);
        }
    }

    private static string GetAlignmentModeDisplayName(ScanChannelAlignmentMode mode)
        => mode switch
        {
            ScanChannelAlignmentMode.Ecc => "Scan_Runtime_AlignmentModeEcc".GetLocalized(),
            ScanChannelAlignmentMode.MutualInformation => "Scan_Runtime_AlignmentModeMutualInformation".GetLocalized(),
            ScanChannelAlignmentMode.EccThenMutualInformation => "Scan_Runtime_AlignmentModeEccThenMutualInformation".GetLocalized(),
            _ => mode.ToString()
        };

    private static string GetChannelRoleDisplayName(string role)
        => role switch
        {
            "Red" => "Scan_Runtime_ChannelRoleRed".GetLocalized(),
            "Green" => "Scan_Runtime_ChannelRoleGreen".GetLocalized(),
            "Blue" => "Scan_Runtime_ChannelRoleBlue".GetLocalized(),
            "White" => "Scan_Runtime_ChannelRoleWhite".GetLocalized(),
            "IR" => "Scan_Runtime_ChannelRoleIr".GetLocalized(),
            "Unused" => "Scan_Runtime_ChannelRoleUnused".GetLocalized(),
            _ => role
        };

    private static string FormatDngAffectedChannelRoles(ScanDngExportResult exportResult)
        => exportResult.AffectedChannelRoles.Count == 0
            ? "Scan_Runtime_DngAlignmentWarningUnknownChannels".GetLocalized()
            : string.Join(", ", exportResult.AffectedChannelRoles.Select(GetChannelRoleDisplayName));

    private string NormalizePreviewMode(string? previewMode)
        => string.IsNullOrWhiteSpace(previewMode) ? PreviewModes[0] : previewMode;

    private bool TryParsePreviewChannelIndex(string? previewMode, out int channelIndex)
    {
        channelIndex = -1;
        if (!ScanPreviewModeOption.TryGetRawChannelIndex(NormalizePreviewMode(previewMode), GetOutputChannelAssignment().Roles.Count, out var slot))
            return false;
        channelIndex = _lastResult is { } result
            ? result.Passes.ToList().FindIndex(capture => capture.LedChannelIndex == slot)
            : slot;
        return channelIndex >= 0;
    }

    public Task CleanupAsync()
    {
        if (_isDisposed)
            return Task.CompletedTask;

        _isDisposed = true;
        _uiLifetimeCts.Cancel();
        _scanCts?.Cancel();
        Deactivate();
        IsOutputAvailable = false;
        PreviewImage = null;
        return Task.CompletedTask;
    }
}
