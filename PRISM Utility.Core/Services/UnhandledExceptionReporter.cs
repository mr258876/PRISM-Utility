namespace PRISM_Utility.Core.Services;

public static class UnhandledExceptionReporter
{
    private const string Source = "WinUI.UnhandledException";

    public static void Report(
        Exception exception,
        Action<string, string> mirror,
        Action<string> debugFallback,
        Action<string> traceFallback)
    {
        var message = Format(exception);

        if (TryInvoke(() => mirror(Source, message)))
            return;

        TryInvoke(() => debugFallback(message));
        TryInvoke(() => traceFallback(message));
    }

    private static string Format(Exception exception)
    {
        try
        {
            return $"Unhandled WinUI exception: {exception}";
        }
        catch (Exception)
        {
            return "Unhandled WinUI exception could not be formatted.";
        }
    }

    private static bool TryInvoke(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
