using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PrismUtility.Core.Tests;

internal static class FilmProfileContractSource
{
    private static readonly Regex ResourceNamePattern = new("<data name=\"(?<name>[^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex PlaceholderPattern = new("\\{(?<index>\\d+)(?:[^}]*)\\}", RegexOptions.Compiled);
    private static readonly Regex XUidPattern = new("x:Uid=\"(?<uid>[^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex RuntimeMessagePattern = new("\"(?<key>FilmProfile\\.Validation\\.[A-Za-z0-9.]+)\"", RegexOptions.Compiled);
    private static readonly Regex XamlCommandBindingPattern = new("^\\{x:Bind\\s+ViewModel\\.(?<command>[A-Za-z_][A-Za-z0-9_]*)\\s*\\}$", RegexOptions.Compiled);
    private static readonly Regex DeclaredMethodPattern = new("\\b(?:public|private|protected|internal)\\s+(?:(?:static|async|partial|virtual|override|sealed|new)\\s+)*(?:[A-Za-z_]\\w*(?:<[^>\\r\\n]+>)?(?:\\[\\])?)\\s+(?<name>[A-Za-z_]\\w*)\\s*\\(", RegexOptions.Compiled);
    private static readonly HashSet<string> CodeBehindEventAttributes = new(StringComparer.Ordinal)
    {
        "Click",
        "CreateResources",
        "Draw",
        "Expanding",
        "Opening",
        "PointerCanceled",
        "PointerCaptureLost",
        "PointerExited",
        "PointerMoved",
        "PointerPressed",
        "PointerReleased",
        "PointerWheelChanged",
        "SelectionChanged",
        "SizeChanged",
        "TextChanged",
        "ViewChanged"
    };
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

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
            || key.StartsWith("ScanDebug_FilmProfileWorkbench", StringComparison.Ordinal)
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

    public static string[] GetXamlNames(string xaml)
        => ParseXml(xaml)
            .Descendants()
            .Select(element => element.Attribute(XName.Get("Name", XamlNamespace))?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyDictionary<string, int> GetXamlCommandBindingCounts(string xaml)
        => GetAttributeValues(xaml, "Command")
            .Select(value => XamlCommandBindingPattern.Match(value))
            .Where(match => match.Success)
            .Select(match => match.Groups["command"].Value)
            .GroupBy(command => command, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, int> GetXamlEventHandlerBindingCounts(string xaml)
        => ParseXml(xaml)
            .Descendants()
            .SelectMany(element => element.Attributes())
            .Where(attribute => CodeBehindEventAttributes.Contains(attribute.Name.LocalName))
            .Select(attribute => $"{attribute.Name.LocalName}={attribute.Value}")
            .GroupBy(binding => binding, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    public static string[] GetDeclaredMethodNames(string source)
        => DeclaredMethodPattern.Matches(source)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyDictionary<string, string> GetPackageReferences(string project)
        => ParseXml(project)
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => new
            {
                Include = element.Attribute("Include")?.Value,
                Version = element.Attribute("Version")?.Value
            })
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Include))
            .OrderBy(reference => reference.Include, StringComparer.Ordinal)
            .ToDictionary(reference => reference.Include!, reference => reference.Version ?? string.Empty, StringComparer.Ordinal);

    public static string[] FindMissingTokens(IEnumerable<string> requiredTokens, IEnumerable<string> actualTokens)
    {
        var actual = actualTokens.ToHashSet(StringComparer.Ordinal);
        return requiredTokens.Where(token => !actual.Contains(token)).ToArray();
    }

    public static string[] FindMissingTokens(IEnumerable<string> requiredTokens, string source)
        => requiredTokens.Where(token => !source.Contains(token, StringComparison.Ordinal)).ToArray();

    public static string[] FindMissingCommandBindings(IReadOnlyDictionary<string, int> requiredBindings, string xaml)
        => FindMissingCountedBindings(requiredBindings, GetXamlCommandBindingCounts(xaml));

    public static string[] FindMissingEventHandlerBindings(IReadOnlyDictionary<string, int> requiredBindings, string xaml)
        => FindMissingCountedBindings(requiredBindings, GetXamlEventHandlerBindingCounts(xaml));

    public static string[] FindMissingPackageReferences(IReadOnlyDictionary<string, string> requiredReferences, string project)
    {
        var actual = GetPackageReferences(project);
        return requiredReferences
            .Where(reference => !actual.TryGetValue(reference.Key, out var version) || !string.Equals(reference.Value, version, StringComparison.Ordinal))
            .Select(reference => reference.Key)
            .ToArray();
    }

    public static string ReadAppText(params string[] parts)
        => File.ReadAllText(Path.Combine([FindHostSoftwareRoot(), "PRISM Utility", .. parts]));

    public static string ReadCoreText(params string[] parts)
        => File.ReadAllText(Path.Combine([FindHostSoftwareRoot(), "PRISM Utility.Core", .. parts]));

    private static IEnumerable<string> GetAttributeValues(string xml, string attributeName)
        => ParseXml(xml)
            .Descendants()
            .Select(element => element.Attribute(attributeName)?.Value)
            .Where(value => value is not null)
            .Cast<string>();

    private static string[] FindMissingCountedBindings(IReadOnlyDictionary<string, int> requiredBindings, IReadOnlyDictionary<string, int> actualBindings)
        => requiredBindings
            .Where(required => !actualBindings.TryGetValue(required.Key, out var actualCount) || actualCount < required.Value)
            .Select(required => required.Key)
            .ToArray();

    private static XDocument ParseXml(string xml)
        => XDocument.Parse(xml, LoadOptions.PreserveWhitespace);

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
