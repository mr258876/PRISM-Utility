using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;

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

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        await _initializeGate.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            IsDebugConsoleEnabled = await _localSettingsService.ReadSettingAsync<bool?>(DebugConsoleEnabledKey) ?? false;
            IsFileLogEnabled = await _localSettingsService.ReadSettingAsync<bool?>(FileLogEnabledKey) ?? false;
            _isInitialized = true;
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    public async Task SetDebugConsoleEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        if (IsDebugConsoleEnabled == enabled)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        await _localSettingsService.SaveSettingAsync(DebugConsoleEnabledKey, enabled);
        IsDebugConsoleEnabled = enabled;
    }

    public async Task SetFileLogEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        if (IsFileLogEnabled == enabled)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        await _localSettingsService.SaveSettingAsync(FileLogEnabledKey, enabled);
        IsFileLogEnabled = enabled;
    }
}
