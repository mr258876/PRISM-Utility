using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using PRISM_Utility.Activation;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Models;
using PRISM_Utility.Services;
using PRISM_Utility.ViewModels;
using PRISM_Utility.Views;

namespace PRISM_Utility;

// To learn more about WinUI 3, see https://docs.microsoft.com/windows/apps/winui/winui3/.
public partial class App : Application
{
    private static readonly TimeSpan MirrorShutdownTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SettingsShutdownTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ViewModelShutdownTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ScannerShutdownTimeout = TimeSpan.FromSeconds(10);
    private readonly IScannerDeviceSessionManager _scannerDeviceSessionManager;
    private readonly SettingsSaveCoordinator _settingsSaveCoordinator;
    private readonly object _viewModelGate = new();
    private readonly List<WeakReference<ScanViewModel>> _createdScanViewModels = [];
    private ScanDebugViewModel? _createdScanDebugViewModel;
    private int _windowShutdownStarted;
    private bool _allowWindowClose;
#if PRISM_VISUAL_QA
    internal static bool IsShuttingDown => Current is App app && Volatile.Read(ref app._windowShutdownStarted) != 0;
#endif

    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        var app = (App)Current;
        if (app.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        app.TrackResolvedViewModel(service);
        return service;
    }

    private void TrackResolvedViewModel(object service)
    {
        lock (_viewModelGate)
        {
            switch (service)
            {
                case ScanViewModel scanViewModel:
                    _createdScanViewModels.RemoveAll(reference => !reference.TryGetTarget(out _));
                    _createdScanViewModels.Add(new WeakReference<ScanViewModel>(scanViewModel));
                    break;
                case ScanDebugViewModel scanDebugViewModel:
                    _createdScanDebugViewModel = scanDebugViewModel;
                    break;
            }
        }
    }

    private static WindowEx? _mainWindow;

    public static WindowEx MainWindow => _mainWindow ??= new MainWindow();

    public static UIElement? AppTitlebar
    {
        get; set;
    }

    public App()
    {
        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Other Activation Handlers

            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IDebugOutputSettingsService, DebugOutputSettingsService>();
            services.AddSingleton<IDebugOutputMirrorService, DebugOutputMirrorService>();
            services.AddSingleton<IScanTransferSettingsService, ScanTransferSettingsService>();
            services.AddSingleton<IScanDeviceSettingsService, ScanDeviceSettingsService>();
            services.AddSingleton<IScanColorManagementSettingsService, ScanColorManagementSettingsService>();
            services.AddSingleton<IScanDngGeometrySettingsService, ScanDngGeometrySettingsService>();
            services.AddSingleton<SettingsSaveCoordinator>();
            services.AddSingleton<IScanCalibrationProfileStorage, LocalSettingsScanCalibrationProfileStorage>();
            services.AddSingleton<IScanCalibrationProfileRepository, ScanCalibrationProfileRepository>();
            services.AddSingleton<IScanFilmProfileFileGateway, ScanFilmProfileFileGateway>();
            services.AddSingleton<IScanFilmProfileDocumentService, ScanFilmProfileDocumentService>();
            services.AddSingleton<IScanFilmProfileFileCoordinator, ScanFilmProfileFileCoordinator>();
            services.AddSingleton<IScanFilmProfileWorkspace, ScanFilmProfileWorkspace>();
            services.AddSingleton<ILanguageSelectorService, LanguageSelectorService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddSingleton<IUsbService, UsbService>();
            services.AddSingleton<IUsbUsageCoordinator, UsbUsageCoordinator>();
            services.AddSingleton<IScanSessionServiceFactory, ScanSessionServiceFactory>();
            services.AddSingleton<IScannerDeviceSessionManager, ScannerDeviceSessionManager>();
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IUiDispatcher, UiDispatcherService>();
            services.AddTransient<IScanProtocolService, ScanProtocolService>();
            services.AddTransient<IScanImageDecoder, ScanImageDecoder>();
            services.AddTransient<IScanPreviewPresenter, ScanPreviewPresenter>();
            services.AddTransient<IScanCompositeImageProcessor, ScanCompositeImageProcessor>();
            services.AddTransient<IScanChannelAlignmentService, ScanChannelAlignmentService>();
            services.AddTransient<IScanWorkflowService, ScanWorkflowService>();
            services.AddTransient<IScanIlluminationService, ScanIlluminationService>();
            services.AddTransient<IScanChannelImageService, ScanChannelImageService>();
            services.AddTransient<IScanParameterService, ScanParameterService>();
            services.AddTransient<IScanAutoCalibrationService, ScanAutoCalibrationService>();
            services.AddTransient<IScanAutoFocusService, ScanAutoFocusService>();
            services.AddTransient<IScanSessionService, ScanSessionService>();
            services.AddSingleton<IScanWorkflowSessionCoordinator, ScanWorkflowSessionCoordinator>();
            services.AddSingleton<IScanDebugSessionCoordinator, ScanDebugSessionCoordinator>();
            services.AddSingleton<IScannerAccessCoordinator, ScannerAccessCoordinator>();
            services.AddTransient<IDngWriterService, DngWriterService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IAtomicFileWriter, AtomicFileWriter>();

            // Views and ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<DeviceConfigurationViewModel>();
            services.AddTransient<DeviceConfigurationPage>();
            services.AddTransient<LogViewModel>();
            services.AddTransient<LogPage>();
            services.AddTransient<ScanViewModel>();
            services.AddTransient<ScanPage>();
            services.AddSingleton<ScanDebugViewModel>();
            services.AddTransient<ScanDebugPage>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainPage>();
            services.AddTransient<ShellPage>();
            services.AddTransient<ShellViewModel>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();

        _scannerDeviceSessionManager = Host.Services.GetRequiredService<IScannerDeviceSessionManager>();
        _settingsSaveCoordinator = Host.Services.GetRequiredService<SettingsSaveCoordinator>();
        InitializeComponent();

        UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        UnhandledExceptionReporter.Report(
            e.Exception,
            (source, message) => GetService<IDebugOutputMirrorService>().Mirror(source, message),
            message => Debug.WriteLine(message),
            message => Trace.WriteLine(message));
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        await App.GetService<ILanguageSelectorService>().InitializeAsync();
        await App.GetService<ILanguageSelectorService>().ApplyLanguageAsync();
        await App.GetService<IDebugOutputSettingsService>().InitializeAsync();

        MainWindow.AppWindow.Closing -= MainWindow_Closing;
        MainWindow.AppWindow.Closing += MainWindow_Closing;
        await App.GetService<IActivationService>().ActivateAsync(args);
    }

    private async void MainWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowWindowClose)
            return;

        args.Cancel = true;
        if (Interlocked.Exchange(ref _windowShutdownStarted, 1) != 0)
            return;

#if PRISM_VISUAL_QA
        PrismVisualQaCaptureService.BeginShutdown();
#endif
        try
        {
            await Task.Yield();
            await ShutdownAsync();
        }
        catch (Exception ex)
        {
            Debugger.Log(0, "AppShutdown", $"Window shutdown failed: {ex}\n");
#if PRISM_VISUAL_QA
            PrismVisualQaCaptureService.CancelShutdown();
#endif
            Interlocked.Exchange(ref _windowShutdownStarted, 0);
            return;
        }

        _allowWindowClose = true;
        MainWindow.Close();
    }

    private async Task ShutdownAsync()
    {
#if PRISM_VISUAL_QA
        try
        {
            await PrismVisualQaCaptureService.DrainActiveAsync();
        }
        catch (Exception ex)
        {
            Debugger.Log(0, "AppShutdown", $"Visual QA capture drain failed: {ex}\n");
            throw;
        }
#endif

        if (MainWindow.Content is Control content)
            content.IsEnabled = false;

        try
        {
            await CleanupCreatedViewModelsAsync();
        }
        catch (Exception ex)
        {
            Debugger.Log(0, "AppShutdown", $"View model shutdown failed: {ex}\n");
        }

        using var settingsShutdownTimeout = new CancellationTokenSource(SettingsShutdownTimeout);
        try
        {
            _settingsSaveCoordinator.CancelPendingOperations();
            await _settingsSaveCoordinator.WhenIdleAsync().WaitAsync(settingsShutdownTimeout.Token);
        }
        catch (OperationCanceledException) when (settingsShutdownTimeout.IsCancellationRequested)
        {
            Debugger.Log(0, "AppShutdown", "Settings persistence shutdown flush timed out.\n");
        }
        catch (Exception ex)
        {
            Debugger.Log(0, "AppShutdown", $"Settings persistence shutdown failed: {ex}\n");
        }

        using var scannerShutdownTimeout = new CancellationTokenSource(ScannerShutdownTimeout);
        try
        {
            var result = await _scannerDeviceSessionManager.ShutdownAsync(scannerShutdownTimeout.Token).WaitAsync(scannerShutdownTimeout.Token);
            if (!result.Success)
                Debugger.Log(0, "AppShutdown", $"Scanner shutdown cleanup incomplete: {result.Message}\n");
        }
        catch (OperationCanceledException) when (scannerShutdownTimeout.IsCancellationRequested)
        {
            Debugger.Log(0, "AppShutdown", "Scanner shutdown cleanup timed out.\n");
        }
        catch (Exception ex)
        {
            Debugger.Log(0, "AppShutdown", $"Scanner shutdown cleanup failed: {ex}\n");
        }

        try
        {
            _settingsSaveCoordinator.Dispose();
        }
        catch (Exception ex)
        {
            Debugger.Log(0, "AppShutdown", $"Settings persistence disposal failed: {ex}\n");
        }

        using var mirrorShutdownTimeout = new CancellationTokenSource(MirrorShutdownTimeout);
        try
        {
            await GetService<IDebugOutputMirrorService>().ShutdownAsync(mirrorShutdownTimeout.Token);
        }
        catch (OperationCanceledException) when (mirrorShutdownTimeout.IsCancellationRequested)
        {
            Debugger.Log(0, "DebugOutputMirror", "Timed out waiting for debug output mirror shutdown.\n");
        }
        catch (Exception ex)
        {
            Debugger.Log(0, "DebugOutputMirror", $"Debug output mirror shutdown failed: {ex}\n");
        }
    }

    private async Task CleanupCreatedViewModelsAsync()
    {
        WeakReference<ScanViewModel>[] scanViewModels;
        ScanDebugViewModel? scanDebugViewModel;
        lock (_viewModelGate)
        {
            scanViewModels = _createdScanViewModels.ToArray();
            scanDebugViewModel = _createdScanDebugViewModel;
            _createdScanViewModels.Clear();
            _createdScanDebugViewModel = null;
        }

        var cleanupTasks = new List<Task>();
        foreach (var reference in scanViewModels)
        {
            if (reference.TryGetTarget(out var scanViewModel))
                cleanupTasks.Add(CleanupViewModelAsync(scanViewModel.CleanupAsync, nameof(ScanViewModel)));
        }

        if (scanDebugViewModel is not null)
            cleanupTasks.Add(CleanupViewModelAsync(scanDebugViewModel.CleanupAsync, nameof(ScanDebugViewModel)));

        try
        {
            await Task.WhenAll(cleanupTasks).WaitAsync(ViewModelShutdownTimeout);
        }
        catch (TimeoutException)
        {
            Debugger.Log(0, "AppShutdown", "View model shutdown cleanup timed out.\n");
        }
    }

    private static async Task CleanupViewModelAsync(Func<Task> cleanup, string viewModelName)
    {
        try
        {
            await cleanup();
        }
        catch (Exception ex)
        {
            Debugger.Log(0, "AppShutdown", $"{viewModelName} shutdown cleanup failed: {ex}\n");
        }
    }
}
