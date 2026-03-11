using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanSessionService
{
    event EventHandler? TargetsChanged;

    ScanTargetState Targets
    {
        get;
    }
    bool IsConnected
    {
        get;
    }
    int SingleTransferMaxRows { get; }
    CancellationToken ConnectionToken
    {
        get;
    }

    void RefreshTargets();
    Task<ScanOperationResult> ConnectAsync(CancellationToken ct);
    Task DisconnectAsync();
    Task<ScanOperationResult> SetWarmUpEnabledAsync(bool enabled, CancellationToken ct);
    Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null);
    Task<ScanStartResult> StartWarmUpSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null);
    Task<ScanStopResult> StopScanAsync(CancellationToken ct);
    Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true);
}
