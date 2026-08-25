using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PrismUtility.Core.Tests;

internal static class FilmProfileContractSource
{
    private static readonly Regex ResourceNamePattern = new("<data name=\"(?<name>[^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex PlaceholderPattern = new("\\{(?<index>\\d+)(?:[^}]*)\\}", RegexOptions.Compiled);
    private static readonly Regex XUidPattern = new("x:Uid=\"(?<uid>[^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex RuntimeMessagePattern = new("\"(?<key>FilmProfile\\.Validation\\.[A-Za-z0-9.]+)\"", RegexOptions.Compiled);

    public static XDocument ReadResourceDocument(string culture)
        => XDocument.Parse(File.ReadAllText(ResourcePath(culture)), LoadOptions.PreserveWhitespace);

    public static Dictionary<string, string> ReadResources(string culture)
        => ReadResourceDocument(culture)
            .Descendants("data")
            .Where(element => element.Attribute("name")?.Value is not null)
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);

    public static HashSet<string> ReadResourceNames(string culture)
        => ResourceNamePattern.Matches(File.ReadAllText(ResourcePath(culture)))
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

    public static IEnumerable<string> GetFilmProfileResourceKeys(IEnumerable<string> keys)
        => keys.Where(key =>
            key.StartsWith("FilmProfile_Validation_", StringComparison.Ordinal)
            || key.StartsWith("ScanDebug_FilmProfileWorkspace", StringComparison.Ordinal)
            || key is "ScanDebug_SaveJsonButton.Content" or "ScanDebug_LoadJsonButton.Content"
            || key.StartsWith("ScanDebug_Runtime_StatusFilmProfile", StringComparison.Ordinal));

    public static string[] GetScanDebugXamlUids()
        => XUidPattern.Matches(ReadAppText("Views", "ScanDebugPage.xaml"))
            .Select(match => match.Groups["uid"].Value)
            .Where(uid => uid.StartsWith("ScanDebug_FilmProfileWorkspace", StringComparison.Ordinal)
                || uid is "ScanDebug_SaveJsonButton" or "ScanDebug_LoadJsonButton")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static string[] ExtractRuntimeMessageKeys(params string[] sources)
        => sources
            .SelectMany(source => RuntimeMessagePattern.Matches(source).Select(match => match.Groups["key"].Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    public static string[] ExtractEnumNames(string source, string enumName)
    {
        var match = Regex.Match(source, $"public enum {enumName}\\s*\\{{(?<body>.*?)\\}}", RegexOptions.Singleline);
        if (!match.Success)
            throw new InvalidOperationException($"Could not find enum {enumName}.");

        return match.Groups["body"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', StringSplitOptions.TrimEntries)[0].Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
    }

    public static string[] GetPlaceholderIndexes(string value)
        => PlaceholderPattern.Matches(value)
            .Select(match => match.Groups["index"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    public static string[] FindMissingKeys(IEnumerable<string> requiredKeys, ISet<string> existingKeys)
        => requiredKeys.Where(key => !existingKeys.Contains(key)).ToArray();

    public static string ReadAppText(params string[] parts)
        => File.ReadAllText(Path.Combine([FindHostSoftwareRoot(), "PRISM Utility", .. parts]));

    public static string ReadCoreText(params string[] parts)
        => File.ReadAllText(Path.Combine([FindHostSoftwareRoot(), "PRISM Utility.Core", .. parts]));

    private static string ResourcePath(string culture)
        => Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "Strings", culture, "Resources.resw");

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
