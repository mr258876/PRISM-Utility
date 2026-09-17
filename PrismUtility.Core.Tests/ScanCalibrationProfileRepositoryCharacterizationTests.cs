using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanCalibrationProfileRepositoryCharacterizationTests
{
    [Fact]
    public void NewProfileBuild_InvalidLevelsAreRejectedWithoutRepair()
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

        Assert.Null(result.Document);
        Assert.False(result.CanApply);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == ScanFilmProfileValidationCode.InvalidRoiInput);
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
