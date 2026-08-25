using System.Collections.ObjectModel;

namespace PRISM_Utility.Core.Models;

public enum ScanFilmProfileValidationSeverity
{
    Warning,
    Error
}

public enum ScanFilmProfileValidationCode
{
    MissingDocument,
    MissingChannelProfiles,
    MalformedJson,
    MissingRequiredProperty,
    UnsupportedSchemaVersion,
    InvalidProfileName,
    InvalidSavedAtUtc,
    InvalidChannelRole,
    InvalidChannelParameters,
    DuplicateChannelRole,
    NoValidChannelProfiles,
    InvalidColorManagement,
    InvalidAlignmentMode,
    InvalidDngExportMode,
    InvalidAcquisitionInput,
    InvalidRoiInput
}

public sealed record ScanFilmProfileValidationIssue
{
    public ScanFilmProfileValidationIssue(
        ScanFilmProfileValidationCode code,
        string fieldPath,
        ScanFilmProfileValidationSeverity severity,
        string messageKey,
        IEnumerable<string>? messageArguments = null)
    {
        Code = code;
        FieldPath = fieldPath;
        Severity = severity;
        MessageKey = messageKey;
        MessageArguments = Array.AsReadOnly((messageArguments ?? []).ToArray());
    }

    public ScanFilmProfileValidationCode Code { get; }
    public string FieldPath { get; }
    public ScanFilmProfileValidationSeverity Severity { get; }
    public string MessageKey { get; }
    public IReadOnlyList<string> MessageArguments { get; }
}

public sealed record ScanFilmProfileValidationResult
{
    public ScanFilmProfileValidationResult(IEnumerable<ScanFilmProfileValidationIssue>? issues = null)
    {
        Issues = Array.AsReadOnly((issues ?? [])
            .OrderBy(issue => issue.FieldPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code)
            .ThenBy(issue => issue.Severity)
            .ToArray());
    }

    public IReadOnlyList<ScanFilmProfileValidationIssue> Issues { get; }
    public bool IsValid => Issues.All(issue => issue.Severity != ScanFilmProfileValidationSeverity.Error);
}

public sealed record ScanFilmProfileDraftConversionResult(
    ScanFilmProfileDraft Draft,
    ScanFilmProfileValidationResult Validation);

public sealed record ScanFilmProfileDraft
{
    public ScanFilmProfileDraft(
        string profileName,
        DateTimeOffset savedAtUtc,
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles,
        string? selectedCalibrationChannel,
        ScanFilmAcquisitionSettings? acquisitionSettings,
        ScanFilmScanRecipeSettings? scanRecipeSettings)
    {
        ProfileName = profileName;
        SavedAtUtc = savedAtUtc;
        ChannelProfiles = CopyChannelProfiles(channelProfiles);
        SelectedCalibrationChannel = selectedCalibrationChannel;
        AcquisitionSettings = acquisitionSettings;
        ScanRecipeSettings = scanRecipeSettings;
    }

    public string ProfileName { get; }
    public DateTimeOffset SavedAtUtc { get; }
    public IReadOnlyDictionary<string, ScanChannelCalibrationProfile> ChannelProfiles { get; }
    public string? SelectedCalibrationChannel { get; }
    public ScanFilmAcquisitionSettings? AcquisitionSettings { get; }
    public ScanFilmScanRecipeSettings? ScanRecipeSettings { get; }

    public static ScanFilmProfileDraft CreateDefault()
        => new(
            string.Empty,
            DateTimeOffset.UnixEpoch,
            null,
            null,
            ScanFilmAcquisitionSettings.CreateDefault(),
            null);

    public static ScanFilmProfileDraftConversionResult FromDocument(ScanFilmParameterProfileSet? document)
    {
        if (document is null)
        {
            return new ScanFilmProfileDraftConversionResult(
                CreateDefault(),
                new ScanFilmProfileValidationResult(
                [
                    new ScanFilmProfileValidationIssue(
                        ScanFilmProfileValidationCode.MissingDocument,
                        "document",
                        ScanFilmProfileValidationSeverity.Error,
                        "FilmProfile.Validation.DocumentMissing")
                ]));
        }

        var issues = new List<ScanFilmProfileValidationIssue>();
        if (document.ChannelProfiles is null)
        {
            issues.Add(new ScanFilmProfileValidationIssue(
                ScanFilmProfileValidationCode.MissingChannelProfiles,
                "channelProfiles",
                ScanFilmProfileValidationSeverity.Error,
                "FilmProfile.Validation.ChannelProfilesMissing"));
        }

        var draft = new ScanFilmProfileDraft(
            document.ProfileName,
            document.SavedAtUtc,
            document.ChannelProfiles,
            document.SelectedCalibrationChannel,
            document.AcquisitionSettings,
            document.ScanRecipeSettings);
        return new ScanFilmProfileDraftConversionResult(draft, new ScanFilmProfileValidationResult(issues));
    }

    public bool HasSameContentAs(ScanFilmProfileDraft? other)
    {
        if (other is null
            || !string.Equals(ProfileName, other.ProfileName, StringComparison.Ordinal)
            || SavedAtUtc != other.SavedAtUtc
            || !string.Equals(SelectedCalibrationChannel, other.SelectedCalibrationChannel, StringComparison.Ordinal)
            || AcquisitionSettings != other.AcquisitionSettings
            || ScanRecipeSettings != other.ScanRecipeSettings
            || ChannelProfiles.Count != other.ChannelProfiles.Count)
        {
            return false;
        }

        foreach (var pair in ChannelProfiles)
        {
            if (!other.ChannelProfiles.TryGetValue(pair.Key, out var otherProfile) || pair.Value != otherProfile)
                return false;
        }

        return true;
    }

    private static IReadOnlyDictionary<string, ScanChannelCalibrationProfile> CopyChannelProfiles(
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? channelProfiles)
    {
        var copied = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
        if (channelProfiles is not null)
        {
            foreach (var pair in channelProfiles)
            {
                if (pair.Value is not null)
                    copied[pair.Key] = CopyProfile(pair.Value);
            }
        }

        return new ReadOnlyDictionary<string, ScanChannelCalibrationProfile>(copied);
    }

    private static ScanChannelCalibrationProfile CopyProfile(ScanChannelCalibrationProfile profile)
        => new(
            profile.Parameters,
            profile.RoiSettings ?? ScanCalibrationRoiSettings.CreateDefault().Normalize(),
            profile.BlackLevel,
            profile.WhiteLevel);
}
