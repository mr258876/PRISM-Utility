using Microsoft.UI.Xaml;

namespace PRISM_Utility.Contracts.Services;

public interface IThemeSelectorService
{
    ElementTheme Theme
    {
        get;
    }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SetThemeAsync(ElementTheme theme, CancellationToken cancellationToken = default);

    Task SetRequestedThemeAsync(CancellationToken cancellationToken = default);
}
