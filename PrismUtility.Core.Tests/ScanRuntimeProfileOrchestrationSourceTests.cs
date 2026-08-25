using Xunit;
using static PrismUtility.Core.Tests.ScanDebugFilmProfileOrchestrationSourceTests;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanRuntimeProfileOrchestrationSourceTests
{
    [Fact]
    public void Todo13Baseline_ScanProfileLoadRetainsExistingProjection()
    {
        var load = ExtractMethod(ReadScanViewModelSource(), "LoadConfigProfile");

        Assert.Contains("SelectedConfigProfileName", load, StringComparison.Ordinal);
        Assert.Contains("_selectedConfigAcquisitionSettings", load, StringComparison.Ordinal);
        Assert.Contains("ApplyAcquisitionSettingsToInputs", load, StringComparison.Ordinal);
        Assert.Contains("ApplyScanRecipeSettings", load, StringComparison.Ordinal);
        Assert.Contains("RefreshLoadedScanRecipeSummary", load, StringComparison.Ordinal);
        Assert.Contains("UpdateExecutionConfigSummary", load, StringComparison.Ordinal);
        Assert.Contains("UpdateReadinessSummaries", load, StringComparison.Ordinal);
        Assert.Contains("ClearStartValidationState", load, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo13Baseline_ScanWorkflowRequiresAConfigAndLooksUpEachRuntimeProfile()
    {
        var workflow = ExtractMethod(ReadScanViewModelSource(), "TryBuildWorkflowRequest");

        Assert.Contains("if (!_isConfigProfileLoaded)", workflow, StringComparison.Ordinal);
        Assert.Contains("TryGetProfile", workflow, StringComparison.Ordinal);
        Assert.True(
            workflow.IndexOf("if (!_isConfigProfileLoaded)", StringComparison.Ordinal)
            < workflow.IndexOf("TryGetProfile", StringComparison.Ordinal));
    }

    [Fact]
    public void Todo13DesiredContract_ScanRuntimeUsesOnlyNarrowProfileContracts()
    {
        var source = ReadScanViewModelSource();

        Assert.DoesNotContain("IScanChannelParameterProfileService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_channelProfiles", source, StringComparison.Ordinal);
        Assert.Contains("IScanCalibrationProfileRepository calibrationProfiles", source, StringComparison.Ordinal);
        Assert.Contains("IScanFilmProfileFileCoordinator filmProfileFiles", source, StringComparison.Ordinal);
        Assert.Contains("private readonly IScanCalibrationProfileRepository _calibrationProfiles;", source, StringComparison.Ordinal);
        Assert.Contains("private readonly IScanFilmProfileFileCoordinator _filmProfileFiles;", source, StringComparison.Ordinal);
        Assert.Contains("await _calibrationProfiles.InitializeAsync(CancellationToken.None);", source, StringComparison.Ordinal);
        Assert.Contains("_calibrationProfiles.Snapshot.Profiles", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo13DesiredContract_LoadSeparatesCanceledAndInvalidFilesBeforeOneReplacement()
    {
        var load = ExtractMethod(ReadScanViewModelSource(), "LoadConfigProfile");
        var importIndex = load.IndexOf("_filmProfileFiles.ImportAsync(CancellationToken.None)", StringComparison.Ordinal);
        var canceledIndex = load.IndexOf("if (imported.WasCanceled)", StringComparison.Ordinal);
        var invalidIndex = load.IndexOf("if (imported.Profile is null)", StringComparison.Ordinal);
        var returnIndex = load.IndexOf("return;", invalidIndex, StringComparison.Ordinal);
        var replaceIndex = load.IndexOf("_calibrationProfiles.ReplaceAsync(", StringComparison.Ordinal);

        Assert.True(importIndex >= 0, "Load must import through the file coordinator.");
        Assert.True(canceledIndex > importIndex, "Cancellation must be handled after import.");
        Assert.True(invalidIndex > canceledIndex, "Invalid imports must be handled separately from cancellation.");
        Assert.True(returnIndex > invalidIndex, "The null-profile branch must return.");
        Assert.True(replaceIndex > returnIndex, "Invalid imports must return before runtime profiles are replaced.");
        Assert.Contains("new ScanCalibrationProfileRepositorySnapshot(profile.ChannelProfiles, profile.SelectedCalibrationChannel)", load, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadConfigProfile_InvalidImportLocalizesFirstValidationIssueThroughValidationPresenter()
    {
        var source = ReadScanViewModelSource();
        var load = ExtractMethod(source, "LoadConfigProfile");

        Assert.Contains("using PRISM_Utility.Helpers;", source, StringComparison.Ordinal);
        Assert.Contains("FilmProfileValidationTextPresenter.GetValidationIssueText", load, StringComparison.Ordinal);
        Assert.DoesNotContain("?.MessageKey", load, StringComparison.Ordinal);
        Assert.DoesNotContain("GetLocalizedFormat(imported.Validation", load, StringComparison.Ordinal);
        Assert.DoesNotContain("GetLocalizedFormat(validationIssue.MessageKey", load, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo13DesiredContract_MissingRuntimeProfileBlocksInsteadOfFallingBack()
    {
        var workflow = ExtractMethod(ReadScanViewModelSource(), "TryBuildWorkflowRequest");

        Assert.Contains("if (!_calibrationProfiles.TryGetProfile", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("? profile.Parameters : fallbackSnapshot", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("fallbackSnapshot", workflow, StringComparison.Ordinal);
        Assert.Contains("return false;", workflow, StringComparison.Ordinal);
    }

    private static string ReadScanViewModelSource()
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", "ScanViewModel.cs"));

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the Host Software source root.");
    }

}
