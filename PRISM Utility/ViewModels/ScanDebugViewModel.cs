using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Models;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PRISM_Utility.ViewModels;

public sealed class ScanCalibrationPromptRequest
{
    public ScanCalibrationPromptRequest(ScanCalibrationPrompt prompt)
    {
        Prompt = prompt;
        CompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public ScanCalibrationPrompt Prompt { get; }

    public TaskCompletionSource<bool> CompletionSource { get; }
}

public partial class ScanDebugViewModel : ObservableRecipient
{
    private static readonly TimeSpan ParameterApplyDebounceWindow = TimeSpan.FromSeconds(1);

    private readonly IScanSessionService _session;
    private readonly IScanParameterService _parameters;
    private readonly IScanImageDecoder _imageDecoder;
    private readonly IScanAutoCalibrationService _autoCalibration;
    private readonly DispatcherQueue _dispatcher;

    private CancellationTokenSource? _scanCts;
    private byte[] _lineBuffer = Array.Empty<byte>();
    private bool _hasValidScanBuffer;
    private DateTime _lastApplyParametersAtUtc = DateTime.MinValue;
    private bool _isDisposed;
    private int _previewRows;

    public ObservableCollection<string> RowOptions { get; } = new() { "64", "128" };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    private string _selectedRows = "128";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    private bool _isWarmUpEnabled;

    [ObservableProperty]
    private bool _isPreviewEnabled = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportBufferCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    private bool _isDevicesPresent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    private bool _isConnecting;

    [ObservableProperty]
    private string _statusText = "Waiting for scanner devices...";

    [ObservableProperty]
    private WriteableBitmap? _previewImage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    private bool _isApplyingParameters;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    private bool _isAutoCalibrating;

    [ObservableProperty]
    private string _exposureTicks = string.Empty;

    [ObservableProperty]
    private string _adc1Offset = string.Empty;

    [ObservableProperty]
    private string _adc1Gain = string.Empty;

    [ObservableProperty]
    private string _adc2Offset = string.Empty;

    [ObservableProperty]
    private string _adc2Gain = string.Empty;

    [ObservableProperty]
    private string _exposureTimeDisplay = "Exposure time: -";

    [ObservableProperty]
    private string _adc1OffsetMvDisplay = "Offset amplitude: -";

    [ObservableProperty]
    private string _adc2OffsetMvDisplay = "Offset amplitude: -";

    [ObservableProperty]
    private string _adc1GainVvDisplay = "Gain: -";

    [ObservableProperty]
    private string _adc2GainVvDisplay = "Gain: -";

    public event EventHandler<ScanCalibrationPromptRequest>? CalibrationPromptRequested;

    public ScanDebugViewModel(IScanSessionService session, IScanParameterService parameters, IScanImageDecoder imageDecoder, IScanAutoCalibrationService autoCalibration)
    {
        _session = session;
        _parameters = parameters;
        _imageDecoder = imageDecoder;
        _autoCalibration = autoCalibration;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _session.TargetsChanged += OnSessionTargetsChanged;
        _session.RefreshTargets();
        UpdateComputedParameterDisplays();
        RefreshPreviewSelectionState();
        RefreshTargets();
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

    partial void OnIsWarmUpEnabledChanged(bool value)
        => _ = HandleWarmUpToggleChangedAsync(value);

    partial void OnSelectedRowsChanged(string value)
        => RefreshPreviewSelectionState();

    partial void OnIsPreviewEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPreviewToggleEnabled));
        OnPropertyChanged(nameof(IsPreviewEnabledForCurrentRows));

        if (!value)
            ClearPreview();
        else if (_hasValidScanBuffer && _previewRows > 0 && !IsPreviewForcedOffForRows(_previewRows))
            RenderPreview(_previewRows);
    }

    public bool IsPreviewToggleEnabled => !IsPreviewForcedOffForSelectedRows();

    public bool IsPreviewEnabledForCurrentRows => IsPreviewEnabled && !IsPreviewForcedOffForSelectedRows();

    private void OnSessionTargetsChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(RefreshTargets);

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
        IsConnected &&
        TryParseRequestedRows(out _);

    private bool CanStopScan() => IsRunning;

    private bool CanExportBuffer() => !IsRunning && _hasValidScanBuffer && _lineBuffer.Length > 0;

    private bool CanConnectDevices() => IsDevicesPresent && !IsConnected && !IsConnecting;

    private bool CanDisconnectDevices() => IsConnected && !IsConnecting;

    private bool CanApplyParameters() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsApplyingParameters &&
        !IsAutoCalibrating;

    private bool CanRunAutoCalibration() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsApplyingParameters &&
        !IsAutoCalibrating;

    [RelayCommand(CanExecute = nameof(CanExportBuffer))]
    private async Task ExportBuffer()
    {
        try
        {
            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("Binary file", new List<string> { ".bin" });
            picker.SuggestedFileName = BuildExportBufferFileName();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                StatusText = "Export canceled.";
                return;
            }

            await FileIO.WriteBytesAsync(file, _lineBuffer);
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
            StatusText = "Scanner sessions connected. Loading parameters...";

            var snapshot = await _parameters.LoadAsync(_session, _session.Session.ConnectionToken);
            ExposureTicks = snapshot.ExposureTicks.ToString();
            Adc1Offset = _parameters.FormatOffsetForInput(snapshot.Adc1Offset);
            Adc1Gain = snapshot.Adc1Gain.ToString();
            Adc2Offset = _parameters.FormatOffsetForInput(snapshot.Adc2Offset);
            Adc2Gain = snapshot.Adc2Gain.ToString();
            UpdateComputedParameterDisplays();

            StatusText = "Scanner sessions connected. Parameters loaded.";

            if (IsWarmUpEnabled)
            {
                var warmUpResult = await _session.SetWarmUpEnabledAsync(true, _session.Session.ConnectionToken);
                StatusText = warmUpResult.Success
                    ? "Scanner sessions connected. Parameters loaded. Warm-up enabled."
                    : $"Scanner sessions connected. Parameters loaded, but warm-up failed: {warmUpResult.Message}";
            }
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
            await _session.DisconnectAsync();
            IsConnected = false;
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

        if (!_parameters.TryParseInput(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, out var snapshot, out var parseError))
        {
            StatusText = parseError;
            return;
        }

        _lastApplyParametersAtUtc = now;

        IsApplyingParameters = true;
        try
        {
            StatusText = "Applying scan parameters...";
            await _parameters.ApplyAsync(_session, snapshot, _session.Session.ConnectionToken);
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
        if (!TryParseRequestedRows(out var rows))
        {
            StatusText = IsWarmUpEnabled
                ? "Rows must be a positive number when warm-up is enabled."
                : $"Rows must be a number in [1, {ScanDebugConstants.MaxRows}].";
            return;
        }

        if (!IsConnected)
        {
            StatusText = "Scanner not connected. Click Connect Devices first.";
            return;
        }

        _scanCts = new CancellationTokenSource();
        IsRunning = true;
        StatusText = "Starting scan...";

        try
        {
            var result = await RunScanAsync(rows, _scanCts.Token);

            StatusText = result.Message;
            if (!result.Success || result.ImageBytes is null)
                return;

            _lineBuffer = result.ImageBytes;
            _previewRows = rows;
            _hasValidScanBuffer = true;
            ExportBufferCommand.NotifyCanExecuteChanged();

            if (IsPreviewForcedOffForRows(rows))
            {
                ClearPreview();
                StatusText = $"{result.Message} Preview skipped automatically for scans over {ScanDebugConstants.MaxPreviewRows} rows.";
                return;
            }

            if (!IsPreviewEnabled)
            {
                ClearPreview();
                StatusText = $"{result.Message} Preview skipped.";
                return;
            }

            RenderPreview(rows);
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

        var result = await _session.StopScanAsync(CancellationToken.None);
        StatusText = result.Message;
        _scanCts?.Cancel();
    }

    private bool TryParseRows(out int rows)
    {
        if (!int.TryParse(SelectedRows, out rows))
            return false;

        return rows > 0 && rows <= ScanDebugConstants.MaxRows;
    }

    private bool TryParseRequestedRows(out int rows)
    {
        if (!int.TryParse(SelectedRows, out rows))
            return false;

        if (IsWarmUpEnabled)
            return rows > 0;

        return rows > 0 && rows <= ScanDebugConstants.MaxRows;
    }

    private async Task<ScanStartResult> RunScanAsync(int rows, CancellationToken ct)
    {
        if (!IsWarmUpEnabled || rows <= ScanDebugConstants.MaxRows)
        {
            return await _session.StartScanAsync(
                rows,
                ct,
                status => _dispatcher.TryEnqueue(() => StatusText = status),
                diagnostic => Debug.WriteLine(diagnostic));
        }

        return await _session.StartWarmUpSegmentedScanAsync(
            rows,
            ct,
            status => _dispatcher.TryEnqueue(() => StatusText = status),
            diagnostic => Debug.WriteLine(diagnostic));
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
            var result = await _session.SetWarmUpEnabledAsync(enabled, _session.Session.ConnectionToken);
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

        if (!_parameters.TryParseInput(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, out var snapshot, out var error))
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

    private void ApplySnapshotToInputs(ScanParameterSnapshot snapshot)
    {
        ExposureTicks = snapshot.ExposureTicks.ToString();
        Adc1Offset = _parameters.FormatOffsetForInput(snapshot.Adc1Offset);
        Adc1Gain = snapshot.Adc1Gain.ToString();
        Adc2Offset = _parameters.FormatOffsetForInput(snapshot.Adc2Offset);
        Adc2Gain = snapshot.Adc2Gain.ToString();
        UpdateComputedParameterDisplays();
    }

    private void ShowCalibrationFrame(byte[] imageBytes, int rows, string phase)
    {
        _lineBuffer = imageBytes;
        _previewRows = rows;
        _hasValidScanBuffer = true;
        ExportBufferCommand.NotifyCanExecuteChanged();

        if (IsPreviewForcedOffForRows(rows) || !IsPreviewEnabled)
        {
            ClearPreview();
            return;
        }

        RenderPreview(rows);
        StatusText = $"{phase}: preview updated.";
    }

    public bool TryGetPreviewSample16(int x, int y, out ushort sample)
    {
        sample = 0;

        if (!_hasValidScanBuffer || PreviewImage is null || _lineBuffer.Length == 0 || !IsPreviewEnabled)
            return false;

        if (x < 0 || y < 0 || x >= PreviewImage.PixelWidth || y >= PreviewImage.PixelHeight)
            return false;

        return _imageDecoder.TryGetSample16(_lineBuffer, _previewRows, x, y, out sample);
    }

    private string BuildExportBufferFileName()
    {
        var rowsText = int.TryParse(SelectedRows, out var rows) ? rows.ToString() : "unknown";
        return $"scan_{DateTime.Now:yyyyMMdd_HHmmss}_rows{rowsText}_bytes{_lineBuffer.Length}";
    }

    private void UpdateComputedParameterDisplays()
    {
        var displays = _parameters.BuildDisplays(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain);
        ExposureTimeDisplay = displays.ExposureTimeDisplay;
        Adc1OffsetMvDisplay = displays.Adc1OffsetMvDisplay;
        Adc2OffsetMvDisplay = displays.Adc2OffsetMvDisplay;
        Adc1GainVvDisplay = displays.Adc1GainVvDisplay;
        Adc2GainVvDisplay = displays.Adc2GainVvDisplay;
    }

    private void RenderPreview(int rows)
    {
        var previewWidth = _imageDecoder.GetDecodedPixelsPerLine();
        if (PreviewImage is null || PreviewImage.PixelWidth != previewWidth || PreviewImage.PixelHeight != rows)
            PreviewImage = new WriteableBitmap(previewWidth, rows);

        using var stream = PreviewImage.PixelBuffer.AsStream();
        _imageDecoder.DecodeToBgra(_lineBuffer, rows, stream);
        PreviewImage.Invalidate();
    }

    private void ClearPreview()
    {
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
        await _session.DisconnectAsync();
        _session.TargetsChanged -= OnSessionTargetsChanged;
        IsConnected = false;
        IsConnecting = false;
        IsRunning = false;
        IsApplyingParameters = false;
    }
}
