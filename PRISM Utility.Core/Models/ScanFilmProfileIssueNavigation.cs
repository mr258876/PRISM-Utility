namespace PRISM_Utility.Core.Models;

public enum ScanFilmProfileIssueSource
{
    CurrentDraft,
    ImportReview
}

public enum ScanFilmProfileIssueNavigationSection
{
    BasicInfo,
    AcquisitionPlan,
    ChannelCalibration
}

public enum ScanFilmProfileIssueEditorTarget
{
    ProfileName,
    ColorManagement,
    AlignmentMode,
    DngExportMode,
    RedWavelength,
    GreenWavelength,
    BlueWavelength,
    OutputGamma,
    TargetWhitePointMode,
    ManualWhitePointColorTemperature,
    Rows,
    ScanMotor,
    MotorDistancePerLine,
    TransportStrategy,
    ChannelAssignment,
    Illumination,
    ChannelStatus,
    ChannelParameters,
    ChannelRoiSettings
}

public sealed record ScanFilmProfileIssueNavigationRequest(
    ScanFilmProfileIssueNavigationSection Section,
    ScanFilmProfileIssueEditorTarget EditorTarget,
    string FieldPath,
    string? ChannelRole);

public static class ScanFilmProfileIssueNavigation
{
    private static readonly IReadOnlyDictionary<string, NavigationTarget> ExactPathTargets =
        new Dictionary<string, NavigationTarget>(StringComparer.Ordinal)
        {
            ["profileName"] = new(ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.ProfileName),
            ["ScanRecipeSettings.ColorManagement.RedWavelengthNm"] = new(ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.RedWavelength),
            ["ScanRecipeSettings.ColorManagement.GreenWavelengthNm"] = new(ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.GreenWavelength),
            ["ScanRecipeSettings.ColorManagement.BlueWavelengthNm"] = new(ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.BlueWavelength),
            ["ScanRecipeSettings.ColorManagement.OutputGamma"] = new(ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.OutputGamma),
            ["ScanRecipeSettings.ColorManagement.TargetWhitePointMode"] = new(ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.TargetWhitePointMode),
            ["ScanRecipeSettings.ColorManagement.ManualWhitePointColorTemperatureK"] = new(ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.ManualWhitePointColorTemperature),
            ["scanRecipeSettings.colorManagement"] = new(ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.ColorManagement),
            ["scanRecipeSettings.alignmentMode"] = new(ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.AlignmentMode),
            ["scanRecipeSettings.dngExportMode"] = new(ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.DngExportMode),
            ["AcquisitionSettings.Rows"] = new(ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.Rows),
            ["acquisitionSettings.rows"] = new(ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.Rows),
            ["AcquisitionSettings.ScanMotor"] = new(ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.ScanMotor),
            ["acquisitionSettings.scanMotorId"] = new(ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.ScanMotor),
            ["AcquisitionSettings.TargetLinePitchMicrometers"] = new(ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.MotorDistancePerLine),
            ["acquisitionSettings.targetLinePitchMicrometers"] = new(ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.MotorDistancePerLine),
            ["AcquisitionSettings.MotorIntervalNs"] = new(ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.MotorDistancePerLine),
            ["acquisitionSettings.transportStrategy"] = new(ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.TransportStrategy),
            ["AcquisitionSettings.ChannelAssignment"] = new(ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.ChannelAssignment),
            ["acquisitionSettings.acquisitionChannelAssignment"] = new(ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.ChannelAssignment),
            ["AcquisitionSettings.Illumination"] = new(ScanFilmProfileIssueNavigationSection.ChannelCalibration, ScanFilmProfileIssueEditorTarget.Illumination),
            ["ChannelProfiles.Selected.Parameters"] = new(ScanFilmProfileIssueNavigationSection.ChannelCalibration, ScanFilmProfileIssueEditorTarget.ChannelParameters),
            ["ChannelProfiles.Selected.RoiSettings"] = new(ScanFilmProfileIssueNavigationSection.ChannelCalibration, ScanFilmProfileIssueEditorTarget.ChannelRoiSettings)
        };

    private static readonly HashSet<string> KnownChannelRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Red",
        "Green",
        "Blue",
        "White",
        "IR"
    };

    public static ScanFilmProfileIssueNavigationRequest? Resolve(
        ScanFilmProfileValidationIssue issue,
        ScanFilmProfileIssueSource source,
        IReadOnlyList<string> availableChannelRoles)
    {
        if (source != ScanFilmProfileIssueSource.CurrentDraft
            || issue.Code is ScanFilmProfileValidationCode.InvalidChannelRole
                or ScanFilmProfileValidationCode.DuplicateChannelRole)
            return null;

        if (ExactPathTargets.TryGetValue(issue.FieldPath, out var target))
            return new(target.Section, target.EditorTarget, issue.FieldPath, null);

        if (string.Equals(issue.FieldPath, "channelProfiles", StringComparison.Ordinal))
        {
            return HasSafeAvailableChannelTarget(availableChannelRoles)
                ? new(
                    ScanFilmProfileIssueNavigationSection.ChannelCalibration,
                    ScanFilmProfileIssueEditorTarget.ChannelStatus,
                    issue.FieldPath,
                    null)
                : null;
        }

        return TryResolveRoleQualifiedPath(issue.FieldPath, availableChannelRoles, out var role, out target)
            ? new(target.Section, target.EditorTarget, issue.FieldPath, role)
            : null;
    }

    private static bool TryResolveRoleQualifiedPath(
        string fieldPath,
        IReadOnlyList<string> availableChannelRoles,
        out string? role,
        out NavigationTarget target)
    {
        role = null;
        target = default;
        const string prefix = "channelProfiles.";
        if (!fieldPath.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var segments = fieldPath[prefix.Length..].Split('.');
        if (segments.Length is < 1 or > 2 || !TryGetUniqueAvailableRole(segments[0], availableChannelRoles, out role))
            return false;

        target = segments.Length switch
        {
            1 => new(ScanFilmProfileIssueNavigationSection.ChannelCalibration, ScanFilmProfileIssueEditorTarget.ChannelStatus),
            2 when string.Equals(segments[1], "parameters", StringComparison.Ordinal) => new(ScanFilmProfileIssueNavigationSection.ChannelCalibration, ScanFilmProfileIssueEditorTarget.ChannelParameters),
            2 when string.Equals(segments[1], "roiSettings", StringComparison.Ordinal) => new(ScanFilmProfileIssueNavigationSection.ChannelCalibration, ScanFilmProfileIssueEditorTarget.ChannelRoiSettings),
            _ => default
        };
        return target != default;
    }

    private static bool HasSafeAvailableChannelTarget(IReadOnlyList<string> availableChannelRoles)
        => !HasDuplicateKnownRoles(availableChannelRoles)
            && availableChannelRoles.Any(KnownChannelRoles.Contains);

    private static bool TryGetUniqueAvailableRole(
        string requestedRole,
        IReadOnlyList<string> availableChannelRoles,
        out string? matchingRole)
    {
        matchingRole = null;
        if (!KnownChannelRoles.Contains(requestedRole) || HasDuplicateKnownRoles(availableChannelRoles))
            return false;

        foreach (var availableRole in availableChannelRoles)
        {
            if (!KnownChannelRoles.Contains(availableRole)
                || !string.Equals(availableRole, requestedRole, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (matchingRole is not null)
                return false;

            matchingRole = availableRole;
        }

        return matchingRole is not null;
    }

    private static bool HasDuplicateKnownRoles(IReadOnlyList<string> availableChannelRoles)
    {
        var seenRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var availableRole in availableChannelRoles)
        {
            if (KnownChannelRoles.Contains(availableRole) && !seenRoles.Add(availableRole))
                return true;
        }

        return false;
    }

    private readonly record struct NavigationTarget(
        ScanFilmProfileIssueNavigationSection Section,
        ScanFilmProfileIssueEditorTarget EditorTarget);
}
