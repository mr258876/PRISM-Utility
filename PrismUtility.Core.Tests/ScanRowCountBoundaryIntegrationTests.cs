using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanRowCountBoundaryIntegrationTests
{
    public static IEnumerable<object[]> InvalidHostRows()
    {
        yield return [0];
        yield return [-1];
        yield return [282_459];
        yield return [checked(ScanRowCountValidation.MaxHostRows + 1)];
        yield return [int.MaxValue];
    }

    [Theory]
    [MemberData(nameof(InvalidHostRows))]
    public async Task ExecutionRunner_InvalidRowsRejectBeforeTransferSettingsAckDrainControlOrImageIo(int rows)
    {
        var protocol = new ScanProtocolService();
        var transferSettings = new CountingTransferSettings();
        var runner = new ScanExecutionRunner(protocol, transferSettings, new ScanAckChannel(protocol));
        var control = new CountingUsbSession();
        var image = new CountingUsbSession();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.StartScanAsync(control, image, rows, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.StartSegmentedScanAsync(control, image, rows, 1, CancellationToken.None));

        Assert.Equal(0, transferSettings.InitializeCallCount);
        Assert.Equal(0, control.IoCallCount);
        Assert.Equal(0, image.IoCallCount);
    }

    [Fact]
    public void Protocol_ValidNormalRowsRetainExactSetScanLinesFrame()
    {
        var frame = new ScanProtocolService().BuildSetScanLinesCommand(ScanDebugConstants.MaxRows);

        Assert.Equal([0xA5, 0x31, 0x04, 0x00, 0x89, 0x00, 0x00, 0x00], frame);
    }

    private sealed class CountingTransferSettings : IScanTransferSettingsService
    {
        public event EventHandler? BulkInReadModeChanged;

        public int InitializeCallCount { get; private set; }
        public ScanBulkInReadMode BulkInReadMode => ScanBulkInReadMode.SingleRequest;
        public ScanBulkInTransferOptions DefaultSettings { get; } = new(ScanBulkInReadMode.SingleRequest, 16 * 1024, 1, ScanDebugConstants.ImageReadTimeoutMs, false);
        public ScanBulkInTransferOptions Settings => DefaultSettings;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCallCount++;
            _ = BulkInReadModeChanged;
            return Task.CompletedTask;
        }

        public Task SetBulkInReadModeAsync(ScanBulkInReadMode mode, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SetSettingsAsync(ScanBulkInTransferOptions settings, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class CountingUsbSession : IUsbBulkDuplexSession
    {
        public int IoCallCount { get; private set; }

        public Task<(int transferred, byte[] data)> ReadBulkInOnceAsync(int bufferSize, int timeoutMs, CancellationToken ct)
        {
            IoCallCount++;
            return Task.FromException<(int transferred, byte[] data)>(new InvalidOperationException("Unexpected USB I/O."));
        }

        public Task<byte[]> ReadBulkInExactAsync(int expectedBytes, int timeoutMs, CancellationToken ct, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null)
        {
            IoCallCount++;
            return Task.FromException<byte[]>(new InvalidOperationException("Unexpected USB I/O."));
        }

        public Task<byte[]> ReadBulkInExactMultiBufferedAsync(int expectedBytes, int transferSize, int maxOutstandingTransfers, int timeoutMs, bool rawIoEnabled, CancellationToken ct, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null)
        {
            IoCallCount++;
            return Task.FromException<byte[]>(new InvalidOperationException("Unexpected USB I/O."));
        }

        public int? GetBulkInMaxTransferSize() => null;

        public Task<int> WriteBulkOutAsync(byte[] data, int timeoutMs, CancellationToken ct)
        {
            IoCallCount++;
            return Task.FromException<int>(new InvalidOperationException("Unexpected USB I/O."));
        }

        public void Dispose()
        {
        }
    }
}
