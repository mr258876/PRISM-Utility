using PRISM_Utility.Core.Services;

namespace PRISM_Utility.Core.Models;

public enum ScanFilmProfileParseErrorCode
{
    MalformedJson,
    MissingRequiredProperty,
    UnsupportedSchemaVersion
}

public sealed record ScanFilmProfileParseError(ScanFilmProfileParseErrorCode Code);

public sealed record ScanFilmProfileParseResult(
    ScanFilmParameterProfileSet? Profile,
    ScanFilmProfileParseError? Error);

public static class ScanFilmProfileCompatibility
{
    private static readonly ScanFilmProfileDocumentService DocumentService = new();

    public static ScanFilmProfileParseResult ParseExchangeJson(string json)
    {
        var result = DocumentService.Parse(json);
        return result.Document is not null
            ? new ScanFilmProfileParseResult(result.Document, null)
            : new ScanFilmProfileParseResult(null, new ScanFilmProfileParseError(MapError(result.Validation)));
    }

    public static IReadOnlyDictionary<string, ScanChannelCalibrationProfile> MigrateLegacyLocalProfiles(
        IReadOnlyDictionary<string, ScanParameterSnapshot>? legacyProfiles)
        => DocumentService.MigrateLegacyLocalProfiles(legacyProfiles);

    private static ScanFilmProfileParseErrorCode MapError(ScanFilmProfileValidationResult validation)
        => validation.Issues.FirstOrDefault()?.Code switch
        {
            ScanFilmProfileValidationCode.UnsupportedSchemaVersion => ScanFilmProfileParseErrorCode.UnsupportedSchemaVersion,
            ScanFilmProfileValidationCode.MalformedJson => ScanFilmProfileParseErrorCode.MalformedJson,
            _ => ScanFilmProfileParseErrorCode.MissingRequiredProperty
        };
}
