namespace PRISM_Utility.Contracts.Services;

public interface IDebugOutputSettingsService
{
    bool IsDebugConsoleEnabled { get; }

    bool IsFileLogEnabled { get; }

    Task InitializeAsync();

    Task SetDebugConsoleEnabledAsync(bool enabled);

    Task SetFileLogEnabledAsync(bool enabled);
}
