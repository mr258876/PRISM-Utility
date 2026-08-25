using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanFilmProfileDraftTests
{
    [Fact]
    public void ParseExchangeJson_FullV5Fixture_PreservesDraftSourceFields()
    {
        var result = ScanFilmProfileCompatibility.ParseExchangeJson(ReadFixture("full-v5.json"));

        var document = Assert.IsType<ScanFilmParameterProfileSet>(result.Profile);
        Assert.Null(result.Error);
        Assert.Equal(5, document.SchemaVersion);
        Assert.Equal("Kodak Portra 400", document.ProfileName);
        Assert.Equal("Green", document.SelectedCalibrationChannel);
        Assert.Equal(new[] { "Blue", "Green" }, document.ChannelProfiles.Keys);
    }

    [Fact]
    public void CreateDefault_ReturnsDeterministicLocaleNeutralProfileValues()
    {
        var draft = ScanFilmProfileDraft.CreateDefault();

        Assert.Equal(string.Empty, draft.ProfileName);
        Assert.DoesNotContain("Untitled Film Profile", draft.ProfileName, StringComparison.Ordinal);
        Assert.Equal(DateTimeOffset.UnixEpoch, draft.SavedAtUtc);
        Assert.Empty(draft.ChannelProfiles);
        Assert.Null(draft.SelectedCalibrationChannel);
        Assert.Equal(ScanFilmAcquisitionSettings.CreateDefault(), draft.AcquisitionSettings);
        Assert.Null(draft.ScanRecipeSettings);
    }

    [Fact]
    public void FromDocument_PreservesSelectedChannelAndDeepCopiesProfiles()
    {
        var source = ParseFullDocument();
        var conversion = ScanFilmProfileDraft.FromDocument(source);
        var originalBlueProfile = source.ChannelProfiles["Blue"];

        var draft = conversion.Draft;
        var editedDraftProfiles = draft.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        editedDraftProfiles["Blue"] = originalBlueProfile with { BlackLevel = 1 };
        source.ChannelProfiles["Blue"] = originalBlueProfile with { BlackLevel = 2 };

        Assert.True(conversion.Validation.IsValid);
        Assert.Equal("Green", draft.SelectedCalibrationChannel);
        Assert.Equal(originalBlueProfile, draft.ChannelProfiles["Blue"]);
        Assert.Equal((ushort)2, source.ChannelProfiles["Blue"].BlackLevel);
        Assert.Equal((ushort)1, editedDraftProfiles["Blue"].BlackLevel);
    }

    [Fact]
    public async Task FromDocument_MissingChannelProfiles_ReturnsSafeDraftAndStructuredIssue()
    {
        var missingCollection = await DeserializeDocumentAsync("""
        {
          "SchemaVersion": 5,
          "ProfileName": "Missing channels",
          "SavedAtUtc": "1970-01-01T00:00:00+00:00",
          "SelectedCalibrationChannel": "Blue"
        }
        """);

        var missingCollectionConversion = ScanFilmProfileDraft.FromDocument(missingCollection);
        var missingDocumentConversion = ScanFilmProfileDraft.FromDocument(null);

        Assert.Empty(missingCollectionConversion.Draft.ChannelProfiles);
        var missingCollectionIssue = Assert.Single(missingCollectionConversion.Validation.Issues);
        Assert.Equal(ScanFilmProfileValidationCode.MissingChannelProfiles, missingCollectionIssue.Code);
        Assert.Equal("channelProfiles", missingCollectionIssue.FieldPath);
        Assert.Equal(ScanFilmProfileValidationSeverity.Error, missingCollectionIssue.Severity);
        Assert.Equal("FilmProfile.Validation.ChannelProfilesMissing", missingCollectionIssue.MessageKey);
        Assert.False(missingCollectionConversion.Validation.IsValid);
        Assert.Equal(string.Empty, missingDocumentConversion.Draft.ProfileName);
        Assert.DoesNotContain("Untitled Film Profile", missingDocumentConversion.Draft.ProfileName, StringComparison.Ordinal);
        Assert.Equal(ScanFilmProfileValidationCode.MissingDocument, Assert.Single(missingDocumentConversion.Validation.Issues).Code);
    }

    [Fact]
    public void HasSameContentAs_DetectsSemanticBaselineChanges()
    {
        var baseline = ScanFilmProfileDraft.FromDocument(ParseFullDocument()).Draft;
        var sameContent = ScanFilmProfileDraft.FromDocument(ParseFullDocument()).Draft;
        var changed = new ScanFilmProfileDraft(
            "Changed profile name",
            baseline.SavedAtUtc,
            baseline.ChannelProfiles,
            baseline.SelectedCalibrationChannel,
            baseline.AcquisitionSettings,
            baseline.ScanRecipeSettings);

        Assert.True(baseline.HasSameContentAs(sameContent));
        Assert.False(baseline.HasSameContentAs(changed));
    }

    [Fact]
    public void ValidationResult_OrdersIssuesAndAggregatesValidity()
    {
        var messageArguments = new[] { "Blue" };
        var result = new ScanFilmProfileValidationResult(
        [
            new ScanFilmProfileValidationIssue(
                ScanFilmProfileValidationCode.MissingDocument,
                "profileName",
                ScanFilmProfileValidationSeverity.Warning,
                "FilmProfile.Validation.ProfileNameMissing"),
            new ScanFilmProfileValidationIssue(
                ScanFilmProfileValidationCode.MissingChannelProfiles,
                "channelProfiles",
                ScanFilmProfileValidationSeverity.Error,
                "FilmProfile.Validation.ChannelProfilesMissing",
                messageArguments)
        ]);
        messageArguments[0] = "Mutated";

        Assert.Equal(
            new[]
            {
                ScanFilmProfileValidationCode.MissingChannelProfiles,
                ScanFilmProfileValidationCode.MissingDocument
            },
            result.Issues.Select(issue => issue.Code));
        Assert.False(result.IsValid);
        Assert.Equal("Blue", result.Issues[0].MessageArguments[0]);
        Assert.True(new ScanFilmProfileValidationResult(
        [
            new ScanFilmProfileValidationIssue(
                ScanFilmProfileValidationCode.MissingDocument,
                "profileName",
                ScanFilmProfileValidationSeverity.Warning,
                "FilmProfile.Validation.ProfileNameMissing")
        ]).IsValid);
    }

    [Fact]
    public async Task FromDocument_NullRoiSettings_UsesTheExpectedDefaultRoi()
    {
        var source = await DeserializeDocumentAsync("""
        {
          "SchemaVersion": 5,
          "ProfileName": "Default ROI",
          "SavedAtUtc": "1970-01-01T00:00:00+00:00",
          "ChannelProfiles": {
            "Blue": {
              "Parameters": {
                "ExposureTicks": 10,
                "Adc1Offset": 0,
                "Adc1Gain": 1,
                "Adc2Offset": 0,
                "Adc2Gain": 1,
                "SysClockKhz": 30000
              }
            }
          },
          "SelectedCalibrationChannel": "Blue"
        }
        """);

        var draft = ScanFilmProfileDraft.FromDocument(source).Draft;

        Assert.Equal(
            new ScanCalibrationRoiSettings(
                new ScanColumnRange(128, 7577),
                new ScanColumnRange(26, 115),
                new ScanColumnRange(1171, 3257),
                new ScanColumnRange(4449, 6535),
                new ScanColumnRange(1171, 6535)),
            draft.ChannelProfiles["Blue"].RoiSettings);
    }

    private static ScanFilmParameterProfileSet ParseFullDocument()
        => Assert.IsType<ScanFilmParameterProfileSet>(ScanFilmProfileCompatibility.ParseExchangeJson(ReadFixture("full-v5.json")).Profile);

    private static async Task<ScanFilmParameterProfileSet> DeserializeDocumentAsync(string json)
        => Assert.IsType<ScanFilmParameterProfileSet>(await Json.ToObjectAsync<ScanFilmParameterProfileSet>(json));

    private static string ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", fileName);
        Assert.True(File.Exists(path), $"Expected copied fixture at '{path}'.");
        return File.ReadAllText(path);
    }
}
