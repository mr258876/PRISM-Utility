using Microsoft.UI.Xaml;

using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.Services;

public class ThemeSelectorService : IThemeSelectorService
{
    private const string SettingsKey = "AppBackgroundRequestedTheme";
    private static readonly TimeSpan ThemeUiCommitTimeout = TimeSpan.FromSeconds(2);

    public ElementTheme Theme { get; set; } = ElementTheme.Default;

    private readonly ILocalSettingsService _localSettingsService;
    private readonly IUiDispatcher _uiDispatcher;

    public ThemeSelectorService(ILocalSettingsService localSettingsService, IUiDispatcher uiDispatcher)
    {
        _localSettingsService = localSettingsService;
        _uiDispatcher = uiDispatcher;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Theme = await LoadThemeFromSettingsAsync();
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task SetThemeAsync(ElementTheme theme, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var previousTheme = Theme;
        if (previousTheme == theme)
            return;

        await SaveThemeInSettingsAsync(theme);
        Theme = theme;

        try
        {
            using var uiCommitTimeout = new CancellationTokenSource(ThemeUiCommitTimeout);
            await SetRequestedThemeAsync(uiCommitTimeout.Token);
        }
        catch
        {
            await RollBackThemeAsync(previousTheme);
            throw;
        }
    }

    public Task SetRequestedThemeAsync(CancellationToken cancellationToken = default)
        => RunOnUiAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (App.MainWindow.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = Theme;

                TitleBarHelper.UpdateTitleBar(Theme);
            }
        }, cancellationToken);

    private async Task<ElementTheme> LoadThemeFromSettingsAsync()
    {
        var themeName = await _localSettingsService.ReadSettingAsync<string>(SettingsKey);

        if (Enum.TryParse(themeName, out ElementTheme cacheTheme))
        {
            return cacheTheme;
        }

        return ElementTheme.Default;
    }

    private async Task SaveThemeInSettingsAsync(ElementTheme theme)
    {
        await _localSettingsService.SaveSettingAsync(SettingsKey, theme.ToString());
    }

    private async Task RollBackThemeAsync(ElementTheme previousTheme)
    {
        using var rollbackTimeout = new CancellationTokenSource(ThemeUiCommitTimeout);
        await SaveThemeInSettingsAsync(previousTheme);
        Theme = previousTheme;
        await SetRequestedThemeAsync(rollbackTimeout.Token);
    }

    private Task RunOnUiAsync(Action action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_uiDispatcher.TryEnqueue(() =>
        {
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
            throw new InvalidOperationException("Unable to dispatch theme update to the UI thread.");
        }

        return completion.Task.WaitAsync(cancellationToken);
    }
}
