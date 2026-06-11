using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
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

public sealed record ScanPreviewModeOption(string Key, string DisplayName);

public static class ScanWorkflowBlockerIds
{
    public const string None = nameof(None);
    public const string Device = nameof(Device);
    public const string Configuration = nameof(Configuration);
    public const string Execution = nameof(Execution);
    public const string Output = nameof(Output);
}

public partial class ScanViewModel : ObservableRecipient
{
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
    private static readonly Brush ScanCardNormalBorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush", Colors.Gray);
    private static readonly Brush ScanCardBlockerBorderBrush = GetThemeBrush("SystemFillColorCautionBrush", Colors.Goldenrod);

    private sealed record ScanStartBlocker(string CardId, string Message);

    private readonly IScannerDeviceSessionManager _sessionManager;
    private readonly IScanWorkflowSessionCoordinator _scanSessionCoordinator;
    private readonly IScanParameterService _parameters;
    private readonly IScanTransferSettingsService _transferSettings;
    private readonly IScanWorkflowService _workflow;
    private readonly IDebugOutputMirrorService _debugOutputMirror;
    private readonly IScanChannelImageService _channelImages;
    private readonly IScanDeviceSettingsService _deviceSettings;
    private readonly IScanColorManagementSettingsService _colorManagementSettings;
    private readonly IScanChannelParameterProfileService _channelProfiles;
    private readonly IUsbUsageCoordinator _usbUsageCoordinator;
    private readonly IUiDispatcher _dispatcher;
    private readonly CancellationTokenSource _uiLifetimeCts = new();

    private CancellationTokenSource? _scanCts;
    private ScanWorkflowResult? _lastResult;
    private ScanParameterSnapshot? _loadedSnapshot;
    private ushort _loadedExposureTicks;
    private uint _loadedSysClockKhz;
    private ScanFilmAcquisitionSettings? _selectedConfigAcquisitionSettings;
    private readonly Task _deviceSettingsInitializationTask;
    private bool _isConfigProfileLoaded;
    private bool _isApplyingDerivedMotorDistance;
    private bool _isMotorDistanceDerivedFromInterval = true;
    private string _lastMotorDistancePerLineUnit = MotorDistanceUnitMillimeters;
    private bool _isDisposed;
    private bool _areSessionEventsSubscribed;
    private bool _isLoadingColorManagementSettings;
    private bool _isLoadingConnectedSessionState;

    public ObservableCollection<string> RowOptions { get; } = new() { "64", "128", "256", "512", "1024", "2048", "4096", "8192" };
    public ObservableCollection<string> DirectionOptions { get; } = new() { ForwardDirection, ReverseDirection };
    public ObservableCollection<string> PreviewModes { get; } = new() { "RGB Composite", "Raw Channel 1", "Raw Channel 2", "Raw Channel 3", "Raw Channel 4" };
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
        ? string.Join(" | ", new[] { CurrentPassText, CurrentLedText, CurrentDirectionText }.Where(text => !string.IsNullOrWhiteSpace(text)))
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
    public partial bool IsSetupEditingEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsOutputSettingsEnabled { get; set; }

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
        IScanChannelParameterProfileService channelProfiles,
        IUsbUsageCoordinator usbUsageCoordinator,
        IUiDispatcher dispatcher)
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
        _deviceSettings = deviceSettings;
        _colorManagementSettings = colorManagementSettings;
        _channelProfiles = channelProfiles;
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
        MotorIntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString();
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
            _sessionManager.TargetsChanged += OnSessionTargetsChanged;
            _sessionManager.SnapshotChanged += OnSessionSnapshotChanged;
            _areSessionEventsSubscribed = true;
        }

        ApplyManagerSnapshot(_sessionManager.Snapshot);
        RefreshTargets();

        if (IsConnected && !HasLoadedScannerParameters())
            _ = LoadConnectedSessionStateIfNeededAsync();
    }

    public void Deactivate()
    {
        if (!_areSessionEventsSubscribed)
            return;

        _sessionManager.TargetsChanged -= OnSessionTargetsChanged;
        _sessionManager.SnapshotChanged -= OnSessionSnapshotChanged;
        _areSessionEventsSubscribed = false;
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
        OnColorManagementChanged(settings => settings with { IsEnabled = value });
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
    }

    partial void OnSelectedAlignmentModeChanged(ScanChannelAlignmentMode value)
    {
        RefreshLoadedScanRecipeSummary();
        UpdatePreviewState();
    }

    partial void OnRedWavelengthNmChanged(string value)
    {
        if (TryParseColorDouble(value, "Scan_Runtime_FieldRedWavelengthNm".GetLocalized(), out var parsed, out _))
            OnColorManagementChanged(settings => settings with { RedWavelengthNm = parsed });
        else
            UpdatePreviewState();

        RefreshLoadedScanRecipeSummary();
    }

    partial void OnGreenWavelengthNmChanged(string value)
    {
        if (TryParseColorDouble(value, "Scan_Runtime_FieldGreenWavelengthNm".GetLocalized(), out var parsed, out _))
            OnColorManagementChanged(settings => settings with { GreenWavelengthNm = parsed });
        else
            UpdatePreviewState();

        RefreshLoadedScanRecipeSummary();
    }

    partial void OnBlueWavelengthNmChanged(string value)
    {
        if (TryParseColorDouble(value, "Scan_Runtime_FieldBlueWavelengthNm".GetLocalized(), out var parsed, out _))
            OnColorManagementChanged(settings => settings with { BlueWavelengthNm = parsed });
        else
            UpdatePreviewState();

        RefreshLoadedScanRecipeSummary();
    }

    partial void OnOutputGammaChanged(string value)
    {
        if (TryParseColorDouble(value, "Scan_Runtime_FieldOutputGamma".GetLocalized(), out var parsed, out _))
            OnColorManagementChanged(settings => settings with { OutputGamma = parsed });
        else
            UpdatePreviewState();

        RefreshLoadedScanRecipeSummary();
    }

    partial void OnSelectedTargetWhitePointModeChanged(string value)
    {
        if (TryParseTargetWhitePointMode(value, out var mode))
            OnColorManagementChanged(settings => settings with { TargetWhitePointMode = mode });
        else
            UpdatePreviewState();

        RefreshLoadedScanRecipeSummary();
    }

    partial void OnManualWhitePointColorTemperatureKChanged(string value)
    {
        if (TryParseColorDouble(value, "Scan_Runtime_FieldManualWhitePointColorTemperatureK".GetLocalized(), out var parsed, out _))
            OnColorManagementChanged(settings => settings with { ManualWhitePointColorTemperatureK = parsed });
        else
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
        var roles = GetEffectiveDeviceChannelRoles();
        IsDualFileDngExportAvailable = ScanChannelRoleHelper.CountRole(roles, "Red") == 1
            && ScanChannelRoleHelper.CountRole(roles, "Green") == 1
            && ScanChannelRoleHelper.CountRole(roles, "Blue") == 1
            && ScanChannelRoleHelper.CountIrOrWhiteRoles(roles) == 1;
        IsSingleFileDngExportForced = !IsDualFileDngExportAvailable;

        if (!IsDualFileDngExportAvailable && SelectedDngExportMode == ScanDngExportMode.LinearRgbIrw)
            SelectedDngExportMode = ScanDngExportMode.LinearRaw4;
    }

    private void OnSessionTargetsChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(RefreshTargets);

    private void OnSessionSnapshotChanged(object? sender, ScannerDeviceSessionSnapshot snapshot)
        => _dispatcher.TryEnqueue(() => ApplyManagerSnapshot(snapshot));

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
    private bool CanStartScan() => !IsConnecting && !IsRunning && !IsOutputOperationRunning;
    private bool CanStopScan() => IsRunning;
    private bool HasLoadedScannerParameters() => _loadedSnapshot is not null && _loadedSysClockKhz >= ScanDebugConstants.MinSysClockKhz;
    private bool CanSaveRgbImage() => IsOutputAvailable && !IsRunning && !IsOutputOperationRunning;
    private bool CanExportDngChannels() => IsOutputAvailable && !IsRunning && !IsOutputOperationRunning;

    private void UpdateReadinessSummaries()
    {
        DeviceStatusSummaryText = BuildDeviceStatusSummary();
        ConfigurationStatusSummaryText = BuildConfigurationStatusSummary();
        ScanStatusSummaryText = BuildScanStatusSummary();
        OutputStatusSummaryText = "Scan_Runtime_OutputStatusSummary".GetLocalizedFormatOrFallback("{0}", OutputSummaryText ?? string.Empty);
        ReadinessReasonText = BuildReadinessReason();
        OutputActionReasonText = BuildOutputActionReason();
        RefreshInteractionState();

        if (PreviewImage is null && IsPreviewEnabled && _lastResult is null)
            PreviewPlaceholderText = BuildPreviewPlaceholderText();
    }

    private string BuildDeviceStatusSummary()
    {
        if (IsConnecting)
            return "Scan_Runtime_DeviceStatusConnecting".GetLocalizedOrFallback("Connecting scanner hardware...");

        if (IsConnected)
            return "Scan_Runtime_DeviceStatusConnected".GetLocalizedOrFallback("Scanner connected.");

        return IsDevicesPresent
            ? "Scan_Runtime_DeviceStatusDetected".GetLocalizedOrFallback("Scanner detected and ready to connect.")
            : "Scan_Runtime_DeviceStatusMissing".GetLocalizedOrFallback("No PRISM scanner (VID 0x0483 / PID 0x619C or 0x619D) detected.");
    }

    private string BuildConfigurationStatusSummary()
        => _isConfigProfileLoaded
            ? "Scan_Runtime_ConfigStatusLoaded".GetLocalizedFormatOrFallback("{0}", SelectedConfigProfileName ?? string.Empty)
            : "Scan_Runtime_ConfigStatusMissing".GetLocalizedOrFallback("No scan configuration loaded.");

    private void UpdateExecutionConfigSummary()
    {
        var rows = string.IsNullOrWhiteSpace(SelectedRows)
            ? "Scan_Runtime_ExecutionConfigUnavailable".GetLocalizedOrFallback("Unavailable")
            : SelectedRows;
        var motor = string.IsNullOrWhiteSpace(SelectedScanMotor)
            ? "Scan_Runtime_ExecutionConfigUnavailable".GetLocalizedOrFallback("Unavailable")
            : SelectedScanMotor;
        var lineMove = BuildLineMoveSummary();
        var dngMode = GetDngExportModeDisplayName(SelectedDngExportMode);

        ExecutionConfigSummaryText = "Scan_Runtime_ExecutionConfigSummary".GetLocalizedFormatOrFallback("Rows: {0} | Motor: {1} | Line move: {2} | DNG: {3}", rows, motor, lineMove, dngMode);
    }

    private string BuildLineMoveSummary()
    {
        var interval = string.IsNullOrWhiteSpace(MotorIntervalUs)
            ? "Scan_Runtime_ExecutionConfigUnavailable".GetLocalizedOrFallback("Unavailable")
            : MotorIntervalUs;

        return string.IsNullOrWhiteSpace(MotorDistancePerLineValue)
            ? "Scan_Runtime_ExecutionConfigIntervalOnly".GetLocalizedFormatOrFallback("Interval {0} us", interval)
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
        var canEdit = !IsConnecting && !IsRunning && !IsOutputOperationRunning;
        IsSetupEditingEnabled = canEdit;
        IsOutputSettingsEnabled = canEdit;
    }

    private async Task<ScanWorkflowRequest?> ValidateStartScanAsync()
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
            if (!TryGetEffectiveMotorIntervalUs(motorSettings, out _))
                blockers.Add(new(ScanWorkflowBlockerIds.Execution, "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanDebugConstants.MotionMinIntervalUs)));
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

        return request;
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
            await _channelProfiles.InitializeAsync();
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
            await _channelProfiles.InitializeAsync();
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
        var request = await ValidateStartScanAsync();
        if (request is null)
            return;

        ClearStartValidationState();
        ClearTopRiskBanner();

        _scanCts = new CancellationTokenSource();
        var uiToken = _uiLifetimeCts.Token;
        IsRunning = true;
        ScanProgressValue = 0;
        ScanProgressMaximum = 1;
        IsScanProgressIndeterminate = true;
        IsOutputAvailable = false;
        _lastResult = null;
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
                            _dispatcher.TryEnqueue(() => ApplyProgress(progress));
                    },
                    status =>
                    {
                        if (!uiToken.IsCancellationRequested)
                            _dispatcher.TryEnqueue(() => StatusText = ScanRuntimeMessageLocalizer.LocalizeScanViewStatus(status));
                    },
                    diagnostic => _debugOutputMirror.Mirror("Scan.Diagnostic", diagnostic)),
                CancellationToken.None,
                waitForAvailability: false);

            if (uiToken.IsCancellationRequested)
                return;

            _lastResult = result;
            IsOutputAvailable = true;
            ScanProgressMaximum = Math.Max(1, result.Passes.Count);
            ScanProgressValue = ScanProgressMaximum;
            IsScanProgressIndeterminate = false;
            OutputSummaryText = "Scan_Runtime_OutputSummaryCaptured".GetLocalizedFormat(result.Passes.Count, result.Rows, result.ComputedMotorStepsPerPass);
            StatusText = "Scan_Runtime_StatusCompleted".GetLocalized();
            ClearTopRiskBanner();
            UpdatePreviewState();
        }
        catch (OperationCanceledException)
        {
            if (uiToken.IsCancellationRequested)
                return;

            StatusText = "Scan_Runtime_StatusCanceled".GetLocalized();
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

    [RelayCommand(CanExecute = nameof(CanSaveRgbImage))]
    private async Task SaveRgbImage()
    {
        if (_lastResult is null)
        {
            StatusText = "Scan_Runtime_StatusNoRgbResult".GetLocalized();
            return;
        }

        if (!TryBuildColorManagementOptions(out var colorManagement, out var colorError))
        {
            StatusText = colorError;
            return;
        }

        try
        {
            var file = await _channelImages.PickRgbImageFileAsync($"scan_rgb_{DateTimeOffset.Now:yyyyMMdd_HHmmss}");
            if (file is null)
            {
                StatusText = "Scan_Runtime_StatusRgbSaveCanceled".GetLocalized();
                return;
            }

            IsOutputOperationRunning = true;
            StatusText = "Scan_Runtime_StatusRgbSaving".GetLocalized();
            var buffer = await _channelImages.BuildRgbCompositeBufferAsync(_lastResult, BuildChannelAssignment(), colorManagement, SelectedAlignmentMode, _channelProfiles.Profiles, true);
            await _channelImages.SaveRgbImageAsync(file, buffer);
            StatusText = "Scan_Runtime_StatusRgbSaved".GetLocalizedFormat(file.Path);
            ClearTopRiskBanner();
        }
        catch (Exception ex)
        {
            StatusText = "Scan_Runtime_StatusRgbSaveFailed".GetLocalizedFormat(ex.Message);
            ShowTopRiskBanner("Scan_Runtime_TopRiskOutputFailedTitle".GetLocalizedOrFallback("Output failed"), StatusText, InfoBarSeverity.Error);
        }
        finally
        {
            IsOutputOperationRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportDngChannels))]
    private async Task ExportDngChannels()
    {
        if (_lastResult is null)
        {
            StatusText = "Scan_Runtime_StatusNoDngResult".GetLocalized();
            return;
        }

        try
        {
            var folder = await _channelImages.PickDngExportFolderAsync();
            if (folder is null)
            {
                StatusText = "Scan_Runtime_StatusDngExportCanceled".GetLocalized();
                return;
            }

            IsOutputOperationRunning = true;
            await _channelImages.ExportDngChannelsAsync(folder, _lastResult, BuildChannelAssignment(), SelectedAlignmentMode, SelectedDngExportMode);
            StatusText = "Scan_Runtime_StatusDngExported".GetLocalizedFormat(folder.Path);
            ClearTopRiskBanner();
        }
        catch (Exception ex)
        {
            StatusText = "Scan_Runtime_StatusDngExportFailed".GetLocalizedFormat(ex.Message);
            ShowTopRiskBanner("Scan_Runtime_TopRiskOutputFailedTitle".GetLocalizedOrFallback("Output failed"), StatusText, InfoBarSeverity.Error);
        }
        finally
        {
            IsOutputOperationRunning = false;
        }
    }

    [RelayCommand]
    private async Task LoadConfigProfile()
    {
        try
        {
            await _channelProfiles.InitializeAsync();

            var imported = await _channelProfiles.ImportProfilesAsync();
            if (imported is null)
            {
                StatusText = "Scan_Runtime_StatusLoadConfigProfileCanceled".GetLocalized();
                return;
            }

            await _channelProfiles.ReplaceProfilesAsync(imported);
            _isConfigProfileLoaded = true;
            StartScanCommand.NotifyCanExecuteChanged();
            SelectedConfigProfileName = imported.ProfileName;
            _selectedConfigAcquisitionSettings = imported.AcquisitionSettings?.Normalize();

            if (_selectedConfigAcquisitionSettings is not null)
                ApplyAcquisitionSettingsToInputs(_selectedConfigAcquisitionSettings);

            ApplyScanRecipeSettings(imported.ScanRecipeSettings);
            RefreshLoadedScanRecipeSummary();
            UpdateExecutionConfigSummary();
            UpdateReadinessSummaries();
            ClearStartValidationState();

            StatusText = (_selectedConfigAcquisitionSettings is null
                    ? "Scan_Runtime_StatusConfigProfileLoadedLegacy"
                    : "Scan_Runtime_StatusConfigProfileLoaded")
                .GetLocalizedFormat(imported.ProfileName);
        }
        catch (Exception ex)
        {
            StatusText = "Scan_Runtime_StatusLoadConfigProfileFailed".GetLocalizedFormat(ex.Message);
        }
    }

    private void ApplyProgress(ScanWorkflowProgress progress)
    {
        UpdateScanProgress(progress);
        CurrentPassText = "Scan_Runtime_CurrentPassProgress".GetLocalizedFormat(progress.CurrentPass, progress.TotalPasses, ScanRuntimeMessageLocalizer.LocalizeScanWorkflowStage(progress.Stage));
        CurrentLedText = "Scan_Runtime_CurrentLedProgress".GetLocalizedFormat(progress.LedChannelIndex + 1);
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

        if (string.Equals(SelectedPreviewMode, PreviewModes[0], StringComparison.Ordinal))
        {
            if (!TryBuildColorManagementOptions(out var colorManagement, out var colorError))
            {
                PreviewImage = null;
                PreviewDescriptionText = colorError;
                PreviewPlaceholderText = colorError;
                return;
            }

            if (_channelImages.TryBuildRgbComposite(_lastResult, BuildChannelAssignment(), colorManagement, SelectedAlignmentMode, PreviewImage, out var frame, out var error, _channelProfiles.Profiles, true) && frame is not null)
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

        var assignment = BuildChannelAssignment();
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
        RefreshLoadedScanRecipeSummary();
    }

    private string BuildPreviewPlaceholderText()
    {
        if (IsRunning)
            return "Scan_Runtime_PreviewPlaceholderScanning".GetLocalizedOrFallback("Preview updates will appear here while scanning.");

        if (!IsConnected || !_isConfigProfileLoaded || !HasLoadedScannerParameters())
            return "Scan_Runtime_PreviewPlaceholderPrepareFirst".GetLocalizedOrFallback("Connect devices and run a scan to populate the preview.");

        return "Scan_Runtime_PreviewPlaceholderNextScan".GetLocalizedFormat(GetPreviewModeDisplayName(SelectedPreviewMode));
    }

    private void UpdatePreviewModeOptions()
    {
        for (var i = 0; i < PreviewModes.Count; i++)
        {
            var mode = PreviewModes[i];
            var option = new ScanPreviewModeOption(mode, GetPreviewModeDisplayName(mode));
            if (i < PreviewModeOptions.Count)
                PreviewModeOptions[i] = option;
            else
                PreviewModeOptions.Add(option);
        }

        while (PreviewModeOptions.Count > PreviewModes.Count)
            PreviewModeOptions.RemoveAt(PreviewModeOptions.Count - 1);
    }

    private void UpdateComputedMotorSummary()
    {
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
        if (!TryGetEffectiveMotorIntervalUs(motorSettings, out var intervalUs))
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorIntervalMinimum".GetLocalizedFormat(ScanDebugConstants.MotionMinIntervalUs);
            return;
        }

        var computedSteps = ScanTimingMath.ComputeMotorStepsPerPass(rows, _loadedExposureTicks, _loadedSysClockKhz, intervalUs);
        var distanceMm = ScanTimingMath.ConvertMotorStepsToMillimeters(computedSteps, motorSettings);
        var speedMmPerSecond = ScanTimingMath.ConvertMotorIntervalToMillimetersPerSecond(intervalUs, motorSettings);
        ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorSummary".GetLocalizedFormat(GetMotorDisplayIndex(), computedSteps, intervalUs, rows, distanceMm.ToString("0.###", CultureInfo.InvariantCulture), speedMmPerSecond.ToString("0.###", CultureInfo.InvariantCulture));
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

        if (!TryParseSelectedMotor(out var motorId, out error))
            return false;

        _deviceSettingsInitializationTask.GetAwaiter().GetResult();

        var motorSettings = _deviceSettings.Settings.GetMotorSettings(motorId);
        if (!TryGetEffectiveMotorIntervalUs(motorSettings, out var intervalUs))
        {
            error = "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanDebugConstants.MotionMinIntervalUs);
            return false;
        }

        if (!HasLoadedScannerParameters() || _loadedSnapshot is not { } fallbackSnapshot)
        {
            error = "Scan_Runtime_ErrorParametersNotLoaded".GetLocalized();
            return false;
        }
        var passRoles = GetEffectiveDeviceChannelRoles();
        var passProfiles = passRoles
            .Select(role => _channelProfiles.TryGetProfile(role, out var profile) ? profile.Parameters : fallbackSnapshot)
            .ToArray();
        var acquisitionSettings = BuildWorkflowAcquisitionSettings(intervalUs);

        request = new ScanWorkflowRequest(
            rows,
            IsWarmUpEnabled,
            new[] { acquisitionSettings.Led1Level, acquisitionSettings.Led2Level, acquisitionSettings.Led3Level, acquisitionSettings.Led4Level },
            passRoles,
            passProfiles,
            motorId,
            intervalUs,
            string.Equals(SelectedStartingDirection, ForwardDirection, StringComparison.OrdinalIgnoreCase),
            IsAlternateMotorDirectionEnabled,
            _loadedExposureTicks,
            _loadedSysClockKhz,
            acquisitionSettings);

        error = string.Empty;
        return true;
    }

    private bool TryParseSelectedMotor(out byte motorId, out string error)
    {
        motorId = 0;
        if (!SelectedScanMotor.StartsWith("Motor", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(SelectedScanMotor[5..], out var displayIndex)
            || displayIndex < 1
            || displayIndex > ScanDebugConstants.MotionMotorCount)
        {
            error = "Scan_Runtime_ErrorScanMotorRange".GetLocalizedFormat(ScanDebugConstants.MotionMotorCount);
            return false;
        }

        motorId = (byte)(displayIndex - 1);
        error = string.Empty;
        return true;
    }

    private int GetMotorDisplayIndex()
        => TryParseSelectedMotor(out var motorId, out _) ? motorId + 1 : 1;

    private void ApplyAcquisitionSettingsToInputs(ScanFilmAcquisitionSettings settings)
    {
        var normalized = settings.Normalize();
        MotorIntervalUs = normalized.MotorIntervalUs.ToString(CultureInfo.InvariantCulture);
        _isMotorDistanceDerivedFromInterval = true;
        RefreshDerivedMotorDistanceFromCurrentInterval();
    }

    private void ApplyScanRecipeSettings(ScanFilmScanRecipeSettings? settings)
    {
        if (settings?.ChannelAssignment is { } assignment)
        {
            IsChannel1Reversed = assignment.Channel1Reversed;
            IsChannel2Reversed = assignment.Channel2Reversed;
            IsChannel3Reversed = assignment.Channel3Reversed;
            IsChannel4Reversed = assignment.Channel4Reversed;
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
    }

    private void RefreshLoadedScanRecipeSummary()
    {
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
    }

    private bool TryGetEffectiveMotorIntervalUs(ScanMotorMechanicalSettings motorSettings, out uint intervalUs)
    {
        intervalUs = 0;

        if (_isMotorDistanceDerivedFromInterval)
            return uint.TryParse(MotorIntervalUs, NumberStyles.Integer, CultureInfo.InvariantCulture, out intervalUs)
                && intervalUs >= ScanDebugConstants.MotionMinIntervalUs;

        if (_loadedSysClockKhz < ScanDebugConstants.MinSysClockKhz)
            return false;

        if (!ScanMotorDistanceText.TryParseMillimeters(MotorDistancePerLineValue, MotorDistancePerLineUnit, motorSettings, out var lineDistanceMm))
            return false;

        if (!ScanTimingMath.TryConvertLineDistanceMillimetersToMotorIntervalUs(lineDistanceMm, _loadedExposureTicks, _loadedSysClockKhz, motorSettings, ScanDebugConstants.MotionMinIntervalUs, out intervalUs))
            return false;

        MotorIntervalUs = intervalUs.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private void RefreshDerivedMotorDistanceFromCurrentInterval()
    {
        if (!_isMotorDistanceDerivedFromInterval)
            return;

        if (!uint.TryParse(MotorIntervalUs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intervalUs)
            || intervalUs < ScanDebugConstants.MotionMinIntervalUs
            || _loadedSysClockKhz < ScanDebugConstants.MinSysClockKhz
            || !TryParseSelectedMotor(out var motorId, out _))
        {
            ApplyDerivedMotorDistance(string.Empty);
            return;
        }

        var lineDistanceMm = ScanTimingMath.ConvertMotorIntervalToLineDistanceMillimeters(intervalUs, _loadedExposureTicks, _loadedSysClockKhz, _deviceSettings.Settings.GetMotorSettings(motorId));
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

    private ScanFilmAcquisitionSettings BuildWorkflowAcquisitionSettings(uint motorIntervalUs)
    {
        var source = (_selectedConfigAcquisitionSettings ?? ScanFilmAcquisitionSettings.CreateDefault()).Normalize();
        return new ScanFilmAcquisitionSettings(
            source.Led1Level,
            source.Led2Level,
            source.Led3Level,
            source.Led4Level,
            source.SteadyMask,
            source.SyncMask,
            source.Led1PulseClock,
            source.Led2PulseClock,
            source.Led3PulseClock,
            source.Led4PulseClock,
            motorIntervalUs).Normalize();
    }

    private bool CanRunExtendedScan()
        => _transferSettings.Settings.ReadMode == ScanBulkInReadMode.MultiBuffered && _transferSettings.Settings.RawIoEnabled;

    private static Brush GetThemeBrush(string resourceKey, Color fallbackColor)
        => Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true && resource is Brush brush
            ? brush
            : new SolidColorBrush(fallbackColor);

    private ScanChannelAssignment BuildChannelAssignment()
    {
        var roles = GetEffectiveDeviceChannelRoles();
        return new(roles[0], roles[1], roles[2], roles[3], IsChannel1Reversed, IsChannel2Reversed, IsChannel3Reversed, IsChannel4Reversed);
    }

    private string[] GetEffectiveDeviceChannelRoles()
        => _deviceSettings.Settings.Normalize().ChannelRoles.ToArray();

    private async Task LoadColorManagementSettingsAsync()
    {
        _isLoadingColorManagementSettings = true;
        try
        {
            await _colorManagementSettings.InitializeAsync();
            ApplyColorManagementSettings(_colorManagementSettings.Settings);
        }
        finally
        {
            _isLoadingColorManagementSettings = false;
        }

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

    private void OnColorManagementChanged(Func<ScanColorManagementOptions, ScanColorManagementOptions> mutate)
    {
        UpdatePreviewState();
        if (_isLoadingColorManagementSettings)
            return;

        _ = SaveColorManagementSettingsAsync(mutate);
    }

    private async Task SaveColorManagementSettingsAsync(Func<ScanColorManagementOptions, ScanColorManagementOptions> mutate)
    {
        await _colorManagementSettings.InitializeAsync();
        await _colorManagementSettings.SetSettingsAsync(mutate(_colorManagementSettings.Settings));
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

        if (string.Equals(mode, PreviewModes[0], StringComparison.Ordinal))
            return "Scan_Runtime_PreviewModeRgbComposite".GetLocalized();

        if (TryParsePreviewChannelIndex(mode, out var channelIndex))
        {
            var roles = GetEffectiveDeviceChannelRoles();
            if (channelIndex < roles.Length)
                return "Scan_Runtime_PreviewModeRawRole".GetLocalizedFormatOrFallback("Raw ({0})", GetChannelRoleDisplayName(roles[channelIndex]));
        }

        return mode;
    }

    private static string GetDngExportModeDisplayName(ScanDngExportMode mode)
        => mode switch
        {
            ScanDngExportMode.LinearRaw4 => "Scan_Runtime_DngExportModeLinearRaw4".GetLocalized(),
            ScanDngExportMode.LinearRgbIrw => "Scan_Runtime_DngExportModeLinearRgbIrw".GetLocalized(),
            _ => mode.ToString()
        };

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

    private string NormalizePreviewMode(string? previewMode)
        => string.IsNullOrWhiteSpace(previewMode) ? PreviewModes[0] : previewMode;

    private bool TryParsePreviewChannelIndex(string? previewMode, out int channelIndex)
    {
        channelIndex = -1;
        previewMode = NormalizePreviewMode(previewMode);
        if (!previewMode.StartsWith("Raw Channel ", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(previewMode[12..], out var oneBasedIndex)
               && (channelIndex = oneBasedIndex - 1) >= 0
               && channelIndex < ScanDebugConstants.IlluminationChannelCount;
    }

    public Task CleanupAsync()
    {
        if (_isDisposed)
            return Task.CompletedTask;

        _isDisposed = true;
        _uiLifetimeCts.Cancel();
        Deactivate();
        IsOutputAvailable = false;
        PreviewImage = null;
        return Task.CompletedTask;
    }
}
