using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanFilmProfileFileCoordinatorTests
{
    [Fact]
    public async Task CancelAndMalformedImport_AreExplicitAndDoNotThrow()
    {
        var canceled = new FakeGateway { Read = new(true, null) };
        var coordinator = new ScanFilmProfileFileCoordinator(canceled, new ScanFilmProfileDocumentService());
        var canceledResult = await coordinator.ImportAsync(CancellationToken.None);

        var malformed = new FakeGateway { Read = new(false, "not json") };
        var malformedResult = await new ScanFilmProfileFileCoordinator(malformed, new ScanFilmProfileDocumentService()).ImportAsync(CancellationToken.None);

        Assert.True(canceledResult.WasCanceled);
        Assert.Null(canceledResult.Profile);
        Assert.False(malformedResult.WasCanceled);
        Assert.Null(malformedResult.Profile);
        Assert.NotNull(malformedResult.Validation);
    }

    [Fact]
    public async Task Export_CancelDoesNotSerializeAndPassesSafeSuggestedName()
    {
        var gateway = new FakeGateway { Write = new(true) };
        var profile = new ScanFilmParameterProfileSet(5, "bad<>name", DateTimeOffset.UnixEpoch, new(), null);
        var result = await new ScanFilmProfileFileCoordinator(gateway, new ScanFilmProfileDocumentService()).ExportAsync(profile, CancellationToken.None);

        Assert.True(result.WasCanceled);
        Assert.Equal("bad__name", gateway.Name);
        Assert.Null(gateway.Text);
        Assert.Null(result.Profile);
        Assert.Null(result.FileName);
    }

    [Fact]
    public async Task Import_MixedInvalidChannels_ExposesValidationButDoesNotPublishProfile()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", "mixed-invalid-channels.json"));
        var result = await new ScanFilmProfileFileCoordinator(new FakeGateway { Read = new(false, json) }, new ScanFilmProfileDocumentService()).ImportAsync(CancellationToken.None);

        Assert.False(result.WasCanceled);
        Assert.Null(result.Profile);
        Assert.NotNull(result.Validation);
        Assert.False(result.Validation.IsValid);
    }

    [Theory]
    [InlineData("invalid-recipe-enums-v5.json", ScanFilmProfileValidationCode.InvalidAlignmentMode)]
    [InlineData("invalid-acquisition-masks-limits-v5.json", ScanFilmProfileValidationCode.InvalidColorManagement)]
    public async Task Import_InvalidNormalizedDocument_DoesNotPublishProfile(string fixtureName, ScanFilmProfileValidationCode expectedCode)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", fixtureName));
        var documents = new ScanFilmProfileDocumentService();
        var parsed = documents.Parse(json);
        var result = await new ScanFilmProfileFileCoordinator(new FakeGateway { Read = new(false, json) }, documents).ImportAsync(CancellationToken.None);

        Assert.NotNull(parsed.Document);
        Assert.False(parsed.CanApply);
        Assert.False(result.WasCanceled);
        Assert.Null(result.Profile);
        Assert.NotNull(result.Validation);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public async Task Export_UsesDocumentServiceSerializerOutput()
    {
        var gateway = new FakeGateway { Write = new(false) };
        var documents = new SerializeTrackingDocumentService();
        var profile = new ScanFilmParameterProfileSet(5, "Profile", DateTimeOffset.UnixEpoch, new(), null);

        var result = await new ScanFilmProfileFileCoordinator(gateway, documents).ExportAsync(profile, CancellationToken.None);

        Assert.False(result.WasCanceled);
        Assert.Equal(1, documents.SerializeCalls);
        Assert.Equal(SerializeTrackingDocumentService.SerializedDocument, gateway.Text);
    }

    [Fact]
    public async Task Todo4_FullV5Fixture_FileCoordinatorExportAndImportPreserveEveryField()
    {
        var documents = new ScanFilmProfileDocumentService();
        var source = Assert.IsType<ScanFilmParameterProfileSet>(documents.Parse(ReadFixture("full-v5.json")).Document);
        var exportGateway = new FakeGateway { Write = new(false) };

        var exported = await new ScanFilmProfileFileCoordinator(exportGateway, documents).ExportAsync(source, CancellationToken.None);
        var imported = await new ScanFilmProfileFileCoordinator(
            new FakeGateway { Read = new(false, Assert.IsType<string>(exportGateway.Text)) },
            documents).ImportAsync(CancellationToken.None);

        Assert.False(exported.WasCanceled);
        Assert.False(imported.WasCanceled);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(source, Assert.IsType<ScanFilmParameterProfileSet>(imported.Profile));
    }

    [Theory]
    [InlineData("future-v8.json", ScanFilmProfileValidationCode.UnsupportedSchemaVersion, "schemaVersion")]
    [InlineData("invalid-v6-acquisition.json", ScanFilmProfileValidationCode.InvalidAcquisitionInput, "acquisitionSettings.rows")]
    public async Task Todo9_InvalidV6OrFutureImport_DoesNotPublishProfile(
        string fixtureName,
        ScanFilmProfileValidationCode expectedCode,
        string expectedPath)
    {
        var result = await new ScanFilmProfileFileCoordinator(
            new FakeGateway { Read = new(false, ReadFixture(fixtureName)) },
            new ScanFilmProfileDocumentService()).ImportAsync(CancellationToken.None);

        Assert.False(result.WasCanceled);
        Assert.Null(result.Profile);
        Assert.Contains(Assert.IsType<ScanFilmProfileValidationResult>(result.Validation).Issues, issue =>
            issue.Code == expectedCode && issue.FieldPath == expectedPath);
    }

    [Fact]
    public async Task Import_UsesActualFileNameInsteadOfLegacyDisplayName()
    {
        var documents = new ScanFilmProfileDocumentService();
        var original = Assert.IsType<ScanFilmParameterProfileSet>(documents.Parse(ReadFixture("full-v5.json")).Document);
        var gateway = new FakeGateway { Read = new(false, documents.Serialize(original), "5207_test.json") };
        var result = await new ScanFilmProfileFileCoordinator(gateway, documents).ImportAsync(CancellationToken.None);
        Assert.Equal("5207_test.json", result.FileName);
        var expected = original with { ProfileName = "5207_test" };
        FilmProfileRoundTripAssertions.EqualCompleteDocument(expected, Assert.IsType<ScanFilmParameterProfileSet>(result.Profile));
    }

    [Fact]
    public async Task SaveAs_SerializesSelectedFileNameAndReturnsTheWrittenDocument()
    {
        var documents = new ScanFilmProfileDocumentService();
        var original = Assert.IsType<ScanFilmParameterProfileSet>(documents.Parse(ReadFixture("full-v5.json")).Document);
        var gateway = new FakeGateway { Write = new(false, "Renamed configuration.json") };
        var result = await new ScanFilmProfileFileCoordinator(gateway, documents).ExportAsync(original, CancellationToken.None);
        Assert.False(result.WasCanceled);
        Assert.Equal("Renamed configuration.json", result.FileName);
        var written = Assert.IsType<ScanFilmParameterProfileSet>(documents.Parse(Assert.IsType<string>(gateway.Text)).Document);
        var expected = original with { ProfileName = "Renamed configuration" };
        FilmProfileRoundTripAssertions.EqualCompleteDocument(expected, written);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(written, Assert.IsType<ScanFilmParameterProfileSet>(result.Profile));
        Assert.NotEqual("Renamed configuration", original.ProfileName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_MissingOrNullLegacyName_UsesFileNameAndPreservesAllParameters(bool useNull)
    {
        var documents = new ScanFilmProfileDocumentService();
        var json = System.Text.Json.Nodes.JsonNode.Parse(ReadFixture("full-v5.json"))!.AsObject();
        var original = Assert.IsType<ScanFilmParameterProfileSet>(documents.Parse(json.ToJsonString()).Document);
        if (useNull)
            json["ProfileName"] = null;
        else
            json.Remove("ProfileName");
        var result = await new ScanFilmProfileFileCoordinator(
            new FakeGateway { Read = new(false, json.ToJsonString(), "档案 5207.json") }, documents).ImportAsync(CancellationToken.None);
        Assert.False(result.WasCanceled);
        Assert.True(result.Validation!.IsValid);
        FilmProfileRoundTripAssertions.EqualCompleteDocument(original with { ProfileName = "档案 5207" },
            Assert.IsType<ScanFilmParameterProfileSet>(result.Profile));
    }

    private sealed class FakeGateway : IScanFilmProfileFileGateway
    {
        public ScanFilmProfileFileReadResult Read { get; init; } = new(true, null);
        public ScanFilmProfileFileWriteResult Write { get; init; } = new(false);
        public string? Name { get; private set; }
        public string? Text { get; private set; }
        public Task<ScanFilmProfileFileReadResult> ReadJsonAsync() => Task.FromResult(Read);
        public Task<ScanFilmProfileFileWriteResult> WriteJsonAsync(string suggestedFileName, Func<string, string> createText)
        {
            Name = suggestedFileName;
            if (Write.WasCanceled)
                return Task.FromResult(Write);
            var fileName = Write.FileName ?? suggestedFileName + ".json";
            Text = createText(fileName);
            return Task.FromResult(Write with { FileName = fileName });
        }
    }

    private sealed class SerializeTrackingDocumentService : IScanFilmProfileDocumentService
    {
        private readonly ScanFilmProfileDocumentService _inner = new();

        public const string SerializedDocument = "serialized-by-document-service";
        public int SerializeCalls { get; private set; }
        public int CurrentSchemaVersion => _inner.CurrentSchemaVersion;
        public ScanFilmProfileDocumentParseResult Parse(string json) => _inner.Parse(json);
        public ScanFilmProfileDocumentBuildResult Build(ScanFilmProfileDraft? draft) => _inner.Build(draft);
        public ScanFilmProfileValidationResult Validate(ScanFilmProfileDraft? draft) => _inner.Validate(draft);
        public IReadOnlyDictionary<string, ScanChannelCalibrationProfile> MigrateLegacyLocalProfiles(IReadOnlyDictionary<string, ScanParameterSnapshot>? legacyProfiles)
            => _inner.MigrateLegacyLocalProfiles(legacyProfiles);

        public string Serialize(ScanFilmParameterProfileSet document)
        {
            SerializeCalls++;
            return SerializedDocument;
        }
    }

    private static string ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", fileName);
        Assert.True(File.Exists(path), $"Expected copied fixture at '{path}'.");
        return File.ReadAllText(path);
    }
}
