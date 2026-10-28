using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanOutputFileSetTransactionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"prism-output-set-{Guid.NewGuid():N}");

    public ScanOutputFileSetTransactionTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Publish_ThreeOutputsReplaceExistingFilesAndKeepEveryChannel()
    {
        var paths = Targets();
        File.WriteAllText(paths[0], "old-rgb");
        using (var transaction = new ScanOutputFileSetTransaction(paths))
        {
            WriteTemporaryOutputs(transaction);
            transaction.Publish(CancellationToken.None);
        }

        Assert.Equal(["new-0", "new-1", "new-2"], paths.Select(File.ReadAllText));
        Assert.Equal(3, Directory.GetFiles(_directory).Length);
    }

    [Fact]
    public void Publish_ThirdOutputFailureRestoresExistingFilesAndRemovesNewOutput()
    {
        var paths = Targets();
        File.WriteAllText(paths[0], "old-rgb");
        File.WriteAllText(paths[2], "old-white");
        using (var transaction = new ScanOutputFileSetTransaction(paths))
        {
            WriteTemporaryOutputs(transaction);
            File.Delete(transaction.TemporaryPaths[2]);

            Assert.Throws<FileNotFoundException>(() => transaction.Publish(CancellationToken.None));
        }

        Assert.Equal("old-rgb", File.ReadAllText(paths[0]));
        Assert.False(File.Exists(paths[1]));
        Assert.Equal("old-white", File.ReadAllText(paths[2]));
        Assert.Equal(2, Directory.GetFiles(_directory).Length);
    }

    [Fact]
    public void Publish_CanceledAfterTwoOutputsRestoresWholeSet()
    {
        var paths = Targets();
        foreach (var path in paths) File.WriteAllText(path, "old");
        using var cancellation = new CancellationTokenSource();
        using (var transaction = new ScanOutputFileSetTransaction(paths))
        {
            WriteTemporaryOutputs(transaction);

            Assert.Throws<OperationCanceledException>(() => transaction.Publish(cancellation.Token, index =>
            {
                if (index == 1) cancellation.Cancel();
            }));
        }

        Assert.All(paths, path => Assert.Equal("old", File.ReadAllText(path)));
        Assert.Equal(3, Directory.GetFiles(_directory).Length);
    }

    [Fact]
    public void Constructor_DuplicateTargetsCreatesNoTemporaryFiles()
    {
        var path = Targets()[0];

        Assert.Throws<ArgumentException>(() => new ScanOutputFileSetTransaction([path, path]));

        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public void Constructor_LaterReservationFailsAndCleansEarlierTemporaryFiles()
    {
        Assert.Throws<DirectoryNotFoundException>(() => new ScanOutputFileSetTransaction(
            [Targets()[0], Path.Combine(_directory, "missing", "white.dng")]));

        Assert.Empty(Directory.GetFiles(_directory));
    }

    private string[] Targets() => [Path.Combine(_directory, "scan_rgb.dng"), Path.Combine(_directory, "scan_ir.dng"), Path.Combine(_directory, "scan_white.dng")];

    private static void WriteTemporaryOutputs(ScanOutputFileSetTransaction transaction)
    {
        for (var index = 0; index < transaction.TemporaryPaths.Count; index++)
            File.WriteAllText(transaction.TemporaryPaths[index], $"new-{index}");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
