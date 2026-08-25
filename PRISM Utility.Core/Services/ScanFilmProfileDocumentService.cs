using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanFilmProfileDocumentService : IScanFilmProfileDocumentService
{
    public const int CurrentSchemaVersionValue = 5;

    public int CurrentSchemaVersion => CurrentSchemaVersionValue;

    public ScanFilmProfileDocumentParseResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Failure(ScanFilmProfileValidationCode.MalformedJson, "document", "FilmProfile.Validation.MalformedJson");

        JObject root;
        try
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            root = JObject.Load(reader);
        }
        catch (JsonException)
        {
            return Failure(ScanFilmProfileValidationCode.MalformedJson, "document", "FilmProfile.Validation.MalformedJson");
        }

        var requiredIssues = ValidateRequiredProperties(root);
        if (requiredIssues.Count > 0)
            return new ScanFilmProfileDocumentParseResult(null, new ScanFilmProfileValidationResult(requiredIssues));

        try
        {
            var source = root.ToObject<ScanFilmParameterProfileSet>();
            return source is null
                ? Failure(ScanFilmProfileValidationCode.MalformedJson, "document", "FilmProfile.Validation.MalformedJson")
                : ScanFilmProfileDocumentNormalization.NormalizeParsedDocument(source, CurrentSchemaVersionValue);
        }
        catch (JsonException)
        {
            return Failure(ScanFilmProfileValidationCode.MalformedJson, "document", "FilmProfile.Validation.MalformedJson");
        }
    }

    public ScanFilmProfileDocumentBuildResult Build(ScanFilmProfileDraft? draft)
        => ScanFilmProfileDocumentNormalization.BuildDocument(draft, CurrentSchemaVersionValue);

    public ScanFilmProfileValidationResult Validate(ScanFilmProfileDraft? draft)
        => Build(draft).Validation;

    public string Serialize(ScanFilmParameterProfileSet document)
        => JsonConvert.SerializeObject(document, Formatting.None);

    public IReadOnlyDictionary<string, ScanChannelCalibrationProfile> MigrateLegacyLocalProfiles(
        IReadOnlyDictionary<string, ScanParameterSnapshot>? legacyProfiles)
        => ScanFilmProfileDocumentNormalization.MigrateLegacyLocalProfiles(legacyProfiles);

    private static List<ScanFilmProfileValidationIssue> ValidateRequiredProperties(JObject root)
    {
        var issues = new List<ScanFilmProfileValidationIssue>();
        var schema = root.Property("SchemaVersion", StringComparison.Ordinal)?.Value;
        if (schema is not JValue { Type: JTokenType.Integer, Value: long version })
            issues.Add(Issue(ScanFilmProfileValidationCode.MissingRequiredProperty, "schemaVersion", "FilmProfile.Validation.SchemaVersionMissing"));
        else if (version != CurrentSchemaVersionValue)
            issues.Add(Issue(ScanFilmProfileValidationCode.UnsupportedSchemaVersion, "schemaVersion", "FilmProfile.Validation.SchemaVersionUnsupported"));

        var profileName = root.Property("ProfileName", StringComparison.Ordinal)?.Value;
        if (profileName is not JValue { Type: JTokenType.String, Value: string name })
            issues.Add(Issue(ScanFilmProfileValidationCode.MissingRequiredProperty, "profileName", "FilmProfile.Validation.ProfileNameMissing"));
        else if (string.IsNullOrWhiteSpace(name))
            issues.Add(Issue(ScanFilmProfileValidationCode.InvalidProfileName, "profileName", "FilmProfile.Validation.ProfileNameInvalid"));

        var timestamp = root.Property("SavedAtUtc", StringComparison.Ordinal)?.Value;
        if (timestamp is not JValue { Type: JTokenType.String, Value: string value }
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
        {
            issues.Add(Issue(ScanFilmProfileValidationCode.InvalidSavedAtUtc, "savedAtUtc", "FilmProfile.Validation.SavedAtUtcInvalid"));
        }

        if (root.Property("ChannelProfiles", StringComparison.Ordinal)?.Value.Type != JTokenType.Object)
            issues.Add(Issue(ScanFilmProfileValidationCode.MissingChannelProfiles, "channelProfiles", "FilmProfile.Validation.ChannelProfilesMissing"));

        return issues;
    }

    private static ScanFilmProfileDocumentParseResult Failure(ScanFilmProfileValidationCode code, string fieldPath, string messageKey)
        => new(null, new ScanFilmProfileValidationResult([Issue(code, fieldPath, messageKey)]));

    private static ScanFilmProfileValidationIssue Issue(ScanFilmProfileValidationCode code, string fieldPath, string messageKey)
        => new(code, fieldPath, ScanFilmProfileValidationSeverity.Error, messageKey);
}
