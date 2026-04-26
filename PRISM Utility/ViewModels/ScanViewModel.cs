using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Models;

namespace PRISM_Utility.ViewModels;

public partial class ScanViewModel : ObservableRecipient
{
    private const string ForwardDirection = "Forward";
    private const string ReverseDirection = "Reverse";
    private const double DefaultRedWavelengthNm = 630.0;
    private const double DefaultGreenWavelengthNm = 530.0;
    private const double DefaultBlueWavelengthNm = 470.0;
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
        MotorIntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString();
        Led1Level = "0";
        Led2Level = "0";
        Led3Level = "0";
        Led4Level = "0";
        Pass1DirectionText = ForwardDirection;
        Pass2DirectionText = ReverseDirection;
        Pass3DirectionText = ForwardDirection;
        Pass4DirectionText = ReverseDirection;
        StatusText = "Waiting for scanner devices 1D50:619C and 1D50:619D.";
        CurrentPassText = "Pass - of 4";
        CurrentLedText = "LED: -";
        CurrentDirectionText = "Direction: -";
        PreviewPlaceholderText = "Run a scan to generate RGB or raw channel preview output.";
        PreviewDescriptionText = "Preview mode: RGB Composite.";
        OutputSummaryText = "No scan outputs captured yet.";
        ComputedMotorSummaryText = "Motor step estimate unavailable until scanner parameters are loaded.";

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
        if (TryParseColorDouble(value, "Red wavelength", out var parsed, out _))
            OnColorManagementChanged(settings => settings with { RedWavelengthNm = parsed });
        else
            UpdatePreviewState();
    }

    partial void OnGreenWavelengthNmChanged(string value)
    {
        if (TryParseColorDouble(value, "Green wavelength", out var parsed, out _))
            OnColorManagementChanged(settings => settings with { GreenWavelengthNm = parsed });
        else
            UpdatePreviewState();
    }

    partial void OnBlueWavelengthNmChanged(string value)
    {
        if (TryParseColorDouble(value, "Blue wavelength", out var parsed, out _))
            OnColorManagementChanged(settings => settings with { BlueWavelengthNm = parsed });
        else
            UpdatePreviewState();
    }

    partial void OnOutputGammaChanged(string value)
    {
        if (TryParseColorDouble(value, "Output gamma", out var parsed, out _))
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
                ? "619C/619D detected. Click Connect Devices to open scanner sessions for Scan."
                : "Waiting for scanner devices 1D50:619C and 1D50:619D.";
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
            StatusText = "USB Debugging is active. Stop it before connecting the Scan page.";
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
                StatusText = result.Message;
                return;
            }

            IsConnected = true;
            _usbUsageCoordinator.SetScanDebugInUse(true);
            StatusText = "Scanner sessions connected. Loading scan state...";

            var statusNotes = new List<string>();

            try
            {
                var snapshot = await _parameters.LoadAsync(_session, _session.ConnectionToken);
                _loadedExposureTicks = snapshot.ExposureTicks;
                _loadedSysClockKhz = snapshot.SysClockKhz;
                _loadedSnapshot = snapshot;
                statusNotes.Add($"Parameters loaded (exp={snapshot.ExposureTicks}, sysclk={snapshot.SysClockKhz} kHz)");
            }
            catch (Exception ex)
            {
                _loadedSnapshot = null;
                _loadedExposureTicks = 0;
                _loadedSysClockKhz = 0;
                statusNotes.Add($"Parameter load unavailable: {ex.Message}");
            }

            try
            {
                var illumination = await _session.GetIlluminationStateAsync(_session.ConnectionToken);
                Led1Level = illumination.Led1Level.ToString();
                Led2Level = illumination.Led2Level.ToString();
                Led3Level = illumination.Led3Level.ToString();
                Led4Level = illumination.Led4Level.ToString();
                statusNotes.Add("Illumination levels loaded");
            }
            catch (Exception ex)
            {
                statusNotes.Add($"Illumination unavailable: {ex.Message}");
            }

            UpdateComputedMotorSummary();
            StatusText = statusNotes.Count > 0
                ? $"Scanner sessions connected. {string.Join(". ", statusNotes)}."
                : "Scanner sessions connected.";
        }
        catch (Exception ex)
        {
            await _session.DisconnectAsync();
            IsConnected = false;
            _usbUsageCoordinator.SetScanDebugInUse(false);
            StatusText = $"Connect failed: {ex.Message}";
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
            PreviewPlaceholderText = "Run a scan to generate RGB or raw channel preview output.";
            OutputSummaryText = "No scan outputs captured yet.";
            StatusText = IsDevicesPresent ? "Disconnected. Click Connect Devices to reconnect." : "Disconnected.";
            CurrentPassText = "Pass - of 4";
            CurrentLedText = "LED: -";
            CurrentDirectionText = "Direction: -";
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
            StatusText = $"Rows above {_session.SingleTransferMaxRows} require Multi-buffer bulk IN and Raw I/O, or enable Warm-up segmented mode.";
            return;
        }

        _scanCts = new CancellationTokenSource();
        IsRunning = true;
        IsOutputAvailable = false;
        _lastResult = null;
        OutputSummaryText = "Scan in progress...";

        try
        {
            var result = await _workflow.ExecuteAsync(
                _session,
                request,
                _scanCts.Token,
                progress => _dispatcher.TryEnqueue(() => ApplyProgress(progress)),
                status => _dispatcher.TryEnqueue(() => StatusText = status),
                diagnostic => _debugOutputMirror.Mirror("Scan.Diagnostic", diagnostic));

            _lastResult = result;
            IsOutputAvailable = true;
            OutputSummaryText = $"Captured {result.Passes.Count} pass(es), rows={result.Rows}, motor steps/pass={result.ComputedMotorStepsPerPass}, interval={result.MotorIntervalUs} us.";
            StatusText = "Scan completed.";
            UpdatePreviewState();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan canceled.";
            CurrentPassText = "Pass canceled";
            CurrentLedText = "LED: Idle";
            CurrentDirectionText = "Direction: Idle";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
            OutputSummaryText = "Scan failed before outputs were produced.";
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
        StatusText = result.Success ? "Stop requested." : result.Message;
    }

    private void MirrorOutput(string source, string message)
        => _debugOutputMirror.Mirror(source, message);

    [RelayCommand(CanExecute = nameof(CanSaveRgbImage))]
    private async Task SaveRgbImage()
    {
        if (_lastResult is null)
        {
            StatusText = "No scan result available for RGB save.";
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
                StatusText = "RGB save canceled.";
                return;
            }

            await _channelImages.SaveRgbImageAsync(file, frame);
            StatusText = $"RGB image saved: {file.Path}";
        }
        catch (Exception ex)
        {
            StatusText = $"RGB save failed: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportRawChannels))]
    private async Task ExportRawChannels()
    {
        if (_lastResult is null)
        {
            StatusText = "No scan result available for raw export.";
            return;
        }

        try
        {
            var folder = await _channelImages.PickRawExportFolderAsync();
            if (folder is null)
            {
                StatusText = "Raw export canceled.";
                return;
            }

            await _channelImages.ExportRawChannelsAsync(folder, _lastResult, BuildChannelAssignment());
            StatusText = $"Raw channel buffers exported to {folder.Path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Raw export failed: {ex.Message}";
        }
    }

    private void ApplyProgress(ScanWorkflowProgress progress)
    {
        CurrentPassText = $"Pass {progress.CurrentPass} of {progress.TotalPasses} - {progress.Stage}";
        CurrentLedText = $"LED: LED{progress.LedChannelIndex + 1}";
        CurrentDirectionText = $"Direction: {(progress.DirectionPositive ? ForwardDirection : ReverseDirection)}";
    }

    private void UpdatePassPlan()
    {
        Pass1DirectionText = GetDirectionForPass(0);
        Pass2DirectionText = GetDirectionForPass(1);
        Pass3DirectionText = GetDirectionForPass(2);
        Pass4DirectionText = GetDirectionForPass(3);

        if (!IsRunning && IsConnected)
            CurrentDirectionText = $"Direction: {Pass1DirectionText}";
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
            PreviewDescriptionText = "Preview is disabled.";
            PreviewPlaceholderText = "Preview disabled.";
            return;
        }

        if (_lastResult is null)
        {
            PreviewImage = null;
            PreviewDescriptionText = $"Preview mode: {SelectedPreviewMode}.";
            PreviewPlaceholderText = $"{SelectedPreviewMode} preview will appear here after the next scan.";
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
                PreviewDescriptionText = $"Preview mode: {SelectedPreviewMode}. Composite uses current channel-role mapping and {(IsColorManagementEnabled ? "spectral sRGB color management" : "sRGB gamma encoding")}.";
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
            PreviewDescriptionText = "Selected raw preview channel is unavailable.";
            PreviewPlaceholderText = PreviewDescriptionText;
            return;
        }

        var capture = _lastResult.Passes[channelIndex];
        if (_channelImages.TryBuildRawPreview(capture, PreviewImage, out var bitmap, out var rawError))
        {
            PreviewImage = bitmap;
            PreviewDescriptionText = $"Preview mode: {SelectedPreviewMode}. Showing grayscale raw buffer from pass {capture.PassIndex}.";
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
        ChannelMappingSummaryText = $"Channel 1 → {SelectedChannel1Role}{BuildReverseSuffix(IsChannel1Reversed)}, Channel 2 → {SelectedChannel2Role}{BuildReverseSuffix(IsChannel2Reversed)}, Channel 3 → {SelectedChannel3Role}{BuildReverseSuffix(IsChannel3Reversed)}, Channel 4 → {SelectedChannel4Role}{BuildReverseSuffix(IsChannel4Reversed)}.";
    }

    private void UpdateComputedMotorSummary()
    {
        if (!int.TryParse(SelectedRows, out var rows) || rows <= 0)
        {
            ComputedMotorSummaryText = "Motor step estimate unavailable until rows are valid.";
            return;
        }

        if (!uint.TryParse(MotorIntervalUs, out var intervalUs) || intervalUs < ScanDebugConstants.MotionMinIntervalUs)
        {
            ComputedMotorSummaryText = $"Motor interval must be at least {ScanDebugConstants.MotionMinIntervalUs} us.";
            return;
        }

        if (_loadedSysClockKhz < ScanDebugConstants.MinSysClockKhz)
        {
            ComputedMotorSummaryText = "Motor step estimate unavailable until scanner exposure/system clock parameters are loaded.";
            return;
        }

        var lineExposureNs = (45827.0 + (_loadedExposureTicks * 6.0)) * (1_000_000.0 / _loadedSysClockKhz);
        var scanDurationUs = (rows * lineExposureNs) / 1000.0;
        var computedSteps = (uint)Math.Max(1, (int)Math.Round(scanDurationUs / Math.Max(intervalUs, 1u), MidpointRounding.AwayFromZero));
        ComputedMotorSummaryText = $"Motor{GetMotorDisplayIndex()} transport estimate: {computedSteps} step(s) per pass at {intervalUs} us interval, based on current exposure/system clock and {rows} row(s).";
    }

    private bool TryBuildWorkflowRequest(out ScanWorkflowRequest request, out string error)
    {
        request = new ScanWorkflowRequest(0, false, Array.Empty<ushort>(), Array.Empty<string>(), Array.Empty<ScanParameterSnapshot>(), 0, 0, false, false, 0, 0);

        if (!int.TryParse(SelectedRows, out var rows) || rows <= 0)
        {
            error = "Rows must be a positive integer.";
            return false;
        }

        if (!TryParseSelectedMotor(out var motorId, out error))
            return false;

        if (!uint.TryParse(MotorIntervalUs, out var intervalUs) || intervalUs < ScanDebugConstants.MotionMinIntervalUs)
        {
            error = $"Motor interval must be an integer >= {ScanDebugConstants.MotionMinIntervalUs} us.";
            return false;
        }

        if (!TryParseLedLevel(Led1Level, "LED1 level", out var led1, out error)
            || !TryParseLedLevel(Led2Level, "LED2 level", out var led2, out error)
            || !TryParseLedLevel(Led3Level, "LED3 level", out var led3, out error)
            || !TryParseLedLevel(Led4Level, "LED4 level", out var led4, out error))
        {
            return false;
        }

        if (_loadedSnapshot is null || _loadedSysClockKhz < ScanDebugConstants.MinSysClockKhz)
        {
            error = "Scanner parameters are not loaded yet. Reconnect the scanner and try again.";
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
            _loadedSysClockKhz);

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
            error = $"Scan motor must be one of Motor1..Motor{ScanDebugConstants.MotionMotorCount}.";
            return false;
        }

        motorId = (byte)(displayIndex - 1);
        error = string.Empty;
        return true;
    }

    private int GetMotorDisplayIndex()
        => TryParseSelectedMotor(out var motorId, out _) ? motorId + 1 : 1;

    private static bool TryParseLedLevel(string text, string fieldName, out ushort value, out string error)
    {
        if (!ushort.TryParse(text, out value))
        {
            error = $"{fieldName} must be an integer in [0, 65535].";
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

        if (!TryParseColorDouble(RedWavelengthNm, "Red wavelength", out var redWavelength, out error)
            || !TryParseColorDouble(GreenWavelengthNm, "Green wavelength", out var greenWavelength, out error)
            || !TryParseColorDouble(BlueWavelengthNm, "Blue wavelength", out var blueWavelength, out error)
            || !TryParseColorDouble(OutputGamma, "Output gamma", out var outputGamma, out error))
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
            error = $"{fieldName} must be a number.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string FormatColorDouble(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string BuildReverseSuffix(bool reversed)
        => reversed ? " (reversed)" : string.Empty;

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
