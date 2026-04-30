namespace PRISM_Utility.Core.Contracts.Services;

public interface IUsbUsageCoordinator
{
    bool IsScanDebugInUse { get; }

    bool IsUsbDebugInUse { get; }

    void SetScanDebugInUse(bool inUse);

    void SetUsbDebugInUse(bool inUse);
}
