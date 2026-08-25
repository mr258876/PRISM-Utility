using System.Text.Json;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class ScanFilmProfileFixtureCharacterizationTests
{
    [Fact]
    public async Task DeserializeLiteralFullV5Fixture_PreservesDocumentValuesAndPropertyNames()
    {
        var json = ReadFixture("full-v5.json");

        var profileSet = await Json.ToObjectAsync<ScanFilmParameterProfileSet>(json);

        Assert.NotNull(profileSet);
        Assert.Equal(5, profileSet.SchemaVersion);
        Assert.Equal("Kodak Portra 400", profileSet.ProfileName);
        Assert.Equal(new DateTimeOffset(2026, 7, 16, 12, 34, 56, TimeSpan.Zero), profileSet.SavedAtUtc);
        Assert.Equal("Green", profileSet.SelectedCalibrationChannel);
        Assert.Equal(new[] { "Blue", "Green" }, profileSet.ChannelProfiles.Keys);
        Assert.Equal(new ScanParameterSnapshot(1300, -10, 11, 18, 14, 48000), profileSet.ChannelProfiles["Green"].Parameters);
        Assert.Equal((ushort)345, profileSet.ChannelProfiles["Blue"].BlackLevel);
        Assert.Equal((ushort)61000, profileSet.ChannelProfiles["Green"].WhiteLevel);
        Assert.Equal((ushort)303, profileSet.AcquisitionSettings!.Led3Level);
        Assert.Equal((byte)10, profileSet.AcquisitionSettings.SyncMask);
        Assert.Equal(1500u, profileSet.AcquisitionSettings.MotorIntervalNs);
        Assert.Equal("Red", profileSet.AcquisitionSettings.Led3ChannelColor);
        Assert.Equal("White", profileSet.ScanRecipeSettings!.ChannelAssignment!.Channel2Role);
        Assert.True(profileSet.ScanRecipeSettings.ChannelAssignment.Channel2Reversed);
        Assert.Equal(ScanTargetWhitePointMode.D50, profileSet.ScanRecipeSettings.ColorManagement!.TargetWhitePointMode);
        Assert.Equal(ScanChannelAlignmentMode.EccThenMutualInformation, profileSet.ScanRecipeSettings.AlignmentMode);
        Assert.Equal(ScanDngExportMode.LinearRgbIrw, profileSet.ScanRecipeSettings.DngExportMode);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            new[]
            {
                "SchemaVersion",
                "ProfileName",
                "SavedAtUtc",
                "ChannelProfiles",
                "SelectedCalibrationChannel",
                "AcquisitionSettings",
                "ScanRecipeSettings"
            },
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.True(document.RootElement.TryGetProperty("AcquisitionSettings", out _));
        Assert.True(document.RootElement.TryGetProperty("ScanRecipeSettings", out _));
    }

    [Fact]
    public async Task DeserializeLiteralMinimalV5Fixture_AllowsAbsentOptionalSettings()
    {
        var profileSet = await Json.ToObjectAsync<ScanFilmParameterProfileSet>(ReadFixture("minimal-v5.json"));

        Assert.NotNull(profileSet);
        Assert.Equal(5, profileSet.SchemaVersion);
        Assert.Equal("Minimal v5 Profile", profileSet.ProfileName);
        Assert.Single(profileSet.ChannelProfiles);
        Assert.Equal("Blue", profileSet.SelectedCalibrationChannel);
        Assert.Null(profileSet.AcquisitionSettings);
        Assert.Null(profileSet.ScanRecipeSettings);
    }

    [Fact]
    public async Task DeserializeLiteralV5Fixture_AllowsExplicitNullOptionalSettings()
    {
        var profileSet = await Json.ToObjectAsync<ScanFilmParameterProfileSet>(ReadFixture("null-optionals-v5.json"));

        Assert.NotNull(profileSet);
        Assert.Equal(5, profileSet.SchemaVersion);
        Assert.Equal("Red", profileSet.SelectedCalibrationChannel);
        Assert.Null(profileSet.AcquisitionSettings);
        Assert.Null(profileSet.ScanRecipeSettings);
    }

    private static string ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", fileName);
        Assert.True(File.Exists(path), $"Expected copied fixture at '{path}'.");
        return File.ReadAllText(path);
    }
}
