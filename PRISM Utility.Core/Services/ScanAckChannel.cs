using System.Diagnostics;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

internal sealed class ScanAckChannel
{
    private readonly IScanProtocolService _protocol;
    private readonly List<byte> _ackReadBuffer = new();
    private readonly Queue<ScanControlFrame> _ackQueue = new();
    private readonly SemaphoreSlim _ackSignal = new(0, int.MaxValue);
    private Task? _readLoopTask;

    public ScanAckChannel(IScanProtocolService protocol)
    {
        _protocol = protocol;
    }

    public void ClearState()
    {
        lock (_ackReadBuffer)
            _ackReadBuffer.Clear();

        lock (_ackQueue)
            _ackQueue.Clear();

        while (_ackSignal.CurrentCount > 0)
            _ackSignal.Wait(0);
    }

    public void Start(IUsbBulkDuplexSession controlSession, CancellationToken ct)
    {
        ClearState();
        _readLoopTask = RunControlAckReadLoopAsync(controlSession, ct);
    }

    public async Task StopAsync()
    {
        if (_readLoopTask is null)
            return;

        try
        {
            await _readLoopTask;
        }
        catch
        {
        }

        _readLoopTask = null;
    }

    public async Task EnsureControlChannelIdleAsync(CancellationToken ct, string runId, Stopwatch scanWatch, Action<string>? onDiagnostic)
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

    public async Task<ScanControlFrame> ReadAckForCommandAsync(byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands)
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

    public async Task<bool> MonitorStartScanAcksAsync(int rows, CancellationToken ct, string runId, Stopwatch scanWatch, Action<string>? onStatus, Action<string>? onDiagnostic)
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

    public async Task<int> DrainPendingAcksAsync(int windowMs, CancellationToken ct)
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
}
