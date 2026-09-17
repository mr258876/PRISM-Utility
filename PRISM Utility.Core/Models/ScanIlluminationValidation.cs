using System.Collections.ObjectModel;

namespace PRISM_Utility.Core.Models;

public enum ScanIlluminationValidationCode
{
    InvalidNumericInput,
    InvalidMaskBits,
    SteadySyncOverlap,
    SyncPulseClockBelowMinimum
}

public sealed record ScanIlluminationValidationIssue(
    string FieldPath,
    ScanIlluminationValidationCode Code);

public sealed class ScanIlluminationValidationResult
{
    public ScanIlluminationValidationResult(IEnumerable<ScanIlluminationValidationIssue> issues)
    {
        Issues = new ReadOnlyCollection<ScanIlluminationValidationIssue>(issues.ToArray());
    }

    public IReadOnlyList<ScanIlluminationValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;

    public void ThrowIfInvalid()
    {
        if (!IsValid)
            throw new ScanIlluminationValidationException(this);
    }
}

public sealed class ScanIlluminationValidationException : Exception
{
    public ScanIlluminationValidationException(ScanIlluminationValidationResult validation)
        : base($"Illumination state validation failed with {validation.Issues.Count} issue(s).")
    {
        Validation = validation;
    }

    public ScanIlluminationValidationResult Validation { get; }
}

public static class ScanIlluminationValidator
{
    public static ScanIlluminationValidationResult ValidateState(ScanIlluminationState state, string fieldPrefix)
    {
        var issues = new List<ScanIlluminationValidationIssue>();
        ValidateMask(state.SteadyMask, $"{fieldPrefix}.steadyMask", issues);
        ValidateMask(state.SyncMask, $"{fieldPrefix}.syncMask", issues);

        var overlap = (byte)(state.SteadyMask & state.SyncMask & ScanDebugConstants.IlluminationValidMask);
        for (var ledIndex = 0; ledIndex < ScanDebugConstants.IlluminationChannelCount; ledIndex++)
        {
            if ((overlap & (1 << ledIndex)) != 0)
            {
                issues.Add(new ScanIlluminationValidationIssue(
                    $"{fieldPrefix}.led{ledIndex + 1}.mode",
                    ScanIlluminationValidationCode.SteadySyncOverlap));
            }
        }

        ValidateSyncPulseClock(state.SyncMask, 0, state.Led1PulseClock, fieldPrefix, issues);
        ValidateSyncPulseClock(state.SyncMask, 1, state.Led2PulseClock, fieldPrefix, issues);
        ValidateSyncPulseClock(state.SyncMask, 2, state.Led3PulseClock, fieldPrefix, issues);
        ValidateSyncPulseClock(state.SyncMask, 3, state.Led4PulseClock, fieldPrefix, issues);

        return new ScanIlluminationValidationResult(issues);
    }

    private static void ValidateMask(byte mask, string fieldPath, ICollection<ScanIlluminationValidationIssue> issues)
    {
        if ((mask & ~ScanDebugConstants.IlluminationValidMask) != 0)
            issues.Add(new ScanIlluminationValidationIssue(fieldPath, ScanIlluminationValidationCode.InvalidMaskBits));
    }

    private static void ValidateSyncPulseClock(
        byte syncMask,
        int ledIndex,
        uint pulseClock,
        string fieldPrefix,
        ICollection<ScanIlluminationValidationIssue> issues)
    {
        if ((syncMask & (1 << ledIndex)) == 0 || pulseClock >= ScanDebugConstants.IlluminationMinSyncPulseClock)
            return;

        issues.Add(new ScanIlluminationValidationIssue(
            $"{fieldPrefix}.led{ledIndex + 1}.pulseClock",
            ScanIlluminationValidationCode.SyncPulseClockBelowMinimum));
    }
}
