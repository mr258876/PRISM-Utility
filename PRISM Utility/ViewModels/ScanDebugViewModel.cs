using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Models;

namespace PRISM_Utility.ViewModels;

public sealed class ScanCalibrationPromptRequest
{
    public ScanCalibrationPromptRequest(ScanCalibrationPrompt prompt)
    {
        Prompt = prompt;
        CompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public ScanCalibrationPrompt Prompt
    {
        get;
    }

    public TaskCompletionSource<bool> CompletionSource
    {
        get;
    }
}

public sealed class ScanNoticeRequest
{
    public ScanNoticeRequest(string title, string content, string closeButtonText)
    {
        Title = title;
        Content = content;
        CloseButtonText = closeButtonText;
        CompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public string Title { get; }

    public string Content { get; }

    public string CloseButtonText { get; }

    public TaskCompletionSource CompletionSource { get; }
}

public partial class ScanDebugViewModel : ObservableRecipient
{
    private static readonly TimeSpan ParameterApplyDebounceWindow = TimeSpan.FromSeconds(1);
    private const double DefaultPreviewGamma = 2.2;
    private static readonly string[] IlluminationChannelLabels = { "LED1", "LED2", "LED3", "LED4" };

    private readonly IScanSessionService _session;
    private readonly IScanParameterService _parameters;
    private readonly IScanImageDecoder _imageDecoder;
    private readonly IScanPreviewPresenter _previewPresenter;
    private readonly IScanBufferExportService _bufferExportService;
    private readonly IScanAutoCalibrationService _autoCalibration;
    private readonly IScanTransferSettingsService _transferSettings;
    private readonly IUsbUsageCoordinator _usbUsageCoordinator;
    private readonly DispatcherQueue _dispatcher;

    private CancellationTokenSource? _scanCts;
    private byte[] _lineBuffer = Array.Empty<byte>();
    private bool _hasValidScanBuffer;
    private DateTime _lastApplyParametersAtUtc = DateTime.MinValue;
    private bool _isDisposed;
    private bool _isMultiBufferedBulkInEnabled;
    private bool _suppressWarmUpToggleCommand;
    private int _previewRows;

    public ObservableCollection<string> RowOptions { get; } = new() { "64", "128", "256", "512", "1024", "2048", "4096" };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    public partial string SelectedRows { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    public partial bool IsWarmUpEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsPreviewEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsContinuousScanEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsWaterfallEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsWaterfallCompressedEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsGammaCorrectionEnabled { get; set; }

    [ObservableProperty]
    public partial string PreviewGamma { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportBufferCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    public partial bool IsDevicesPresent { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    public partial bool IsConnecting { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial bool IsScanReadProgressVisible { get; set; }

    [ObservableProperty]
    public partial Visibility ScanReadProgressVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial double ScanReadProgressValue { get; set; }

    [ObservableProperty]
    public partial double ScanReadProgressMaximum { get; set; } = 1;

    partial void OnIsScanReadProgressVisibleChanged(bool value)
        => ScanReadProgressVisibility = value ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    public partial WriteableBitmap? PreviewImage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    public partial bool IsApplyingParameters { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    public partial bool IsAutoCalibrating { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    public partial bool IsApplyingIllumination { get; set; }

    [ObservableProperty]
    public partial string ExposureTicks { get; set; }

    [ObservableProperty]
    public partial string Adc1Offset { get; set; }

    [ObservableProperty]
    public partial string Adc1Gain { get; set; }

    [ObservableProperty]
    public partial string Adc2Offset { get; set; }

    [ObservableProperty]
    public partial string Adc2Gain { get; set; }

    [ObservableProperty]
    public partial string SysClockKhz { get; set; }

    [ObservableProperty]
    public partial string ExposureTimeDisplay { get; set; }

    [ObservableProperty]
    public partial string Adc1OffsetMvDisplay { get; set; }

    [ObservableProperty]
    public partial string Adc2OffsetMvDisplay { get; set; }

    [ObservableProperty]
    public partial string Adc1GainVvDisplay { get; set; }

    [ObservableProperty]
    public partial string Adc2GainVvDisplay { get; set; }

    [ObservableProperty]
    public partial string SysClockMhzDisplay { get; set; }

    [ObservableProperty]
    public partial string Led1Level { get; set; }

    [ObservableProperty]
    public partial string Led2Level { get; set; }

    [ObservableProperty]
    public partial string Led3Level { get; set; }

    [ObservableProperty]
    public partial string Led4Level { get; set; }

    [ObservableProperty]
    public partial bool IsLed1SteadyEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed2SteadyEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed3SteadyEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed4SteadyEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed1SyncEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed2SyncEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed3SyncEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed4SyncEnabled { get; set; }

    [ObservableProperty]
    public partial string Led1PulseClock { get; set; }

    [ObservableProperty]
    public partial string Led2PulseClock { get; set; }

    [ObservableProperty]
    public partial string Led3PulseClock { get; set; }

    [ObservableProperty]
    public partial string Led4PulseClock { get; set; }

    [ObservableProperty]
    public partial string IlluminationSummaryText { get; set; }

    public event EventHandler<ScanCalibrationPromptRequest>? CalibrationPromptRequested;

    public event EventHandler<ScanNoticeRequest>? NoticeRequested;

    public ScanDebugViewModel(IScanSessionService session, IScanParameterService parameters, IScanImageDecoder imageDecoder, IScanPreviewPresenter previewPresenter, IScanBufferExportService bufferExportService, IScanAutoCalibrationService autoCalibration, IScanTransferSettingsService transferSettings, IUsbUsageCoordinator usbUsageCoordinator)
    {
        _session = session;
        _parameters = parameters;
        _imageDecoder = imageDecoder;
        _previewPresenter = previewPresenter;
        _bufferExportService = bufferExportService;
        _autoCalibration = autoCalibration;
        _transferSettings = transferSettings;
        _usbUsageCoordinator = usbUsageCoordinator;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        SelectedRows = "128";
        IsPreviewEnabled = true;
        IsWaterfallCompressedEnabled = true;
        IsGammaCorrectionEnabled = true;
        PreviewGamma = DefaultPreviewGamma.ToString("0.0");
        StatusText = "Waiting for scanner devices...";
        ExposureTicks = string.Empty;
        Adc1Offset = string.Empty;
        Adc1Gain = string.Empty;
        Adc2Offset = string.Empty;
        Adc2Gain = string.Empty;
        SysClockKhz = string.Empty;
        ExposureTimeDisplay = "Exposure time: -";
        Adc1OffsetMvDisplay = "Offset amplitude: -";
        Adc2OffsetMvDisplay = "Offset amplitude: -";
        Adc1GainVvDisplay = "Gain: -";
        Adc2GainVvDisplay = "Gain: -";
        SysClockMhzDisplay = "System clock: -";
        Led1Level = "0";
        Led2Level = "0";
        Led3Level = "0";
        Led4Level = "0";
        Led1PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        Led2PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        Led3PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        Led4PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        IlluminationSummaryText = "Illumination state: -";

        _session.TargetsChanged += OnSessionTargetsChanged;
        _transferSettings.BulkInReadModeChanged += OnTransferSettingsChanged;
        _session.RefreshTargets();
        UpdateComputedParameterDisplays();
        RefreshPreviewSelectionState();
        RefreshTargets();
        _ = InitializeTransferSettingsAsync();
    }

    partial void OnExposureTicksChanged(string value)
        => UpdateComputedParameterDisplays();

    partial void OnAdc1OffsetChanged(string value)
        => UpdateComputedParameterDisplays();

    partial void OnAdc2OffsetChanged(string value)
        => UpdateComputedParameterDisplays();

    partial void OnAdc1GainChanged(string value)
        => UpdateComputedParameterDisplays();

    partial void OnAdc2GainChanged(string value)
        => UpdateComputedParameterDisplays();

    partial void OnSysClockKhzChanged(string value)
        => UpdateComputedParameterDisplays();

    partial void OnIsWarmUpEnabledChanged(bool value)
    {
        if (_suppressWarmUpToggleCommand)
            return;

        _ = HandleWarmUpToggleChangedAsync(value);
    }

    partial void OnSelectedRowsChanged(string value)
        => RefreshPreviewSelectionState();

    partial void OnIsRunningChanged(bool value)
        => OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));

    partial void OnIsApplyingIlluminationChanged(bool value)
        => OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));

    partial void OnIsPreviewEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPreviewToggleEnabled));
        OnPropertyChanged(nameof(IsPreviewEnabledForCurrentRows));

        if (!value)
            ClearPreview();
        else if (_hasValidScanBuffer && _previewRows > 0 && !IsPreviewForcedOffForRows(_previewRows))
            RenderPreview(_previewRows);
    }

    partial void OnIsWaterfallEnabledChanged(bool value)
    {
        _previewPresenter.Reset();

        if (!_hasValidScanBuffer || _previewRows <= 0 || !IsPreviewEnabled || IsPreviewForcedOffForRows(_previewRows))
            return;

        RenderPreview(_previewRows);
    }

    partial void OnIsWaterfallCompressedEnabledChanged(bool value)
    {
        _previewPresenter.Reset();

        if (!_hasValidScanBuffer || _previewRows <= 0 || !IsPreviewEnabled || !IsWaterfallEnabled || IsPreviewForcedOffForRows(_previewRows))
            return;

        RenderPreview(_previewRows);
    }

    partial void OnIsGammaCorrectionEnabledChanged(bool value)
    {
        if (!_hasValidScanBuffer || _previewRows <= 0 || !IsPreviewEnabled || IsPreviewForcedOffForRows(_previewRows))
            return;

        RenderPreview(_previewRows);
    }

    partial void OnPreviewGammaChanged(string value)
    {
        if (!_hasValidScanBuffer || _previewRows <= 0 || !IsPreviewEnabled || IsPreviewForcedOffForRows(_previewRows))
            return;

        if (!IsGammaCorrectionEnabled)
            return;

        RenderPreview(_previewRows);
    }

    public bool IsPreviewToggleEnabled => !IsPreviewForcedOffForSelectedRows();

    public bool IsPreviewEnabledForCurrentRows => IsPreviewEnabled && !IsPreviewForcedOffForSelectedRows();

    public bool AreScanAcquisitionSettingsEditable => !IsRunning && !IsApplyingIllumination;

    private void OnSessionTargetsChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(RefreshTargets);

    private void OnTransferSettingsChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(() =>
        {
            _isMultiBufferedBulkInEnabled = _transferSettings.Settings.ReadMode == ScanBulkInReadMode.MultiBuffered;
            StartScanCommand.NotifyCanExecuteChanged();
        });

    private void RefreshTargets()
    {
        IsDevicesPresent = _session.Targets.IsDevicesPresent;

        if (!IsConnected)
        {
            StatusText = IsDevicesPresent
                ? "619C/619D detected. Click Connect Devices to validate endpoints and open sessions."
                : "Waiting for scanner devices 1D50:619C and 1D50:619D.";
        }

        ConnectDevicesCommand.NotifyCanExecuteChanged();
        DisconnectDevicesCommand.NotifyCanExecuteChanged();
        StartScanCommand.NotifyCanExecuteChanged();
    }

    private bool CanStartScan() =>
        !IsRunning &&
        !IsApplyingIllumination &&
        IsConnected &&
        TryParseRequestedRows(out _);

    private bool CanStopScan() => IsRunning;

    private bool CanExportBuffer() => !IsRunning && _hasValidScanBuffer && _lineBuffer.Length > 0;

    private bool CanConnectDevices() => IsDevicesPresent && !IsConnected && !IsConnecting;

    private bool CanDisconnectDevices() => IsConnected && !IsConnecting && !IsApplyingIllumination;

    private bool CanApplyParameters() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsApplyingParameters &&
        !IsApplyingIllumination &&
        !IsAutoCalibrating;

    private bool CanManageIllumination() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsApplyingParameters &&
        !IsAutoCalibrating &&
        !IsApplyingIllumination;

    private bool CanRunAutoCalibration() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsApplyingParameters &&
        !IsApplyingIllumination &&
        !IsAutoCalibrating;

    [RelayCommand(CanExecute = nameof(CanExportBuffer))]
    private async Task ExportBuffer()
    {
        try
        {
            var file = await _bufferExportService.PickExportFileAsync(BuildExportBufferFileName());
            if (file is null)
            {
                StatusText = "Export canceled.";
                return;
            }

            await _bufferExportService.WriteBufferAsync(file, _lineBuffer);
            StatusText = $"Buffer exported: {_lineBuffer.Length} bytes -> {file.Path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnectDevices))]
    private async Task ConnectDevices()
    {
        if (_usbUsageCoordinator.IsUsbDebugInUse)
        {
            await RequestNoticeAsync(
                "USB busy",
                "Scan Debug is unavailable while USB Debugging is active. Stop USB Debugging first.",
                "OK");
            StatusText = "USB Debugging is active. Stop it before connecting Scan Debug.";
            return;
        }

        IsConnecting = true;
        try
        {
            var result = await _session.ConnectAsync(CancellationToken.None);
            if (!result.Success)
            {
                StatusText = result.Message;
                return;
            }

            IsConnected = true;
            _usbUsageCoordinator.SetScanDebugInUse(true);
            StatusText = "Scanner sessions connected. Loading parameters...";

            var statusNotes = new List<string>();

            try
            {
                var snapshot = await _parameters.LoadAsync(_session, _session.ConnectionToken);
                ExposureTicks = snapshot.ExposureTicks.ToString();
                Adc1Offset = _parameters.FormatOffsetForInput(snapshot.Adc1Offset);
                Adc1Gain = snapshot.Adc1Gain.ToString();
                Adc2Offset = _parameters.FormatOffsetForInput(snapshot.Adc2Offset);
                Adc2Gain = snapshot.Adc2Gain.ToString();
                SysClockKhz = snapshot.SysClockKhz.ToString();
                UpdateComputedParameterDisplays();
                statusNotes.Add("Parameters loaded");
            }
            catch (Exception ex)
            {
                statusNotes.Add($"Parameter load unavailable: {ex.Message}");
            }

            try
            {
                await LoadIlluminationStateAsync(_session.ConnectionToken);
                statusNotes.Add("Illumination state loaded");
            }
            catch (Exception ex)
            {
                ResetIlluminationInputs();
                statusNotes.Add($"Illumination unavailable: {ex.Message}");
            }

            if (IsWarmUpEnabled)
            {
                var warmUpResult = await _session.SetWarmUpEnabledAsync(true, _session.ConnectionToken);
                statusNotes.Add(warmUpResult.Success ? "Warm-up enabled" : $"Warm-up failed: {warmUpResult.Message}");
            }

            StatusText = statusNotes.Count > 0
                ? $"Scanner sessions connected. {string.Join(". ", statusNotes)}."
                : "Scanner sessions connected.";
        }
        catch (Exception ex)
        {
            await _session.DisconnectAsync();
            IsConnected = false;
            StatusText = $"Connect failed: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
            ConnectDevicesCommand.NotifyCanExecuteChanged();
            DisconnectDevicesCommand.NotifyCanExecuteChanged();
            StartScanCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnectDevices))]
    private async Task DisconnectDevices()
    {
        IsConnecting = true;
        try
        {
            _scanCts?.Cancel();

            if (IsWarmUpEnabled)
            {
                var warmUpResult = await _session.SetWarmUpEnabledAsync(false, _session.ConnectionToken);
                _suppressWarmUpToggleCommand = true;
                try
                {
                    IsWarmUpEnabled = false;
                }
                finally
                {
                    _suppressWarmUpToggleCommand = false;
                }

                if (!warmUpResult.Success)
                    StatusText = $"Warm-up disable before disconnect failed: {warmUpResult.Message}";
            }

            await _session.DisconnectAsync();
            IsConnected = false;
            _usbUsageCoordinator.SetScanDebugInUse(false);
            ResetIlluminationInputs();
            StatusText = IsDevicesPresent ? "Disconnected. Click Connect Devices to reconnect." : "Disconnected.";
        }
        finally
        {
            IsConnecting = false;
            ConnectDevicesCommand.NotifyCanExecuteChanged();
            DisconnectDevicesCommand.NotifyCanExecuteChanged();
            StartScanCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyParameters))]
    private async Task ApplyParameters()
    {
        if (!IsConnected)
        {
            StatusText = "Scanner not connected. Click Connect Devices first.";
            return;
        }

        var now = DateTime.UtcNow;
        if (now - _lastApplyParametersAtUtc < ParameterApplyDebounceWindow)
        {
            StatusText = "Apply ignored: please wait 1 second before next update.";
            return;
        }

        if (!_parameters.TryParseInput(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockKhz, out var snapshot, out var parseError))
        {
            StatusText = parseError;
            return;
        }

        _lastApplyParametersAtUtc = now;

        IsApplyingParameters = true;
        try
        {
            StatusText = "Applying scan parameters...";
            await _parameters.ApplyAsync(_session, snapshot, _session.ConnectionToken);
            StatusText = "Parameters updated successfully.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Parameter update canceled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Parameter update failed: {ex.Message}";
        }
        finally
        {
            IsApplyingParameters = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageIllumination))]
    private async Task RefreshIllumination()
    {
        if (!IsConnected)
        {
            StatusText = "Scanner not connected. Click Connect Devices first.";
            return;
        }

        IsApplyingIllumination = true;
        try
        {
            StatusText = "Refreshing illumination state...";
            await LoadIlluminationStateAsync(_session.ConnectionToken);
            StatusText = "Illumination state refreshed.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Illumination refresh canceled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Illumination refresh failed: {ex.Message}";
        }
        finally
        {
            IsApplyingIllumination = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageIllumination))]
    private async Task ApplyIllumination()
    {
        if (!IsConnected)
        {
            StatusText = "Scanner not connected. Click Connect Devices first.";
            return;
        }

        if (!TryBuildIlluminationRequest(out var request, out var error))
        {
            StatusText = error;
            return;
        }

        IsApplyingIllumination = true;
        try
        {
            StatusText = "Applying illumination settings...";

            var currentState = await _session.GetIlluminationStateAsync(_session.ConnectionToken);
            if (currentState.SyncMask != 0)
                await _session.ConfigureExposureLightingAsync(0, _session.ConnectionToken);

            if (currentState.SteadyMask != 0)
                await _session.SetSteadyIlluminationAsync(0, _session.ConnectionToken);

            await _session.SetIlluminationLevelsAsync(request.Led1Level, request.Led2Level, request.Led3Level, request.Led4Level, _session.ConnectionToken);
            await _session.SetSyncPulseClocksAsync(request.Led1PulseClock, request.Led2PulseClock, request.Led3PulseClock, request.Led4PulseClock, _session.ConnectionToken);
            await _session.SetSteadyIlluminationAsync(request.SteadyMask, _session.ConnectionToken);
            await _session.ConfigureExposureLightingAsync(request.SyncMask, _session.ConnectionToken);

            await LoadIlluminationStateAsync(_session.ConnectionToken);
            StatusText = "Illumination settings updated.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Illumination update canceled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Illumination update failed: {ex.Message}";
        }
        finally
        {
            IsApplyingIllumination = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunAutoCalibration))]
    private Task AutoBlackAdjust()
        => RunAutoCalibrationAsync((session, snapshot, prompt, status, applied, frame, ct) => _autoCalibration.AutoBlackAdjustAsync(session, snapshot, prompt, status, applied, frame, ct), "Auto black calibration completed.");

    [RelayCommand(CanExecute = nameof(CanRunAutoCalibration))]
    private Task AutoWhiteAdjust()
        => RunAutoCalibrationAsync((session, snapshot, prompt, status, applied, frame, ct) => _autoCalibration.AutoWhiteAdjustAsync(session, snapshot, prompt, status, applied, frame, ct), "Auto white calibration completed.");

    [RelayCommand(CanExecute = nameof(CanRunAutoCalibration))]
    private Task AutoCalibrate()
        => RunAutoCalibrationAsync((session, snapshot, prompt, status, applied, frame, ct) => _autoCalibration.AutoCalibrateAsync(session, snapshot, prompt, status, applied, frame, ct), "Auto calibration completed.");

    [RelayCommand(CanExecute = nameof(CanStartScan))]
    private async Task StartScan()
    {
        await Task.Yield();

        if (!TryParseRequestedRows(out var rows))
        {
            var singleTransferMaxRows = _session.SingleTransferMaxRows;
            StatusText = IsWarmUpEnabled
                ? "Rows must be a positive number when warm-up is enabled."
                : $"Rows must be a number in [1, {singleTransferMaxRows}].";
            return;
        }

        if (rows > _session.SingleTransferMaxRows && !CanRunExtendedScan())
        {
            await RequestNoticeAsync(
                "Rows limit exceeded",
                $"Rows greater than {_session.SingleTransferMaxRows} require both Multi-buffer bulk IN and Raw I/O to be enabled in Settings.",
                "OK");
            StatusText = $"Rows above {_session.SingleTransferMaxRows} require Multi-buffer bulk IN and Raw I/O.";
            return;
        }

        if (!IsConnected)
        {
            StatusText = "Scanner not connected. Click Connect Devices first.";
            return;
        }

        _scanCts = new CancellationTokenSource();
        IsRunning = true;
        IsScanReadProgressVisible = true;
        ScanReadProgressValue = 0;
        ScanReadProgressMaximum = Math.Max(1, rows * ScanDebugConstants.BytesPerLine);
        StatusText = IsContinuousScanEnabled ? "Starting continuous scan..." : "Starting scan...";

        try
        {
            if (IsContinuousScanEnabled)
                await RunContinuousScanLoopAsync(rows, _scanCts.Token);
            else
                await RunSingleScanAsync(rows, _scanCts.Token);
        }
        finally
        {
            IsRunning = false;
            IsScanReadProgressVisible = false;
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
        var result = await _session.StopScanAsync(CancellationToken.None);
        StatusText = result.Success ? "Stop requested." : result.Message;
    }

    private bool TryParseRows(out int rows)
    {
        if (!int.TryParse(SelectedRows, out rows))
            return false;

        if (CanRunExtendedScan())
            return rows > 0;

        return rows > 0 && rows <= _session.SingleTransferMaxRows;
    }

    private bool TryParseRequestedRows(out int rows)
    {
        if (!int.TryParse(SelectedRows, out rows))
            return false;

        if (IsWarmUpEnabled)
            return rows > 0;

        if (CanRunExtendedScan())
            return rows > 0;

        return rows > 0 && rows <= _session.SingleTransferMaxRows;
    }

    private async Task<ScanStartResult> RunScanAsync(int rows, CancellationToken ct)
    {
        if (CanRunExtendedScan() || !IsWarmUpEnabled || rows <= _session.SingleTransferMaxRows)
        {
            return await _session.StartScanAsync(
                rows,
                ct,
                status => _dispatcher.TryEnqueue(() => StatusText = status),
                diagnostic => Debug.WriteLine(diagnostic),
                ReportScanReadProgress);
        }

        return await _session.StartWarmUpSegmentedScanAsync(
            rows,
            ct,
            status => _dispatcher.TryEnqueue(() => StatusText = status),
            diagnostic => Debug.WriteLine(diagnostic),
            ReportScanReadProgress);
    }

    private async Task RunSingleScanAsync(int rows, CancellationToken ct)
    {
        var result = await RunScanAsync(rows, ct);

        StatusText = result.Message;
        if (!result.Success || result.ImageBytes is null)
            return;

        ApplyScanFrame(result.ImageBytes, rows, result.Message);
    }

    private async Task RunContinuousScanLoopAsync(int rows, CancellationToken ct)
    {
        var frameCount = 0;
        while (!ct.IsCancellationRequested)
        {
            var result = await RunScanAsync(rows, ct);
            if (!result.Success)
            {
                StatusText = result.Message;
                return;
            }

            if (result.ImageBytes is null)
            {
                StatusText = "Continuous scan completed without image data.";
                return;
            }

            frameCount++;
            ApplyScanFrame(result.ImageBytes, rows, $"Continuous preview updated ({frameCount}).");
        }
    }

    private void ApplyScanFrame(byte[] imageBytes, int rows, string successStatus)
    {
        _lineBuffer = imageBytes;
        _previewRows = rows;
        _hasValidScanBuffer = true;
        ExportBufferCommand.NotifyCanExecuteChanged();

        if (IsPreviewForcedOffForRows(rows))
        {
            ClearPreview();
            StatusText = $"{successStatus} Preview skipped automatically for scans over {ScanDebugConstants.MaxPreviewRows} rows.";
            return;
        }

        if (!IsPreviewEnabled)
        {
            ClearPreview();
            StatusText = $"{successStatus} Preview skipped.";
            return;
        }

        if (!RenderPreview(rows))
            return;

        StatusText = successStatus;
    }

    private async Task HandleWarmUpToggleChangedAsync(bool enabled)
    {
        if (_isDisposed)
            return;

        if (!IsConnected)
        {
            StatusText = enabled
                ? "Warm-up will be enabled after the scanner is connected."
                : "Warm-up disabled.";
            return;
        }

        try
        {
            var result = await _session.SetWarmUpEnabledAsync(enabled, _session.ConnectionToken);
            StatusText = result.Message;
        }
        catch (OperationCanceledException)
        {
            StatusText = enabled ? "Warm-up enable canceled." : "Warm-up disable canceled.";
        }
    }

    private async Task RunAutoCalibrationAsync(Func<IScanSessionService, ScanParameterSnapshot, Func<ScanCalibrationPrompt, Task<bool>>, Action<string>, Action<ScanParameterSnapshot>, Action<byte[], int, string>, CancellationToken, Task<ScanParameterSnapshot>> operation, string successMessage)
    {
        if (!IsConnected)
        {
            StatusText = "Scanner not connected. Click Connect Devices first.";
            return;
        }

        if (!_parameters.TryParseInput(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockKhz, out var snapshot, out var error))
        {
            StatusText = error;
            return;
        }

        IsAutoCalibrating = true;
        var calibrationCts = new CancellationTokenSource();
        try
        {
            StatusText = "Auto calibration started...";
            var calibrated = await operation(
                _session,
                snapshot,
                RequestCalibrationPromptAsync,
                status => _dispatcher.TryEnqueue(() => StatusText = status),
                applied => _dispatcher.TryEnqueue(() => ApplySnapshotToInputs(applied)),
                (imageBytes, rows, phase) => _dispatcher.TryEnqueue(() => ShowCalibrationFrame(imageBytes, rows, phase)),
                calibrationCts.Token);

            ApplySnapshotToInputs(calibrated);
            StatusText = successMessage;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Auto calibration canceled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Auto calibration failed: {ex.Message}";
        }
        finally
        {
            calibrationCts.Dispose();
            IsAutoCalibrating = false;
        }
    }

    private Task<bool> RequestCalibrationPromptAsync(ScanCalibrationPrompt prompt)
    {
        var request = new ScanCalibrationPromptRequest(prompt);
        CalibrationPromptRequested?.Invoke(this, request);
        return request.CompletionSource.Task;
    }

    private Task RequestNoticeAsync(string title, string content, string closeButtonText)
    {
        var request = new ScanNoticeRequest(title, content, closeButtonText);
        NoticeRequested?.Invoke(this, request);
        return request.CompletionSource.Task;
    }

    private bool CanRunExtendedScan() =>
        _transferSettings.Settings.ReadMode == ScanBulkInReadMode.MultiBuffered &&
        _transferSettings.Settings.RawIoEnabled;

    private void ReportScanReadProgress(int transferredBytes, int totalBytes)
        => _dispatcher.TryEnqueue(() =>
        {
            IsScanReadProgressVisible = true;
            ScanReadProgressMaximum = Math.Max(1, totalBytes);
            ScanReadProgressValue = Math.Clamp(transferredBytes, 0, totalBytes);
        });

    private async Task InitializeTransferSettingsAsync()
    {
        await _transferSettings.InitializeAsync();
        await EnqueueOnUiAsync(() =>
        {
            _isMultiBufferedBulkInEnabled = _transferSettings.Settings.ReadMode == ScanBulkInReadMode.MultiBuffered;
            StartScanCommand.NotifyCanExecuteChanged();
        });
    }

    private Task EnqueueOnUiAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }))
        {
            completion.SetException(new InvalidOperationException("Failed to dispatch work to the UI thread."));
        }

        return completion.Task;
    }

    private void ApplySnapshotToInputs(ScanParameterSnapshot snapshot)
    {
        ExposureTicks = snapshot.ExposureTicks.ToString();
        Adc1Offset = _parameters.FormatOffsetForInput(snapshot.Adc1Offset);
        Adc1Gain = snapshot.Adc1Gain.ToString();
        Adc2Offset = _parameters.FormatOffsetForInput(snapshot.Adc2Offset);
        Adc2Gain = snapshot.Adc2Gain.ToString();
        SysClockKhz = snapshot.SysClockKhz.ToString();
        UpdateComputedParameterDisplays();
    }

    private void ShowCalibrationFrame(byte[] imageBytes, int rows, string phase)
    {
        ApplyScanFrame(imageBytes, rows, $"{phase}: preview updated.");
    }

    public bool TryGetPreviewSample16(int x, int y, out ushort sample)
    {
        sample = 0;

        if (!_hasValidScanBuffer || PreviewImage is null || _lineBuffer.Length == 0 || !IsPreviewEnabled || IsWaterfallEnabled)
            return false;

        if (x < 0 || y < 0 || x >= PreviewImage.PixelWidth || y >= PreviewImage.PixelHeight)
            return false;

        return _imageDecoder.TryGetSample16(_lineBuffer, _previewRows, x, y, out sample);
    }

    private string BuildExportBufferFileName()
        => _bufferExportService.BuildExportBufferFileName(SelectedRows, _lineBuffer.Length, DateTimeOffset.Now);

    private void UpdateComputedParameterDisplays()
    {
        var displays = _parameters.BuildDisplays(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockKhz);
        ExposureTimeDisplay = displays.ExposureTimeDisplay;
        Adc1OffsetMvDisplay = displays.Adc1OffsetMvDisplay;
        Adc2OffsetMvDisplay = displays.Adc2OffsetMvDisplay;
        Adc1GainVvDisplay = displays.Adc1GainVvDisplay;
        Adc2GainVvDisplay = displays.Adc2GainVvDisplay;
        SysClockMhzDisplay = displays.SysClockMhzDisplay;
    }

    private async Task LoadIlluminationStateAsync(CancellationToken ct)
    {
        var state = await _session.GetIlluminationStateAsync(ct);
        ApplyIlluminationStateToInputs(state);
    }

    private void ApplyIlluminationStateToInputs(ScanIlluminationState state)
    {
        Led1Level = state.Led1Level.ToString();
        Led2Level = state.Led2Level.ToString();
        Led3Level = state.Led3Level.ToString();
        Led4Level = state.Led4Level.ToString();
        Led1PulseClock = state.Led1PulseClock.ToString();
        Led2PulseClock = state.Led2PulseClock.ToString();
        Led3PulseClock = state.Led3PulseClock.ToString();
        Led4PulseClock = state.Led4PulseClock.ToString();
        IsLed1SteadyEnabled = (state.SteadyMask & 0x01) != 0;
        IsLed2SteadyEnabled = (state.SteadyMask & 0x02) != 0;
        IsLed3SteadyEnabled = (state.SteadyMask & 0x04) != 0;
        IsLed4SteadyEnabled = (state.SteadyMask & 0x08) != 0;
        IsLed1SyncEnabled = (state.SyncMask & 0x01) != 0;
        IsLed2SyncEnabled = (state.SyncMask & 0x02) != 0;
        IsLed3SyncEnabled = (state.SyncMask & 0x04) != 0;
        IsLed4SyncEnabled = (state.SyncMask & 0x08) != 0;
        IlluminationSummaryText = BuildIlluminationSummary(state);
    }

    private void ResetIlluminationInputs()
    {
        Led1Level = "0";
        Led2Level = "0";
        Led3Level = "0";
        Led4Level = "0";
        Led1PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        Led2PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        Led3PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        Led4PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        IsLed1SteadyEnabled = false;
        IsLed2SteadyEnabled = false;
        IsLed3SteadyEnabled = false;
        IsLed4SteadyEnabled = false;
        IsLed1SyncEnabled = false;
        IsLed2SyncEnabled = false;
        IsLed3SyncEnabled = false;
        IsLed4SyncEnabled = false;
        IlluminationSummaryText = "Illumination state: -";
    }

    private bool TryBuildIlluminationRequest(out IlluminationRequest request, out string error)
    {
        request = new IlluminationRequest(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        if (!TryParseLedLevel(Led1Level, "LED1 Level", out var led1Level, out error)
            || !TryParseLedLevel(Led2Level, "LED2 Level", out var led2Level, out error)
            || !TryParseLedLevel(Led3Level, "LED3 Level", out var led3Level, out error)
            || !TryParseLedLevel(Led4Level, "LED4 Level", out var led4Level, out error)
            || !TryParsePulseClock(Led1PulseClock, "LED1 Pulse Clock", out var led1PulseClock, out error)
            || !TryParsePulseClock(Led2PulseClock, "LED2 Pulse Clock", out var led2PulseClock, out error)
            || !TryParsePulseClock(Led3PulseClock, "LED3 Pulse Clock", out var led3PulseClock, out error)
            || !TryParsePulseClock(Led4PulseClock, "LED4 Pulse Clock", out var led4PulseClock, out error))
        {
            return false;
        }

        var steadyMask = BuildMask(IsLed1SteadyEnabled, IsLed2SteadyEnabled, IsLed3SteadyEnabled, IsLed4SteadyEnabled);
        var syncMask = BuildMask(IsLed1SyncEnabled, IsLed2SyncEnabled, IsLed3SyncEnabled, IsLed4SyncEnabled);

        if ((steadyMask & syncMask) != 0)
        {
            error = "Steady and sync selections cannot overlap on the same LED channel.";
            return false;
        }

        if (!ValidateSyncPulse(syncMask, 0x01, led1PulseClock, "LED1 Pulse Clock", out error)
            || !ValidateSyncPulse(syncMask, 0x02, led2PulseClock, "LED2 Pulse Clock", out error)
            || !ValidateSyncPulse(syncMask, 0x04, led3PulseClock, "LED3 Pulse Clock", out error)
            || !ValidateSyncPulse(syncMask, 0x08, led4PulseClock, "LED4 Pulse Clock", out error))
        {
            return false;
        }

        request = new IlluminationRequest(
            led1Level,
            led2Level,
            led3Level,
            led4Level,
            steadyMask,
            syncMask,
            led1PulseClock,
            led2PulseClock,
            led3PulseClock,
            led4PulseClock);
        error = string.Empty;
        return true;
    }

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

    private static bool TryParsePulseClock(string text, string fieldName, out uint value, out string error)
    {
        if (!uint.TryParse(text, out value))
        {
            error = $"{fieldName} must be a non-negative integer.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateSyncPulse(byte syncMask, byte channelBit, uint pulseClock, string fieldName, out string error)
    {
        if ((syncMask & channelBit) != 0 && pulseClock < ScanDebugConstants.IlluminationMinSyncPulseClock)
        {
            error = $"{fieldName} must be at least {ScanDebugConstants.IlluminationMinSyncPulseClock} when sync is enabled for that channel.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static byte BuildMask(bool bit0, bool bit1, bool bit2, bool bit3)
        => (byte)((bit0 ? 0x01 : 0x00) |
                  (bit1 ? 0x02 : 0x00) |
                  (bit2 ? 0x04 : 0x00) |
                  (bit3 ? 0x08 : 0x00));

    private static string BuildIlluminationSummary(ScanIlluminationState state)
        => $"Illumination state: Steady={FormatMask(state.SteadyMask)}, Sync={FormatMask(state.SyncMask)}, Active={FormatMask(state.SyncActiveMask)}";

    private static string FormatMask(byte mask)
    {
        if ((mask & ScanDebugConstants.IlluminationValidMask) == 0)
            return "none";

        var labels = new List<string>(ScanDebugConstants.IlluminationChannelCount);
        for (var index = 0; index < ScanDebugConstants.IlluminationChannelCount; index++)
        {
            if (((mask >> index) & 0x01) != 0)
                labels.Add(IlluminationChannelLabels[index]);
        }

        return string.Join(", ", labels);
    }

    private sealed record IlluminationRequest(
        ushort Led1Level,
        ushort Led2Level,
        ushort Led3Level,
        ushort Led4Level,
        byte SteadyMask,
        byte SyncMask,
        uint Led1PulseClock,
        uint Led2PulseClock,
        uint Led3PulseClock,
        uint Led4PulseClock);

    private bool RenderPreview(int rows)
    {
        var gamma = 1.0;
        if (IsGammaCorrectionEnabled && !double.TryParse(PreviewGamma, out gamma))
            gamma = double.NaN;

        if (!_previewPresenter.TryRender(
                _lineBuffer,
                rows,
                new ScanPreviewRenderOptions(IsWaterfallEnabled, IsWaterfallCompressedEnabled, IsGammaCorrectionEnabled, gamma),
                PreviewImage,
                out var bitmap,
                out var error))
        {
            StatusText = error;
            return false;
        }

        PreviewImage = bitmap;
        return true;
    }

    private void ClearPreview()
    {
        _previewPresenter.Reset();
        PreviewImage = null;
    }

    private void RefreshPreviewSelectionState()
    {
        OnPropertyChanged(nameof(IsPreviewToggleEnabled));
        OnPropertyChanged(nameof(IsPreviewEnabledForCurrentRows));
    }

    private bool IsPreviewForcedOffForSelectedRows()
        => int.TryParse(SelectedRows, out var rows) && IsPreviewForcedOffForRows(rows);

    private static bool IsPreviewForcedOffForRows(int rows)
        => rows > ScanDebugConstants.MaxPreviewRows;

    public async Task CleanupAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _scanCts?.Cancel();

        if (IsConnected && IsWarmUpEnabled)
        {
            try
            {
                await _session.SetWarmUpEnabledAsync(false, CancellationToken.None);
                _suppressWarmUpToggleCommand = true;
                try
                {
                    IsWarmUpEnabled = false;
                }
                finally
                {
                    _suppressWarmUpToggleCommand = false;
                }
            }
            catch
            {
            }
        }

        await _session.DisconnectAsync();
        _usbUsageCoordinator.SetScanDebugInUse(false);
        _session.TargetsChanged -= OnSessionTargetsChanged;
        _transferSettings.BulkInReadModeChanged -= OnTransferSettingsChanged;
        IsConnected = false;
        IsConnecting = false;
        IsRunning = false;
        IsApplyingParameters = false;
        IsApplyingIllumination = false;
        ResetIlluminationInputs();
    }
}
