using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanFilmProfileDraftFileServiceTests
{
    private readonly ScanFilmProfileDraftFileService _service = new();

    [Fact]
    public void RoundTrip_UnnamedUnfinishedDraft_PreservesEmptyAndInvalidInputExactly()
    {
        var current = ScanFilmProfileDraft.CreateDefault();
        var file = new ScanFilmProfileDraftFile(
            current,
            current,
            new Dictionary<string, string?>
            {
                ["RowsText"] = string.Empty,
                ["TargetLinePitchText"] = "  invalid value  ",
                ["ProfileNameText"] = null
            },
            new Dictionary<string, IReadOnlyDictionary<string, string?>>
            {
                ["Red"] = new Dictionary<string, string?>
                {
                    ["ExposureMicroseconds"] = "-1.5e?",
                    ["Adc1GainText"] = "  ",
                    ["EffectiveStartText"] = string.Empty
                }
            });

        var parsed = _service.Parse(_service.Serialize(file));

        Assert.True(parsed.CanOpen);
        Assert.Equal(ScanFilmProfileDraftFileError.None, parsed.Error);
        var restored = Assert.IsType<ScanFilmProfileDraftFile>(parsed.DraftFile);
        Assert.True(current.HasSameContentAs(restored.CurrentDraft));
        Assert.True(current.HasSameContentAs(restored.BaselineDraft));
        Assert.Equal(string.Empty, restored.CurrentDraft.ProfileName);
        Assert.Empty(restored.CurrentDraft.ChannelProfiles);
        Assert.Equal(file.RawFields, restored.RawFields);
        Assert.Equal(file.ChannelRawFields["Red"], restored.ChannelRawFields["red"]);
        Assert.False(new ScanFilmProfileDocumentService().Build(restored.CurrentDraft).CanApply);
    }

    [Fact]
    public void RoundTrip_TypedChannelProfilesAndOriginalBaseline_ArePreservedWithoutNormalization()
    {
        var source = new ScanFilmProfileDocumentService().Parse(ReadFixture("full-v5.json"));
        var baseline = ScanFilmProfileDraft.FromDocument(Assert.IsType<ScanFilmParameterProfileSet>(source.Document)).Draft;
        var current = new ScanFilmProfileDraft(
            "  unfinished name  ",
            baseline.SavedAtUtc,
            baseline.ChannelProfiles,
            baseline.SelectedCalibrationChannel,
            baseline.AcquisitionSettings! with { Rows = 0, TargetLinePitchMicrometers = -3.25 },
            new ScanFilmScanRecipeSettings(
                new ScanChannelAssignment("Red", "Green", "Blue", "White", true, false, true, false),
                ScanColorManagementOptions.CreateDefault() with { OutputGamma = -1 },
                ScanChannelAlignmentMode.EccThenMutualInformation,
                ScanDngExportMode.LinearRgbIrw));

        var restored = Assert.IsType<ScanFilmProfileDraftFile>(
            _service.Parse(_service.Serialize(new ScanFilmProfileDraftFile(current, baseline))).DraftFile);

        Assert.True(current.HasSameContentAs(restored.CurrentDraft));
        Assert.True(baseline.HasSameContentAs(restored.BaselineDraft));
        Assert.False(restored.CurrentDraft.HasSameContentAs(restored.BaselineDraft));
        Assert.Equal("  unfinished name  ", restored.CurrentDraft.ProfileName);
        Assert.Equal(0, restored.CurrentDraft.AcquisitionSettings!.Rows);
        Assert.Equal(-3.25, restored.CurrentDraft.AcquisitionSettings.TargetLinePitchMicrometers);
        Assert.Equal(-1, restored.CurrentDraft.ScanRecipeSettings!.ColorManagement!.OutputGamma);
    }

    [Fact]
    public void Constructor_CopiesRawFieldsAndChannelCollections()
    {
        var fields = new Dictionary<string, string?> { ["RowsText"] = "unfinished" };
        var channelFields = new Dictionary<string, string?> { ["ExposureText"] = string.Empty };
        var channels = new Dictionary<string, IReadOnlyDictionary<string, string?>> { ["Blue"] = channelFields };
        var file = new ScanFilmProfileDraftFile(ScanFilmProfileDraft.CreateDefault(), ScanFilmProfileDraft.CreateDefault(), fields, channels);

        fields["RowsText"] = "128";
        channelFields["ExposureText"] = "100";
        channels.Clear();

        Assert.Equal("unfinished", file.RawFields["RowsText"]);
        Assert.Equal(string.Empty, file.ChannelRawFields["Blue"]["ExposureText"]);
    }

    [Fact]
    public void DraftEnvelope_IsRejectedByRunnableDocumentParser_EvenIfRenamedJson()
    {
        var text = _service.Serialize(EmptyFile());

        var parsed = new ScanFilmProfileDocumentService().Parse(text);

        Assert.False(parsed.CanApply);
        Assert.Null(parsed.Document);
        Assert.Contains(parsed.Validation.Issues, issue => issue.Code == ScanFilmProfileValidationCode.MissingRequiredProperty);
        Assert.Equal(ScanFilmProfileDraftFileService.DocumentKind, JObject.Parse(text)["Kind"]!.Value<string>());
        Assert.Null(JObject.Parse(text)["SchemaVersion"]);
    }

    [Fact]
    public void RunnableProfile_IsRejectedAsAnEditingDraft()
    {
        var parsed = _service.Parse(ReadFixture("full-v5.json"));

        Assert.False(parsed.CanOpen);
        Assert.Null(parsed.DraftFile);
        Assert.Equal(ScanFilmProfileDraftFileError.UnsupportedKind, parsed.Error);
    }

    [Theory]
    [InlineData("Kind", "\"some-other-document\"", ScanFilmProfileDraftFileError.UnsupportedKind)]
    [InlineData("Version", "2", ScanFilmProfileDraftFileError.UnsupportedVersion)]
    [InlineData("Version", "\"1\"", ScanFilmProfileDraftFileError.UnsupportedVersion)]
    [InlineData("Version", "1.0", ScanFilmProfileDraftFileError.UnsupportedVersion)]
    public void Parse_RejectsDifferentKindOrUnsupportedVersion(string path, string replacement, ScanFilmProfileDraftFileError expected)
    {
        var root = JObject.Parse(_service.Serialize(EmptyFile()));
        root.SelectToken(path)!.Replace(JToken.Parse(replacement));

        var parsed = _service.Parse(root.ToString(Formatting.None));

        Assert.False(parsed.CanOpen);
        Assert.Null(parsed.DraftFile);
        Assert.Equal(expected, parsed.Error);
        Assert.Equal(path, parsed.FieldPath);
    }

    [Theory]
    [InlineData("CurrentDraft", "null")]
    [InlineData("CurrentDraft.ProfileName", "128")]
    [InlineData("CurrentDraft.ProfileName", "null")]
    [InlineData("CurrentDraft.SavedAtUtc", "\"not a timestamp\"")]
    [InlineData("CurrentDraft.ChannelProfiles", "[]")]
    [InlineData("CurrentDraft.ChannelProfiles", "{\"Red\":null}")]
    [InlineData("CurrentDraft.AcquisitionSettings.Rows", "\"128\"")]
    [InlineData("CurrentDraft.AcquisitionSettings.WarmUpEnabled", "\"true\"")]
    [InlineData("CurrentDraft.AcquisitionSettings.Led1Level", "65536")]
    [InlineData("RawFields", "{\"RowsText\":128}")]
    [InlineData("RawFields", "{\"RowsText\":{\"nested\":\"value\"}}")]
    [InlineData("ChannelRawFields", "{\"Blue\":null}")]
    [InlineData("ChannelRawFields", "{\"Blue\":{},\"blue\":{}}")]
    public void Parse_RejectsWrongStorageTypesWithoutProducingPartialDraft(string path, string replacement)
    {
        var root = JObject.Parse(_service.Serialize(EmptyFile()));
        root.SelectToken(path)!.Replace(JToken.Parse(replacement));

        var parsed = _service.Parse(root.ToString(Formatting.None));

        Assert.False(parsed.CanOpen);
        Assert.Null(parsed.DraftFile);
        Assert.Equal(ScanFilmProfileDraftFileError.InvalidShape, parsed.Error);
    }

    [Theory]
    [InlineData("BaselineDraft")]
    [InlineData("RawFields")]
    [InlineData("ChannelRawFields")]
    public void Parse_RejectsMissingRequiredEnvelopeMember(string member)
    {
        var root = JObject.Parse(_service.Serialize(EmptyFile()));
        root.Remove(member);

        var parsed = _service.Parse(root.ToString(Formatting.None));

        Assert.False(parsed.CanOpen);
        Assert.Equal(ScanFilmProfileDraftFileError.InvalidShape, parsed.Error);
    }

    [Fact]
    public void Parse_RejectsUnknownMembersToAvoidSilentlyLosingEditingData()
    {
        var root = JObject.Parse(_service.Serialize(EmptyFile()));
        root["SomeFutureEditorState"] = "unsaved work";

        var parsed = _service.Parse(root.ToString(Formatting.None));

        Assert.False(parsed.CanOpen);
        Assert.Equal(ScanFilmProfileDraftFileError.InvalidShape, parsed.Error);
    }

    [Fact]
    public void Parse_RejectsDuplicateMembersAndTrailingDocument()
    {
        var text = _service.Serialize(EmptyFile());
        var duplicated = text.Replace("\"Version\": 1", "\"Version\": 1, \"Version\": 1", StringComparison.Ordinal);

        Assert.Equal(ScanFilmProfileDraftFileError.InvalidJson, _service.Parse(duplicated).Error);
        Assert.Equal(ScanFilmProfileDraftFileError.InvalidJson, _service.Parse(text + " {}").Error);
        Assert.Equal(ScanFilmProfileDraftFileError.InvalidJson, _service.Parse("[1, 2]").Error);
        Assert.Equal(ScanFilmProfileDraftFileError.InvalidJson, _service.Parse(null).Error);
    }

    [Fact]
    public void Parse_EnforcesFileSizeInUtf8Bytes()
    {
        var text = new string('中', ScanFilmProfileDraftFileService.MaximumFileBytes / 3 + 1);

        var parsed = _service.Parse(text);

        Assert.False(parsed.CanOpen);
        Assert.Equal(ScanFilmProfileDraftFileError.TooLarge, parsed.Error);
    }

    [Fact]
    public void Serialize_RejectsExcessiveRawFieldText_WithoutTruncatingIt()
    {
        var file = new ScanFilmProfileDraftFile(
            ScanFilmProfileDraft.CreateDefault(),
            ScanFilmProfileDraft.CreateDefault(),
            new Dictionary<string, string?>
            {
                ["RowsText"] = new string('1', ScanFilmProfileDraftFileService.MaximumFieldTextLength + 1)
            });

        Assert.Throws<ArgumentException>(() => _service.Serialize(file));
        Assert.Equal(ScanFilmProfileDraftFileService.MaximumFieldTextLength + 1, file.RawFields["RowsText"]!.Length);
    }

    [Fact]
    public void Parse_RejectsExcessiveFieldOrChannelCounts()
    {
        var root = JObject.Parse(_service.Serialize(EmptyFile()));
        root["RawFields"] = JObject.FromObject(Enumerable.Range(0, ScanFilmProfileDraftFileService.MaximumRawFields + 1)
            .ToDictionary(index => $"field{index}", _ => string.Empty));
        Assert.Equal(ScanFilmProfileDraftFileError.InvalidShape, _service.Parse(root.ToString(Formatting.None)).Error);

        root["RawFields"] = new JObject();
        root["ChannelRawFields"] = JObject.FromObject(Enumerable.Range(0, ScanFilmProfileDraftFileService.MaximumChannels + 1)
            .ToDictionary(index => $"channel{index}", _ => new Dictionary<string, string?>()));
        Assert.Equal(ScanFilmProfileDraftFileError.InvalidShape, _service.Parse(root.ToString(Formatting.None)).Error);
    }

    private static ScanFilmProfileDraftFile EmptyFile()
        => new(ScanFilmProfileDraft.CreateDefault(), ScanFilmProfileDraft.CreateDefault());

    private static string ReadFixture(string fileName)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", fileName));
}
