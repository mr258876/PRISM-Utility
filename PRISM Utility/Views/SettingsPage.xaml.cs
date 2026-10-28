using System.Diagnostics;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

using PRISM_Utility.Contracts.ViewModels;
using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;

namespace PRISM_Utility.Views;

public sealed partial class SettingsPage : Page, IPageViewModelHost<SettingsViewModel>
{
    private enum SettingsSection
    {
        Overview,
        ScanDefaults,
        Device,
        Advanced
    }

    private SettingsSection _section;
    private double _overviewVerticalOffset;
    private Button? _overviewFocusTarget;
    private bool _isLeaveDialogOpen;
    private bool _allowNavigationAfterSave;

    public SettingsViewModel ViewModel
    {
        get;
    }

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        ViewModel.ConfirmDeviceSettingsLeaveAsync = ConfirmDeviceSettingsLeaveAsync;
        InitializeComponent();
        UpdateBreadcrumb();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        TryNavigateWithinSettings(e.Parameter);
    }

    internal bool TryNavigateWithinSettings(object? parameter)
    {
        var targetSection = parameter is string value && value == SettingsViewModel.DeviceChannelsNavigationParameter
            ? SettingsSection.Device
            : SettingsSection.Overview;
        if (_section == targetSection)
            return false;

        if (targetSection == SettingsSection.Device)
            ShowDetail(SettingsSection.Device);
        else
            TryReturnToOverview();
        return true;
    }

    protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        if (_allowNavigationAfterSave)
            return;

        if (e.NavigationMode == NavigationMode.Back && TryReturnToOverview())
        {
            e.Cancel = true;
            return;
        }

        if (!ViewModel.HasDeviceSettingsChanges)
            return;

        e.Cancel = true;
        if (_isLeaveDialogOpen)
            return;

        var navigationMode = e.NavigationMode;
        var destinationType = e.SourcePageType;
        var parameter = e.Parameter;
        var clearNavigation = Frame.Tag is true;
        try
        {
            if (!await ConfirmDeviceSettingsLeaveAsync())
                return;

            _allowNavigationAfterSave = true;
            var navigation = App.GetService<INavigationService>();
            if (navigationMode == NavigationMode.Back)
                navigation.GoBack();
            else if (navigationMode == NavigationMode.Forward)
                Frame.GoForward();
            else
            {
                var pages = App.GetService<IPageService>();
                var route = Enum.GetValues<AppRoute>().First(route => pages.GetPageType(route) == destinationType);
                navigation.NavigateTo(route, parameter, clearNavigation);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to leave device settings: {ex}");
        }
        finally
        {
            _allowNavigationAfterSave = false;
        }
    }

    private async Task<bool> ConfirmDeviceSettingsLeaveAsync()
    {
        if (!ViewModel.HasDeviceSettingsChanges)
            return true;
        if (_isLeaveDialogOpen)
            return false;

        _isLeaveDialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                RequestedTheme = ActualTheme,
                Title = "Settings_DeviceLeaveTitle".GetLocalized(),
                Content = "Settings_DeviceLeaveMessage".GetLocalized(),
                PrimaryButtonText = "Settings_DeviceSaveAndLeave".GetLocalized(),
                SecondaryButtonText = "Settings_DeviceDiscardAndLeave".GetLocalized(),
                CloseButtonText = "Settings_DeviceKeepEditing".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.None)
                return false;

            if (ViewModel.SaveDeviceSettingsCommand.ExecutionTask is { } saving)
                await saving;
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.SaveDeviceSettingsCommand.ExecuteAsync(null);
                if (ViewModel.HasDeviceSettingsChanges)
                    ShowDetail(SettingsSection.Device);
            }
            else
            {
                ViewModel.RevertDeviceSettingsCommand.Execute(null);
            }
            return !ViewModel.HasDeviceSettingsChanges;
        }
        finally
        {
            _isLeaveDialogOpen = false;
        }
    }

    private void SettingRow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid row || row.Children.Count < 2
            || row.Children[1] is not FrameworkElement control)
            return;

        var stacked = e.NewSize.Width < 560;
        Grid.SetColumn(control, stacked ? 0 : 1);
        Grid.SetRow(control, stacked ? 1 : 0);
        control.HorizontalAlignment = stacked ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        row.ColumnSpacing = stacked ? 0 : 24;
        row.RowSpacing = stacked ? 12 : 0;
    }

    private void ThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedValue: ElementTheme theme }
            && ViewModel.ElementTheme != theme)
            ViewModel.SwitchThemeCommand.Execute(theme);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_section != SettingsSection.Overview)
            RestoreSectionViewport();
    }

    private void ScanDefaultsButton_Click(object sender, RoutedEventArgs e)
        => ShowDetail(SettingsSection.ScanDefaults);

    private void DeviceSettingsButton_Click(object sender, RoutedEventArgs e)
        => ShowDetail(SettingsSection.Device);

    private void AdvancedSettingsButton_Click(object sender, RoutedEventArgs e)
        => ShowDetail(SettingsSection.Advanced);

    private void SettingsBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index == 0)
            TryReturnToOverview();
    }

    private void ShowDetail(SettingsSection section)
    {
        if (_section == SettingsSection.Overview)
            _overviewVerticalOffset = SettingsScrollViewer.VerticalOffset;

        _overviewFocusTarget = section switch
        {
            SettingsSection.ScanDefaults => ScanDefaultsButton,
            SettingsSection.Device => DeviceSettingsButton,
            SettingsSection.Advanced => AdvancedSettingsButton,
            _ => null
        };
        SetSection(section);
    }

    internal bool TryReturnToOverview()
    {
        if (_section == SettingsSection.Overview)
            return false;

        SetSection(SettingsSection.Overview);
        return true;
    }

    private void SetSection(SettingsSection section)
    {
        _section = section;
        UpdateBreadcrumb();
        SettingsOverviewPanel.Visibility = section == SettingsSection.Overview ? Visibility.Visible : Visibility.Collapsed;
        ScanDefaultsPanel.Visibility = section == SettingsSection.ScanDefaults ? Visibility.Visible : Visibility.Collapsed;
        DeviceSettingsPanel.Visibility = section == SettingsSection.Device ? Visibility.Visible : Visibility.Collapsed;
        DeviceSaveBar.Visibility = DeviceSettingsPanel.Visibility;
        AdvancedSettingsPanel.Visibility = section == SettingsSection.Advanced ? Visibility.Visible : Visibility.Collapsed;

        if (IsLoaded)
            RestoreSectionViewport();
    }

    private void UpdateBreadcrumb()
    {
        var rootTitle = "Settings_BreadcrumbRoot".GetLocalized();
        var detailTitle = _section switch
        {
            SettingsSection.ScanDefaults => ScanDefaultsEntryTitle.Text,
            SettingsSection.Device => DeviceSettingsEntryTitle.Text,
            SettingsSection.Advanced => AdvancedSettingsEntryTitle.Text,
            _ => null
        };
        SettingsBreadcrumb.ItemsSource = detailTitle is null
            ? new[] { rootTitle }
            : new[] { rootTitle, detailTitle };
    }

    private void RestoreSectionViewport()
    {
        var expectedSection = _section;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!IsLoaded || _section != expectedSection)
                return;

            // Recalculate the scroll range after switching the visible settings view.
            UpdateLayout();
            var isOverview = _section == SettingsSection.Overview;
            if (isOverview)
                _overviewFocusTarget?.Focus(FocusState.Programmatic);
            else
                (FocusManager.FindFirstFocusableElement(SettingsBreadcrumb) as Control)?.Focus(FocusState.Programmatic);
            SettingsScrollViewer.ChangeView(null, isOverview ? _overviewVerticalOffset : 0, null, true);
        });
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        ViewModel.ConfirmDeviceSettingsLeaveAsync = null;
        try
        {
            await ViewModel.TeardownSettingsPersistenceAsync();
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Settings persistence unload flush timed out.");
        }
    }
}
