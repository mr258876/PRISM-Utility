using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;
using LibUsbDotNet.WinUsb;
using PRISM_Utility.Core.Contracts.Services;

internal sealed class UsbBulkDuplexSession : IUsbBulkDuplexSession
{
    private readonly UsbDevice _dev;
    private readonly byte _interfaceId;
    private readonly UsbEndpointReader? _reader;
    private readonly UsbEndpointWriter? _writer;
    private readonly object _readLock = new();
    private readonly object _writeLock = new();
    private bool _disposed;

    public UsbBulkDuplexSession(UsbRegistry reg, byte configId, byte interfaceId, byte altId, byte? inEndpointAddress, byte? outEndpointAddress)
    {
        _interfaceId = interfaceId;

        if (!reg.Open(out UsbDevice dev) || dev is null)
            throw new InvalidOperationException("Open device failed (driver/permission/device removed).");

        _dev = dev;

        try
        {
            if (_dev is IUsbDevice wholeUsb)
            {
                wholeUsb.SetConfiguration(configId);
                wholeUsb.ClaimInterface(interfaceId);
                if (altId != 0)
                    throw new InvalidOperationException($"Alternate setting {altId} is required for interface {interfaceId}, but this LibUsbDotNet version cannot select alternate settings.");
            }

            if (inEndpointAddress is not null)
            {
                _reader = OpenBulkEndpointReader(inEndpointAddress.Value);
                if (_reader is null)
                    throw new InvalidOperationException($"Open bulk IN endpoint 0x{inEndpointAddress.Value:X2} failed.");
            }

            if (outEndpointAddress is not null)
            {
                _writer = OpenBulkEndpointWriter(outEndpointAddress.Value);
                if (_writer is null)
                    throw new InvalidOperationException($"Open bulk OUT endpoint 0x{outEndpointAddress.Value:X2} failed.");
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private UsbEndpointReader OpenBulkEndpointReader(byte endpointAddress)
    {
        try
        {
            var reader = _dev.OpenEndpointReader((ReadEndpointID)endpointAddress);
            if (reader.EndpointInfo is null)
                throw new InvalidOperationException($"Bulk IN endpoint 0x{endpointAddress:X2} is not exposed by the claimed interface/alternate setting.");

            return reader;
        }
        catch (NullReferenceException ex)
        {
            throw new InvalidOperationException($"Open bulk IN endpoint 0x{endpointAddress:X2} failed. Check the WinUSB/libusb driver binding for this interface.", ex);
        }
    }

    private UsbEndpointWriter OpenBulkEndpointWriter(byte endpointAddress)
    {
        try
        {
            var writer = _dev.OpenEndpointWriter((WriteEndpointID)endpointAddress);
            if (writer.EndpointInfo is null)
                throw new InvalidOperationException($"Bulk OUT endpoint 0x{endpointAddress:X2} is not exposed by the claimed interface/alternate setting.");

            return writer;
        }
        catch (NullReferenceException ex)
        {
            throw new InvalidOperationException($"Open bulk OUT endpoint 0x{endpointAddress:X2} failed. Check the WinUSB/libusb driver binding for this interface.", ex);
        }
    }

    public Task<(int transferred, byte[] data)> ReadBulkInOnceAsync(int bufferSize, int timeoutMs, CancellationToken ct)
    {
        if (bufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));
        if (timeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        if (_reader is null)
            throw new InvalidOperationException("Bulk IN endpoint is not opened for this session.");

        ThrowIfDisposed();

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            lock (_readLock)
            {
                var buf = new byte[bufferSize];
                var ec = _reader.Read(buf, timeoutMs, out var transferred);
                if (ec == ErrorCode.IoTimedOut && transferred <= 0)
                    return (0, Array.Empty<byte>());
                if (ec == ErrorCode.IoTimedOut)
                {
                    var timedOutCopy = new byte[transferred];
                    Buffer.BlockCopy(buf, 0, timedOutCopy, 0, transferred);
                    return (transferred, timedOutCopy);
                }
                if (ec != ErrorCode.None)
                    throw new IOException($"Read error: {ec}");

                var copy = new byte[transferred];
                Buffer.BlockCopy(buf, 0, copy, 0, transferred);
                return (transferred, copy);
            }
        }, ct);
    }

    public Task<byte[]> ReadBulkInExactAsync(int expectedBytes, int timeoutMs, CancellationToken ct, Action<int, int>? onProgress = null)
    {
        if (expectedBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedBytes));
        if (timeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        if (_reader is null)
            throw new InvalidOperationException("Bulk IN endpoint is not opened for this session.");

        ThrowIfDisposed();

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            lock (_readLock)
            {
                const int extraReadSlackBytes = 64 * 1024;
                var readBufferSize = checked(expectedBytes + extraReadSlackBytes);
                var buffer = new byte[readBufferSize];
                var ec = _reader.Read(buffer, timeoutMs, out var transferred);
                if (ec != ErrorCode.None)
                    throw new IOException($"Read error: {ec}");
                if (transferred > expectedBytes)
                    throw new IOException($"Long read: expected {expectedBytes}, actual {transferred}");
                if (transferred != expectedBytes)
                    throw new IOException($"Short read: expected {expectedBytes}, actual {transferred}");

                var exact = new byte[expectedBytes];
                Buffer.BlockCopy(buffer, 0, exact, 0, expectedBytes);
                onProgress?.Invoke(expectedBytes, expectedBytes);
                return exact;
            }
        }, ct);
    }

    public Task<byte[]> ReadBulkInExactMultiBufferedAsync(int expectedBytes, int transferSize, int maxOutstandingTransfers, int timeoutMs, bool rawIoEnabled, CancellationToken ct, Action<int, int>? onProgress = null)
    {
        if (expectedBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedBytes));
        if (transferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(transferSize));
        if (maxOutstandingTransfers <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxOutstandingTransfers));
        if (timeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        if (_reader is null)
            throw new InvalidOperationException("Bulk IN endpoint is not opened for this session.");

        ThrowIfDisposed();

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            lock (_readLock)
            {
                _reader.Reset();
                return ReadBulkInExactMultiBufferedCore(expectedBytes, transferSize, maxOutstandingTransfers, timeoutMs, rawIoEnabled, ct, onProgress);
            }
        }, ct);
    }

    public int? GetBulkInMaxTransferSize()
    {
        ThrowIfDisposed();
        lock (_readLock)
        {
            _ = TryConfigureWinUsbPipePolicies(1000, false, out var maxTransferSize);
            return maxTransferSize;
        }
    }

    public Task<int> WriteBulkOutAsync(byte[] data, int timeoutMs, CancellationToken ct)
    {
        if (data.Length == 0)
            return Task.FromResult(0);
        if (timeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        if (_writer is null)
            throw new InvalidOperationException("Bulk OUT endpoint is not opened for this session.");

        ThrowIfDisposed();

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            lock (_writeLock)
            {
                var ec = _writer.Write(data, timeoutMs, out var transferred);
                if (ec != ErrorCode.None)
                    throw new IOException($"Write error: {ec}");
                return transferred;
            }
        }, ct);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try { _reader?.Dispose(); } catch { }
        try { _writer?.Dispose(); } catch { }
        try
        {
            if (_dev is IUsbDevice wholeUsb)
                wholeUsb.ReleaseInterface(_interfaceId);
        }
        catch { }
        try { _dev.Close(); } catch { }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UsbBulkDuplexSession));
    }

    private byte[] ReadBulkInExactMultiBufferedCore(int expectedBytes, int transferSize, int maxOutstandingTransfers, int timeoutMs, bool rawIoEnabled, CancellationToken ct, Action<int, int>? onProgress)
    {
        var pipePolicies = TryConfigureWinUsbPipePolicies(timeoutMs, rawIoEnabled, out var maxTransferSize);
        var maxPacketSize = _reader?.EndpointInfo?.Descriptor?.MaxPacketSize ?? 512;
        var cappedTransferSize = maxTransferSize is > 0 ? Math.Min(transferSize, maxTransferSize.Value) : transferSize;
        var normalizedTransferSize = NormalizeTransferSize(cappedTransferSize, maxPacketSize);
        var slotCount = Math.Max(1, Math.Min(maxOutstandingTransfers, (expectedBytes + normalizedTransferSize - 1) / normalizedTransferSize));
        var imageBytes = new byte[expectedBytes];
        var slots = new TransferSlot[slotCount];
        var completedTransfers = new Dictionary<long, CompletedTransfer>();
        var totalTransferred = 0;
        var bytesSubmitted = 0;
        long nextSequenceToSubmit = 0;
        long nextSequenceToCommit = 0;

        try
        {
            for (var i = 0; i < slotCount; i++)
            {
                slots[i] = new TransferSlot(new byte[normalizedTransferSize], null, 0);
                if (!TrySubmitTransferSlot(slots[i], normalizedTransferSize, expectedBytes, ref bytesSubmitted, timeoutMs, nextSequenceToSubmit++, rawIoEnabled))
                    break;
            }

            while (totalTransferred < expectedBytes)
            {
                ct.ThrowIfCancellationRequested();

                var activeSlots = slots.Where(static slot => slot.Transfer is not null).ToArray();
                if (activeSlots.Length == 0)
                    throw new IOException($"Short read: expected {expectedBytes}, actual {totalTransferred}");

                var waitHandles = activeSlots.Select(static slot => slot.Transfer!.AsyncWaitHandle).ToArray();
                var signaledIndex = WaitHandle.WaitAny(waitHandles, timeoutMs);
                if (signaledIndex == WaitHandle.WaitTimeout)
                    throw new IOException("Read error: IoTimedOut");

                var slot = activeSlots[signaledIndex];
                var completedTransfer = slot.Transfer!;
                var completedSequence = slot.Sequence;
                var completedLogicalLength = slot.LogicalLength;
                var ec = completedTransfer.Wait(out var transferred);
                completedTransfer.Dispose();
                slot.Transfer = null;

                if (ec != ErrorCode.None && ec != ErrorCode.Success && !(ec == ErrorCode.IoTimedOut && transferred > 0))
                    throw new IOException($"Read error: {ec}");

                TrySubmitTransferSlot(slot, normalizedTransferSize, expectedBytes, ref bytesSubmitted, timeoutMs, nextSequenceToSubmit++, rawIoEnabled);
                completedTransfers[completedSequence] = new CompletedTransfer(slot.Buffer, transferred, completedLogicalLength);

                while (completedTransfers.Remove(nextSequenceToCommit, out var completed))
                {
                    if (completed.Transferred > completed.LogicalLength)
                        throw new IOException($"Long read: expected at most {completed.LogicalLength}, actual {completed.Transferred}");
                    if (completed.Transferred > expectedBytes - totalTransferred)
                        throw new IOException($"Long read: expected {expectedBytes}, actual {totalTransferred + completed.Transferred}");

                    if (completed.Transferred > 0)
                    {
                        Buffer.BlockCopy(completed.Buffer, 0, imageBytes, totalTransferred, completed.Transferred);
                        totalTransferred += completed.Transferred;
                        onProgress?.Invoke(totalTransferred, expectedBytes);
                    }

                    nextSequenceToCommit++;
                }
            }

            return imageBytes;
        }
        finally
        {
            foreach (var slot in slots)
            {
                if (slot.Transfer is null)
                    continue;

                try { if (!slot.Transfer.IsCompleted) slot.Transfer.Cancel(); } catch { }
                try { slot.Transfer.Dispose(); } catch { }
            }

            if (pipePolicies is not null)
            {
                try { pipePolicies.RawIo = false; } catch { }
            }
        }
    }

    private UsbTransfer SubmitAsyncRead(byte[] buffer, int length, int timeoutMs)
    {
        if (_reader is null)
            throw new InvalidOperationException("Bulk IN endpoint is not opened for this session.");

        var ec = _reader.SubmitAsyncTransfer(buffer, 0, length, timeoutMs, out var transfer);
        if (ec != ErrorCode.None && ec != ErrorCode.Success)
            throw new IOException($"Read submit error: {ec}");

        return transfer;
    }

    private bool TrySubmitTransferSlot(TransferSlot slot, int normalizedTransferSize, int expectedBytes, ref int bytesSubmitted, int timeoutMs, long sequence, bool rawIoEnabled)
    {
        var logicalRemaining = expectedBytes - bytesSubmitted;
        if (logicalRemaining <= 0)
            return false;

        var logicalLength = Math.Min(normalizedTransferSize, logicalRemaining);
        var requestLength = rawIoEnabled ? AlignUp(logicalLength, _reader?.EndpointInfo?.Descriptor?.MaxPacketSize ?? 512) : logicalLength;
        if (requestLength > slot.Buffer.Length)
            throw new IOException($"Request length {requestLength} exceeds slot capacity {slot.Buffer.Length}");

        slot.Sequence = sequence;
        slot.LogicalLength = logicalLength;
        slot.Transfer = SubmitAsyncRead(slot.Buffer, requestLength, timeoutMs);
        bytesSubmitted += logicalLength;
        return true;
    }

    private PipePolicies? TryConfigureWinUsbPipePolicies(int timeoutMs, bool rawIoEnabled, out int? maxTransferSize)
    {
        maxTransferSize = null;
        if (_dev is not WinUsbDevice winUsb || _reader is null)
            return null;

        try
        {
            var pipePolicies = winUsb.EndpointPolicies((ReadEndpointID)_reader.EpNum);
            pipePolicies.PipeTransferTimeout = timeoutMs;
            pipePolicies.RawIo = rawIoEnabled;
            maxTransferSize = pipePolicies.MaxTransferSize;
            return pipePolicies;
        }
        catch
        {
            return null;
        }
    }

    private static int NormalizeTransferSize(int transferSize, int maxPacketSize)
    {
        if (maxPacketSize <= 0)
            return transferSize;

        var remainder = transferSize % maxPacketSize;
        if (remainder == 0)
            return transferSize;

        var roundedDown = transferSize - remainder;
        return roundedDown > 0 ? roundedDown : transferSize;
    }

    private static int AlignUp(int value, int alignment)
    {
        if (alignment <= 0)
            return value;

        var remainder = value % alignment;
        if (remainder == 0)
            return value;

        return checked(value + (alignment - remainder));
    }

    private sealed class TransferSlot
    {
        public TransferSlot(byte[] buffer, UsbTransfer? transfer, long sequence)
        {
            Buffer = buffer;
            Transfer = transfer;
            Sequence = sequence;
        }

        public byte[] Buffer { get; }
        public UsbTransfer? Transfer { get; set; }
        public long Sequence { get; set; }
        public int LogicalLength { get; set; }
    }

    private sealed class CompletedTransfer
    {
        public CompletedTransfer(byte[] buffer, int transferred, int logicalLength)
        {
            Buffer = buffer;
            Transferred = transferred;
            LogicalLength = logicalLength;
        }

        public byte[] Buffer { get; }
        public int Transferred { get; }
        public int LogicalLength { get; }
    }
}
