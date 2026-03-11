namespace PRISM_Utility.Core.Models;

public enum ScanBulkInReadMode
{
    SingleRequest = 0,
    MultiBuffered = 1
}

public sealed record ScanBulkInTransferOptions(
    ScanBulkInReadMode ReadMode,
    int RequestBytes,
    int OutstandingReads,
    int TimeoutMs,
    bool RawIoEnabled);
