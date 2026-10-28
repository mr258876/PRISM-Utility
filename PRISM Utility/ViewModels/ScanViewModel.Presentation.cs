using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;

public partial class ScanViewModel
{
    private string _runtimeFeedbackText = string.Empty;
    private string _outputFeedbackText = string.Empty;
    private string _lastOutputPath = string.Empty;
    private string _lastOutputSuccessText = string.Empty;
    private bool _lastOutputIsFile;
    private int _scanPresentationVersion;
    private InfoBarSeverity _runtimeFeedbackSeverity = InfoBarSeverity.Informational;
    private InfoBarSeverity _outputFeedbackSeverity = InfoBarSeverity.Informational;
    private LoadedScanSettings? _loadedSettingsBaseline;
    private bool _isRestoringLoadedSettings;

    // Terminal scan feedback survives device refreshes and output operations.
    // Running progress has its own surface in the same status strip.
    public bool HasRuntimeFeedback => !IsRunning && !IsTopRiskBannerVisible && !string.IsNullOrEmpty(_runtimeFeedbackText);
    public string RuntimeFeedbackText => _runtimeFeedbackText;
    public InfoBarSeverity RuntimeFeedbackSeverity => _runtimeFeedbackSeverity;
    public bool HasOutputFeedback => !string.IsNullOrEmpty(_outputFeedbackText)
        && !(IsTopRiskBannerVisible && string.Equals(TopRiskBannerText, _outputFeedbackText, StringComparison.Ordinal));
    public string OutputFeedbackText => _outputFeedbackText;
    public InfoBarSeverity OutputFeedbackSeverity => _outputFeedbackSeverity;
    public string LastOutputPath => _lastOutputPath;
    public bool HasOutputLocation => !string.IsNullOrEmpty(_lastOutputPath);
    public bool IsOfflineGuidanceVisible => !IsConnected && !IsConnecting && !IsRunning;
    public string OfflineGuidanceText => "Scan_OfflineGuidance".GetLocalizedOrFallback(
        "You can load a configuration and adjust this scan offline. Connect the scanner when you are ready to capture.");

    public bool HasSessionChanges => GetSessionChangeLabels().Count > 0;
    public string SessionChangesText => _loadedSettingsBaseline is null
        ? "Scan_SessionSettingsNoBaseline".GetLocalizedOrFallback("Load a configuration to track changes for this scan.")
        : GetSessionChangeLabels() is { Count: > 0 } changes
            ? "Scan_SessionSettingsChanged".GetLocalizedFormatOrFallback(
                "Adjusted for this scan: {0}. The saved configuration is unchanged.", string.Join(" · ", changes))
            : "Scan_SessionSettingsUnchanged".GetLocalizedOrFallback("Using the settings loaded with this configuration.");

    public string SessionChangesCompactText => GetSessionChangeLabels() is { Count: > 0 } changes
        ? "Scan_SessionSettingsChangedCompact".GetLocalizedFormatOrFallback("Adjusted: {0}", string.Join(" · ", changes))
        : string.Empty;

    public string ExecutionPrimarySummaryText => "Scan_ExecutionPrimarySummary".GetLocalizedFormatOrFallback(
        "{0} rows · {1} passes · {2}", string.IsNullOrWhiteSpace(SelectedRows) ? "—" : SelectedRows,
        GetEffectiveDeviceChannelRoles().Count(ScanChannelRoleHelper.IsActiveRole), GetDirectionDisplayName(SelectedStartingDirection));

    public string ExecutionSecondarySummaryText => "Scan_ExecutionSecondarySummary".GetLocalizedFormatOrFallback(
        "{0} · {1}", string.IsNullOrWhiteSpace(MotorDistancePerLineValue)
            ? "Scan_ExecutionLineMoveUnset".GetLocalizedOrFallback("Line distance not set")
            : "Scan_ExecutionLineMoveCompact".GetLocalizedFormatOrFallback(
                "{0} {1}/line", MotorDistancePerLineValue, MotorDistancePerLineUnit),
        (IsAlternateMotorDirectionEnabled ? "Scan_ExecutionAlternateCompact" : "Scan_ExecutionSameDirectionCompact")
            .GetLocalizedOrFallback(IsAlternateMotorDirectionEnabled ? "Alternating" : "Same direction"));

    public string ExecutionRowsSummaryText => "Scan_ExecutionRowsSummary".GetLocalizedFormatOrFallback(
        "{0} rows", string.IsNullOrWhiteSpace(SelectedRows) ? "—" : SelectedRows);

    public string ExecutionChannelsSummaryText
    {
        get
        {
            var roles = GetEffectiveDeviceChannelRoles().Where(ScanChannelRoleHelper.IsActiveRole).ToArray();
            if (roles.Length == 0)
                return "Scan_ExecutionChannelsNone".GetLocalizedOrFallback("No channels selected");
            var summary = "Scan_ExecutionChannelsSummary".GetLocalizedFormatOrFallback(
                "{0} passes · {1}", roles.Length, string.Join(" → ", roles.Select(GetChannelRoleDisplayName)));
            return roles.Contains("White") ? summary + " · " + BuildWhiteSourceSummary(GetConfiguredWhiteLightSource()) : summary;
        }
    }

    public string ExecutionMotionSummaryText => "Scan_ExecutionMotionSummary".GetLocalizedFormatOrFallback(
        "{0} · {1} · {2}", GetDirectionDisplayName(SelectedStartingDirection),
        (IsAlternateMotorDirectionEnabled ? "Scan_ExecutionAlternate" : "Scan_ExecutionSameDirection")
            .GetLocalizedOrFallback(IsAlternateMotorDirectionEnabled ? "Alternate each pass" : "Same direction each pass"),
        string.IsNullOrWhiteSpace(MotorDistancePerLineValue)
            ? "Scan_ExecutionLineMoveUnset".GetLocalizedOrFallback("Line distance not set")
            : "Scan_ExecutionLineMoveSummary".GetLocalizedFormatOrFallback(
                "{0} {1} per line", MotorDistancePerLineValue, MotorDistancePerLineUnit));

    public string ExecutionPreviewSummaryText => !IsPreviewEnabled
        ? "Scan_Runtime_PreviewDisabledDescription".GetLocalizedOrFallback("Preview disabled.")
        : "Scan_ExecutionPreviewSummary".GetLocalizedFormatOrFallback(
            "{0} · {1}", GetPreviewModeDisplayName(SelectedPreviewMode), GetAlignmentModeDisplayName(SelectedAlignmentMode));

    private void RecordScanCompletion(string message, InfoBarSeverity severity)
    {
        _runtimeFeedbackText = message;
        _runtimeFeedbackSeverity = severity;
        NotifyRuntimeFeedbackChanged();
    }

    private void SetOutputFeedback(string message, InfoBarSeverity severity)
    {
        _outputFeedbackText = message;
        _outputFeedbackSeverity = severity;
        OnPropertyChanged(nameof(OutputFeedbackText));
        OnPropertyChanged(nameof(OutputFeedbackSeverity));
        OnPropertyChanged(nameof(HasOutputFeedback));
    }

    private void RecordOutputSuccess(string path, bool isFile, string message)
    {
        _lastOutputPath = path;
        _lastOutputIsFile = isFile;
        _lastOutputSuccessText = message;
        SetOutputFeedback(message, InfoBarSeverity.Success);
        NotifyOutputLocationChanged();
    }

    private void ResetScanPresentationForNewRun()
    {
        CurrentPassText = "Scan_Runtime_ProgressStagePreparing".GetLocalizedOrFallback("Preparing");
        CurrentLedText = string.Empty;
        CurrentDirectionText = string.Empty;
        _scanPresentationVersion++;
        _runtimeFeedbackText = string.Empty;
        _lastOutputPath = string.Empty;
        _lastOutputSuccessText = string.Empty;
        _lastOutputIsFile = false;
        SetOutputFeedback(string.Empty, InfoBarSeverity.Informational);
        NotifyRuntimeFeedbackChanged();
        NotifyOutputLocationChanged();
    }

    private bool CanOpenLastOutputFolder() => HasOutputLocation && !IsOutputOperationRunning;

    [RelayCommand(CanExecute = nameof(CanOpenLastOutputFolder))]
    private async Task OpenLastOutputFolder()
    {
        if (!CanOpenLastOutputFolder())
            return;
        var path = _lastOutputPath;
        var presentationVersion = _scanPresentationVersion;
        try
        {
            if (_outputLocationService is not null && await _outputLocationService.OpenAsync(path, _lastOutputIsFile))
            {
                if (presentationVersion == _scanPresentationVersion && string.Equals(path, _lastOutputPath, StringComparison.Ordinal))
                    SetOutputFeedback(_lastOutputSuccessText, InfoBarSeverity.Success);
                return;
            }
        }
        catch (Exception)
        {
            // Also contain failures from alternative host implementations.
        }
        if (presentationVersion != _scanPresentationVersion || !string.Equals(path, _lastOutputPath, StringComparison.Ordinal))
            return;
        SetOutputFeedback("Scan_OutputLocationUnavailable".GetLocalizedFormatOrFallback(
            "The saved location could not be opened. Check that it still exists and is accessible: {0}", path), InfoBarSeverity.Warning);
    }

    private void NotifyRuntimeFeedbackChanged()
    {
        OnPropertyChanged(nameof(HasRuntimeFeedback));
        OnPropertyChanged(nameof(RuntimeFeedbackText));
        OnPropertyChanged(nameof(RuntimeFeedbackSeverity));
    }

    private void NotifyOutputLocationChanged()
    {
        OnPropertyChanged(nameof(LastOutputPath));
        OnPropertyChanged(nameof(HasOutputLocation));
        OpenLastOutputFolderCommand.NotifyCanExecuteChanged();
    }

    private sealed record LoadedScanSettings(
        string Rows, string StartingDirection, bool AlternateDirection, bool WarmUp,
        string LineDistance, string LineDistanceUnit, string MotorInterval, bool DistanceDerivedFromInterval,
        bool PreviewEnabled, string PreviewMode, ScanChannelAlignmentMode AlignmentMode, ScanDngExportMode DngMode,
        bool ColorManagement, string Red, string Green, string Blue, string Gamma, string WhitePointMode, string WhitePointTemperature);

    private LoadedScanSettings CaptureCurrentScanSettings() => new(
        SelectedRows, SelectedStartingDirection, IsAlternateMotorDirectionEnabled, IsWarmUpEnabled,
        MotorDistancePerLineValue, MotorDistancePerLineUnit, MotorIntervalUs, _isMotorDistanceDerivedFromInterval,
        IsPreviewEnabled, SelectedPreviewMode, SelectedAlignmentMode, SelectedDngExportMode,
        IsColorManagementEnabled, RedWavelengthNm, GreenWavelengthNm, BlueWavelengthNm, OutputGamma,
        SelectedTargetWhitePointMode, ManualWhitePointColorTemperatureK);

    private void CaptureLoadedSettingsBaseline()
    {
        _loadedSettingsBaseline = CaptureCurrentScanSettings();
        NotifySessionSettingsChanged();
    }

    private IReadOnlyList<string> GetSessionChangeLabels()
    {
        if (_loadedSettingsBaseline is not { } baseline)
            return Array.Empty<string>();
        var current = CaptureCurrentScanSettings();
        var changes = new List<string>();
        if (current.Rows != baseline.Rows)
            changes.Add("Scan_ChangedRows".GetLocalizedOrFallback("Rows"));
        if (current.StartingDirection != baseline.StartingDirection || current.AlternateDirection != baseline.AlternateDirection
            || current.WarmUp != baseline.WarmUp)
            changes.Add("Scan_ChangedTransport".GetLocalizedOrFallback("Transport"));
        // A line distance derived from device timing can change on connection;
        // that projection is not a user override of the loaded interval.
        if (current.DistanceDerivedFromInterval != baseline.DistanceDerivedFromInterval
            || (current.DistanceDerivedFromInterval
                ? current.MotorInterval != baseline.MotorInterval
                : current.LineDistance != baseline.LineDistance || current.LineDistanceUnit != baseline.LineDistanceUnit))
            changes.Add("Scan_ChangedLineDistance".GetLocalizedOrFallback("Line distance"));
        if (current.PreviewEnabled != baseline.PreviewEnabled || current.PreviewMode != baseline.PreviewMode
            || current.AlignmentMode != baseline.AlignmentMode)
            changes.Add("Scan_ChangedPreview".GetLocalizedOrFallback("Preview and alignment"));
        if (current.ColorManagement != baseline.ColorManagement || current.Red != baseline.Red || current.Green != baseline.Green
            || current.Blue != baseline.Blue || current.Gamma != baseline.Gamma || current.WhitePointMode != baseline.WhitePointMode
            || current.WhitePointTemperature != baseline.WhitePointTemperature)
            changes.Add("Scan_ChangedColor".GetLocalizedOrFallback("Color"));
        if (current.DngMode != baseline.DngMode)
            changes.Add("Scan_ChangedOutput".GetLocalizedOrFallback("DNG output"));
        return changes;
    }

    private bool CanRestoreLoadedSettings() => !_isDisposed && _loadedSettingsBaseline is not null && HasSessionChanges
        && IsSetupEditingEnabled && !IsConnecting && !IsRunning && !IsOutputOperationRunning && !_isConfigProfileLoading;

    [RelayCommand(CanExecute = nameof(CanRestoreLoadedSettings))]
    private void RestoreLoadedSettings()
    {
        if (!CanRestoreLoadedSettings() || _loadedSettingsBaseline is not { } baseline)
            return;
        _isRestoringLoadedSettings = true;
        _isApplyingDerivedMotorDistance = true;
        try
        {
            SelectedRows = baseline.Rows;
            SelectedStartingDirection = baseline.StartingDirection;
            IsAlternateMotorDirectionEnabled = baseline.AlternateDirection;
            IsWarmUpEnabled = baseline.WarmUp;
            _isMotorDistanceDerivedFromInterval = false;
            MotorDistancePerLineUnit = baseline.LineDistanceUnit;
            MotorIntervalUs = baseline.MotorInterval;
            MotorDistancePerLineValue = baseline.LineDistance;
            _isMotorDistanceDerivedFromInterval = baseline.DistanceDerivedFromInterval;
            IsPreviewEnabled = baseline.PreviewEnabled;
            SelectedPreviewMode = baseline.PreviewMode;
            SelectedAlignmentMode = baseline.AlignmentMode;
            SelectedDngExportMode = baseline.DngMode;
            IsColorManagementEnabled = baseline.ColorManagement;
            RedWavelengthNm = baseline.Red;
            GreenWavelengthNm = baseline.Green;
            BlueWavelengthNm = baseline.Blue;
            OutputGamma = baseline.Gamma;
            SelectedTargetWhitePointMode = baseline.WhitePointMode;
            ManualWhitePointColorTemperatureK = baseline.WhitePointTemperature;
        }
        finally
        {
            _isApplyingDerivedMotorDistance = false;
            _isRestoringLoadedSettings = false;
        }
        RefreshDerivedMotorDistanceFromCurrentInterval();
        UpdatePassPlan();
        UpdateComputedMotorSummary();
        RefreshLoadedScanRecipeSummary();
        UpdateExecutionConfigSummary();
        UpdatePreviewState();
        NotifySessionSettingsChanged();
    }

    private void NotifySessionSettingsChanged()
    {
        if (_isRestoringLoadedSettings)
            return;
        OnPropertyChanged(nameof(HasSessionChanges));
        OnPropertyChanged(nameof(SessionChangesText));
        OnPropertyChanged(nameof(SessionChangesCompactText));
        OnPropertyChanged(nameof(ExecutionPrimarySummaryText));
        OnPropertyChanged(nameof(ExecutionSecondarySummaryText));
        OnPropertyChanged(nameof(ExecutionRowsSummaryText));
        OnPropertyChanged(nameof(ExecutionChannelsSummaryText));
        OnPropertyChanged(nameof(ExecutionMotionSummaryText));
        OnPropertyChanged(nameof(ExecutionPreviewSummaryText));
        RestoreLoadedSettingsCommand.NotifyCanExecuteChanged();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_isRestoringLoadedSettings)
            return;
        if (e.PropertyName is nameof(SelectedRows) or nameof(SelectedStartingDirection) or nameof(IsAlternateMotorDirectionEnabled)
            or nameof(IsWarmUpEnabled) or nameof(MotorDistancePerLineValue) or nameof(MotorDistancePerLineUnit) or nameof(MotorIntervalUs)
            or nameof(IsPreviewEnabled) or nameof(SelectedPreviewMode) or nameof(SelectedAlignmentMode) or nameof(SelectedDngExportMode)
            or nameof(IsColorManagementEnabled) or nameof(RedWavelengthNm) or nameof(GreenWavelengthNm) or nameof(BlueWavelengthNm)
            or nameof(OutputGamma) or nameof(SelectedTargetWhitePointMode) or nameof(ManualWhitePointColorTemperatureK)
            or nameof(ChannelMappingSummaryText) or nameof(IsConfigProfileLoaded) or nameof(IsSetupEditingEnabled))
            NotifySessionSettingsChanged();
        if (e.PropertyName is nameof(IsRunning) or nameof(IsConnecting) or nameof(IsConnected) or nameof(IsOutputOperationRunning))
        {
            NotifyRuntimeFeedbackChanged();
            OnPropertyChanged(nameof(IsOfflineGuidanceVisible));
            RestoreLoadedSettingsCommand.NotifyCanExecuteChanged();
            OpenLastOutputFolderCommand.NotifyCanExecuteChanged();
        }
        if (e.PropertyName is nameof(IsTopRiskBannerVisible) or nameof(TopRiskBannerText))
        {
            OnPropertyChanged(nameof(HasRuntimeFeedback));
            OnPropertyChanged(nameof(HasOutputFeedback));
        }
    }
}
