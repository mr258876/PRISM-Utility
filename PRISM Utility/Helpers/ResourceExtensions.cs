using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace PRISM_Utility.Helpers;

public static class ResourceExtensions
{
    public static string GetLocalized(this string resourceKey)
    {
        return new ResourceLoader().GetString(resourceKey);
    }

    public static string GetLocalizedFormat(this string resourceKey, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, resourceKey.GetLocalized(), args);
}
