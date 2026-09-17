namespace PRISM_Utility.Core.Models;

public enum ScanRoiOperationOwner
{
    AdcCalibration,
    AutoFocus,
    ImageReferenceSampling
}

public enum ScanRoiValidationCode
{
    Missing,
    Empty,
    Inverted,
    OutOfBounds,
    TooNarrow,
    Overlap,
    DoesNotContain
}

public sealed record ScanRoiValidationIssue(
    ScanRoiOperationOwner Owner,
    ScanRoiValidationCode Code,
    string FieldPath);

public enum ScanRoiEditorTarget
{
    AdcEffective,
    AdcShield,
    FocusLeft,
    FocusRight,
    FocusOverall,
    ImageReferenceColumn
}

public sealed record ScanRoiIssueNavigationRequest(
    ScanRoiOperationOwner Owner,
    ScanRoiEditorTarget Target,
    string FieldPath);

public sealed record ScanRoiValidationResult<T>(T? Value, IReadOnlyList<ScanRoiValidationIssue> Issues)
    where T : class
{
    public bool IsValid => Issues.Count == 0;
}

public sealed class ScanRoiValidationException : ArgumentException
{
    public ScanRoiValidationException(IReadOnlyList<ScanRoiValidationIssue> issues)
        : base("The operation ROI is invalid.")
    {
        Issues = issues;
    }

    public IReadOnlyList<ScanRoiValidationIssue> Issues { get; }
}

public sealed record ScanAdcCalibrationRoi(ScanColumnRange EffectiveRange, ScanColumnRange ShieldRange)
{
    public const int MinimumRangeWidth = 2;

    public static ScanRoiValidationResult<ScanAdcCalibrationRoi> TryCreate(ScanCalibrationRoiSettings? settings, int imageWidth)
    {
        var issues = new List<ScanRoiValidationIssue>();
        if (settings is null)
            issues.Add(new(ScanRoiOperationOwner.AdcCalibration, ScanRoiValidationCode.Missing, "RoiSettings"));
        else
        {
            ScanOperationRoiValidation.ValidateRange(settings.EffectiveRange, imageWidth, MinimumRangeWidth, ScanRoiOperationOwner.AdcCalibration, "EffectiveRange", issues);
            ScanOperationRoiValidation.ValidateRange(settings.ShieldRange, imageWidth, MinimumRangeWidth, ScanRoiOperationOwner.AdcCalibration, "ShieldRange", issues);
            if (issues.Count == 0 && ScanOperationRoiValidation.Overlaps(settings.EffectiveRange, settings.ShieldRange))
                issues.Add(new(ScanRoiOperationOwner.AdcCalibration, ScanRoiValidationCode.Overlap, "ShieldRange"));
        }

        return new(issues.Count == 0 ? new(settings!.EffectiveRange, settings.ShieldRange) : null, issues);
    }

    public static ScanAdcCalibrationRoi Require(ScanCalibrationRoiSettings? settings, int imageWidth)
        => TryCreate(settings, imageWidth).Require();
}

public sealed record ScanFocusRoi(ScanColumnRange LeftRange, ScanColumnRange RightRange, ScanColumnRange OverallRange)
{
    public const int MinimumRangeWidth = 3;

    public static ScanRoiValidationResult<ScanFocusRoi> TryCreate(ScanCalibrationRoiSettings? settings, int imageWidth)
    {
        var issues = new List<ScanRoiValidationIssue>();
        if (settings is null)
            issues.Add(new(ScanRoiOperationOwner.AutoFocus, ScanRoiValidationCode.Missing, "RoiSettings"));
        else
        {
            ScanOperationRoiValidation.ValidateRange(settings.FocusLeftRange, imageWidth, MinimumRangeWidth, ScanRoiOperationOwner.AutoFocus, "FocusLeftRange", issues);
            ScanOperationRoiValidation.ValidateRange(settings.FocusRightRange, imageWidth, MinimumRangeWidth, ScanRoiOperationOwner.AutoFocus, "FocusRightRange", issues);
            ScanOperationRoiValidation.ValidateRange(settings.FocusOverallRange, imageWidth, MinimumRangeWidth, ScanRoiOperationOwner.AutoFocus, "FocusOverallRange", issues);
            if (issues.Count == 0 && ScanOperationRoiValidation.Overlaps(settings.FocusLeftRange, settings.FocusRightRange))
                issues.Add(new(ScanRoiOperationOwner.AutoFocus, ScanRoiValidationCode.Overlap, "FocusRightRange"));
            if (issues.Count == 0 && (!ScanOperationRoiValidation.Contains(settings.FocusOverallRange, settings.FocusLeftRange) || !ScanOperationRoiValidation.Contains(settings.FocusOverallRange, settings.FocusRightRange)))
                issues.Add(new(ScanRoiOperationOwner.AutoFocus, ScanRoiValidationCode.DoesNotContain, "FocusOverallRange"));
        }

        return new(issues.Count == 0 ? new(settings!.FocusLeftRange, settings.FocusRightRange, settings.FocusOverallRange) : null, issues);
    }

    public static ScanFocusRoi Require(ScanCalibrationRoiSettings? settings, int imageWidth)
        => TryCreate(settings, imageWidth).Require();
}

public sealed record ScanImageReferenceColumnRange(ScanColumnRange ColumnRange)
{
    public static ScanRoiValidationResult<ScanImageReferenceColumnRange> TryCreate(ScanColumnRange? range, int imageWidth)
    {
        var issues = new List<ScanRoiValidationIssue>();
        ScanOperationRoiValidation.ValidateRange(range, imageWidth, 1, ScanRoiOperationOwner.ImageReferenceSampling, "ColumnRange", issues);
        return new(issues.Count == 0 ? new(range!) : null, issues);
    }

    public static ScanImageReferenceColumnRange Require(ScanColumnRange? range, int imageWidth)
        => TryCreate(range, imageWidth).Require();
}

public static class ScanRoiValidationResultExtensions
{
    public static T Require<T>(this ScanRoiValidationResult<T> result) where T : class
        => result.Value ?? throw new ScanRoiValidationException(result.Issues);
}

internal static class ScanOperationRoiValidation
{
    internal static void ValidateRange(ScanColumnRange? range, int imageWidth, int minimumWidth, ScanRoiOperationOwner owner, string fieldPath, ICollection<ScanRoiValidationIssue> issues)
    {
        if (range is null)
            issues.Add(new(owner, ScanRoiValidationCode.Missing, fieldPath));
        else if (range.Start == 0 && range.EndInclusive == -1)
            issues.Add(new(owner, ScanRoiValidationCode.Empty, fieldPath));
        else if (range.Start > range.EndInclusive)
            issues.Add(new(owner, ScanRoiValidationCode.Inverted, fieldPath));
        else if (imageWidth <= 0 || range.Width == 0)
            issues.Add(new(owner, ScanRoiValidationCode.Empty, fieldPath));
        else if (range.Start < 0 || range.EndInclusive >= imageWidth)
            issues.Add(new(owner, ScanRoiValidationCode.OutOfBounds, fieldPath));
        else if (range.Width < minimumWidth)
            issues.Add(new(owner, ScanRoiValidationCode.TooNarrow, fieldPath));
    }

    internal static bool Overlaps(ScanColumnRange left, ScanColumnRange right)
        => left.Start <= right.EndInclusive && right.Start <= left.EndInclusive;

    internal static bool Contains(ScanColumnRange container, ScanColumnRange value)
        => container.Start <= value.Start && container.EndInclusive >= value.EndInclusive;
}
