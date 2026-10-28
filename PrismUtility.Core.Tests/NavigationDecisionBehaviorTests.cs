using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Contracts.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "UI002")]
public sealed class NavigationDecisionBehaviorTests
{
    [Fact]
    public void SameRouteWithNullParameterSuppressesNavigationAndKeepsLastParameter()
    {
        var lastParameter = new object();
        var decision = NavigationTransitionDecider.Decide(typeof(CurrentPage), typeof(CurrentPage), null, lastParameter, clearNavigation: false);

        Assert.False(decision.ShouldNavigate);
        Assert.False(decision.ClearNavigation);
        Assert.Null(decision.Parameter);
        Assert.Same(lastParameter, decision.ApplyNavigationResult(navigated: false, lastParameter));
    }

    [Fact]
    public void SameRouteWithSameNonNullParameterSuppressesNavigation()
    {
        const string parameter = "profile-1";
        var decision = NavigationTransitionDecider.Decide(typeof(CurrentPage), typeof(CurrentPage), parameter, parameter, clearNavigation: false);

        Assert.False(decision.ShouldNavigate);
        Assert.Equal(parameter, decision.ApplyNavigationResult(navigated: false, parameter));
    }

    [Fact]
    public void SameRouteWithDifferentParameterNavigatesAndUpdatesLastParameterAfterSuccess()
    {
        var decision = NavigationTransitionDecider.Decide(typeof(CurrentPage), typeof(CurrentPage), "profile-2", "profile-1", clearNavigation: false);

        Assert.True(decision.ShouldNavigate);
        Assert.Equal("profile-2", decision.ApplyNavigationResult(navigated: true, "profile-1"));
        Assert.Equal("profile-1", decision.ApplyNavigationResult(navigated: false, "profile-1"));
    }

    [Fact]
    public void DifferentRouteWithNullParameterNavigatesWithoutOverwritingLastParameterWhenFrameNavigateFails()
    {
        var lastParameter = new object();
        var decision = NavigationTransitionDecider.Decide(typeof(CurrentPage), typeof(NextPage), null, lastParameter, clearNavigation: false);

        Assert.True(decision.ShouldNavigate);
        Assert.Null(decision.Parameter);
        Assert.Same(lastParameter, decision.ApplyNavigationResult(navigated: false, lastParameter));
        Assert.Null(decision.ApplyNavigationResult(navigated: true, lastParameter));
    }

    [Fact]
    public void AcceptedButCanceledFrameRequestKeepsTheParameterAndDoesNotNotifyDeparture()
    {
        var completion = new NavigationCompletionTracker();
        var outgoing = new TrackedViewModel();
        var lastParameter = new object();
        var decision = NavigationTransitionDecider.Decide(typeof(CurrentPage), typeof(NextPage), "next", lastParameter, false);

        // Frame.Navigate returns true for a request canceled by OnNavigatingFrom, without Navigated.
        var completed = completion.TryNavigate(() => true);
        NavigationLifecycleDispatcher.NotifyNavigatedFromAfterFrameResult(outgoing, completed);

        Assert.False(completed);
        Assert.Same(lastParameter, decision.ApplyNavigationResult(completed, lastParameter));
        Assert.Equal(0, outgoing.DepartureCount);
    }

    [Fact]
    public void ActualNavigatedEventCompletesTheRequestAndOnlyThatRequestNotifiesDeparture()
    {
        var completion = new NavigationCompletionTracker();
        var outgoing = new TrackedViewModel();
        var decision = NavigationTransitionDecider.Decide(typeof(CurrentPage), typeof(NextPage), "next", "previous", false);

        var completed = completion.TryNavigate(() =>
        {
            completion.NotifyNavigated();
            return true;
        });
        NavigationLifecycleDispatcher.NotifyNavigatedFromAfterFrameResult(outgoing, completed);

        Assert.True(completed);
        Assert.Equal("next", decision.ApplyNavigationResult(completed, "previous"));
        Assert.Equal(1, outgoing.DepartureCount);
        Assert.False(completion.TryNavigate(() => true)); // An earlier event cannot complete a later canceled request.
    }

    [Fact]
    public void ClearNavigationDecisionIsCarriedToBackStackClearCheck()
    {
        var decision = NavigationTransitionDecider.Decide(null, typeof(CurrentPage), null, null, clearNavigation: true);

        Assert.True(decision.ShouldNavigate);
        Assert.True(decision.ClearNavigation);
        Assert.True(NavigationTransitionDecider.ShouldClearBackStack(decision.ClearNavigation));
        Assert.False(NavigationTransitionDecider.ShouldClearBackStack(false));
        Assert.False(NavigationTransitionDecider.ShouldClearBackStack(null));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void BackDecisionRequiresAFrameThatCanGoBack(bool hasFrame, bool frameCanGoBack, bool expected)
        => Assert.Equal(expected, NavigationTransitionDecider.CanGoBack(hasFrame, frameCanGoBack));

    [Fact]
    public void NavigationViewResolverRoutesSettingsAndSuppressesUnsetItems()
    {
        var settings = NavigationViewRouteResolver.Resolve(isSettingsInvoked: true, itemRoute: null);
        var routed = NavigationViewRouteResolver.Resolve(isSettingsInvoked: false, itemRoute: AppRoute.ScanDebug);
        var unset = NavigationViewRouteResolver.Resolve(isSettingsInvoked: false, itemRoute: null);

        Assert.True(settings.ShouldNavigate);
        Assert.Equal(AppRoute.Settings, settings.Route);
        Assert.Null(settings.Diagnostic);
        Assert.True(routed.ShouldNavigate);
        Assert.Equal(AppRoute.ScanDebug, routed.Route);
        Assert.False(unset.ShouldNavigate);
        Assert.Null(unset.Route);
        Assert.Equal("NavigationView item invoked without an AppRoute.", unset.Diagnostic);
    }

    [Fact]
    public void CanceledNavigationRestoresSettingsAfterTheControlFinishesSelectingTheInvokedItem()
    {
        var dispatcher = new Queue<Action>();
        var settingsItem = new object();
        var clickedItem = new object();
        object? selected = settingsItem;

        NavigationViewSelectionRestorer.Schedule(
            action => { dispatcher.Enqueue(action); return true; },
            () => typeof(SettingsPage),
            typeof(SettingsPage), settingsItem,
            _ => throw new InvalidOperationException("Settings uses its dedicated item."),
            item => selected = item);
        selected = clickedItem; // NavigationView completes its own ItemInvoked selection.
        dispatcher.Dequeue()();

        Assert.Same(settingsItem, selected);
    }

    [Fact]
    public void DelayedSelectionRestoreUsesTheLatestActualPageAfterAnotherNavigationSucceeds()
    {
        var dispatcher = new Queue<Action>();
        var settingsItem = new object();
        var nextPageItem = new object();
        Type? currentPage = typeof(SettingsPage);
        object? selected = null;

        NavigationViewSelectionRestorer.Schedule(
            action => { dispatcher.Enqueue(action); return true; },
            () => currentPage,
            typeof(SettingsPage), settingsItem,
            page => page == typeof(NextPage) ? nextPageItem : null,
            item => selected = item);
        currentPage = typeof(NextPage);
        dispatcher.Dequeue()();

        Assert.Same(nextPageItem, selected);
    }

    [Fact]
    public void SelectionRestoreDoesNotChangeAReplacedOrDetachedNavigationView()
    {
        var dispatcher = new Queue<Action>();
        Type? currentPage = typeof(SettingsPage);
        var selected = new object();
        var previousSelection = selected;

        NavigationViewSelectionRestorer.Schedule(
            action => { dispatcher.Enqueue(action); return true; },
            () => currentPage,
            typeof(SettingsPage), new object(),
            _ => null,
            item => selected = item);
        currentPage = null; // The originating shell was detached before dispatch.
        dispatcher.Dequeue()();

        Assert.Same(previousSelection, selected);
    }

    private sealed class CurrentPage;

    private sealed class NextPage;

    private sealed class SettingsPage;

    private sealed class TrackedViewModel : INavigationAware
    {
        public int DepartureCount { get; private set; }
        public void OnNavigatedTo(object parameter) { }
        public void OnNavigatedFrom() => DepartureCount++;
    }
}
