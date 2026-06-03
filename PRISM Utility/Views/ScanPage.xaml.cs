using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var stopwatch = Stopwatch.StartNew();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.Activate();
        stopwatch.Stop();
        NavigationTimingLogger.Write($"ScanPage.Loaded Activate={stopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var stopwatch = Stopwatch.StartNew();
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.Deactivate();
        stopwatch.Stop();
        NavigationTimingLogger.Write($"ScanPage.Unloaded Deactivate={stopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScanViewModel.FirstBlockingCardId))
            BringBlockingCardIntoView(ViewModel.FirstBlockingCardId);
    }

    private void BringBlockingCardIntoView(string blockerId)
    {
        FrameworkElement? target = blockerId switch
        {
            ScanWorkflowBlockerIds.Device => DevicePreparationCard,
            ScanWorkflowBlockerIds.Configuration => ConfigurationPreparationCard,
            ScanWorkflowBlockerIds.Execution => ExecutionPreparationCard,
            ScanWorkflowBlockerIds.Output => OutputPreparationCard,
            _ => null
        };

        if (target is null)
            return;

        var position = target.TransformToVisual(WorkflowScrollViewer).TransformPoint(new Windows.Foundation.Point());
        WorkflowScrollViewer.ChangeView(null, Math.Max(0, WorkflowScrollViewer.VerticalOffset + position.Y - 24), null);
        target.StartBringIntoView();
    }
}
