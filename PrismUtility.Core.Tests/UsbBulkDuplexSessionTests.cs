using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class UsbBulkDuplexSessionTests
{
    [Fact]
    public void SnapshotTransferBytes_CopiesCompletedBytesBeforeSlotReuse()
    {
        var source = new byte[] { 1, 2, 3, 4, 5 };

        var snapshot = UsbBulkDuplexSession.SnapshotTransferBytes(source, 3);
        source[0] = 9;
        source[1] = 8;
        source[2] = 7;

        Assert.Equal(new byte[] { 1, 2, 3 }, snapshot);
    }

    [Fact]
    public void SnapshotTransferBytes_ZeroTransfer_ReturnsEmptyArray()
    {
        var source = new byte[] { 1, 2, 3 };
        var snapshot = UsbBulkDuplexSession.SnapshotTransferBytes(source, 0);

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot);
    }

    [Fact]
    public void SnapshotTransferBytes_FullTransfer_MaintainsLength()
    {
        var source = new byte[64];
        for (var i = 0; i < source.Length; i++)
            source[i] = (byte)(i + 1);

        var snapshot = UsbBulkDuplexSession.SnapshotTransferBytes(source, source.Length);
        Assert.Equal(source.Length, snapshot.Length);
        Assert.Equal(source, snapshot);
    }

    [Fact]
    public void SnapshotTransferBytes_NegativeTransfer_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UsbBulkDuplexSession.SnapshotTransferBytes(new byte[8], -1));
    }

    [Fact]
    public void SnapshotTransferBytes_TransferExceedsBuffer_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UsbBulkDuplexSession.SnapshotTransferBytes(new byte[4], 5));
    }

    [Fact]
    public void ReportCompletedWholeRows_NotifiesOnlyWhenAdditionalWholeRowsAreAvailable()
    {
        var notifications = new List<int>();
        var imageBytes = new byte[ScanDebugConstants.BytesPerLine * 4];

        var reportedRows = UsbBulkDuplexSession.ReportCompletedWholeRows(ScanDebugConstants.BytesPerLine - 1, 0, imageBytes, (_, rows) => notifications.Add(rows));
        reportedRows = UsbBulkDuplexSession.ReportCompletedWholeRows(ScanDebugConstants.BytesPerLine, reportedRows, imageBytes, (_, rows) => notifications.Add(rows));
        reportedRows = UsbBulkDuplexSession.ReportCompletedWholeRows((2 * ScanDebugConstants.BytesPerLine) + 17, reportedRows, imageBytes, (_, rows) => notifications.Add(rows));
        _ = UsbBulkDuplexSession.ReportCompletedWholeRows((2 * ScanDebugConstants.BytesPerLine) + 99, reportedRows, imageBytes, (_, rows) => notifications.Add(rows));

        Assert.Equal(new[] { 1, 2 }, notifications);
    }

    [Fact]
    public void ReportCompletedWholeRows_NullHandler_ReturnsCurrentRowCount()
    {
        var imageBytes = new byte[ScanDebugConstants.BytesPerLine * 3];
        var result = UsbBulkDuplexSession.ReportCompletedWholeRows(ScanDebugConstants.BytesPerLine * 2, 0, imageBytes, null);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ReportCompletedWholeRows_NoNewWholeRows_DoesNotInvokeCallback()
    {
        var invoked = false;
        var imageBytes = new byte[ScanDebugConstants.BytesPerLine * 2];
        var result = UsbBulkDuplexSession.ReportCompletedWholeRows(ScanDebugConstants.BytesPerLine - 1, 1, imageBytes, (_, _) => invoked = true);
        Assert.Equal(1, result);
        Assert.False(invoked);
    }

    [Fact]
    public void ReportCompletedWholeRows_AdvancingByMultipleRows_NotifiesOncePerCall()
    {
        var rowNotifications = new List<int>();
        var imageBytes = new byte[ScanDebugConstants.BytesPerLine * 5];
        var reportedRows = 0;

        reportedRows = UsbBulkDuplexSession.ReportCompletedWholeRows(3 * ScanDebugConstants.BytesPerLine, reportedRows, imageBytes, (_, rows) => rowNotifications.Add(rows));

        Assert.Single(rowNotifications);
        Assert.Equal(3, rowNotifications[0]);
        Assert.Equal(3, reportedRows);
    }

    [Fact]
    public void SnapshotTransferBytes_Isolation_BufferMutationDoesNotAffectSnapshot()
    {
        var rng = new Random(42);
        var original = new byte[1024];
        rng.NextBytes(original);

        var snapshot = UsbBulkDuplexSession.SnapshotTransferBytes(original, original.Length);

        for (var i = 0; i < original.Length; i++)
            original[i] = (byte)(original[i] ^ 0xFF);

        Assert.Equal(1024, snapshot.Length);
        for (var i = 0; i < snapshot.Length; i++)
            Assert.Equal((byte)(snapshot[i] ^ 0xFF), original[i]);
    }
}
