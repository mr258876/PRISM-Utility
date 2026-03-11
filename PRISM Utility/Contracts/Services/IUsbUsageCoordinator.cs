namespace PRISM_Utility.Contracts.Services;

public interface IUsbUsageCoordinator
{
    bool IsScanDebugInUse { get; }

    bool IsUsbDebugInUse { get; }

    void SetScanDebugInUse(bool inUse);

    void SetUsbDebugInUse(bool inUse);
}
