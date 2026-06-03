using System.Diagnostics;
using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace PRISM_Utility.Helpers;

public static class ResourceExtensions
{
    private static readonly ResourceLoader ResourceLoader = new();

    private static string GetMissingResourceFallback(string resourceKey)
    {
#if DEBUG
        return $"!!{resourceKey}!!";
#else
        return resourceKey;
#endif
    }

    public static string GetLocalizedOrFallback(this string resourceKey, string fallback)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
            return fallback;

        try
        {
            var localized = ResourceLoader.GetString(resourceKey);
            if (!string.IsNullOrEmpty(localized))
                return localized;

            var message = $"Missing localized resource '{resourceKey}'.";
            Debug.WriteLine(message);
            Trace.WriteLine(message);
            return fallback;
        }
        catch (Exception ex)
        {
            var message = $"Failed to load localized resource '{resourceKey}'.";
            Debug.WriteLine($"{message} {ex}");
            Trace.WriteLine($"{message} {ex}");
            return fallback;
        }
    }

    public static string GetLocalizedFormatOrFallback(this string resourceKey, string fallbackFormat, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, resourceKey.GetLocalizedOrFallback(fallbackFormat), args);

    public static string GetLocalized(this string resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
            throw new ArgumentException("Resource key cannot be null or whitespace.", nameof(resourceKey));

        try
        {
            var localized = ResourceLoader.GetString(resourceKey);
            if (!string.IsNullOrEmpty(localized))
                return localized;

            var message = $"Missing localized resource '{resourceKey}'.";
            Debug.WriteLine(message);
            Trace.WriteLine(message);
            return GetMissingResourceFallback(resourceKey);
        }
        catch (Exception ex)
        {
            var message = $"Failed to load localized resource '{resourceKey}'.";
            Debug.WriteLine($"{message} {ex}");
            Trace.WriteLine($"{message} {ex}");
            return GetMissingResourceFallback(resourceKey);
        }
    }

    public static string GetLocalizedFormat(this string resourceKey, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, resourceKey.GetLocalized(), args);
}
