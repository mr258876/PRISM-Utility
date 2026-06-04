namespace PRISM_Utility.Core.Models;

public enum ScannerAccessMode
{
    None = 0,
    ScanWorkflow = 1,
    ScanDebug = 2,
    UsbDebugRaw = 3,
    Unknown = 4
}

public enum ScannerAccessAvailability
{
    Available = 0,
    NoDevice = 1,
    Active = 2,
    Connecting = 3,
    Running = 4,
    ReconnectRequired = 5,
    Faulted = 6,
    BlockedByScanWorkflow = 7,
    BlockedByScanDebug = 8,
    BlockedByUsbDebug = 9,
    BlockedByUnknownOwner = 10
}

public sealed record ScannerAccessSnapshot(
    ScannerAccessMode ActiveMode,
    ScannerAccessAvailability Availability,
    ScannerDeviceSessionSnapshot ScannerSession,
    UsbUsageLeaseSnapshot? UsbLease,
    ScanTargetState Targets,
    string BlockedReason)
{
    public bool IsScannerConnected => ScannerSession.State is ScannerSessionState.Connected or ScannerSessionState.Running;

    public bool IsRawUsbActive => UsbLease?.OwnerType == UsbUsageOwnerType.RawUsb;
}
