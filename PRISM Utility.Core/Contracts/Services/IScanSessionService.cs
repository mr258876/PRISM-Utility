using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanSessionService : IDisposable, IAsyncDisposable
{
    event EventHandler? TargetsChanged;
    event Action<ScanMotorState>? MotionEventReceived;

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
    Task<ScanIlluminationState> GetIlluminationStateAsync(CancellationToken ct);
    Task SetIlluminationLevelsAsync(ushort led1Level, ushort led2Level, ushort led3Level, ushort led4Level, CancellationToken ct);
    Task SetSteadyIlluminationAsync(byte steadyMask, CancellationToken ct);
    Task ConfigureExposureLightingAsync(byte syncMask, CancellationToken ct);
    Task SetSyncPulseClocksAsync(uint led1PulseClock, uint led2PulseClock, uint led3PulseClock, uint led4PulseClock, CancellationToken ct);
    Task<IReadOnlyList<ScanMotorState>> GetMotionStateAsync(CancellationToken ct);
    Task SetMotorEnabledAsync(byte motorId, bool enabled, CancellationToken ct);
    Task MoveMotorStepsAsync(byte motorId, bool direction, uint steps, uint intervalUs, CancellationToken ct);
    Task PrepareMotorOnExposureSyncAsync(byte motorId, bool direction, uint steps, uint intervalUs, CancellationToken ct);
    Task<ScanMotorState> WaitForMotorMotionCompleteAsync(byte motorId, uint steps, uint intervalUs, CancellationToken ct);
    Task<ScanMotorState> MoveMotorStepsAndWaitForCompletionAsync(byte motorId, bool direction, uint steps, uint intervalUs, CancellationToken ct);
    Task StopMotorAsync(byte motorId, CancellationToken ct);
    Task ApplyMotorConfigAsync(byte motorId, CancellationToken ct);
    Task<ScanOperationResult> SetWarmUpEnabledAsync(bool enabled, CancellationToken ct);
    Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null);
    Task<ScanStartResult> StartWarmUpSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null);
    Task<ScanStopResult> StopScanAsync(CancellationToken ct);
    Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true);
}
