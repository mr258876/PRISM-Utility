using System.Globalization;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Helpers;

public static class ScanMotorDistanceText
{
    public const string StepsUnit = "steps";
    public const string MicrometersUnit = "um";
    public const string MillimetersUnit = "mm";

    public static string NormalizeUnit(string? unit)
        => unit?.Trim().ToLowerInvariant() switch
        {
            StepsUnit => StepsUnit,
            MicrometersUnit => MicrometersUnit,
            _ => MillimetersUnit
        };

    public static bool TryParseMillimeters(string? valueText, string? unit, ScanMotorMechanicalSettings motorSettings, out double distanceMm)
    {
        distanceMm = 0.0;
        if (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || parsed <= 0.0)
        {
            return false;
        }

        distanceMm = NormalizeUnit(unit) switch
        {
            StepsUnit => parsed / Math.Max(ScanTimingMath.ComputeMotorStepsPerMillimeter(motorSettings), double.Epsilon),
            MicrometersUnit => parsed / 1000.0,
            _ => parsed
        };

        return double.IsFinite(distanceMm) && distanceMm > 0.0;
    }

    public static bool TryFormatDisplayValue(double distanceMm, string? unit, ScanMotorMechanicalSettings motorSettings, out string valueText)
    {
        valueText = string.Empty;
        if (!double.IsFinite(distanceMm) || distanceMm <= 0.0)
            return false;

        var converted = NormalizeUnit(unit) switch
        {
            StepsUnit => distanceMm * ScanTimingMath.ComputeMotorStepsPerMillimeter(motorSettings),
            MicrometersUnit => distanceMm * 1000.0,
            _ => distanceMm
        };

        if (!double.IsFinite(converted) || converted <= 0.0)
            return false;

        valueText = converted.ToString("0.#########", CultureInfo.InvariantCulture);
        return true;
    }
}
