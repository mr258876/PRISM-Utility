using System.Text.RegularExpressions;
using PRISM_Utility.Contracts.Services;

namespace PRISM_Utility.Helpers;

public enum LogEntryLevel { Unknown, Information, Warning, Error }

public sealed record LogDisplayEntry(DebugOutputMirrorEntry Entry, LogEntryLevel Level)
{
    public string Display => Entry.Display;
    public override string ToString() => $"{LevelText}: {Display}";
    public string LevelText => Level switch
    {
        LogEntryLevel.Error => "Log_LevelError".GetLocalizedOrFallback("Error"),
        LogEntryLevel.Warning => "Log_LevelWarning".GetLocalizedOrFallback("Warning"),
        LogEntryLevel.Information => "Log_LevelInformation".GetLocalizedOrFallback("Info"),
        _ => "Log_LevelUnknown".GetLocalizedOrFallback("Unclassified")
    };
}

public static class LogEntryPresentation
{
    private static readonly Regex LevelMarker = new(@"\[(error|fatal|warn(?:ing)?|info|debug|trace)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Failure = new(@"\b(?:failed|failure|fatal|[a-z]*exception)\b|\berror\s*:|失败|错误|异常", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Warning = new(@"\b(?:warn(?:ing)?|timed out)\b|警告|超时", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static LogEntryLevel Classify(DebugOutputMirrorEntry entry)
    {
        var text = entry.Source + " " + entry.Message;
        var marker = LevelMarker.Match(text);
        if (marker.Success)
            return marker.Groups[1].Value.ToLowerInvariant() switch
            {
                "error" or "fatal" => LogEntryLevel.Error,
                "warn" or "warning" => LogEntryLevel.Warning,
                _ => LogEntryLevel.Information
            };
        if (Failure.IsMatch(text))
            return LogEntryLevel.Error;
        return Warning.IsMatch(text) ? LogEntryLevel.Warning : LogEntryLevel.Unknown;
    }

    public static bool Matches(LogDisplayEntry entry, string? query, string? levelFilter)
        => (string.IsNullOrWhiteSpace(query) || entry.Display.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrEmpty(levelFilter) || levelFilter == "All"
                || string.Equals(entry.Level.ToString(), levelFilter, StringComparison.Ordinal));
}
