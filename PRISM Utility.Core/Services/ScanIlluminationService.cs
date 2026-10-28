using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanIlluminationService : IScanIlluminationService
{
    public Task<ScanIlluminationState> GetStateAsync(IScanSessionService session, CancellationToken ct)
        => session.GetIlluminationStateAsync(ct);

    public async Task ApplyStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
    {
        ValidateState(state);
        await ApplyValidatedStateAsync(session, state, ct);
    }

    public async Task ApplyStateWithSafeTransitionAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
    {
        ValidateState(state);
        var currentState = await session.GetIlluminationStateAsync(ct);
        if (currentState.SyncMask != 0)
            await session.ConfigureExposureLightingAsync(0, ct);

        if (currentState.SteadyMask != 0)
            await session.SetSteadyIlluminationAsync(0, ct);

        await ApplyValidatedStateAsync(session, state, ct);
    }

    public Task ApplySingleChannelAsync(IScanSessionService session, ScanFilmAcquisitionSettings settings, byte ledIndex, CancellationToken ct)
        => ApplyStateAsync(session, BuildSingleChannelState(settings, ledIndex), ct);

    /// <summary>Resolves a logical capture channel to the physical illumination outputs.</summary>
    public static ScanIlluminationState BuildChannelState(
        ScanFilmAcquisitionSettings settings,
        string channelRole,
        byte channelIndex,
        WhiteChannelSettings? whiteChannel = null)
    {
        whiteChannel ??= settings.WhiteChannelSettings;
        if (!string.Equals(channelRole, "White", StringComparison.OrdinalIgnoreCase))
            return BuildSingleChannelState(settings, channelIndex);

        var roles = new[] { settings.Led1ChannelColor, settings.Led2ChannelColor, settings.Led3ChannelColor, settings.Led4ChannelColor };
        int ResolveLed(string role)
        {
            var indices = Enumerable.Range(0, roles.Length)
                .Where(index => string.Equals(roles[index], role, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (indices.Length != 1)
                throw new InvalidOperationException($"The selected white illumination source requires exactly one mapped {role} LED.");
            return indices[0];
        }

        // A legacy white profile is dedicated-white, never an implicit RGB substitute.
        if (whiteChannel is null)
            return BuildSingleChannelState(settings, (byte)ResolveLed("White"));

        var lighting = whiteChannel.Source switch
        {
            WhiteLightSource.DedicatedLed => whiteChannel.DedicatedLed?.Lighting ?? new WhiteChannelLightingSettings(),
            WhiteLightSource.RgbComposite => whiteChannel.RgbComposite?.Lighting ?? new WhiteChannelLightingSettings(),
            _ => throw new InvalidOperationException("The selected white illumination source is not supported.")
        };
        var levels = new ushort[ScanDebugConstants.IlluminationChannelCount];
        var pulses = Enumerable.Repeat(ScanDebugConstants.IlluminationMinSyncPulseClock, ScanDebugConstants.IlluminationChannelCount).ToArray();
        byte steadyMask = 0;
        byte syncMask = 0;

        void AddComponent(string role, WhiteLightComponentSettings? configuredComponent)
        {
            var component = configuredComponent ?? new WhiteLightComponentSettings();
            var index = ResolveLed(role);
            levels[index] = component.Level;
            pulses[index] = component.PulseClock;
            switch (component.Mode)
            {
                case WhiteLightDriveMode.Off:
                    break;
                case WhiteLightDriveMode.Steady:
                    steadyMask |= (byte)(1 << index);
                    break;
                case WhiteLightDriveMode.Synchronized:
                    syncMask |= (byte)(1 << index);
                    break;
                default:
                    throw new InvalidOperationException("The selected white illumination drive mode is not supported.");
            }
        }

        if (whiteChannel.Source == WhiteLightSource.DedicatedLed)
            AddComponent("White", lighting.DedicatedLed);
        else
        {
            AddComponent("Red", lighting.Red);
            AddComponent("Green", lighting.Green);
            AddComponent("Blue", lighting.Blue);
        }

        var state = new ScanIlluminationState(
            levels[0], levels[1], levels[2], levels[3], steadyMask, syncMask, 0,
            pulses[0], pulses[1], pulses[2], pulses[3]);
        ValidateState(state);
        return state;
    }

    private static ScanIlluminationState BuildSingleChannelState(ScanFilmAcquisitionSettings settings, byte ledIndex)
    {
        if (ledIndex >= ScanDebugConstants.IlluminationChannelCount)
            throw new ArgumentOutOfRangeException(nameof(ledIndex));

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

        return new ScanIlluminationState(
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
                normalized.Led4PulseClock);
    }

    public async Task TurnOffAsync(IScanSessionService session, CancellationToken ct)
    {
        await session.ConfigureExposureLightingAsync(0, ct);
        await session.SetSteadyIlluminationAsync(0, ct);
    }

    public async Task RestoreStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
    {
        ValidateState(state);
        await session.ConfigureExposureLightingAsync(0, ct);
        await session.SetSteadyIlluminationAsync(0, ct);
        await session.SetIlluminationLevelsAsync(state.Led1Level, state.Led2Level, state.Led3Level, state.Led4Level, ct);
        await session.SetSyncPulseClocksAsync(state.Led1PulseClock, state.Led2PulseClock, state.Led3PulseClock, state.Led4PulseClock, ct);
        await session.SetSteadyIlluminationAsync(state.SteadyMask, ct);
        await session.ConfigureExposureLightingAsync(state.SyncMask, ct);
    }

    private static void ValidateState(ScanIlluminationState state)
        => ScanIlluminationValidator.ValidateState(state, "rawIllumination").ThrowIfInvalid();

    private static async Task ApplyValidatedStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
    {
        await session.SetIlluminationLevelsAsync(state.Led1Level, state.Led2Level, state.Led3Level, state.Led4Level, ct);
        await session.SetSyncPulseClocksAsync(state.Led1PulseClock, state.Led2PulseClock, state.Led3PulseClock, state.Led4PulseClock, ct);
        await session.SetSteadyIlluminationAsync(state.SteadyMask, ct);
        await session.ConfigureExposureLightingAsync(state.SyncMask, ct);
    }
}
