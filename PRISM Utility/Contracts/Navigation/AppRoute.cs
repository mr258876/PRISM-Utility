namespace PRISM_Utility.Contracts.Navigation;

public enum AppRoute
{
    Main,
    Log,
    Scan,
    ScanDebug,
    Settings,
    DeviceConfiguration
}

public readonly record struct AppNavigationTarget(AppRoute Route, object? Parameter)
{
    public const string DeviceSettingsParameter = "device-channels";

    public static AppNavigationTarget Resolve(AppRoute route, object? parameter = null)
        => route == AppRoute.DeviceConfiguration
            ? new(AppRoute.Settings, DeviceSettingsParameter)
            : new(route, parameter);
}
