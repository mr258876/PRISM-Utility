using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class DeviceConfigurationInputValidationTests
{
    public DeviceConfigurationInputValidationTests()
        => AppContext.SetSwitch("PRISM.Utility.UseResourceFallbacks", true);

    [Theory]
    [InlineData("-1")]
    [InlineData("2147483647")]
    [InlineData("abc")]
    public void InvalidDngStartStaysVisibleAndDoesNotReplaceSavedGeometry(string input)
    {
        var geometry = new RecordingGeometrySettings();
        var editor = new DeviceConfigurationViewModel(new RecordingDeviceSettings(), geometry);
        var previous = geometry.Settings;

        editor.DngActiveStart = input;

        Assert.Equal(input, editor.DngActiveStart);
        Assert.True(editor.HasDngGeometryValidationError);
        Assert.Contains("Active start column", editor.DngGeometryValidationMessage);
        Assert.Same(previous, geometry.Settings);
        Assert.Equal(0, geometry.SaveCount);

        editor.DngActiveStart = (previous.ActiveRange.Start + 1).ToString();

        Assert.False(editor.HasDngGeometryValidationError);
        Assert.Equal(previous.ActiveRange.Start + 1, geometry.Settings.ActiveRange.Start);
        Assert.Equal(1, geometry.SaveCount);
    }

    [Fact]
    public void ReversedActiveRangeIsReportedWithoutNormalizingTheInput()
    {
        var geometry = new RecordingGeometrySettings();
        var editor = new DeviceConfigurationViewModel(new RecordingDeviceSettings(), geometry);
        var input = (geometry.Settings.ActiveRange.Start - 1).ToString();

        editor.DngActiveEndInclusive = input;

        Assert.Equal(input, editor.DngActiveEndInclusive);
        Assert.Contains("at least the start", editor.DngGeometryValidationMessage);
        Assert.Equal(0, geometry.SaveCount);
    }

    [Theory]
    [InlineData("-1-10")]
    [InlineData("10-1")]
    [InlineData("0-x")]
    [InlineData("0-1,2-3,4-5,6-7,8-9")]
    public void MalformedMaskedRangesStayVisibleAndDoNotSave(string input)
    {
        var geometry = new RecordingGeometrySettings();
        var editor = new DeviceConfigurationViewModel(new RecordingDeviceSettings(), geometry);

        editor.DngMaskedRanges = input;

        Assert.Equal(input, editor.DngMaskedRanges);
        Assert.True(editor.HasDngGeometryValidationError);
        Assert.Equal(0, geometry.SaveCount);
    }

    [Fact]
    public void MaskedRangeInsideActiveColumnsDoesNotTriggerTheServiceDefaultFallback()
    {
        var geometry = new RecordingGeometrySettings();
        var editor = new DeviceConfigurationViewModel(new RecordingDeviceSettings(), geometry);
        var previous = geometry.Settings;
        var input = $"{previous.ActiveRange.Start}-{previous.ActiveRange.Start + 3}";

        editor.DngMaskedRanges = input;

        Assert.Equal(input, editor.DngMaskedRanges);
        Assert.Contains("outside the active", editor.DngGeometryValidationMessage);
        Assert.Same(previous, geometry.Settings);
        Assert.Equal(0, geometry.SaveCount);
    }

    [Fact]
    public void InvalidMotorInputsHaveLocalMessagesAndRemainUnsavedUntilCorrected()
    {
        var devices = new RecordingDeviceSettings();
        var editor = new DeviceConfigurationViewModel(devices, new RecordingGeometrySettings());
        var previous = devices.Settings;

        editor.Motor1StepsPerRevolution = "0";
        editor.Motor2Microsteps = "-1";
        editor.Motor3LeadLengthMm = "NaN";

        Assert.True(editor.HasMotor1ValidationError);
        Assert.True(editor.HasMotor2ValidationError);
        Assert.True(editor.HasMotor3ValidationError);
        Assert.Contains("Steps per revolution", editor.Motor1ValidationMessage);
        Assert.Contains("Microsteps", editor.Motor2ValidationMessage);
        Assert.Contains("Lead length", editor.Motor3ValidationMessage);
        Assert.Equal("0", editor.Motor1StepsPerRevolution);
        Assert.Equal("-1", editor.Motor2Microsteps);
        Assert.Equal("NaN", editor.Motor3LeadLengthMm);
        Assert.Same(previous, devices.Settings);
        Assert.Equal(0, devices.SaveCount);

        editor.Motor1StepsPerRevolution = "400";
        editor.Motor2Microsteps = "32";
        editor.Motor3LeadLengthMm = "4.5";

        Assert.False(editor.HasMotor1ValidationError);
        Assert.False(editor.HasMotor2ValidationError);
        Assert.False(editor.HasMotor3ValidationError);
        Assert.Equal((uint)400, devices.Settings.Motor1!.StepsPerRevolution);
        Assert.Equal((uint)32, devices.Settings.Motor2!.Microsteps);
        Assert.Equal(4.5, devices.Settings.Motor3!.LeadLengthMm);
        Assert.Equal(1, devices.SaveCount);
    }

    [Fact]
    public void ValidMechanicalEditKeepsMotorAssignmentsThatThisPageDoesNotEdit()
    {
        var settings = ScanDeviceSettings.CreateDefault() with
        {
            FocusMotorMapping = new ScanFocusMotorMapping(1, 2, true, false),
            DefaultScanMotorId = 0
        };
        var devices = new RecordingDeviceSettings(settings);
        var editor = new DeviceConfigurationViewModel(devices, new RecordingGeometrySettings());

        editor.Motor1StepsPerRevolution = "400";

        Assert.Equal(settings.FocusMotorMapping, devices.Settings.FocusMotorMapping);
        Assert.Equal(settings.DefaultScanMotorId, devices.Settings.DefaultScanMotorId);
        Assert.Equal((uint)400, devices.Settings.Motor1!.StepsPerRevolution);
        Assert.Equal(1, devices.SaveCount);
    }

    [Fact]
    public void FailedAutomaticSaveKeepsInputAndReportsTheFailure()
    {
        var geometry = new RecordingGeometrySettings { SaveFailure = new IOException("disk unavailable") };
        var editor = new DeviceConfigurationViewModel(new RecordingDeviceSettings(), geometry);
        var input = (geometry.Settings.ActiveRange.Start + 1).ToString();

        editor.DngActiveStart = input;

        Assert.Equal(input, editor.DngActiveStart);
        Assert.True(editor.HasDngGeometryValidationError);
        Assert.Contains("disk unavailable", editor.DngGeometryValidationMessage);
    }

    private sealed class RecordingDeviceSettings(ScanDeviceSettings? initial = null) : IScanDeviceSettingsService
    {
        public ScanDeviceSettings DefaultSettings => ScanDeviceSettings.CreateDefault();
        public ScanDeviceSettings Settings { get; private set; } = (initial ?? ScanDeviceSettings.CreateDefault()).Normalize();
        public int SaveCount { get; private set; }
        public Task InitializeAsync() => Task.CompletedTask;
        public Task SetSettingsAsync(ScanDeviceSettings settings)
        {
            Settings = settings.Normalize();
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingGeometrySettings : IScanDngGeometrySettingsService
    {
        public ScanDngGeometrySettings DefaultSettings => ScanDngGeometrySettings.CreateDefault();
        public ScanDngGeometrySettings Settings { get; private set; } = ScanDngGeometrySettings.CreateDefault();
        public int SaveCount { get; private set; }
        public Exception? SaveFailure { get; init; }
        public Task InitializeAsync() => Task.CompletedTask;
        public Task SetSettingsAsync(ScanDngGeometrySettings settings)
        {
            if (SaveFailure is not null)
                return Task.FromException(SaveFailure);
            var normalized = settings.Normalize();
            Settings = normalized.MaskedBlackRanges.Length == 0 ? DefaultSettings : normalized;
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
