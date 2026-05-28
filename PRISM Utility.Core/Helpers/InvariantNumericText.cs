using System.Globalization;

namespace PRISM_Utility.Core.Helpers;

public static class InvariantNumericText
{
    public static bool TryParseDouble(string? text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public static string FormatCompactDouble(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
