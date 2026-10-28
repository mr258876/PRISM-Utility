using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;
using Microsoft.UI.Xaml;

namespace PRISM_Utility.ViewModels;

public partial class ScanDebugViewModel
{
    private sealed record ObservationCopy(ScanObservationSnapshot Data, CaptureEvidence? Evidence,
        string RawStatus, string RawSource, string RawStatistics, string RawContext,
        string RowInput, string StartInput, string EndInput, string? RawRole, bool IsWaterfall);

    private ObservationCopy? _frozenObservation;
    private ObservationCopy? _baselineObservation;
    private bool _isShowingBaseline;
    private string _observationFeedback = string.Empty;
    private string _observationStartInput = "0";
    private string _observationEndInput = (ScanDebugConstants.DecodedPixelsPerLine - 1).ToString(CultureInfo.InvariantCulture);
    private bool _isObservationSelectionEnabled;
    private bool _isObservationInverted;
    private bool _wasObservationSnapshotVisible;

    public bool IsObservationInverted
    {
        get => _isObservationInverted;
        set => SetProperty(ref _isObservationInverted, value);
    }

    public string ObservationStartInput
    {
        get => VisibleObservationCopy?.StartInput ?? _observationStartInput;
        set { if (IsObservationLive && SetProperty(ref _observationStartInput, value)) RefreshObservationSelection(); }
    }

    public string ObservationEndInput
    {
        get => VisibleObservationCopy?.EndInput ?? _observationEndInput;
        set { if (IsObservationLive && SetProperty(ref _observationEndInput, value)) RefreshObservationSelection(); }
    }

    public string ObservationRawRowInput
    {
        get => VisibleObservationCopy?.RowInput ?? RawSignalRowInput;
        set { if (IsObservationLive) RawSignalRowInput = value; }
    }

    public bool IsObservationSelectionEnabled
    {
        get => _isObservationSelectionEnabled;
        set
        {
            if (!SetProperty(ref _isObservationSelectionEnabled, value))
                return;
            if (value)
            {
                IsRoiEditModeEnabled = false;
                IsColumnSampleEditModeEnabled = false;
            }
        }
    }

    public bool CanEditObservationFromPreview => IsObservationLive && PreviewFrame is not null && IsPreviewEnabled
        && !IsWaterfallEnabled && (_lastWorkflowResult is null && _streamingWorkflowPreviewResult is null
            || _lastWorkflowResult is not null && GetSingleActiveRoleIndex(_lastWorkflowChannelAssignment
                ?? BuildCapturedWorkflowChannelAssignment(_lastWorkflowResult)) >= 0);

    public bool CanMutateObservationFromPreview => CanEditObservationFromPreview && IsObservationSelectionEnabled;
    public string ObservationSelectionStatusText => VisibleObservationCopy is { } copy
        ? "ScanDebug_ObservationSelectionSnapshot".GetLocalizedFormat(copy.StartInput, copy.EndInput)
        : TryGetObservationRange(_imageDecoder.GetDecodedPixelsPerLine(), out var range)
        ? "ScanDebug_ObservationSelectionSummary".GetLocalizedFormat(range.Start, range.EndInclusive)
        : "ScanDebug_ObservationSelectionInvalid".GetLocalized();

    public bool TryGetObservationRange(int width, out ScanColumnRange range)
    {
        range = new ScanColumnRange(0, 0);
        if (!int.TryParse(_observationStartInput, NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(_observationEndInput, NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            || start < 0 || end < start || end >= width)
            return false;
        range = new ScanColumnRange(start, end);
        return true;
    }

    public void UpdateObservationRange(int start, int endInclusive, int width)
    {
        if (!CanEditObservationFromPreview || start < 0 || endInclusive < 0 || start >= width || endInclusive >= width)
            return;
        SetProperty(ref _observationStartInput, Math.Min(start, endInclusive).ToString(CultureInfo.InvariantCulture), nameof(ObservationStartInput));
        SetProperty(ref _observationEndInput, Math.Max(start, endInclusive).ToString(CultureInfo.InvariantCulture), nameof(ObservationEndInput));
        RefreshObservationSelection();
    }

    private void RefreshObservationSelection()
    {
        RefreshRawRoiIfChanged();
        OnPropertyChanged(nameof(ObservationSelectionStatusText));
        UseObservationRangeForCalibrationCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUseObservationRangeForCalibration))]
    private void UseObservationRangeForCalibration()
    {
        var width = _imageDecoder.GetDecodedPixelsPerLine();
        if (!IsObservationLive || !TryGetObservationRange(width, out var range))
            return;
        if (!TryBuildRoiEditCandidate(range, width, out _, out var issue))
        {
            _observationFeedback = FormatRoiEditIssue(issue);
            OnPropertyChanged(nameof(ObservationFeedbackText));
            OnPropertyChanged(nameof(ObservationFeedbackVisibility));
            OnPropertyChanged(nameof(ObservationNoticeVisibility));
            return;
        }
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ApplySelectedRoiInputs, out var runtimeClaim))
            return;
        using (runtimeClaim)
        {
            // Fill the owner-specific numeric draft. Its existing Apply command remains the commit boundary.
            if (IsAdcRoiSelection(SelectedRoiSelection))
                SelectedAdcRoiSelection = SelectedRoiSelection;
            else
                SelectedFocusRoiSelection = SelectedRoiSelection;
            SetRoiNumericDraft(SelectedRoiSelection, range.Start.ToString(CultureInfo.InvariantCulture), isStart: true);
            SetRoiNumericDraft(SelectedRoiSelection, range.EndInclusive.ToString(CultureInfo.InvariantCulture), isStart: false);
            _observationFeedback = "ScanDebug_ObservationSelectionCopied".GetLocalizedFormat(GetRoiSelectionDisplayName(SelectedRoiSelection));
            OnPropertyChanged(nameof(ObservationFeedbackText));
            OnPropertyChanged(nameof(ObservationFeedbackVisibility));
            OnPropertyChanged(nameof(ObservationNoticeVisibility));
        }
    }

    private bool CanUseObservationRangeForCalibration() => IsObservationLive && CanEditProfileDraft() && IsRoiNumericChannelReady()
        && TryGetObservationRange(_imageDecoder.GetDecodedPixelsPerLine(), out _);

    private ObservationCopy? VisibleObservationCopy => _isShowingBaseline ? _baselineObservation : _frozenObservation;
    private CaptureEvidence? VisibleObservationEvidence => VisibleObservationCopy is { } copy ? copy.Evidence : _displayedCaptureEvidence;
    public bool IsObservationSnapshotVisible => VisibleObservationCopy is not null;
    public bool IsObservationLive => !IsObservationSnapshotVisible;
    public bool CanEditObservationGamma => IsObservationLive && IsGammaCorrectionEnabled;
    public Visibility ObservationLiveToolsVisibility => IsObservationLive ? Visibility.Visible : Visibility.Collapsed;
    public bool HasObservationBaseline => _baselineObservation is not null;
    public bool HasObservationSample => ObservationFrame is { Width: > 0, Height: > 0 };
    public Visibility ObservationSummaryVisibility => HasObservationSample ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ObservationEmptyVisibility => HasObservationSample ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ObservationRawSourceVisibility => HasObservationSample && !string.IsNullOrWhiteSpace(ObservationRawSourceText)
        ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ObservationEvidenceSourceVisibility => HasObservationSample && VisibleObservationEvidence is not null
        ? Visibility.Visible : Visibility.Collapsed;
    public string ObservationEvidenceStatusText
    {
        get
        {
            if (!HasObservationSample)
                return string.Empty;
            if (VisibleObservationEvidence is not { } capture)
                return "ScanDebug_ObservationEvidenceUnconfirmed".GetLocalized();
            if (capture.DeviceParameterCommandGeneration != _deviceParameterCommandGeneration)
                return "ScanDebug_ObservationEvidenceParametersChanged".GetLocalized();
            if (!IsConnected || capture.SessionGeneration != _calibrationDeviceSessionGeneration
                || !string.Equals(capture.DeviceId, _calibrationDeviceSessionId, StringComparison.Ordinal))
                return "ScanDebug_ObservationEvidenceHistorical".GetLocalized();
            if (!IsCompositeObservation(capture)
                && !string.Equals(capture.Role, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
                return "ScanDebug_ObservationEvidenceOtherChannel".GetLocalized();
            return "ScanDebug_ObservationEvidenceRecorded".GetLocalized();
        }
    }
    public string ObservationEvidenceHintText => !HasObservationSample ? string.Empty
        : !IsConnected ? "ScanDebug_ObservationEvidenceConnectNext".GetLocalized()
        : IsObservationSnapshotVisible ? "ScanDebug_ObservationEvidenceResumeNext".GetLocalized()
        : IsAcquisitionRunning ? "ScanDebug_ObservationEvidenceAcquiring".GetLocalized()
        : ObservationWarningVisibility == Visibility.Visible ? "ScanDebug_ObservationEvidenceSampleNext".GetLocalized()
        : "ScanDebug_ObservationEvidenceComparisonPending".GetLocalized();
    public Visibility ObservationWarningVisibility => HasObservationSample && (VisibleObservationEvidence is not { } capture
        || capture.DeviceParameterCommandGeneration != _deviceParameterCommandGeneration
        || !IsConnected || capture.SessionGeneration != _calibrationDeviceSessionGeneration
        || !string.Equals(capture.DeviceId, _calibrationDeviceSessionId, StringComparison.Ordinal)
        || !IsCompositeObservation(capture) && !string.Equals(capture.Role, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
        ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ObservationFeedbackVisibility => string.IsNullOrWhiteSpace(_observationFeedback) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ObservationNoticeVisibility => ObservationWarningVisibility == Visibility.Visible || ObservationFeedbackVisibility == Visibility.Visible
        ? Visibility.Visible : Visibility.Collapsed;
    public ScanPreviewFrame? ObservationFrame => VisibleObservationCopy?.Data.Frame ?? PreviewFrame;
    public ScanRawSignalResult? ObservationRawSignalResult => VisibleObservationCopy is { } copy ? copy.Data.Signal : RawSignalResult;
    public string ObservationRawStatusText => VisibleObservationCopy?.RawStatus ?? RawSignalStatusText;
    public string ObservationRawSourceText => VisibleObservationCopy?.RawSource ?? RawSignalSourceText;
    public string ObservationRawStatisticsText => VisibleObservationCopy?.RawStatistics ?? RawSignalStatisticsText;
    public string ObservationRawContextText => VisibleObservationCopy?.RawContext ?? (RawSignalResult is { } raw
        ? "ScanDebug_ObservationRawContext".GetLocalizedFormat(_rawRole ?? "ScanDebug_CaptureUnknown".GetLocalized(),
            raw.ProfileRow, raw.Columns.Start, raw.Columns.EndInclusive)
        : string.Empty);
    public string ObservationFeedbackText => _observationFeedback;
    public string ObservationModeText => (_isShowingBaseline ? "ScanDebug_ObservationBaseline"
        : _frozenObservation is not null ? "ScanDebug_ObservationFrozen"
        : !HasObservationSample ? "ScanDebug_ObservationEmpty"
        : IsAcquisitionRunning && IsPreviewEnabledForCurrentRows ? "ScanDebug_ObservationUpdating" : "ScanDebug_ObservationLatest").GetLocalized();
    public string ObservationFreezeActionText => (_frozenObservation is null ? "ScanDebug_FreezeObservation"
        : "ScanDebug_ResumeObservation").GetLocalized();
    public string ObservationBaselineActionText => (_isShowingBaseline ? "ScanDebug_ShowCurrentObservation"
        : "ScanDebug_ShowBaselineObservation").GetLocalized();

    public string ObservationSourceSummaryText => !HasObservationSample ? string.Empty : VisibleObservationEvidence is { } capture
        ? "ScanDebug_ObservationSourceSummary".GetLocalizedFormat(capture.Id.ToString("N")[..8],
            IsCompositeObservation(capture) ? "ScanDebug_ObservationCompositeRole".GetLocalizedFormat(capture.Role) : capture.Role,
            capture.LedChannelIndex?.ToString() ?? "ScanDebug_CaptureUnknown".GetLocalized(), capture.CompletedRows,
            (capture.CompletedAtUtc ?? capture.RequestedAtUtc)?.ToLocalTime().ToString("HH:mm:ss")
                ?? "ScanDebug_CaptureUnknown".GetLocalized())
        : "ScanDebug_ObservationSourceUnconfirmed".GetLocalized();

    public string ObservationFullSourceText => BuildCaptureSourceText(VisibleObservationEvidence);
    private static bool IsCompositeObservation(CaptureEvidence capture)
        => capture.IsComposite;
    public string ObservationComparisonText
    {
        get
        {
            if (VisibleObservationEvidence is not { } capture)
                return HasObservationSample ? "ScanDebug_ObservationSourceUnknown".GetLocalized() : string.Empty;
            if (capture.DeviceParameterCommandGeneration != _deviceParameterCommandGeneration)
                return "ScanDebug_ObservationParametersChanged".GetLocalized();
            if (IsCompositeObservation(capture))
                return !IsConnected || capture.SessionGeneration != _calibrationDeviceSessionGeneration
                    || !string.Equals(capture.DeviceId, _calibrationDeviceSessionId, StringComparison.Ordinal)
                    ? "ScanDebug_CaptureHistorical".GetLocalized()
                    : "ScanDebug_ObservationCompositeComparison".GetLocalized();
            if (!string.Equals(capture.Role, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
                return "ScanDebug_ObservationChannelMismatch".GetLocalizedFormat(capture.Role, SelectedCalibrationChannel);
            if (!IsConnected || capture.SessionGeneration != _calibrationDeviceSessionGeneration
                || !string.Equals(capture.DeviceId, _calibrationDeviceSessionId, StringComparison.Ordinal))
                return "ScanDebug_CaptureHistorical".GetLocalized();
            return !capture.HasSubmittedParameters || _deviceTimingState.StateKind != ScanDeviceClockStateKind.DeviceKnown
                ? "ScanDebug_CaptureComparisonUnknown".GetLocalized()
                : "ScanDebug_CaptureComparisonSubmittedNotReadback".GetLocalized();
        }
    }

    public string ObservationBaselineSummaryText => _baselineObservation is not { } baseline
        ? string.Empty
        : "ScanDebug_ObservationBaselineSummary".GetLocalizedFormat(
            baseline.Evidence?.Id.ToString("N")[..8] ?? "ScanDebug_CaptureUnknown".GetLocalized(),
            baseline.Evidence?.Role ?? "ScanDebug_CaptureUnknown".GetLocalized(),
            baseline.Data.Signal?.Mean.ToString("F2") ?? "ScanDebug_CaptureUnknown".GetLocalized(),
            baseline.RawRole ?? "ScanDebug_CaptureUnknown".GetLocalized());

    [RelayCommand(CanExecute = nameof(CanFreezeObservation))]
    private void ToggleObservationFreeze()
    {
        if (_frozenObservation is not null)
        {
            _frozenObservation = null;
            _isShowingBaseline = false;
        }
        else
        {
            // Freeze the current display, including A if it is selected; no acquisition command is sent.
            if (!TryCopyObservation(out var copy))
                return;
            _frozenObservation = copy;
            _isShowingBaseline = false;
        }
        _observationFeedback = string.Empty;
        NotifyObservationChanged();
    }

    private bool CanFreezeObservation() => _frozenObservation is not null || ObservationFrame is not null;

    [RelayCommand(CanExecute = nameof(CanSetObservationBaseline))]
    private void SetObservationBaseline()
    {
        if (!TryCopyObservation(out var copy))
            return;
        _baselineObservation = copy;
        _observationFeedback = string.Empty;
        NotifyObservationChanged();
    }

    private bool CanSetObservationBaseline() => ObservationFrame is not null;

    [RelayCommand(CanExecute = nameof(HasObservationBaseline))]
    private void ToggleObservationBaseline()
    {
        _isShowingBaseline = !_isShowingBaseline;
        _observationFeedback = string.Empty;
        NotifyObservationChanged();
    }

    [RelayCommand(CanExecute = nameof(HasObservationBaseline))]
    private void ClearObservationBaseline()
    {
        _isShowingBaseline = false;
        _baselineObservation = null;
        NotifyObservationChanged();
    }

    private bool TryCopyObservation(out ObservationCopy? copy)
    {
        copy = null;
        // RAW analysis can lag image presentation. Attach it only when its capture matches the image evidence.
        var signal = IsObservationSnapshotVisible || _rawCaptureId == _displayedCaptureEvidence?.Id
            ? ObservationRawSignalResult : null;
        if (!ScanObservationSnapshot.TryCreate(ObservationFrame, signal, out var data))
        {
            _observationFeedback = "ScanDebug_ObservationCopyUnavailable".GetLocalized();
            OnPropertyChanged(nameof(ObservationFeedbackText));
            OnPropertyChanged(nameof(ObservationFeedbackVisibility));
            OnPropertyChanged(nameof(ObservationNoticeVisibility));
            return false;
        }
        copy = new ObservationCopy(data!, VisibleObservationEvidence,
            signal is null ? "ScanDebug_RawSignalUnavailableSource".GetLocalized() : ObservationRawStatusText,
            signal is null ? string.Empty : ObservationRawSourceText,
            signal is null ? string.Empty : ObservationRawStatisticsText,
            signal is null ? string.Empty : ObservationRawContextText,
            ObservationRawRowInput, ObservationStartInput, ObservationEndInput,
            signal is null ? null : VisibleObservationCopy?.RawRole ?? _rawRole,
            VisibleObservationCopy?.IsWaterfall ?? IsWaterfallEnabled);
        return true;
    }

    private void NotifyObservationChanged()
    {
        if (!CanEditObservationFromPreview)
            IsObservationSelectionEnabled = false;
        // Live frames do not change the display mode. Avoid cancelling a pan on every frame publication.
        if (_wasObservationSnapshotVisible != IsObservationSnapshotVisible)
        {
            _wasObservationSnapshotVisible = IsObservationSnapshotVisible;
            OnPropertyChanged(nameof(IsObservationSnapshotVisible));
        }
        foreach (var property in new[] { nameof(ObservationFrame), nameof(ObservationRawSignalResult),
            nameof(ObservationRawStatusText), nameof(ObservationRawSourceText), nameof(ObservationRawStatisticsText),
            nameof(ObservationRawContextText),
            nameof(ObservationRawRowInput), nameof(ObservationStartInput), nameof(ObservationEndInput),
            nameof(ObservationSelectionStatusText), nameof(ObservationFooterText), nameof(ObservationFooterVisibility),
            nameof(ObservationSourceSummaryText), nameof(ObservationFullSourceText), nameof(ObservationComparisonText),
            nameof(ObservationModeText), nameof(ObservationFreezeActionText), nameof(ObservationBaselineActionText),
            nameof(HasObservationSample), nameof(ObservationSummaryVisibility), nameof(ObservationWarningVisibility),
            nameof(ObservationEmptyVisibility), nameof(ObservationRawSourceVisibility),
            nameof(ObservationEvidenceSourceVisibility), nameof(ObservationEvidenceStatusText), nameof(ObservationEvidenceHintText),
            nameof(ObservationFeedbackVisibility),
            nameof(ObservationNoticeVisibility),
            nameof(ObservationBaselineSummaryText), nameof(ObservationFeedbackText), nameof(HasObservationBaseline),
            nameof(IsObservationLive), nameof(CanEditObservationGamma), nameof(CanEditRoiSelection),
            nameof(ObservationLiveToolsVisibility),
            nameof(CanEditColumnSampleSelection), nameof(CanMutateRoiFromPreview), nameof(CanMutateColumnSampleFromPreview),
            nameof(CanEditObservationFromPreview), nameof(CanMutateObservationFromPreview) })
            OnPropertyChanged(property);
        ToggleObservationFreezeCommand.NotifyCanExecuteChanged();
        SetObservationBaselineCommand.NotifyCanExecuteChanged();
        ToggleObservationBaselineCommand.NotifyCanExecuteChanged();
        ClearObservationBaselineCommand.NotifyCanExecuteChanged();
        UseObservationRangeForCalibrationCommand.NotifyCanExecuteChanged();
        NotifyColumnSampleAvailabilityChanged();
        NotifyManualReferenceAvailabilityChanged();
    }

    private void ClearObservationCopies()
    {
        _frozenObservation = null;
        _baselineObservation = null;
        _isShowingBaseline = false;
        _observationFeedback = string.Empty;
        NotifyObservationChanged();
    }
}
