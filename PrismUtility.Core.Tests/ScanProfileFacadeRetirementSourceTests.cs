using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanProfileFacadeRetirementSourceTests
{
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
        var whiteChannel = File.ReadAllText(Path.Combine(HostSoftwareRoot(), "PRISM Utility", "ViewModels", "ScanDebugViewModel.WhiteChannel.cs"));

        Assert.Contains("IScanCalibrationProfileRepository", scan, StringComparison.Ordinal);
        Assert.Contains("IScanFilmProfileFileCoordinator", scan, StringComparison.Ordinal);
        Assert.Contains("_calibrationProfiles.Snapshot.Profiles", scan, StringComparison.Ordinal);
        Assert.Contains("IScanCalibrationProfileRepository", scanDebug, StringComparison.Ordinal);
        Assert.Contains("_calibrationProfiles.Snapshot.Profiles", scanDebug, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.ApplyStagedImportAsync", scanDebug, StringComparison.Ordinal);
        Assert.Contains("if (!TryGetSavedCalibrationProfile(channelRole, out var profile))", scanDebug, StringComparison.Ordinal);
        var savedProfile = ScanDebugFilmProfileOrchestrationSourceTests.ExtractMethod(whiteChannel, "TryGetSavedCalibrationProfile");
        Assert.Contains("_calibrationProfiles.TryGetProfile(role, out profile!)", savedProfile, StringComparison.Ordinal);
        Assert.Contains("saved?.GetSourceSettings(SelectedWhiteLightSource)?.Profile", savedProfile, StringComparison.Ordinal);
        Assert.Contains("saved is null && SelectedWhiteLightSource == WhiteLightSource.DedicatedLed", savedProfile, StringComparison.Ordinal);
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

}
