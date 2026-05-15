using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;
using PRISM_Utility.Models;

namespace PRISM_Utility.ViewModels;

public partial class ScanViewModel : ObservableRecipient
{
    private const string ForwardDirection = "Forward";
    private const string ReverseDirection = "Reverse";
    private const string MotorDistanceUnitSteps = "steps";
    private const string MotorDistanceUnitMicrometers = "um";
    private const string MotorDistanceUnitMillimeters = "mm";
    private static readonly string[] MotorDistanceUnitLabels = { MotorDistanceUnitSteps, MotorDistanceUnitMicrometers, MotorDistanceUnitMillimeters };
    private const double DefaultRedWavelengthNm = 680.0;
    private const double DefaultGreenWavelengthNm = 525.0;
    private const double DefaultBlueWavelengthNm = 450.0;
    private const double DefaultOutputGamma = 2.2;
    private readonly IScanSessionService _session;
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

    private CancellationTokenSource? _scanCts;
    private ScanWorkflowResult? _lastResult;
    private ScanParameterSnapshot? _loadedSnapshot;
    private ushort _loadedExposureTicks;
    private uint _loadedSysClockKhz;
    private ScanFilmAcquisitionSettings? _selectedConfigAcquisitionSettings;
    private readonly Task _deviceSettingsInitializationTask;
    private bool _isApplyingDerivedMotorDistance;
    private bool _isMotorDistanceDerivedFromInterval = true;
    private string _lastMotorDistancePerLineUnit = MotorDistanceUnitMillimeters;
    private bool _isDisposed;
    private bool _isLoadingColorManagementSettings;

    public ObservableCollection<string> RowOptions { get; } = new() { "64", "128", "256", "512", "1024", "2048", "4096", "8192" };
    public ObservableCollection<string> DirectionOptions { get; } = new() { ForwardDirection, ReverseDirection };
    public ObservableCollection<string> ChannelRoleOptions { get; } = new() { "Red", "Green", "Blue", "White", "IR", "Unused" };
    public ObservableCollection<string> PreviewModes { get; } = new() { "RGB Composite", "Raw Channel 1", "Raw Channel 2", "Raw Channel 3", "Raw Channel 4" };
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
    public partial string SelectedChannel1Role { get; set; }

    [ObservableProperty]
    public partial string SelectedChannel2Role { get; set; }

    [ObservableProperty]
    public partial string SelectedChannel3Role { get; set; }

    [ObservableProperty]
    public partial string SelectedChannel4Role { get; set; }

    [ObservableProperty]
    public partial string RedWavelengthNm { get; set; }

    [ObservableProperty]
    public partial string GreenWavelengthNm { get; set; }

    [ObservableProperty]
    public partial string BlueWavelengthNm { get; set; }

    [ObservableProperty]
    public partial string OutputGamma { get; set; }

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
    public partial string MotorDistancePerLineValue { get; set; }

    [ObservableProperty]
    public partial string MotorDistancePerLineUnit { get; set; }

    [ObservableProperty]
    public partial string MotorIntervalUs { get; set; }

    [ObservableProperty]
    public partial string Led1Level { get; set; }

    [ObservableProperty]
    public partial string Led2Level { get; set; }

    [ObservableProperty]
    public partial string Led3Level { get; set; }

    [ObservableProperty]
    public partial string Led4Level { get; set; }

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
    public partial WriteableBitmap? PreviewImage { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string CurrentPassText { get; set; }

    [ObservableProperty]
    public partial string CurrentLedText { get; set; }

    [ObservableProperty]
    public partial string CurrentDirectionText { get; set; }

    [ObservableProperty]
    public partial string PreviewPlaceholderText { get; set; }

    [ObservableProperty]
    public partial string PreviewDescriptionText { get; set; }

    [ObservableProperty]
    public partial string ChannelMappingSummaryText { get; set; }

    [ObservableProperty]
    public partial string OutputSummaryText { get; set; }

    [ObservableProperty]
    public partial string ComputedMotorSummaryText { get; set; }

    public ScanViewModel(
        IScanSessionService session,
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
        _session = session;
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

        SelectedRows = RowOptions[1];
        IsWarmUpEnabled = false;
        IsPreviewEnabled = true;
        IsAlternateMotorDirectionEnabled = true;
        IsColorManagementEnabled = true;
        SelectedStartingDirection = DirectionOptions[0];
        SelectedPreviewMode = PreviewModes[0];
        SelectedChannel1Role = "Blue";
        SelectedChannel2Role = "White";
        SelectedChannel3Role = "Red";
        SelectedChannel4Role = "Green";
        RedWavelengthNm = DefaultRedWavelengthNm.ToString("0");
        GreenWavelengthNm = DefaultGreenWavelengthNm.ToString("0");
        BlueWavelengthNm = DefaultBlueWavelengthNm.ToString("0");
        OutputGamma = DefaultOutputGamma.ToString("0.0");
        SelectedDngExportMode = DngExportModeOptions[0];
        SelectedAlignmentMode = AlignmentModeOptions[0];
        UpdateDngExportModeAvailability();
        IsChannel1Reversed = false;
        IsChannel2Reversed = false;
        IsChannel3Reversed = false;
        IsChannel4Reversed = false;
        SelectedScanMotor = MotorOptions[Math.Min(1, MotorOptions.Count - 1)];
        SelectedConfigProfileName = "Scan_Runtime_ConfigProfileNotSelected".GetLocalized();
        MotorDistancePerLineValue = string.Empty;
        MotorDistancePerLineUnit = MotorDistanceUnitMillimeters;
        MotorIntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString();
        Led1Level = "0";
        Led2Level = "0";
        Led3Level = "0";
        Led4Level = "0";
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
        OutputSummaryText = "Scan_Runtime_OutputSummaryEmpty".GetLocalized();
        ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorUnavailableUntilParametersLoaded".GetLocalized();

        _session.TargetsChanged += OnSessionTargetsChanged;
        RefreshTargets();
        UpdatePassPlan();
        UpdateChannelMappingSummary();
        UpdatePreviewState();
        _ = LoadColorManagementSettingsAsync();
    }

    partial void OnIsAlternateMotorDirectionEnabledChanged(bool value)
        => UpdatePassPlan();

    partial void OnSelectedStartingDirectionChanged(string value)
        => UpdatePassPlan();

    partial void OnSelectedRowsChanged(string value)
        => UpdateComputedMotorSummary();

    partial void OnMotorDistancePerLineValueChanged(string value)
    {
        if (_isApplyingDerivedMotorDistance)
        {
            UpdateComputedMotorSummary();
            return;
        }

        _isMotorDistanceDerivedFromInterval = false;
        UpdateComputedMotorSummary();
    }

    partial void OnMotorDistancePerLineUnitChanged(string value)
    {
        var normalizedUnit = NormalizeMotorDistanceUnit(value);
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
            return;
        }

        if (!string.Equals(previousUnit, normalizedUnit, StringComparison.Ordinal)
            && TryParseDisplayedMotorDistanceMillimeters(MotorDistancePerLineValue, previousUnit, GetCurrentMotorSettings(), out var lineDistanceMm)
            && TryFormatMotorDistanceDisplayValue(lineDistanceMm, normalizedUnit, GetCurrentMotorSettings(), out var convertedValue))
        {
            ApplyDerivedMotorDistance(convertedValue);
        }

        UpdateComputedMotorSummary();
    }

    partial void OnMotorIntervalUsChanged(string value)
    {
        if (_isMotorDistanceDerivedFromInterval)
            RefreshDerivedMotorDistanceFromCurrentInterval();

        UpdateComputedMotorSummary();
    }

    partial void OnSelectedScanMotorChanged(string value)
    {
        if (_isMotorDistanceDerivedFromInterval)
            RefreshDerivedMotorDistanceFromCurrentInterval();

        UpdateComputedMotorSummary();
    }

    partial void OnIsPreviewEnabledChanged(bool value)
        => UpdatePreviewState();

    partial void OnStatusTextChanged(string value)
        => MirrorOutput("Scan.Status", value);

    partial void OnCurrentPassTextChanged(string value)
        => MirrorOutput("Scan.Pass", value);

    partial void OnCurrentLedTextChanged(string value)
        => MirrorOutput("Scan.Led", value);

    partial void OnCurrentDirectionTextChanged(string value)
        => MirrorOutput("Scan.Direction", value);

    partial void OnOutputSummaryTextChanged(string value)
        => MirrorOutput("Scan.Output", value);

    partial void OnIsColorManagementEnabledChanged(bool value)
        => OnColorManagementChanged(settings => settings with { IsEnabled = value });

    partial void OnSelectedPreviewModeChanged(string value)
        => UpdatePreviewState();

    partial void OnSelectedDngExportModeChanged(ScanDngExportMode value)
    {
        if (value == ScanDngExportMode.LinearRgbIrw && !IsDualFileDngExportAvailable)
            SelectedDngExportMode = ScanDngExportMode.LinearRaw4;
    }

    partial void OnSelectedAlignmentModeChanged(ScanChannelAlignmentMode value)
        => UpdatePreviewState();

    partial void OnSelectedChannel1RoleChanged(string value)
        => OnChannelRoleChanged();

    partial void OnSelectedChannel2RoleChanged(string value)
        => OnChannelRoleChanged();

    partial void OnSelectedChannel3RoleChanged(string value)
        => OnChannelRoleChanged();

    partial void OnSelectedChannel4RoleChanged(string value)
        => OnChannelRoleChanged();

    partial void OnRedWavelengthNmChanged(string value)
    {
        if (TryParseColorDouble(value, "Scan_Runtime_FieldRedWavelengthNm".GetLocalized(), out var parsed, out _))
            OnColorManagementChanged(settings => settings with { RedWavelengthNm = parsed });
        else
            UpdatePreviewState();
    }

    partial void OnGreenWavelengthNmChanged(string value)
    {
        if (TryParseColorDouble(value, "Scan_Runtime_FieldGreenWavelengthNm".GetLocalized(), out var parsed, out _))
            OnColorManagementChanged(settings => settings with { GreenWavelengthNm = parsed });
        else
            UpdatePreviewState();
    }

    partial void OnBlueWavelengthNmChanged(string value)
    {
        if (TryParseColorDouble(value, "Scan_Runtime_FieldBlueWavelengthNm".GetLocalized(), out var parsed, out _))
            OnColorManagementChanged(settings => settings with { BlueWavelengthNm = parsed });
        else
            UpdatePreviewState();
    }

    partial void OnOutputGammaChanged(string value)
    {
        if (TryParseColorDouble(value, "Scan_Runtime_FieldOutputGamma".GetLocalized(), out var parsed, out _))
            OnColorManagementChanged(settings => settings with { OutputGamma = parsed });
        else
            UpdatePreviewState();
    }

    partial void OnIsChannel1ReversedChanged(bool value)
        => OnChannelRoleChanged();

    partial void OnIsChannel2ReversedChanged(bool value)
        => OnChannelRoleChanged();

    partial void OnIsChannel3ReversedChanged(bool value)
        => OnChannelRoleChanged();

    partial void OnIsChannel4ReversedChanged(bool value)
        => OnChannelRoleChanged();

    private void OnChannelRoleChanged()
    {
        UpdateDngExportModeAvailability();
        UpdateChannelMappingSummary();
        UpdatePreviewState();
    }

    private void UpdateDngExportModeAvailability()
    {
        var roles = new[] { SelectedChannel1Role, SelectedChannel2Role, SelectedChannel3Role, SelectedChannel4Role };
        IsDualFileDngExportAvailable = CountRole(roles, "Red") == 1
            && CountRole(roles, "Green") == 1
            && CountRole(roles, "Blue") == 1
            && CountIrOrWhiteRole(roles) == 1;
        IsSingleFileDngExportForced = !IsDualFileDngExportAvailable;

        if (!IsDualFileDngExportAvailable && SelectedDngExportMode == ScanDngExportMode.LinearRgbIrw)
            SelectedDngExportMode = ScanDngExportMode.LinearRaw4;
    }

    private static int CountRole(IEnumerable<string> roles, string role)
        => roles.Count(candidate => string.Equals(candidate, role, StringComparison.Ordinal));

    private static int CountIrOrWhiteRole(IEnumerable<string> roles)
        => roles.Count(candidate => string.Equals(candidate, "IR", StringComparison.Ordinal) || string.Equals(candidate, "White", StringComparison.Ordinal));

    private void OnSessionTargetsChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(RefreshTargets);

    private void RefreshTargets()
    {
        IsDevicesPresent = _session.Targets.IsDevicesPresent;

        if (!IsConnected && !IsConnecting)
        {
            StatusText = IsDevicesPresent
                ? "Scan_Runtime_StatusDevicesDetected".GetLocalized()
                : "Scan_Runtime_StatusWaitingForDevices".GetLocalized();
        }
    }

    private bool CanConnectDevices() => IsDevicesPresent && !IsConnected && !IsConnecting && !IsRunning;
    private bool CanDisconnectDevices() => IsConnected && !IsConnecting && !IsRunning;
    private bool CanStartScan() => IsConnected && !IsConnecting && !IsRunning;
    private bool CanStopScan() => IsRunning;
    private bool CanSaveRgbImage() => IsOutputAvailable && !IsRunning;
    private bool CanExportDngChannels() => IsOutputAvailable && !IsRunning;

    [RelayCommand(CanExecute = nameof(CanConnectDevices))]
    private async Task ConnectDevices()
    {
        if (_usbUsageCoordinator.IsUsbDebugInUse)
        {
            StatusText = "Scan_Runtime_UsbDebugActive".GetLocalized();
            return;
        }

        IsConnecting = true;
        try
        {
            await _transferSettings.InitializeAsync();
            await _channelProfiles.InitializeAsync();
            await _deviceSettings.InitializeAsync();

            var result = await _session.ConnectAsync(CancellationToken.None);
            if (!result.Success)
            {
                StatusText = ScanRuntimeMessageLocalizer.LocalizeScanViewStatus(result.Message);
                return;
            }

            IsConnected = true;
            _usbUsageCoordinator.SetScanDebugInUse(true);
            StatusText = "Scan_Runtime_StatusLoadingState".GetLocalized();

            var statusNotes = new List<string>();

            try
            {
                var snapshot = await _parameters.LoadAsync(_session, _session.ConnectionToken);
                _loadedExposureTicks = snapshot.ExposureTicks;
                _loadedSysClockKhz = snapshot.SysClockKhz;
                _loadedSnapshot = snapshot;
                RefreshDerivedMotorDistanceFromCurrentInterval();
                statusNotes.Add("Scan_Runtime_StatusParametersLoaded".GetLocalizedFormat(snapshot.ExposureTicks, snapshot.SysClockKhz));
            }
            catch (Exception ex)
            {
                _loadedSnapshot = null;
                _loadedExposureTicks = 0;
                _loadedSysClockKhz = 0;
                statusNotes.Add("Scan_Runtime_StatusParameterLoadUnavailable".GetLocalizedFormat(ex.Message));
            }

            try
            {
                var illumination = await _session.GetIlluminationStateAsync(_session.ConnectionToken);
                Led1Level = illumination.Led1Level.ToString();
                Led2Level = illumination.Led2Level.ToString();
                Led3Level = illumination.Led3Level.ToString();
                Led4Level = illumination.Led4Level.ToString();
                statusNotes.Add("Scan_Runtime_StatusIlluminationLoaded".GetLocalized());
            }
            catch (Exception ex)
            {
                statusNotes.Add("Scan_Runtime_StatusIlluminationUnavailable".GetLocalizedFormat(ex.Message));
            }

            if (_selectedConfigAcquisitionSettings is not null)
                ApplyAcquisitionSettingsToInputs(_selectedConfigAcquisitionSettings);

            UpdateComputedMotorSummary();
            StatusText = statusNotes.Count > 0
                ? "Scan_Runtime_StatusConnectedWithNotes".GetLocalizedFormat(string.Join(". ", statusNotes))
                : "Scan_Runtime_StatusConnected".GetLocalized();
        }
        catch (Exception ex)
        {
            await _session.DisconnectAsync();
            IsConnected = false;
            _usbUsageCoordinator.SetScanDebugInUse(false);
            StatusText = "Scan_Runtime_StatusConnectFailed".GetLocalizedFormat(ex.Message);
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
            _scanCts?.Cancel();
            await _session.DisconnectAsync();
            _usbUsageCoordinator.SetScanDebugInUse(false);
            IsConnected = false;
            IsOutputAvailable = false;
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
        if (!TryBuildWorkflowRequest(out var request, out var error))
        {
            StatusText = error;
            return;
        }

        if (request.Rows > _session.SingleTransferMaxRows && !request.WarmUpEnabled && !CanRunExtendedScan())
        {
            StatusText = "Scan_Runtime_StatusRowsLimitExceeded".GetLocalizedFormat(_session.SingleTransferMaxRows);
            return;
        }

        _scanCts = new CancellationTokenSource();
        IsRunning = true;
        IsOutputAvailable = false;
        _lastResult = null;
        OutputSummaryText = "Scan_Runtime_OutputSummaryInProgress".GetLocalized();

        try
        {
            var result = await _workflow.ExecuteAsync(
                _session,
                request,
                _scanCts.Token,
                progress => _dispatcher.TryEnqueue(() => ApplyProgress(progress)),
                status => _dispatcher.TryEnqueue(() => StatusText = ScanRuntimeMessageLocalizer.LocalizeScanViewStatus(status)),
                diagnostic => _debugOutputMirror.Mirror("Scan.Diagnostic", diagnostic));

            _lastResult = result;
            IsOutputAvailable = true;
            OutputSummaryText = "Scan_Runtime_OutputSummaryCaptured".GetLocalizedFormat(result.Passes.Count, result.Rows, result.ComputedMotorStepsPerPass);
            StatusText = "Scan_Runtime_StatusCompleted".GetLocalized();
            UpdatePreviewState();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan_Runtime_StatusCanceled".GetLocalized();
            CurrentPassText = "Scan_Runtime_CurrentPassCanceled".GetLocalized();
            CurrentLedText = "Scan_Runtime_CurrentLedStopped".GetLocalized();
            CurrentDirectionText = "Scan_Runtime_CurrentDirectionStopped".GetLocalized();
        }
        catch (Exception ex)
        {
            StatusText = "Scan_Runtime_StatusFailed".GetLocalizedFormat(ScanRuntimeMessageLocalizer.LocalizeScanViewStatus(ex.Message));
            OutputSummaryText = "Scan_Runtime_OutputSummaryFailed".GetLocalized();
        }
        finally
        {
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
                await _session.StopMotorAsync(motorId, CancellationToken.None);
            }
            catch
            {
            }
        }

        var result = await _session.StopScanAsync(CancellationToken.None);
        StatusText = result.Success ? "Scan_Runtime_StatusStopRequested".GetLocalized() : ScanRuntimeMessageLocalizer.LocalizeScanViewStatus(result.Message);
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

        if (!_channelImages.TryBuildRgbComposite(_lastResult, BuildChannelAssignment(), colorManagement, SelectedAlignmentMode, null, out var frame, out var error) || frame is null)
        {
            StatusText = error;
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

            await _channelImages.SaveRgbImageAsync(file, frame);
            StatusText = "Scan_Runtime_StatusRgbSaved".GetLocalizedFormat(file.Path);
        }
        catch (Exception ex)
        {
            StatusText = "Scan_Runtime_StatusRgbSaveFailed".GetLocalizedFormat(ex.Message);
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

            await _channelImages.ExportDngChannelsAsync(folder, _lastResult, BuildChannelAssignment(), SelectedAlignmentMode, SelectedDngExportMode);
            StatusText = "Scan_Runtime_StatusDngExported".GetLocalizedFormat(folder.Path);
        }
        catch (Exception ex)
        {
            StatusText = "Scan_Runtime_StatusDngExportFailed".GetLocalizedFormat(ex.Message);
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
            SelectedConfigProfileName = imported.ProfileName;
            _selectedConfigAcquisitionSettings = imported.AcquisitionSettings?.Normalize();

            if (_selectedConfigAcquisitionSettings is not null)
                ApplyAcquisitionSettingsToInputs(_selectedConfigAcquisitionSettings);

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
        CurrentPassText = "Scan_Runtime_CurrentPassProgress".GetLocalizedFormat(progress.CurrentPass, progress.TotalPasses, ScanRuntimeMessageLocalizer.LocalizeScanWorkflowStage(progress.Stage));
        CurrentLedText = "Scan_Runtime_CurrentLedProgress".GetLocalizedFormat(progress.LedChannelIndex + 1);
        CurrentDirectionText = "Scan_Runtime_CurrentDirectionProgress".GetLocalizedFormat(GetDirectionDisplayName(progress.DirectionPositive ? ForwardDirection : ReverseDirection));
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
            PreviewPlaceholderText = "Scan_Runtime_PreviewPlaceholderNextScan".GetLocalizedFormat(GetPreviewModeDisplayName(SelectedPreviewMode));
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

            if (_channelImages.TryBuildRgbComposite(_lastResult, BuildChannelAssignment(), colorManagement, SelectedAlignmentMode, PreviewImage, out var frame, out var error) && frame is not null)
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

        if (!TryParsePreviewChannelIndex(out var channelIndex) || _lastResult.Passes.Count <= channelIndex)
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
        ChannelMappingSummaryText = "Scan_Runtime_ChannelMappingSummary".GetLocalizedFormat(GetChannelRoleDisplayName(SelectedChannel1Role), GetChannelRoleDisplayName(SelectedChannel2Role), GetChannelRoleDisplayName(SelectedChannel3Role), GetChannelRoleDisplayName(SelectedChannel4Role));
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

        if (!TryParseLedLevel(Led1Level, "Scan_Runtime_FieldLed1Level".GetLocalized(), out var led1, out error)
            || !TryParseLedLevel(Led2Level, "Scan_Runtime_FieldLed2Level".GetLocalized(), out var led2, out error)
            || !TryParseLedLevel(Led3Level, "Scan_Runtime_FieldLed3Level".GetLocalized(), out var led3, out error)
            || !TryParseLedLevel(Led4Level, "Scan_Runtime_FieldLed4Level".GetLocalized(), out var led4, out error))
        {
            return false;
        }

        if (_loadedSnapshot is null || _loadedSysClockKhz < ScanDebugConstants.MinSysClockKhz)
        {
            error = "Scan_Runtime_ErrorParametersNotLoaded".GetLocalized();
            return false;
        }

        var fallbackSnapshot = _loadedSnapshot;
        var passRoles = new[] { SelectedChannel1Role, SelectedChannel2Role, SelectedChannel3Role, SelectedChannel4Role };
        var passProfiles = passRoles
            .Select(role => _channelProfiles.TryGetProfile(role, out var profile) ? profile.Parameters : fallbackSnapshot)
            .ToArray();

        request = new ScanWorkflowRequest(
            rows,
            IsWarmUpEnabled,
            new[] { led1, led2, led3, led4 },
            passRoles,
            passProfiles,
            motorId,
            intervalUs,
            string.Equals(SelectedStartingDirection, ForwardDirection, StringComparison.OrdinalIgnoreCase),
            IsAlternateMotorDirectionEnabled,
            _loadedExposureTicks,
            _loadedSysClockKhz,
            BuildWorkflowAcquisitionSettings(intervalUs, led1, led2, led3, led4));

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
        Led1Level = normalized.Led1Level.ToString(CultureInfo.InvariantCulture);
        Led2Level = normalized.Led2Level.ToString(CultureInfo.InvariantCulture);
        Led3Level = normalized.Led3Level.ToString(CultureInfo.InvariantCulture);
        Led4Level = normalized.Led4Level.ToString(CultureInfo.InvariantCulture);
        SelectedChannel1Role = NormalizeChannelRoleFromProfile(normalized.Led1ChannelColor, "Blue");
        SelectedChannel2Role = NormalizeChannelRoleFromProfile(normalized.Led2ChannelColor, "White");
        SelectedChannel3Role = NormalizeChannelRoleFromProfile(normalized.Led3ChannelColor, "Red");
        SelectedChannel4Role = NormalizeChannelRoleFromProfile(normalized.Led4ChannelColor, "Green");
    }

    private string NormalizeChannelRoleFromProfile(string? channelColor, string fallback)
        => ChannelRoleOptions.FirstOrDefault(option => string.Equals(option, channelColor, StringComparison.OrdinalIgnoreCase)) ?? fallback;

    private bool TryGetEffectiveMotorIntervalUs(ScanMotorMechanicalSettings motorSettings, out uint intervalUs)
    {
        intervalUs = 0;

        if (_isMotorDistanceDerivedFromInterval)
            return uint.TryParse(MotorIntervalUs, NumberStyles.Integer, CultureInfo.InvariantCulture, out intervalUs)
                && intervalUs >= ScanDebugConstants.MotionMinIntervalUs;

        if (_loadedSysClockKhz < ScanDebugConstants.MinSysClockKhz)
            return false;

        if (!TryParseDisplayedMotorDistanceMillimeters(MotorDistancePerLineValue, MotorDistancePerLineUnit, motorSettings, out var lineDistanceMm))
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
        if (!TryFormatMotorDistanceDisplayValue(lineDistanceMm, MotorDistancePerLineUnit, _deviceSettings.Settings.GetMotorSettings(motorId), out var displayValue))
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

    private static string NormalizeMotorDistanceUnit(string? unit)
        => unit?.Trim().ToLowerInvariant() switch
        {
            MotorDistanceUnitSteps => MotorDistanceUnitSteps,
            MotorDistanceUnitMicrometers => MotorDistanceUnitMicrometers,
            _ => MotorDistanceUnitMillimeters
        };

    private static bool TryParseDisplayedMotorDistanceMillimeters(string valueText, string unit, ScanMotorMechanicalSettings motorSettings, out double lineDistanceMm)
    {
        lineDistanceMm = 0.0;
        if (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || parsed <= 0.0)
        {
            return false;
        }

        lineDistanceMm = NormalizeMotorDistanceUnit(unit) switch
        {
            MotorDistanceUnitSteps => parsed / Math.Max(ScanTimingMath.ComputeMotorStepsPerMillimeter(motorSettings), double.Epsilon),
            MotorDistanceUnitMicrometers => parsed / 1000.0,
            _ => parsed
        };

        return double.IsFinite(lineDistanceMm) && lineDistanceMm > 0.0;
    }

    private static bool TryFormatMotorDistanceDisplayValue(double lineDistanceMm, string unit, ScanMotorMechanicalSettings motorSettings, out string valueText)
    {
        valueText = string.Empty;
        if (!double.IsFinite(lineDistanceMm) || lineDistanceMm <= 0.0)
            return false;

        var converted = NormalizeMotorDistanceUnit(unit) switch
        {
            MotorDistanceUnitSteps => lineDistanceMm * ScanTimingMath.ComputeMotorStepsPerMillimeter(motorSettings),
            MotorDistanceUnitMicrometers => lineDistanceMm * 1000.0,
            _ => lineDistanceMm
        };

        if (!double.IsFinite(converted) || converted <= 0.0)
            return false;

        valueText = converted.ToString("0.#########", CultureInfo.InvariantCulture);
        return true;
    }

    private ScanFilmAcquisitionSettings BuildWorkflowAcquisitionSettings(uint motorIntervalUs, ushort led1, ushort led2, ushort led3, ushort led4)
    {
        var source = _selectedConfigAcquisitionSettings?.Normalize() ?? ScanFilmAcquisitionSettings.CreateDefault();
        return new ScanFilmAcquisitionSettings(
            led1,
            led2,
            led3,
            led4,
            source.SteadyMask,
            source.SyncMask,
            source.Led1PulseClock,
            source.Led2PulseClock,
            source.Led3PulseClock,
            source.Led4PulseClock,
            motorIntervalUs).Normalize();
    }

    private static bool TryParseLedLevel(string text, string fieldName, out ushort value, out string error)
    {
        if (!ushort.TryParse(text, out value))
        {
            error = "Shared_Runtime_ErrorIntegerRange0To65535".GetLocalizedFormat(fieldName);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool CanRunExtendedScan()
        => _transferSettings.Settings.ReadMode == ScanBulkInReadMode.MultiBuffered && _transferSettings.Settings.RawIoEnabled;

    private ScanChannelAssignment BuildChannelAssignment()
        => new(SelectedChannel1Role, SelectedChannel2Role, SelectedChannel3Role, SelectedChannel4Role, IsChannel1Reversed, IsChannel2Reversed, IsChannel3Reversed, IsChannel4Reversed);

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
            || !TryParseColorDouble(OutputGamma, "Scan_Runtime_FieldOutputGamma".GetLocalized(), out var outputGamma, out error))
        {
            return false;
        }

        options = new ScanColorManagementOptions(IsColorManagementEnabled, redWavelength, greenWavelength, blueWavelength, outputGamma);
        error = string.Empty;
        return true;
    }

    private static bool TryParseColorDouble(string text, string fieldName, out double value, out string error)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            error = "Shared_Runtime_ErrorNumber".GetLocalizedFormat(fieldName);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string FormatColorDouble(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string GetDirectionDisplayName(string direction)
        => string.Equals(direction, ForwardDirection, StringComparison.OrdinalIgnoreCase)
            ? "Scan_Runtime_DirectionForward".GetLocalized()
            : "Scan_Runtime_DirectionReverse".GetLocalized();

    private static string GetPreviewModeDisplayName(string mode)
        => mode switch
        {
            "RGB Composite" => "Scan_Runtime_PreviewModeRgbComposite".GetLocalized(),
            "Raw Channel 1" => "Scan_Runtime_PreviewModeRawChannel".GetLocalizedFormat(1),
            "Raw Channel 2" => "Scan_Runtime_PreviewModeRawChannel".GetLocalizedFormat(2),
            "Raw Channel 3" => "Scan_Runtime_PreviewModeRawChannel".GetLocalizedFormat(3),
            "Raw Channel 4" => "Scan_Runtime_PreviewModeRawChannel".GetLocalizedFormat(4),
            _ => mode
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

    private bool TryParsePreviewChannelIndex(out int channelIndex)
    {
        channelIndex = -1;
        if (!SelectedPreviewMode.StartsWith("Raw Channel ", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(SelectedPreviewMode[12..], out var oneBasedIndex)
               && (channelIndex = oneBasedIndex - 1) >= 0
               && channelIndex < ScanDebugConstants.IlluminationChannelCount;
    }

    public async Task CleanupAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _scanCts?.Cancel();

        await _session.DisposeAsync();
        _usbUsageCoordinator.SetScanDebugInUse(false);
        _session.TargetsChanged -= OnSessionTargetsChanged;
        IsConnected = false;
        IsConnecting = false;
        IsRunning = false;
        IsOutputAvailable = false;
        PreviewImage = null;
    }
}
