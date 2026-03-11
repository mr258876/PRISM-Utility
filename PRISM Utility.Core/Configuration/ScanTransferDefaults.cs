using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Configuration;

public static class ScanTransferDefaults
{
    public static readonly ScanBulkInTransferOptions Settings = new(
        ScanBulkInReadMode.MultiBuffered,
        512 * 1024,
        4,
        1000,
        true);
}
