using System.Text.Json;
using PRISM_Utility.Core.Helpers;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Set002")]
public sealed class Set002SettingsPathTests
{
    [Fact]
    public void Set002Baseline_ConfiguredStartupRoot_IsSharedByConfiguredSettingsAndLogPaths()
    {
        var configuredRoot = ReadConfiguredApplicationDataFolder();
        var paths = ApplicationDataPathResolver.Resolve(configuredRoot);

        Assert.Equal("PRISM_Utility/ApplicationData", configuredRoot);
        Assert.Equal(NormalizePath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), configuredRoot)), NormalizePath(paths.RootPath));
        Assert.Equal(paths.RootPath, Path.GetDirectoryName(paths.LocalSettingsPath));
        Assert.Equal(Path.Combine(paths.RootPath, "Logs"), Path.GetDirectoryName(paths.DebugOutputLogPath));
        Assert.NotEqual(Path.GetFileName(paths.LocalSettingsPath), Path.GetFileName(paths.DebugOutputLogPath));
        Assert.Equal("LocalSettings.json", Path.GetFileName(paths.LocalSettingsPath));
        Assert.Equal("DebugOutput.log", Path.GetFileName(paths.DebugOutputLogPath));
    }

    [Theory]
    [InlineData("PRISM_Utility/ApplicationData", "PRISM_Utility/ApplicationData")]
    [InlineData(null, "PRISM_Utility/ApplicationData")]
    [InlineData("", "PRISM_Utility/ApplicationData")]
    [InlineData("   ", "PRISM_Utility/ApplicationData")]
    [InlineData("ReviewOnly/CustomRoot", "ReviewOnly/CustomRoot")]
    public void Set002RuntimeResolution_ConfiguredAndBlankRootsShareRootWithDistinctFiles(string? configuredRoot, string expectedRelativeRoot)
    {
        var paths = ApplicationDataPathResolver.Resolve(configuredRoot);
        var expectedRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), expectedRelativeRoot);

        Assert.Equal(NormalizePath(expectedRoot), NormalizePath(paths.RootPath));
        Assert.Equal(paths.RootPath, Path.GetDirectoryName(paths.LocalSettingsPath));
        Assert.Equal(Path.Combine(paths.RootPath, "Logs"), Path.GetDirectoryName(paths.DebugOutputLogPath));
        Assert.NotEqual(Path.GetFileName(paths.LocalSettingsPath), Path.GetFileName(paths.DebugOutputLogPath));
    }

    private static string ReadConfiguredApplicationDataFolder()
    {
        var appSettingsPath = Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
        return document.RootElement
            .GetProperty("LocalSettingsOptions")
            .GetProperty("ApplicationDataFolder")
            .GetString()
            ?? throw new InvalidOperationException("appsettings.json does not define a non-null application-data folder.");
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate Host Software source root.");
    }
}
