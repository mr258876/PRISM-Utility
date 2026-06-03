using System.Diagnostics;

namespace PRISM_Utility.Helpers;

internal static class NavigationTimingLogger
{
    public static void Write(string message)
    {
        Debug.WriteLine($"[NavigationTiming] {DateTimeOffset.Now:HH:mm:ss.fff} {message}");
    }
}
