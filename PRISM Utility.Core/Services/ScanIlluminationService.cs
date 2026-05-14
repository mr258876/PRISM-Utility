using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanIlluminationService : IScanIlluminationService
{
    public Task<ScanIlluminationState> GetStateAsync(IScanSessionService session, CancellationToken ct)
        => session.GetIlluminationStateAsync(ct);

    public async Task ApplyStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
    {
        await session.SetIlluminationLevelsAsync(state.Led1Level, state.Led2Level, state.Led3Level, state.Led4Level, ct);
        await session.SetSyncPulseClocksAsync(state.Led1PulseClock, state.Led2PulseClock, state.Led3PulseClock, state.Led4PulseClock, ct);
        await session.SetSteadyIlluminationAsync(state.SteadyMask, ct);
        await session.ConfigureExposureLightingAsync(state.SyncMask, ct);
    }

    public async Task ApplyStateWithSafeTransitionAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
    {
        var currentState = await session.GetIlluminationStateAsync(ct);
        if (currentState.SyncMask != 0)
            await session.ConfigureExposureLightingAsync(0, ct);

        if (currentState.SteadyMask != 0)
            await session.SetSteadyIlluminationAsync(0, ct);

        await ApplyStateAsync(session, state, ct);
    }

    public async Task ApplySingleChannelAsync(IScanSessionService session, ScanFilmAcquisitionSettings settings, byte ledIndex, CancellationToken ct)
    {
        var normalized = settings.Normalize();
        var levels = new ushort[ScanDebugConstants.IlluminationChannelCount];
        levels[ledIndex] = ledIndex switch
        {
            0 => normalized.Led1Level,
            1 => normalized.Led2Level,
            2 => normalized.Led3Level,
            3 => normalized.Led4Level,
            _ => 0
        };

        var ledMask = (byte)(1 << ledIndex);
        var syncMask = (byte)(normalized.SyncMask & ledMask);
        var steadyMask = (byte)(normalized.SteadyMask & ledMask);
        if (syncMask == 0 && steadyMask == 0)
            steadyMask = ledMask;

        await ApplyStateAsync(
            session,
            new ScanIlluminationState(
                levels[0],
                levels[1],
                levels[2],
                levels[3],
                steadyMask,
                syncMask,
                0,
                normalized.Led1PulseClock,
                normalized.Led2PulseClock,
                normalized.Led3PulseClock,
                normalized.Led4PulseClock),
            ct);
    }

    public async Task TurnOffAsync(IScanSessionService session, CancellationToken ct)
    {
        await session.ConfigureExposureLightingAsync(0, ct);
        await session.SetSteadyIlluminationAsync(0, ct);
    }

    public async Task RestoreStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
    {
        await session.SetIlluminationLevelsAsync(state.Led1Level, state.Led2Level, state.Led3Level, state.Led4Level, ct);
        await session.SetSteadyIlluminationAsync(state.SteadyMask, ct);
        await session.ConfigureExposureLightingAsync(state.SyncMask, ct);
        await session.SetSyncPulseClocksAsync(state.Led1PulseClock, state.Led2PulseClock, state.Led3PulseClock, state.Led4PulseClock, ct);
    }
}
