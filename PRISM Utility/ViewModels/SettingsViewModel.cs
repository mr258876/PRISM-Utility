using System.Reflection;
using System.Globalization;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Xaml;

using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.Models;

using Windows.ApplicationModel;

namespace PRISM_Utility.ViewModels;

public partial class SettingsViewModel : ObservableRecipient
{
    private const string ScopeTheme = "theme";
    private const string ScopeLanguage = "language";
    private const string ScopeDebug = "debug";
    private const string ScopeTransfer = "transfer";
    private const string ScopeColor = "color";
    private static readonly TimeSpan SettingsTeardownFlushTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan UiDispatchTimeout = TimeSpan.FromSeconds(2);

    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ILanguageSelectorService _languageSelectorService;
    private readonly IDebugOutputSettingsService _debugOutputSettingsService;
    private readonly IDebugOutputMirrorService _debugOutputMirrorService;
    private readonly IScanTransferSettingsService _scanTransferSettingsService;
    private readonly IScanColorManagementSettingsService _colorManagementSettingsService;
    private readonly SettingsSaveCoordinator _settingsSaveCoordinator;
    private readonly SettingsSaveOwnerHandle _settingsSaveOwner;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly CancellationTokenSource _settingsLifecycleCancellation = new();
    private readonly List<Task> _settingsPersistenceObservers = [];
    private readonly object _settingsPersistenceObserversGate = new();
    private readonly Dictionary<string, string> _settingsPersistenceErrorsByScope = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _settingsPersistenceErrorLabelsByScope = new(StringComparer.Ordinal);
    private readonly object _settingsPersistenceTeardownGate = new();
    private Task? _settingsPersistenceTeardownTask;
    private int _settingsPersistenceDisposed;
    private int _settingsSaveOwnerDisposed;
    private bool _isLoadingLanguageSettings;
    private bool _isLoadingDebugOutputSettings;
    private bool _isLoadingScanTransferMode;
    private bool _isLoadingColorManagementSettings;

    [ObservableProperty]
    public partial ElementTheme ElementTheme { get; set; }

    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VersionDescription { get; set; }

    [ObservableProperty]
    public partial string BuildDescription { get; set; }

    [ObservableProperty]
    public partial Visibility BuildVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial bool IsDebugConsoleMirrorEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsDebugFileLogEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsMultiBufferedBulkInEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsRawIoEnabled { get; set; }

    [ObservableProperty]
    public partial string MultiBufferedRequestBytes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MultiBufferedOutstandingReads { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MultiBufferedTimeoutMs { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsColorManagementEnabled { get; set; }

    [ObservableProperty]
    public partial string RedWavelengthNm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GreenWavelengthNm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BlueWavelengthNm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputGamma { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManualWhitePointColorTemperatureEnabled))]
    public partial string SelectedTargetWhitePointMode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ManualWhitePointColorTemperatureK { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSettingsPersistenceError))]
    public partial string SettingsPersistenceErrorMessage { get; set; } = string.Empty;

    public bool HasSettingsPersistenceError => !string.IsNullOrWhiteSpace(SettingsPersistenceErrorMessage);

    public bool IsManualWhitePointColorTemperatureEnabled =>
        string.Equals(
            SelectedTargetWhitePointMode,
            nameof(ScanTargetWhitePointMode.ManualColorTemperature),
            StringComparison.Ordinal);

    public IReadOnlyList<AppLanguageOption> LanguageOptions { get; } =
    [
        new("system", "Settings_Language_System".GetLocalized()),
        new("en-US", "Settings_Language_English".GetLocalized()),
        new("zh-CN", "Settings_Language_SimplifiedChinese".GetLocalized())
    ];

    public ICommand SwitchThemeCommand
    {
        get;
    }

    public IAsyncRelayCommand RestoreScanTransferDefaultsCommand { get; }

    public IAsyncRelayCommand RestoreColorManagementDefaultsCommand { get; }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, ILanguageSelectorService languageSelectorService, IDebugOutputSettingsService debugOutputSettingsService, IDebugOutputMirrorService debugOutputMirrorService, IScanTransferSettingsService scanTransferSettingsService, IScanColorManagementSettingsService colorManagementSettingsService, SettingsSaveCoordinator settingsSaveCoordinator, IUiDispatcher uiDispatcher)
    {
        _themeSelectorService = themeSelectorService;
        _languageSelectorService = languageSelectorService;
        _debugOutputSettingsService = debugOutputSettingsService;
        _debugOutputMirrorService = debugOutputMirrorService;
        _scanTransferSettingsService = scanTransferSettingsService;
        _colorManagementSettingsService = colorManagementSettingsService;
        _settingsSaveCoordinator = settingsSaveCoordinator;
        _settingsSaveOwner = _settingsSaveCoordinator.CreateOwner();
        _uiDispatcher = uiDispatcher;
        ElementTheme = _themeSelectorService.Theme;
        var versionInfo = GetVersionInfo();
        VersionDescription = versionInfo.VersionDescription;
        BuildDescription = versionInfo.BuildDescription;

        SwitchThemeCommand = new AsyncRelayCommand<ElementTheme>(SwitchThemeAsync);

        RestoreScanTransferDefaultsCommand = new AsyncRelayCommand(RestoreScanTransferDefaultsAsync);
        RestoreColorManagementDefaultsCommand = new AsyncRelayCommand(RestoreColorManagementDefaultsAsync);

        QueueSettingsOperation(ScopeLanguage, "Settings_PersistenceScopeLanguage", LoadLanguageSettingsAsync);
        QueueSettingsOperation(ScopeDebug, "Settings_PersistenceScopeDebugOutput", LoadDebugOutputSettingsAsync);
        QueueSettingsOperation(ScopeTransfer, "Settings_PersistenceScopeTransfer", LoadScanTransferSettingsAsync);
        QueueSettingsOperation(ScopeColor, "Settings_PersistenceScopeColor", LoadColorManagementSettingsAsync);
    }

    public async Task FlushSettingsPersistenceAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task[] observers;
            lock (_settingsPersistenceObserversGate)
                observers = _settingsPersistenceObservers.ToArray();

            await Task.WhenAll(observers.Append(_settingsSaveCoordinator.WhenIdleAsync(_settingsSaveOwner.OwnerId))).WaitAsync(cancellationToken);

            lock (_settingsPersistenceObserversGate)
            {
                if (_settingsPersistenceObservers.Count == 0)
                    return;
            }
        }
    }

    public void CancelSettingsPersistence()
    {
        _settingsSaveCoordinator.CancelPendingOperations(_settingsSaveOwner.OwnerId);
    }

    public Task TeardownSettingsPersistenceAsync(CancellationToken cancellationToken = default)
    {
        lock (_settingsPersistenceTeardownGate)
        {
            _settingsPersistenceTeardownTask ??= TeardownSettingsPersistenceCoreAsync(cancellationToken);
            return _settingsPersistenceTeardownTask;
        }
    }

    private async Task TeardownSettingsPersistenceCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var flushTimeout = new CancellationTokenSource(SettingsTeardownFlushTimeout);
            using var boundedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, flushTimeout.Token);
            await FlushSettingsPersistenceAsync(boundedCancellation.Token);
        }
        catch (OperationCanceledException ex)
        {
            _debugOutputMirrorService.Mirror("Settings.Persistence", $"teardown flush canceled: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _settingsPersistenceDisposed, 1);
            _settingsLifecycleCancellation.Cancel();
            CancelSettingsPersistence();
            DisposeSettingsSaveOwner();
        }
    }

    private async Task SwitchThemeAsync(ElementTheme theme)
    {
        if (ElementTheme == theme)
            return;

        ElementTheme = theme;
        await QueueSettingsOperationAsync(
            ScopeTheme,
            "Settings_PersistenceScopeTheme",
            cancellationToken => _themeSelectorService.SetThemeAsync(theme, cancellationToken),
            RollBackThemeSettings);
    }

    private void QueueSettingsOperation(string scope, string labelResourceKey, Func<CancellationToken, Task> operation, Action? rollback = null)
    {
        if (IsSettingsPersistenceDisposed())
            return;

        TrackSettingsPersistenceObserver(QueueSettingsOperationAsync(scope, labelResourceKey, operation, rollback));
    }

    private async Task QueueSettingsOperationAsync(string scope, string labelResourceKey, Func<CancellationToken, Task> operation, Action? rollback = null)
    {
        if (IsSettingsPersistenceDisposed())
            return;

        try
        {
            var result = await _settingsSaveCoordinator.EnqueueAsync(_settingsSaveOwner.OwnerId, scope, operation, _settingsLifecycleCancellation.Token);
            MirrorSettingsOperationFailure(result);
            await RunOnUiAsync(() => ApplySettingsOperationResult(result, labelResourceKey, rollback));
        }
        catch (Exception ex)
        {
            _debugOutputMirrorService.Mirror("Settings.Persistence", $"{scope} observer failed: {ex.GetType().Name}: {ex.Message}");
            await RunOnUiAsync(() => SetSettingsPersistenceError(scope, labelResourceKey, ex));
        }
    }

    private void TrackSettingsPersistenceObserver(Task observer)
    {
        lock (_settingsPersistenceObserversGate)
            _settingsPersistenceObservers.Add(observer);

        observer.GetAwaiter().OnCompleted(() => RemoveCompletedSettingsPersistenceObserver(observer));
    }

    private void RemoveCompletedSettingsPersistenceObserver(Task observer)
    {
        if (observer.IsFaulted)
            _ = observer.Exception;

        lock (_settingsPersistenceObserversGate)
            _settingsPersistenceObservers.Remove(observer);
    }

    private void ApplySettingsOperationResult(SettingsSaveOperationResult result, string labelResourceKey, Action? rollback)
    {
        if (!_settingsSaveCoordinator.IsLatest(result))
            return;

        if (result.Status == SettingsSaveOperationStatus.Succeeded)
        {
            ClearSettingsPersistenceError(result.Scope);
            return;
        }

        if (result.Status != SettingsSaveOperationStatus.Failed || result.Error is null)
            return;

        rollback?.Invoke();
        SetSettingsPersistenceError(result.Scope, labelResourceKey, result.Error);
    }

    private void MirrorSettingsOperationFailure(SettingsSaveOperationResult result)
    {
        if (result.Status != SettingsSaveOperationStatus.Failed || result.Error is null || !_settingsSaveCoordinator.IsLatest(result))
            return;

        _debugOutputMirrorService.Mirror(
            "Settings.Persistence",
            $"{result.Scope} save failed: {result.Error.GetType().Name}: {result.Error.Message}");
    }

    private void SetSettingsPersistenceError(string scope, string labelResourceKey, Exception error)
    {
        _settingsPersistenceErrorLabelsByScope[scope] = labelResourceKey.GetLocalized();
        _settingsPersistenceErrorsByScope[scope] = "Settings_PersistenceSaveFailedFormat".GetLocalizedFormatOrFallback(
            "{0} setting could not be saved: {1}",
            _settingsPersistenceErrorLabelsByScope[scope],
            error.Message);
        RefreshSettingsPersistenceErrorMessage();
    }

    private void ClearSettingsPersistenceError(string scope)
    {
        _settingsPersistenceErrorLabelsByScope.Remove(scope);
        _settingsPersistenceErrorsByScope.Remove(scope);
        RefreshSettingsPersistenceErrorMessage();
    }

    private void RefreshSettingsPersistenceErrorMessage()
    {
        SettingsPersistenceErrorMessage = string.Join(Environment.NewLine, _settingsPersistenceErrorsByScope.Values);
    }

    private async Task RunOnUiAsync(Action action, CancellationToken cancellationToken = default)
    {
        if (IsSettingsPersistenceDisposed())
            return;

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var dispatchTimeout = new CancellationTokenSource(UiDispatchTimeout);
        using var dispatchCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _settingsLifecycleCancellation.Token,
            cancellationToken,
            dispatchTimeout.Token);
        var dispatchToken = dispatchCancellation.Token;
        using var cancellationRegistration = dispatchToken.Register(static state => ((TaskCompletionSource)state!).TrySetResult(), completion);

        if (!_uiDispatcher.TryEnqueue(() =>
        {
            if (dispatchToken.IsCancellationRequested || IsSettingsPersistenceDisposed())
            {
                completion.TrySetResult();
                return;
            }

            try
            {
                action();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }))
        {
            throw new InvalidOperationException("Unable to dispatch settings state update to the UI thread.");
        }

        await completion.Task.ConfigureAwait(false);
    }

    private bool IsSettingsPersistenceDisposed()
        => Volatile.Read(ref _settingsPersistenceDisposed) != 0;

    private void DisposeSettingsSaveOwner()
    {
        if (Interlocked.Exchange(ref _settingsSaveOwnerDisposed, 1) == 0)
            _settingsSaveOwner.Dispose();
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (_isLoadingLanguageSettings || string.IsNullOrWhiteSpace(value))
            return;

        QueueSettingsOperation(
            ScopeLanguage,
            "Settings_PersistenceScopeLanguage",
            cancellationToken => _languageSelectorService.SetLanguageAsync(value, cancellationToken),
            RollBackLanguageSettings);
    }

    partial void OnIsDebugConsoleMirrorEnabledChanged(bool value)
    {
        if (_isLoadingDebugOutputSettings)
            return;

        QueueSettingsOperation(
            ScopeDebug,
            "Settings_PersistenceScopeDebugOutput",
            cancellationToken => _debugOutputSettingsService.SetDebugConsoleEnabledAsync(value, cancellationToken),
            RollBackDebugOutputSettings);
    }

    partial void OnIsDebugFileLogEnabledChanged(bool value)
    {
        if (_isLoadingDebugOutputSettings)
            return;

        QueueSettingsOperation(
            ScopeDebug,
            "Settings_PersistenceScopeDebugOutput",
            cancellationToken => _debugOutputSettingsService.SetFileLogEnabledAsync(value, cancellationToken),
            RollBackDebugOutputSettings);
    }

    partial void OnIsMultiBufferedBulkInEnabledChanged(bool value)
    {
        if (_isLoadingScanTransferMode)
            return;

        QueueSettingsOperation(ScopeTransfer, "Settings_PersistenceScopeTransfer", cancellationToken => SaveTransferSettingsAsync(settings => settings with
        {
            ReadMode = value ? ScanBulkInReadMode.MultiBuffered : ScanBulkInReadMode.SingleRequest
        }, cancellationToken), RollBackTransferSettings);
    }

    partial void OnIsRawIoEnabledChanged(bool value)
    {
        if (_isLoadingScanTransferMode)
            return;

        QueueSettingsOperation(ScopeTransfer, "Settings_PersistenceScopeTransfer", cancellationToken => SaveTransferSettingsAsync(settings => settings with { RawIoEnabled = value }, cancellationToken), RollBackTransferSettings);
    }

    partial void OnMultiBufferedRequestBytesChanged(string value)
    {
        if (_isLoadingScanTransferMode || !TryParsePositiveInt(value, out var parsed))
            return;

        QueueSettingsOperation(ScopeTransfer, "Settings_PersistenceScopeTransfer", cancellationToken => SaveTransferSettingsAsync(settings => settings with { RequestBytes = parsed }, cancellationToken), RollBackTransferSettings);
    }

    partial void OnMultiBufferedOutstandingReadsChanged(string value)
    {
        if (_isLoadingScanTransferMode || !TryParsePositiveInt(value, out var parsed))
            return;

        QueueSettingsOperation(ScopeTransfer, "Settings_PersistenceScopeTransfer", cancellationToken => SaveTransferSettingsAsync(settings => settings with { OutstandingReads = parsed }, cancellationToken), RollBackTransferSettings);
    }

    partial void OnMultiBufferedTimeoutMsChanged(string value)
    {
        if (_isLoadingScanTransferMode || !TryParsePositiveInt(value, out var parsed))
            return;

        QueueSettingsOperation(ScopeTransfer, "Settings_PersistenceScopeTransfer", cancellationToken => SaveTransferSettingsAsync(settings => settings with { TimeoutMs = parsed }, cancellationToken), RollBackTransferSettings);
    }

    partial void OnIsColorManagementEnabledChanged(bool value)
    {
        if (_isLoadingColorManagementSettings)
            return;

        QueueSettingsOperation(ScopeColor, "Settings_PersistenceScopeColor", cancellationToken => SaveColorManagementSettingsAsync(settings => settings with { IsEnabled = value }, cancellationToken), RollBackColorManagementSettings);
    }

    private async Task LoadDebugOutputSettingsAsync(CancellationToken cancellationToken)
    {
        await _debugOutputSettingsService.InitializeAsync(cancellationToken);
        await RunOnUiAsync(() =>
        {
            _isLoadingDebugOutputSettings = true;
            try
            {
                ApplyDebugOutputSettings();
            }
            finally
            {
                _isLoadingDebugOutputSettings = false;
            }
        }, cancellationToken);
    }

    private void ApplyDebugOutputSettings()
    {
        IsDebugConsoleMirrorEnabled = _debugOutputSettingsService.IsDebugConsoleEnabled;
        IsDebugFileLogEnabled = _debugOutputSettingsService.IsFileLogEnabled;
    }

    private void RollBackThemeSettings()
    {
        ElementTheme = _themeSelectorService.Theme;
    }

    private void RollBackLanguageSettings()
    {
        _isLoadingLanguageSettings = true;
        try
        {
            SelectedLanguage = _languageSelectorService.CurrentLanguage;
        }
        finally
        {
            _isLoadingLanguageSettings = false;
        }
    }

    private void RollBackDebugOutputSettings()
    {
        _isLoadingDebugOutputSettings = true;
        try
        {
            ApplyDebugOutputSettings();
        }
        finally
        {
            _isLoadingDebugOutputSettings = false;
        }
    }

    private async Task LoadLanguageSettingsAsync(CancellationToken cancellationToken)
    {
        await _languageSelectorService.InitializeAsync(cancellationToken);
        await RunOnUiAsync(() =>
        {
            _isLoadingLanguageSettings = true;
            try
            {
                SelectedLanguage = _languageSelectorService.CurrentLanguage;
            }
            finally
            {
                _isLoadingLanguageSettings = false;
            }
        }, cancellationToken);
    }

    partial void OnRedWavelengthNmChanged(string value)
    {
        if (_isLoadingColorManagementSettings || !TryParseColorDouble(value, out var parsed))
            return;

        QueueSettingsOperation(ScopeColor, "Settings_PersistenceScopeColor", cancellationToken => SaveColorManagementSettingsAsync(settings => settings with { RedWavelengthNm = parsed }, cancellationToken), RollBackColorManagementSettings);
    }

    partial void OnGreenWavelengthNmChanged(string value)
    {
        if (_isLoadingColorManagementSettings || !TryParseColorDouble(value, out var parsed))
            return;

        QueueSettingsOperation(ScopeColor, "Settings_PersistenceScopeColor", cancellationToken => SaveColorManagementSettingsAsync(settings => settings with { GreenWavelengthNm = parsed }, cancellationToken), RollBackColorManagementSettings);
    }

    partial void OnBlueWavelengthNmChanged(string value)
    {
        if (_isLoadingColorManagementSettings || !TryParseColorDouble(value, out var parsed))
            return;

        QueueSettingsOperation(ScopeColor, "Settings_PersistenceScopeColor", cancellationToken => SaveColorManagementSettingsAsync(settings => settings with { BlueWavelengthNm = parsed }, cancellationToken), RollBackColorManagementSettings);
    }

    partial void OnOutputGammaChanged(string value)
    {
        if (_isLoadingColorManagementSettings || !TryParseColorDouble(value, out var parsed))
            return;

        QueueSettingsOperation(ScopeColor, "Settings_PersistenceScopeColor", cancellationToken => SaveColorManagementSettingsAsync(settings => settings with { OutputGamma = parsed }, cancellationToken), RollBackColorManagementSettings);
    }

    partial void OnSelectedTargetWhitePointModeChanged(string value)
    {
        if (_isLoadingColorManagementSettings)
            return;

        if (TryParseTargetWhitePointMode(value, out var mode))
            QueueSettingsOperation(ScopeColor, "Settings_PersistenceScopeColor", cancellationToken => SaveColorManagementSettingsAsync(settings => settings with { TargetWhitePointMode = mode }, cancellationToken), RollBackColorManagementSettings);
    }

    partial void OnManualWhitePointColorTemperatureKChanged(string value)
    {
        if (_isLoadingColorManagementSettings || !TryParseColorDouble(value, out var parsed))
            return;

        QueueSettingsOperation(ScopeColor, "Settings_PersistenceScopeColor", cancellationToken => SaveColorManagementSettingsAsync(settings => settings with { ManualWhitePointColorTemperatureK = parsed }, cancellationToken), RollBackColorManagementSettings);
    }

    private async Task LoadScanTransferSettingsAsync(CancellationToken cancellationToken)
    {
        await _scanTransferSettingsService.InitializeAsync(cancellationToken);
        await RunOnUiAsync(() =>
        {
            _isLoadingScanTransferMode = true;
            try
            {
                ApplyTransferSettings(_scanTransferSettingsService.Settings);
            }
            finally
            {
                _isLoadingScanTransferMode = false;
            }
        }, cancellationToken);
    }

    private void ApplyTransferSettings(ScanBulkInTransferOptions settings)
    {
        IsMultiBufferedBulkInEnabled = settings.ReadMode == ScanBulkInReadMode.MultiBuffered;
        IsRawIoEnabled = settings.RawIoEnabled;
        MultiBufferedRequestBytes = settings.RequestBytes.ToString();
        MultiBufferedOutstandingReads = settings.OutstandingReads.ToString();
        MultiBufferedTimeoutMs = settings.TimeoutMs.ToString();
    }

    private void RollBackTransferSettings()
    {
        _isLoadingScanTransferMode = true;
        try
        {
            ApplyTransferSettings(_scanTransferSettingsService.Settings);
        }
        finally
        {
            _isLoadingScanTransferMode = false;
        }
    }

    private async Task SaveTransferSettingsAsync(Func<ScanBulkInTransferOptions, ScanBulkInTransferOptions> mutate, CancellationToken cancellationToken)
    {
        await _scanTransferSettingsService.InitializeAsync(cancellationToken);
        await _scanTransferSettingsService.SetSettingsAsync(mutate(_scanTransferSettingsService.Settings), cancellationToken);
    }

    private Task RestoreScanTransferDefaultsAsync()
    {
        QueueSettingsOperation(
            ScopeTransfer,
            "Settings_PersistenceScopeTransfer",
            RestoreScanTransferDefaultsCoreAsync,
            RollBackTransferSettings);
        return FlushSettingsPersistenceAsync();
    }

    private async Task RestoreScanTransferDefaultsCoreAsync(CancellationToken cancellationToken)
    {
        await _scanTransferSettingsService.InitializeAsync(cancellationToken);
        var defaults = _scanTransferSettingsService.DefaultSettings;
        await RunOnUiAsync(() =>
        {
            _isLoadingScanTransferMode = true;
            try
            {
                ApplyTransferSettings(defaults);
            }
            finally
            {
                _isLoadingScanTransferMode = false;
            }
        }, cancellationToken);
        await _scanTransferSettingsService.SetSettingsAsync(defaults, cancellationToken);
    }

    private static bool TryParsePositiveInt(string value, out int parsed)
        => int.TryParse(value, out parsed) && parsed > 0;

    private async Task LoadColorManagementSettingsAsync(CancellationToken cancellationToken)
    {
        await _colorManagementSettingsService.InitializeAsync(cancellationToken);
        await RunOnUiAsync(() =>
        {
            _isLoadingColorManagementSettings = true;
            try
            {
                ApplyColorManagementSettings(_colorManagementSettingsService.Settings);
            }
            finally
            {
                _isLoadingColorManagementSettings = false;
            }
        }, cancellationToken);
    }

    private void ApplyColorManagementSettings(ScanColorManagementOptions settings)
    {
        IsColorManagementEnabled = settings.IsEnabled;
        RedWavelengthNm = FormatColorDouble(settings.RedWavelengthNm);
        GreenWavelengthNm = FormatColorDouble(settings.GreenWavelengthNm);
        BlueWavelengthNm = FormatColorDouble(settings.BlueWavelengthNm);
        OutputGamma = FormatColorDouble(settings.OutputGamma);
        SelectedTargetWhitePointMode = settings.TargetWhitePointMode.ToString();
        ManualWhitePointColorTemperatureK = FormatColorDouble(settings.ManualWhitePointColorTemperatureK);
    }

    private void RollBackColorManagementSettings()
    {
        _isLoadingColorManagementSettings = true;
        try
        {
            ApplyColorManagementSettings(_colorManagementSettingsService.Settings);
        }
        finally
        {
            _isLoadingColorManagementSettings = false;
        }
    }

    private async Task SaveColorManagementSettingsAsync(Func<ScanColorManagementOptions, ScanColorManagementOptions> mutate, CancellationToken cancellationToken)
    {
        await _colorManagementSettingsService.InitializeAsync(cancellationToken);
        await _colorManagementSettingsService.SetSettingsAsync(mutate(_colorManagementSettingsService.Settings), cancellationToken);
    }

    private Task RestoreColorManagementDefaultsAsync()
    {
        QueueSettingsOperation(
            ScopeColor,
            "Settings_PersistenceScopeColor",
            RestoreColorManagementDefaultsCoreAsync,
            RollBackColorManagementSettings);
        return FlushSettingsPersistenceAsync();
    }

    private async Task RestoreColorManagementDefaultsCoreAsync(CancellationToken cancellationToken)
    {
        await _colorManagementSettingsService.InitializeAsync(cancellationToken);
        var defaults = _colorManagementSettingsService.DefaultSettings;
        await RunOnUiAsync(() =>
        {
            _isLoadingColorManagementSettings = true;
            try
            {
                ApplyColorManagementSettings(defaults);
            }
            finally
            {
                _isLoadingColorManagementSettings = false;
            }
        }, cancellationToken);
        await _colorManagementSettingsService.SetSettingsAsync(defaults, cancellationToken);
    }

    private static bool TryParseColorDouble(string value, out double parsed)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);

    private static string FormatColorDouble(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool TryParseTargetWhitePointMode(string value, out ScanTargetWhitePointMode mode)
        => Enum.TryParse(value, out mode)
            && mode is ScanTargetWhitePointMode.D65 or ScanTargetWhitePointMode.D50 or ScanTargetWhitePointMode.ManualColorTemperature;

    private static (string VersionDescription, string BuildDescription) GetVersionInfo()
    {
        Version version;

        if (AppContext.TryGetSwitch("PRISM.Utility.UseResourceFallbacks", out var useResourceFallbacks) && useResourceFallbacks)
        {
            version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        }
        else if (RuntimeHelper.IsMSIX)
        {
            var packageVersion = Package.Current.Id.Version;

            version = new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var buildLabel = TryGetBuildLabel(assembly);

        var versionText = "Settings_VersionDescriptionFormat".GetLocalizedFormat(
            "AppDisplayName".GetLocalized(),
            $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}");
        if (string.IsNullOrWhiteSpace(buildLabel))
            return (versionText, string.Empty);

        return string.IsNullOrWhiteSpace(buildLabel)
            ? (versionText, string.Empty)
            : (versionText, "Settings_BuildDescriptionFormat".GetLocalizedFormat(buildLabel));
    }

    partial void OnBuildDescriptionChanged(string value)
        => BuildVisibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;

    private static string? TryGetBuildLabel(Assembly assembly)
    {
        var metadataBuildLabel = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "BuildLabel", StringComparison.Ordinal))?
            .Value;

        if (!string.IsNullOrWhiteSpace(metadataBuildLabel))
            return metadataBuildLabel;

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var buildLabelStart = informationalVersion.IndexOf('(');
            var buildLabelEnd = informationalVersion.LastIndexOf(')');
            if (buildLabelStart >= 0 && buildLabelEnd > buildLabelStart)
            {
                var buildText = informationalVersion.Substring(buildLabelStart + 1, buildLabelEnd - buildLabelStart - 1).Trim();
                if (!string.IsNullOrWhiteSpace(buildText))
                    return buildText;
            }
        }

        return null;
    }
}
