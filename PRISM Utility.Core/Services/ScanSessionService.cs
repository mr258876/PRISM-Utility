using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Contracts.Models;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public class ScanSessionService : IScanSessionService
{
    private const int MotionCompletionPaddingMs = 10000;
    private const double MotionCompletionTimeoutMultiplier = 2.0;

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
    private bool _disposed;

    public event EventHandler? TargetsChanged;

    public event Action<ScanMotorState>? MotionEventReceived;

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
        _ackChannel.MotionEventReceived += motionEvent => MotionEventReceived?.Invoke(motionEvent);
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
        ThrowIfDisposed();
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
        ThrowIfDisposed();
        await DisconnectSessionsInternalAsync();
        IsConnected = false;
    }

    public async Task<ScanIlluminationState> GetIlluminationStateAsync(CancellationToken ct)
    {
        var response = await SendControlCommandAndEnsureOkAsync(
            _protocol.BuildGetIlluminationStateCommand(),
            ScanDebugConstants.UsbCmdIlluminationGetState,
            "GET_ILLUMINATION_STATE",
            ct);

        return _protocol.ParseIlluminationStatePayload(response.Payload);
    }

    public async Task SetIlluminationLevelsAsync(ushort led1Level, ushort led2Level, ushort led3Level, ushort led4Level, CancellationToken ct)
    {
        var response = await SendControlCommandAndEnsureOkAsync(
            _protocol.BuildSetIlluminationLevelsCommand(led1Level, led2Level, led3Level, led4Level),
            ScanDebugConstants.UsbCmdIlluminationSetLevels,
            "SET_ILLUMINATION_LEVELS",
            ct);

        EnsurePayloadLength(response, ScanDebugConstants.IlluminationSetLevelsPayloadLength, "SET_ILLUMINATION_LEVELS");
    }

    public async Task SetSteadyIlluminationAsync(byte steadyMask, CancellationToken ct)
    {
        var response = await SendControlCommandAndEnsureOkAsync(
            _protocol.BuildSetSteadyIlluminationCommand(steadyMask),
            ScanDebugConstants.UsbCmdIlluminationSetSteady,
            "SET_STEADY_ILLUMINATION",
            ct);

        EnsurePayloadLength(response, ScanDebugConstants.IlluminationMaskPayloadLength, "SET_STEADY_ILLUMINATION");
    }

    public async Task ConfigureExposureLightingAsync(byte syncMask, CancellationToken ct)
    {
        var response = await SendControlCommandAndEnsureOkAsync(
            _protocol.BuildConfigureExposureLightingCommand(syncMask),
            ScanDebugConstants.UsbCmdIlluminationConfigSync,
            "CONFIGURE_EXPOSURE_LIGHTING",
            ct);

        EnsurePayloadLength(response, ScanDebugConstants.IlluminationMaskPayloadLength, "CONFIGURE_EXPOSURE_LIGHTING");
    }

    public async Task SetSyncPulseClocksAsync(uint led1PulseClock, uint led2PulseClock, uint led3PulseClock, uint led4PulseClock, CancellationToken ct)
    {
        var response = await SendControlCommandAndEnsureOkAsync(
            _protocol.BuildSetSyncPulseClocksCommand(led1PulseClock, led2PulseClock, led3PulseClock, led4PulseClock),
            ScanDebugConstants.UsbCmdIlluminationSetSyncPulse,
            "SET_SYNC_PULSE_CLOCKS",
            ct);

        EnsurePayloadLength(response, ScanDebugConstants.IlluminationSetSyncPulsePayloadLength, "SET_SYNC_PULSE_CLOCKS");
    }

    public async Task<IReadOnlyList<ScanMotorState>> GetMotionStateAsync(CancellationToken ct)
    {
        var response = await SendControlCommandAndEnsureOkAsync(
            _protocol.BuildGetMotionStateCommand(),
            ScanDebugConstants.UsbCmdMotionGetState,
            "GET_MOTION_STATE",
            ct);

        return _protocol.ParseMotionStatePayload(response.Payload);
    }

    public async Task SetMotorEnabledAsync(byte motorId, bool enabled, CancellationToken ct)
    {
        var response = await SendControlCommandAndEnsureOkAsync(
            _protocol.BuildSetMotorEnableCommand(motorId, enabled),
            ScanDebugConstants.UsbCmdMotionSetEnable,
            "SET_MOTOR_ENABLE",
            ct);

        EnsurePayloadLength(response, ScanDebugConstants.MotionSetEnablePayloadLength, "SET_MOTOR_ENABLE");
    }

    public async Task MoveMotorStepsAsync(byte motorId, bool direction, uint steps, uint intervalUs, CancellationToken ct)
    {
        _ackChannel.ClearMotionEvent(motorId);
        var response = await SendControlCommandAndEnsureOkAsync(
            _protocol.BuildMoveMotorStepsCommand(motorId, direction, steps, intervalUs),
            ScanDebugConstants.UsbCmdMotionMoveSteps,
            "MOVE_MOTOR_STEPS",
            ct);

        EnsurePayloadLength(response, ScanDebugConstants.MotionMoveStepsPayloadLength, "MOVE_MOTOR_STEPS");
    }

    public async Task PrepareMotorOnExposureSyncAsync(byte motorId, bool direction, uint steps, uint intervalUs, CancellationToken ct)
    {
        _ackChannel.ClearMotionEvent(motorId);
        var response = await SendControlCommandAndEnsureOkAsync(
            _protocol.BuildPrepareMotorOnSyncCommand(motorId, direction, steps, intervalUs),
            ScanDebugConstants.UsbCmdMotionPrepareOnSync,
            "PREPARE_MOTOR_ON_SYNC",
            ct);

        EnsurePayloadLength(response, ScanDebugConstants.MotionMoveStepsPayloadLength, "PREPARE_MOTOR_ON_SYNC");
    }

    public async Task<ScanMotorState> WaitForMotorMotionCompleteAsync(byte motorId, uint steps, uint intervalUs, CancellationToken ct)
    {
        var expectedTravelMs = Math.Ceiling((double)steps * intervalUs / 1000.0);
        var totalTimeoutMs = Math.Min(
            uint.MaxValue,
            Math.Max(ScanDebugConstants.AckTimeoutMs, (expectedTravelMs * MotionCompletionTimeoutMultiplier) + MotionCompletionPaddingMs));
        var deadline = DateTime.UtcNow.AddMilliseconds(totalTimeoutMs);
        ScanMotorState? lastObservedMotorState = null;

        while (!ct.IsCancellationRequested)
        {
            var remainingMs = (uint)Math.Max(0, (deadline - DateTime.UtcNow).TotalMilliseconds);
            if (remainingMs == 0)
                break;

            try
            {
                return await _ackChannel.ReadMotionCompleteEventAsync(motorId, Math.Min(remainingMs, 500u), ct);
            }
            catch (IOException ex) when (IsMotionEventWaitTimeout(ex) || _protocol.IsIoTimeout(ex))
            {
                var states = await GetMotionStateAsync(ct);
                var motorState = states.FirstOrDefault(state => state.MotorId == motorId);
                lastObservedMotorState = motorState;
                if (motorState is not null && !motorState.Running && motorState.RemainingSteps == 0)
                    return motorState;
            }
        }

        var lastObservedStateText = lastObservedMotorState is null
            ? "last_state=<unavailable>"
            : $"last_state={{motor={lastObservedMotorState.MotorId}, enabled={lastObservedMotorState.Enabled}, running={lastObservedMotorState.Running}, direction={lastObservedMotorState.Direction}, diag=0x{lastObservedMotorState.Diag:X2}, interval_us={lastObservedMotorState.IntervalUs}, remaining_steps={lastObservedMotorState.RemainingSteps}}}";
        throw new IOException($"Timed out waiting motion complete event for motor {motorId}. steps={steps}, interval_us={intervalUs}, expected_travel_ms={expectedTravelMs:0}, timeout_ms={totalTimeoutMs:0}, {lastObservedStateText}");
    }

    private static bool IsMotionEventWaitTimeout(IOException ex)
        => ex.Message.Contains("Timed out waiting motion complete event", StringComparison.OrdinalIgnoreCase);

    public async Task<ScanMotorState> MoveMotorStepsAndWaitForCompletionAsync(byte motorId, bool direction, uint steps, uint intervalUs, CancellationToken ct)
    {
        await MoveMotorStepsAsync(motorId, direction, steps, intervalUs, ct);
        return await WaitForMotorMotionCompleteAsync(motorId, steps, intervalUs, ct);
    }

    public async Task StopMotorAsync(byte motorId, CancellationToken ct)
    {
        var response = await SendControlCommandAndEnsureOkAsync(
            _protocol.BuildStopMotorCommand(motorId),
            ScanDebugConstants.UsbCmdMotionStop,
            "STOP_MOTOR",
            ct);

        EnsurePayloadLength(response, ScanDebugConstants.MotionSingleMotorPayloadLength, "STOP_MOTOR");
    }

    public async Task ApplyMotorConfigAsync(byte motorId, CancellationToken ct)
    {
        var response = await SendControlCommandAndEnsureOkAsync(
            _protocol.BuildApplyMotorConfigCommand(motorId),
            ScanDebugConstants.UsbCmdMotionApplyConfig,
            "APPLY_MOTOR_CONFIG",
            ct);

        EnsurePayloadLength(response, ScanDebugConstants.MotionSingleMotorPayloadLength, "APPLY_MOTOR_CONFIG");
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
        if (_disposed)
            return;

        _disposed = true;
        _usb.DevicesChanged -= OnDevicesChanged;

        try
        {
            DisconnectSessionsInternalAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }

        IsConnected = false;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _usb.DevicesChanged -= OnDevicesChanged;

        try
        {
            await DisconnectSessionsInternalAsync();
        }
        catch
        {
        }

        IsConnected = false;
        GC.SuppressFinalize(this);
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

    private async Task<ScanControlFrame> SendControlCommandAndEnsureOkAsync(byte[] command, byte expectedCommand, string commandName, CancellationToken ct)
    {
        var response = await SendControlCommandAndWaitAckAsync(command, expectedCommand, ScanDebugConstants.AckTimeoutMs, ct, true);
        if (response.Status != 0x00)
            throw new IOException($"{commandName} failed: {_protocol.MapStatus(response.Status)} (0x{response.Status:X2})");

        return response;
    }

    private static void EnsurePayloadLength(ScanControlFrame response, int expectedPayloadLength, string commandName)
    {
        if (response.Payload.Length != expectedPayloadLength)
            throw new IOException($"{commandName} payload length invalid: {response.Payload.Length} (expected {expectedPayloadLength})");
    }

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

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ScanSessionService));
    }
}
