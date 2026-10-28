using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

public sealed partial class LogPage : Page, IPageViewModelHost<LogViewModel>
{
    public LogViewModel ViewModel
    {
        get;
    }

    public LogPage()
    {
        ViewModel = App.GetService<LogViewModel>();
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.VisibleEntriesChanged += OnVisibleEntriesChanged;
        ViewModel.Activate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.VisibleEntriesChanged -= OnVisibleEntriesChanged;
        ViewModel.Deactivate();
    }

    private void OnVisibleEntriesChanged(object? sender, EventArgs e)
    {
        if (IsLoaded && ViewModel.IsFollowing && ViewModel.Entries.Count > 0)
            LogEntriesList.ScrollIntoView(ViewModel.Entries[^1]);
    }

    private void CopyVisibleErrorsButton_Click(object sender, RoutedEventArgs e)
        => CopyText(ViewModel.GetVisibleErrorsText());

    private void CopySelectedButton_Click(object sender, RoutedEventArgs e)
        => CopyText(ViewModel.SelectedEntry?.Display ?? string.Empty);

    private void CopyText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            ViewModel.CopyStatusText = "Log_CopySucceeded".GetLocalizedOrFallback("Copied to clipboard.");
        }
        catch (Exception)
        {
            ViewModel.CopyStatusText = "Log_CopyFailed".GetLocalizedOrFallback("Could not copy. Try again.");
        }
    }
}
