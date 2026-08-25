using PRISM_Utility.Contracts.Navigation;
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

    private sealed class CurrentPage;

    private sealed class NextPage;
}
