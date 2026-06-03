using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using CommunityToolkit.WinUI.Animations;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.Services;

// For more information on navigation between pages see
// https://github.com/microsoft/TemplateStudio/blob/main/docs/WinUI/navigation.md
public class NavigationService : INavigationService
{
    private readonly IPageService _pageService;
    private object? _lastParameterUsed;
    private Frame? _frame;

    public event NavigatedEventHandler? Navigated;

    public Frame? Frame
    {
        get
        {
            if (_frame == null)
            {
                _frame = App.MainWindow.Content as Frame;
                RegisterFrameEvents();
            }

            return _frame;
        }

        set
        {
            UnregisterFrameEvents();
            _frame = value;
            RegisterFrameEvents();
        }
    }

    [MemberNotNullWhen(true, nameof(Frame), nameof(_frame))]
    public bool CanGoBack => Frame != null && Frame.CanGoBack;

    public NavigationService(IPageService pageService)
    {
        _pageService = pageService;
    }

    private void RegisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated += OnNavigated;
        }
    }

    private void UnregisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated -= OnNavigated;
        }
    }

    public bool GoBack()
    {
        if (CanGoBack)
        {
            var vmBeforeNavigation = _frame.GetPageViewModel();
            _frame.GoBack();
            if (vmBeforeNavigation is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }

            return true;
        }

        return false;
    }

    public bool NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false)
    {
        var pageType = _pageService.GetPageType(pageKey);

        if (_frame != null && (_frame.Content?.GetType() != pageType || (parameter != null && !parameter.Equals(_lastParameterUsed))))
        {
            var currentPageName = _frame.Content?.GetType().Name ?? "(none)";
            var targetPageName = pageType.Name;
            var totalStopwatch = Stopwatch.StartNew();
            NavigationTimingLogger.Write($"Start {currentPageName} -> {targetPageName}, clearNavigation={clearNavigation}, hasParameter={parameter is not null}");

            _frame.Tag = clearNavigation;
            var vmBeforeNavigation = _frame.GetPageViewModel();
            var frameNavigateStopwatch = Stopwatch.StartNew();
            var navigated = _frame.Navigate(pageType, parameter);
            frameNavigateStopwatch.Stop();
            if (navigated)
            {
                _lastParameterUsed = parameter;
                if (vmBeforeNavigation is INavigationAware navigationAware)
                {
                    navigationAware.OnNavigatedFrom();
                }
            }

            totalStopwatch.Stop();
            NavigationTimingLogger.Write($"End {currentPageName} -> {targetPageName}, navigated={navigated}, frameNavigate={frameNavigateStopwatch.Elapsed.TotalMilliseconds:0.0} ms, total={totalStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

            return navigated;
        }

        return false;
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        if (sender is Frame frame)
        {
            var stopwatch = Stopwatch.StartNew();
            var clearNavigation = (bool)frame.Tag;
            if (clearNavigation)
            {
                frame.BackStack.Clear();
            }

            if (frame.GetPageViewModel() is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedTo(e.Parameter);
            }

            Navigated?.Invoke(sender, e);
            stopwatch.Stop();
            var pageName = frame.Content?.GetType().Name ?? "(unknown)";
            NavigationTimingLogger.Write($"OnNavigated {pageName}, callback={stopwatch.Elapsed.TotalMilliseconds:0.0} ms");
        }
    }

    public void SetListDataItemForNextConnectedAnimation(object item) => Frame?.SetListDataItemForNextConnectedAnimation(item);
}
