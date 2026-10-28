using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

internal static class WhiteChannelSettingsValidation
{
    public static bool TryValidate(WhiteChannelSettings? settings, out string fieldPath)
    {
        const string prefix = "acquisitionSettings.whiteChannelSettings";
        fieldPath = prefix;
        if (settings is null)
            return true;
        if (!Enum.IsDefined(settings.Source))
        {
            fieldPath = $"{prefix}.source";
            return false;
        }
        foreach (var source in new[] { WhiteLightSource.DedicatedLed, WhiteLightSource.RgbComposite })
        {
            var slot = settings.GetSourceSettings(source);
            var path = $"{prefix}.{(source == WhiteLightSource.DedicatedLed ? "dedicatedLed" : "rgbComposite")}";
            if (slot?.Profile is { } profile && !ScanCalibrationProfileSettingsNormalizer.TryValidateProfile(profile, out _))
            {
                fieldPath = $"{path}.profile";
                return false;
            }
            if (slot?.Lighting is not { } lighting)
                continue;
            foreach (var component in new[] { lighting.DedicatedLed, lighting.Red, lighting.Green, lighting.Blue })
            {
                if (component is not null && (!Enum.IsDefined(component.Mode)
                    || component.PulseClock < ScanDebugConstants.IlluminationMinSyncPulseClock))
                {
                    fieldPath = $"{path}.lighting";
                    return false;
                }
            }
        }
        return true;
    }
}
