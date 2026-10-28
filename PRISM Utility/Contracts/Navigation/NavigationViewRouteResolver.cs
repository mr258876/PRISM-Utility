namespace PRISM_Utility.Contracts.Navigation;

public sealed record NavigationViewRouteResolution(AppRoute? Route, string? Diagnostic)
{
    public bool ShouldNavigate => Route.HasValue;
}

public static class NavigationViewRouteResolver
{
    public static NavigationViewRouteResolution Resolve(bool isSettingsInvoked, AppRoute? itemRoute)
    {
        if (isSettingsInvoked)
            return new(AppRoute.Settings, null);

        return itemRoute.HasValue
            ? new(itemRoute.Value, null)
            : new(null, "NavigationView item invoked without an AppRoute.");
    }
}

public static class NavigationViewSelectionRestorer
{
    public static void Schedule(
        Func<Action, bool> enqueue,
        Func<Type?> getCurrentPageType,
        Type settingsPageType,
        object? settingsItem,
        Func<Type, object?> findMenuItem,
        Action<object?> setSelectedItem)
    {
        enqueue(() =>
        {
            // Resolve at dispatch time, after the control's selection event and any later navigation.
            var currentPageType = getCurrentPageType();
            if (currentPageType is null)
                return;

            setSelectedItem(currentPageType == settingsPageType
                ? settingsItem
                : findMenuItem(currentPageType));
        });
    }
}
