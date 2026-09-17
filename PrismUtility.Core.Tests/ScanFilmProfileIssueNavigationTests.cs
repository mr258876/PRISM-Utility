using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanFilmProfileIssueNavigationTests
{
    public static IEnumerable<object[]> CurrentEditorPaths()
    {
        yield return Case("profileName", ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.ProfileName);

        yield return Case("ScanRecipeSettings.ColorManagement.RedWavelengthNm", ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.RedWavelength);
        yield return Case("ScanRecipeSettings.ColorManagement.GreenWavelengthNm", ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.GreenWavelength);
        yield return Case("ScanRecipeSettings.ColorManagement.BlueWavelengthNm", ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.BlueWavelength);
        yield return Case("ScanRecipeSettings.ColorManagement.OutputGamma", ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.OutputGamma);
        yield return Case("ScanRecipeSettings.ColorManagement.TargetWhitePointMode", ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.TargetWhitePointMode);
        yield return Case("ScanRecipeSettings.ColorManagement.ManualWhitePointColorTemperatureK", ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.ManualWhitePointColorTemperature);
        yield return Case("scanRecipeSettings.colorManagement", ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.ColorManagement);
        yield return Case("scanRecipeSettings.alignmentMode", ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.AlignmentMode);
        yield return Case("scanRecipeSettings.dngExportMode", ScanFilmProfileIssueNavigationSection.BasicInfo, ScanFilmProfileIssueEditorTarget.DngExportMode);

        yield return Case("AcquisitionSettings.Rows", ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.Rows);
        yield return Case("acquisitionSettings.rows", ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.Rows);
        yield return Case("AcquisitionSettings.ScanMotor", ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.ScanMotor);
        yield return Case("acquisitionSettings.scanMotorId", ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.ScanMotor);
        yield return Case("AcquisitionSettings.TargetLinePitchMicrometers", ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.MotorDistancePerLine);
        yield return Case("acquisitionSettings.targetLinePitchMicrometers", ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.MotorDistancePerLine);
        yield return Case("AcquisitionSettings.MotorIntervalNs", ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.MotorDistancePerLine);
        yield return Case("acquisitionSettings.transportStrategy", ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.TransportStrategy);
        yield return Case("AcquisitionSettings.ChannelAssignment", ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.ChannelAssignment);
        yield return Case("acquisitionSettings.acquisitionChannelAssignment", ScanFilmProfileIssueNavigationSection.AcquisitionPlan, ScanFilmProfileIssueEditorTarget.ChannelAssignment);
        yield return Case("AcquisitionSettings.Illumination", ScanFilmProfileIssueNavigationSection.ChannelCalibration, ScanFilmProfileIssueEditorTarget.Illumination);

        yield return Case("ChannelProfiles.Selected.Parameters", ScanFilmProfileIssueNavigationSection.ChannelCalibration, ScanFilmProfileIssueEditorTarget.ChannelParameters);
        yield return Case("ChannelProfiles.Selected.RoiSettings", ScanFilmProfileIssueNavigationSection.ChannelCalibration, ScanFilmProfileIssueEditorTarget.ChannelRoiSettings);
    }

    public static IEnumerable<object[]> SupportedCurrentFieldPaths()
    {
        foreach (var path in CurrentEditorPaths())
            yield return [(string)path[0]];

        yield return ["channelProfiles"];
        yield return ["channelProfiles.Red"];
        yield return ["channelProfiles.Red.parameters"];
        yield return ["channelProfiles.Red.roiSettings"];
    }

    [Theory]
    [MemberData(nameof(CurrentEditorPaths))]
    public void Resolve_CurrentDraftExactEditorPath_ReturnsExpectedRequest(
        string fieldPath,
        ScanFilmProfileIssueNavigationSection section,
        ScanFilmProfileIssueEditorTarget editorTarget)
    {
        var issue = CreateIssue(fieldPath);

        var actual = ScanFilmProfileIssueNavigation.Resolve(
            issue,
            ScanFilmProfileIssueSource.CurrentDraft,
            ["Red", "Green"]);

        Assert.Equal(new ScanFilmProfileIssueNavigationRequest(section, editorTarget, fieldPath, null), actual);
    }

    [Theory]
    [MemberData(nameof(SupportedCurrentFieldPaths))]
    public void Resolve_ImportReviewForEverySupportedCurrentPath_ReturnsNoRequest(string fieldPath)
    {
        var actual = ScanFilmProfileIssueNavigation.Resolve(
            CreateIssue(fieldPath),
            ScanFilmProfileIssueSource.ImportReview,
            ["Red"]);

        Assert.Null(actual);
    }

    [Fact]
    public void Resolve_GenericChannelProfilesWithSafeAvailableTarget_ReturnsStatusWithoutSelectingChannel()
    {
        var actual = ScanFilmProfileIssueNavigation.Resolve(
            CreateIssue("channelProfiles"),
            ScanFilmProfileIssueSource.CurrentDraft,
            ["Red", "Green"]);

        Assert.Equal(new ScanFilmProfileIssueNavigationRequest(
            ScanFilmProfileIssueNavigationSection.ChannelCalibration,
            ScanFilmProfileIssueEditorTarget.ChannelStatus,
            "channelProfiles",
            null), actual);
    }

    [Theory]
    [InlineData("channelProfiles.Red", ScanFilmProfileIssueEditorTarget.ChannelStatus)]
    [InlineData("channelProfiles.Red.parameters", ScanFilmProfileIssueEditorTarget.ChannelParameters)]
    [InlineData("channelProfiles.Red.roiSettings", ScanFilmProfileIssueEditorTarget.ChannelRoiSettings)]
    public void Resolve_RoleQualifiedCurrentPathWithUniqueKnownRole_ReturnsMatchingRole(
        string fieldPath,
        ScanFilmProfileIssueEditorTarget editorTarget)
    {
        var actual = ScanFilmProfileIssueNavigation.Resolve(
            CreateIssue(fieldPath),
            ScanFilmProfileIssueSource.CurrentDraft,
            ["Green", "Red"]);

        Assert.Equal(new ScanFilmProfileIssueNavigationRequest(
            ScanFilmProfileIssueNavigationSection.ChannelCalibration,
            editorTarget,
            fieldPath,
            "Red"), actual);
    }

    [Theory]
    [InlineData(ScanFilmProfileValidationCode.InvalidChannelRole, "channelProfiles.Red")]
    [InlineData(ScanFilmProfileValidationCode.InvalidChannelRole, "channelProfiles.Red.parameters")]
    [InlineData(ScanFilmProfileValidationCode.InvalidChannelRole, "channelProfiles.Red.roiSettings")]
    [InlineData(ScanFilmProfileValidationCode.DuplicateChannelRole, "channelProfiles.Red")]
    [InlineData(ScanFilmProfileValidationCode.DuplicateChannelRole, "channelProfiles.Red.parameters")]
    [InlineData(ScanFilmProfileValidationCode.DuplicateChannelRole, "channelProfiles.Red.roiSettings")]
    public void Resolve_CurrentDraftRoleQualifiedPathWithUnsafeChannelRoleIssue_ReturnsNoRequest(
        ScanFilmProfileValidationCode code,
        string fieldPath)
    {
        var issue = new ScanFilmProfileValidationIssue(
            code,
            fieldPath,
            ScanFilmProfileValidationSeverity.Error,
            "FilmProfile.Validation.ChannelRoleInvalid");

        var actual = ScanFilmProfileIssueNavigation.Resolve(
            issue,
            ScanFilmProfileIssueSource.CurrentDraft,
            ["Red"]);

        Assert.Null(actual);
    }

    [Theory]
    [InlineData("channelProfiles.Red")]
    [InlineData("channelProfiles.Red.parameters")]
    [InlineData("channelProfiles.Red.roiSettings")]
    public void Resolve_ChannelPathsWithoutASafeUniqueRoleTarget_ReturnsNoRequest(string fieldPath)
    {
        Assert.Null(ScanFilmProfileIssueNavigation.Resolve(CreateIssue(fieldPath), ScanFilmProfileIssueSource.CurrentDraft, []));
        Assert.Null(ScanFilmProfileIssueNavigation.Resolve(CreateIssue(fieldPath), ScanFilmProfileIssueSource.CurrentDraft, ["Unused"]));
        Assert.Null(ScanFilmProfileIssueNavigation.Resolve(CreateIssue(fieldPath), ScanFilmProfileIssueSource.CurrentDraft, ["Red", "red"]));
        Assert.Null(ScanFilmProfileIssueNavigation.Resolve(CreateIssue(fieldPath), ScanFilmProfileIssueSource.CurrentDraft, [" Red "]));
        Assert.Null(ScanFilmProfileIssueNavigation.Resolve(CreateIssue(fieldPath), ScanFilmProfileIssueSource.CurrentDraft, ["Green"]));
    }

    [Theory]
    [InlineData()]
    [InlineData("Unused")]
    [InlineData("Red", "red")]
    [InlineData(" Red ")]
    public void Resolve_GenericChannelProfilesWithoutASafeAvailableTarget_ReturnsNoRequest(params string[] availableRoles)
    {
        Assert.Null(ScanFilmProfileIssueNavigation.Resolve(
            CreateIssue("channelProfiles"),
            ScanFilmProfileIssueSource.CurrentDraft,
            availableRoles));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("document")]
    [InlineData("schemaVersion")]
    [InlineData("savedAtUtc")]
    [InlineData("RoiSelection")]
    [InlineData("rawIllumination.led1")]
    [InlineData("pendingCalibration.parameters")]
    [InlineData("acquisitionSettings.scanMotorId.someMember")]
    [InlineData("AcquisitionSettings.Rows.someMember")]
    [InlineData("scanRecipeSettings.colorManagement.redWavelengthNm")]
    [InlineData("ChannelProfiles.Selected.Parameters.exposure")]
    [InlineData("channelProfiles.Red.unknownSuffix")]
    [InlineData("channelProfiles.Unknown")]
    [InlineData("CHANNELPROFILES.Red")]
    public void Resolve_UnsupportedOrUnsafePath_ReturnsNoRequest(string fieldPath)
    {
        var actual = ScanFilmProfileIssueNavigation.Resolve(
            CreateIssue(fieldPath),
            ScanFilmProfileIssueSource.CurrentDraft,
            ["Red"]);

        Assert.Null(actual);
    }

    [Fact]
    public void Resolve_DoesNotMutateIssueOrAvailableRoles()
    {
        var messageArguments = new[] { "before" };
        var issue = new ScanFilmProfileValidationIssue(
            ScanFilmProfileValidationCode.InvalidChannelParameters,
            "channelProfiles.Red.parameters",
            ScanFilmProfileValidationSeverity.Error,
            "FilmProfile.Validation.ChannelParametersInvalid",
            messageArguments);
        var availableRoles = new[] { "Red", "Green" };

        var actual = ScanFilmProfileIssueNavigation.Resolve(issue, ScanFilmProfileIssueSource.CurrentDraft, availableRoles);

        Assert.Equal("channelProfiles.Red.parameters", issue.FieldPath);
        Assert.Equal("before", Assert.Single(issue.MessageArguments));
        Assert.Equal(new[] { "Red", "Green" }, availableRoles);
        Assert.Equal("Red", actual?.ChannelRole);
    }

    private static object[] Case(
        string fieldPath,
        ScanFilmProfileIssueNavigationSection section,
        ScanFilmProfileIssueEditorTarget editorTarget)
        => [fieldPath, section, editorTarget];

    private static ScanFilmProfileValidationIssue CreateIssue(string fieldPath)
        => new(
            ScanFilmProfileValidationCode.InvalidAcquisitionInput,
            fieldPath,
            ScanFilmProfileValidationSeverity.Error,
            "FilmProfile.Validation.AcquisitionInputInvalid");
}
