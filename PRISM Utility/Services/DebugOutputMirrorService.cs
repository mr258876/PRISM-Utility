using System.Diagnostics;
using Microsoft.Extensions.Options;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Models;

namespace PRISM_Utility.Services;

public sealed class DebugOutputMirrorService : IDebugOutputMirrorService
{
    private const int MaxRecentEntryCount = 500;

    private readonly IDebugOutputSettingsService _settingsService;
    private readonly SemaphoreSlim _logFileGate = new(1, 1);
    private readonly object _recentEntriesGate = new();
    private readonly List<DebugOutputMirrorEntry> _recentEntries = new();
    private readonly string _logFilePath;

    public DebugOutputMirrorService(IDebugOutputSettingsService settingsService, IOptions<LocalSettingsOptions> options)
    {
        _settingsService = settingsService;

        var applicationDataFolder = options.Value.ApplicationDataFolder ?? "PRISM_Utility/ApplicationData";
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logFilePath = Path.Combine(localApplicationData, applicationDataFolder, "Logs", "DebugOutput.log");
    }

    public event EventHandler<DebugOutputMirrorEntry>? EntryMirrored;

    public IReadOnlyList<DebugOutputMirrorEntry> RecentEntries
    {
        get
        {
            lock (_recentEntriesGate)
            {
                return _recentEntries.ToArray();
            }
        }
    }

    public void Mirror(string source, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var timestamp = DateTimeOffset.Now;
        var line = $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {message}";
        var entry = new DebugOutputMirrorEntry(timestamp, source, message, line);
        AddRecentEntry(entry);
        EntryMirrored?.Invoke(this, entry);

        if (_settingsService.IsDebugConsoleEnabled)
        {
            Debug.WriteLine(line);
            Trace.WriteLine(line);
        }

        if (_settingsService.IsFileLogEnabled)
            _ = AppendLineAsync(line);
    }

    public void ClearRecentEntries()
    {
        lock (_recentEntriesGate)
        {
            _recentEntries.Clear();
        }
    }

    private void AddRecentEntry(DebugOutputMirrorEntry entry)
    {
        lock (_recentEntriesGate)
        {
            _recentEntries.Add(entry);
            if (_recentEntries.Count > MaxRecentEntryCount)
                _recentEntries.RemoveRange(0, _recentEntries.Count - MaxRecentEntryCount);
        }
    }

    private async Task AppendLineAsync(string line)
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(_logFilePath);
            if (string.IsNullOrWhiteSpace(directoryPath))
                return;

            Directory.CreateDirectory(directoryPath);

            await _logFileGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine).ConfigureAwait(false);
            }
            finally
            {
                _logFileGate.Release();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DebugOutputMirror] Failed to append log file: {ex.Message}");
        }
    }
}
