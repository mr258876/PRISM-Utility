using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Usb001")]
public sealed class Usb001ContractTests
{
    [Fact]
    public async Task RunBulkInLoopAsync_BlockedSession_CancelsAndRestartsWithoutStaleState()
    {
        await AssertCancellationRunAsync(cancellationAttempts: 1);
        await AssertCancellationRunAsync(cancellationAttempts: 3);
    }

    [Fact]
    public void StopBulkIn_RetirementContract_LeavesNoApiOrSourceDeclaration()
    {
        Assert.Null(typeof(IUsbService).GetMethod("StopBulkIn"));

        var remainingDeclarations = Directory
            .EnumerateFiles(FindHostSoftwareRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !string.Equals(Path.GetFileName(path), $"{nameof(Usb001ContractTests)}.cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("StopBulkIn", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(remainingDeclarations);
    }

    private static async Task AssertCancellationRunAsync(int cancellationAttempts)
    {
        using var cancellation = new CancellationTokenSource();
        using var session = new BlockingBulkInSession();
        var states = new List<BulkInState>();
        var progress = new RecordingProgress();

        var run = UsbService.RunBulkInLoopAsync(session, 64, progress, states.Add, cancellation.Token);

        await session.BlockedReadStarted.WaitAsync(TimeSpan.FromSeconds(5));

        for (var attempt = 0; attempt < cancellationAttempts; attempt++)
            cancellation.Cancel();

        await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(cancellation.Token, session.ReadCancellationToken);
        Assert.Equal(
            [BulkInState.Starting, BulkInState.Running, BulkInState.Stopping, BulkInState.Stopped],
            states);
        var report = Assert.Single(progress.Reports);
        Assert.Equal(2, report.transferred);
        Assert.Equal([0x10, 0x20], report.data);
    }

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility.Core")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the Host Software source root.");
    }

    private static bool IsBuildOutput(string path)
        => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private sealed class RecordingProgress : IProgress<(int transferred, byte[] data)>
    {
        public List<(int transferred, byte[] data)> Reports { get; } = [];

        public void Report((int transferred, byte[] data) value)
            => Reports.Add(value);
    }

    private sealed class BlockingBulkInSession : IUsbBulkDuplexSession
    {
        private readonly TaskCompletionSource _blockedReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readCount;

        public CancellationToken ReadCancellationToken { get; private set; }
        public Task BlockedReadStarted => _blockedReadStarted.Task;

        public async Task<(int transferred, byte[] data)> ReadBulkInOnceAsync(int bufferSize, int timeoutMs, CancellationToken ct)
        {
            ReadCancellationToken = ct;

            if (Interlocked.Increment(ref _readCount) == 1)
                return (2, [0x10, 0x20]);

            _blockedReadStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
            return (0, Array.Empty<byte>());
        }

        public Task<byte[]> ReadBulkInExactAsync(int expectedBytes, int timeoutMs, CancellationToken ct, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null)
            => throw new NotSupportedException();

        public Task<byte[]> ReadBulkInExactMultiBufferedAsync(int expectedBytes, int transferSize, int maxOutstandingTransfers, int timeoutMs, bool rawIoEnabled, CancellationToken ct, Action<int, int>? onProgress = null, ScanRowsAvailableHandler? onRowsAvailable = null)
            => throw new NotSupportedException();

        public int? GetBulkInMaxTransferSize()
            => throw new NotSupportedException();

        public Task<int> WriteBulkOutAsync(byte[] data, int timeoutMs, CancellationToken ct)
            => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
