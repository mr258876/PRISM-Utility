using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Contracts.Navigation;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Views;

namespace PRISM_Utility
{
    public sealed class TestWindow
    {
        public UIElement? Content { get; set; }
        public string Title { get; set; } = string.Empty;
        public void Activate() { }
    }

    public static class App
    {
        public static TestWindow MainWindow { get; } = new();

        public static T GetService<T>() where T : class
        {
            if (typeof(T) == typeof(INavigationService))
                return (T)(object)new TestNavigationService();

            if (typeof(T) == typeof(ShellPage))
                return (T)(object)new ShellPage();

            throw new InvalidOperationException($"No test service registered for {typeof(T).FullName}.");
        }
    }

    public sealed class TestNavigationService : INavigationService
    {
        public bool NavigateTo(AppRoute route, object? parameter = null, bool clearNavigation = false) => true;
        public bool GoBack() => false;
    }
}

namespace PRISM_Utility.Views
{
    public sealed class ShellPage : Page;
}

namespace PRISM_Utility.Helpers
{
    public static class TitleBarHelper
    {
        public static void UpdateTitleBar(ElementTheme theme) { }
    }
}

namespace PRISM_Utility.Contracts.Services
{
    using PRISM_Utility.Contracts.Navigation;

    public interface INavigationService
    {
        bool NavigateTo(AppRoute route, object? parameter = null, bool clearNavigation = false);
        bool GoBack();
    }
}
