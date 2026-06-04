namespace PRISM_Utility.Contracts.Services;

public sealed record DebugOutputMirrorEntry(DateTimeOffset Timestamp, string Source, string Message, string Display);

public interface IDebugOutputMirrorService
{
    event EventHandler<DebugOutputMirrorEntry>? EntryMirrored;

    IReadOnlyList<DebugOutputMirrorEntry> RecentEntries { get; }

    void Mirror(string source, string message);

    void ClearRecentEntries();
}
