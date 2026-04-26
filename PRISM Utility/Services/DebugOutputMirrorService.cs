using System.Diagnostics;
using Microsoft.Extensions.Options;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Models;

namespace PRISM_Utility.Services;

public sealed class DebugOutputMirrorService : IDebugOutputMirrorService
{
    private readonly IDebugOutputSettingsService _settingsService;
    private readonly SemaphoreSlim _logFileGate = new(1, 1);
    private readonly string _logFilePath;

    public DebugOutputMirrorService(IDebugOutputSettingsService settingsService, IOptions<LocalSettingsOptions> options)
    {
        _settingsService = settingsService;

        var applicationDataFolder = options.Value.ApplicationDataFolder ?? "PRISM_Utility/ApplicationData";
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logFilePath = Path.Combine(localApplicationData, applicationDataFolder, "Logs", "DebugOutput.log");
    }

    public void Mirror(string source, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {message}";
        if (_settingsService.IsDebugConsoleEnabled)
        {
            Debug.WriteLine(line);
            Trace.WriteLine(line);
        }

        if (_settingsService.IsFileLogEnabled)
            _ = AppendLineAsync(line);
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
