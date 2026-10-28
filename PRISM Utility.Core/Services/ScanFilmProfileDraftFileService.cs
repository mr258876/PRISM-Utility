using System.Globalization;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

/// <summary>
/// The draft format is independent of the runnable film profile JSON schema.
/// It checks storage shape and size only; unfinished scan settings are valid drafts.
/// </summary>
public sealed class ScanFilmProfileDraftFileService
{
    public const string FileExtension = ".prismdraft";
    public const string DocumentKind = "prism.film-profile-draft";
    public const int CurrentVersion = 1;
    public const int MaximumFileBytes = 2 * 1024 * 1024;
    public const int MaximumRawFields = 256;
    public const int MaximumChannels = 16;
    public const int MaximumFieldTextLength = 8192;
    public const int MaximumFieldNameLength = 128;
    private static readonly string[] EnvelopeProperties =
        ["Kind", "Version", "CurrentDraft", "BaselineDraft", "RawFields", "ChannelRawFields"];
    private static readonly DefaultContractResolver Contracts = new();
    private static readonly NullabilityInfoContext Nullability = new();

    public string Serialize(ScanFilmProfileDraftFile draftFile)
    {
        ArgumentNullException.ThrowIfNull(draftFile);
        var root = new JObject
        {
            ["Kind"] = DocumentKind,
            ["Version"] = CurrentVersion,
            ["CurrentDraft"] = JToken.FromObject(draftFile.CurrentDraft),
            ["BaselineDraft"] = JToken.FromObject(draftFile.BaselineDraft),
            ["RawFields"] = JToken.FromObject(draftFile.RawFields),
            ["ChannelRawFields"] = JToken.FromObject(draftFile.ChannelRawFields)
        };
        var text = root.ToString(Formatting.Indented);
        if (IsTooLarge(text))
            throw new ArgumentException("The draft exceeds the maximum file size.", nameof(draftFile));
        var check = Parse(text);
        if (!check.CanOpen)
            throw new ArgumentException($"Invalid draft storage data at '{check.FieldPath ?? "document"}'.", nameof(draftFile));
        return text;
    }

    public ScanFilmProfileDraftFileParseResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Failure(ScanFilmProfileDraftFileError.InvalidJson);
        if (IsTooLarge(text))
            return Failure(ScanFilmProfileDraftFileError.TooLarge);

        JObject root;
        try
        {
            using var reader = new JsonTextReader(new StringReader(text))
            {
                DateParseHandling = DateParseHandling.None,
                MaxDepth = 32
            };
            root = JObject.Load(reader, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                CommentHandling = CommentHandling.Ignore
            });
            if (reader.Read())
                return Failure(ScanFilmProfileDraftFileError.InvalidJson);
        }
        catch (JsonException)
        {
            return Failure(ScanFilmProfileDraftFileError.InvalidJson);
        }

        if (root["Kind"] is not JValue { Type: JTokenType.String, Value: string kind }
            || !string.Equals(kind, DocumentKind, StringComparison.Ordinal))
            return Failure(ScanFilmProfileDraftFileError.UnsupportedKind, "Kind");
        if (root["Version"]?.Type != JTokenType.Integer
            || !int.TryParse(root["Version"]!.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
            || version != CurrentVersion)
            return Failure(ScanFilmProfileDraftFileError.UnsupportedVersion, "Version");
        if (ValidateShape(root) is { } path)
            return Failure(ScanFilmProfileDraftFileError.InvalidShape, path);

        try
        {
            var channels = ((JObject)root["ChannelRawFields"]!).Properties().ToDictionary(
                property => property.Name,
                property => (IReadOnlyDictionary<string, string?>)ReadFields((JObject)property.Value),
                StringComparer.OrdinalIgnoreCase);
            var file = new ScanFilmProfileDraftFile(
                root["CurrentDraft"]!.ToObject<ScanFilmProfileDraft>()!,
                root["BaselineDraft"]!.ToObject<ScanFilmProfileDraft>()!,
                ReadFields((JObject)root["RawFields"]!),
                channels);
            return new ScanFilmProfileDraftFileParseResult(file, ScanFilmProfileDraftFileError.None);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or OverflowException)
        {
            return Failure(ScanFilmProfileDraftFileError.InvalidShape);
        }
    }

    private static string? ValidateShape(JObject root)
    {
        if (root.Properties().Count() != EnvelopeProperties.Length
            || EnvelopeProperties.Any(name => root.Property(name, StringComparison.Ordinal) is null))
            return "document";
        if (!HasExactTypes(root["CurrentDraft"]!, typeof(ScanFilmProfileDraft), false))
            return "CurrentDraft";
        if (!HasExactTypes(root["BaselineDraft"]!, typeof(ScanFilmProfileDraft), false))
            return "BaselineDraft";
        if (!HasRawFields(root["RawFields"]!))
            return "RawFields";
        if (root["ChannelRawFields"] is not JObject channels
            || channels.Properties().Count() > MaximumChannels
            || channels.Properties().Select(property => property.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != channels.Properties().Count())
            return "ChannelRawFields";
        foreach (var channel in channels.Properties())
        {
            if (!HasFieldName(channel.Name) || !HasRawFields(channel.Value))
                return $"ChannelRawFields.{channel.Name}";
        }

        return null;
    }

    private static bool HasRawFields(JToken token)
        => token is JObject fields
           && fields.Properties().Count() <= MaximumRawFields
           && fields.Properties().All(property => HasFieldName(property.Name)
               && (property.Value.Type == JTokenType.Null
                   || property.Value is JValue { Type: JTokenType.String, Value: string value }
                   && value.Length <= MaximumFieldTextLength));

    private static bool HasFieldName(string name)
        => !string.IsNullOrWhiteSpace(name) && name.Length <= MaximumFieldNameLength;

    // Newtonsoft normally coerces e.g. a number into a string or "true" into a
    // bool. Reject those conversions for a versioned editing file.
    private static bool HasExactTypes(JToken token, Type expectedType, bool nullable)
    {
        var underlyingType = Nullable.GetUnderlyingType(expectedType);
        if (token.Type == JTokenType.Null)
            return nullable || underlyingType is not null;
        expectedType = underlyingType ?? expectedType;
        if (expectedType == typeof(string))
            return token is JValue { Type: JTokenType.String, Value: string value } && value.Length <= MaximumFieldTextLength;
        if (expectedType == typeof(DateTimeOffset))
            return token.Type == JTokenType.String
                && DateTimeOffset.TryParse(token.Value<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
        if (expectedType == typeof(bool))
            return token.Type == JTokenType.Boolean;
        if (expectedType == typeof(WhiteLightSource) || expectedType == typeof(WhiteLightDriveMode))
            return token.Type == JTokenType.Integer
                && int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var enumValue)
                && Enum.IsDefined(expectedType, enumValue);
        if (expectedType.IsEnum || expectedType == typeof(byte) || expectedType == typeof(ushort)
            || expectedType == typeof(uint) || expectedType == typeof(int))
            return token.Type == JTokenType.Integer;
        if (expectedType == typeof(double))
            return token.Type is JTokenType.Integer or JTokenType.Float
                && double.TryParse(token.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && double.IsFinite(number);

        var contract = Contracts.ResolveContract(expectedType);
        if (contract is JsonDictionaryContract dictionary)
        {
            return token is JObject entries
                && entries.Properties().Count() <= MaximumChannels
                && entries.Properties().Select(property => property.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == entries.Properties().Count()
                && entries.Properties().All(property => HasFieldName(property.Name)
                    && HasExactTypes(property.Value, dictionary.DictionaryValueType!, false));
        }

        if (contract is not JsonObjectContract objectContract || token is not JObject obj)
            return false;
        var properties = objectContract.Properties.Where(property => !property.Ignored && property.Readable).ToArray();
        return obj.Properties().All(input => properties.Any(property => property.PropertyName == input.Name))
            && properties.All(property => obj.Property(property.PropertyName!, StringComparison.Ordinal) is { } input
                ? HasExactTypes(input.Value, property.PropertyType!, AllowsNull(property))
                : IsOptionalLegacyMember(expectedType, property.PropertyName));
    }

    private static bool IsOptionalLegacyMember(Type type, string? member)
        => type == typeof(ScanFilmAcquisitionSettings) && member == nameof(ScanFilmAcquisitionSettings.WhiteChannelSettings)
           || type == typeof(ScanChannelAssignment) && member is nameof(ScanChannelAssignment.Channel5Role) or nameof(ScanChannelAssignment.Channel5Reversed);

    private static bool AllowsNull(JsonProperty property)
    {
        if (Nullable.GetUnderlyingType(property.PropertyType!) is not null)
            return true;
        if (property.PropertyType!.IsValueType)
            return false;
        var reflected = property.DeclaringType?.GetProperty(property.UnderlyingName!, BindingFlags.Public | BindingFlags.Instance);
        if (reflected is null)
            return false;
        // NullabilityInfoContext caches metadata in mutable dictionaries.
        lock (Nullability)
            return Nullability.Create(reflected).ReadState == NullabilityState.Nullable;
    }

    private static Dictionary<string, string?> ReadFields(JObject fields)
        => fields.Properties().ToDictionary(property => property.Name, property => property.Value.Value<string?>(), StringComparer.Ordinal);

    private static bool IsTooLarge(string text)
        => text.Length > MaximumFileBytes || Encoding.UTF8.GetByteCount(text) > MaximumFileBytes;

    private static ScanFilmProfileDraftFileParseResult Failure(ScanFilmProfileDraftFileError error, string? path = null)
        => new(null, error, path);
}
