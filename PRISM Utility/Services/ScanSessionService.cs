using System.Diagnostics;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Models;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Models;

namespace PRISM_Utility.Services;

public class ScanSessionService : IScanSessionService, IDisposable
{
    private readonly IUsbService _usb;
    private readonly IScanProtocolService _protocol;

    private UsbPipeSelection? _bulkInPipe;
    private UsbPipeSelection? _bulkOutPipe;
    private UsbPipeSelection? _bulkOutAckPipe;
    private IUsbBulkDuplexSession? _controlSession;
    private IUsbBulkDuplexSession? _imageSession;
    private CancellationTokenSource? _connectionCts;
    private Task? _controlAckReadTask;
    private readonly List<byte> _ackReadBuffer = new();
    private readonly Queue<ScanControlFrame> _ackQueue = new();
    private readonly SemaphoreSlim _ackSignal = new(0, int.MaxValue);

    public event EventHandler? TargetsChanged;

    public ScanTargetState Targets { get; private set; } = new(false, null, null);

    public bool IsConnected
    {
        get; private set;
    }

    public ScanSessionSnapshot Session
        => new(IsConnected, _controlSession, _connectionCts?.Token ?? CancellationToken.None);

    public ScanSessionService(IUsbService usb, IScanProtocolService protocol)
    {
        _usb = usb;
        _protocol = protocol;
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

            _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ClearAckState();
            _controlAckReadTask = RunControlAckReadLoopAsync(_controlSession, _connectionCts.Token);
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

    public async Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null)
    {
        if (!IsConnected || _controlSession is null || _imageSession is null)
            return new ScanStartResult(false, "Scanner not connected. Click Connect Devices first.", null);

        var targetBytes = rows * ScanDebugConstants.BytesPerLine;
        var runId = Guid.NewGuid().ToString("N")[..8];
        var scanWatch = Stopwatch.StartNew();
        void Mark(string phase) => onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] {phase}");

        try
        {
            ClearAckState();
            Mark("ACK buffers reset");

            await EnsureControlChannelIdleAsync(ct, runId, scanWatch, onDiagnostic);
            Mark("Idle probe completed");

            var setRowsFrame = await SendControlCommandAndWaitAckAsync(_protocol.BuildSetScanLinesCommand(rows), ScanDebugConstants.UsbCmdSetScanLines, ScanDebugConstants.SetRowsAckTotalTimeoutMs, ct, true);
            var setRowsAck = _protocol.ParseScanAck(setRowsFrame);
            _protocol.EnsureAckOk(setRowsAck, rows, "SET_SCAN_LINES");
            Mark("SET_SCAN_LINES ACK received");

            onStatus?.Invoke("Scan lines configured. 619C IN opened. Starting scan...");

            var drainedBeforeStart = await DrainImageBeforeScanAsync(_imageSession, ct);
            Mark($"619C pre-start drain bytes={drainedBeforeStart}");

            var imageReadTask = await ArmImageSingleReadAsync(_imageSession, targetBytes, ct, runId, scanWatch, onDiagnostic);
            Mark("619C single read armed");

            onStatus?.Invoke("619C read started. Sending START_SCAN...");
            await _controlSession.WriteBulkOutAsync(_protocol.BuildStartScanCommand(), ScanDebugConstants.AckTimeoutMs, ct);
            Mark("START_SCAN sent");

            var ackTask = MonitorStartScanAcksAsync(rows, ct, runId, scanWatch, onStatus, onDiagnostic);
            onStatus?.Invoke("Scan command sent, receiving image lines...");

            var gotDoneAck = await WaitForStartScanDoneAckAsync(ackTask, ScanDebugConstants.StartScanDoneAckWaitAfterImageMs);
            Mark(gotDoneAck ? "Done ACK observed" : "Done ACK wait timed out");
            if (!gotDoneAck)
            {
                onStatus?.Invoke("START_SCAN done ACK not received in time.");
                await FinalizeAfterFailedImageReadAsync(_controlSession, ackTask, ct, runId, scanWatch, onDiagnostic);
                return new ScanStartResult(false, "START_SCAN done ACK not received in time.", null);
            }

            var imageChunk = await imageReadTask;
            Mark($"619C single read bytes: {imageChunk.transferred}/{targetBytes}");
            if (imageChunk.transferred != targetBytes)
                throw new IOException($"619C transfer size mismatch: expected {targetBytes}, actual {imageChunk.transferred}");

            var buffer = new byte[targetBytes];
            Buffer.BlockCopy(imageChunk.data, 0, buffer, 0, targetBytes);
            onStatus?.Invoke("Scan completed.");
            Mark("Scan completed");
            return new ScanStartResult(true, "Scan completed.", buffer);
        }
        catch (OperationCanceledException)
        {
            Mark("Scan canceled");
            return new ScanStartResult(false, "Scan stopped.", null);
        }
        catch (Exception ex)
        {
            Mark($"Scan failed: {ex.Message}");
            return new ScanStartResult(false, $"Scan failed after {scanWatch.ElapsedMilliseconds} ms: {ex.Message}", null);
        }
    }

    public async Task<ScanStartResult> StartWarmUpSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null)
    {
        if (!IsConnected || _controlSession is null || _imageSession is null)
            return new ScanStartResult(false, "Scanner not connected. Click Connect Devices first.", null);

        if (totalRows <= ScanDebugConstants.MaxRows)
            return await StartScanAsync(totalRows, ct, onStatus, onDiagnostic);

        var totalTargetBytes = totalRows * ScanDebugConstants.BytesPerLine;
        var segmentCount = (int)Math.Ceiling((double)totalRows / ScanDebugConstants.MaxRows);
        var runId = Guid.NewGuid().ToString("N")[..8];
        var scanWatch = Stopwatch.StartNew();
        void Mark(string phase) => onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] {phase}");

        try
        {
            ClearAckState();
            Mark("ACK buffers reset");

            await EnsureControlChannelIdleAsync(ct, runId, scanWatch, onDiagnostic);
            Mark("Idle probe completed");

            var drainedBeforeStart = await DrainImageBeforeScanAsync(_imageSession, ct);
            Mark($"619C pre-start drain bytes={drainedBeforeStart}");

            var buffer = new byte[totalTargetBytes];
            var completedRows = 0;
            var bufferOffset = 0;
            var totalTransferMs = 0L;
            var totalAckWaitMs = 0L;
            for (var segmentIndex = 1; segmentIndex <= segmentCount; segmentIndex++)
            {
                ct.ThrowIfCancellationRequested();

                var segmentWatch = Stopwatch.StartNew();

                var segmentRows = Math.Min(ScanDebugConstants.MaxRows, totalRows - completedRows);
                var segmentTargetBytes = checked(segmentRows * ScanDebugConstants.BytesPerLine);

                onStatus?.Invoke($"[{segmentIndex}/{segmentCount}] Configuring {segmentRows} lines...");
                var setRowsFrame = await SendControlCommandAndWaitAckAsync(_protocol.BuildSetScanLinesCommand(segmentRows), ScanDebugConstants.UsbCmdSetScanLines, ScanDebugConstants.SetRowsAckTotalTimeoutMs, ct, true);
                var setRowsAck = _protocol.ParseScanAck(setRowsFrame);
                _protocol.EnsureAckOk(setRowsAck, segmentRows, "SET_SCAN_LINES");
                Mark($"Segment {segmentIndex}/{segmentCount}: SET_SCAN_LINES ACK received for {segmentRows} rows at {segmentWatch.ElapsedMilliseconds} ms");

                var imageReadTask = await ArmImageSingleReadAsync(_imageSession, segmentTargetBytes, ct, runId, scanWatch, onDiagnostic);
                Mark($"Segment {segmentIndex}/{segmentCount}: 619C read armed for {segmentTargetBytes} bytes at {segmentWatch.ElapsedMilliseconds} ms");

                onStatus?.Invoke($"[{segmentIndex}/{segmentCount}] Sending START_SCAN...");
                await _controlSession.WriteBulkOutAsync(_protocol.BuildStartScanCommand(), ScanDebugConstants.AckTimeoutMs, ct);
                Mark($"Segment {segmentIndex}/{segmentCount}: START_SCAN sent at {segmentWatch.ElapsedMilliseconds} ms");

                var ackTask = MonitorStartScanAcksAsync(segmentRows, ct, runId, scanWatch, status => onStatus?.Invoke($"[{segmentIndex}/{segmentCount}] {status}"), onDiagnostic);

                var transferWatch = Stopwatch.StartNew();
                var imageChunk = await imageReadTask;
                transferWatch.Stop();
                totalTransferMs += transferWatch.ElapsedMilliseconds;
                Mark($"Segment {segmentIndex}/{segmentCount}: 619C read bytes {imageChunk.transferred}/{segmentTargetBytes} at {segmentWatch.ElapsedMilliseconds} ms (transfer={transferWatch.ElapsedMilliseconds} ms)");
                if (imageChunk.transferred != segmentTargetBytes)
                    throw new IOException($"619C transfer size mismatch on segment {segmentIndex}/{segmentCount}: expected {segmentTargetBytes}, actual {imageChunk.transferred}");

                Buffer.BlockCopy(imageChunk.data, 0, buffer, bufferOffset, segmentTargetBytes);
                bufferOffset += segmentTargetBytes;

                var ackWaitWatch = Stopwatch.StartNew();
                var gotDoneAck = await WaitForStartScanDoneAckAsync(ackTask, ScanDebugConstants.StartScanDoneAckWaitAfterImageMs);
                ackWaitWatch.Stop();
                totalAckWaitMs += ackWaitWatch.ElapsedMilliseconds;
                Mark(gotDoneAck
                    ? $"Segment {segmentIndex}/{segmentCount}: done ACK observed at {segmentWatch.ElapsedMilliseconds} ms (ack-wait={ackWaitWatch.ElapsedMilliseconds} ms)"
                    : $"Segment {segmentIndex}/{segmentCount}: done ACK wait timed out at {segmentWatch.ElapsedMilliseconds} ms (ack-wait={ackWaitWatch.ElapsedMilliseconds} ms)");

                if (!gotDoneAck)
                {
                    onStatus?.Invoke($"[{segmentIndex}/{segmentCount}] START_SCAN done ACK not received in time.");
                    await FinalizeAfterFailedImageReadAsync(_controlSession, ackTask, ct, runId, scanWatch, onDiagnostic);
                    return new ScanStartResult(false, $"Segment {segmentIndex}/{segmentCount} done ACK not received in time.", null);
                }

                completedRows += segmentRows;
                var segmentElapsedMs = Math.Max(1L, segmentWatch.ElapsedMilliseconds);
                var transferPercent = (transferWatch.ElapsedMilliseconds * 100.0) / segmentElapsedMs;
                var ackWaitPercent = (ackWaitWatch.ElapsedMilliseconds * 100.0) / segmentElapsedMs;
                Mark($"Segment {segmentIndex}/{segmentCount}: completed rows={completedRows}/{totalRows}, bytes={bufferOffset}/{totalTargetBytes}, elapsed={segmentElapsedMs} ms, transfer={transferWatch.ElapsedMilliseconds} ms ({transferPercent:F1}%), ack-wait={ackWaitWatch.ElapsedMilliseconds} ms ({ackWaitPercent:F1}%)");
            }

            Mark($"Segmented scan image bytes={bufferOffset}/{totalTargetBytes}");
            if (bufferOffset != totalTargetBytes)
                throw new IOException($"619C transfer size mismatch: expected {totalTargetBytes}, actual {bufferOffset}");

            var totalTransferSeconds = Math.Max(totalTransferMs, 1L) / 1000.0;
            var totalMegabytes = totalTargetBytes / (1024.0 * 1024.0);
            var totalDataRateMBps = totalMegabytes / totalTransferSeconds;
            Mark($"Segmented scan totals: bytes={totalTargetBytes}, transfer={totalTransferMs} ms, ack-wait={totalAckWaitMs} ms, data-rate={totalDataRateMBps:F2} MiB/s");

            onStatus?.Invoke($"Scan completed in {segmentCount} segment(s).");
            Mark("Segmented warm-up scan completed");
            return new ScanStartResult(true, $"Scan completed in {segmentCount} segment(s).", buffer);
        }
        catch (OperationCanceledException)
        {
            Mark("Segmented warm-up scan canceled");
            return new ScanStartResult(false, "Scan stopped.", null);
        }
        catch (Exception ex)
        {
            Mark($"Segmented warm-up scan failed: {ex.Message}");
            return new ScanStartResult(false, $"Scan failed after {scanWatch.ElapsedMilliseconds} ms: {ex.Message}", null);
        }
    }

    public async Task<ScanStopResult> StopScanAsync(CancellationToken ct)
    {
        if (!IsConnected || _controlSession is null)
            return new ScanStopResult(false, "Scanner not connected.");

        try
        {
            await _controlSession.WriteBulkOutAsync(_protocol.BuildStopScanCommand(), ScanDebugConstants.AckTimeoutMs, ct);
            var stopAckFrame = await ReadAckForCommandAsync(ScanDebugConstants.UsbCmdStopScan, ScanDebugConstants.StopAckTotalTimeoutMs, ct, true);
            var stopAck = _protocol.ParseScanAck(stopAckFrame);

            if (stopAck.Command != ScanDebugConstants.UsbCmdStopScan)
                return new ScanStopResult(false, $"Unexpected ACK cmd while stopping: 0x{stopAck.Command:X2}");

            if (stopAck.Status == 0x00)
                return new ScanStopResult(true, $"STOP_SCAN ACK received (target={stopAck.Target}, completed={stopAck.Completed}).");

            return new ScanStopResult(false, $"STOP_SCAN ACK status: {_protocol.MapStatus(stopAck.Status)} (0x{stopAck.Status:X2})");
        }
        catch (Exception ex)
        {
            return new ScanStopResult(false, $"STOP_SCAN send failed: {ex.Message}");
        }
    }

    public async Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true)
    {
        if (_controlSession is null)
            throw new InvalidOperationException("Control session is not connected.");

        await _controlSession.WriteBulkOutAsync(command, ScanDebugConstants.AckTimeoutMs, ct);
        return await ReadAckForCommandAsync(expectedCommand, totalTimeoutMs, ct, ignoreForeignCommands);
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

    private async Task FinalizeAfterFailedImageReadAsync(IUsbBulkDuplexSession controlSession, Task<bool> ackTask, CancellationToken ct, string runId, Stopwatch scanWatch, Action<string>? onDiagnostic)
    {
        var gotDoneAck = await WaitForStartScanDoneAckAsync(ackTask, ScanDebugConstants.FailureDoneAckWaitMs);
        onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] {(gotDoneAck ? "Finalize: done ACK received during grace wait" : "Finalize: done ACK not received during grace wait")}");

        if (gotDoneAck)
            return;

        try
        {
            await ackTask;
        }
        catch (Exception ex)
        {
            onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] Finalize: ack task ended with error: {ex.Message}");
        }

        try
        {
            await controlSession.WriteBulkOutAsync(_protocol.BuildStopScanCommand(), ScanDebugConstants.AckTimeoutMs, CancellationToken.None);
            _ = await ReadAckForCommandAsync(ScanDebugConstants.UsbCmdStopScan, ScanDebugConstants.StopAckTotalTimeoutMs, CancellationToken.None, true);
            onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] Finalize: STOP_SCAN sent and ACK received");
        }
        catch (Exception ex)
        {
            onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] Finalize: STOP_SCAN path failed: {ex.Message}");
        }

        try
        {
            var drained = await DrainPendingAcksAsync(ScanDebugConstants.PreScanAckDrainWindowMs, ct);
            onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] Finalize: drained {drained} pending ACK frame(s)");
        }
        catch (Exception ex)
        {
            onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] Finalize: ACK drain failed: {ex.Message}");
        }
    }

    private async Task<bool> MonitorStartScanAcksAsync(int rows, CancellationToken ct, string runId, Stopwatch scanWatch, Action<string>? onStatus, Action<string>? onDiagnostic)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(ScanDebugConstants.StartScanAckMonitorTotalTimeoutMs);
        var hasAcceptAck = false;
        var busyCount = 0;
        var timeoutCount = 0;

        while (!ct.IsCancellationRequested)
        {
            var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0)
            {
                onStatus?.Invoke("START_SCAN ACK monitor timed out.");
                onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] ACK monitor timed out. busy={busyCount}, timeoutSlices={timeoutCount}");
                return false;
            }

            try
            {
                var readTimeout = Math.Min(remaining, ScanDebugConstants.AckTimeoutMs);
                var frame = await ReadNextFrameAsync(readTimeout, ct);
                if (frame.Opcode != ScanDebugConstants.UsbCmdStartScan)
                    continue;

                var ack = _protocol.ParseScanAck(frame);
                if (ack.Status == 0xE6)
                {
                    busyCount++;
                    onStatus?.Invoke($"Device busy (cmd=0x{ack.Command:X2})");
                    if (busyCount >= ScanDebugConstants.StartScanBusyMaxCount)
                    {
                        onStatus?.Invoke("Device stayed busy for too long.");
                        return false;
                    }

                    var backoffMs = Math.Min(ScanDebugConstants.BusyBackoffMaxMs, ScanDebugConstants.BusyBackoffInitialMs * (1 << Math.Min(4, busyCount - 1)));
                    await Task.Delay(backoffMs, ct);
                    continue;
                }

                if (ack.Status != 0x00)
                {
                    onStatus?.Invoke($"START_SCAN ACK status: {_protocol.MapStatus(ack.Status)} (0x{ack.Status:X2})");
                    return false;
                }

                if (ack.Target != (uint)rows)
                {
                    onStatus?.Invoke($"START_SCAN ACK target mismatch: {ack.Target} (expected {rows})");
                    return false;
                }

                if (!hasAcceptAck && ack.Completed == 0)
                {
                    hasAcceptAck = true;
                    onStatus?.Invoke("START_SCAN ACK received.");
                    continue;
                }

                if (ack.Completed == (uint)rows)
                {
                    onStatus?.Invoke("START_SCAN done ACK received.");
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (IOException ex) when (_protocol.IsIoTimeout(ex))
            {
                timeoutCount++;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private async Task EnsureControlChannelIdleAsync(CancellationToken ct, string runId, Stopwatch scanWatch, Action<string>? onDiagnostic)
    {
        for (var attempt = 1; attempt <= ScanDebugConstants.IdleProbeAttempts; attempt++)
        {
            var drained = await DrainPendingAcksAsync(ScanDebugConstants.IdleProbeWindowMs, ct);
            if (drained <= 0)
            {
                onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] Idle probe attempt {attempt}: no pending ACK.");
                return;
            }

            onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] Idle probe attempt {attempt}: drained {drained} ACK frame(s).");
            if (attempt < ScanDebugConstants.IdleProbeAttempts)
                await Task.Delay(ScanDebugConstants.IdleProbeRetryDelayMs, ct);
        }
    }

    private async Task<ScanControlFrame> ReadAckForCommandAsync(byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(totalTimeoutMs);
        while (!ct.IsCancellationRequested)
        {
            var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0)
                break;

            var readTimeout = Math.Min(remaining, ScanDebugConstants.AckTimeoutMs);
            try
            {
                var frame = await ReadNextFrameAsync(readTimeout, ct);
                if (frame.Opcode == expectedCommand)
                    return frame;

                if (ignoreForeignCommands)
                    continue;

                throw new IOException($"Unexpected ACK cmd: expected 0x{expectedCommand:X2}, actual 0x{frame.Opcode:X2}");
            }
            catch (IOException ex) when (_protocol.IsIoTimeout(ex))
            {
            }
        }

        throw new IOException($"Timed out waiting ACK cmd 0x{expectedCommand:X2}");
    }

    private async Task<int> DrainPendingAcksAsync(int windowMs, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(windowMs);
        var drained = 0;

        while (!ct.IsCancellationRequested)
        {
            var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0)
                break;

            var readTimeout = Math.Min(remaining, ScanDebugConstants.AckReadSliceTimeoutMs);
            try
            {
                _ = await ReadNextFrameAsync(readTimeout, ct);
                drained++;
            }
            catch (IOException ex) when (_protocol.IsIoTimeout(ex))
            {
                break;
            }
            catch
            {
                break;
            }
        }

        return drained;
    }

    private void ClearAckState()
    {
        lock (_ackReadBuffer)
            _ackReadBuffer.Clear();

        lock (_ackQueue)
            _ackQueue.Clear();

        while (_ackSignal.CurrentCount > 0)
            _ackSignal.Wait(0);
    }

    private async Task<ScanControlFrame> ReadNextFrameAsync(int totalTimeoutMs, CancellationToken ct)
    {
        if (TryDequeueFrameFromQueue(out var bufferedFrame))
            return bufferedFrame;

        var gotSignal = await _ackSignal.WaitAsync(totalTimeoutMs, ct);
        if (!gotSignal)
            throw new IOException("Timed out waiting ACK frame");

        if (TryDequeueFrameFromQueue(out var frame))
            return frame;

        var deadline = DateTime.UtcNow.AddMilliseconds(ScanDebugConstants.AckReadSliceTimeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (TryDequeueFrameFromQueue(out frame))
                return frame;
            await Task.Delay(5, ct);
        }

        throw new IOException("Timed out waiting ACK frame");
    }

    private bool TryDequeueFrameFromQueue(out ScanControlFrame frame)
    {
        lock (_ackQueue)
        {
            if (_ackQueue.Count > 0)
            {
                frame = _ackQueue.Dequeue();
                return true;
            }
        }

        frame = default!;
        return false;
    }

    private async Task RunControlAckReadLoopAsync(IUsbBulkDuplexSession controlSession, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var chunk = await controlSession.ReadBulkInOnceAsync(ScanDebugConstants.AckReadChunkSize, ScanDebugConstants.AckReadSliceTimeoutMs, ct);
                if (chunk.transferred <= 0)
                    continue;

                lock (_ackReadBuffer)
                {
                    _ackReadBuffer.AddRange(chunk.data.AsSpan(0, chunk.transferred).ToArray());
                    while (_protocol.TryDequeueControlFrame(_ackReadBuffer, out var frame))
                    {
                        lock (_ackQueue)
                            _ackQueue.Enqueue(frame);
                        _ackSignal.Release();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(30, CancellationToken.None);
            }
        }
    }

    private async Task<int> DrainImageBeforeScanAsync(IUsbBulkDuplexSession imageSession, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(ScanDebugConstants.ImagePreStartDrainWindowMs);
        var drained = 0;
        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0)
                break;

            var timeout = Math.Min(remaining, ScanDebugConstants.ImagePreStartDrainSliceTimeoutMs);
            var chunk = await imageSession.ReadBulkInOnceAsync(ScanDebugConstants.ImageReadChunkSize, timeout, ct);
            if (chunk.transferred <= 0)
                break;

            drained += chunk.transferred;
        }

        return drained;
    }

    private static async Task<Task<(int transferred, byte[] data)>> ArmImageSingleReadAsync(IUsbBulkDuplexSession imageSession, int bufferSize, CancellationToken ct, string runId, Stopwatch scanWatch, Action<string>? onDiagnostic)
    {
        for (var attempt = 1; attempt <= ScanDebugConstants.ImageReadArmMaxAttempts; attempt++)
        {
            var task = imageSession.ReadBulkInOnceAsync(bufferSize, ScanDebugConstants.ImageReadTimeoutMs, ct);
            await Task.Delay(ScanDebugConstants.ImageReadArmDelayMs, ct);
            if (!task.IsCompleted)
            {
                onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] 619C read armed on attempt {attempt}");
                return task;
            }

            var chunk = await task;
            onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] 619C read completed too early on attempt {attempt}, bytes={chunk.transferred}");
        }

        throw new IOException("Failed to arm 619C read before START_SCAN");
    }

    private static async Task<bool> WaitForStartScanDoneAckAsync(Task<bool> ackTask, int timeoutMs)
    {
        var completed = await Task.WhenAny(ackTask, Task.Delay(timeoutMs));
        if (completed != ackTask)
            return false;

        return await ackTask;
    }

    private async Task DisconnectSessionsInternalAsync()
    {
        _connectionCts?.Cancel();

        if (_controlAckReadTask is not null)
        {
            try
            {
                await _controlAckReadTask;
            }
            catch
            {
            }
        }

        _controlAckReadTask = null;

        _connectionCts?.Dispose();
        _connectionCts = null;

        _imageSession?.Dispose();
        _imageSession = null;
        _controlSession?.Dispose();
        _controlSession = null;

        _bulkInPipe = null;
        _bulkOutPipe = null;
        _bulkOutAckPipe = null;
        ClearAckState();
    }
}
