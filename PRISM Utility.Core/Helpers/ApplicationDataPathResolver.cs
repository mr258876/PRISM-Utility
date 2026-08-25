namespace PRISM_Utility.Core.Helpers;

public sealed record ApplicationDataPaths(
    string RootPath,
    string LocalSettingsPath,
    string DebugOutputLogPath,
    string LocalSettingsFileName);

public static class ApplicationDataPathResolver
{
    public const string DefaultApplicationDataFolder = "PRISM_Utility/ApplicationData";
    public const string DefaultLocalSettingsFile = "LocalSettings.json";

    public static ApplicationDataPaths Resolve(string? configuredApplicationDataFolder, string? localSettingsFile = null)
    {
        var applicationDataFolder = string.IsNullOrWhiteSpace(configuredApplicationDataFolder)
            ? DefaultApplicationDataFolder
            : configuredApplicationDataFolder;
        applicationDataFolder = applicationDataFolder.Replace('/', Path.DirectorySeparatorChar);
        var rootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            applicationDataFolder);
        var localSettingsFileName = localSettingsFile ?? DefaultLocalSettingsFile;

        return new ApplicationDataPaths(
            rootPath,
            Path.Combine(rootPath, localSettingsFileName),
            Path.Combine(rootPath, "Logs", "DebugOutput.log"),
            localSettingsFileName);
    }
}
