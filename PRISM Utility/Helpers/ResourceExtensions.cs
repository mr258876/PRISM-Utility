using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace PRISM_Utility.Helpers;

public static class ResourceExtensions
{
    private static readonly ResourceLoader _resourceLoader = new();

    public static string GetLocalized(this string resourceKey)
    {
        return _resourceLoader.GetString(resourceKey);
    }

    public static string GetLocalizedFormat(this string resourceKey, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, resourceKey.GetLocalized(), args);
}
