using PRISM_Utility.Contracts.Services;

namespace PRISM_Utility.Services;

public sealed class DebugOutputSettingsService : IDebugOutputSettingsService
{
    private const string DebugConsoleEnabledKey = "DebugConsoleMirrorEnabled";
    private const string FileLogEnabledKey = "DebugFileLogEnabled";

    private readonly ILocalSettingsService _localSettingsService;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private bool _isInitialized;

    public bool IsDebugConsoleEnabled { get; private set; }

    public bool IsFileLogEnabled { get; private set; }

    public DebugOutputSettingsService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await _initializeGate.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            IsDebugConsoleEnabled = await _localSettingsService.ReadSettingAsync<bool?>(DebugConsoleEnabledKey) ?? false;
            IsFileLogEnabled = await _localSettingsService.ReadSettingAsync<bool?>(FileLogEnabledKey) ?? false;
            _isInitialized = true;
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    public async Task SetDebugConsoleEnabledAsync(bool enabled)
    {
        await InitializeAsync();
        if (IsDebugConsoleEnabled == enabled)
            return;

        IsDebugConsoleEnabled = enabled;
        await _localSettingsService.SaveSettingAsync(DebugConsoleEnabledKey, enabled);
    }

    public async Task SetFileLogEnabledAsync(bool enabled)
    {
        await InitializeAsync();
        if (IsFileLogEnabled == enabled)
            return;

        IsFileLogEnabled = enabled;
        await _localSettingsService.SaveSettingAsync(FileLogEnabledKey, enabled);
    }
}
