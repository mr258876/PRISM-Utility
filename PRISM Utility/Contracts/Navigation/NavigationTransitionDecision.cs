namespace PRISM_Utility.Contracts.Navigation;

public sealed record NavigationTransitionDecision(bool ShouldNavigate, bool ClearNavigation, object? Parameter)
{
    public object? ApplyNavigationResult(bool navigated, object? lastParameter)
        => navigated ? Parameter : lastParameter;
}

public static class NavigationTransitionDecider
{
    public static NavigationTransitionDecision Decide(Type? currentPageType, Type targetPageType, object? parameter, object? lastParameter, bool clearNavigation)
        => new(currentPageType != targetPageType || (parameter is not null && !parameter.Equals(lastParameter)), clearNavigation, parameter);

    public static bool CanGoBack(bool hasFrame, bool frameCanGoBack)
        => hasFrame && frameCanGoBack;

    public static bool ShouldClearBackStack(object? frameTag)
        => frameTag is true;
}

public sealed class NavigationCompletionTracker
{
    private long _sequence;

    public void NotifyNavigated() => _sequence++;

    public bool TryNavigate(Func<bool> requestNavigation)
    {
        var previousSequence = _sequence;
        // WinUI can accept a request while OnNavigatingFrom cancels it.
        // Only a Navigated event confirms that the current page changed.
        return requestNavigation() && _sequence != previousSequence;
    }
}
