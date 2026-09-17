using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanFilmProfileDocumentService : IScanFilmProfileDocumentService
{
    private const int MinimumSupportedSchemaVersion = 5;
    private static readonly string[] V6AcquisitionMemberNames =
    [
        nameof(ScanFilmAcquisitionSettings.Rows),
        nameof(ScanFilmAcquisitionSettings.ScanMotorId),
        nameof(ScanFilmAcquisitionSettings.TargetLinePitchMicrometers),
        nameof(ScanFilmAcquisitionSettings.StartingDirectionPositive),
        nameof(ScanFilmAcquisitionSettings.WarmUpEnabled),
        nameof(ScanFilmAcquisitionSettings.TransportStrategy),
        nameof(ScanFilmAcquisitionSettings.AcquisitionChannelAssignment)
    ];
    private static readonly string[] AssignmentMemberNames =
    [
        nameof(ScanChannelAssignment.Channel1Role),
        nameof(ScanChannelAssignment.Channel2Role),
        nameof(ScanChannelAssignment.Channel3Role),
        nameof(ScanChannelAssignment.Channel4Role),
        nameof(ScanChannelAssignment.Channel1Reversed),
        nameof(ScanChannelAssignment.Channel2Reversed),
        nameof(ScanChannelAssignment.Channel3Reversed),
        nameof(ScanChannelAssignment.Channel4Reversed)
    ];
    public const int CurrentSchemaVersionValue = 6;

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

        var schema = root.Property("SchemaVersion", StringComparison.Ordinal)?.Value;
        if (schema is not JValue { Type: JTokenType.Integer })
            return Failure(ScanFilmProfileValidationCode.MissingRequiredProperty, "schemaVersion", "FilmProfile.Validation.SchemaVersionMissing");

        var actualVersion = schema.ToString(Formatting.None);
        if (!int.TryParse(actualVersion, NumberStyles.Integer, CultureInfo.InvariantCulture, out var schemaVersion)
            || schemaVersion < MinimumSupportedSchemaVersion
            || schemaVersion > CurrentSchemaVersionValue)
        {
            return Failure(
                ScanFilmProfileValidationCode.UnsupportedSchemaVersion,
                "schemaVersion",
                "FilmProfile.Validation.SchemaVersionUnsupported",
                [
                    actualVersion,
                    MinimumSupportedSchemaVersion.ToString(CultureInfo.InvariantCulture),
                    CurrentSchemaVersionValue.ToString(CultureInfo.InvariantCulture)
                ]);
        }

        var requiredIssues = ValidateRequiredProperties(root);
        if (requiredIssues.Any(issue => issue.Severity == ScanFilmProfileValidationSeverity.Error))
            return new ScanFilmProfileDocumentParseResult(null, new ScanFilmProfileValidationResult(requiredIssues));

        var materialized = (JObject)root.DeepClone();
        if (schemaVersion == CurrentSchemaVersionValue)
        {
            if (CanonicalizeAndValidateV6Acquisition(materialized) is { } malformedPath)
                return Failure(ScanFilmProfileValidationCode.MalformedJson, malformedPath, "FilmProfile.Validation.MalformedJson");

            ApplyV6OptionalDefaults(materialized);
        }
        else
        {
            StripV6OnlyAcquisitionMembers(materialized);
        }

        try
        {
            var source = materialized.ToObject<ScanFilmParameterProfileSet>();
            if (source is null)
                return Failure(ScanFilmProfileValidationCode.MalformedJson, "document", "FilmProfile.Validation.MalformedJson");

            return schemaVersion == MinimumSupportedSchemaVersion
                ? ScanFilmProfileDocumentNormalization.MigrateV5ToV6(source, CurrentSchemaVersionValue)
                : ScanFilmProfileDocumentNormalization.NormalizeCurrentDocument(source, CurrentSchemaVersionValue);
        }
        catch (JsonException exception)
        {
            return Failure(
                ScanFilmProfileValidationCode.MalformedJson,
                CanonicalizeJsonPath(GetJsonPath(exception)),
                "FilmProfile.Validation.MalformedJson");
        }
    }

    public ScanFilmProfileDocumentBuildResult Build(ScanFilmProfileDraft? draft)
        => ScanFilmProfileDocumentNormalization.BuildDocument(draft, CurrentSchemaVersionValue);

    public ScanFilmProfileValidationResult Validate(ScanFilmProfileDraft? draft)
        => Build(draft).Validation;

    public string Serialize(ScanFilmParameterProfileSet document)
        => JsonConvert.SerializeObject(
            ScanFilmProfileDocumentNormalization.NormalizeForSerialization(document, CurrentSchemaVersionValue),
            Formatting.None);

    public IReadOnlyDictionary<string, ScanChannelCalibrationProfile> MigrateLegacyLocalProfiles(
        IReadOnlyDictionary<string, ScanParameterSnapshot>? legacyProfiles)
        => ScanFilmProfileDocumentNormalization.MigrateLegacyLocalProfiles(legacyProfiles);

    private static List<ScanFilmProfileValidationIssue> ValidateRequiredProperties(JObject root)
    {
        var issues = new List<ScanFilmProfileValidationIssue>();
        var profileName = root.Property("ProfileName", StringComparison.Ordinal)?.Value;
        if (profileName is not JValue { Type: JTokenType.String, Value: string name })
            issues.Add(Issue(ScanFilmProfileValidationCode.MissingRequiredProperty, "profileName", "FilmProfile.Validation.ProfileNameMissing"));

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

    private static void ApplyV6OptionalDefaults(JObject root)
    {
        if (root.Property("AcquisitionSettings", StringComparison.Ordinal)?.Value is not JObject acquisition)
            return;

        var defaults = ScanFilmAcquisitionSettings.CreateDefault();
        SetDefault(acquisition, nameof(ScanFilmAcquisitionSettings.Rows), defaults.Rows);
        SetDefault(acquisition, nameof(ScanFilmAcquisitionSettings.ScanMotorId), defaults.ScanMotorId);
        SetDefault(acquisition, nameof(ScanFilmAcquisitionSettings.StartingDirectionPositive), defaults.StartingDirectionPositive);
        SetDefault(acquisition, nameof(ScanFilmAcquisitionSettings.WarmUpEnabled), defaults.WarmUpEnabled);
        SetDefault(acquisition, nameof(ScanFilmAcquisitionSettings.TransportStrategy), (int)defaults.TransportStrategy);
    }

    private static string? CanonicalizeAndValidateV6Acquisition(JObject root)
    {
        var containerProperties = root.Properties()
            .Where(property => string.Equals(property.Name, "AcquisitionSettings", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (containerProperties.Count > 1)
            return "acquisitionSettings";
        if (containerProperties.Count == 0 || containerProperties[0].Value is not JObject acquisition)
            return null;

        foreach (var memberName in V6AcquisitionMemberNames)
        {
            var matches = acquisition.Properties()
                .Where(property => string.Equals(property.Name, memberName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var fieldPath = $"acquisitionSettings.{ToFieldName(memberName)}";
            if (matches.Count > 1)
                return fieldPath;
            if (matches.Count == 0)
                continue;

            var property = matches[0];
            if (!HasValidAcquisitionValue(memberName, property.Value))
                return fieldPath;
            CanonicalizeProperty(property, memberName);
        }

        CanonicalizeProperty(containerProperties[0], "AcquisitionSettings");
        return null;
    }

    private static bool HasValidAcquisitionValue(string memberName, JToken value)
        => value.Type == JTokenType.Null || memberName switch
        {
            nameof(ScanFilmAcquisitionSettings.Rows)
                or nameof(ScanFilmAcquisitionSettings.ScanMotorId)
                or nameof(ScanFilmAcquisitionSettings.TransportStrategy) => value.Type == JTokenType.Integer,
            nameof(ScanFilmAcquisitionSettings.TargetLinePitchMicrometers) => value.Type is JTokenType.Integer or JTokenType.Float,
            nameof(ScanFilmAcquisitionSettings.StartingDirectionPositive)
                or nameof(ScanFilmAcquisitionSettings.WarmUpEnabled) => value.Type == JTokenType.Boolean,
            nameof(ScanFilmAcquisitionSettings.AcquisitionChannelAssignment) => CanonicalizeAndValidateAssignment(value),
            _ => false
        };

    private static bool CanonicalizeAndValidateAssignment(JToken token)
    {
        if (token is not JObject assignment)
            return false;

        foreach (var memberName in AssignmentMemberNames)
        {
            var matches = assignment.Properties()
                .Where(property => string.Equals(property.Name, memberName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var expectedType = memberName.EndsWith("Role", StringComparison.Ordinal)
                ? JTokenType.String
                : JTokenType.Boolean;
            if (matches.Count != 1 || matches[0].Value.Type != expectedType)
                return false;
            CanonicalizeProperty(matches[0], memberName);
        }

        return true;
    }

    private static void StripV6OnlyAcquisitionMembers(JObject root)
    {
        foreach (var acquisition in root.Properties()
                     .Where(property => string.Equals(property.Name, "AcquisitionSettings", StringComparison.OrdinalIgnoreCase))
                     .Select(property => property.Value)
                     .OfType<JObject>())
        {
            foreach (var property in acquisition.Properties()
                         .Where(property => GetCanonicalName(V6AcquisitionMemberNames, property.Name) is not null)
                         .ToList())
            {
                property.Remove();
            }
        }
    }

    private static void CanonicalizeProperty(JProperty property, string canonicalName)
    {
        if (!string.Equals(property.Name, canonicalName, StringComparison.Ordinal))
            property.Replace(new JProperty(canonicalName, property.Value.DeepClone()));
    }

    private static void SetDefault(JObject source, string propertyName, object value)
    {
        var property = source.Property(propertyName, StringComparison.Ordinal);
        if (property is null)
            source.Add(propertyName, JToken.FromObject(value));
        else if (property.Value.Type == JTokenType.Null)
            property.Value = JToken.FromObject(value);
    }

    private static string? GetJsonPath(JsonException exception)
        => exception switch
        {
            JsonSerializationException serializationException => serializationException.Path,
            JsonReaderException readerException => readerException.Path,
            _ => null
        };

    private static string CanonicalizeJsonPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "document";

        var canonical = string.Join('.', path.Split('.').Select(segment =>
        {
            var bracketIndex = segment.IndexOf('[', StringComparison.Ordinal);
            var propertyName = bracketIndex < 0 ? segment : segment[..bracketIndex];
            var suffix = bracketIndex < 0 ? string.Empty : segment[bracketIndex..];
            return propertyName.Length == 0
                ? suffix
                : $"{char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}{suffix}";
        }));
        var segments = canonical.Split('.');
        var memberName = segments.Length > 1
            ? GetCanonicalName(V6AcquisitionMemberNames, segments[1])
            : null;
        return segments.Length > 2
               && string.Equals(segments[0], "acquisitionSettings", StringComparison.OrdinalIgnoreCase)
               && memberName is not null
            ? $"acquisitionSettings.{ToFieldName(memberName)}"
            : canonical;
    }

    private static string? GetCanonicalName(IEnumerable<string> canonicalNames, string visibleName)
        => canonicalNames.FirstOrDefault(name => string.Equals(name, visibleName, StringComparison.OrdinalIgnoreCase));

    private static string ToFieldName(string propertyName)
        => $"{char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}";

    private static ScanFilmProfileDocumentParseResult Failure(
        ScanFilmProfileValidationCode code,
        string fieldPath,
        string messageKey,
        IEnumerable<string>? messageArguments = null)
        => new(null, new ScanFilmProfileValidationResult([Issue(code, fieldPath, messageKey, messageArguments)]));

    private static ScanFilmProfileValidationIssue Issue(
        ScanFilmProfileValidationCode code,
        string fieldPath,
        string messageKey,
        IEnumerable<string>? messageArguments = null)
        => new(code, fieldPath, ScanFilmProfileValidationSeverity.Error, messageKey, messageArguments);
}
