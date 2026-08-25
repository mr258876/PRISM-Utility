using System.Globalization;

using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.Views;

using Microsoft.Windows.Globalization;
using Windows.System.UserProfile;

namespace PRISM_Utility.Services;

public sealed class LanguageSelectorService : ILanguageSelectorService
{
    private const string LanguageSettingsKey = "AppRequestedLanguage";
    private const string SystemLanguageOptionTag = "system";
    private const string DefaultLanguageTag = "en-US";
    private const string SimplifiedChineseLanguageTag = "zh-CN";
    private static readonly TimeSpan LanguageUiCommitTimeout = TimeSpan.FromSeconds(2);

    private static readonly string[] SupportedLanguageTags =
    [
        DefaultLanguageTag,
        SimplifiedChineseLanguageTag
    ];

    private readonly ILocalSettingsService _localSettingsService;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly Func<Task>? _refreshShellAsync;
    private readonly Func<CancellationToken, Task>? _refreshShellWithCancellationAsync;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private bool _isInitialized;

    public string CurrentLanguage { get; private set; } = SystemLanguageOptionTag;

    public LanguageSelectorService(ILocalSettingsService localSettingsService, IThemeSelectorService themeSelectorService, IUiDispatcher uiDispatcher, Func<Task>? refreshShellAsync = null, Func<CancellationToken, Task>? refreshShellWithCancellationAsync = null)
    {
        _localSettingsService = localSettingsService;
        _themeSelectorService = themeSelectorService;
        _uiDispatcher = uiDispatcher;
        _refreshShellAsync = refreshShellAsync;
        _refreshShellWithCancellationAsync = refreshShellWithCancellationAsync;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        await _initializeGate.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            CurrentLanguage = NormalizeLanguageTag(await _localSettingsService.ReadSettingAsync<string>(LanguageSettingsKey));
            _isInitialized = true;
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    public async Task ApplyLanguageAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ApplyLanguage(ResolveEffectiveLanguageTag(CurrentLanguage));
    }

    public async Task SetLanguageAsync(string languageTag, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var normalizedLanguageTag = NormalizeLanguageTag(languageTag);
        if (string.Equals(CurrentLanguage, normalizedLanguageTag, StringComparison.OrdinalIgnoreCase))
            return;

        var previousLanguageTag = CurrentLanguage;
        cancellationToken.ThrowIfCancellationRequested();
        await _localSettingsService.SaveSettingAsync(LanguageSettingsKey, normalizedLanguageTag);
        CurrentLanguage = normalizedLanguageTag;
        try
        {
            ApplyLanguage(ResolveEffectiveLanguageTag(CurrentLanguage));
            using var uiCommitTimeout = new CancellationTokenSource(LanguageUiCommitTimeout);
            await RefreshShellOnUiAsync(uiCommitTimeout.Token);
        }
        catch
        {
            await RollBackLanguageAsync(previousLanguageTag);
            throw;
        }
    }

    private async Task RollBackLanguageAsync(string previousLanguageTag)
    {
        using var rollbackTimeout = new CancellationTokenSource(LanguageUiCommitTimeout);
        await _localSettingsService.SaveSettingAsync(LanguageSettingsKey, previousLanguageTag);
        CurrentLanguage = previousLanguageTag;
        ApplyLanguage(ResolveEffectiveLanguageTag(CurrentLanguage));
        await RefreshShellOnUiAsync(rollbackTimeout.Token);
    }

    private static string NormalizeLanguageTag(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
            return SystemLanguageOptionTag;

        if (string.Equals(SystemLanguageOptionTag, languageTag, StringComparison.OrdinalIgnoreCase))
            return SystemLanguageOptionTag;

        foreach (var supportedLanguageTag in SupportedLanguageTags)
        {
            if (string.Equals(supportedLanguageTag, languageTag, StringComparison.OrdinalIgnoreCase))
                return supportedLanguageTag;
        }

        return DefaultLanguageTag;
    }

    private static string ResolveEffectiveLanguageTag(string languageTag)
        => string.Equals(languageTag, SystemLanguageOptionTag, StringComparison.OrdinalIgnoreCase)
            ? ResolveSystemLanguageTag()
            : languageTag;

    private static string ResolveSystemLanguageTag()
    {
        foreach (var language in GlobalizationPreferences.Languages)
        {
            if (string.IsNullOrWhiteSpace(language))
                continue;

            if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return SimplifiedChineseLanguageTag;

            if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                return DefaultLanguageTag;
        }

        return DefaultLanguageTag;
    }

    private static void ApplyLanguage(string languageTag)
    {
        if (!AppContext.TryGetSwitch("PRISM.Utility.SkipWinGlobalizationApply", out var skipWinGlobalizationApply) || !skipWinGlobalizationApply)
            ApplicationLanguages.PrimaryLanguageOverride = languageTag;

        var culture = CultureInfo.GetCultureInfo(languageTag);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        ResourceExtensions.ResetResourceLoader();
    }

    private Task RefreshShellOnUiAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_refreshShellWithCancellationAsync is not null)
            return _refreshShellWithCancellationAsync(cancellationToken).WaitAsync(cancellationToken);

        if (_refreshShellAsync is not null)
            return _refreshShellAsync().WaitAsync(cancellationToken);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_uiDispatcher.TryEnqueue(async () =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RefreshShellAsync(cancellationToken);
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }))
        {
            throw new InvalidOperationException("Unable to dispatch language shell refresh to the UI thread.");
        }

        return completion.Task.WaitAsync(cancellationToken);
    }

    private async Task RefreshShellAsync(CancellationToken cancellationToken)
    {
        if (App.MainWindow.Content is null)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        App.MainWindow.Content = App.GetService<ShellPage>();
        App.MainWindow.Title = "AppDisplayName".GetLocalized();
        await _themeSelectorService.SetRequestedThemeAsync(cancellationToken);

        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(AppRoute.Settings, clearNavigation: true);
        App.MainWindow.Activate();
    }
}
