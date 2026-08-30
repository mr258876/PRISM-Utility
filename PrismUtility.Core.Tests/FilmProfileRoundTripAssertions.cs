using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

internal static class FilmProfileRoundTripAssertions
{
    public static void EqualCompleteDocument(ScanFilmParameterProfileSet expected, ScanFilmParameterProfileSet actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.ProfileName, actual.ProfileName);
        Assert.Equal(expected.SavedAtUtc, actual.SavedAtUtc);
        Assert.Equal(expected.SelectedCalibrationChannel, actual.SelectedCalibrationChannel);
        Assert.Equal(expected.ChannelProfiles.Keys, actual.ChannelProfiles.Keys);

        foreach (var channel in expected.ChannelProfiles)
        {
            var actualProfile = Assert.IsType<ScanChannelCalibrationProfile>(actual.ChannelProfiles[channel.Key]);
            Assert.Equal(channel.Value.Parameters.ExposureTicks, actualProfile.Parameters.ExposureTicks);
            Assert.Equal(channel.Value.Parameters.Adc1Offset, actualProfile.Parameters.Adc1Offset);
            Assert.Equal(channel.Value.Parameters.Adc1Gain, actualProfile.Parameters.Adc1Gain);
            Assert.Equal(channel.Value.Parameters.Adc2Offset, actualProfile.Parameters.Adc2Offset);
            Assert.Equal(channel.Value.Parameters.Adc2Gain, actualProfile.Parameters.Adc2Gain);
            Assert.Equal(channel.Value.Parameters.SysClockKhz, actualProfile.Parameters.SysClockKhz);
            Assert.Equal(channel.Value.RoiSettings.EffectiveRange, actualProfile.RoiSettings.EffectiveRange);
            Assert.Equal(channel.Value.RoiSettings.ShieldRange, actualProfile.RoiSettings.ShieldRange);
            Assert.Equal(channel.Value.RoiSettings.FocusLeftRange, actualProfile.RoiSettings.FocusLeftRange);
            Assert.Equal(channel.Value.RoiSettings.FocusRightRange, actualProfile.RoiSettings.FocusRightRange);
            Assert.Equal(channel.Value.RoiSettings.FocusOverallRange, actualProfile.RoiSettings.FocusOverallRange);
            Assert.Equal(channel.Value.BlackLevel, actualProfile.BlackLevel);
            Assert.Equal(channel.Value.WhiteLevel, actualProfile.WhiteLevel);
        }

        var expectedAcquisition = Assert.IsType<ScanFilmAcquisitionSettings>(expected.AcquisitionSettings);
        var actualAcquisition = Assert.IsType<ScanFilmAcquisitionSettings>(actual.AcquisitionSettings);
        Assert.Equal(expectedAcquisition.Led1Level, actualAcquisition.Led1Level);
        Assert.Equal(expectedAcquisition.Led2Level, actualAcquisition.Led2Level);
        Assert.Equal(expectedAcquisition.Led3Level, actualAcquisition.Led3Level);
        Assert.Equal(expectedAcquisition.Led4Level, actualAcquisition.Led4Level);
        Assert.Equal(expectedAcquisition.SteadyMask, actualAcquisition.SteadyMask);
        Assert.Equal(expectedAcquisition.SyncMask, actualAcquisition.SyncMask);
        Assert.Equal(expectedAcquisition.Led1PulseClock, actualAcquisition.Led1PulseClock);
        Assert.Equal(expectedAcquisition.Led2PulseClock, actualAcquisition.Led2PulseClock);
        Assert.Equal(expectedAcquisition.Led3PulseClock, actualAcquisition.Led3PulseClock);
        Assert.Equal(expectedAcquisition.Led4PulseClock, actualAcquisition.Led4PulseClock);
        Assert.Equal(expectedAcquisition.MotorIntervalNs, actualAcquisition.MotorIntervalNs);
        Assert.Equal(expectedAcquisition.Led1ChannelColor, actualAcquisition.Led1ChannelColor);
        Assert.Equal(expectedAcquisition.Led2ChannelColor, actualAcquisition.Led2ChannelColor);
        Assert.Equal(expectedAcquisition.Led3ChannelColor, actualAcquisition.Led3ChannelColor);
        Assert.Equal(expectedAcquisition.Led4ChannelColor, actualAcquisition.Led4ChannelColor);

        var expectedRecipe = Assert.IsType<ScanFilmScanRecipeSettings>(expected.ScanRecipeSettings);
        var actualRecipe = Assert.IsType<ScanFilmScanRecipeSettings>(actual.ScanRecipeSettings);
        var expectedAssignment = Assert.IsType<ScanChannelAssignment>(expectedRecipe.ChannelAssignment);
        var actualAssignment = Assert.IsType<ScanChannelAssignment>(actualRecipe.ChannelAssignment);
        Assert.Equal(expectedAssignment.Roles, actualAssignment.Roles);
        Assert.Equal(expectedAssignment.ReversedFlags, actualAssignment.ReversedFlags);

        var expectedColor = Assert.IsType<ScanColorManagementOptions>(expectedRecipe.ColorManagement);
        var actualColor = Assert.IsType<ScanColorManagementOptions>(actualRecipe.ColorManagement);
        Assert.Equal(expectedColor.IsEnabled, actualColor.IsEnabled);
        Assert.Equal(expectedColor.RedWavelengthNm, actualColor.RedWavelengthNm);
        Assert.Equal(expectedColor.GreenWavelengthNm, actualColor.GreenWavelengthNm);
        Assert.Equal(expectedColor.BlueWavelengthNm, actualColor.BlueWavelengthNm);
        Assert.Equal(expectedColor.OutputGamma, actualColor.OutputGamma);
        Assert.Equal(expectedColor.TargetWhitePointMode, actualColor.TargetWhitePointMode);
        Assert.Equal(expectedColor.ManualWhitePointColorTemperatureK, actualColor.ManualWhitePointColorTemperatureK);
        Assert.Equal(expectedRecipe.AlignmentMode, actualRecipe.AlignmentMode);
        Assert.Equal(expectedRecipe.DngExportMode, actualRecipe.DngExportMode);
    }
}
