namespace PRISM_Utility.Core.Models;

public enum WhiteLightSource
{
    DedicatedLed = 0,
    RgbComposite = 1
}

public enum WhiteLightDriveMode
{
    Off = 0,
    Steady = 1,
    Synchronized = 2
}

public sealed record WhiteLightComponentSettings(
    ushort Level = 0,
    uint PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock,
    WhiteLightDriveMode Mode = WhiteLightDriveMode.Steady);

/// <summary>Illumination values follow logical lamp roles, not physical LED socket numbers.</summary>
public sealed record WhiteChannelLightingSettings(
    WhiteLightComponentSettings? DedicatedLed = null,
    WhiteLightComponentSettings? Red = null,
    WhiteLightComponentSettings? Green = null,
    WhiteLightComponentSettings? Blue = null);

public sealed record WhiteChannelSourceSettings(
    ScanChannelCalibrationProfile? Profile = null,
    WhiteChannelLightingSettings? Lighting = null);

public sealed record WhiteChannelSettings(
    WhiteLightSource Source = WhiteLightSource.DedicatedLed,
    WhiteChannelSourceSettings? DedicatedLed = null,
    WhiteChannelSourceSettings? RgbComposite = null)
{
    [Newtonsoft.Json.JsonIgnore]
    public WhiteChannelSourceSettings? ActiveSettings => GetSourceSettings(Source);

    public WhiteChannelSourceSettings? GetSourceSettings(WhiteLightSource source)
        => source switch
        {
            WhiteLightSource.DedicatedLed => DedicatedLed,
            WhiteLightSource.RgbComposite => RgbComposite,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };

    public WhiteChannelSettings WithSourceSettings(WhiteLightSource source, WhiteChannelSourceSettings? settings)
        => source switch
        {
            WhiteLightSource.DedicatedLed => this with { DedicatedLed = settings },
            WhiteLightSource.RgbComposite => this with { RgbComposite = settings },
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };

    public WhiteChannelSettings WithActiveProfile(ScanChannelCalibrationProfile? profile)
        => profile is null && ActiveSettings is null ? this
            : WithSourceSettings(Source, (ActiveSettings ?? new()) with { Profile = profile });

    public WhiteChannelSettings WithActiveLighting(WhiteChannelLightingSettings? lighting)
        => WithSourceSettings(Source, (ActiveSettings ?? new()) with { Lighting = lighting });

    public static WhiteChannelSettings FromLegacy(
        ScanChannelCalibrationProfile? profile,
        ScanFilmAcquisitionSettings? acquisition = null)
        => new(DedicatedLed: profile is null ? null : new(profile,
            acquisition is null ? null : new(DedicatedLed: FindLegacyWhiteComponent(acquisition))));

    private static WhiteLightComponentSettings? FindLegacyWhiteComponent(ScanFilmAcquisitionSettings acquisition)
    {
        var component = FindComponent(acquisition, "White");
        // Legacy single-channel capture enabled steady light when neither mask selected the lamp.
        return component is { Mode: WhiteLightDriveMode.Off }
            ? component with { Mode = WhiteLightDriveMode.Steady }
            : component;
    }

    public static WhiteLightComponentSettings? FindComponent(ScanFilmAcquisitionSettings acquisition, string role)
    {
        var roles = new[] { acquisition.Led1ChannelColor, acquisition.Led2ChannelColor, acquisition.Led3ChannelColor, acquisition.Led4ChannelColor };
        var indexes = Enumerable.Range(0, roles.Length).Where(i => string.Equals(roles[i], role, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (indexes.Length != 1)
            return null;
        var index = indexes[0];
        var levels = new[] { acquisition.Led1Level, acquisition.Led2Level, acquisition.Led3Level, acquisition.Led4Level };
        var pulses = new[] { acquisition.Led1PulseClock, acquisition.Led2PulseClock, acquisition.Led3PulseClock, acquisition.Led4PulseClock };
        var bit = 1 << index;
        return new(levels[index], pulses[index], (acquisition.SteadyMask & bit) != 0
            ? WhiteLightDriveMode.Steady
            : (acquisition.SyncMask & bit) != 0 ? WhiteLightDriveMode.Synchronized : WhiteLightDriveMode.Off);
    }
}
