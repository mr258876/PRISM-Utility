using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

public sealed partial class ScanPage : Page
{
    public ScanViewModel ViewModel
    {
        get;
    }

    public ScanPage()
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stepStopwatch = Stopwatch.StartNew();
        ViewModel = App.GetService<ScanViewModel>();
        NavigationTimingLogger.Write($"ScanPage.ctor GetService<ScanViewModel>={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        InitializeComponent();
        NavigationTimingLogger.Write($"ScanPage.ctor InitializeComponent={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        totalStopwatch.Stop();
        NavigationTimingLogger.Write($"ScanPage.ctor total={totalStopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private void DeferredExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (sender.Tag is string contentName)
            FindName(contentName);
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var stopwatch = Stopwatch.StartNew();
        ViewModel.Activate();
        stopwatch.Stop();
        NavigationTimingLogger.Write($"ScanPage.Loaded Activate={stopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var stopwatch = Stopwatch.StartNew();
        ViewModel.Deactivate();
        stopwatch.Stop();
        NavigationTimingLogger.Write($"ScanPage.Unloaded Deactivate={stopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }
}
