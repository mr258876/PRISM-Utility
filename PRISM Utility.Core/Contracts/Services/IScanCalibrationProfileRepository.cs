using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanCalibrationProfileRepository
{
    ScanCalibrationProfileRepositorySnapshot Snapshot { get; }
    Task InitializeAsync(CancellationToken ct);
    Task<ScanCalibrationProfileRepositorySnapshot> ReadAsync(CancellationToken ct);
    bool TryGetProfile(string channelRole, out ScanChannelCalibrationProfile profile);
    Task SaveProfileAsync(string channelRole, ScanChannelCalibrationProfile profile, CancellationToken ct);
    Task<bool> ClearProfileAsync(string channelRole, CancellationToken ct);
    Task ReplaceAsync(ScanCalibrationProfileRepositorySnapshot snapshot, CancellationToken ct);
    Task<string?> GetSelectedChannelAsync(CancellationToken ct);
    Task SetSelectedChannelAsync(string? channelRole, CancellationToken ct);
}
