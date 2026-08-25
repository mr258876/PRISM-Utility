using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanCalibrationProfileRepositoryCharacterizationTests
{
    [Fact]
    public void ExistingProfileNormalization_PreservesTrimmedCaseInsensitiveRolesLevelsAndSelectedChannel()
    {
        IScanFilmProfileDocumentService documents = new ScanFilmProfileDocumentService();
        var draft = new ScanFilmProfileDraft(
            " Calibration ",
            DateTimeOffset.UnixEpoch,
            new Dictionary<string, ScanChannelCalibrationProfile>
            {
                [" blue "] = new(
                    new ScanParameterSnapshot(10, 0, 1, 0, 1, 30_000),
                    ScanCalibrationRoiSettings.CreateDefault(),
                    BlackLevel: 100,
                    WhiteLevel: 100)
            },
            " BLUE ",
            null,
            null);

        var result = documents.Build(draft);

        var document = Assert.IsType<ScanFilmParameterProfileSet>(result.Document);
        var profile = Assert.Single(document.ChannelProfiles);
        Assert.True(result.CanApply);
        Assert.Equal("blue", profile.Key);
        Assert.True(document.ChannelProfiles.ContainsKey("BLUE"));
        Assert.Equal((ushort)99, profile.Value.BlackLevel);
        Assert.Equal((ushort)100, profile.Value.WhiteLevel);
        Assert.Equal("BLUE", document.SelectedCalibrationChannel);
        Assert.Equal(5, document.SchemaVersion);
    }

    [Fact]
    public void ExistingLegacyMigration_NormalizesProfilesWithoutLevels()
    {
        var migrated = ScanFilmProfileCompatibility.MigrateLegacyLocalProfiles(
            new Dictionary<string, ScanParameterSnapshot>
            {
                [" blue "] = new(10, -2, 3, 4, 5, 30_000),
                ["invalid"] = new(9, 0, 64, 0, 1, 30_000)
            });

        var profile = Assert.Single(migrated);
        Assert.Equal("blue", profile.Key);
        Assert.True(migrated.ContainsKey("BLUE"));
        Assert.Equal(ScanCalibrationRoiSettings.CreateDefault().Normalize(), profile.Value.RoiSettings);
        Assert.Null(profile.Value.BlackLevel);
        Assert.Null(profile.Value.WhiteLevel);
    }
}
