using System.Globalization;

namespace PRISM_Utility.Core.Helpers;

public static class ScanMotorIntervalText
{
    private const uint NanosecondsPerMicrosecond = 1_000;

    public static bool TryParseMicroseconds(string? text, out uint intervalNs)
    {
        intervalNs = 0;
        var trimmedText = text?.Trim();
        if (string.IsNullOrEmpty(trimmedText)
            || trimmedText[0] == '+'
            || !uint.TryParse(trimmedText, NumberStyles.None, CultureInfo.InvariantCulture, out var intervalUs))
            return false;

        try
        {
            intervalNs = checked(intervalUs * NanosecondsPerMicrosecond);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    public static bool TryFormatMicroseconds(uint intervalNs, out string text)
    {
        text = string.Empty;
        if (intervalNs % NanosecondsPerMicrosecond != 0)
            return false;

        text = (intervalNs / NanosecondsPerMicrosecond).ToString(CultureInfo.InvariantCulture);
        return true;
    }

    public static uint MinimumWholeMicroseconds(uint intervalNs)
        => intervalNs / NanosecondsPerMicrosecond + (intervalNs % NanosecondsPerMicrosecond == 0 ? 0u : 1u);
}
