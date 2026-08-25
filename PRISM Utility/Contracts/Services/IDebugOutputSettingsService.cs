namespace PRISM_Utility.Contracts.Services;

public interface IDebugOutputSettingsService
{
    bool IsDebugConsoleEnabled { get; }

    bool IsFileLogEnabled { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SetDebugConsoleEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    Task SetFileLogEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}
