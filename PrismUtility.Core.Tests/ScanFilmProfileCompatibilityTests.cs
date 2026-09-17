using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanFilmProfileCompatibilityTests
{
    [Fact]
    public void ParseExchangeJson_RejectsFutureSchemaWithoutReturningPartialDocument()
    {
        var outcome = ParseFixture("future-v7.json");

        Assert.Null(outcome.Profile);
        Assert.Equal(ScanFilmProfileParseErrorCode.UnsupportedSchemaVersion, outcome.Error?.Code);
    }

    [Theory]
    [InlineData("missing-required-v5.json", "MissingRequiredProperty")]
    [InlineData("malformed-v5.json", "MalformedJson")]
    public void ParseExchangeJson_RejectsMalformedRequiredDataWithoutReturningPartialDocument(string fixtureName, string failureCode)
    {
        var outcome = ParseFixture(fixtureName);

        Assert.Null(outcome.Profile);
        Assert.Equal(Enum.Parse<ScanFilmProfileParseErrorCode>(failureCode), outcome.Error?.Code);
    }

    [Theory]
    [InlineData("full-v5.json", "Green", true)]
    [InlineData("minimal-v5.json", "Blue", false)]
    [InlineData("null-optionals-v5.json", "Red", false)]
    public void ParseExchangeJson_MigratesLiteralV5OptionalSettingsVariants(string fixtureName, string selectedChannel, bool hasRecipeSettings)
    {
        var outcome = ParseFixture(fixtureName);

        var profileSet = Assert.IsType<ScanFilmParameterProfileSet>(outcome.Profile);
        Assert.Null(outcome.Error);
        Assert.Equal(6, profileSet.SchemaVersion);
        Assert.Equal(selectedChannel, profileSet.SelectedCalibrationChannel);
        Assert.NotNull(profileSet.AcquisitionSettings);
        Assert.Equal(hasRecipeSettings, profileSet.ScanRecipeSettings is not null);
    }

    [Fact]
    public void ParseExchangeJson_RejectsChangedV5PropertyNames()
    {
        var outcome = ParseFixture("wrong-property-name-v5.json");

        Assert.Null(outcome.Profile);
        Assert.Equal(ScanFilmProfileParseErrorCode.MissingRequiredProperty, outcome.Error?.Code);
    }

    [Fact]
    public void ParseExchangeJson_DropsInvalidProfileEntriesAndFallsBackToTheFirstValidSelectedChannel()
    {
        var outcome = ParseFixture("invalid-profiles-v5.json");

        Assert.Null(outcome.Error);
        var profileSet = Assert.IsType<ScanFilmParameterProfileSet>(outcome.Profile);
        Assert.Equal("Blue", Assert.Single(profileSet.ChannelProfiles).Key);
        Assert.Equal(new ScanParameterSnapshot(10, 0, 1, 0, 1, 30000), profileSet.ChannelProfiles["Blue"].Parameters);
        Assert.Equal(ScanCalibrationRoiSettings.CreateDefault().Normalize(), profileSet.ChannelProfiles["Blue"].RoiSettings);
        Assert.Equal("Blue", profileSet.SelectedCalibrationChannel);
    }

    [Fact]
    public async Task MigrateLegacyLocalProfiles_DropsInvalidEntriesAndCreatesDefaultRoiProfiles()
    {
        var legacyProfiles = await Json.ToObjectAsync<Dictionary<string, ScanParameterSnapshot>>(ReadFixture("legacy-local-snapshots.json"));
        Assert.NotNull(legacyProfiles);

        var migrated = MigrateLegacyProfiles(legacyProfiles);

        var profile = Assert.Single(migrated);
        Assert.Equal("Blue", profile.Key);
        Assert.Equal(new ScanParameterSnapshot(10, -2, 3, 4, 5, 30000), profile.Value.Parameters);
        Assert.Equal(ScanCalibrationRoiSettings.CreateDefault().Normalize(), profile.Value.RoiSettings);
        Assert.Null(profile.Value.BlackLevel);
        Assert.Null(profile.Value.WhiteLevel);
    }

    private static ScanFilmProfileParseResult ParseFixture(string fileName)
        => ScanFilmProfileCompatibility.ParseExchangeJson(ReadFixture(fileName));

    private static IReadOnlyDictionary<string, ScanChannelCalibrationProfile> MigrateLegacyProfiles(Dictionary<string, ScanParameterSnapshot> legacyProfiles)
        => ScanFilmProfileCompatibility.MigrateLegacyLocalProfiles(legacyProfiles);

    private static string ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", fileName);
        Assert.True(File.Exists(path), $"Expected copied fixture at '{path}'.");
        return File.ReadAllText(path);
    }
}
