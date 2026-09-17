using System.Diagnostics;
using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace PRISM_Utility.Helpers;

public static class ResourceExtensions
{
    private const string ResourceFallbackSwitch = "PRISM.Utility.UseResourceFallbacks";
    private static readonly object ResourceLoaderGate = new();
    private static ResourceLoader? ResourceLoader;

    public static void ResetResourceLoader()
    {
        lock (ResourceLoaderGate)
        {
            ResourceLoader = null;
        }
    }

    private static bool UseResourceFallbacks()
        => AppContext.TryGetSwitch(ResourceFallbackSwitch, out var enabled) && enabled;

    private static ResourceLoader GetResourceLoader()
        => ResourceLoader ??= new ResourceLoader();

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

        if (UseResourceFallbacks())
            return fallback;

        try
        {
            string localized;
            lock (ResourceLoaderGate)
            {
                var loader = GetResourceLoader();
                localized = loader.GetString(resourceKey);
                if (string.IsNullOrEmpty(localized) || string.Equals(localized, resourceKey, StringComparison.Ordinal))
                    localized = loader.GetString(ToResourceLoaderName(resourceKey));
            }
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

        if (UseResourceFallbacks())
            return GetMissingResourceFallback(resourceKey);

        try
        {
            string localized;
            lock (ResourceLoaderGate)
            {
                var loader = GetResourceLoader();
                localized = loader.GetString(resourceKey);
                if (string.IsNullOrEmpty(localized) || string.Equals(localized, resourceKey, StringComparison.Ordinal))
                    localized = loader.GetString(ToResourceLoaderName(resourceKey));
            }
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

    private static string ToResourceLoaderName(string resourceKey)
    {
        var separator = resourceKey.LastIndexOf('.');
        return separator > 0 && separator < resourceKey.Length - 1
            ? resourceKey[..separator] + "/" + resourceKey[(separator + 1)..]
            : resourceKey;
    }
}
