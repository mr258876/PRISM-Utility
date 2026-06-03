using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Models;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;


public sealed record DialogRequest(string Title, string Content);

public partial class UsbDebugViewModel : ObservableRecipient, IDisposable
{
    private const string RawUsbOwnerOperation = "USB Debug raw diagnostics";
    private const string RawUsbOwnerIdPrefix = "usb-debug-raw";
    private const string ObserverIdPrefix = "usb-debug-observer";

    private readonly IUsbService _usb;
    private readonly IUsbUsageCoordinator _usbUsageCoordinator;
    private readonly IScannerDeviceSessionManager _scannerManager;
    private readonly IUiDispatcher _dispatcher;
    private readonly IDebugOutputMirrorService _debugOutputMirror;
    private readonly string _rawUsbOwnerId = $"{RawUsbOwnerIdPrefix}-{Guid.NewGuid():N}";
    private readonly ScannerSessionObserverPermission _observerPermission;

    public ObservableCollection<UsbDeviceDto> UsbDevices { get; } = new();
    public ObservableCollection<UsbConfigDto> BulkInConfigs { get; } = new();
    public ObservableCollection<UsbInterfaceDto> BulkInInterfaces { get; } = new();
    public ObservableCollection<UsbEndpointDto> BulkInEndpoints { get; } = new();

    public ObservableCollection<UsbConfigDto> BulkOutConfigs { get; } = new();
    public ObservableCollection<UsbInterfaceDto> BulkOutInterfaces { get; } = new();
    public ObservableCollection<UsbEndpointDto> BulkOutEndpoints { get; } = new();

    [ObservableProperty][NotifyCanExecuteChangedFor(nameof(StartBulkInCommand))] public partial UsbDeviceDto? SelectedBulkInUsbDevice { get; set; }
    [ObservableProperty][NotifyCanExecuteChangedFor(nameof(StartBulkInCommand))] public partial UsbConfigDto? SelectedBulkInConfig { get; set; }
    [ObservableProperty][NotifyCanExecuteChangedFor(nameof(StartBulkInCommand))] public partial UsbInterfaceDto? SelectedBulkInInterface { get; set; }
    [ObservableProperty][NotifyCanExecuteChangedFor(nameof(StartBulkInCommand))] public partial UsbEndpointDto? SelectedBulkInEndpoint { get; set; }
    [ObservableProperty] public partial string SelectedBulkInSize { get; set; }

    [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SendBulkOutCommand))] public partial UsbDeviceDto? SelectedBulkOutUsbDevice { get; set; }
    [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SendBulkOutCommand))] public partial UsbConfigDto? SelectedBulkOutConfig { get; set; }
    [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SendBulkOutCommand))] public partial UsbInterfaceDto? SelectedBulkOutInterface { get; set; }
    [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SendBulkOutCommand))] public partial UsbEndpointDto? SelectedBulkOutEndpoint { get; set; }
    [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SendBulkOutCommand))] public partial string BulkOutText { get; set; }
    [ObservableProperty] public partial bool IsBulkOutHexMode { get; set; }

    private CancellationTokenSource? _bulkInCts;
    private IUsbBulkDuplexSession? _bulkInSession;
    private UsbPipeSelection? _runningBulkInPipe;
    private byte? _runningBulkInOutEndpoint;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBulkInCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopBulkInCommand))]
    public partial bool IsBulkInRunning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopBulkInCommand))]
    public partial bool IsBulkInStopping { get; set; }

    [ObservableProperty] public partial string LogText { get; set; }
    [ObservableProperty] public partial string ObservationStatusText { get; set; }

    public event EventHandler<DialogRequest>? DialogRequested;

    private ScannerDeviceSessionSnapshot _scannerSnapshot;
    private IUsbUsageLease? _rawUsbLease;

    private void RequestDialog(string title, string content)
        => DialogRequested?.Invoke(this, new DialogRequest(title, content));

    public UsbDebugViewModel(
        IUsbService usb,
        IUsbUsageCoordinator usbUsageCoordinator,
        IScannerDeviceSessionManager scannerManager,
        IUiDispatcher dispatcher,
        IDebugOutputMirrorService debugOutputMirror)
    {
        _usb = usb;
        _usbUsageCoordinator = usbUsageCoordinator;
        _scannerManager = scannerManager;
        _dispatcher = dispatcher;
        _debugOutputMirror = debugOutputMirror;
        _scannerSnapshot = scannerManager.Snapshot;
        _observerPermission = scannerManager.GrantObserverPermission(
            $"{ObserverIdPrefix}-{Guid.NewGuid():N}",
            ScannerSessionObserverScope.SessionState | ScannerSessionObserverScope.DeviceCatalog | ScannerSessionObserverScope.Diagnostics,
            DateTimeOffset.UtcNow);
        SelectedBulkInSize = "4096";
        BulkOutText = string.Empty;
        LogText = string.Empty;
        ObservationStatusText = string.Empty;

        _usb.DevicesChanged += OnUsbDevicesChanged;
        _scannerManager.SnapshotChanged += OnScannerSnapshotChanged;
        RefreshDeviceList();
        UpdateObservationStatus();
    }

    private void OnScannerSnapshotChanged(object? sender, ScannerDeviceSessionSnapshot snapshot)
    {
        _scannerSnapshot = snapshot;
        UpdateObservationStatus();
        RefreshCommandStates();
    }

    private void OnUsbDevicesChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(RefreshDeviceList);

    private void RefreshDeviceList()
    {
        var oldId = SelectedBulkInUsbDevice?.Id;
        var oldBulkOutId = SelectedBulkOutUsbDevice?.Id;

        UsbDevices.Clear();
        foreach (var d in _usb.GetDevices()) UsbDevices.Add(d);

        SelectedBulkInUsbDevice =
            oldId is null
                ? UsbDevices.FirstOrDefault()
                : UsbDevices.FirstOrDefault(x => x.Id == oldId) ?? UsbDevices.FirstOrDefault();

        SelectedBulkOutUsbDevice =
            oldBulkOutId is null
                ? UsbDevices.FirstOrDefault()
                : UsbDevices.FirstOrDefault(x => x.Id == oldBulkOutId) ?? UsbDevices.FirstOrDefault();
    }

    partial void OnSelectedBulkInUsbDeviceChanged(UsbDeviceDto? value)
    {
        BulkInConfigs.Clear();
        BulkInInterfaces.Clear();
        BulkInEndpoints.Clear();

        if (value is null) return;

        foreach (var cfg in _usb.GetConfigs(value.Id)) BulkInConfigs.Add(cfg);

        SelectedBulkInConfig = BulkInConfigs.FirstOrDefault();
    }

    partial void OnSelectedBulkOutUsbDeviceChanged(UsbDeviceDto? value)
    {
        BulkOutConfigs.Clear();
        BulkOutInterfaces.Clear();
        BulkOutEndpoints.Clear();

        if (value is null) return;

        foreach (var cfg in _usb.GetConfigs(value.Id)) BulkOutConfigs.Add(cfg);

        SelectedBulkOutConfig = BulkOutConfigs.FirstOrDefault();
    }

    partial void OnSelectedBulkInConfigChanged(UsbConfigDto? value)
    {
        BulkInInterfaces.Clear();
        BulkInEndpoints.Clear();
        SelectedBulkInInterface = null;
        SelectedBulkInEndpoint = null;

        if (SelectedBulkInUsbDevice is null || value is null) return;

        foreach (var itf in _usb.GetInterfaces(SelectedBulkInUsbDevice.Id, value.ConfigId)) BulkInInterfaces.Add(itf);

        SelectedBulkInInterface = BulkInInterfaces.FirstOrDefault();
    }

    partial void OnSelectedBulkOutConfigChanged(UsbConfigDto? value)
    {
        BulkOutInterfaces.Clear();
        BulkOutEndpoints.Clear();
        SelectedBulkOutInterface = null;
        SelectedBulkOutEndpoint = null;

        if (SelectedBulkOutUsbDevice is null || value is null) return;

        foreach (var itf in _usb.GetInterfaces(SelectedBulkOutUsbDevice.Id, value.ConfigId)) BulkOutInterfaces.Add(itf);

        SelectedBulkOutInterface = BulkOutInterfaces.FirstOrDefault();
    }

    partial void OnSelectedBulkInInterfaceChanged(UsbInterfaceDto? value)
    {
        BulkInEndpoints.Clear();
        SelectedBulkInEndpoint = null;

        if (SelectedBulkInUsbDevice is null || SelectedBulkInConfig is null || value is null) return;

        // 接口下的端点集合 :contentReference[oaicite:15]{index=15}
        foreach (var ep in _usb.GetBulkInEndpoints(SelectedBulkInUsbDevice.Id, SelectedBulkInConfig.ConfigId, value.InterfaceId, value.AlternateId))
        {
            var item = ep;

            // 只给用户选 Bulk IN（避免选错导致读不到数据）
            if (item.IsIn && item.TransferType == "Bulk")
                BulkInEndpoints.Add(item);
        }

        SelectedBulkInEndpoint = BulkInEndpoints.FirstOrDefault();
    }

    partial void OnSelectedBulkOutInterfaceChanged(UsbInterfaceDto? value)
    {
        BulkOutEndpoints.Clear();
        SelectedBulkOutEndpoint = null;

        if (SelectedBulkOutUsbDevice is null || SelectedBulkOutConfig is null || value is null) return;

        foreach (var ep in _usb.GetBulkOutEndpoints(SelectedBulkOutUsbDevice.Id, SelectedBulkOutConfig.ConfigId, value.InterfaceId, value.AlternateId))
        {
            var item = ep;
            if (!item.IsIn && item.TransferType == "Bulk")
                BulkOutEndpoints.Add(item);
        }

        SelectedBulkOutEndpoint = BulkOutEndpoints.FirstOrDefault();
    }

    private bool CanStartBulkIn() =>
        SelectedBulkInUsbDevice is not null &&
        SelectedBulkInConfig is not null &&
        SelectedBulkInInterface is not null &&
        SelectedBulkInEndpoint is not null &&
        CanUseExclusiveRawCommands() &&
        !IsBulkInRunning;

    private bool CanStopBulkIn() => IsBulkInRunning && !IsBulkInStopping;

    private bool CanSendBulkOut() =>
        SelectedBulkOutUsbDevice is not null &&
        SelectedBulkOutConfig is not null &&
        SelectedBulkOutInterface is not null &&
        SelectedBulkOutEndpoint is not null &&
        CanUseExclusiveRawCommands() &&
        !string.IsNullOrWhiteSpace(BulkOutText);

    [RelayCommand(CanExecute = nameof(CanStartBulkIn))]
    private async Task StartBulkIn()
    {
        if (SelectedBulkInUsbDevice is null || SelectedBulkInConfig is null || SelectedBulkInInterface is null || SelectedBulkInEndpoint is null)
        {
            RequestDialog("Shared_Dialog_Error.Title".GetLocalized(), "UsbDebug_Runtime_BulkInMissingParams".GetLocalized());
            return;
        }

        if (!CanUseExclusiveRawCommands())
        {
            ShowReadOnlyObservationDialog();
            return;
        }

        if (!int.TryParse(SelectedBulkInSize, out var size) || size <= 0) return;

        var acquireResult = await TryAcquireRawDiagnosticLeaseAsync("Bulk IN");
        if (!acquireResult.Success || acquireResult.Lease is null)
        {
            ShowRawLeaseUnavailableFeedback(acquireResult);
            return;
        }

        _bulkInCts = new CancellationTokenSource();
        IProgress<(int transferred, byte[] data)> progress = new Progress<(int transferred, byte[] data)>(p =>
        {
            var hexLen = Math.Min(p.transferred, 64);
            var hex = BitConverter.ToString(p.data, 0, hexLen);
            AppendLog($"RX ({p.transferred}): {hex}");
        });

        _runningBulkInPipe = new UsbPipeSelection(
            SelectedBulkInUsbDevice.Id,
            SelectedBulkInConfig.ConfigId,
            SelectedBulkInInterface.InterfaceId,
            SelectedBulkInInterface.AlternateId,
            SelectedBulkInEndpoint.Address);

        try
        {
            _rawUsbLease = acquireResult.Lease;
            _runningBulkInOutEndpoint = ResolveRunningBulkInOutEndpoint();
            _bulkInSession = _usb.OpenBulkDuplexSession(
                SelectedBulkInUsbDevice.Id,
                SelectedBulkInConfig.ConfigId,
                SelectedBulkInInterface.InterfaceId,
                SelectedBulkInInterface.AlternateId,
                SelectedBulkInEndpoint.Address,
                _runningBulkInOutEndpoint);

            IsBulkInRunning = true;
            IsBulkInStopping = false;
            AppendLog("UsbDebug_Runtime_BulkInStarted".GetLocalizedOrFallback("Bulk IN started."));
            RefreshCommandStates();

            while (!_bulkInCts.Token.IsCancellationRequested)
            {
                var chunk = await _bulkInSession.ReadBulkInOnceAsync(size, 2000, _bulkInCts.Token);
                if (chunk.transferred > 0)
                    progress.Report(chunk);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppendLog("UsbDebug_Runtime_BulkInError".GetLocalizedFormatOrFallback("Bulk IN error: {0}", ex.Message));
        }
        finally
        {
            _bulkInSession?.Dispose();
            _bulkInSession = null;
            _runningBulkInPipe = null;
            _runningBulkInOutEndpoint = null;

            _bulkInCts?.Dispose();
            _bulkInCts = null;

            IsBulkInStopping = false;
            IsBulkInRunning = false;
            await ReleaseRawUsbLeaseAsync();
            AppendLog("UsbDebug_Runtime_BulkInStopped".GetLocalizedOrFallback("Bulk IN stopped."));
            RefreshCommandStates();
        }
    }


    [RelayCommand(CanExecute = nameof(CanStopBulkIn))]
    private void StopBulkIn()
    {
        IsBulkInStopping = true;
        _bulkInCts?.Cancel();
        AppendLog("UsbDebug_Runtime_BulkInStopping".GetLocalizedOrFallback("Stopping Bulk IN..."));
    }

    [RelayCommand(CanExecute = nameof(CanSendBulkOut))]
    private async Task SendBulkOut()
    {
        if (SelectedBulkOutUsbDevice is null || SelectedBulkOutConfig is null || SelectedBulkOutInterface is null || SelectedBulkOutEndpoint is null)
        {
            RequestDialog("Shared_Dialog_Error.Title".GetLocalized(), "UsbDebug_Runtime_BulkOutMissingParams".GetLocalized());
            return;
        }

        if (string.IsNullOrWhiteSpace(BulkOutText))
        {
            RequestDialog("Shared_Dialog_Error.Title".GetLocalized(), "UsbDebug_Runtime_BulkOutPayloadRequired".GetLocalized());
            return;
        }

        if (!CanUseExclusiveRawCommands())
        {
            ShowReadOnlyObservationDialog();
            return;
        }

        try
        {
            var data = IsBulkOutHexMode
                ? ParseBulkOutHexPayload(BulkOutText)
                : Encoding.UTF8.GetBytes(BulkOutText);

            var transferred = await SendBulkOutByBestPathAsync(data);

            var hexLen = Math.Min(transferred, 64);
            var hex = BitConverter.ToString(data, 0, hexLen);
            AppendLog($"TX ({transferred}): {hex}");
        }
        catch (Exception ex)
        {
            AppendLog("UsbDebug_Runtime_BulkOutError".GetLocalizedFormatOrFallback("Bulk OUT error: {0}", ex.Message));
            RequestDialog(
                "UsbDebug_Runtime_BulkOutFailed.Title".GetLocalizedOrFallback("Bulk OUT failed"),
                ex.Message);
        }
    }

    private static byte[] ParseBulkOutHexPayload(string text)
    {
        var tokens = Regex.Split(text.Trim(), "[\\s-]+", RegexOptions.CultureInvariant)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();

        if (tokens.Length == 0)
            throw new FormatException("UsbDebug_Runtime_NoHexBytesFound".GetLocalized());

        var bytes = new byte[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            var rawToken = tokens[i].Trim();
            var token = rawToken;

            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                token = token[2..];

            if (token.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                token = token[..^1];

            if (token.Length != 2)
                throw new FormatException("UsbDebug_Runtime_HexTokenLength".GetLocalizedFormat(i + 1));

            if (!byte.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out var b))
                throw new FormatException("UsbDebug_Runtime_HexTokenInvalid".GetLocalizedFormat(i + 1, rawToken));

            bytes[i] = b;
        }

        return bytes;
    }

    private async Task<int> SendBulkOutByBestPathAsync(byte[] data)
    {
        if (SelectedBulkOutUsbDevice is null || SelectedBulkOutConfig is null || SelectedBulkOutInterface is null || SelectedBulkOutEndpoint is null)
            throw new InvalidOperationException("UsbDebug_Runtime_BulkOutMissingParams".GetLocalized());

        var requested = new UsbPipeSelection(
            SelectedBulkOutUsbDevice.Id,
            SelectedBulkOutConfig.ConfigId,
            SelectedBulkOutInterface.InterfaceId,
            SelectedBulkOutInterface.AlternateId,
            SelectedBulkOutEndpoint.Address);

        if (IsBulkInRunning && _bulkInSession is not null && _runningBulkInPipe is not null && _runningBulkInOutEndpoint is not null)
        {
            if (IsSameInterface(_runningBulkInPipe, requested) && _runningBulkInOutEndpoint == requested.EndpointAddress)
                return await _bulkInSession.WriteBulkOutAsync(data, 3000, CancellationToken.None);
        }

        var acquireResult = await TryAcquireRawDiagnosticLeaseAsync("Bulk OUT");
        if (!acquireResult.Success || acquireResult.Lease is null)
            throw new InvalidOperationException(BuildRawLeaseUnavailableMessage(acquireResult));

        try
        {
            using var session = _usb.OpenBulkDuplexSession(
                requested.DeviceId,
                requested.ConfigId,
                requested.InterfaceId,
                requested.AltId,
                null,
                requested.EndpointAddress);
            return await session.WriteBulkOutAsync(data, 3000, CancellationToken.None);
        }
        finally
        {
            if (!ReferenceEquals(acquireResult.Lease, _rawUsbLease))
                await acquireResult.Lease.ReleaseAsync();
        }
    }

    private bool CanUseExclusiveRawCommands()
        => _scannerSnapshot.ActiveOwner is null;

    private ValueTask<UsbUsageLeaseAcquireResult> TryAcquireRawDiagnosticLeaseAsync(string operation)
    {
        if (_rawUsbLease is not null)
        {
            return ValueTask.FromResult(new UsbUsageLeaseAcquireResult(
                true,
                _rawUsbLease,
                _usbUsageCoordinator.ActiveLease,
                string.Empty));
        }

        return _usbUsageCoordinator.TryAcquireLeaseAsync(_rawUsbOwnerId, UsbUsageOwnerType.RawUsb, $"{RawUsbOwnerOperation}: {operation}");
    }

    private async Task ReleaseRawUsbLeaseAsync()
    {
        if (_rawUsbLease is null)
            return;

        var lease = _rawUsbLease;
        _rawUsbLease = null;
        await lease.ReleaseAsync();
    }

    private void UpdateObservationStatus()
    {
        ObservationStatusText = BuildObservationStatusText();
    }

    private string BuildObservationStatusText()
    {
        if (_scannerSnapshot.ActiveOwner is not null && _observerPermission.Allows(ScannerSessionObserverScope.SessionState))
        {
            return "UsbDebug_ObservationStatus_ReadOnly".GetLocalizedOrFallback(
                "Read-only observation mode: another scanner workflow owns the device. You can inspect session state, logs, and device information here, but Bulk IN/OUT is unavailable until that session disconnects.");
        }

        if (_rawUsbLease is not null || IsBulkInRunning)
        {
            return "UsbDebug_ObservationStatus_ExclusiveActive".GetLocalizedOrFallback(
                "Exclusive raw diagnostics mode is active. USB Debug currently owns the raw USB lease and can use Bulk IN/OUT.");
        }

        return "UsbDebug_ObservationStatus_ExclusiveAvailable".GetLocalizedOrFallback(
            "Exclusive raw diagnostics are available while no scanner workflow owns the device. Bulk IN/OUT will claim the raw USB lease on demand.");
    }

    private void RefreshCommandStates()
    {
        StartBulkInCommand.NotifyCanExecuteChanged();
        SendBulkOutCommand.NotifyCanExecuteChanged();
        StopBulkInCommand.NotifyCanExecuteChanged();
    }

    private void ShowReadOnlyObservationDialog()
    {
        RequestDialog(
            "Shared_Dialog_UsbBusy.Title".GetLocalizedOrFallback("USB busy"),
            "UsbDebug_Runtime_ReadOnlyObservationOnly.Content".GetLocalizedOrFallback(
                "A scanner session currently owns the device. USB Debug remains available for read-only observation of session state, logs, and device information, but raw Bulk IN/OUT commands are unavailable until that session disconnects."));
    }

    private void ShowRawLeaseUnavailableFeedback(UsbUsageLeaseAcquireResult acquireResult)
    {
        RequestDialog(
            "Shared_Dialog_UsbBusy.Title".GetLocalizedOrFallback("USB busy"),
            BuildRawLeaseUnavailableMessage(acquireResult));
    }

    private string BuildRawLeaseUnavailableMessage(UsbUsageLeaseAcquireResult acquireResult)
    {
        if (acquireResult.ActiveLease?.OwnerType == UsbUsageOwnerType.Scanner || _scannerSnapshot.ActiveOwner is not null)
        {
            return "UsbDebug_Runtime_ReadOnlyObservationOnly.Content".GetLocalizedOrFallback(
                "A scanner session currently owns the device. USB Debug remains available for read-only observation of session state, logs, and device information, but raw Bulk IN/OUT commands are unavailable until that session disconnects.");
        }

        if (acquireResult.ActiveLease is not null)
        {
            return "UsbDebug_Runtime_ExclusiveLeaseUnavailable.Content".GetLocalizedFormatOrFallback(
                "USB Debug could not acquire exclusive raw access because the device is already owned by {0} for {1}.",
                acquireResult.ActiveLease.OwnerId,
                acquireResult.ActiveLease.Operation);
        }

        return "UsbDebug_Runtime_ExclusiveLeaseUnavailableFallback.Content".GetLocalizedOrFallback(
            "USB Debug could not acquire exclusive raw access for this operation.");
    }

    private byte? ResolveRunningBulkInOutEndpoint()
    {
        if (SelectedBulkInUsbDevice is null || SelectedBulkInConfig is null || SelectedBulkInInterface is null)
            return null;

        if (SelectedBulkOutUsbDevice is null || SelectedBulkOutConfig is null || SelectedBulkOutInterface is null || SelectedBulkOutEndpoint is null)
            return null;

        if (SelectedBulkInUsbDevice.Id != SelectedBulkOutUsbDevice.Id)
            return null;
        if (SelectedBulkInConfig.ConfigId != SelectedBulkOutConfig.ConfigId)
            return null;
        if (SelectedBulkInInterface.InterfaceId != SelectedBulkOutInterface.InterfaceId)
            return null;
        if (SelectedBulkInInterface.AlternateId != SelectedBulkOutInterface.AlternateId)
            return null;

        return SelectedBulkOutEndpoint.Address;
    }

    private static bool IsSameInterface(UsbPipeSelection left, UsbPipeSelection right)
        => left.DeviceId == right.DeviceId &&
           left.ConfigId == right.ConfigId &&
           left.InterfaceId == right.InterfaceId &&
           left.AltId == right.AltId;

    private void AppendLog(string msg)
    {
        _debugOutputMirror.Mirror("UsbDebug.Log", msg);
        _dispatcher.TryEnqueue(() =>
        {
            LogText += $"[{DateTime.Now:HH:mm:ss}] {msg}\r\n";
        });
    }

    public void Dispose()
    {
        _bulkInCts?.Cancel();
        _bulkInSession?.Dispose();
        _rawUsbLease?.Dispose();
        _scannerManager.SnapshotChanged -= OnScannerSnapshotChanged;
        _usb.DevicesChanged -= OnUsbDevicesChanged;
    }

    private sealed record UsbPipeSelection(string DeviceId, byte ConfigId, byte InterfaceId, byte AltId, byte EndpointAddress);
}
