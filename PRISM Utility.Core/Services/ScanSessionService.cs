using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Contracts.Models;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public class ScanSessionService : IScanSessionService, IDisposable
{
    private readonly IUsbService _usb;
    private readonly IScanProtocolService _protocol;
    private readonly ScanAckChannel _ackChannel;
    private readonly ScanExecutionRunner _executionRunner;

    private UsbPipeSelection? _bulkInPipe;
    private UsbPipeSelection? _bulkOutPipe;
    private UsbPipeSelection? _bulkOutAckPipe;
    private IUsbBulkDuplexSession? _controlSession;
    private IUsbBulkDuplexSession? _imageSession;
    private CancellationTokenSource? _connectionCts;
    private int _singleTransferMaxBytes = ScanDebugConstants.ImageRingBufferSize;

    public event EventHandler? TargetsChanged;

    public ScanTargetState Targets { get; private set; } = new(false, null, null);

    public bool IsConnected { get; private set; }

    public int SingleTransferMaxRows => Math.Max(1, _singleTransferMaxBytes / ScanDebugConstants.BytesPerLine);

    public CancellationToken ConnectionToken
        => _connectionCts?.Token ?? CancellationToken.None;

    public ScanSessionService(IUsbService usb, IScanProtocolService protocol, IScanTransferSettingsService transferSettings)
    {
        _usb = usb;
        _protocol = protocol;
        _ackChannel = new ScanAckChannel(protocol);
        _executionRunner = new ScanExecutionRunner(protocol, transferSettings, _ackChannel);
        _usb.DevicesChanged += OnDevicesChanged;
        RefreshTargets();
    }

    public void RefreshTargets()
    {
        var devices = _usb.GetDevices();
        var bulkInDevice = devices.FirstOrDefault(d => d.Vid == ScanDebugConstants.BulkInVid && d.Pid == ScanDebugConstants.BulkInPid);
        var bulkOutDevice = devices.FirstOrDefault(d => d.Vid == ScanDebugConstants.BulkOutVid && d.Pid == ScanDebugConstants.BulkOutPid);
        Targets = new ScanTargetState(bulkInDevice is not null && bulkOutDevice is not null, bulkInDevice?.Id, bulkOutDevice?.Id);
        TargetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<ScanOperationResult> ConnectAsync(CancellationToken ct)
    {
        RefreshTargets();
        if (!Targets.IsDevicesPresent)
            return new ScanOperationResult(false, "619C/619D not detected.");

        var devices = _usb.GetDevices();
        var bulkInDevice = devices.FirstOrDefault(d => d.Id == Targets.BulkInDeviceId);
        var bulkOutDevice = devices.FirstOrDefault(d => d.Id == Targets.BulkOutDeviceId);
        if (bulkInDevice is null || bulkOutDevice is null)
            return new ScanOperationResult(false, "Device disappeared during connect.");

        _bulkInPipe = FindPipe(bulkInDevice, ScanDebugConstants.BulkInEndpoint, true);
        _bulkOutPipe = FindPipe(bulkOutDevice, ScanDebugConstants.BulkOutEndpoint, false);
        _bulkOutAckPipe = FindPipe(bulkOutDevice, ScanDebugConstants.BulkOutAckEndpoint, true);

        if (_bulkInPipe is null || _bulkOutPipe is null || _bulkOutAckPipe is null)
            return new ScanOperationResult(false, "Endpoint check failed. Need 619C IN 0x82 and 619D OUT 0x01/IN 0x81.");

        if (!IsSameInterface(_bulkOutPipe, _bulkOutAckPipe))
            return new ScanOperationResult(false, "Endpoint check failed: 619D OUT 0x01 and IN 0x81 must be on same interface.");

        try
        {
            _controlSession = _usb.OpenBulkDuplexSession(
                _bulkOutPipe.DeviceId,
                _bulkOutPipe.ConfigId,
                _bulkOutPipe.InterfaceId,
                _bulkOutPipe.AltId,
                _bulkOutAckPipe.EndpointAddress,
                _bulkOutPipe.EndpointAddress);

            _imageSession = _usb.OpenBulkDuplexSession(
                _bulkInPipe.DeviceId,
                _bulkInPipe.ConfigId,
                _bulkInPipe.InterfaceId,
                _bulkInPipe.AltId,
                _bulkInPipe.EndpointAddress,
                null);

            _singleTransferMaxBytes = ResolveSingleTransferMaxBytes(_imageSession, _bulkInPipe);

            _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _ackChannel.Start(_controlSession, _connectionCts.Token);
            IsConnected = true;
            return new ScanOperationResult(true, "Scanner sessions connected.");
        }
        catch (Exception ex)
        {
            await DisconnectAsync();
            return new ScanOperationResult(false, $"Connect failed: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        await DisconnectSessionsInternalAsync();
        IsConnected = false;
    }

    public async Task<ScanOperationResult> SetWarmUpEnabledAsync(bool enabled, CancellationToken ct)
    {
        if (!IsConnected || _controlSession is null)
            return new ScanOperationResult(false, "Scanner not connected. Click Connect Devices first.");

        try
        {
            if (enabled)
            {
                var warmUpFrame = await SendControlCommandAndWaitAckAsync(_protocol.BuildWarmUpCommand(), ScanDebugConstants.UsbCmdWarmUp, ScanDebugConstants.StopAckTotalTimeoutMs, ct, true);
                var warmUpAck = _protocol.ParseScanAck(warmUpFrame);

                if (warmUpAck.Command != ScanDebugConstants.UsbCmdWarmUp)
                    return new ScanOperationResult(false, $"Unexpected ACK cmd while enabling warm-up: 0x{warmUpAck.Command:X2}");

                if (warmUpAck.Status != 0x00)
                    return new ScanOperationResult(false, $"WARM_UP ACK status: {_protocol.MapStatus(warmUpAck.Status)} (0x{warmUpAck.Status:X2})");

                return new ScanOperationResult(true, $"Warm-up enabled (target={warmUpAck.Target}, completed={warmUpAck.Completed}).");
            }

            var stopResult = await StopScanAsync(ct);
            return new ScanOperationResult(stopResult.Success, stopResult.Success ? "Warm-up disabled." : stopResult.Message);
        }
        catch (Exception ex)
        {
            return new ScanOperationResult(false, $"Warm-up command failed: {ex.Message}");
        }
    }

    public Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null)
    {
        if (!IsConnected || _controlSession is null || _imageSession is null)
            return Task.FromResult(new ScanStartResult(false, "Scanner not connected. Click Connect Devices first.", null));

        return _executionRunner.StartScanAsync(_controlSession, _imageSession, rows, ct, onStatus, onDiagnostic, onProgress);
    }

    public Task<ScanStartResult> StartWarmUpSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null)
    {
        if (!IsConnected || _controlSession is null || _imageSession is null)
            return Task.FromResult(new ScanStartResult(false, "Scanner not connected. Click Connect Devices first.", null));

        return _executionRunner.StartWarmUpSegmentedScanAsync(_controlSession, _imageSession, totalRows, SingleTransferMaxRows, ct, onStatus, onDiagnostic, onProgress);
    }

    public Task<ScanStopResult> StopScanAsync(CancellationToken ct)
    {
        if (!IsConnected || _controlSession is null)
            return Task.FromResult(new ScanStopResult(false, "Scanner not connected."));

        return _executionRunner.StopScanAsync(_controlSession, ct);
    }

    public Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true)
    {
        if (_controlSession is null)
            throw new InvalidOperationException("Control session is not connected.");

        return _executionRunner.SendControlCommandAndWaitAckAsync(_controlSession, command, expectedCommand, totalTimeoutMs, ct, ignoreForeignCommands);
    }

    public void Dispose()
    {
        _usb.DevicesChanged -= OnDevicesChanged;
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
        => RefreshTargets();

    private UsbPipeSelection? FindPipe(UsbDeviceDto device, byte endpointAddress, bool isIn)
    {
        foreach (var cfg in _usb.GetConfigs(device.Id))
        {
            foreach (var itf in _usb.GetInterfaces(device.Id, cfg.ConfigId))
            {
                var endpoints = isIn
                    ? _usb.GetBulkInEndpoints(device.Id, cfg.ConfigId, itf.InterfaceId, itf.AlternateId)
                    : _usb.GetBulkOutEndpoints(device.Id, cfg.ConfigId, itf.InterfaceId, itf.AlternateId);

                foreach (var endpoint in endpoints)
                {
                    if (endpoint.Address == endpointAddress && endpoint.IsIn == isIn)
                        return new UsbPipeSelection(device.Id, cfg.ConfigId, itf.InterfaceId, itf.AlternateId, endpointAddress);
                }
            }
        }

        return null;
    }

    private static bool IsSameInterface(UsbPipeSelection left, UsbPipeSelection right)
        => left.DeviceId == right.DeviceId &&
           left.ConfigId == right.ConfigId &&
           left.InterfaceId == right.InterfaceId &&
           left.AltId == right.AltId;

    private int ResolveSingleTransferMaxBytes(IUsbBulkDuplexSession? imageSession, UsbPipeSelection? pipe)
    {
        var cachedFromSession = imageSession?.GetBulkInMaxTransferSize();
        if (cachedFromSession is > 0)
            return cachedFromSession.Value;

        if (pipe is null)
            return ScanDebugConstants.ImageRingBufferSize;

        var maxTransferBytes = _usb.GetBulkInMaxTransferSize(pipe.DeviceId, pipe.ConfigId, pipe.InterfaceId, pipe.AltId, pipe.EndpointAddress);
        return maxTransferBytes is > 0 ? maxTransferBytes.Value : ScanDebugConstants.ImageRingBufferSize;
    }

    private async Task DisconnectSessionsInternalAsync()
    {
        _connectionCts?.Cancel();
        await _ackChannel.StopAsync();

        _connectionCts?.Dispose();
        _connectionCts = null;

        _imageSession?.Dispose();
        _imageSession = null;
        _controlSession?.Dispose();
        _controlSession = null;
        _singleTransferMaxBytes = ScanDebugConstants.ImageRingBufferSize;

        _bulkInPipe = null;
        _bulkOutPipe = null;
        _bulkOutAckPipe = null;
        _ackChannel.ClearState();
    }
}
