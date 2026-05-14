using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Helpers;

public static class ScanTimingMath
{
    private const double ExposureBaseTicks = 45827.0;
    private const double ExposureTickScale = 6.0;
    private const double NanosecondsPerSecond = 1_000_000_000.0;
    private const double NanosecondsPerMicrosecond = 1000.0;
    private const double MicrosecondsPerSecond = 1_000_000.0;
    private const double MicrometersPerMillimeter = 1000.0;
    private const double KhzToHzScale = 1_000_000.0;

    public static double ExposureTicksToNanoseconds(ushort exposureTicks, uint sysClockKhz)
        => (ExposureBaseTicks + (exposureTicks * ExposureTickScale)) * (KhzToHzScale / Math.Max(sysClockKhz, 1u));

    public static uint ExposureTicksToNanosecondsFloor(ushort exposureTicks, uint sysClockKhz)
        => (uint)Math.Max(1, Math.Floor(ExposureTicksToNanoseconds(exposureTicks, sysClockKhz)));

    public static double ExposureTicksToMicroseconds(ushort exposureTicks, uint sysClockKhz)
        => ExposureTicksToNanoseconds(exposureTicks, sysClockKhz) / NanosecondsPerMicrosecond;

    public static uint ExposureTicksToMicrosecondsCeil(ushort exposureTicks, uint sysClockKhz)
        => (uint)Math.Max(1, (int)Math.Ceiling(ExposureTicksToMicroseconds(exposureTicks, sysClockKhz)));

    public static ushort NanosecondsToExposureTicks(double nanoseconds, uint sysClockKhz)
    {
        var ticks = ((nanoseconds * Math.Max(sysClockKhz, 1u)) / KhzToHzScale - ExposureBaseTicks) / ExposureTickScale;
        var rounded = (int)Math.Round(ticks, MidpointRounding.AwayFromZero);
        return (ushort)Math.Clamp(rounded, ScanDebugConstants.MinExposureTicks, ushort.MaxValue);
    }

    public static double ComputeScanDurationMicroseconds(int rows, ushort exposureTicks, uint sysClockKhz)
        => Math.Max(rows, 0) * ExposureTicksToMicroseconds(exposureTicks, sysClockKhz);

    public static uint ComputeMotorStepsPerPass(int rows, ushort exposureTicks, uint sysClockKhz, uint motorIntervalNs)
    {
        var scanDurationNs = ExposureTicksToNanoseconds(exposureTicks, sysClockKhz) * Math.Max(rows, 0);
        return (uint)Math.Max(1, (int)Math.Round(scanDurationNs / Math.Max(motorIntervalNs, 1u), MidpointRounding.AwayFromZero));
    }

    public static double ComputeMotorStepsPerMillimeter(ScanMotorMechanicalSettings settings)
    {
        var normalized = settings.Normalize();
        return (normalized.StepsPerRevolution * (double)normalized.Microsteps) / normalized.LeadLengthMm;
    }

    public static double ConvertMotorStepsToMillimeters(uint steps, ScanMotorMechanicalSettings settings)
        => steps / Math.Max(ComputeMotorStepsPerMillimeter(settings), double.Epsilon);

    public static double ConvertMotorStepsToMicrometers(uint steps, ScanMotorMechanicalSettings settings)
        => ConvertMotorStepsToMillimeters(steps, settings) * MicrometersPerMillimeter;

    public static bool TryConvertMillimetersToMotorSteps(double millimeters, ScanMotorMechanicalSettings settings, out uint steps)
    {
        steps = 0;
        if (!double.IsFinite(millimeters) || millimeters <= 0.0)
            return false;

        var exactSteps = millimeters * ComputeMotorStepsPerMillimeter(settings);
        if (!double.IsFinite(exactSteps) || exactSteps < 0.5 || exactSteps > uint.MaxValue)
            return false;

        var rounded = Math.Round(exactSteps, MidpointRounding.AwayFromZero);
        if (rounded < 1.0 || rounded > uint.MaxValue)
            return false;

        steps = (uint)rounded;
        return true;
    }

    public static double ConvertMotorIntervalToStepsPerSecond(uint intervalNs)
        => NanosecondsPerSecond / Math.Max(intervalNs, 1u);

    public static double ConvertMotorIntervalToMillimetersPerSecond(uint intervalNs, ScanMotorMechanicalSettings settings)
        => ConvertMotorIntervalToStepsPerSecond(intervalNs) / Math.Max(ComputeMotorStepsPerMillimeter(settings), double.Epsilon);

    public static double ConvertMotorIntervalToMicrometersPerSecond(uint intervalNs, ScanMotorMechanicalSettings settings)
        => ConvertMotorIntervalToMillimetersPerSecond(intervalNs, settings) * MicrometersPerMillimeter;

    public static bool TryConvertMillimetersPerSecondToMotorIntervalNs(double millimetersPerSecond, ScanMotorMechanicalSettings settings, uint minIntervalNs, out uint intervalNs)
    {
        intervalNs = 0;
        if (!double.IsFinite(millimetersPerSecond) || millimetersPerSecond <= 0.0)
            return false;

        var stepsPerSecond = millimetersPerSecond * ComputeMotorStepsPerMillimeter(settings);
        if (!double.IsFinite(stepsPerSecond) || stepsPerSecond <= 0.0)
            return false;

        var computed = Math.Ceiling(NanosecondsPerSecond / stepsPerSecond);
        if (!double.IsFinite(computed) || computed < minIntervalNs || computed > uint.MaxValue)
            return false;

        intervalNs = (uint)computed;
        return true;
    }

    public static bool TryConvertMillimetersPerSecondToMotorIntervalUs(double millimetersPerSecond, ScanMotorMechanicalSettings settings, uint minIntervalUs, out uint intervalUs)
        => TryConvertMillimetersPerSecondToMotorIntervalNs(millimetersPerSecond, settings, minIntervalUs, out intervalUs);

    public static double ConvertMotorIntervalToLineDistanceMillimeters(uint intervalNs, ushort exposureTicks, uint sysClockKhz, ScanMotorMechanicalSettings settings)
    {
        var lineTimeNs = ExposureTicksToNanoseconds(exposureTicks, sysClockKhz);
        var speedMmPerSecond = ConvertMotorIntervalToMillimetersPerSecond(intervalNs, settings);
        return (speedMmPerSecond * lineTimeNs) / NanosecondsPerSecond;
    }

    public static bool TryConvertLineDistanceMillimetersToMotorIntervalNs(double lineDistanceMillimeters, ushort exposureTicks, uint sysClockKhz, ScanMotorMechanicalSettings settings, uint minIntervalNs, out uint intervalNs)
    {
        intervalNs = 0;
        if (!double.IsFinite(lineDistanceMillimeters) || lineDistanceMillimeters <= 0.0)
            return false;

        var lineTimeNs = ExposureTicksToNanoseconds(exposureTicks, sysClockKhz);
        if (!double.IsFinite(lineTimeNs) || lineTimeNs <= 0.0)
            return false;

        var speedMmPerSecond = (lineDistanceMillimeters * NanosecondsPerSecond) / lineTimeNs;
        return TryConvertMillimetersPerSecondToMotorIntervalNs(speedMmPerSecond, settings, minIntervalNs, out intervalNs);
    }

    public static bool TryConvertLineDistanceMillimetersToMotorIntervalUs(double lineDistanceMillimeters, ushort exposureTicks, uint sysClockKhz, ScanMotorMechanicalSettings settings, uint minIntervalUs, out uint intervalUs)
        => TryConvertLineDistanceMillimetersToMotorIntervalNs(lineDistanceMillimeters, exposureTicks, sysClockKhz, settings, minIntervalUs, out intervalUs);

    public static double ExposureNanosecondsToReciprocalSeconds(double exposureNanoseconds)
        => NanosecondsPerSecond / Math.Max(exposureNanoseconds, double.Epsilon);

    public static int ComputeSettlingMillisecondsForLines(ushort exposureTicks, uint sysClockKhz, int lineCount)
        => Math.Max(1, (int)Math.Ceiling((ExposureTicksToNanoseconds(exposureTicks, sysClockKhz) * Math.Max(lineCount, 1)) / 1_000_000.0));
}
