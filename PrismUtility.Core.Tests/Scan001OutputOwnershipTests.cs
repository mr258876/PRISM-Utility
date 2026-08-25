using Xunit;
using Xunit.Sdk;
using PRISM_Utility.Core.Services;

namespace PrismUtility.Core.Tests;

public sealed class Scan001OutputOwnershipTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Publish_TemporaryMoveFailure_RestoresTargetAndCleansArtifacts(bool targetExists)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "target.dng");
        if (targetExists) File.WriteAllText(target, "original");
        try
        {
            using var transaction = new ScanOutputFileTransaction(target, () => throw new IOException("injected"));
            File.WriteAllText(transaction.TemporaryPath, "replacement");
            Assert.Throws<IOException>(() => transaction.Publish(CancellationToken.None));
            transaction.Dispose();
            Assert.Equal(targetExists ? "original" : null, File.Exists(target) ? File.ReadAllText(target) : null);
            Assert.False(File.Exists(transaction.TemporaryPath));
            Assert.False(File.Exists($"{transaction.TemporaryPath}.backup"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Publish_BackupCleanupFailure_LeavesCommittedTargetAndBackupForLaterRetry()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "target.dng");
        File.WriteAllText(target, "original");
        try
        {
            using var transaction = new ScanOutputFileTransaction(target, beforeBackupDelete: () => throw new IOException("cleanup"));
            File.WriteAllText(transaction.TemporaryPath, "new");
            transaction.Publish(CancellationToken.None);
            Assert.Equal("new", File.ReadAllText(target));
            Assert.True(File.Exists($"{transaction.TemporaryPath}.backup"));
            transaction.Dispose();
            Assert.Equal("new", File.ReadAllText(target));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void PairPublish_FailureBetweenPublishes_RestoresBothTargets(bool firstExists, bool secondExists)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var first = Path.Combine(directory, "rgb.dng");
        var second = Path.Combine(directory, "irw.dng");
        if (firstExists) File.WriteAllText(first, "first");
        if (secondExists) File.WriteAllText(second, "second");
        try
        {
            using var transaction = new ScanOutputFilePairTransaction(first, second);
            File.WriteAllText(transaction.FirstTemporaryPath, "new-first");
            File.WriteAllText(transaction.SecondTemporaryPath, "new-second");
            Assert.Throws<InvalidOperationException>(() => transaction.Publish(CancellationToken.None, () => throw new InvalidOperationException("boundary")));
            Assert.Equal(firstExists ? "first" : null, File.Exists(first) ? File.ReadAllText(first) : null);
            Assert.Equal(secondExists ? "second" : null, File.Exists(second) ? File.ReadAllText(second) : null);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PairPublish_PublishFaultsAndCleanupFault_PreserveCommittedTargets(bool secondFault)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var first = Path.Combine(directory, "rgb.dng");
        var second = Path.Combine(directory, "irw.dng");
        File.WriteAllText(first, "first");
        File.WriteAllText(second, "second");
        try
        {
            using var failed = new ScanOutputFilePairTransaction(first, second, secondFault ? null : () => throw new IOException("first"), secondFault ? () => throw new IOException("second") : null);
            File.WriteAllText(failed.FirstTemporaryPath, "new-first");
            File.WriteAllText(failed.SecondTemporaryPath, "new-second");
            Assert.Throws<IOException>(() => failed.Publish(CancellationToken.None));
            Assert.Equal("first", File.ReadAllText(first));
            Assert.Equal("second", File.ReadAllText(second));

            using var committed = new ScanOutputFilePairTransaction(first, second, firstCleanupFault: () => throw new IOException("cleanup"));
            File.WriteAllText(committed.FirstTemporaryPath, "committed-first");
            File.WriteAllText(committed.SecondTemporaryPath, "committed-second");
            committed.Publish(CancellationToken.None);
            Assert.Equal("committed-first", File.ReadAllText(first));
            Assert.Equal("committed-second", File.ReadAllText(second));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("mono.dng")]
    [InlineData("raw4.dng")]
    [InlineData("rgb.dng")]
    [InlineData("irw.dng")]
    [InlineData("rgb.png")]
    public void CanceledPublish_PreservesExistingTarget(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, fileName);
        File.WriteAllBytes(target, "original"u8.ToArray());
        var expected = File.ReadAllBytes(target);

        try
        {
            var transaction = new ScanOutputFileTransaction(target);
            var temporaryPath = transaction.TemporaryPath;
            File.WriteAllBytes(temporaryPath, "replacement"u8.ToArray());
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => transaction.Publish(cancellation.Token));
            Assert.Equal(expected, File.ReadAllBytes(target));
            transaction.Dispose();
            Assert.False(File.Exists(temporaryPath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
