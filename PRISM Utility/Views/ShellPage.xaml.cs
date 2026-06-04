using System;
using System.ComponentModel;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;

using Windows.System;

namespace PRISM_Utility.Views;

// TODO: Update NavigationViewItem titles and icons in ShellPage.xaml.
public sealed partial class ShellPage : Page
{
    private const string ScannerStatusRunningGlyph = "\uE768";
    private static readonly string[] ScannerStatusRunningBadgeGlyphs =
    {
        "\uE846",
        "\uE847",
        "\uE84E",
        "\uE84F"
    };

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _scannerStatusBadgeTimer;
    private int _scannerStatusBadgeFrameIndex;

    public ShellViewModel ViewModel
    {
        get;
    }

    public ShellPage(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        RefreshScannerStatusBadgeIcon();

        // TODO: Set the title bar icon by updating /Assets/WindowIcon.ico.
        // A custom title bar is required for full window theme and Mica support.
        // https://docs.microsoft.com/windows/apps/develop/title-bar?tabs=winui3#full-customization
        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated;
        Unloaded += OnUnloaded;
        AppTitleBarText.Text = "AppDisplayName".GetLocalized();
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TitleBarHelper.UpdateTitleBar(RequestedTheme);

        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = AppTitleBarText as UIElement;
    }

    private void NavigationViewControl_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        AppTitleBar.Margin = new Thickness()
        {
            Left = sender.CompactPaneLength * (sender.DisplayMode == NavigationViewDisplayMode.Minimal ? 2 : 1),
            Top = AppTitleBar.Margin.Top,
            Right = AppTitleBar.Margin.Right,
            Bottom = AppTitleBar.Margin.Bottom
        };
    }

    private void ScannerConnectionItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ShowScannerConnectionFlyout();
        e.Handled = true;
    }

    private void ScannerConnectionItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter or VirtualKey.Space)
        {
            ShowScannerConnectionFlyout();
            e.Handled = true;
        }
    }

    private void ShowScannerConnectionFlyout()
        => FlyoutBase.ShowAttachedFlyout(ScannerConnectionItem);

    private void DeviceConfigurationButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.NavigateToDeviceConfigurationCommand.Execute(null);
        ScannerConnectionFlyout.Hide();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.ScannerStatusBadgeGlyph))
        {
            RefreshScannerStatusBadgeIcon();
        }
    }

    private void RefreshScannerStatusBadgeIcon()
    {
        if (ViewModel.ScannerStatusBadgeGlyph == ScannerStatusRunningGlyph)
        {
            StartScannerStatusBadgeAnimation();
            return;
        }

        StopScannerStatusBadgeAnimation();
        SetScannerStatusBadgeGlyph(ViewModel.ScannerStatusBadgeGlyph);
    }

    private void StartScannerStatusBadgeAnimation()
    {
        _scannerStatusBadgeTimer ??= DispatcherQueue.CreateTimer();
        _scannerStatusBadgeTimer.Interval = TimeSpan.FromMilliseconds(300);
        _scannerStatusBadgeTimer.Tick -= OnScannerStatusBadgeTimerTick;
        _scannerStatusBadgeTimer.Tick += OnScannerStatusBadgeTimerTick;

        if (_scannerStatusBadgeTimer.IsRunning)
        {
            return;
        }

        _scannerStatusBadgeFrameIndex = 0;
        SetScannerStatusBadgeGlyph(ScannerStatusRunningBadgeGlyphs[_scannerStatusBadgeFrameIndex]);
        _scannerStatusBadgeTimer.Start();
    }

    private void StopScannerStatusBadgeAnimation()
    {
        if (_scannerStatusBadgeTimer is null)
        {
            return;
        }

        _scannerStatusBadgeTimer.Stop();
        _scannerStatusBadgeTimer.Tick -= OnScannerStatusBadgeTimerTick;
        _scannerStatusBadgeFrameIndex = 0;
    }

    private void OnScannerStatusBadgeTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        _scannerStatusBadgeFrameIndex = (_scannerStatusBadgeFrameIndex + 1) % ScannerStatusRunningBadgeGlyphs.Length;
        SetScannerStatusBadgeGlyph(ScannerStatusRunningBadgeGlyphs[_scannerStatusBadgeFrameIndex]);
    }

    private void SetScannerStatusBadgeGlyph(string glyph)
    {
        ScannerStatusBadge.IconSource = new FontIconSource
        {
            FontFamily = GetSymbolThemeFontFamily(),
            Glyph = glyph
        };
    }

    private static FontFamily GetSymbolThemeFontFamily()
        => Application.Current?.Resources.TryGetValue("SymbolThemeFontFamily", out var resource) == true && resource is FontFamily fontFamily
            ? fontFamily
            : new FontFamily("Segoe Fluent Icons");

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopScannerStatusBadgeAnimation();
        App.MainWindow.Activated -= MainWindow_Activated;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.NavigationViewService.UnregisterEvents();
        ViewModel.UnregisterNavigation();
        Unloaded -= OnUnloaded;
    }

    private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
    {
        var keyboardAccelerator = new KeyboardAccelerator() { Key = key };

        if (modifiers.HasValue)
        {
            keyboardAccelerator.Modifiers = modifiers.Value;
        }

        keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;

        return keyboardAccelerator;
    }

    private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();

        var result = navigationService.GoBack();

        args.Handled = result;
    }
}
