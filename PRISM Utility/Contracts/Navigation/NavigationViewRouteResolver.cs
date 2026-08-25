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
