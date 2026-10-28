using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Helpers;

public static class FilmProfileValidationTextPresenter
{
    private const string GenericValidationResourceKey = "FilmProfile_Validation_GenericInputInvalid";
    private const string SelectedParameterPath = "ChannelProfiles.Selected.Parameters";

    public static ScanFilmProfileValidationIssue CreateChannelParameterInputIssue(string? parseError, string channelDisplayName, bool isSystemClockUnset = false, string? exposureInputRangeText = null)
    {
        // These prefixes are the parameter service's field identifiers, before its detailed reason.
        var (messageKey, arguments) = parseError switch
        {
            { } error when error.StartsWith("System Clock ", StringComparison.Ordinal) =>
                (isSystemClockUnset ? "FilmProfile.Validation.SystemClockInputMissing" : "FilmProfile.Validation.SystemClockInputInvalid", Array.Empty<string>()),
            { } error when error.StartsWith("Exposure ", StringComparison.Ordinal) =>
                ("FilmProfile.Validation.ExposureInputInvalid", new[] { channelDisplayName, exposureInputRangeText ?? string.Empty }),
            { } error when error.StartsWith("ADC1 Offset ", StringComparison.Ordinal) =>
                ("FilmProfile.Validation.AdcOffsetInputInvalid", new[] { channelDisplayName, "ADC1" }),
            { } error when error.StartsWith("ADC2 Offset ", StringComparison.Ordinal) =>
                ("FilmProfile.Validation.AdcOffsetInputInvalid", new[] { channelDisplayName, "ADC2" }),
            { } error when error.StartsWith("ADC1 Gain ", StringComparison.Ordinal) =>
                ("FilmProfile.Validation.AdcGainInputInvalid", new[] { channelDisplayName, "ADC1" }),
            { } error when error.StartsWith("ADC2 Gain ", StringComparison.Ordinal) =>
                ("FilmProfile.Validation.AdcGainInputInvalid", new[] { channelDisplayName, "ADC2" }),
            _ => ("FilmProfile.Validation.ChannelParametersInvalid", Array.Empty<string>())
        };
        return new ScanFilmProfileValidationIssue(
            ScanFilmProfileValidationCode.InvalidChannelParameters,
            messageKey == "FilmProfile.Validation.SystemClockInputMissing" ? $"{SelectedParameterPath}.SysClockMhz" : SelectedParameterPath,
            ScanFilmProfileValidationSeverity.Error,
            messageKey,
            arguments);
    }

    public static IReadOnlyList<ScanFilmProfileValidationIssue> CreateChannelParameterInputIssues(
        string? parseError, string channelDisplayName, IReadOnlyDictionary<string, string> inputs,
        bool isSystemClockUnset, string exposureInputRangeText)
    {
        var issues = new List<ScanFilmProfileValidationIssue>();
        foreach (var (field, input) in inputs)
        {
            if (!string.IsNullOrWhiteSpace(input) || field == "SysClockMhz" && !isSystemClockUnset)
                continue;
            var (key, arguments) = field switch
            {
                "SysClockMhz" => ("SystemClockInputMissing", Array.Empty<string>()),
                "ExposureMicroseconds" => ("ExposureInputMissing", new[] { channelDisplayName }),
                "Adc1Offset" => ("AdcOffsetInputMissing", new[] { channelDisplayName, "ADC1" }),
                "Adc2Offset" => ("AdcOffsetInputMissing", new[] { channelDisplayName, "ADC2" }),
                "Adc1Gain" => ("AdcGainInputMissing", new[] { channelDisplayName, "ADC1" }),
                "Adc2Gain" => ("AdcGainInputMissing", new[] { channelDisplayName, "ADC2" }),
                _ => (string.Empty, Array.Empty<string>())
            };
            if (key.Length > 0)
                issues.Add(new(ScanFilmProfileValidationCode.InvalidChannelParameters,
                    $"{SelectedParameterPath}.{field}", ScanFilmProfileValidationSeverity.Error,
                    $"FilmProfile.Validation.{key}", arguments));
        }

        var failure = CreateChannelParameterInputIssue(parseError, channelDisplayName, isSystemClockUnset, exposureInputRangeText);
        var failureField = failure.MessageKey switch
        {
            "FilmProfile.Validation.SystemClockInputMissing" or "FilmProfile.Validation.SystemClockInputInvalid" => "SysClockMhz",
            "FilmProfile.Validation.ExposureInputInvalid" => "ExposureMicroseconds",
            "FilmProfile.Validation.AdcOffsetInputInvalid" => failure.MessageArguments[1] == "ADC1" ? "Adc1Offset" : "Adc2Offset",
            "FilmProfile.Validation.AdcGainInputInvalid" => failure.MessageArguments[1] == "ADC1" ? "Adc1Gain" : "Adc2Gain",
            _ => string.Empty
        };
        if (!issues.Any(issue => issue.FieldPath == $"{SelectedParameterPath}.{failureField}"))
            issues.Add(failure);
        return issues;
    }

    public static bool IsPendingInput(ScanFilmProfileValidationIssue issue)
        => issue.MessageKey is "FilmProfile.Validation.SystemClockInputMissing"
            or "FilmProfile.Validation.ExposureInputMissing"
            or "FilmProfile.Validation.AdcOffsetInputMissing"
            or "FilmProfile.Validation.AdcGainInputMissing";

    public static bool IsSystemClockInputIssue(ScanFilmProfileValidationIssue issue)
        => issue.MessageKey is "FilmProfile.Validation.SystemClockInputMissing" or "FilmProfile.Validation.SystemClockInputInvalid";

    public static string GetFieldDisplayName(string? fieldPath)
    {
        var path = fieldPath?.ToLowerInvariant() ?? string.Empty;
        var field = path.Split('.').Last();
        var (key, fallback) = field switch
        {
            "profilename" => ("ProfileName", "Profile name"),
            "sysclockmhz" => ("SystemClock", "System clock"),
            "exposuremicroseconds" => ("Exposure", "Exposure time"),
            "adc1offset" => ("Adc1Offset", "ADC1 offset"),
            "adc2offset" => ("Adc2Offset", "ADC2 offset"),
            "adc1gain" => ("Adc1Gain", "ADC1 gain"),
            "adc2gain" => ("Adc2Gain", "ADC2 gain"),
            "redwavelengthnm" => ("RedWavelength", "Red wavelength"),
            "greenwavelengthnm" => ("GreenWavelength", "Green wavelength"),
            "bluewavelengthnm" => ("BlueWavelength", "Blue wavelength"),
            "outputgamma" => ("OutputGamma", "Output gamma"),
            "targetwhitepointmode" or "manualwhitepointcolortemperaturek" => ("WhitePoint", "Target white point"),
            "alignmentmode" => ("Alignment", "Channel alignment"),
            "dngexportmode" => ("DngExport", "DNG output"),
            "rows" => ("Rows", "Scan rows"),
            "scanmotor" or "scanmotorid" => ("ScanMotor", "Scan motor"),
            "targetlinepitchmicrometers" or "motorintervalns" => ("LinePitch", "Motor distance per line"),
            "transportstrategy" => ("Transport", "Transport mode"),
            "channelassignment" or "acquisitionchannelassignment" => ("ChannelAssignment", "Scan channels"),
            "illumination" => ("Illumination", "Channel illumination"),
            _ when path.Contains("colorManagement", StringComparison.OrdinalIgnoreCase) => ("Color", "Color management"),
            _ when path.StartsWith("channelprofiles", StringComparison.Ordinal) => ("Channels", "Channel configuration"),
            _ when path.StartsWith("acquisitionsettings", StringComparison.Ordinal) => ("Acquisition", "Acquisition settings"),
            _ => ("Configuration", "Film configuration")
        };
        return $"FilmProfile_Field_{key}".GetLocalizedOrFallback(fallback);
    }

    public static string GetValidationIssueText(ScanFilmProfileValidationIssue? issue)
    {
        if (issue is null)
            return string.Empty;

        var resourceKey = ToResourceKey(issue.MessageKey);
        var fallback = issue.MessageKey switch
        {
            "FilmProfile.Validation.ExposureInputInvalid" => "{0}: In channel details, enter an exposure in μs within the allowed range. {1}",
            "FilmProfile.Validation.ExposureInputMissing" => "{0}: Exposure time is not entered. Set it in channel details, in μs.",
            "FilmProfile.Validation.AdcOffsetInputMissing" => "{0}: {1} offset is not entered. Enter an integer from −255 to +255 in channel details.",
            "FilmProfile.Validation.AdcGainInputMissing" => "{0}: {1} gain is not entered. Enter an integer from 0–63 in channel details.",
            "FilmProfile.Validation.SystemClockInputMissing" => "System clock is not set. In Configuration properties → Device settings, enter 30–200 MHz with at most three decimal places. You can save a draft now.",
            "FilmProfile.Validation.SystemClockInputInvalid" => "In Configuration properties → Device settings, enter a system clock from 30–200 MHz with at most three decimal places.",
            "FilmProfile.Validation.AdcOffsetInputInvalid" => "{0}: Enter an integer from −255 to +255 for {1} offset.",
            "FilmProfile.Validation.AdcGainInputInvalid" => "{0}: Enter an integer from 0–63 for {1} gain.",
            _ => GenericValidationResourceKey.GetLocalizedOrFallback("Review this film profile setting and try again.")
        };
        return issue.MessageArguments.Count == 0
            ? resourceKey.GetLocalizedOrFallback(fallback)
            : resourceKey.GetLocalizedFormatOrFallback(fallback, issue.MessageArguments.Cast<object>().ToArray());
    }

    private static string ToResourceKey(string messageKey)
        => messageKey.Replace('.', '_');
}
