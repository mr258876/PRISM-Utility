using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

internal static class ScanFilmProfileDocumentNormalization
{
    public static ScanFilmProfileDocumentParseResult MigrateV5ToV6(ScanFilmParameterProfileSet source, int schemaVersion)
        => NormalizeParsedDocument(source, schemaVersion, migrateV5: true, repairLegacyProfiles: true);

    public static ScanFilmProfileDocumentParseResult NormalizeCurrentDocument(ScanFilmParameterProfileSet source, int schemaVersion)
        => NormalizeParsedDocument(source, schemaVersion, migrateV5: false, repairLegacyProfiles: true);

    public static ScanFilmParameterProfileSet NormalizeForSerialization(ScanFilmParameterProfileSet source, int schemaVersion)
        => source with
        {
            SchemaVersion = schemaVersion,
            AcquisitionSettings = source.SchemaVersion == 5
                ? MigrateV5Acquisition(source.AcquisitionSettings)
                : (source.AcquisitionSettings ?? ScanFilmAcquisitionSettings.CreateDefault()).Normalize()
        };

    private static ScanFilmProfileDocumentParseResult NormalizeParsedDocument(
        ScanFilmParameterProfileSet source,
        int schemaVersion,
        bool migrateV5,
        bool repairLegacyProfiles)
    {
        var issues = new List<ScanFilmProfileValidationIssue>();
        var profileName = source.ProfileName.Trim();
        if (profileName.Length == 0)
            issues.Add(Issue(ScanFilmProfileValidationCode.InvalidProfileName, "profileName", "FilmProfile.Validation.ProfileNameInvalid", ScanFilmProfileValidationSeverity.Warning));

        var profiles = NormalizeProfiles(source.ChannelProfiles, issues, repairLegacyProfiles);
        var recipe = NormalizeRecipe(source.ScanRecipeSettings, issues);
        var acquisition = migrateV5
            ? MigrateV5Acquisition(source.AcquisitionSettings)
            : NormalizeV6Acquisition(source.AcquisitionSettings, issues);
        AddNoProfilesIssue(profiles, issues);
        var validation = new ScanFilmProfileValidationResult(issues);
        if (profiles.Count == 0)
            return new ScanFilmProfileDocumentParseResult(null, validation);

        var document = new ScanFilmParameterProfileSet(
            schemaVersion,
            profileName,
            source.SavedAtUtc,
            profiles,
            NormalizeSelectedChannel(source.SelectedCalibrationChannel, profiles),
            acquisition,
            recipe);
        return new ScanFilmProfileDocumentParseResult(document, validation);
    }

    public static ScanFilmProfileDocumentBuildResult BuildDocument(ScanFilmProfileDraft? draft, int schemaVersion)
    {
        if (draft is null)
            return new ScanFilmProfileDocumentBuildResult(null, Validation(ScanFilmProfileValidationCode.MissingDocument, "document", "FilmProfile.Validation.DocumentMissing"));

        var issues = new List<ScanFilmProfileValidationIssue>();
        if (string.IsNullOrWhiteSpace(draft.ProfileName))
            issues.Add(Issue(ScanFilmProfileValidationCode.InvalidProfileName, "profileName", "FilmProfile.Validation.ProfileNameInvalid", ScanFilmProfileValidationSeverity.Warning));

        var profiles = NormalizeProfiles(draft.ChannelProfiles, issues, repairLegacyProfiles: false);
        AddNoProfilesIssue(profiles, issues);
        var recipe = NormalizeRecipe(draft.ScanRecipeSettings, issues);
        var acquisition = NormalizeV6Acquisition(draft.AcquisitionSettings, issues);
        var validation = new ScanFilmProfileValidationResult(issues);
        if (!validation.IsValid)
            return new ScanFilmProfileDocumentBuildResult(null, validation);

        return new ScanFilmProfileDocumentBuildResult(
            new ScanFilmParameterProfileSet(
                schemaVersion,
                draft.ProfileName.Trim(),
                draft.SavedAtUtc,
                profiles,
                NormalizeSelectedChannel(draft.SelectedCalibrationChannel, profiles),
                acquisition,
                recipe),
            validation);
    }

    public static IReadOnlyDictionary<string, ScanChannelCalibrationProfile> MigrateLegacyLocalProfiles(
        IReadOnlyDictionary<string, ScanParameterSnapshot>? legacyProfiles)
    {
        var migrated = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
        if (legacyProfiles is null)
            return migrated;

        foreach (var pair in legacyProfiles.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var role = NormalizeRole(pair.Key);
            if (!string.IsNullOrEmpty(role)
                && pair.Value is not null
                && !migrated.ContainsKey(role)
                && ScanDebugValidation.TryNormalizeSnapshot(pair.Value, out var snapshot))
            {
                migrated[role] = new ScanChannelCalibrationProfile(snapshot, ScanCalibrationRoiSettings.CreateDefault().Normalize());
            }
        }

        return migrated;
    }

    private static Dictionary<string, ScanChannelCalibrationProfile> NormalizeProfiles(
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? source,
        List<ScanFilmProfileValidationIssue> issues,
        bool repairLegacyProfiles)
    {
        var profiles = new Dictionary<string, ScanChannelCalibrationProfile>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            issues.Add(Issue(ScanFilmProfileValidationCode.MissingChannelProfiles, "channelProfiles", "FilmProfile.Validation.ChannelProfilesMissing"));
            return profiles;
        }

        foreach (var pair in source.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var role = NormalizeRole(pair.Key);
            var fieldPath = $"channelProfiles.{pair.Key}";
            if (string.IsNullOrEmpty(role))
            {
                issues.Add(Issue(ScanFilmProfileValidationCode.InvalidChannelRole, fieldPath, "FilmProfile.Validation.ChannelRoleInvalid"));
                continue;
            }

            if (profiles.ContainsKey(role))
            {
                issues.Add(Issue(ScanFilmProfileValidationCode.DuplicateChannelRole, fieldPath, "FilmProfile.Validation.ChannelRoleDuplicate"));
                continue;
            }

            if (pair.Value is null || pair.Value.Parameters is null || !ScanDebugValidation.TryNormalizeSnapshot(pair.Value.Parameters, out var parameters))
            {
                issues.Add(Issue(ScanFilmProfileValidationCode.InvalidChannelParameters, $"{fieldPath}.parameters", "FilmProfile.Validation.ChannelParametersInvalid"));
                continue;
            }

            if (ScanCalibrationProfileSettingsNormalizer.TryValidateProfile(pair.Value, out var profile)
                || (repairLegacyProfiles && ScanCalibrationProfileSettingsNormalizer.TryNormalizeProfile(pair.Value, out profile)))
            {
                profiles[role] = profile;
                continue;
            }

            issues.Add(Issue(ScanFilmProfileValidationCode.InvalidRoiInput, $"{fieldPath}.roiSettings", "FilmProfile.Validation.RoiInputInvalid"));
        }

        return profiles;
    }

    private static ScanFilmScanRecipeSettings? NormalizeRecipe(ScanFilmScanRecipeSettings? settings, List<ScanFilmProfileValidationIssue> issues)
    {
        if (settings is null)
            return null;

        if (settings.ColorManagement is { } colorManagement
            && (!double.IsFinite(colorManagement.RedWavelengthNm)
                || !double.IsFinite(colorManagement.GreenWavelengthNm)
                || !double.IsFinite(colorManagement.BlueWavelengthNm)
                || !double.IsFinite(colorManagement.OutputGamma)
                || colorManagement.RedWavelengthNm <= 0
                || colorManagement.GreenWavelengthNm <= 0
                || colorManagement.BlueWavelengthNm <= 0
                || colorManagement.OutputGamma <= 0))
        {
            issues.Add(Issue(ScanFilmProfileValidationCode.InvalidColorManagement, "scanRecipeSettings.colorManagement", "FilmProfile.Validation.ColorManagementInvalid"));
        }

        if (settings.AlignmentMode is { } alignmentMode && !Enum.IsDefined(alignmentMode))
            issues.Add(Issue(ScanFilmProfileValidationCode.InvalidAlignmentMode, "scanRecipeSettings.alignmentMode", "FilmProfile.Validation.AlignmentModeInvalid"));

        if (settings.DngExportMode is { } dngExportMode && !Enum.IsDefined(dngExportMode))
            issues.Add(Issue(ScanFilmProfileValidationCode.InvalidDngExportMode, "scanRecipeSettings.dngExportMode", "FilmProfile.Validation.DngExportModeInvalid"));

        var assignment = settings.ChannelAssignment;
        return new ScanFilmScanRecipeSettings(
            assignment is null ? null : new ScanChannelAssignment(
                NormalizeRecipeRole(assignment.Channel1Role, "Blue"),
                NormalizeRecipeRole(assignment.Channel2Role, "White"),
                NormalizeRecipeRole(assignment.Channel3Role, "Red"),
                NormalizeRecipeRole(assignment.Channel4Role, "Green"),
                assignment.Channel1Reversed,
                assignment.Channel2Reversed,
                assignment.Channel3Reversed,
                assignment.Channel4Reversed),
            settings.ColorManagement,
            settings.AlignmentMode,
            settings.DngExportMode);
    }

    private static ScanFilmAcquisitionSettings MigrateV5Acquisition(ScanFilmAcquisitionSettings? settings)
    {
        var defaults = ScanFilmAcquisitionSettings.CreateDefault();
        var normalized = (settings ?? defaults).Normalize();
        return normalized with
        {
            Rows = defaults.Rows,
            ScanMotorId = defaults.ScanMotorId,
            TargetLinePitchMicrometers = defaults.TargetLinePitchMicrometers,
            StartingDirectionPositive = defaults.StartingDirectionPositive,
            WarmUpEnabled = defaults.WarmUpEnabled,
            TransportStrategy = defaults.TransportStrategy,
            AcquisitionChannelAssignment = defaults.AcquisitionChannelAssignment
        };
    }

    private static ScanFilmAcquisitionSettings NormalizeV6Acquisition(
        ScanFilmAcquisitionSettings? settings,
        List<ScanFilmProfileValidationIssue> issues)
    {
        var normalized = (settings ?? ScanFilmAcquisitionSettings.CreateDefault()).Normalize();
        if (!ScanRowCountValidation.IsValidForHostBuffer(normalized.Rows))
            issues.Add(AcquisitionIssue("acquisitionSettings.rows"));

        if (normalized.ScanMotorId >= ScanDebugConstants.MotionMotorCount)
            issues.Add(AcquisitionIssue("acquisitionSettings.scanMotorId"));

        if (normalized.TargetLinePitchMicrometers is double targetLinePitch
            && (!double.IsFinite(targetLinePitch) || targetLinePitch <= 0.0))
        {
            issues.Add(AcquisitionIssue("acquisitionSettings.targetLinePitchMicrometers"));
        }

        if (!Enum.IsDefined(normalized.TransportStrategy))
            issues.Add(AcquisitionIssue("acquisitionSettings.transportStrategy"));

        if (normalized.AcquisitionChannelAssignment is { } assignment
            && ScanChannelRoleHelper.CountActiveRoles(assignment.Roles) == 0)
        {
            issues.Add(AcquisitionIssue("acquisitionSettings.acquisitionChannelAssignment"));
        }

        return normalized;
    }

    private static ScanFilmProfileValidationIssue AcquisitionIssue(string fieldPath)
        => Issue(
            ScanFilmProfileValidationCode.InvalidAcquisitionInput,
            fieldPath,
            "FilmProfile.Validation.AcquisitionInputInvalid");

    private static void AddNoProfilesIssue(Dictionary<string, ScanChannelCalibrationProfile> profiles, List<ScanFilmProfileValidationIssue> issues)
    {
        if (profiles.Count == 0)
            issues.Add(Issue(ScanFilmProfileValidationCode.NoValidChannelProfiles, "channelProfiles", "FilmProfile.Validation.NoValidChannelProfiles"));
    }

    private static string? NormalizeSelectedChannel(string? selectedChannel, Dictionary<string, ScanChannelCalibrationProfile> profiles)
    {
        var normalized = NormalizeRole(selectedChannel);
        return profiles.ContainsKey(normalized) ? normalized : profiles.Keys.FirstOrDefault();
    }

    private static string NormalizeRecipeRole(string? role, string fallback)
    {
        var normalized = NormalizeRole(role);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string NormalizeRole(string? role)
        => string.IsNullOrWhiteSpace(role) ? string.Empty : role.Trim();

    private static ScanFilmProfileValidationResult Validation(ScanFilmProfileValidationCode code, string fieldPath, string messageKey)
        => new([Issue(code, fieldPath, messageKey)]);

    private static ScanFilmProfileValidationIssue Issue(ScanFilmProfileValidationCode code, string fieldPath, string messageKey)
        => Issue(code, fieldPath, messageKey, ScanFilmProfileValidationSeverity.Error);

    private static ScanFilmProfileValidationIssue Issue(ScanFilmProfileValidationCode code, string fieldPath, string messageKey, ScanFilmProfileValidationSeverity severity)
        => new(code, fieldPath, severity, messageKey);
}
