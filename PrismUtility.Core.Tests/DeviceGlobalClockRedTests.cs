using System.Globalization;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Todo13DeviceGlobalClock")]
public sealed class DeviceGlobalClockRedTests
{
    [Fact]
    public void GivenLegacySnapshot_WhenConstructed_ThenSysClockKhzRemainsSourceMetadata()
    {
        var snapshot = new ScanParameterSnapshot(1_000, -10, 12, 8, 15, 48_000);

        Assert.Equal(48_000u, snapshot.SysClockKhz);
        Assert.Contains(nameof(ScanParameterSnapshot.SysClockKhz), typeof(ScanParameterSnapshot).GetProperties().Select(property => property.Name));
    }

    [Theory]
    [InlineData(30_000u)]
    [InlineData(125_000u)]
    [InlineData(200_000u)]
    public void GivenLegalClockKhz_WhenFormattedAsMhz_ThenTheTextRetainsExactKhzPrecision(uint sysClockKhz)
    {
        var text = (sysClockKhz / 1_000m).ToString("0.###", CultureInfo.InvariantCulture);

        Assert.Equal((decimal)sysClockKhz, decimal.Parse(text, CultureInfo.InvariantCulture) * 1_000m);
    }

    [Theory]
    [InlineData("30.000", "1727.567", 30_000u)]
    [InlineData("125", "414.616", 125_000u)]
    [InlineData("200.000", "259.135", 200_000u)]
    public void GivenLegalMhzAndMicroseconds_WhenParsed_ThenTheSnapshotUsesKHzAndDerivedTicks(string sysClockMhz, string exposureMicroseconds, uint expectedKhz)
    {
        var service = new ScanParameterService(new ScanProtocolService());

        var parsed = service.TryParseInput(exposureMicroseconds, "-10", "12", "8", "15", sysClockMhz, out var snapshot, out var error);

        Assert.True(parsed, error);
        Assert.Equal(1_000, snapshot.ExposureTicks);
        Assert.Equal(expectedKhz, snapshot.SysClockKhz);
    }

    [Fact]
    public void GivenClockOutsideTheLegalMhzRange_WhenParsed_ThenItIsRejected()
    {
        var service = new ScanParameterService(new ScanProtocolService());

        var parsed = service.TryParseInput("1000", "-10", "12", "8", "15", "200001", out _, out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData("29.999")]
    [InlineData("200.001")]
    [InlineData("125.0005")]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void GivenInvalidMhzText_WhenParsed_ThenItIsRejectedWithoutRounding(string sysClockMhz)
    {
        var service = new ScanParameterService(new ScanProtocolService());

        var parsed = service.TryParseInput("1000", "-10", "12", "8", "15", sysClockMhz, out _, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void GivenExposureTicksAndClock_WhenProjected_ThenThePhysicalMicrosecondsFollowTheFirmwareFormula()
    {
        const ushort exposureTicks = 1_000;
        const uint sysClockKhz = 125_000;

        var microseconds = ScanTimingMath.ExposureTicksToMicroseconds(exposureTicks, sysClockKhz);

        Assert.Equal((45_827d + (exposureTicks * 6d)) * 1_000d / sysClockKhz, microseconds, 9);
    }

    [Fact]
    public void GivenRepresentablePhysicalExposure_WhenRoundTripped_ThenTicksUseDeterministicAwayFromZeroRounding()
    {
        const ushort exposureTicks = 1_000;
        const uint sysClockKhz = 125_000;
        var microseconds = ScanTimingMath.ExposureTicksToMicroseconds(exposureTicks, sysClockKhz);

        var roundTrippedTicks = ScanTimingMath.NanosecondsToExposureTicks(microseconds * 1_000d, sysClockKhz);

        Assert.Equal(exposureTicks, roundTrippedTicks);
    }

    [Fact]
    public void GivenExposureBelowUshortRange_WhenConverted_ThenTheInputIsRejectedInsteadOfClamped()
    {
        const uint sysClockKhz = 125_000;
        var belowMinimumNanoseconds = (ScanTimingMath.ExposureTicksToMicroseconds(ScanDebugConstants.MinExposureTicks, sysClockKhz) * 1_000d) - 1d;

        Assert.Throws<ArgumentOutOfRangeException>(() => ScanTimingMath.NanosecondsToExposureTicks(belowMinimumNanoseconds, sysClockKhz));
    }

    [Fact]
    public void GivenExposureAboveUshortRange_WhenConverted_ThenTheInputIsRejectedInsteadOfClamped()
    {
        const uint sysClockKhz = 125_000;
        var aboveMaximumNanoseconds = (ScanTimingMath.ExposureTicksToMicroseconds(ushort.MaxValue, sysClockKhz) * 1_000d) + 1d;

        Assert.Throws<ArgumentOutOfRangeException>(() => ScanTimingMath.NanosecondsToExposureTicks(aboveMaximumNanoseconds, sysClockKhz));
    }

    [Fact]
    public void GivenParameterServiceContract_WhenApplyingClockAndChannelValues_ThenClockOwnershipIsExplicit()
    {
        var contract = ReadCoreSource("Contracts", "Services", "IScanParameterService.cs");
        var service = ReadCoreSource("Services", "ScanParameterService.cs");
        var applyStart = service.IndexOf("public async Task ApplyAsync", StringComparison.Ordinal);
        var applyEnd = service.IndexOf("private async Task SetParameterAsync", applyStart, StringComparison.Ordinal);
        var applyBody = service[applyStart..applyEnd];
        var setU32Start = service.IndexOf("private async Task SetParameterAsync(IScanSessionService session, ScanParameterDefinition parameter, uint value", StringComparison.Ordinal);
        var setU32Body = service[setU32Start..];

        Assert.Contains("bool TryParseInput(string exposureMicroseconds, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockMhz", contract, StringComparison.Ordinal);
        Assert.Contains("ScanParameterDisplays BuildDisplays(string exposureMicroseconds, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockMhz", contract, StringComparison.Ordinal);
        Assert.Contains("Task ApplyGlobalClockAsync(IScanSessionService session, uint sysClockKhz, CancellationToken ct);", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("SysClockKhzParameter", applyBody, StringComparison.Ordinal);
        Assert.Equal(5, applyBody.Split("await SetParameterAsync", StringSplitOptions.None).Length - 1);
        Assert.Contains("await SetParameterAsync(session, SysClockKhzParameter, sysClockKhz, ct);", service, StringComparison.Ordinal);
        Assert.Contains("ParseU32ParamPayload", service, StringComparison.Ordinal);
        Assert.Contains("response.Status != 0x00", setU32Body, StringComparison.Ordinal);
        Assert.Contains("if (echoed != value)", setU32Body, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTimingStateModel_WhenClockOrApplyStateChanges_ThenItIsImmutableAndReadRequiredIsRepresentable()
    {
        var path = Path.Combine(HostSoftwareRoot(), "PRISM Utility.Core", "Models", "ScanDeviceTimingModels.cs");

        Assert.True(File.Exists(path), "Expected the Core-owned immutable ScanDeviceTimingModels contract.");
        var source = File.ReadAllText(path);
        Assert.Contains("enum ScanDeviceClockStateKind", source, StringComparison.Ordinal);
        Assert.Contains("Unknown", source, StringComparison.Ordinal);
        Assert.Contains("DeviceKnown", source, StringComparison.Ordinal);
        Assert.Contains("Edited", source, StringComparison.Ordinal);
        Assert.Contains("ReadRequired", source, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<string> RevalidationRequiredChannelRoles", source, StringComparison.Ordinal);
        Assert.Contains("RequiresDeviceRead", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenViewModelTimingInputs_WhenTheyChange_ThenGlobalClockExposureAndReadRequiredTransitionsAreHandled()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");

        Assert.Contains("partial void OnSysClockMhzChanged(string value)", source, StringComparison.Ordinal);
        Assert.Contains("partial void OnExposureMicrosecondsChanged(string value)", source, StringComparison.Ordinal);
        Assert.Contains("ScanDeviceClockStateKind.ReadRequired", source, StringComparison.Ordinal);
        Assert.Contains("RequiresDeviceRead", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenWorkflowWithLegacyProfiles_WhenTransportPlanIsBuilt_ThenRequestGlobalClockRebindsEveryPass()
    {
        var source = ReadCoreSource("Services", "ScanWorkflowTransportPlan.cs");

        Assert.Contains("request.SysClockKhz", source, StringComparison.Ordinal);
        Assert.DoesNotContain("request.PassParameterProfiles[passIndex].SysClockKhz", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTimingUi_WhenRendered_ThenMhzAndMicrosecondsArePrimaryAndTheFormulaIsDocumented()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");
        var englishResources = ReadAppSource("Strings", "en-us", "Resources.resw");
        var chineseResources = ReadAppSource("Strings", "zh-CN", "Resources.resw");

        Assert.Contains("Text=\"{x:Bind ViewModel.SysClockMhz, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.ExposureMicroseconds, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.ExposureTicks, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsReadOnly=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_ExposureFormulaNote\"", xaml, StringComparison.Ordinal);
        Assert.Contains("name=\"ScanDebug_ExposureFormulaNote.Text\"", englishResources, StringComparison.Ordinal);
        Assert.Contains("name=\"ScanDebug_ExposureFormulaNote.Text\"", chineseResources, StringComparison.Ordinal);
        Assert.Contains("微秒输入；tick 只读。", chineseResources, StringComparison.Ordinal);
        Assert.DoesNotContain("全局时钟计算", chineseResources, StringComparison.Ordinal);
        Assert.DoesNotContain("仅作高级只读值", chineseResources, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenDynamicExposureDisplay_WhenProjected_ThenCorePayloadIsDataNeutralAndViewModelLocalizesTheLabel()
    {
        var service = new ScanParameterService(new ScanProtocolService());
        var display = service.BuildDisplays("1000", "-10", "12", "8", "15", "125").ExposureTimeDisplay;
        var serviceSource = ReadCoreSource("Services", "ScanParameterService.cs");
        var viewModelSource = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var englishResources = ReadAppSource("Strings", "en-us", "Resources.resw");
        var chineseResources = ReadAppSource("Strings", "zh-CN", "Resources.resw");

        Assert.Contains("ns", display, StringComparison.Ordinal);
        Assert.Contains("us", display, StringComparison.Ordinal);
        Assert.Contains("1/", display, StringComparison.Ordinal);
        Assert.DoesNotContain("Exposure time", display, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exposure time:", serviceSource, StringComparison.Ordinal);
        Assert.Contains("ExposureTimeDisplay = LocalizeExposureTimeDisplay(displays.ExposureTimeDisplay);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("private static string LocalizeExposureTimeDisplay(string exposurePayload)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_Runtime_ExposureTimeFormat", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("name=\"ScanDebug_Runtime_ExposureTimeFormat\"", englishResources, StringComparison.Ordinal);
        Assert.Contains("<value>Exposure time: {0}</value>", englishResources, StringComparison.Ordinal);
        Assert.Contains("name=\"ScanDebug_Runtime_ExposureTimeFormat\"", chineseResources, StringComparison.Ordinal);
        Assert.Contains("<value>曝光时间：{0}</value>", chineseResources, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenDynamicChannelClockDisplay_WhenProjected_ThenCorePayloadIsDataNeutralAndViewModelLocalizesTheLabel()
    {
        var service = new ScanParameterService(new ScanProtocolService());
        var display = service.BuildDisplays("1000", "-10", "12", "8", "15", "125").SysClockMhzDisplay;
        var idleDisplay = service.BuildDisplays("1000", "-10", "12", "8", "15", "not-a-clock").SysClockMhzDisplay;
        var serviceSource = ReadCoreSource("Services", "ScanParameterService.cs");
        var viewModelSource = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var englishResources = ReadAppSource("Strings", "en-us", "Resources.resw");
        var chineseResources = ReadAppSource("Strings", "zh-CN", "Resources.resw");

        Assert.Equal("125 MHz", display);
        Assert.Equal("-", idleDisplay);
        Assert.DoesNotContain("System clock", display, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System clock:", serviceSource, StringComparison.Ordinal);
        Assert.Contains("SysClockMhzDisplay = LocalizeSysClockMhzDisplay(displays.SysClockMhzDisplay);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("private static string LocalizeSysClockMhzDisplay(string sysClockPayload)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_Runtime_SystemClockFormat", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("name=\"ScanDebug_Runtime_SystemClockFormat\"", englishResources, StringComparison.Ordinal);
        Assert.Contains("<value>System clock: {0}</value>", englishResources, StringComparison.Ordinal);
        Assert.Contains("name=\"ScanDebug_Runtime_SystemClockFormat\"", chineseResources, StringComparison.Ordinal);
        Assert.Contains("<value>系统时钟：{0}</value>", chineseResources, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenDynamicOffsetAndGainDisplays_WhenProjected_ThenCorePayloadsContainOnlyValuesAndUnits()
    {
        var service = new ScanParameterService(new ScanProtocolService());
        var displays = service.BuildDisplays("1000", "-10", "12", "8", "15", "125");
        var invalidDisplays = service.BuildDisplays("1000", "bad-offset", "bad-gain", "bad-offset", "bad-gain", "125");

        Assert.Equal("-11.719 mV", displays.Adc1OffsetMvDisplay);
        Assert.Equal("+9.375 mV", displays.Adc2OffsetMvDisplay);
        Assert.Equal("1.189 V/V", displays.Adc1GainVvDisplay);
        Assert.Equal("1.248 V/V", displays.Adc2GainVvDisplay);
        Assert.Equal("-", invalidDisplays.Adc1OffsetMvDisplay);
        Assert.Equal("-", invalidDisplays.Adc1GainVvDisplay);
        Assert.DoesNotContain("Offset", displays.Adc1OffsetMvDisplay, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Gain", displays.Adc1GainVvDisplay, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GivenDynamicOffsetAndGainDisplays_WhenLocalized_ThenViewModelOwnsEnglishAndChineseLabels()
    {
        const string offsetPayload = "-11.719 mV";
        const string gainPayload = "1.189 V/V";
        var viewModelSource = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");
        var serviceSource = ReadCoreSource("Services", "ScanParameterService.cs");
        var englishResources = FilmProfileContractSource.ReadResources("en-us");
        var chineseResources = FilmProfileContractSource.ReadResources("zh-CN");

        Assert.DoesNotContain("Offset amplitude:", serviceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Gain:", serviceSource, StringComparison.Ordinal);
        Assert.Contains("Adc1OffsetMvDisplay = LocalizeOffsetAmplitudeDisplay(displays.Adc1OffsetMvDisplay);", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Adc1GainVvDisplay = LocalizeGainDisplay(displays.Adc1GainVvDisplay);", viewModelSource, StringComparison.Ordinal);
        Assert.Equal("Offset amplitude: -11.719 mV", string.Format(CultureInfo.GetCultureInfo("en-US"), englishResources["ScanDebug_Runtime_OffsetAmplitudeFormat"], offsetPayload));
        Assert.Equal("偏移：-11.719 mV", string.Format(CultureInfo.GetCultureInfo("zh-CN"), chineseResources["ScanDebug_Runtime_OffsetAmplitudeFormat"], offsetPayload));
        Assert.Equal("Gain: 1.189 V/V", string.Format(CultureInfo.GetCultureInfo("en-US"), englishResources["ScanDebug_Runtime_GainFormat"], gainPayload));
        Assert.Equal("增益：1.189 V/V", string.Format(CultureInfo.GetCultureInfo("zh-CN"), chineseResources["ScanDebug_Runtime_GainFormat"], gainPayload));
        Assert.Equal(
            FilmProfileContractSource.GetPlaceholderIndexes(englishResources["ScanDebug_Runtime_OffsetAmplitudeFormat"]),
            FilmProfileContractSource.GetPlaceholderIndexes(chineseResources["ScanDebug_Runtime_OffsetAmplitudeFormat"]));
        Assert.Equal(
            FilmProfileContractSource.GetPlaceholderIndexes(englishResources["ScanDebug_Runtime_GainFormat"]),
            FilmProfileContractSource.GetPlaceholderIndexes(chineseResources["ScanDebug_Runtime_GainFormat"]));
    }

    [Fact]
    public void GivenPersistentDeviceSettings_WhenTimingOwnershipMovesToRuntime_ThenSettingsRemainMotorAndChannelRoleOnly()
    {
        var propertyNames = typeof(ScanDeviceSettings).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain(propertyNames, propertyName => propertyName.Contains("Clock", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, propertyName => propertyName.Contains("Exposure", StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadCoreSource(params string[] pathParts)
        => File.ReadAllText(Path.Combine([HostSoftwareRoot(), "PRISM Utility.Core", .. pathParts]));

    private static string ReadAppSource(params string[] pathParts)
        => File.ReadAllText(Path.Combine([HostSoftwareRoot(), "PRISM Utility", .. pathParts]));

    private static string HostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the Host Software source root.");
    }
}
