using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;
using PRISM_Utility.Models;

namespace PRISM_Utility.ViewModels;

public partial class ScanViewModel : ObservableRecipient
{
    private const string ForwardDirection = "Forward";
    private const string ReverseDirection = "Reverse";
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
    private bool _isDisposed;
    private bool _isLoadingColorManagementSettings;

    public ObservableCollection<string> RowOptions { get; } = new() { "64", "128", "256", "512", "1024", "2048", "4096", "8192" };
    public ObservableCollection<string> DirectionOptions { get; } = new() { ForwardDirection, ReverseDirection };
    public ObservableCollection<string> ChannelRoleOptions { get; } = new() { "Red", "Green", "Blue", "White", "IR", "Unused" };
    public ObservableCollection<string> PreviewModes { get; } = new() { "RGB Composite", "Raw Channel 1", "Raw Channel 2", "Raw Channel 3", "Raw Channel 4" };
    public ObservableCollection<string> MotorOptions { get; } = new() { "Motor1", "Motor2", "Motor3" };

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
    [NotifyCanExecuteChangedFor(nameof(ExportRawChannelsCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveRgbImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportRawChannelsCommand))]
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
        _colorManagementSettings = colorManagementSettings;
        _channelProfiles = channelProfiles;
        _usbUsageCoordinator = usbUsageCoordinator;
        _dispatcher = dispatcher;

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
        IsChannel1Reversed = false;
        IsChannel2Reversed = false;
        IsChannel3Reversed = false;
        IsChannel4Reversed = false;
        SelectedScanMotor = MotorOptions[Math.Min(1, MotorOptions.Count - 1)];
        SelectedConfigProfileName = "Scan_Runtime_ConfigProfileNotSelected".GetLocalized();
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

    partial void OnMotorIntervalUsChanged(string value)
        => UpdateComputedMotorSummary();

    partial void OnSelectedScanMotorChanged(string value)
        => UpdateComputedMotorSummary();

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
        if (TryParseColorDouble(value, "Scan_RedWavelengthTextBox/Header".GetLocalized(), out var parsed, out _))
            OnColorManagementChanged(settings => settings with { RedWavelengthNm = parsed });
        else
            UpdatePreviewState();
    }

    partial void OnGreenWavelengthNmChanged(string value)
    {
        if (TryParseColorDouble(value, "Scan_GreenWavelengthTextBox/Header".GetLocalized(), out var parsed, out _))
            OnColorManagementChanged(settings => settings with { GreenWavelengthNm = parsed });
        else
            UpdatePreviewState();
    }

    partial void OnBlueWavelengthNmChanged(string value)
    {
        if (TryParseColorDouble(value, "Scan_BlueWavelengthTextBox/Header".GetLocalized(), out var parsed, out _))
            OnColorManagementChanged(settings => settings with { BlueWavelengthNm = parsed });
        else
            UpdatePreviewState();
    }

    partial void OnOutputGammaChanged(string value)
    {
        if (TryParseColorDouble(value, "Scan_OutputGammaTextBox/Header".GetLocalized(), out var parsed, out _))
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
        UpdateChannelMappingSummary();
        UpdatePreviewState();
    }

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
    private bool CanExportRawChannels() => IsOutputAvailable && !IsRunning;

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
            OutputSummaryText = "Scan_Runtime_OutputSummaryCaptured".GetLocalizedFormat(result.Passes.Count, result.Rows, result.ComputedMotorStepsPerPass, result.MotorIntervalUs);
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

        if (!_channelImages.TryBuildRgbComposite(_lastResult, BuildChannelAssignment(), colorManagement, null, out var frame, out var error) || frame is null)
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

    [RelayCommand(CanExecute = nameof(CanExportRawChannels))]
    private async Task ExportRawChannels()
    {
        if (_lastResult is null)
        {
            StatusText = "Scan_Runtime_StatusNoRawResult".GetLocalized();
            return;
        }

        try
        {
            var folder = await _channelImages.PickRawExportFolderAsync();
            if (folder is null)
            {
                StatusText = "Scan_Runtime_StatusRawExportCanceled".GetLocalized();
                return;
            }

            await _channelImages.ExportRawChannelsAsync(folder, _lastResult, BuildChannelAssignment());
            StatusText = "Scan_Runtime_StatusRawExported".GetLocalizedFormat(folder.Path);
        }
        catch (Exception ex)
        {
            StatusText = "Scan_Runtime_StatusRawExportFailed".GetLocalizedFormat(ex.Message);
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

            if (_channelImages.TryBuildRgbComposite(_lastResult, BuildChannelAssignment(), colorManagement, PreviewImage, out var frame, out var error) && frame is not null)
            {
                PreviewImage = frame.Bitmap;
                PreviewDescriptionText = "Scan_Runtime_PreviewCompositeDescription".GetLocalizedFormat(GetPreviewModeDisplayName(SelectedPreviewMode), IsColorManagementEnabled ? "Scan_Runtime_PreviewCompositeModeSpectral".GetLocalized() : "Scan_Runtime_PreviewCompositeModeGamma".GetLocalized());
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
        var capture = _lastResult.Passes[channelIndex];
        var manuallyReverse = channelIndex < assignment.ReversedFlags.Count && assignment.ReversedFlags[channelIndex];
        if (_channelImages.TryBuildRawPreview(capture, manuallyReverse, PreviewImage, out var bitmap, out var rawError))
        {
            PreviewImage = bitmap;
            PreviewDescriptionText = "Scan_Runtime_PreviewRawDescription".GetLocalizedFormat(GetPreviewModeDisplayName(SelectedPreviewMode), capture.PassIndex);
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

        if (!uint.TryParse(MotorIntervalUs, out var intervalUs) || intervalUs < ScanDebugConstants.MotionMinIntervalUs)
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorIntervalMinimum".GetLocalizedFormat(ScanDebugConstants.MotionMinIntervalUs);
            return;
        }

        if (_loadedSysClockKhz < ScanDebugConstants.MinSysClockKhz)
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorUnavailableUntilParametersLoaded".GetLocalized();
            return;
        }

        var lineExposureNs = (45827.0 + (_loadedExposureTicks * 6.0)) * (1_000_000.0 / _loadedSysClockKhz);
        var scanDurationUs = (rows * lineExposureNs) / 1000.0;
        var computedSteps = (uint)Math.Max(1, (int)Math.Round(scanDurationUs / Math.Max(intervalUs, 1u), MidpointRounding.AwayFromZero));
        ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorSummary".GetLocalizedFormat(GetMotorDisplayIndex(), computedSteps, intervalUs, rows);
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

        if (!uint.TryParse(MotorIntervalUs, out var intervalUs) || intervalUs < ScanDebugConstants.MotionMinIntervalUs)
        {
            error = "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanDebugConstants.MotionMinIntervalUs);
            return false;
        }

        if (!TryParseLedLevel(Led1Level, "Scan_Led1LevelTextBox.Header".GetLocalized(), out var led1, out error)
            || !TryParseLedLevel(Led2Level, "Scan_Led2LevelTextBox.Header".GetLocalized(), out var led2, out error)
            || !TryParseLedLevel(Led3Level, "Scan_Led3LevelTextBox.Header".GetLocalized(), out var led3, out error)
            || !TryParseLedLevel(Led4Level, "Scan_Led4LevelTextBox.Header".GetLocalized(), out var led4, out error))
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
        Led1Level = normalized.Led1Level.ToString(CultureInfo.InvariantCulture);
        Led2Level = normalized.Led2Level.ToString(CultureInfo.InvariantCulture);
        Led3Level = normalized.Led3Level.ToString(CultureInfo.InvariantCulture);
        Led4Level = normalized.Led4Level.ToString(CultureInfo.InvariantCulture);
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

        if (!TryParseColorDouble(RedWavelengthNm, "Scan_RedWavelengthTextBox/Header".GetLocalized(), out var redWavelength, out error)
            || !TryParseColorDouble(GreenWavelengthNm, "Scan_GreenWavelengthTextBox/Header".GetLocalized(), out var greenWavelength, out error)
            || !TryParseColorDouble(BlueWavelengthNm, "Scan_BlueWavelengthTextBox/Header".GetLocalized(), out var blueWavelength, out error)
            || !TryParseColorDouble(OutputGamma, "Scan_OutputGammaTextBox/Header".GetLocalized(), out var outputGamma, out error))
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
