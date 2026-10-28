using Microsoft.UI.Xaml.Controls;

using Microsoft.UI.Xaml;
using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

public sealed partial class MainPage : Page, IPageViewModelHost<MainViewModel>
{
    public MainViewModel ViewModel
    {
        get;
    }

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => ViewModel.Activate();

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => ViewModel.Deactivate();

    private void WorkflowCards_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var useColumns = e.NewSize.Width >= 760;
        WorkflowCards.ColumnSpacing = useColumns ? 16 : 0;
        SecondaryWorkflowColumn.Width = useColumns
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        Grid.SetColumn(CalibrationWorkflowCard, useColumns ? 1 : 0);
        Grid.SetRow(CalibrationWorkflowCard, useColumns ? 0 : 1);
    }
}
