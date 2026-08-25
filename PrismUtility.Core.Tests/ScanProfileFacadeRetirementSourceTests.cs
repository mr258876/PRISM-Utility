using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanProfileFacadeRetirementSourceTests
{
    [Fact]
    public void Todo13Baseline_FacadeDefinitionsExistOnlyAsRetirementCandidates()
    {
        var baseline = File.ReadAllText(Path.Combine(RepositoryRoot(), ".omo", "evidence", "task-13-scan-profile-editor-architecture-baseline.txt"));

        Assert.Contains("FacadeDefinitions=2", baseline, StringComparison.Ordinal);
        Assert.Contains("FacadeFiles=2", baseline, StringComparison.Ordinal);
        Assert.Contains("FacadeDi=1", baseline, StringComparison.Ordinal);
        Assert.Contains("FacadeCallers=0", baseline, StringComparison.Ordinal);
        Assert.Contains("RuntimeFacadeRefs=0", baseline, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo13DesiredContract_FacadeSourceFilesAndReferencesAreGone()
    {
        var appRoot = Path.Combine(HostSoftwareRoot(), "PRISM Utility");
        var sourceFiles = Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories).ToArray();
        var source = string.Join("\n", sourceFiles.Select(File.ReadAllText));

        Assert.Empty(Directory.EnumerateFiles(appRoot, "*ScanChannelParameterProfileService.cs", SearchOption.AllDirectories));
        Assert.DoesNotContain("IScanChannelParameterProfileService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ScanChannelParameterProfileService", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo13DesiredContract_AppHasNoFacadeRegistration()
    {
        var app = File.ReadAllText(Path.Combine(HostSoftwareRoot(), "PRISM Utility", "App.xaml.cs"));

        Assert.DoesNotContain("AddSingleton<IScanChannelParameterProfileService, ScanChannelParameterProfileService>()", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Todo13RuntimeContracts_ScanAndScanDebugRemainRepositoryBound()
    {
        var scan = File.ReadAllText(Path.Combine(HostSoftwareRoot(), "PRISM Utility", "ViewModels", "ScanViewModel.cs"));
        var scanDebug = File.ReadAllText(Path.Combine(HostSoftwareRoot(), "PRISM Utility", "ViewModels", "ScanDebugViewModel.cs"));

        Assert.Contains("IScanCalibrationProfileRepository", scan, StringComparison.Ordinal);
        Assert.Contains("IScanFilmProfileFileCoordinator", scan, StringComparison.Ordinal);
        Assert.Contains("_calibrationProfiles.Snapshot.Profiles", scan, StringComparison.Ordinal);
        Assert.Contains("IScanCalibrationProfileRepository", scanDebug, StringComparison.Ordinal);
        Assert.Contains("_calibrationProfiles.Snapshot.Profiles", scanDebug, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.ApplyStagedImportAsync", scanDebug, StringComparison.Ordinal);
        Assert.Contains("if (!_calibrationProfiles.TryGetProfile", scanDebug, StringComparison.Ordinal);
    }

    private static string HostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the Host Software source root.");
    }

    private static string RepositoryRoot()
        => Directory.GetParent(HostSoftwareRoot())!.FullName;
}
