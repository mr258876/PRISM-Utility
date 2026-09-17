using System.Collections.ObjectModel;
using PRISM_Utility.Core.Helpers;

namespace PRISM_Utility.Core.Models;

public enum ScanFocusMotorMappingValidationError
{
    None,
    LeftMotorIdOutOfRange,
    RightMotorIdOutOfRange,
    DuplicateMotorIds
}

public sealed record ScanFocusMotorMappingValidationResult(
    ScanFocusMotorMappingValidationError Error,
    string Message)
{
    public bool IsValid => Error == ScanFocusMotorMappingValidationError.None;
}

public sealed record ScanFocusMotorMapping(
    byte LeftMotorId = 0,
    byte RightMotorId = 2,
    bool ZPositiveDirection = false,
    bool TiltPositiveDirection = false)
{
    public ScanFocusMotorMappingValidationResult Validate()
    {
        if (LeftMotorId >= ScanDebugConstants.MotionMotorCount)
            return new(ScanFocusMotorMappingValidationError.LeftMotorIdOutOfRange, $"Left focus motor ID must be in [0, {ScanDebugConstants.MotionMotorCount - 1}].");
        if (RightMotorId >= ScanDebugConstants.MotionMotorCount)
            return new(ScanFocusMotorMappingValidationError.RightMotorIdOutOfRange, $"Right focus motor ID must be in [0, {ScanDebugConstants.MotionMotorCount - 1}].");
        if (LeftMotorId == RightMotorId)
            return new(ScanFocusMotorMappingValidationError.DuplicateMotorIds, "Left and right focus motor IDs must be distinct.");

        return new(ScanFocusMotorMappingValidationError.None, string.Empty);
    }
}

public enum ScanAutofocusPresetKind
{
    Quick,
    Standard,
    Fine,
    Custom
}

public sealed record ScanAutofocusPresetDefinition(
    ScanAutofocusPresetKind Kind,
    int SampleRows,
    double TiltProbeMillimeters,
    double ZProbeMillimeters,
    uint MotorIntervalNs,
    bool ZPositiveDirection,
    bool TiltPositiveDirection,
    int MaxTiltIterations,
    int MaxZIterations);

public sealed record ScanAutofocusBounds(
    long MaxZSteps,
    double MaxZMillimeters,
    long MaxTiltSteps,
    double MaxTiltMillimeters,
    int CaptureCount,
    long TotalMovementSteps,
    double TotalMovementMillimeters,
    long EstimatedDurationMs);

public sealed record ScanAutofocusResolvedOptions(
    ScanAutofocusPresetDefinition Definition,
    uint TiltProbeSteps,
    uint ZProbeSteps,
    ScanAutofocusBounds Bounds);

public static class ScanAutofocusPresetCatalog
{
    private static readonly IReadOnlyList<ScanAutofocusPresetDefinition> BuiltIns =
        new ReadOnlyCollection<ScanAutofocusPresetDefinition>(
        [
            new(ScanAutofocusPresetKind.Quick, 64, 0.25, 0.5, 500_000, false, false, 4, 5),
            new(ScanAutofocusPresetKind.Standard, 128, 0.5, 1.0, 500_000, false, false, 8, 10),
            new(ScanAutofocusPresetKind.Fine, 128, 0.25, 0.5, 500_000, false, false, 16, 20)
        ]);

    public static IReadOnlyList<ScanAutofocusPresetDefinition> Definitions => BuiltIns;

    public static ScanAutofocusPresetDefinition Get(ScanAutofocusPresetKind kind)
        => BuiltIns.FirstOrDefault(definition => definition.Kind == kind)
            ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, "Custom autofocus has no built-in definition.");
}

public static class ScanAutofocusPresetResolver
{
    public static ScanAutofocusResolvedOptions Resolve(
        ScanAutofocusPresetDefinition definition,
        ScanMotorMechanicalSettings mechanics)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(mechanics);
        ValidateDefinition(definition);
        ValidateMechanics(mechanics);

        if (!ScanTimingMath.TryConvertMillimetersToMotorSteps(definition.TiltProbeMillimeters, mechanics, out var tiltProbeSteps))
            throw new ArgumentOutOfRangeException(nameof(definition), "Autofocus tilt distance is outside the supported motor-step range.");
        if (!ScanTimingMath.TryConvertMillimetersToMotorSteps(definition.ZProbeMillimeters, mechanics, out var zProbeSteps))
            throw new ArgumentOutOfRangeException(nameof(definition), "Autofocus Z distance is outside the supported motor-step range.");

        var stepsPerMillimeter = ScanTimingMath.ComputeMotorStepsPerMillimeter(mechanics);
        var bounds = ComputeBounds(tiltProbeSteps, zProbeSteps, definition.MotorIntervalNs, definition.MaxTiltIterations, definition.MaxZIterations, stepsPerMillimeter);
        return new ScanAutofocusResolvedOptions(definition, tiltProbeSteps, zProbeSteps, bounds);
    }

    internal static ScanAutofocusBounds ComputeBounds(
        uint tiltProbeSteps,
        uint zProbeSteps,
        uint motorIntervalNs,
        int maxTiltIterations,
        int maxZIterations,
        double stepsPerMillimeter)
    {
        checked
        {
            var fineZProbeSteps = Math.Max(1u, zProbeSteps / 4);
            var zPassCount = fineZProbeSteps < zProbeSteps ? 2 : 1;
            var coarseRadius = (ulong)zProbeSteps * (ulong)maxZIterations;
            var fineRadius = zPassCount == 2 ? (ulong)fineZProbeSteps * (ulong)maxZIterations : 0UL;
            var maxZSteps = 2UL * (coarseRadius + fineRadius);
            var maxTiltSteps = (ulong)tiltProbeSteps * (ulong)maxTiltIterations;
            var captureCount = 1UL
                + (2UL * (ulong)zPassCount * ((2UL * (ulong)maxZIterations) + 1UL))
                + ((ulong)maxTiltIterations + 1UL);
            var coarseZMovement = (5UL * zProbeSteps * (ulong)maxZIterations) + (4UL * zProbeSteps);
            var fineZMovement = zPassCount == 2
                ? (5UL * fineZProbeSteps * (ulong)maxZIterations) + (4UL * fineZProbeSteps)
                : 0UL;
            var tiltMovement = (maxTiltIterations == 1 ? 5UL : (ulong)maxTiltIterations + 5UL) * tiltProbeSteps;
            var totalMovementSteps = (2UL * (coarseZMovement + fineZMovement)) + tiltMovement;
            var estimatedDurationMs = (ulong)Math.Ceiling((decimal)totalMovementSteps * motorIntervalNs / 1_000_000m);

            if (maxZSteps > long.MaxValue || maxTiltSteps > long.MaxValue || captureCount > int.MaxValue
                || totalMovementSteps > long.MaxValue || estimatedDurationMs > long.MaxValue)
            {
                throw new OverflowException("Autofocus bounds exceed the supported range.");
            }

            return new ScanAutofocusBounds(
                (long)maxZSteps,
                maxZSteps / stepsPerMillimeter,
                (long)maxTiltSteps,
                maxTiltSteps / stepsPerMillimeter,
                (int)captureCount,
                (long)totalMovementSteps,
                totalMovementSteps / stepsPerMillimeter,
                (long)estimatedDurationMs);
        }
    }

    private static void ValidateDefinition(ScanAutofocusPresetDefinition definition)
    {
        if (!Enum.IsDefined(definition.Kind))
            throw new ArgumentOutOfRangeException(nameof(definition), "Autofocus preset kind is invalid.");
        if (definition.SampleRows <= 0)
            throw new ArgumentOutOfRangeException(nameof(definition), "Autofocus sample rows must be greater than zero.");
        if (!double.IsFinite(definition.TiltProbeMillimeters) || definition.TiltProbeMillimeters <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(definition), "Autofocus tilt distance must be finite and positive.");
        if (!double.IsFinite(definition.ZProbeMillimeters) || definition.ZProbeMillimeters <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(definition), "Autofocus Z distance must be finite and positive.");
        if (definition.MotorIntervalNs < ScanDebugConstants.MotionMinIntervalNs)
            throw new ArgumentOutOfRangeException(nameof(definition), $"Autofocus motor interval must be at least {ScanDebugConstants.MotionMinIntervalNs} ns.");
        if (definition.MaxTiltIterations <= 0 || definition.MaxZIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(definition), "Autofocus iteration counts must be greater than zero.");
    }

    private static void ValidateMechanics(ScanMotorMechanicalSettings mechanics)
    {
        if (mechanics.StepsPerRevolution == 0 || mechanics.Microsteps == 0
            || !double.IsFinite(mechanics.LeadLengthMm) || mechanics.LeadLengthMm <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(mechanics), "Motor mechanics must be finite and positive.");
        }

        var stepsPerMillimeter = ScanTimingMath.ComputeMotorStepsPerMillimeter(mechanics);
        if (!double.IsFinite(stepsPerMillimeter) || stepsPerMillimeter <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(mechanics), "Motor steps per millimeter must be finite and positive.");
    }
}
