using PRISM_Utility.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class FilmProfileValidationTextPresenterTests
{
    [Theory]
    [InlineData("channelProfiles", "FilmProfile_Field_Channels", "Channel configuration")]
    [InlineData("ChannelProfiles.Red.Parameters", "FilmProfile_Field_Channels", "Channel configuration")]
    [InlineData("AcquisitionSettings.Rows", "FilmProfile_Field_Rows", "Scan rows")]
    [InlineData("scanRecipeSettings.colorManagement", "FilmProfile_Field_Color", "Color management")]
    public void GetFieldDisplayName_KnownPathsUseUserFacingNames(string path, string resourceKey, string fallback)
    {
        var displayName = FilmProfileValidationTextPresenter.GetFieldDisplayName(path);

        Assert.Equal(resourceKey.GetLocalizedOrFallback(fallback), displayName);
        Assert.NotEqual(path, displayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("futureSchema.privateInternalSetting")]
    public void GetFieldDisplayName_UnknownOrMissingPathHasReadableFallback(string? path)
    {
        var displayName = FilmProfileValidationTextPresenter.GetFieldDisplayName(path);

        Assert.Equal("FilmProfile_Field_Configuration".GetLocalizedOrFallback("Film configuration"), displayName);
        Assert.False(string.IsNullOrWhiteSpace(displayName));
        Assert.DoesNotContain("privateInternalSetting", displayName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "bad", "FilmProfile.Validation.ExposureInputInvalid", "μs", null)]
    [InlineData(1, "-256", "FilmProfile.Validation.AdcOffsetInputInvalid", "−255", "ADC1")]
    [InlineData(2, "64", "FilmProfile.Validation.AdcGainInputInvalid", "0–63", "ADC1")]
    [InlineData(3, "256", "FilmProfile.Validation.AdcOffsetInputInvalid", "+255", "ADC2")]
    [InlineData(4, "64", "FilmProfile.Validation.AdcGainInputInvalid", "0–63", "ADC2")]
    [InlineData(5, "29", "FilmProfile.Validation.SystemClockInputInvalid", "30–200 MHz", null)]
    public void ChannelParameterInputIssue_UsesActualServiceFailureToNameFieldAndAllowedRange(
        int inputIndex, string invalidInput, string messageKey, string expectedRange, string? expectedAdc)
    {
        var inputs = new[] { "1000", "0", "1", "0", "1", "48" };
        inputs[inputIndex] = invalidInput;
        var service = new ScanParameterService(null!);
        Assert.False(service.TryParseInput(inputs[0], inputs[1], inputs[2], inputs[3], inputs[4], inputs[5], out _, out var error));

        var issue = FilmProfileValidationTextPresenter.CreateChannelParameterInputIssue(error, "Red");
        var message = FilmProfileValidationTextPresenter.GetValidationIssueText(issue);

        Assert.Equal(ScanFilmProfileValidationCode.InvalidChannelParameters, issue.Code);
        Assert.Equal("ChannelProfiles.Selected.Parameters", issue.FieldPath);
        Assert.Equal(messageKey, issue.MessageKey);
        Assert.Contains(expectedRange, message);
        if (inputIndex != 5)
            Assert.Contains("Red", message);
        if (expectedAdc is not null)
        {
            Assert.Equal(expectedAdc, issue.MessageArguments[1]);
            Assert.Contains(expectedAdc, message);
        }
        Assert.NotNull(ScanFilmProfileIssueNavigation.Resolve(issue, ScanFilmProfileIssueSource.CurrentDraft, ["Red"]));
    }

    [Fact]
    public void ChannelParameterInputIssue_UnknownServiceFailureKeepsReadableSafeFallback()
    {
        var issue = FilmProfileValidationTextPresenter.CreateChannelParameterInputIssue("future internal parser detail", "Red");

        Assert.Equal("FilmProfile.Validation.ChannelParametersInvalid", issue.MessageKey);
        Assert.Empty(issue.MessageArguments);
        Assert.False(string.IsNullOrWhiteSpace(FilmProfileValidationTextPresenter.GetValidationIssueText(issue)));
        Assert.DoesNotContain("future internal", FilmProfileValidationTextPresenter.GetValidationIssueText(issue));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("29", false)]
    [InlineData("52.0001", false)]
    [InlineData("unfinished", false)]
    public void SystemClockInputIssue_DistinguishesMissingInputWithoutRelaxingValidation(string clockInput, bool pending)
    {
        var service = new ScanParameterService(null!);
        Assert.False(service.TryParseInput("1000", "0", "1", "0", "1", clockInput, out _, out var error));

        var issue = FilmProfileValidationTextPresenter.CreateChannelParameterInputIssue(
            error, "Red", isSystemClockUnset: string.IsNullOrWhiteSpace(clockInput));

        Assert.Equal(pending, FilmProfileValidationTextPresenter.IsPendingInput(issue));
        Assert.True(FilmProfileValidationTextPresenter.IsSystemClockInputIssue(issue));
        Assert.Equal(ScanFilmProfileValidationCode.InvalidChannelParameters, issue.Code);
        Assert.Equal(ScanFilmProfileValidationSeverity.Error, issue.Severity);
        Assert.False(new ScanFilmProfileValidationResult([issue]).IsValid);
        var navigation = ScanFilmProfileIssueNavigation.Resolve(issue, ScanFilmProfileIssueSource.CurrentDraft, ["Red"]);
        Assert.NotNull(navigation);
        Assert.Equal(ScanFilmProfileIssueEditorTarget.ChannelParameters, navigation.EditorTarget);
        Assert.Contains("30–200 MHz", FilmProfileValidationTextPresenter.GetValidationIssueText(issue));
    }

    [Fact]
    public void SystemClockInputIssue_UnsetContextDoesNotReclassifyAnotherParameterFailure()
    {
        var service = new ScanParameterService(null!);
        Assert.False(service.TryParseInput("unfinished", "0", "1", "0", "1", "52", out _, out var error));

        var issue = FilmProfileValidationTextPresenter.CreateChannelParameterInputIssue(error, "Red", isSystemClockUnset: true);

        Assert.False(FilmProfileValidationTextPresenter.IsPendingInput(issue));
        Assert.False(FilmProfileValidationTextPresenter.IsSystemClockInputIssue(issue));
        Assert.Equal("FilmProfile.Validation.ExposureInputInvalid", issue.MessageKey);
        Assert.Equal(ScanFilmProfileValidationSeverity.Error, issue.Severity);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("29", false)]
    public void ChannelParameterInputs_AllBlankFieldsAreListedOnceWithIndependentNavigation(string clock, bool clockPending)
    {
        var inputs = new Dictionary<string, string>
        {
            ["SysClockMhz"] = clock, ["ExposureMicroseconds"] = "",
            ["Adc1Offset"] = "", ["Adc1Gain"] = "", ["Adc2Offset"] = "", ["Adc2Gain"] = ""
        };
        var service = new ScanParameterService(null!);
        Assert.False(service.TryParseInput("", "", "", "", "", clock, out _, out var error));

        var issues = FilmProfileValidationTextPresenter.CreateChannelParameterInputIssues(error, "Red", inputs, clockPending, "");

        Assert.Equal(6, issues.Count);
        Assert.Equal(6, issues.Select(issue => issue.FieldPath).Distinct().Count());
        Assert.Equal(clockPending, FilmProfileValidationTextPresenter.IsPendingInput(
            Assert.Single(issues, FilmProfileValidationTextPresenter.IsSystemClockInputIssue)));
        foreach (var issue in issues)
        {
            Assert.Equal(ScanFilmProfileValidationSeverity.Error, issue.Severity);
            var navigation = Assert.IsType<ScanFilmProfileIssueNavigationRequest>(
                ScanFilmProfileIssueNavigation.Resolve(issue, ScanFilmProfileIssueSource.CurrentDraft, ["Red"]));
            Assert.Equal(issue.FieldPath, navigation.FieldPath);
            Assert.Equal(ScanFilmProfileIssueEditorTarget.ChannelParameters, navigation.EditorTarget);
        }
        Assert.False(new ScanFilmProfileValidationResult(issues).IsValid);
    }

    [Fact]
    public void ExposureIssue_UsesTheCurrentMicrosecondRangeWithoutRawHardwareTicks()
    {
        var service = new ScanParameterService(null!);
        Assert.False(service.TryParseInput("1", "0", "1", "0", "1", "52", out _, out var error));

        var issue = FilmProfileValidationTextPresenter.CreateChannelParameterInputIssue(error, "Red",
            exposureInputRangeText: "881.289–8443.019 μs");
        var text = FilmProfileValidationTextPresenter.GetValidationIssueText(issue);

        Assert.Contains("881.289–8443.019 μs", text);
        Assert.DoesNotContain("tick", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(FilmProfileValidationTextPresenter.IsPendingInput(issue));
    }
}
