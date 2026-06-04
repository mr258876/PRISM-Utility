using System.Globalization;

using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;
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

    private static readonly string[] SupportedLanguageTags =
    [
        DefaultLanguageTag,
        SimplifiedChineseLanguageTag
    ];

    private readonly ILocalSettingsService _localSettingsService;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private bool _isInitialized;

    public string CurrentLanguage { get; private set; } = SystemLanguageOptionTag;

    public LanguageSelectorService(ILocalSettingsService localSettingsService, IThemeSelectorService themeSelectorService)
    {
        _localSettingsService = localSettingsService;
        _themeSelectorService = themeSelectorService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await _initializeGate.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            CurrentLanguage = NormalizeLanguageTag(await _localSettingsService.ReadSettingAsync<string>(LanguageSettingsKey));
            _isInitialized = true;
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    public async Task ApplyLanguageAsync()
    {
        await InitializeAsync();
        ApplyLanguage(ResolveEffectiveLanguageTag(CurrentLanguage));
    }

    public async Task SetLanguageAsync(string languageTag)
    {
        await InitializeAsync();

        var normalizedLanguageTag = NormalizeLanguageTag(languageTag);
        if (string.Equals(CurrentLanguage, normalizedLanguageTag, StringComparison.OrdinalIgnoreCase))
            return;

        CurrentLanguage = normalizedLanguageTag;
        ApplyLanguage(ResolveEffectiveLanguageTag(CurrentLanguage));
        await _localSettingsService.SaveSettingAsync(LanguageSettingsKey, CurrentLanguage);
        await RefreshShellAsync();
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
        ApplicationLanguages.PrimaryLanguageOverride = languageTag;

        var culture = CultureInfo.GetCultureInfo(languageTag);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        ResourceExtensions.ResetResourceLoader();
    }

    private async Task RefreshShellAsync()
    {
        if (App.MainWindow.Content is null)
            return;

        App.MainWindow.Content = App.GetService<ShellPage>();
        App.MainWindow.Title = "AppDisplayName".GetLocalized();
        await _themeSelectorService.SetRequestedThemeAsync();

        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(SettingsViewModel).FullName!, clearNavigation: true);
        App.MainWindow.Activate();
    }
}
