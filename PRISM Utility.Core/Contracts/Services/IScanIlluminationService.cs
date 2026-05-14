using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanIlluminationService
{
    Task<ScanIlluminationState> GetStateAsync(IScanSessionService session, CancellationToken ct);

    Task ApplyStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct);

    Task ApplyStateWithSafeTransitionAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct);

    Task ApplySingleChannelAsync(IScanSessionService session, ScanFilmAcquisitionSettings settings, byte ledIndex, CancellationToken ct);

    Task TurnOffAsync(IScanSessionService session, CancellationToken ct);

    Task RestoreStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct);
}
