using System.Diagnostics;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

internal sealed class ScanExecutionRunner
{
    private readonly IScanProtocolService _protocol;
    private readonly IScanTransferSettingsService _transferSettings;
    private readonly ScanAckChannel _ackChannel;

    public ScanExecutionRunner(IScanProtocolService protocol, IScanTransferSettingsService transferSettings, ScanAckChannel ackChannel)
    {
        _protocol = protocol;
        _transferSettings = transferSettings;
        _ackChannel = ackChannel;
    }

    public async Task<ScanStartResult> StartScanAsync(IUsbBulkDuplexSession controlSession, IUsbBulkDuplexSession imageSession, int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, uint? expectedLineTimeUs = null)
    {
        var targetBytes = rows * ScanDebugConstants.BytesPerLine;
        await _transferSettings.InitializeAsync();
        var transferSettings = _transferSettings.Settings;
        var runId = Guid.NewGuid().ToString("N")[..8];
        var scanWatch = Stopwatch.StartNew();
        void Mark(string phase) => onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] {phase}");

        try
        {
            _ackChannel.ClearState();
            Mark("ACK buffers reset");
            await _ackChannel.EnsureControlChannelIdleAsync(ct, runId, scanWatch, onDiagnostic);
            Mark("Idle probe completed");

            var drainedBeforeStart = await DrainImageBeforeScanAsync(imageSession, ct);
            Mark($"619C pre-start drain bytes={drainedBeforeStart}");

            onProgress?.Invoke(0, targetBytes);
            var segmentResult = await ExecuteScanSegmentAsync(
                controlSession,
                imageSession,
                rows,
                transferSettings,
                ct,
                runId,
                scanWatch,
                onStatus,
                onDiagnostic,
                onProgress,
                targetBytes,
                expectedLineTimeUs,
                null);

            onStatus?.Invoke("Scan completed.");
            Mark("Scan completed");
            return new ScanStartResult(true, "Scan completed.", segmentResult.Buffer);
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

    public async Task<ScanStartResult> StartSegmentedScanAsync(IUsbBulkDuplexSession controlSession, IUsbBulkDuplexSession imageSession, int totalRows, int singleTransferMaxRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, uint? expectedLineTimeUs = null)
    {
        if (totalRows <= singleTransferMaxRows)
            return await StartScanAsync(controlSession, imageSession, totalRows, ct, onStatus, onDiagnostic, onProgress, expectedLineTimeUs);

        var totalTargetBytes = totalRows * ScanDebugConstants.BytesPerLine;
        onProgress?.Invoke(0, totalTargetBytes);
        var segmentCount = (int)Math.Ceiling((double)totalRows / singleTransferMaxRows);
        var runId = Guid.NewGuid().ToString("N")[..8];
        var scanWatch = Stopwatch.StartNew();
        void Mark(string phase) => onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] {phase}");

        try
        {
            _ackChannel.ClearState();
            Mark("ACK buffers reset");
            await _ackChannel.EnsureControlChannelIdleAsync(ct, runId, scanWatch, onDiagnostic);
            Mark("Idle probe completed");

            var drainedBeforeStart = await DrainImageBeforeScanAsync(imageSession, ct);
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
                var segmentRows = Math.Min(singleTransferMaxRows, totalRows - completedRows);
                var segmentTargetBytes = checked(segmentRows * ScanDebugConstants.BytesPerLine);
                var segmentBaseOffset = bufferOffset;

                var segmentResult = await ExecuteScanSegmentAsync(
                    controlSession,
                    imageSession,
                    segmentRows,
                    new ScanBulkInTransferOptions(ScanBulkInReadMode.SingleRequest, ScanDebugConstants.ImageMultiBufferRequestBytes, ScanDebugConstants.ImageMultiBufferOutstandingReads, ScanDebugConstants.ImageReadTimeoutMs, false),
                    ct,
                    runId,
                    scanWatch,
                    status => onStatus?.Invoke($"[{segmentIndex}/{segmentCount}] {status}"),
                    onDiagnostic,
                    (segmentTransferred, _) => onProgress?.Invoke(segmentBaseOffset + segmentTransferred, totalTargetBytes),
                    segmentTargetBytes,
                    expectedLineTimeUs,
                    $"Segment {segmentIndex}/{segmentCount}");

                totalTransferMs += segmentResult.TransferMs;
                totalAckWaitMs += segmentResult.AckWaitMs;
                var imageChunk = segmentResult.Buffer;

                Buffer.BlockCopy(imageChunk, 0, buffer, bufferOffset, segmentTargetBytes);
                bufferOffset += segmentTargetBytes;

                completedRows += segmentRows;
                var segmentElapsedMs = Math.Max(1L, segmentWatch.ElapsedMilliseconds);
                var transferPercent = (segmentResult.TransferMs * 100.0) / segmentElapsedMs;
                var ackWaitPercent = (segmentResult.AckWaitMs * 100.0) / segmentElapsedMs;
                Mark($"Segment {segmentIndex}/{segmentCount}: completed rows={completedRows}/{totalRows}, bytes={bufferOffset}/{totalTargetBytes}, elapsed={segmentElapsedMs} ms, transfer={segmentResult.TransferMs} ms ({transferPercent:F1}%), ack-wait={segmentResult.AckWaitMs} ms ({ackWaitPercent:F1}%)");
            }

            Mark($"Segmented scan image bytes={bufferOffset}/{totalTargetBytes}");
            if (bufferOffset != totalTargetBytes)
                throw new IOException($"619C transfer size mismatch: expected {totalTargetBytes}, actual {bufferOffset}");

            var totalTransferSeconds = Math.Max(totalTransferMs, 1L) / 1000.0;
            var totalMegabytes = totalTargetBytes / (1024.0 * 1024.0);
            var totalDataRateMBps = totalMegabytes / totalTransferSeconds;
            Mark($"Segmented scan totals: bytes={totalTargetBytes}, transfer={totalTransferMs} ms, ack-wait={totalAckWaitMs} ms, data-rate={totalDataRateMBps:F2} MiB/s");

            onStatus?.Invoke($"Scan completed in {segmentCount} segment(s).");
            Mark("Segmented scan completed");
            return new ScanStartResult(true, $"Scan completed in {segmentCount} segment(s).", buffer);
        }
        catch (OperationCanceledException)
        {
            Mark("Segmented scan canceled");
            return new ScanStartResult(false, "Scan stopped.", null);
        }
        catch (Exception ex)
        {
            Mark($"Segmented scan failed: {ex.Message}");
            return new ScanStartResult(false, $"Scan failed after {scanWatch.ElapsedMilliseconds} ms: {ex.Message}", null);
        }
    }

    private async Task<ScanSegmentResult> ExecuteScanSegmentAsync(
        IUsbBulkDuplexSession controlSession,
        IUsbBulkDuplexSession imageSession,
        int rows,
        ScanBulkInTransferOptions transferSettings,
        CancellationToken ct,
        string runId,
        Stopwatch scanWatch,
        Action<string>? onStatus,
        Action<string>? onDiagnostic,
        Action<int, int>? onProgress,
        int targetBytes,
        uint? expectedLineTimeUs,
        string? segmentLabel)
    {
        var doneAckWaitTimeoutMs = ResolveStartScanDoneAckWaitMs(rows, expectedLineTimeUs);
        var segmentPrefix = string.IsNullOrWhiteSpace(segmentLabel) ? string.Empty : $"{segmentLabel}: ";

        onStatus?.Invoke($"Configuring {rows} lines...");
        var setRowsFrame = await SendControlCommandAndWaitAckAsync(controlSession, _protocol.BuildSetScanLinesCommand(rows), ScanDebugConstants.UsbCmdSetScanLines, ScanDebugConstants.SetRowsAckTotalTimeoutMs, ct, true);
        var setRowsAck = _protocol.ParseScanAck(setRowsFrame);
        _protocol.EnsureAckOk(setRowsAck, rows, "SET_SCAN_LINES");
        onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] {segmentPrefix}SET_SCAN_LINES ACK received for {rows} rows");

        var readMode = transferSettings.ReadMode;
        var imageReadTask = await ArmImageReadAsync(imageSession, targetBytes, transferSettings, ct, runId, scanWatch, onDiagnostic, onProgress);
        onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] {segmentPrefix}619C {(readMode == ScanBulkInReadMode.MultiBuffered ? "multi-buffer" : "single-request")} read armed for {targetBytes} bytes");

        onStatus?.Invoke("Sending START_SCAN...");
        await controlSession.WriteBulkOutAsync(_protocol.BuildStartScanCommand(), ScanDebugConstants.AckTimeoutMs, ct);
        onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] {segmentPrefix}START_SCAN sent");

        var ackTask = _ackChannel.MonitorStartScanAcksAsync(rows, doneAckWaitTimeoutMs, ct, runId, scanWatch, onStatus, onDiagnostic);
        onStatus?.Invoke("Scan command sent, receiving image lines...");

        var transferWatch = Stopwatch.StartNew();
        var buffer = await imageReadTask;
        transferWatch.Stop();
        onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] {segmentPrefix}619C image bytes: {buffer.Length}/{targetBytes} (transfer={transferWatch.ElapsedMilliseconds} ms)");

        var ackWaitWatch = Stopwatch.StartNew();
        var gotDoneAck = await WaitForStartScanDoneAckAsync(ackTask, doneAckWaitTimeoutMs);
        ackWaitWatch.Stop();
        onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] {segmentPrefix}{(gotDoneAck ? "Done ACK observed" : "Done ACK wait timed out")} (ack-wait={ackWaitWatch.ElapsedMilliseconds} ms)");

        if (!gotDoneAck)
        {
            onStatus?.Invoke("START_SCAN done ACK not received in time.");
            await FinalizeAfterFailedImageReadAsync(controlSession, ackTask, ct, runId, scanWatch, onDiagnostic);
            throw new IOException($"{segmentPrefix}START_SCAN done ACK not received in time.");
        }

        if (buffer.Length != targetBytes)
            throw new IOException($"{segmentPrefix}619C transfer size mismatch: expected {targetBytes}, actual {buffer.Length}");

        return new ScanSegmentResult(buffer, transferWatch.ElapsedMilliseconds, ackWaitWatch.ElapsedMilliseconds);
    }

    public async Task<ScanStopResult> StopScanAsync(IUsbBulkDuplexSession controlSession, CancellationToken ct)
    {
        try
        {
            await controlSession.WriteBulkOutAsync(_protocol.BuildStopScanCommand(), ScanDebugConstants.AckTimeoutMs, ct);
            var stopAckFrame = await _ackChannel.ReadAckForCommandAsync(ScanDebugConstants.UsbCmdStopScan, ScanDebugConstants.StopAckTotalTimeoutMs, ct, true);
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

    public async Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(IUsbBulkDuplexSession controlSession, byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true)
    {
        await controlSession.WriteBulkOutAsync(command, ScanDebugConstants.AckTimeoutMs, ct);
        return await _ackChannel.ReadAckForCommandAsync(expectedCommand, totalTimeoutMs, ct, ignoreForeignCommands);
    }

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
            _ = await _ackChannel.ReadAckForCommandAsync(ScanDebugConstants.UsbCmdStopScan, ScanDebugConstants.StopAckTotalTimeoutMs, CancellationToken.None, true);
            onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] Finalize: STOP_SCAN sent and ACK received");
        }
        catch (Exception ex)
        {
            onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] Finalize: STOP_SCAN path failed: {ex.Message}");
        }

        try
        {
            var drained = await _ackChannel.DrainPendingAcksAsync(ScanDebugConstants.PreScanAckDrainWindowMs, ct);
            onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] Finalize: drained {drained} pending ACK frame(s)");
        }
        catch (Exception ex)
        {
            onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] Finalize: ACK drain failed: {ex.Message}");
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

    private static async Task<Task<byte[]>> ArmImageReadAsync(IUsbBulkDuplexSession imageSession, int expectedBytes, ScanBulkInTransferOptions transferSettings, CancellationToken ct, string runId, Stopwatch scanWatch, Action<string>? onDiagnostic, Action<int, int>? onProgress)
    {
        var readMode = transferSettings.ReadMode;
        for (var attempt = 1; attempt <= ScanDebugConstants.ImageReadArmMaxAttempts; attempt++)
        {
            var task = readMode == ScanBulkInReadMode.MultiBuffered
                ? imageSession.ReadBulkInExactMultiBufferedAsync(expectedBytes, transferSettings.RequestBytes, transferSettings.OutstandingReads, transferSettings.TimeoutMs, transferSettings.RawIoEnabled, ct, onProgress)
                : imageSession.ReadBulkInExactAsync(expectedBytes, transferSettings.TimeoutMs, ct, onProgress);

            await Task.Delay(ScanDebugConstants.ImageReadArmDelayMs, ct);
            if (!task.IsCompleted)
            {
                onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] 619C {readMode} read armed on attempt {attempt}");
                return task;
            }

            var chunk = await task;
            onDiagnostic?.Invoke($"[ScanDebug][run={runId}][{scanWatch.ElapsedMilliseconds,6} ms] 619C {readMode} read completed too early on attempt {attempt}, bytes={chunk.Length}");
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

    private static int ResolveStartScanDoneAckWaitMs(int rows, uint? expectedLineTimeUs)
    {
        if (rows <= 0 || expectedLineTimeUs is null || expectedLineTimeUs.Value == 0)
            return ScanDebugConstants.StartScanDoneAckWaitAfterImageMs;

        var expectedScanMs = Math.Ceiling((double)rows * expectedLineTimeUs.Value / 1000.0);
        var timeoutMs = (expectedScanMs * ScanDebugConstants.StartScanDoneAckWaitMultiplier) + ScanDebugConstants.StartScanDoneAckWaitPaddingMs;
        return (int)Math.Min(int.MaxValue, Math.Max(ScanDebugConstants.StartScanDoneAckWaitAfterImageMs, timeoutMs));
    }

    private sealed record ScanSegmentResult(byte[] Buffer, long TransferMs, long AckWaitMs);
}
