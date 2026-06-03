using System.Globalization;

namespace PRISM_Utility.Helpers
{
    public static class ResourceExtensions
    {
        public static string GetLocalizedOrFallback(this string resourceKey, string fallback)
            => fallback;

        public static string GetLocalizedFormatOrFallback(this string resourceKey, string fallbackFormat, params object[] args)
            => string.Format(CultureInfo.CurrentCulture, fallbackFormat, args);

        public static string GetLocalized(this string resourceKey)
            => resourceKey;

        public static string GetLocalizedFormat(this string resourceKey, params object[] args)
            => string.Format(CultureInfo.CurrentCulture, resourceKey, args);
    }
}
