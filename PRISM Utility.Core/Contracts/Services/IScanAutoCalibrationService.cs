using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanAutoCalibrationService
{
    Task<ScanParameterSnapshot> AutoBlackAdjustAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct);
    Task<ScanParameterSnapshot> AutoWhiteAdjustAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct);
    Task<ScanParameterSnapshot> AutoCalibrateAsync(IScanSessionService session, ScanParameterSnapshot currentSnapshot, Func<ScanCalibrationPrompt, Task<bool>> promptAsync, Action<string>? onStatus, Action<ScanParameterSnapshot>? onSnapshotApplied, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct);
}
