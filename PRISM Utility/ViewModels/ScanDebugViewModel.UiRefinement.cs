using System.Globalization;
using Microsoft.UI.Xaml;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;

public partial class ScanDebugViewModel
{
    public string AcquisitionCalibrationSummaryText
    {
        get
        {
            var status = GetSharedAcquisitionCalibrationStatus();
            return status is null
                ? string.Empty
                : "ScanDebug_AcquisitionCalibrationSummaryFormat".GetLocalizedFormatOrFallback(
                    "{0} selected channels: {1}", AcquisitionChannels.Count(channel => channel.IsSelected), status);
        }
    }

    public Visibility AcquisitionCalibrationSummaryVisibility => GetSharedAcquisitionCalibrationStatus() is null
        ? Visibility.Collapsed : Visibility.Visible;

    public Visibility CalibrationGuideActionVisibility => CalibrationGuideStep == ScanDebugCalibrationGuideStep.ConnectDevice
        ? Visibility.Collapsed : Visibility.Visible;

    private string? GetSharedAcquisitionCalibrationStatus()
    {
        var selectedChannels = GetSelectedAcquisitionChannelsForSummary();
        if (selectedChannels.Length < 2)
            return null;

        var status = selectedChannels[0].CalibrationStatusText;
        return selectedChannels.All(channel => string.Equals(channel.CalibrationStatusText, status, StringComparison.Ordinal))
            ? status : null;
    }

    private void NotifyAcquisitionCalibrationPresentation()
    {
        OnPropertyChanged(nameof(AcquisitionCalibrationSummaryText));
        OnPropertyChanged(nameof(AcquisitionCalibrationSummaryVisibility));
        foreach (var channel in AcquisitionChannels)
            channel.RefreshStatus();
    }

    private static IReadOnlyList<ScanFilmProfileValidationIssue> OrderParameterInputsForDisplay(IReadOnlyList<ScanFilmProfileValidationIssue> issues)
    {
        static int InputOrder(ScanFilmProfileValidationIssue issue) => issue.MessageKey switch
        {
            "FilmProfile.Validation.SystemClockInputMissing" or "FilmProfile.Validation.SystemClockInputInvalid" => 0,
            "FilmProfile.Validation.ExposureInputMissing" or "FilmProfile.Validation.ExposureInputInvalid" => 1,
            "FilmProfile.Validation.AdcOffsetInputMissing" or "FilmProfile.Validation.AdcOffsetInputInvalid" => issue.MessageArguments.ElementAtOrDefault(1) == "ADC2" ? 4 : 2,
            "FilmProfile.Validation.AdcGainInputMissing" or "FilmProfile.Validation.AdcGainInputInvalid" => issue.MessageArguments.ElementAtOrDefault(1) == "ADC2" ? 5 : 3,
            _ => -1
        };

        // Reorder only the parameter slots, leaving unrelated issues in their original positions.
        var inputs = issues.Where(issue => InputOrder(issue) >= 0).OrderBy(InputOrder).ToArray();
        var index = 0;
        return issues.Select(issue => InputOrder(issue) >= 0 ? inputs[index++] : issue).ToArray();
    }

    // An unfinished field is a neutral editing state. Validation still protects
    // saving a complete configuration and issuing device commands.
    public string ClockInputErrorText => string.IsNullOrWhiteSpace(SysClockMhz) || TryParseSysClockMhzText(SysClockMhz, out _)
        ? string.Empty
        : "ScanDebug_ClockInputFormatHint".GetLocalizedOrFallback("Enter 30–200 MHz with at most three decimal places.");

    public Visibility ClockRangeHintVisibility => string.IsNullOrEmpty(ClockInputErrorText) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ExposureInputRangeVisibility => TryParseSysClockMhzText(SysClockMhz, out _)
        ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ExposureClockEntryVisibility => TryParseSysClockMhzText(SysClockMhz, out _)
        ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ExposureTimeDisplayVisibility => TryParseTimingProjectionInputs(ExposureMicroseconds, SysClockMhz, out _, out _)
        ? Visibility.Visible : Visibility.Collapsed;

    public string ChannelInputFeedbackText => string.Join(Environment.NewLine, CurrentFilmProfileValidationIssues
        .Where(issue => issue.Code == ScanFilmProfileValidationCode.InvalidChannelParameters
            && !FilmProfileValidationTextPresenter.IsPendingInput(issue)
            && !FilmProfileValidationTextPresenter.IsSystemClockInputIssue(issue))
        .Where(issue => ScanFilmProfileIssueNavigation.Resolve(issue, ScanFilmProfileIssueSource.CurrentDraft, CalibrationChannelOptions) is { } request
            && (request.ChannelRole is null || string.Equals(request.ChannelRole, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase)))
        .Select(issue => issue.MessageKey == "FilmProfile.Validation.ExposureInputInvalid"
            ? "ScanDebug_ExposureInputFormatHint".GetLocalizedOrFallback("Enter an exposure in μs within the allowed range shown for this clock.")
            : FilmProfileValidationTextPresenter.GetValidationIssueText(issue)));

    public string CompactClockStateText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SysClockMhz))
                return "ScanDebug_ClockNotSet".GetLocalizedOrFallback("Clock not entered");
            if (!TryParseSysClockMhzText(SysClockMhz, out var clockKhz))
                return "ScanDebug_ClockInputInvalidState".GetLocalizedOrFallback("Clock input is invalid");

            var clock = (clockKhz / 1000m).ToString("0.###", CultureInfo.InvariantCulture);
            if (!IsConnected)
                return "ScanDebug_ClockPendingOffline".GetLocalizedFormatOrFallback("{0} MHz · Not applied (device offline)", clock);
            if (RequiresDeviceRead)
                return "ScanDebug_ClockNeedsConfirmation".GetLocalizedFormatOrFallback("{0} MHz · Read device to confirm", clock);
            return _deviceTimingState.StateKind == ScanDeviceClockStateKind.DeviceKnown && _confirmedDeviceClockKhz == clockKhz
                ? "ScanDebug_ClockApplied".GetLocalizedFormatOrFallback("{0} MHz · Applied", clock)
                : "ScanDebug_ClockPendingApply".GetLocalizedFormatOrFallback("{0} MHz · Not applied", clock);
        }
    }

    public void NotifyCompactClockPresentation()
    {
        OnPropertyChanged(nameof(ClockInputErrorText));
        OnPropertyChanged(nameof(ClockRangeHintVisibility));
        OnPropertyChanged(nameof(ChannelInputFeedbackText));
        OnPropertyChanged(nameof(CompactClockStateText));
    }
}
