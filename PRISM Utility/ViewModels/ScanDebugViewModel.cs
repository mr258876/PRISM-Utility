using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.Models;
using Windows.UI;

namespace PRISM_Utility.ViewModels;

public sealed class ScanCalibrationPromptRequest
{
    public ScanCalibrationPromptRequest(ScanCalibrationPrompt prompt)
    {
        Prompt = prompt;
        CompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public ScanCalibrationPrompt Prompt
    {
        get;
    }

    public TaskCompletionSource<bool> CompletionSource
    {
        get;
    }
}

public sealed class ScanFilmProfileDiscardConfirmationRequest
{
    public ScanFilmProfileDiscardConfirmationRequest(
        string titleResourceKey = "ScanDebug_Runtime_FilmProfileDirtyConfirmationTitle",
        string messageResourceKey = "ScanDebug_Runtime_FilmProfileDirtyConfirmationMessage",
        string primaryButtonResourceKey = "ScanDebug_Runtime_FilmProfileDirtyConfirmationDiscardButton",
        string closeButtonResourceKey = "ScanDebug_Runtime_FilmProfileDirtyConfirmationStayButton")
    {
        TitleResourceKey = titleResourceKey;
        MessageResourceKey = messageResourceKey;
        PrimaryButtonResourceKey = primaryButtonResourceKey;
        CloseButtonResourceKey = closeButtonResourceKey;
    }

    public string TitleResourceKey { get; }

    public string MessageResourceKey { get; }

    public string PrimaryButtonResourceKey { get; }

    public string CloseButtonResourceKey { get; }

    public TaskCompletionSource<bool> CompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class ScanNoticeRequest
{
    public ScanNoticeRequest(string title, string content, string closeButtonText)
    {
        Title = title;
        Content = content;
        CloseButtonText = closeButtonText;
        CompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public string Title { get; }

    public string Content { get; }

    public string CloseButtonText { get; }

    public TaskCompletionSource CompletionSource { get; }
}

public sealed class ScanRoiValidationIssueDisplay
{
    public required ScanRoiValidationIssue Issue { get; init; }

    public required string Message { get; init; }

    public required string TechnicalPath { get; init; }

    public required string AutomationId { get; init; }

    public required string AutomationName { get; init; }
}

public sealed class ScanFilmProfileValidationIssueDisplay
{
    public required ScanFilmProfileIssueSource Source { get; init; }

    public required ScanFilmProfileValidationIssue Issue { get; init; }

    public required string FieldPath { get; init; }

    public required string Message { get; init; }

    public required string AutomationId { get; init; }

    public required string AutomationName { get; init; }

    public required bool CanNavigate { get; init; }
}

internal sealed record CalibrationChannelSelectionLoad(string Role, int LoadVersion, int ProjectionVersion, Task<bool> Completion);

internal sealed record CurrentFilmProfileIssueNavigationContext(
    ScanFilmProfileDraft CurrentDraft,
    CalibrationChannelSelectionLoad? SelectionLoad);

public enum CalibrationChannelStatusKind
{
    Saved,
    Modified,
    Invalid,
    Missing,
    CopiedUnverified
}

public sealed class ScanDebugCalibrationChannelItemViewModel : ObservableObject
{
    private readonly ScanDebugViewModel _owner;
    private readonly string _role;

    public ScanDebugCalibrationChannelItemViewModel(ScanDebugViewModel owner, string role)
    {
        _owner = owner;
        _role = role;
    }

    public string Role => _role;

    public string DisplayName => ScanDebugViewModel.GetCalibrationChannelDisplayName(_role);

    public CalibrationChannelStatusKind StatusKind => _owner.GetCalibrationChannelStatusKind(_role);

    public string StatusText => _owner.GetCalibrationChannelStatusText(_role);

    public string StatusIconGlyph => StatusKind switch
    {
        CalibrationChannelStatusKind.Saved => "\uE930",
        CalibrationChannelStatusKind.Invalid => "\uE7BA",
        CalibrationChannelStatusKind.Modified => "\uE70F",
        CalibrationChannelStatusKind.CopiedUnverified => "\uE8C8",
        CalibrationChannelStatusKind.Missing => "\uE946",
        _ => "\uE946"
    };

    public string LedMappingText => "ScanDebug_ChannelLedBindingItem".GetLocalizedFormat(DisplayName, _owner.GetBoundLedName(_role));

    public bool IsSelected => string.Equals(_role, _owner.SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase);

    public string SelectionText => IsSelected
        ? "ScanDebug_Runtime_ChannelStatusSelected".GetLocalized()
        : "ScanDebug_Runtime_ChannelStatusNotSelected".GetLocalized();

    public string AccessibilityText => "ScanDebug_Runtime_ChannelStatusAccessibility".GetLocalizedFormat(
        DisplayName,
        StatusText,
        LedMappingText,
        SelectionText);

    public void Refresh()
    {
        OnPropertyChanged(nameof(StatusKind));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusIconGlyph));
        OnPropertyChanged(nameof(LedMappingText));
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(AccessibilityText));
    }
}

public sealed class ScanDebugIlluminationChannelViewModel : ObservableObject
{
    private readonly ScanDebugViewModel _owner;
    private string _role;

    public ScanDebugIlluminationChannelViewModel(ScanDebugViewModel owner, int ledIndex, string role)
    {
        _owner = owner;
        LedIndex = ledIndex;
        _role = role;
    }

    public int LedIndex { get; }

    public int LedNumber => LedIndex + 1;

    public string Role => _role;

    public string DisplayName => $"{GetChannelRoleDisplayName(_role)} (LED{LedNumber})";

    public string LevelHeader => "ScanDebug_IlluminationLevelHeader".GetLocalized();

    public string LevelPlaceholder => "ScanDebug_IlluminationLevelPlaceholder".GetLocalized();

    public string PulseClockHeader => "ScanDebug_IlluminationPulseClockHeader".GetLocalized();

    public string PulseClockPlaceholder => "ScanDebug_IlluminationPulseClockPlaceholder".GetLocalized();

    public string SteadyLabel => "ScanDebug_IlluminationSteadyLabel".GetLocalized();

    public string SyncLabel => "ScanDebug_IlluminationSyncLabel".GetLocalized();

    public string WorkModeHeader => "ScanDebug_IlluminationWorkModeHeader".GetLocalized();

    public IReadOnlyList<string> WorkModeOptions => _owner.IlluminationWorkModeOptions;

    public string Level
    {
        get => _owner.GetIlluminationLevelInput(LedIndex);
        set
        {
            if (_owner.SetIlluminationLevelInput(LedIndex, value))
                OnPropertyChanged();
        }
    }

    public string PulseClock
    {
        get => _owner.GetIlluminationPulseClockInput(LedIndex);
        set
        {
            if (_owner.SetIlluminationPulseClockInput(LedIndex, value))
                OnPropertyChanged();
        }
    }

    public bool IsSteadyEnabled
    {
        get => _owner.GetIlluminationSteadyInput(LedIndex);
        set
        {
            if (_owner.SetIlluminationSteadyInput(LedIndex, value))
                OnPropertyChanged();
        }
    }

    public bool IsSyncEnabled
    {
        get => _owner.GetIlluminationSyncInput(LedIndex);
        set
        {
            if (_owner.SetIlluminationSyncInput(LedIndex, value))
                OnPropertyChanged();
        }
    }

    public string WorkMode
    {
        get => _owner.GetIlluminationWorkModeInput(LedIndex);
        set
        {
            if (_owner.SetIlluminationWorkModeInput(LedIndex, value))
            {
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSteadyEnabled));
                OnPropertyChanged(nameof(IsSyncEnabled));
            }
        }
    }

    public void UpdateRole(string role)
    {
        if (string.Equals(_role, role, StringComparison.Ordinal))
            return;

        _role = role;
        OnPropertyChanged(nameof(Role));
        OnPropertyChanged(nameof(DisplayName));
    }

    public void RefreshInputBindings()
    {
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(PulseClock));
        OnPropertyChanged(nameof(IsSteadyEnabled));
        OnPropertyChanged(nameof(IsSyncEnabled));
        OnPropertyChanged(nameof(WorkMode));
    }

    private static string GetChannelRoleDisplayName(string role)
        => role switch
        {
            "Red" => "Scan_Runtime_ChannelRoleRed".GetLocalized(),
            "Green" => "Scan_Runtime_ChannelRoleGreen".GetLocalized(),
            "Blue" => "Scan_Runtime_ChannelRoleBlue".GetLocalized(),
            "White" => "Scan_Runtime_ChannelRoleWhite".GetLocalized(),
            "IR" => "Scan_Runtime_ChannelRoleIr".GetLocalized(),
            _ => role
        };
}

public sealed class ScanDebugAcquisitionChannelViewModel : ObservableObject
{
    private readonly ScanDebugViewModel _owner;
    private string _role;
    private bool _isSelected;

    public ScanDebugAcquisitionChannelViewModel(ScanDebugViewModel owner, int ledIndex, string role, bool isSelected)
    {
        _owner = owner;
        LedIndex = ledIndex;
        _role = role;
        _isSelected = isSelected;
        CalibrateCommand = new RelayCommand(() => _owner.RequestCalibrationForAcquisitionChannel(Role));
    }

    public int LedIndex { get; }

    public int LedNumber => LedIndex + 1;

    public string Role => _role;

    public string DisplayName => ScanDebugViewModel.GetCalibrationChannelDisplayName(_role);

    public string ChannelLedBindingText => "ScanDebug_ChannelLedBindingItem".GetLocalizedFormat(DisplayName, $"LED{LedNumber}");

    public string CalibrationStatusText => _owner.GetAcquisitionChannelCalibrationStatusText(_role);

    public bool IsSelectionEditable => _owner.AreScanAcquisitionSettingsEditable;

    public IRelayCommand CalibrateCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
                _owner.SetAcquisitionChannelSelection(this, value);
        }
    }

    public void UpdateRole(string role)
    {
        if (string.Equals(_role, role, StringComparison.Ordinal))
            return;

        _role = role;
        OnPropertyChanged(nameof(Role));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(ChannelLedBindingText));
        OnPropertyChanged(nameof(CalibrationStatusText));
    }

    public void SetSelectedFromOwner(bool isSelected)
        => SetProperty(ref _isSelected, isSelected, nameof(IsSelected));

    public void RefreshStatus()
        => OnPropertyChanged(nameof(CalibrationStatusText));

    public void RefreshSelectionEditability()
        => OnPropertyChanged(nameof(IsSelectionEditable));
}

public partial class ScanDebugViewModel : ObservableRecipient
{
    private static readonly TimeSpan ParameterApplyDebounceWindow = TimeSpan.FromSeconds(1);
    private const double DefaultPreviewGamma = 2.2;
    private const string ForwardDirection = "Forward";
    private const string ReverseDirection = "Reverse";
    private const string IlluminationWorkModeOff = "Off";
    private const string IlluminationWorkModeSteady = "Steady";
    private const string IlluminationWorkModeSync = "Sync";
    private static readonly string[] IlluminationChannelLabels = { "LED1", "LED2", "LED3", "LED4" };
    private static readonly string[] DirectionLabels = { ForwardDirection, ReverseDirection };
    private static readonly string[] MotorDirectionLabels = { "Dir0", "Dir1" };
    private const string MotorUnitSteps = ScanMotorDistanceText.StepsUnit;
    private const string MotorUnitMicrometers = ScanMotorDistanceText.MicrometersUnit;
    private const string MotorUnitMillimeters = ScanMotorDistanceText.MillimetersUnit;
    private static readonly string[] MotorUnitLabels = { MotorUnitSteps, MotorUnitMicrometers, MotorUnitMillimeters };
    private static readonly string[] ScanMotorLabels = { "Motor1", "Motor2", "Motor3" };
    private static readonly string[] RoiSelectionLabels = { "BW Active", "BW Shield", "Focus Overall", "Focus Left", "Focus Right" };
    private static readonly string[] AdcRoiSelectionLabels = { "BW Active", "BW Shield" };
    private static readonly string[] FocusRoiSelectionLabels = { "Focus Overall", "Focus Left", "Focus Right" };
    private static readonly ScanAutofocusPresetKind[] AutofocusPresetKinds =
    {
        ScanAutofocusPresetKind.Quick,
        ScanAutofocusPresetKind.Standard,
        ScanAutofocusPresetKind.Fine,
        ScanAutofocusPresetKind.Custom
    };
    private const string RoiSelectionBwActive = "BW Active";
    private const string RoiSelectionBwShield = "BW Shield";
    private const string RoiSelectionFocusOverall = "Focus Overall";
    private const string RoiSelectionFocusLeft = "Focus Left";
    private const string RoiSelectionFocusRight = "Focus Right";
    private const int CalibrationOffsetMin = -255;
    private const int CalibrationOffsetMax = 255;
    private const int CalibrationGainMin = 0;
    private const int CalibrationGainMax = 63;
    private const int AutofocusRowsMin = 1;
    private const int ScanPreviewStreamThrottleMs = 100;
    private const int ManualFocusHoldRepeatDelayMs = 90;
    private const int ManualFocusMotionPollDelayMs = 75;
    private const int ManualFocusMotionTimeoutPaddingMs = 10000;
    private const double ManualFocusMotionTimeoutMultiplier = 2.0;
    private const double AutofocusDistanceMinMm = 0.001;
    private static readonly TimeSpan FilmProfileOperationAutoCloseDelay = TimeSpan.FromSeconds(4);
    private const double DefaultAutofocusTiltProbeMm = 0.5;
    private const double DefaultAutofocusZProbeMm = 1.0;
    private const double DefaultManualFocusDistanceMm = 0.05;
    private const double ManualFocusMaximumDistanceMm = 1.0;
    private const uint ManualFocusMaximumStepsPerMappedMotor = 1_000_000;
    private const double ManualFocusMaximumEstimatedOneWayTravelDurationMs = 30_000.0;
    private const double FocusMappingTestMoveMm = 0.01;
    private static readonly Lazy<Brush> LimitBlockNormalBrush = new(() => GetThemeBrush("SystemFillColorTransparentBrush", Colors.Transparent));
    private static readonly Lazy<Brush> LimitBlockAlertBrush = new(() => GetThemeBrush("SystemFillColorCriticalBrush", Colors.IndianRed));
    private static readonly Lazy<Brush> LimitBlockNormalTextBrush = new(() => GetThemeBrush("TextFillColorSecondaryBrush", Colors.Gray));
    private static readonly Lazy<Brush> LimitBlockAlertTextBrush = new(() => GetThemeBrush("TextOnAccentFillColorPrimaryBrush", Colors.White));

    private readonly IScanSessionService _discoverySession;
    private IScanSessionService _session;
    private readonly IScanParameterService _parameters;
    private readonly IScanImageDecoder _imageDecoder;
    private readonly IScanPreviewPresenter _previewPresenter;
    private readonly IScanChannelImageService _channelImages;
    private readonly IScanAutoCalibrationService _autoCalibration;
    private readonly IScanAutoFocusService _autoFocus;
    private readonly IScanIlluminationService _illumination;
    private readonly IScanTransferSettingsService _transferSettings;
    private readonly IScanWorkflowService _workflow;
    private readonly IScanDeviceSettingsService _deviceSettings;
    private readonly IScanCalibrationProfileRepository _calibrationProfiles;
    private readonly IScanFilmProfileWorkspace _filmProfileWorkspace;
    private readonly IScanFilmProfileFileCoordinator _filmProfileFiles;
    private readonly IDebugOutputMirrorService _debugOutputMirror;
    private readonly IScanDebugSessionCoordinator _sessionCoordinator;
    private readonly IUiDispatcher _dispatcher;
    private readonly ScanDebugRuntimeOperationClaims _runtimeOperationClaims = new();
    private readonly ScanMotionRuntimeState _motionRuntimeState = new();
    private int _calibrationWorkspaceGeneration;
    private int _calibrationDeviceSessionGeneration;
    private string? _calibrationDeviceSessionId;
    private readonly SemaphoreSlim _selectedCalibrationChannelPersistenceGate = new(1, 1);
    private readonly TimeProvider _operationTimeProvider;
    private readonly object _streamingPreviewLock = new();

    private CancellationTokenSource? _scanCts;
    private byte[] _lineBuffer = Array.Empty<byte>();
    private ScanWorkflowResult? _lastWorkflowResult;
    private ScanChannelAssignment? _lastWorkflowChannelAssignment;
    private string? _lastMonochromeChannelRole;
    private ScanWorkflowResult? _streamingWorkflowPreviewResult;
    private ScanChannelAssignment? _streamingWorkflowPreviewAssignment;
    private Dictionary<int, int>? _streamingWorkflowPreviewCompletedRowsByPassIndex;
    private bool _hasValidScanBuffer;
    private DateTime _lastApplyParametersAtUtc = DateTime.MinValue;
    private bool _areRuntimeBindingsAttached;
    private bool _isFilmProfileWorkspaceSubscribed;
    private ScanFilmProfileDraft? _lastProjectedFilmProfileDraft;
    private ScanFilmProfileImportResult? _lastProjectedFilmProfileImportResult;
    private bool _isNewFilmProfilePendingExport;
    private bool _isMultiBufferedBulkInEnabled;
    private bool _suppressWarmUpToggleCommand;
    private bool _isUpdatingRoiInputs;
    private bool _isUpdatingColumnSampleInputs;
    private bool _isSynchronizingOwnerRoiSelections;
    private bool _isSynchronizingFilmProfileWorkspace;
    private bool _isSynchronizingTimingInputs;
    private ScanDeviceTimingState _deviceTimingState = ScanDeviceTimingState.Unknown;
    private ScanIlluminationState? _deviceIlluminationSnapshot;
    private readonly Dictionary<string, ScanChannelCalibrationProfile> _copiedUnverifiedCalibrationProfiles = new(StringComparer.OrdinalIgnoreCase);
    private ScanChannelCalibrationProfile? _selectedCalibrationEditorReferenceProfile;
    private string? _selectedCalibrationEditorReferenceRole;
    private bool _hasInvalidFilmProfileInput;
    private long _filmProfileOperationPublicationId;
    private ITimer? _filmProfileOperationAutoCloseTimer;
    private string? _calibrationChannelBeforeSelectionChange;
    private CalibrationChannelSelectionLoad? _selectedCalibrationChannelLoad;
    private bool _isApplyingDerivedMotorSpeed;
    private bool _isMotor1SpeedDerivedFromInterval = true;
    private bool _isMotor2SpeedDerivedFromInterval = true;
    private bool _isMotor3SpeedDerivedFromInterval = true;
    private bool _isApplyingDerivedMotorDistance;
    private bool _isMotorDistanceDerivedFromInterval = true;
    private string _lastMotorDistancePerLineUnit = MotorUnitMillimeters;
    private int _previewRows;
    private int _previewFrameVersion;
    private int _profileLoadVersion;
    private int _calibrationProjectionVersion;
    private int _streamingPreviewSessionVersion;
    private bool _isStreamingPreviewActive;
    private int _streamingPreviewTargetRows;
    private int _pendingStreamingPreviewRows;
    private long _lastStreamingPreviewEnqueueTick;
    private bool _isStreamingPreviewQueued;
    private ScanWorkflowRequest? _streamingWorkflowPreviewRequest;
    private byte[][]? _streamingWorkflowPreviewPassBuffers;
    private int[]? _streamingWorkflowPreviewPassCompletedRows;
    private bool[]? _streamingWorkflowPreviewPassDirections;
    private uint[]? _streamingWorkflowPreviewPassMotorSteps;
    private uint[]? _streamingWorkflowPreviewPassMotorIntervals;
    private ScanFilmAcquisitionSettings? _selectedFilmAcquisitionSettings;
    private ScanCalibrationRoiSettings _roiSettings = ScanCalibrationRoiSettings.CreateDefault();
    private ScanColumnRange _columnSampleRange = new(ScanDebugConstants.EffectivePixelStart, ScanDebugConstants.EffectivePixelEnd);
    private ushort? _columnSampleMean;
    private string? _columnSampleMeanChannelRole;
    private long _columnSampleMeanOwnerVersion = -1;
    private string? _columnSampleSourceChannelRole;
    private long _columnSampleSourceOwnerVersion = -1;
    private long _columnSampleOwnerVersion;
    private int _columnSampleFrameVersion = -1;
    private ScanColumnRange? _columnSampleMeanRange;
#if PRISM_VISUAL_QA
    private object _visualQaLastRoiEditAttempt = new { operation = "none" };
    private object _visualQaLastColumnSampleEditAttempt = new { operation = "none" };
#endif
    private readonly Task _deviceSettingsInitializationTask;
    private CancellationTokenSource? _manualFocusCts;
    private CancellationTokenSource? _manualFocusStopAllCts;
    private CancellationTokenSource? _focusMappingTestCts;
    private ScanDebugRuntimeOperationClaims.ScanDebugRuntimeOperationLease? _manualFocusRuntimeClaim;
    private Task? _manualFocusTask;
    private bool _manualFocusMoveInProgress;
    private CancellationTokenSource? _autoFocusCts;
    private CancellationTokenSource? _autoCalibrationCts;
    private ScanFocusMotorMapping? _activeAutoFocusMappingSnapshot;
    private ScanFocusMotorMapping? _activeManualFocusMappingSnapshot;
    private bool _isApplyingFocusMappingInputs;
    private bool _hasAppliedDeviceSettingsFocusMapping;

    public ObservableCollection<string> RowOptions { get; } = new() { "64", "128", "256", "512", "1024", "2048", "4096" };

    public ObservableCollection<ScanDebugIlluminationChannelViewModel> ActiveIlluminationChannels { get; } = new();

    public ObservableCollection<ScanDebugAcquisitionChannelViewModel> AcquisitionChannels { get; } = new();

    public ObservableCollection<ScanDebugCalibrationChannelItemViewModel> CalibrationChannelItems { get; } = new();

    public ObservableCollection<string> DirectionOptions { get; } = new(DirectionLabels);

    public ObservableCollection<string> MotorDirectionOptions { get; } = new(MotorDirectionLabels);

    public ObservableCollection<string> MotorOptions { get; } = new(ScanMotorLabels);

    public ObservableCollection<string> MotorUnitOptions { get; } = new(MotorUnitLabels);

    public ObservableCollection<string> MotorDistancePerLineUnitOptions { get; } = new(MotorUnitLabels);

    public ObservableCollection<string> CalibrationChannelOptions { get; } = new() { "Red", "Green", "Blue", "White", "IR" };

    public IReadOnlyList<string> CalibrationCopySourceChannelOptions
        => CalibrationChannelOptions
            .Where(role => !string.Equals(role, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public IReadOnlyList<string> IlluminationWorkModeOptions { get; } = new[]
    {
        "ScanDebug_IlluminationWorkModeOff".GetLocalized(),
        "ScanDebug_IlluminationWorkModeSteady".GetLocalized(),
        "ScanDebug_IlluminationWorkModeSync".GetLocalized()
    };

    public IReadOnlyList<string> AutofocusPresetOptions { get; } = new[]
    {
        "ScanDebug_Runtime_AutofocusPresetQuick".GetLocalized(),
        "ScanDebug_Runtime_AutofocusPresetStandard".GetLocalized(),
        "ScanDebug_Runtime_AutofocusPresetFine".GetLocalized(),
        "ScanDebug_Runtime_AutofocusPresetCustom".GetLocalized()
    };

    public ObservableCollection<ScanDngExportMode> DngExportModeOptions { get; } = new() { ScanDngExportMode.LinearRaw4, ScanDngExportMode.LinearRgbIrw };

    public ObservableCollection<ScanChannelAlignmentMode> AlignmentModeOptions { get; } = new() { ScanChannelAlignmentMode.Ecc, ScanChannelAlignmentMode.MutualInformation, ScanChannelAlignmentMode.EccThenMutualInformation };

    public ObservableCollection<ScanDebugCaptureMode> CaptureModeOptions { get; } = new() { ScanDebugCaptureMode.Single, ScanDebugCaptureMode.Continuous, ScanDebugCaptureMode.Transport };

    public ObservableCollection<string> RoiSelectionOptions { get; } = new(RoiSelectionLabels);

    public ObservableCollection<string> AdcRoiSelectionOptions { get; } = new(AdcRoiSelectionLabels);

    public ObservableCollection<string> FocusRoiSelectionOptions { get; } = new(FocusRoiSelectionLabels);

    public event Action<ScanRoiIssueNavigationRequest>? RoiIssueNavigationRequested;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    public partial string SelectedRows { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    public partial bool IsWarmUpEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsPreviewEnabled { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    public partial ScanDebugCaptureMode SelectedCaptureMode { get; set; }

    public bool IsContinuousScanEnabled => SelectedCaptureMode == ScanDebugCaptureMode.Continuous;

    [ObservableProperty]
    public partial bool IsMultiChannelScanEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsAlternateMotorDirectionEnabled { get; set; }

    public bool IsScanMotorTransportEnabled => SelectedCaptureMode == ScanDebugCaptureMode.Transport;

    [ObservableProperty]
    public partial string SelectedStartingDirection { get; set; }

    [ObservableProperty]
    public partial string SelectedScanMotor { get; set; }

    [ObservableProperty]
    public partial string MotorDistancePerLineValue { get; set; }

    [ObservableProperty]
    public partial string MotorDistancePerLineUnit { get; set; }

    [ObservableProperty]
    public partial string MotorIntervalUs { get; set; }

    [ObservableProperty]
    public partial string ComputedMotorSummaryText { get; set; }

    [ObservableProperty]
    public partial bool IsScanLedAutoControlEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsWaterfallEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsWaterfallCompressedEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsGammaCorrectionEnabled { get; set; }

    [ObservableProperty]
    public partial string PreviewGamma { get; set; }

    [ObservableProperty]
    public partial bool IsWhiteLevelPreviewEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsColumnSampleEditModeEnabled { get; set; }

    [ObservableProperty]
    public partial string ColumnSampleStatusText { get; set; }

    [ObservableProperty]
    public partial string ColumnSampleStartInput { get; set; }

    [ObservableProperty]
    public partial string ColumnSampleEndInput { get; set; }

    [ObservableProperty]
    public partial int ColumnSampleOverlayVersion { get; set; }

    [ObservableProperty]
    public partial string SelectedCalibrationChannel { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCalibrationProfileFromChannelCommand))]
    public partial string? SelectedCalibrationCopySourceChannel { get; set; }

    [ObservableProperty]
    public partial bool IsChannel1Reversed { get; set; }

    [ObservableProperty]
    public partial bool IsChannel2Reversed { get; set; }

    [ObservableProperty]
    public partial bool IsChannel3Reversed { get; set; }

    [ObservableProperty]
    public partial bool IsChannel4Reversed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScanRecipeManualWhitePointColorTemperatureEnabled))]
    public partial bool IsScanRecipeColorManagementEnabled { get; set; }

    [ObservableProperty]
    public partial string ScanRecipeRedWavelengthNm { get; set; }

    [ObservableProperty]
    public partial string ScanRecipeGreenWavelengthNm { get; set; }

    [ObservableProperty]
    public partial string ScanRecipeBlueWavelengthNm { get; set; }

    [ObservableProperty]
    public partial string ScanRecipeOutputGamma { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScanRecipeManualWhitePointColorTemperatureEnabled))]
    public partial string SelectedScanRecipeTargetWhitePointMode { get; set; }

    [ObservableProperty]
    public partial string ScanRecipeManualWhitePointColorTemperatureK { get; set; }

    public bool IsScanRecipeManualWhitePointColorTemperatureEnabled =>
        IsScanRecipeColorManagementEnabled
        && string.Equals(
            SelectedScanRecipeTargetWhitePointMode,
            nameof(ScanTargetWhitePointMode.ManualColorTemperature),
            StringComparison.Ordinal);

    [ObservableProperty]
    public partial ScanChannelAlignmentMode SelectedProfileAlignmentMode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedProfileDngExportModeAccessibleText))]
    public partial ScanDngExportMode SelectedProfileDngExportMode { get; set; }

    [ObservableProperty]
    public partial ScanDngExportMode SelectedDebugDngExportMode { get; set; }

    [ObservableProperty]
    public partial string CalibrationChannelStatusText { get; set; }

    public ScanDebugCalibrationChannelItemViewModel? SelectedCalibrationChannelItem
    {
        get => CalibrationChannelItems.FirstOrDefault(channel => string.Equals(channel.Role, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value is null || string.Equals(value.Role, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
                return;

            SelectedCalibrationChannel = value.Role;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    public partial string FilmProfileName { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFilmProfileJsonCommand))]
    public partial bool HasUnsavedProfileChanges { get; set; }

    [ObservableProperty]
    public partial string FilmProfileOperationMessage { get; set; } = "ScanDebug_FilmProfileWorkbenchOperationReady".GetLocalized();

    [ObservableProperty]
    public partial InfoBarSeverity FilmProfileOperationSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial bool FilmProfileOperationIsOpen { get; set; }

    [ObservableProperty]
    public partial Visibility FilmProfileOperationVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NewFilmProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadFilmProfileJsonCommand))]
    [NotifyCanExecuteChangedFor(nameof(ValidateFilmProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveFilmProfileJsonCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyStagedFilmProfileImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(DiscardStagedFilmProfileImportCommand))]
    [NotifyPropertyChangedFor(nameof(CanApplyStagedFilmProfileImport))]
    public partial bool IsFilmProfileOperationRunning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentFilmProfileValidationSummary))]
    [NotifyPropertyChangedFor(nameof(CurrentFilmProfileValidationHeadline))]
    [NotifyPropertyChangedFor(nameof(CurrentFilmProfileValidationIssueCountText))]
    [NotifyPropertyChangedFor(nameof(IsCurrentFilmProfileValidationValid))]
    [NotifyPropertyChangedFor(nameof(CurrentFilmProfileValidationSeverity))]
    [NotifyPropertyChangedFor(nameof(CurrentFilmProfileValidationIssueDisplays))]
    [NotifyPropertyChangedFor(nameof(BasicCurrentFilmProfileValidationIssueDisplays))]
    [NotifyPropertyChangedFor(nameof(BasicCurrentFilmProfileValidationHeadline))]
    [NotifyPropertyChangedFor(nameof(BasicCurrentFilmProfileValidationIssueCountText))]
    [NotifyPropertyChangedFor(nameof(BasicCurrentFilmProfileValidationSeverity))]
    [NotifyPropertyChangedFor(nameof(AcquisitionCurrentFilmProfileValidationIssueDisplays))]
    [NotifyPropertyChangedFor(nameof(AcquisitionCurrentFilmProfileValidationHeadline))]
    [NotifyPropertyChangedFor(nameof(AcquisitionCurrentFilmProfileValidationIssueCountText))]
    [NotifyPropertyChangedFor(nameof(AcquisitionCurrentFilmProfileValidationSeverity))]
    [NotifyPropertyChangedFor(nameof(ChannelCalibrationCurrentFilmProfileValidationIssueDisplays))]
    [NotifyPropertyChangedFor(nameof(ChannelCalibrationCurrentFilmProfileValidationHeadline))]
    [NotifyPropertyChangedFor(nameof(ChannelCalibrationCurrentFilmProfileValidationIssueCountText))]
    [NotifyPropertyChangedFor(nameof(ChannelCalibrationCurrentFilmProfileValidationSeverity))]
    [NotifyCanExecuteChangedFor(nameof(SaveFilmProfileJsonCommand))]
    [NotifyCanExecuteChangedFor(nameof(NavigateToCurrentFilmProfileValidationIssueCommand))]
    public partial IReadOnlyList<ScanFilmProfileValidationIssue> CurrentFilmProfileValidationIssues { get; set; } = Array.Empty<ScanFilmProfileValidationIssue>();

    public string CurrentFilmProfileValidationSummary => FormatFilmProfileValidationIssues(CurrentFilmProfileValidationIssues);

    public string CurrentFilmProfileValidationHeadline => FormatFilmProfileValidationCount(CurrentFilmProfileValidationIssues.Count);

    public string CurrentFilmProfileValidationIssueCountText => CurrentFilmProfileValidationHeadline;

    public IReadOnlyList<ScanFilmProfileValidationIssueDisplay> CurrentFilmProfileValidationIssueDisplays => BuildFilmProfileValidationIssueDisplays(
        ScanFilmProfileIssueSource.CurrentDraft,
        CurrentFilmProfileValidationIssues);

    public IReadOnlyList<ScanFilmProfileValidationIssueDisplay> BasicCurrentFilmProfileValidationIssueDisplays => BuildCurrentFilmProfileValidationIssueDisplays(
        ScanFilmProfileIssueNavigationSection.BasicInfo);

    public string BasicCurrentFilmProfileValidationHeadline => FormatFilmProfileValidationCount(BasicCurrentFilmProfileValidationIssueDisplays.Count);

    public string BasicCurrentFilmProfileValidationIssueCountText => BasicCurrentFilmProfileValidationHeadline;

    public InfoBarSeverity BasicCurrentFilmProfileValidationSeverity => GetCurrentFilmProfileValidationSeverity(
        ScanFilmProfileIssueNavigationSection.BasicInfo);

    public IReadOnlyList<ScanFilmProfileValidationIssueDisplay> AcquisitionCurrentFilmProfileValidationIssueDisplays => BuildCurrentFilmProfileValidationIssueDisplays(
        ScanFilmProfileIssueNavigationSection.AcquisitionPlan);

    public string AcquisitionCurrentFilmProfileValidationHeadline => FormatFilmProfileValidationCount(AcquisitionCurrentFilmProfileValidationIssueDisplays.Count);

    public string AcquisitionCurrentFilmProfileValidationIssueCountText => AcquisitionCurrentFilmProfileValidationHeadline;

    public InfoBarSeverity AcquisitionCurrentFilmProfileValidationSeverity => GetCurrentFilmProfileValidationSeverity(
        ScanFilmProfileIssueNavigationSection.AcquisitionPlan);

    public IReadOnlyList<ScanFilmProfileValidationIssueDisplay> ChannelCalibrationCurrentFilmProfileValidationIssueDisplays => BuildCurrentFilmProfileValidationIssueDisplays(
        ScanFilmProfileIssueNavigationSection.ChannelCalibration);

    public string ChannelCalibrationCurrentFilmProfileValidationHeadline => FormatFilmProfileValidationCount(ChannelCalibrationCurrentFilmProfileValidationIssueDisplays.Count);

    public string ChannelCalibrationCurrentFilmProfileValidationIssueCountText => ChannelCalibrationCurrentFilmProfileValidationHeadline;

    public InfoBarSeverity ChannelCalibrationCurrentFilmProfileValidationSeverity => GetCurrentFilmProfileValidationSeverity(
        ScanFilmProfileIssueNavigationSection.ChannelCalibration);

    public bool IsCurrentFilmProfileValidationValid => new ScanFilmProfileValidationResult(CurrentFilmProfileValidationIssues).IsValid;

    public InfoBarSeverity CurrentFilmProfileValidationSeverity => CurrentFilmProfileValidationIssues.Any(issue => issue.Severity == ScanFilmProfileValidationSeverity.Error)
        ? InfoBarSeverity.Error
        : CurrentFilmProfileValidationIssues.Count > 0
            ? InfoBarSeverity.Warning
            : InfoBarSeverity.Success;

    public string SelectedProfileDngExportModeAccessibleText => ScanSelectorDisplayNameConverter.GetDngExportModeDisplayName(SelectedProfileDngExportMode);

    public IReadOnlyList<ScanRoiValidationIssue> AdcRoiValidationIssues => BuildAdcRoiValidationIssues();

    public IReadOnlyList<ScanRoiValidationIssueDisplay> AdcRoiValidationIssueDisplays => BuildRoiValidationIssueDisplays(AdcRoiValidationIssues);

    public IReadOnlyList<ScanRoiValidationIssue> FocusRoiValidationIssues => BuildFocusRoiValidationIssues();

    public IReadOnlyList<ScanRoiValidationIssueDisplay> FocusRoiValidationIssueDisplays => BuildRoiValidationIssueDisplays(FocusRoiValidationIssues);

    public IReadOnlyList<ScanRoiValidationIssue> ImageReferenceRoiValidationIssues => BuildImageReferenceRoiValidationIssues();

    public IReadOnlyList<ScanRoiValidationIssueDisplay> ImageReferenceRoiValidationIssueDisplays => BuildRoiValidationIssueDisplays(ImageReferenceRoiValidationIssues);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyStagedFilmProfileImport))]
    [NotifyCanExecuteChangedFor(nameof(ApplyStagedFilmProfileImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(DiscardStagedFilmProfileImportCommand))]
    public partial bool HasStagedFilmProfileImport { get; set; }

    [ObservableProperty]
    public partial string StagedFilmProfileImportSummary { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StagedFilmProfileImportValidationSummary))]
    [NotifyPropertyChangedFor(nameof(StagedFilmProfileImportValidationHeadline))]
    [NotifyPropertyChangedFor(nameof(StagedFilmProfileImportValidationIssueCountText))]
    [NotifyPropertyChangedFor(nameof(StagedFilmProfileImportValidationIssueDisplays))]
    [NotifyPropertyChangedFor(nameof(IsStagedFilmProfileImportValid))]
    [NotifyPropertyChangedFor(nameof(CanApplyStagedFilmProfileImport))]
    [NotifyCanExecuteChangedFor(nameof(ApplyStagedFilmProfileImportCommand))]
    public partial IReadOnlyList<ScanFilmProfileValidationIssue> StagedFilmProfileImportValidationIssues { get; set; } = Array.Empty<ScanFilmProfileValidationIssue>();

    public string StagedFilmProfileImportDisplayNameText => HasStagedFilmProfileImport
        ? GetFilmProfileDisplayName(StagedFilmProfileImportSummary)
        : _filmProfileWorkspace.Snapshot.ImportResult.State == ScanFilmProfileImportResultState.Error
            ? "ScanDebug_FilmProfileWorkbenchImportError".GetLocalized()
        : "ScanDebug_FilmProfileWorkbenchStagedImportNone".GetLocalized();

    public string StagedFilmProfileImportChannelCountText => _filmProfileWorkspace.Snapshot.StagedImport is { } staged
        ? "ScanDebug_FilmProfileWorkbenchStagedImportChannelCount".GetLocalizedFormat(staged.Draft.ChannelProfiles.Count)
        : "ScanDebug_FilmProfileWorkbenchImportResultNoChannels".GetLocalized();

    public bool HasPendingFilmProfileImportResult => _filmProfileWorkspace.Snapshot.ImportResult.State != ScanFilmProfileImportResultState.None;

    public Visibility FilmProfileImportResultReviewVisibility => HasPendingFilmProfileImportResult
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility FilmProfileWorkbenchContentVisibility => HasPendingFilmProfileImportResult
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility StagedFilmProfileImportReviewVisibility => FilmProfileImportResultReviewVisibility;

    public InfoBarSeverity StagedFilmProfileImportSeverity => _filmProfileWorkspace.Snapshot.ImportResult.State == ScanFilmProfileImportResultState.Error ? InfoBarSeverity.Error : InfoBarSeverity.Informational;

    public string StagedFilmProfileDirtyReplacementWarningText => HasStagedFilmProfileImport && HasUnsavedProfileChanges
        ? "ScanDebug_FilmProfileWorkbenchStagedImportDirtyWarning".GetLocalized()
        : string.Empty;

    public Visibility StagedFilmProfileDirtyReplacementWarningVisibility => string.IsNullOrWhiteSpace(StagedFilmProfileDirtyReplacementWarningText)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public string StagedFilmProfileImportValidationSummary => StagedFilmProfileImportValidationIssues.Count == 0
        ? "ScanDebug_FilmProfileWorkbenchValidationValid".GetLocalized()
        : FormatFilmProfileValidationIssues(StagedFilmProfileImportValidationIssues);

    public string StagedFilmProfileImportValidationHeadline => FormatFilmProfileValidationCount(StagedFilmProfileImportValidationIssues.Count);

    public string StagedFilmProfileImportValidationIssueCountText => StagedFilmProfileImportValidationHeadline;

    public IReadOnlyList<ScanFilmProfileValidationIssueDisplay> StagedFilmProfileImportValidationIssueDisplays => BuildFilmProfileValidationIssueDisplays(
        ScanFilmProfileIssueSource.ImportReview,
        StagedFilmProfileImportValidationIssues);

    public bool IsStagedFilmProfileImportValid => new ScanFilmProfileValidationResult(StagedFilmProfileImportValidationIssues).IsValid;

    public bool CanApplyStagedFilmProfileImport => CanApplyStagedFilmProfileImportCommand();

    [ObservableProperty]
    public partial string SelectedRoiSelection { get; set; }

    [ObservableProperty]
    public partial string SelectedAdcRoiSelection { get; set; }

    [ObservableProperty]
    public partial string SelectedFocusRoiSelection { get; set; }

    [ObservableProperty]
    public partial bool IsBwActiveRoiOverlayVisible { get; set; }

    [ObservableProperty]
    public partial bool IsBwShieldRoiOverlayVisible { get; set; }

    [ObservableProperty]
    public partial bool IsFocusOverallRoiOverlayVisible { get; set; }

    [ObservableProperty]
    public partial bool IsFocusLeftRoiOverlayVisible { get; set; }

    [ObservableProperty]
    public partial bool IsFocusRightRoiOverlayVisible { get; set; }

    [ObservableProperty]
    public partial bool IsImageReferenceOverlayVisible { get; set; }

    [ObservableProperty]
    public partial bool IsRoiEditModeEnabled { get; set; }

    [ObservableProperty]
    public partial string RoiStatusText { get; set; }

    [ObservableProperty]
    public partial string RoiStartInput { get; set; }

    [ObservableProperty]
    public partial string RoiEndInput { get; set; }

    [ObservableProperty]
    public partial string RoiInputStatusText { get; set; }

    [ObservableProperty]
    public partial int RoiOverlayVersion { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportDngCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyDeviceClockCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoFocusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshMotionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyMotorConfigCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportDngCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyDeviceClockCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoFocusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshMotionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyMotorConfigCommand))]
    public partial bool IsOutputOperationRunning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    public partial bool IsDevicesPresent { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoFocusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshMotionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyMotorConfigCommand))]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoFocusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshMotionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyMotorConfigCommand))]
    public partial bool IsConnecting { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string DngAlignmentWarningMessage { get; set; }

    [ObservableProperty]
    public partial Visibility DngAlignmentWarningVisibility { get; set; } = Visibility.Collapsed;

    public string DeviceStateText => IsConnecting
        ? "ScanDebug_Runtime_DeviceStateConnecting".GetLocalized()
        : IsConnected
            ? "ScanDebug_Runtime_DeviceStateConnected".GetLocalized()
            : IsDevicesPresent
                ? "ScanDebug_Runtime_DeviceStateDetected".GetLocalized()
                : "ScanDebug_Runtime_DeviceStateWaiting".GetLocalized();

    public string FilmProfileDeviceStatusText => IsConnected
        ? "ScanDebug_FilmProfileWorkbenchDeviceConnected".GetLocalized()
        : "ScanDebug_FilmProfileWorkbenchDeviceDisconnected".GetLocalized();

    public string FilmProfileHardwareUnavailableReasonText => IsConnected
        ? string.Empty
        : "ScanDebug_DisabledReasonConnectDevice".GetLocalized();

    [ObservableProperty]
    public partial bool IsScanReadProgressVisible { get; set; }

    [ObservableProperty]
    public partial Visibility ScanReadProgressVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial double ScanReadProgressValue { get; set; }

    [ObservableProperty]
    public partial double ScanReadProgressMaximum { get; set; } = 1;

    partial void OnIsScanReadProgressVisibleChanged(bool value)
        => ScanReadProgressVisibility = value ? Visibility.Visible : Visibility.Collapsed;

    partial void OnCalibrationChannelStatusTextChanged(string value)
        => MirrorOutput("ScanDebug.Calibration", value);

    partial void OnRoiStatusTextChanged(string value)
        => MirrorOutput("ScanDebug.Roi", value);

    partial void OnRoiInputStatusTextChanged(string value)
        => MirrorOutput("ScanDebug.RoiInput", value);

    partial void OnStatusTextChanged(string value)
        => MirrorOutput("ScanDebug.Status", value);

    partial void OnPendingCalibrationResultChanged(PendingCalibrationResult? value)
    {
        AcceptPendingCalibrationCommand.NotifyCanExecuteChanged();
        AcceptAndSavePendingCalibrationCommand.NotifyCanExecuteChanged();
        RestorePendingCalibrationCommand.NotifyCanExecuteChanged();
        CancelPendingCalibrationCommand.NotifyCanExecuteChanged();
    }

    partial void OnPreviewFrameChanged(ScanPreviewFrame? value)
    {
        InvalidateColumnSampleCache();
        NotifyColumnSampleAvailabilityChanged();
        NotifyPreviewStatePropertiesChanged();
    }

    partial void OnIlluminationSummaryTextChanged(string value)
        => MirrorOutput("ScanDebug.Illumination", value);

    partial void OnMotionSummaryTextChanged(string value)
        => MirrorOutput("ScanDebug.Motion", value);

    partial void OnMotor1StatusTextChanged(string value)
        => MirrorOutput("ScanDebug.Motor1", value);

    partial void OnMotor2StatusTextChanged(string value)
        => MirrorOutput("ScanDebug.Motor2", value);

    partial void OnMotor3StatusTextChanged(string value)
        => MirrorOutput("ScanDebug.Motor3", value);

    partial void OnAutofocusSummaryTextChanged(string value)
        => MirrorOutput("ScanDebug.Autofocus", value);

    [ObservableProperty]
    public partial ScanPreviewFrame? PreviewFrame { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyDeviceClockCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoFocusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshMotionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyMotorConfigCommand))]
    public partial bool IsApplyingParameters { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyDeviceClockCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoFocusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshMotionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyMotorConfigCommand))]
    public partial bool IsApplyingDeviceClock { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyDeviceClockCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoFocusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshMotionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyMotorConfigCommand))]
    public partial bool IsAutoCalibrating { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyDeviceClockCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoFocusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshMotionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyMotorConfigCommand))]
    public partial bool IsAutoFocusing { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyDeviceClockCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoFocusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshMotionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyMotorConfigCommand))]
    public partial bool IsManualFocusing { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyDeviceClockCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoFocusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshMotionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyMotorConfigCommand))]
    public partial bool IsApplyingIllumination { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectDevicesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoBlackAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoWhiteAdjustCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoCalibrateCommand))]
    [NotifyCanExecuteChangedFor(nameof(AutoFocusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyIlluminationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshMotionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisableMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMotorCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyMotorConfigCommand))]
    public partial bool IsApplyingMotion { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFocusMappingCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestLeftFocusMappingCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestRightFocusMappingCommand))]
    public partial bool IsUpdatingFocusMapping { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveChannelProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearChannelProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveColumnSampleAsBlackLevelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveColumnSampleAsWhiteLevelCommand))]
    public partial bool IsCalibrationRepositoryOperationRunning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingCalibrationCandidateReviewVisibility))]
    [NotifyPropertyChangedFor(nameof(PendingCalibrationSelectedChannelText))]
    [NotifyPropertyChangedFor(nameof(PendingCalibrationAdcDifferenceText))]
    [NotifyPropertyChangedFor(nameof(PendingCalibrationBlackDeviationText))]
    [NotifyPropertyChangedFor(nameof(PendingCalibrationSaturationText))]
    [NotifyPropertyChangedFor(nameof(PendingCalibrationNoiseText))]
    [NotifyPropertyChangedFor(nameof(PendingCalibrationValidationText))]
    [NotifyPropertyChangedFor(nameof(PendingCalibrationValidationIssuesText))]
    public partial PendingCalibrationResult? PendingCalibrationResult { get; set; }

    public Visibility PendingCalibrationCandidateReviewVisibility => PendingCalibrationResult is { State: PendingCalibrationResultState.Pending }
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string PendingCalibrationSelectedChannelText => PendingCalibrationResult is { State: PendingCalibrationResultState.Pending } pending
        ? "ScanDebug_Runtime_PendingCalibrationSelectedChannel".GetLocalizedFormat(GetCalibrationChannelDisplayName(pending.SelectedChannel))
        : string.Empty;

    public string PendingCalibrationAdcDifferenceText => FormatPendingCalibrationMetric(
        PendingCalibrationResult?.BeforeMetrics.AdcOutputDifferencePercent,
        PendingCalibrationResult?.AfterMetrics.AdcOutputDifferencePercent,
        "%");

    public string PendingCalibrationBlackDeviationText => FormatPendingCalibrationMetric(
        PendingCalibrationResult?.BeforeMetrics.BlackLevelDeviation,
        PendingCalibrationResult?.AfterMetrics.BlackLevelDeviation,
        string.Empty);

    public string PendingCalibrationSaturationText => FormatPendingCalibrationMetric(
        PendingCalibrationResult?.BeforeMetrics.SaturatedPixelPercent,
        PendingCalibrationResult?.AfterMetrics.SaturatedPixelPercent,
        "%");

    public string PendingCalibrationNoiseText => FormatPendingCalibrationMetric(
        PendingCalibrationResult?.BeforeMetrics.NoiseStandardDeviation,
        PendingCalibrationResult?.AfterMetrics.NoiseStandardDeviation,
        string.Empty);

    public string PendingCalibrationValidationText => PendingCalibrationResult is { State: PendingCalibrationResultState.Pending } pending
        ? pending.Validation.IsValid
            ? pending.Validation.Issues.Count == 0
                ? "ScanDebug_Runtime_PendingCalibrationValidationValid".GetLocalized()
                : "ScanDebug_Runtime_PendingCalibrationValidationWarning".GetLocalized()
            : "ScanDebug_Runtime_PendingCalibrationValidationInvalid".GetLocalized()
        : string.Empty;

    public string PendingCalibrationValidationIssuesText => PendingCalibrationResult is { State: PendingCalibrationResultState.Pending } pending
        ? pending.Validation.Issues.Count == 0
            ? "ScanDebug_Runtime_PendingCalibrationValidationNoIssues".GetLocalized()
            : "ScanDebug_Runtime_PendingCalibrationValidationIssues".GetLocalizedFormat(string.Join("; ", pending.Validation.Issues.Select(FormatPendingCalibrationValidationIssue)))
        : string.Empty;

    [ObservableProperty]
    public partial string ExposureTicks { get; set; }

    [ObservableProperty]
    public partial string ExposureMicroseconds { get; set; }

    [ObservableProperty]
    public partial string Adc1Offset { get; set; }

    [ObservableProperty]
    public partial string Adc1Gain { get; set; }

    [ObservableProperty]
    public partial string Adc2Offset { get; set; }

    [ObservableProperty]
    public partial string Adc2Gain { get; set; }

    [ObservableProperty]
    public partial string SysClockKhz { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyDeviceClockCommand))]
    public partial string SysClockMhz { get; set; }

    [ObservableProperty]
    public partial string SysClockMhzDisplay { get; set; }

    public ScanDeviceTimingState DeviceTimingState => _deviceTimingState;

    public bool RequiresDeviceRead => _deviceTimingState.RequiresDeviceRead;

    [ObservableProperty]
    public partial string ExposureTimeDisplay { get; set; }

    [ObservableProperty]
    public partial string Adc1OffsetMvDisplay { get; set; }

    [ObservableProperty]
    public partial string Adc2OffsetMvDisplay { get; set; }

    [ObservableProperty]
    public partial string Adc1GainVvDisplay { get; set; }

    [ObservableProperty]
    public partial string Adc2GainVvDisplay { get; set; }

    [ObservableProperty]
    public partial string Led1Level { get; set; }

    [ObservableProperty]
    public partial string Led2Level { get; set; }

    [ObservableProperty]
    public partial string Led3Level { get; set; }

    [ObservableProperty]
    public partial string Led4Level { get; set; }

    [ObservableProperty]
    public partial bool IsLed1SteadyEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed2SteadyEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed3SteadyEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed4SteadyEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed1SyncEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed2SyncEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed3SyncEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLed4SyncEnabled { get; set; }

    [ObservableProperty]
    public partial string Led1PulseClock { get; set; }

    [ObservableProperty]
    public partial string Led2PulseClock { get; set; }

    [ObservableProperty]
    public partial string Led3PulseClock { get; set; }

    [ObservableProperty]
    public partial string Led4PulseClock { get; set; }

    [ObservableProperty]
    public partial string RawLed1Level { get; set; }

    [ObservableProperty]
    public partial string RawLed2Level { get; set; }

    [ObservableProperty]
    public partial string RawLed3Level { get; set; }

    [ObservableProperty]
    public partial string RawLed4Level { get; set; }

    [ObservableProperty]
    public partial bool IsRawLed1SteadyEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsRawLed2SteadyEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsRawLed3SteadyEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsRawLed4SteadyEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsRawLed1SyncEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsRawLed2SyncEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsRawLed3SyncEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsRawLed4SyncEnabled { get; set; }

    [ObservableProperty]
    public partial string RawLed1PulseClock { get; set; }

    [ObservableProperty]
    public partial string RawLed2PulseClock { get; set; }

    [ObservableProperty]
    public partial string RawLed3PulseClock { get; set; }

    [ObservableProperty]
    public partial string RawLed4PulseClock { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ScanIlluminationValidationIssue> RawIlluminationValidationIssues { get; set; } = Array.Empty<ScanIlluminationValidationIssue>();

    public ScanIlluminationState? DeviceIlluminationSnapshot => _deviceIlluminationSnapshot;

    public string DeviceLed1Level => _deviceIlluminationSnapshot?.Led1Level.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public string DeviceLed2Level => _deviceIlluminationSnapshot?.Led2Level.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public string DeviceLed3Level => _deviceIlluminationSnapshot?.Led3Level.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public string DeviceLed4Level => _deviceIlluminationSnapshot?.Led4Level.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public string DeviceLed1PulseClock => _deviceIlluminationSnapshot?.Led1PulseClock.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public string DeviceLed2PulseClock => _deviceIlluminationSnapshot?.Led2PulseClock.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public string DeviceLed3PulseClock => _deviceIlluminationSnapshot?.Led3PulseClock.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public string DeviceLed4PulseClock => _deviceIlluminationSnapshot?.Led4PulseClock.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public bool IsDeviceLed1SteadyEnabled => (_deviceIlluminationSnapshot?.SteadyMask & 0x01) != 0;

    public bool IsDeviceLed2SteadyEnabled => (_deviceIlluminationSnapshot?.SteadyMask & 0x02) != 0;

    public bool IsDeviceLed3SteadyEnabled => (_deviceIlluminationSnapshot?.SteadyMask & 0x04) != 0;

    public bool IsDeviceLed4SteadyEnabled => (_deviceIlluminationSnapshot?.SteadyMask & 0x08) != 0;

    public bool IsDeviceLed1SyncEnabled => (_deviceIlluminationSnapshot?.SyncMask & 0x01) != 0;

    public bool IsDeviceLed2SyncEnabled => (_deviceIlluminationSnapshot?.SyncMask & 0x02) != 0;

    public bool IsDeviceLed3SyncEnabled => (_deviceIlluminationSnapshot?.SyncMask & 0x04) != 0;

    public bool IsDeviceLed4SyncEnabled => (_deviceIlluminationSnapshot?.SyncMask & 0x08) != 0;

    [ObservableProperty]
    public partial string SessionTestLedIndex { get; set; }

    [ObservableProperty]
    public partial string SessionTestLedLevel { get; set; }

    [ObservableProperty]
    public partial string SessionTestPulseClock { get; set; }

    [ObservableProperty]
    public partial string IlluminationSummaryText { get; set; }

    [ObservableProperty]
    public partial string MotionSummaryText { get; set; }

    [ObservableProperty]
    public partial string MotionStateReadRequiredText { get; set; }

    public bool IsMotionStateReadRequired => _motionRuntimeState.RequiresRead;

    public Visibility MotionStateReadRequiredVisibility => IsMotionStateReadRequired ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    public partial string Motor1RoleText { get; set; }

    [ObservableProperty]
    public partial string Motor2RoleText { get; set; }

    [ObservableProperty]
    public partial string Motor3RoleText { get; set; }

    [ObservableProperty]
    public partial string Motor1MoveSummaryText { get; set; }

    [ObservableProperty]
    public partial string Motor2MoveSummaryText { get; set; }

    [ObservableProperty]
    public partial string Motor3MoveSummaryText { get; set; }

    [ObservableProperty]
    public partial string Motor1ApplyConfigEffectText { get; set; }

    [ObservableProperty]
    public partial string Motor2ApplyConfigEffectText { get; set; }

    [ObservableProperty]
    public partial string Motor3ApplyConfigEffectText { get; set; }

    [ObservableProperty]
    public partial string Motor1StatusText { get; set; }

    [ObservableProperty]
    public partial string Motor2StatusText { get; set; }

    [ObservableProperty]
    public partial string Motor3StatusText { get; set; }

    [ObservableProperty]
    public partial string Motor1MoveDirection { get; set; }

    [ObservableProperty]
    public partial string Motor2MoveDirection { get; set; }

    [ObservableProperty]
    public partial string Motor3MoveDirection { get; set; }

    [ObservableProperty]
    public partial string Motor1MoveValue { get; set; }

    [ObservableProperty]
    public partial string Motor2MoveValue { get; set; }

    [ObservableProperty]
    public partial string Motor3MoveValue { get; set; }

    [ObservableProperty]
    public partial string Motor1MoveUnit { get; set; }

    [ObservableProperty]
    public partial string Motor2MoveUnit { get; set; }

    [ObservableProperty]
    public partial string Motor3MoveUnit { get; set; }

    [ObservableProperty]
    public partial string Motor1SpeedValue { get; set; }

    [ObservableProperty]
    public partial string Motor2SpeedValue { get; set; }

    [ObservableProperty]
    public partial string Motor3SpeedValue { get; set; }

    [ObservableProperty]
    public partial string Motor1SpeedUnit { get; set; }

    [ObservableProperty]
    public partial string Motor2SpeedUnit { get; set; }

    [ObservableProperty]
    public partial string Motor3SpeedUnit { get; set; }

    [ObservableProperty]
    public partial string Motor1MoveSteps { get; set; }

    [ObservableProperty]
    public partial string Motor2MoveSteps { get; set; }

    [ObservableProperty]
    public partial string Motor3MoveSteps { get; set; }

    [ObservableProperty]
    public partial string Motor1IntervalNs { get; set; }

    [ObservableProperty]
    public partial string Motor2IntervalNs { get; set; }

    [ObservableProperty]
    public partial string Motor3IntervalNs { get; set; }

    [ObservableProperty]
    public partial string AutofocusSampleRows { get; set; }

    [ObservableProperty]
    public partial string AutofocusTiltProbeSteps { get; set; }

    [ObservableProperty]
    public partial string AutofocusZProbeSteps { get; set; }

    [ObservableProperty]
    public partial string AutofocusMotorIntervalUs { get; set; }

    [ObservableProperty]
    public partial string AutofocusMaxTiltIterations { get; set; }

    [ObservableProperty]
    public partial string AutofocusMaxZIterations { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomAutofocusPresetSelected))]
    [NotifyPropertyChangedFor(nameof(SelectedAutofocusPresetKey))]
    [NotifyPropertyChangedFor(nameof(SelectedAutofocusPresetDisplayName))]
    public partial ScanAutofocusPresetKind SelectedAutofocusPreset { get; set; }

    public string SelectedAutofocusPresetKey
    {
        get => SelectedAutofocusPreset.ToString();
        set
        {
            if (Enum.TryParse<ScanAutofocusPresetKind>(value, out var preset))
                SelectedAutofocusPreset = preset;
        }
    }

    public string SelectedAutofocusPresetDisplayName
    {
        get => ScanSelectorDisplayNameConverter.GetAutofocusPresetDisplayName(SelectedAutofocusPreset);
        set
        {
            for (var index = 0; index < AutofocusPresetOptions.Count; index++)
            {
                if (string.Equals(value, AutofocusPresetOptions[index], StringComparison.Ordinal))
                {
                    SelectedAutofocusPreset = AutofocusPresetKinds[index];
                    return;
                }
            }
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFocusMappingCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestLeftFocusMappingCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestRightFocusMappingCommand))]
    public partial string FocusMappingLeftMotor { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFocusMappingCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestLeftFocusMappingCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestRightFocusMappingCommand))]
    public partial string FocusMappingRightMotor { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFocusMappingCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestLeftFocusMappingCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestRightFocusMappingCommand))]
    public partial string FocusMappingZPositiveDirection { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFocusMappingCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestLeftFocusMappingCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestRightFocusMappingCommand))]
    public partial string FocusMappingTiltPositiveDirection { get; set; }

    [ObservableProperty]
    public partial string ManualFocusDistanceMm { get; set; }

    [ObservableProperty]
    public partial string AutofocusSummaryText { get; set; }

    [ObservableProperty]
    public partial string AutofocusBoundsText { get; set; }

    [ObservableProperty]
    public partial string FocusMappingStatusText { get; set; }

    [ObservableProperty]
    public partial Visibility FocusMappingValidationVisibility { get; set; } = Visibility.Collapsed;

    public bool IsCustomAutofocusPresetSelected => SelectedAutofocusPreset == ScanAutofocusPresetKind.Custom;

    public bool CanSaveFocusMappingAction => CanSaveFocusMapping();

    public bool CanTestFocusMappingAction => CanTestFocusMapping();

    public string Adc1OffsetLimitText => BuildBoundedLimitText(Adc1Offset, CalibrationOffsetMin, CalibrationOffsetMax, "ADC1 offset");

    public string Adc2OffsetLimitText => BuildBoundedLimitText(Adc2Offset, CalibrationOffsetMin, CalibrationOffsetMax, "ADC2 offset");

    public string Adc1GainLimitText => BuildBoundedLimitText(Adc1Gain, CalibrationGainMin, CalibrationGainMax, "ADC1 gain");

    public string Adc2GainLimitText => BuildBoundedLimitText(Adc2Gain, CalibrationGainMin, CalibrationGainMax, "ADC2 gain");

    public string AutofocusSampleRowsLimitText => BuildBoundedLimitText(AutofocusSampleRows, AutofocusRowsMin, _session.SingleTransferMaxRows, "Sample rows");

    public string AutofocusTiltProbeStepsLimitText => BuildPositiveDistanceLimitText(AutofocusTiltProbeSteps, "Tilt probe distance");

    public string AutofocusZProbeStepsLimitText => BuildPositiveDistanceLimitText(AutofocusZProbeSteps, "Z probe distance");

    public string ManualFocusDistanceLimitText => BuildPositiveDistanceLimitText(ManualFocusDistanceMm, "Manual focus distance");

    public string AutofocusMotorIntervalLimitText => BuildLowerBoundLimitText(AutofocusMotorIntervalUs, ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs), "Motor interval");

    public Brush Adc1OffsetLimitBrush => BuildBoundedLimitBrush(Adc1Offset, CalibrationOffsetMin, CalibrationOffsetMax);

    public Brush Adc2OffsetLimitBrush => BuildBoundedLimitBrush(Adc2Offset, CalibrationOffsetMin, CalibrationOffsetMax);

    public Brush Adc1GainLimitBrush => BuildBoundedLimitBrush(Adc1Gain, CalibrationGainMin, CalibrationGainMax);

    public Brush Adc2GainLimitBrush => BuildBoundedLimitBrush(Adc2Gain, CalibrationGainMin, CalibrationGainMax);

    public Brush AutofocusSampleRowsLimitBrush => BuildBoundedLimitBrush(AutofocusSampleRows, AutofocusRowsMin, _session.SingleTransferMaxRows);

    public Brush AutofocusTiltProbeStepsLimitBrush => BuildPositiveDistanceLimitBrush(AutofocusTiltProbeSteps);

    public Brush AutofocusZProbeStepsLimitBrush => BuildPositiveDistanceLimitBrush(AutofocusZProbeSteps);

    public Brush ManualFocusDistanceLimitBrush => BuildPositiveDistanceLimitBrush(ManualFocusDistanceMm);

    public Brush AutofocusMotorIntervalLimitBrush => BuildLowerBoundLimitBrush(AutofocusMotorIntervalUs, ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));

    public Brush Adc1OffsetLimitTextBrush => BuildBoundedLimitTextBrush(Adc1Offset, CalibrationOffsetMin, CalibrationOffsetMax);

    public Brush Adc2OffsetLimitTextBrush => BuildBoundedLimitTextBrush(Adc2Offset, CalibrationOffsetMin, CalibrationOffsetMax);

    public Brush Adc1GainLimitTextBrush => BuildBoundedLimitTextBrush(Adc1Gain, CalibrationGainMin, CalibrationGainMax);

    public Brush Adc2GainLimitTextBrush => BuildBoundedLimitTextBrush(Adc2Gain, CalibrationGainMin, CalibrationGainMax);

    public Brush AutofocusSampleRowsLimitTextBrush => BuildBoundedLimitTextBrush(AutofocusSampleRows, AutofocusRowsMin, _session.SingleTransferMaxRows);

    public Brush AutofocusTiltProbeStepsLimitTextBrush => BuildPositiveDistanceLimitTextBrush(AutofocusTiltProbeSteps);

    public Brush AutofocusZProbeStepsLimitTextBrush => BuildPositiveDistanceLimitTextBrush(AutofocusZProbeSteps);

    public Brush ManualFocusDistanceLimitTextBrush => BuildPositiveDistanceLimitTextBrush(ManualFocusDistanceMm);

    public Brush AutofocusMotorIntervalLimitTextBrush => BuildLowerBoundLimitTextBrush(AutofocusMotorIntervalUs, ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));

    public event EventHandler<ScanCalibrationPromptRequest>? CalibrationPromptRequested;

    public event EventHandler<ScanFilmProfileDiscardConfirmationRequest>? FilmProfileDiscardConfirmationRequested;

    public event EventHandler<ScanNoticeRequest>? NoticeRequested;

    public event EventHandler? CalibrationSectionRequested;

    public event Action<ScanFilmProfileIssueNavigationRequest>? CurrentFilmProfileIssueNavigationRequested;

    public ScanDebugViewModel(IScanSessionService session, IScanParameterService parameters, IScanImageDecoder imageDecoder, IScanPreviewPresenter previewPresenter, IScanChannelImageService channelImages, IScanAutoCalibrationService autoCalibration, IScanAutoFocusService autoFocus, IScanIlluminationService illumination, IScanTransferSettingsService transferSettings, IScanWorkflowService workflow, IScanDeviceSettingsService deviceSettings, IScanCalibrationProfileRepository calibrationProfiles, IScanFilmProfileWorkspace filmProfileWorkspace, IScanFilmProfileFileCoordinator filmProfileFiles, IDebugOutputMirrorService debugOutputMirror, IScanDebugSessionCoordinator sessionCoordinator, IUiDispatcher dispatcher, TimeProvider? operationTimeProvider = null)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stepStopwatch = Stopwatch.StartNew();
        _discoverySession = session;
        _session = session;
        _parameters = parameters;
        _imageDecoder = imageDecoder;
        _previewPresenter = previewPresenter;
        _channelImages = channelImages;
        _autoCalibration = autoCalibration;
        _autoFocus = autoFocus;
        _illumination = illumination;
        _transferSettings = transferSettings;
        _workflow = workflow;
        _deviceSettings = deviceSettings;
        _calibrationProfiles = calibrationProfiles;
        _filmProfileWorkspace = filmProfileWorkspace;
        _filmProfileFiles = filmProfileFiles;
        _debugOutputMirror = debugOutputMirror;
        _sessionCoordinator = sessionCoordinator;
        _sessionCoordinator.SnapshotChanged += OnSessionCoordinatorSnapshotChanged;
        _dispatcher = dispatcher;
        _operationTimeProvider = operationTimeProvider ?? TimeProvider.System;
        _isSynchronizingFilmProfileWorkspace = true;
        _deviceSettingsInitializationTask = _deviceSettings.InitializeAsync();
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor dependencies={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        SelectedRows = "128";
        IsPreviewEnabled = true;
        IsAlternateMotorDirectionEnabled = true;
        SelectedCaptureMode = ScanDebugCaptureMode.Transport;
        IsScanLedAutoControlEnabled = true;
        IsWaterfallCompressedEnabled = true;
        IsGammaCorrectionEnabled = true;
        IsWhiteLevelPreviewEnabled = true;
        PreviewGamma = DefaultPreviewGamma.ToString("0.0");
        SelectedStartingDirection = DirectionOptions[0];
        SelectedScanMotor = MotorOptions[Math.Min(1, MotorOptions.Count - 1)];
        MotorDistancePerLineValue = string.Empty;
        MotorDistancePerLineUnit = MotorUnitMillimeters;
        MotorIntervalUs = FormatMotorIntervalInput(ScanDebugConstants.MotionDefaultIntervalNs);
        ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorUnavailableUntilParametersLoaded".GetLocalized();
        SelectedCalibrationChannel = CalibrationChannelOptions[0];
        InitializeCalibrationChannelItems();
        IsScanRecipeColorManagementEnabled = true;
        ScanRecipeRedWavelengthNm = "680";
        ScanRecipeGreenWavelengthNm = "525";
        ScanRecipeBlueWavelengthNm = "450";
        ScanRecipeOutputGamma = "2.2";
        SelectedScanRecipeTargetWhitePointMode = nameof(ScanTargetWhitePointMode.D65);
        ScanRecipeManualWhitePointColorTemperatureK = "6504";
        SelectedProfileAlignmentMode = AlignmentModeOptions[0];
        SelectedProfileDngExportMode = DngExportModeOptions[0];
        SelectedDebugDngExportMode = DngExportModeOptions[0];
        CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_NoSavedProfileLoadedYet".GetLocalizedFormat(GetCalibrationChannelDisplayName(CalibrationChannelOptions[0]));
        FilmProfileName = string.Empty;
        SelectedAdcRoiSelection = AdcRoiSelectionOptions[0];
        SelectedFocusRoiSelection = FocusRoiSelectionOptions[0];
        SelectedRoiSelection = SelectedAdcRoiSelection;
        IsBwActiveRoiOverlayVisible = true;
        IsBwShieldRoiOverlayVisible = true;
        IsFocusOverallRoiOverlayVisible = true;
        IsFocusLeftRoiOverlayVisible = true;
        IsFocusRightRoiOverlayVisible = true;
        IsImageReferenceOverlayVisible = true;
        RoiStatusText = string.Empty;
        RoiStartInput = "0";
        RoiEndInput = "0";
        RoiInputStatusText = "ScanDebug_Runtime_RoiInputsSynchronized".GetLocalized();
        ColumnSampleStartInput = ScanDebugConstants.EffectivePixelStart.ToString(CultureInfo.InvariantCulture);
        ColumnSampleEndInput = ScanDebugConstants.EffectivePixelEnd.ToString(CultureInfo.InvariantCulture);
        ColumnSampleStatusText = "ScanDebug_Runtime_ColumnSampleUnavailable".GetLocalized();
        StatusText = "ScanDebug_Runtime_StatusWaitingForDevicesShort".GetLocalized();
        DngAlignmentWarningMessage = string.Empty;
        ExposureTicks = string.Empty;
        Adc1Offset = string.Empty;
        Adc1Gain = string.Empty;
        Adc2Offset = string.Empty;
        Adc2Gain = string.Empty;
        SysClockKhz = string.Empty;
        ExposureTimeDisplay = "ScanDebug_Runtime_ExposureTimeIdle".GetLocalized();
        Adc1OffsetMvDisplay = "ScanDebug_Runtime_OffsetAmplitudeIdle".GetLocalized();
        Adc2OffsetMvDisplay = "ScanDebug_Runtime_OffsetAmplitudeIdle".GetLocalized();
        Adc1GainVvDisplay = "ScanDebug_Runtime_GainIdle".GetLocalized();
        Adc2GainVvDisplay = "ScanDebug_Runtime_GainIdle".GetLocalized();
        SysClockMhzDisplay = "ScanDebug_Runtime_SystemClockIdle".GetLocalized();
        Led1Level = "0";
        Led2Level = "0";
        Led3Level = "0";
        Led4Level = "0";
        Led1PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        Led2PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        Led3PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        Led4PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        RawLed1Level = "0";
        RawLed2Level = "0";
        RawLed3Level = "0";
        RawLed4Level = "0";
        RawLed1PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        RawLed2PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        RawLed3PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        RawLed4PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        SessionTestLedIndex = "1";
        SessionTestLedLevel = "0";
        SessionTestPulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor defaultProperties={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        RefreshActiveIlluminationChannels();
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor RefreshActiveIlluminationChannels={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        IlluminationSummaryText = "ScanDebug_Runtime_IlluminationSummaryIdle".GetLocalized();
        MotionSummaryText = "ScanDebug_Runtime_MotionSummaryIdle".GetLocalized();
        MotionStateReadRequiredText = string.Empty;
        Motor1RoleText = string.Empty;
        Motor2RoleText = string.Empty;
        Motor3RoleText = string.Empty;
        Motor1MoveSummaryText = string.Empty;
        Motor2MoveSummaryText = string.Empty;
        Motor3MoveSummaryText = string.Empty;
        Motor1ApplyConfigEffectText = string.Empty;
        Motor2ApplyConfigEffectText = string.Empty;
        Motor3ApplyConfigEffectText = string.Empty;
        Motor1StatusText = "ScanDebug_Runtime_MotorStatusIdle".GetLocalized();
        Motor2StatusText = "ScanDebug_Runtime_MotorStatusIdle".GetLocalized();
        Motor3StatusText = "ScanDebug_Runtime_MotorStatusIdle".GetLocalized();
        Motor1MoveDirection = MotorDirectionLabels[0];
        Motor2MoveDirection = MotorDirectionLabels[0];
        Motor3MoveDirection = MotorDirectionLabels[0];
        Motor1MoveValue = "200";
        Motor2MoveValue = "200";
        Motor3MoveValue = "200";
        Motor1MoveUnit = MotorUnitSteps;
        Motor2MoveUnit = MotorUnitSteps;
        Motor3MoveUnit = MotorUnitSteps;
        Motor1MoveSteps = "200";
        Motor2MoveSteps = "200";
        Motor3MoveSteps = "200";
        ApplyMotorSpeedFromIntervalNs(0, ScanDebugConstants.MotionDefaultIntervalNs);
        ApplyMotorSpeedFromIntervalNs(1, ScanDebugConstants.MotionDefaultIntervalNs);
        ApplyMotorSpeedFromIntervalNs(2, ScanDebugConstants.MotionDefaultIntervalNs);
        SelectedAutofocusPreset = ScanAutofocusPresetKind.Standard;
        ApplyAutofocusPresetToInputs(ScanAutofocusPresetCatalog.Get(ScanAutofocusPresetKind.Standard));
        ApplyFocusMappingInputs(_deviceSettings.DefaultSettings.FocusMotorMapping ?? new ScanFocusMotorMapping());
        ManualFocusDistanceMm = DefaultManualFocusDistanceMm.ToString("0.###", CultureInfo.InvariantCulture);
        AutofocusSummaryText = "ScanDebug_Runtime_AutofocusIdle".GetLocalized();
        FocusMappingStatusText = "ScanDebug_Runtime_FocusMappingDefaultStatus".GetLocalized();
        RefreshAutofocusBounds();
        RefreshRoiStatus();
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor hardwareDefaults={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        IsConnected = IsDeviceConnected;
        if (IsConnected && _sessionCoordinator.ConnectedSession is { } connectedSession)
        {
            _session = connectedSession;
        }
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor connectedSessionCheck={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        _session.RefreshTargets();
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
        RefreshPreviewSelectionState();
        RefreshTargets();
        UpdateMotorSemanticProjection();
        _isSynchronizingFilmProfileWorkspace = false;
        RefreshFilmProfileWorkspaceProjection();
        _ = InitializeTransferSettingsAsync();
        _ = InitializeDeviceSettingsProjectionAsync();
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor runtimeRefresh={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        totalStopwatch.Stop();
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor total={totalStopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    partial void OnMotor1SpeedValueChanged(string value)
    {
        OnMotorSpeedInputChanged(0);
        UpdateMotorSemanticProjection();
    }

    partial void OnMotor2SpeedValueChanged(string value)
    {
        OnMotorSpeedInputChanged(1);
        UpdateMotorSemanticProjection();
    }

    partial void OnMotor3SpeedValueChanged(string value)
    {
        OnMotorSpeedInputChanged(2);
        UpdateMotorSemanticProjection();
    }

    partial void OnMotor1SpeedUnitChanged(string value)
    {
        OnMotorSpeedInputChanged(0);
        UpdateMotorSemanticProjection();
    }

    partial void OnMotor2SpeedUnitChanged(string value)
    {
        OnMotorSpeedInputChanged(1);
        UpdateMotorSemanticProjection();
    }

    partial void OnMotor3SpeedUnitChanged(string value)
    {
        OnMotorSpeedInputChanged(2);
        UpdateMotorSemanticProjection();
    }

    partial void OnMotor1MoveDirectionChanged(string value) => UpdateMotorSemanticProjection();
    partial void OnMotor2MoveDirectionChanged(string value) => UpdateMotorSemanticProjection();
    partial void OnMotor3MoveDirectionChanged(string value) => UpdateMotorSemanticProjection();
    partial void OnMotor1MoveValueChanged(string value) => UpdateMotorSemanticProjection();
    partial void OnMotor2MoveValueChanged(string value) => UpdateMotorSemanticProjection();
    partial void OnMotor3MoveValueChanged(string value) => UpdateMotorSemanticProjection();
    partial void OnMotor1MoveUnitChanged(string value) => UpdateMotorSemanticProjection();
    partial void OnMotor2MoveUnitChanged(string value) => UpdateMotorSemanticProjection();
    partial void OnMotor3MoveUnitChanged(string value) => UpdateMotorSemanticProjection();

    partial void OnExposureTicksChanged(string value)
    {
        if (_isSynchronizingTimingInputs)
            return;

        UpdateComputedParameterDisplays();
        RefreshDerivedMotorDistanceFromCurrentInterval();
        UpdateComputedMotorSummary();
        RefreshCalibrationChannelItems();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnExposureMicrosecondsChanged(string value)
    {
        if (_isSynchronizingTimingInputs)
            return;

        UpdateTimingProjectionFromInputs(false);
    }

    partial void OnAdc1OffsetChanged(string value)
    {
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
        RefreshCalibrationChannelItems();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnAdc2OffsetChanged(string value)
    {
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
        RefreshCalibrationChannelItems();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnAdc1GainChanged(string value)
    {
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
        RefreshCalibrationChannelItems();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnAdc2GainChanged(string value)
    {
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
        RefreshCalibrationChannelItems();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnSysClockMhzChanged(string value)
    {
        if (_isSynchronizingTimingInputs)
            return;

        OnPropertyChanged(nameof(DeviceClockDisabledReasonText));
        UpdateTimingProjectionFromInputs(true);
    }

    partial void OnSysClockKhzChanged(string value)
    {
        if (_isSynchronizingTimingInputs)
            return;

        UpdateComputedParameterDisplays();
        RefreshDerivedMotorDistanceFromCurrentInterval();
        UpdateComputedMotorSummary();
        RefreshCalibrationChannelItems();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnAutofocusSampleRowsChanged(string value)
    {
        RefreshLimitBlockBindings();
        RefreshAutofocusBounds();
        NotifyAutofocusAvailabilityChanged();
    }

    partial void OnAutofocusTiltProbeStepsChanged(string value)
    {
        RefreshLimitBlockBindings();
        RefreshAutofocusBounds();
        NotifyAutofocusAvailabilityChanged();
    }

    partial void OnAutofocusZProbeStepsChanged(string value)
    {
        RefreshLimitBlockBindings();
        RefreshAutofocusBounds();
        NotifyAutofocusAvailabilityChanged();
    }

    partial void OnAutofocusMotorIntervalUsChanged(string value)
    {
        RefreshLimitBlockBindings();
        RefreshAutofocusBounds();
        NotifyAutofocusAvailabilityChanged();
    }

    partial void OnAutofocusMaxTiltIterationsChanged(string value)
    {
        RefreshAutofocusBounds();
        NotifyAutofocusAvailabilityChanged();
    }

    partial void OnAutofocusMaxZIterationsChanged(string value)
    {
        RefreshAutofocusBounds();
        NotifyAutofocusAvailabilityChanged();
    }

    partial void OnSelectedAutofocusPresetChanged(ScanAutofocusPresetKind value)
    {
        if (value != ScanAutofocusPresetKind.Custom)
            ApplyAutofocusPresetToInputs(ScanAutofocusPresetCatalog.Get(value));

        RefreshAutofocusBounds();
        OnPropertyChanged(nameof(IsCustomAutofocusPresetSelected));
        NotifyAutofocusAvailabilityChanged();
    }

    partial void OnFocusMappingLeftMotorChanged(string value)
        => OnFocusMappingInputChanged();

    partial void OnFocusMappingRightMotorChanged(string value)
        => OnFocusMappingInputChanged();

    partial void OnFocusMappingZPositiveDirectionChanged(string value)
        => OnFocusMappingInputChanged();

    partial void OnFocusMappingTiltPositiveDirectionChanged(string value)
    {
        OnFocusMappingInputChanged();
    }

    partial void OnManualFocusDistanceMmChanged(string value)
    {
        RefreshLimitBlockBindings();
        RefreshAutofocusBounds();
        OnPropertyChanged(nameof(CanStartManualFocusAction));
    }

    partial void OnIsWarmUpEnabledChanged(bool value)
    {
        SynchronizeFilmProfileDraftFromInputs();

        if (_suppressWarmUpToggleCommand)
            return;

        _ = HandleWarmUpToggleChangedAsync(value);
    }

    partial void OnSelectedRowsChanged(string value)
    {
        RefreshPreviewSelectionState();
        NotifyActionAvailabilityChanged();
        UpdateComputedMotorSummary();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnIsMultiChannelScanEnabledChanged(bool value)
    {
        NotifyScanWorkflowDependencyEditabilityChanged();
        NotifyAcquisitionPlanChanged();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnSelectedCaptureModeChanged(ScanDebugCaptureMode value)
    {
        OnPropertyChanged(nameof(IsContinuousScanEnabled));
        OnPropertyChanged(nameof(IsScanMotorTransportEnabled));
        NotifyCaptureModeAvailabilityChanged();
        NotifyScanWorkflowDependencyEditabilityChanged();
    }

    partial void OnIsScanLedAutoControlEnabledChanged(bool value)
    {
        if (!value)
            IsScanLedAutoControlEnabled = true;
    }

    partial void OnFilmProfileNameChanged(string value)
    {
        SynchronizeFilmProfileDraftFromInputs();
        NotifyProfileStateChanged();
    }

    partial void OnHasUnsavedProfileChangesChanged(bool value)
    {
        NotifyProfileStateChanged();
        NotifyStagedFilmProfileImportStateChanged();
    }

    partial void OnMotorDistancePerLineValueChanged(string value)
    {
        if (_isApplyingDerivedMotorDistance)
        {
            UpdateComputedMotorSummary();
            return;
        }

        _isMotorDistanceDerivedFromInterval = false;
        UpdateComputedMotorSummary();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnMotorDistancePerLineUnitChanged(string value)
    {
        var normalizedUnit = ScanMotorDistanceText.NormalizeUnit(value);
        var previousUnit = _lastMotorDistancePerLineUnit;
        _lastMotorDistancePerLineUnit = normalizedUnit;

        if (_isApplyingDerivedMotorDistance)
        {
            UpdateComputedMotorSummary();
            return;
        }

        if (_isMotorDistanceDerivedFromInterval)
        {
            RefreshDerivedMotorDistanceFromCurrentInterval();
            UpdateComputedMotorSummary();
            SynchronizeFilmProfileDraftFromInputs();
            return;
        }

        if (!string.Equals(previousUnit, normalizedUnit, StringComparison.Ordinal)
            && ScanMotorDistanceText.TryParseMillimeters(MotorDistancePerLineValue, previousUnit, GetCurrentScanMotorSettings(), out var lineDistanceMm)
            && ScanMotorDistanceText.TryFormatDisplayValue(lineDistanceMm, normalizedUnit, GetCurrentScanMotorSettings(), out var convertedValue))
        {
            ApplyDerivedMotorDistance(convertedValue);
        }

        UpdateComputedMotorSummary();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnMotorIntervalUsChanged(string value)
    {
        if (_isMotorDistanceDerivedFromInterval)
            RefreshDerivedMotorDistanceFromCurrentInterval();

        UpdateComputedMotorSummary();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnSelectedStartingDirectionChanged(string value)
    {
        NotifyAcquisitionPlanChanged();
        UpdateMotorSemanticProjection();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnSelectedScanMotorChanged(string value)
    {
        if (_isMotorDistanceDerivedFromInterval)
            RefreshDerivedMotorDistanceFromCurrentInterval();

        UpdateComputedMotorSummary();
        UpdateMotorSemanticProjection();
        SynchronizeFilmProfileDraftFromInputs();
    }

    partial void OnIsAlternateMotorDirectionEnabledChanged(bool value)
        => SynchronizeFilmProfileDraftFromInputs();

    partial void OnIsWhiteLevelPreviewEnabledChanged(bool value)
    {
        if (_hasValidScanBuffer && _previewRows > 0 && IsPreviewEnabled && !IsPreviewForcedOffForRows(_previewRows))
            RenderPreview(_previewRows);
    }

    partial void OnSelectedCalibrationChannelChanging(string value)
        => _calibrationChannelBeforeSelectionChange = SelectedCalibrationChannel;

    partial void OnSelectedCalibrationChannelChanged(string value)
    {
        if (_isSynchronizingFilmProfileWorkspace)
            return;

        InvalidateColumnSampleOwnership();
        ClearSelectedCalibrationEditorBaseline();
        SynchronizeFilmProfileDraftFromInputs(_calibrationChannelBeforeSelectionChange);
        _calibrationChannelBeforeSelectionChange = null;
        NotifyAcquisitionPlanChanged();
        OnPropertyChanged(nameof(CurrentCalibrationChannelSummaryText));
        OnPropertyChanged(nameof(SelectedCalibrationChannelItem));
        OnPropertyChanged(nameof(CalibrationCopySourceChannelOptions));
        if (string.Equals(SelectedCalibrationCopySourceChannel, value, StringComparison.OrdinalIgnoreCase))
            SelectedCalibrationCopySourceChannel = CalibrationCopySourceChannelOptions.FirstOrDefault();
        CopyCalibrationProfileFromChannelCommand.NotifyCanExecuteChanged();
        RefreshCalibrationChannelItems();
        NotifyCurrentCalibrationIlluminationChannelChanged();
        OnPropertyChanged(nameof(CurrentCalibrationChannelReversed));
        NotifyPreviewStatePropertiesChanged();
        _ = HandleSelectedCalibrationChannelChangedAsync(value, Volatile.Read(ref _calibrationProjectionVersion));
        InvalidatePendingCalibrationIfStale();
    }

    partial void OnIsChannel1ReversedChanged(bool value)
    {
        SynchronizeFilmProfileDraftFromInputs();
        NotifyCurrentCalibrationChannelReversedChanged(0);
    }

    partial void OnIsChannel2ReversedChanged(bool value)
    {
        SynchronizeFilmProfileDraftFromInputs();
        NotifyCurrentCalibrationChannelReversedChanged(1);
    }

    partial void OnIsChannel3ReversedChanged(bool value)
    {
        SynchronizeFilmProfileDraftFromInputs();
        NotifyCurrentCalibrationChannelReversedChanged(2);
    }

    partial void OnIsChannel4ReversedChanged(bool value)
    {
        SynchronizeFilmProfileDraftFromInputs();
        NotifyCurrentCalibrationChannelReversedChanged(3);
    }

    partial void OnIsScanRecipeColorManagementEnabledChanged(bool value)
        => SynchronizeFilmProfileDraftFromInputs();

    partial void OnScanRecipeRedWavelengthNmChanged(string value)
        => SynchronizeFilmProfileDraftFromInputs();

    partial void OnScanRecipeGreenWavelengthNmChanged(string value)
        => SynchronizeFilmProfileDraftFromInputs();

    partial void OnScanRecipeBlueWavelengthNmChanged(string value)
        => SynchronizeFilmProfileDraftFromInputs();

    partial void OnScanRecipeOutputGammaChanged(string value)
        => SynchronizeFilmProfileDraftFromInputs();

    partial void OnSelectedScanRecipeTargetWhitePointModeChanged(string value)
        => SynchronizeFilmProfileDraftFromInputs();

    partial void OnScanRecipeManualWhitePointColorTemperatureKChanged(string value)
        => SynchronizeFilmProfileDraftFromInputs();

    partial void OnSelectedProfileAlignmentModeChanged(ScanChannelAlignmentMode value)
        => SynchronizeFilmProfileDraftFromInputs();

    partial void OnSelectedProfileDngExportModeChanged(ScanDngExportMode value)
        => SynchronizeFilmProfileDraftFromInputs();

    partial void OnSelectedRoiSelectionChanged(string value)
    {
        SynchronizeOwnerRoiSelections(value);
        RefreshRoiInputTexts();
        RefreshRoiStatus();
    }

    partial void OnSelectedAdcRoiSelectionChanged(string value)
    {
        if (_isSynchronizingOwnerRoiSelections || value is not (RoiSelectionBwActive or RoiSelectionBwShield))
            return;

        SelectedRoiSelection = value;
    }

    partial void OnSelectedFocusRoiSelectionChanged(string value)
    {
        if (_isSynchronizingOwnerRoiSelections || value is not (RoiSelectionFocusOverall or RoiSelectionFocusLeft or RoiSelectionFocusRight))
            return;

        SelectedRoiSelection = value;
    }

    private void SynchronizeOwnerRoiSelections(string value)
    {
        _isSynchronizingOwnerRoiSelections = true;
        try
        {
            if (value is RoiSelectionBwActive or RoiSelectionBwShield)
                SelectedAdcRoiSelection = value;
            else if (value is RoiSelectionFocusOverall or RoiSelectionFocusLeft or RoiSelectionFocusRight)
                SelectedFocusRoiSelection = value;
        }
        finally
        {
            _isSynchronizingOwnerRoiSelections = false;
        }
    }

    partial void OnIsBwActiveRoiOverlayVisibleChanged(bool value)
        => RefreshRoiOverlayVisibility();

    partial void OnIsBwShieldRoiOverlayVisibleChanged(bool value)
        => RefreshRoiOverlayVisibility();

    partial void OnIsFocusOverallRoiOverlayVisibleChanged(bool value)
        => RefreshRoiOverlayVisibility();

    partial void OnIsFocusLeftRoiOverlayVisibleChanged(bool value)
        => RefreshRoiOverlayVisibility();

    partial void OnIsFocusRightRoiOverlayVisibleChanged(bool value)
        => RefreshRoiOverlayVisibility();

    partial void OnIsImageReferenceOverlayVisibleChanged(bool value)
        => RefreshRoiOverlayVisibility();

    partial void OnIsRoiEditModeEnabledChanged(bool value)
    {
        if (value && !CanEditRoiSelection)
        {
            IsRoiEditModeEnabled = false;
            return;
        }

        RefreshRoiStatus();
    }

    partial void OnIsColumnSampleEditModeEnabledChanged(bool value)
    {
        if (value && !CanEditColumnSampleSelection)
        {
            IsColumnSampleEditModeEnabled = false;
            return;
        }

        RefreshColumnSampleStatus();
    }

    partial void OnHasStagedFilmProfileImportChanged(bool value)
        => NotifyStagedFilmProfileImportStateChanged();

    partial void OnStagedFilmProfileImportSummaryChanged(string value)
        => NotifyStagedFilmProfileImportStateChanged();

    partial void OnStagedFilmProfileImportValidationIssuesChanged(IReadOnlyList<ScanFilmProfileValidationIssue> value)
        => NotifyStagedFilmProfileImportStateChanged();

    partial void OnIsFilmProfileOperationRunningChanged(bool value)
    {
        NotifyStagedFilmProfileImportStateChanged();
        NotifyRuntimeOperationAvailabilityChanged();
    }

    partial void OnIsCalibrationRepositoryOperationRunningChanged(bool value)
        => NotifyRuntimeOperationAvailabilityChanged();

    partial void OnFilmProfileOperationIsOpenChanged(bool value)
    {
        FilmProfileOperationVisibility = value ? Visibility.Visible : Visibility.Collapsed;
        if (!value)
            CancelFilmProfileOperationAutoCloseTimer();
    }

    partial void OnRoiStartInputChanged(string value)
    {
        if (!_isUpdatingRoiInputs)
        {
            RoiInputStatusText = "ScanDebug_Runtime_RoiRangeChanged".GetLocalized();
            RefreshCalibrationChannelItems();
            NotifyRoiEditCandidateAvailabilityChanged();
            NotifyRoiValidationIssuesChanged();
        }
    }

    partial void OnRoiEndInputChanged(string value)
    {
        if (!_isUpdatingRoiInputs)
        {
            RoiInputStatusText = "ScanDebug_Runtime_RoiRangeChanged".GetLocalized();
            RefreshCalibrationChannelItems();
            NotifyRoiEditCandidateAvailabilityChanged();
            NotifyRoiValidationIssuesChanged();
        }
    }

    partial void OnColumnSampleStartInputChanged(string value)
    {
        if (!_isUpdatingColumnSampleInputs)
            ApplyColumnSampleInputs();
    }

    partial void OnColumnSampleEndInputChanged(string value)
    {
        if (!_isUpdatingColumnSampleInputs)
            ApplyColumnSampleInputs();
    }

    partial void OnIsRunningChanged(bool value)
    {
        NotifyScanAcquisitionSettingsEditabilityChanged();
        OnPropertyChanged(nameof(IsAcquisitionRunning));
        NotifyActionAvailabilityChanged();
        NotifyPreviewStatePropertiesChanged();
    }

    partial void OnIsOutputOperationRunningChanged(bool value)
        => NotifyDeviceActionAvailabilityChanged();

    partial void OnIsDevicesPresentChanged(bool value)
    {
        OnPropertyChanged(nameof(DeviceStateText));
        NotifyDeviceActionAvailabilityChanged();
        NotifyPreviewStatePropertiesChanged();
    }

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(DeviceStateText));
        OnPropertyChanged(nameof(FilmProfileDeviceStatusText));
        OnPropertyChanged(nameof(FilmProfileHardwareUnavailableReasonText));
        NotifyDeviceActionAvailabilityChanged();
        NotifyPreviewStatePropertiesChanged();
    }

    partial void OnIsConnectingChanged(bool value)
    {
        NotifyScanAcquisitionSettingsEditabilityChanged();
        OnPropertyChanged(nameof(DeviceStateText));
        NotifyDeviceActionAvailabilityChanged();
    }

    partial void OnIsApplyingParametersChanged(bool value)
    {
        NotifyScanAcquisitionSettingsEditabilityChanged();
        NotifyActionAvailabilityChanged();
    }

    partial void OnIsApplyingDeviceClockChanged(bool value)
    {
        NotifyScanAcquisitionSettingsEditabilityChanged();
        NotifyActionAvailabilityChanged();
    }

    partial void OnIsAutoCalibratingChanged(bool value)
    {
        NotifyScanAcquisitionSettingsEditabilityChanged();
        NotifyActionAvailabilityChanged();
    }

    partial void OnIsAutoFocusingChanged(bool value)
    {
        NotifyScanAcquisitionSettingsEditabilityChanged();
        NotifyDeviceActionAvailabilityChanged();
    }

    partial void OnIsManualFocusingChanged(bool value)
    {
        NotifyScanAcquisitionSettingsEditabilityChanged();
        NotifyDeviceActionAvailabilityChanged();
    }

    partial void OnIsApplyingIlluminationChanged(bool value)
    {
        NotifyScanAcquisitionSettingsEditabilityChanged();
        NotifyDeviceActionAvailabilityChanged();
    }

    partial void OnIsApplyingMotionChanged(bool value)
    {
        NotifyScanAcquisitionSettingsEditabilityChanged();
        NotifyDeviceActionAvailabilityChanged();
    }

    partial void OnIsUpdatingFocusMappingChanged(bool value)
    {
        NotifyDeviceActionAvailabilityChanged();
        OnPropertyChanged(nameof(CanSaveFocusMappingAction));
        OnPropertyChanged(nameof(CanTestFocusMappingAction));
    }

    partial void OnIsPreviewEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPreviewToggleEnabled));
        OnPropertyChanged(nameof(IsPreviewEnabledForCurrentRows));
        OnPropertyChanged(nameof(CanEditRoiSelection));
        OnPropertyChanged(nameof(CanEditColumnSampleSelection));

        EnsureRoiEditModeAvailability();
        EnsureColumnSampleEditModeAvailability();

        if (!value)
            ClearPreview();
        else if (_hasValidScanBuffer && _previewRows > 0 && !IsPreviewForcedOffForRows(_previewRows))
            RenderPreview(_previewRows);
    }

    partial void OnIsWaterfallEnabledChanged(bool value)
    {
        if (value && GetSelectedAcquisitionChannelCount() > 1)
        {
            IsWaterfallEnabled = false;
            return;
        }

        OnPropertyChanged(nameof(WaterfallPreviewOptionsVisibility));
        OnPropertyChanged(nameof(CanEditRoiSelection));
        OnPropertyChanged(nameof(CanEditColumnSampleSelection));
        EnsureRoiEditModeAvailability();
        EnsureColumnSampleEditModeAvailability();
        _previewPresenter.Reset();

        if (!_hasValidScanBuffer || _previewRows <= 0 || !IsPreviewEnabled || IsPreviewForcedOffForRows(_previewRows))
            return;

        RenderPreview(_previewRows);
    }

    partial void OnIsWaterfallCompressedEnabledChanged(bool value)
    {
        _previewPresenter.Reset();

        if (!_hasValidScanBuffer || _previewRows <= 0 || !IsPreviewEnabled || !IsWaterfallEnabled || IsPreviewForcedOffForRows(_previewRows))
            return;

        RenderPreview(_previewRows);
    }

    partial void OnIsGammaCorrectionEnabledChanged(bool value)
    {
        if (!_hasValidScanBuffer || _previewRows <= 0 || !IsPreviewEnabled || IsPreviewForcedOffForRows(_previewRows))
            return;

        RenderPreview(_previewRows);
    }

    partial void OnPreviewGammaChanged(string value)
    {
        if (!_hasValidScanBuffer || _previewRows <= 0 || !IsPreviewEnabled || IsPreviewForcedOffForRows(_previewRows))
            return;

        if (!IsGammaCorrectionEnabled)
            return;

        RenderPreview(_previewRows);
    }

    public bool IsPreviewToggleEnabled => !IsPreviewForcedOffForSelectedRows();

    public bool IsPreviewEnabledForCurrentRows => IsPreviewEnabled && !IsPreviewForcedOffForSelectedRows();

    public bool HasPreviewImage => PreviewFrame is { Width: > 0, Height: > 0 };

    public Visibility PreviewEmptyStateVisibility => HasPreviewImage ? Visibility.Collapsed : Visibility.Visible;

    public string PreviewEmptyStateTitleText => BuildPreviewEmptyStateTitle();

    public string PreviewEmptyStateDescriptionText => BuildPreviewEmptyStateDescription();

    public bool IsDeviceConnectActionAvailable => CanConnectDevices();

    public bool IsDeviceDisconnectActionAvailable => CanDisconnectDevices();

    public bool IsStartActionAvailable => CanStartScan();

    public bool IsStopActionAvailable => CanStopScan();

    public bool IsExportDngActionAvailable => CanExportDng();

    public bool CanStartManualFocusAction => CanUseManualFocusSurface() && TryBuildManualFocusRequest(positive: true, out _, out _);

    public bool CanRunAutoFocusAction => CanRunAutoFocus();

    public string CurrentProfileNameText => string.IsNullOrWhiteSpace(FilmProfileName)
        ? "ScanDebug_Runtime_FilmProfileUntitled".GetLocalized()
        : FilmProfileName;

    private static string GetFilmProfileDisplayName(string? profileName)
        => string.IsNullOrWhiteSpace(profileName)
            ? "ScanDebug_Runtime_FilmProfileUntitled".GetLocalized()
            : profileName.Trim();

    public string ProfileSaveStateText => HasUnsavedProfileChanges
        ? "ScanDebug_ProfileStateUnsaved".GetLocalized()
        : "ScanDebug_ProfileStateSaved".GetLocalized();

    public string AcquisitionPlanSummaryText
    {
        get
        {
            var selectedCount = GetSelectedAcquisitionChannelsForSummary().Length;
            if (selectedCount == 0)
                return "ScanDebug_AcquisitionPlanNoActiveChannels".GetLocalized();

            return selectedCount > 1
                ? "ScanDebug_AcquisitionPlanSelectedChannels".GetLocalizedFormat(selectedCount)
                : "ScanDebug_AcquisitionPlanCurrentChannel".GetLocalizedFormat(AcquisitionChannelOrderText);
        }
    }

    public string AcquisitionChannelOrderText
    {
        get
        {
            var roles = GetSelectedAcquisitionChannelsForSummary()
                .Select(channel => GetCalibrationChannelDisplayName(channel.Role))
                .ToArray();
            return roles.Length == 0
                ? "ScanDebug_AcquisitionPlanNoActiveChannels".GetLocalized()
                : string.Join(" -> ", roles);
        }
    }

    public string CurrentCalibrationChannelSummaryText => "ScanDebug_ChannelCalibrationCurrentSummary".GetLocalizedFormat(
        GetCalibrationChannelDisplayName(SelectedCalibrationChannel),
        GetBoundLedName(SelectedCalibrationChannel));

    public ScanDebugIlluminationChannelViewModel? CurrentCalibrationIlluminationChannel
        => ActiveIlluminationChannels.FirstOrDefault(channel => string.Equals(channel.Role, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase));

    public bool HasCurrentCalibrationIlluminationChannel => CurrentCalibrationIlluminationChannel is not null;

    public bool CurrentCalibrationChannelReversed
    {
        get => CurrentCalibrationIlluminationChannel?.LedIndex switch
        {
            0 => IsChannel1Reversed,
            1 => IsChannel2Reversed,
            2 => IsChannel3Reversed,
            3 => IsChannel4Reversed,
            _ => false
        };
        set
        {
            switch (CurrentCalibrationIlluminationChannel?.LedIndex)
            {
                case 0:
                    IsChannel1Reversed = value;
                    break;
                case 1:
                    IsChannel2Reversed = value;
                    break;
                case 2:
                    IsChannel3Reversed = value;
                    break;
                case 3:
                    IsChannel4Reversed = value;
                    break;
                default:
                    return;
            }

            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> CurrentCalibrationIlluminationWorkModeOptions => IlluminationWorkModeOptions;

    public string CurrentCalibrationIlluminationLevel
    {
        get => CurrentCalibrationIlluminationChannel?.Level ?? string.Empty;
        set
        {
            var channel = CurrentCalibrationIlluminationChannel;
            if (channel is null)
                return;

            channel.Level = value;
            OnPropertyChanged();
        }
    }

    public string CurrentCalibrationIlluminationPulseClock
    {
        get => CurrentCalibrationIlluminationChannel?.PulseClock ?? string.Empty;
        set
        {
            var channel = CurrentCalibrationIlluminationChannel;
            if (channel is null)
                return;

            channel.PulseClock = value;
            OnPropertyChanged();
        }
    }

    public string CurrentCalibrationIlluminationWorkMode
    {
        get => CurrentCalibrationIlluminationChannel?.WorkMode ?? IlluminationWorkModeOptions[0];
        set
        {
            var channel = CurrentCalibrationIlluminationChannel;
            if (channel is null)
                return;

            channel.WorkMode = value;
            OnPropertyChanged();
        }
    }

    public string ChannelLedBindingSummaryText
    {
        get
        {
            var bindings = GetSelectedAcquisitionChannelsForSummary()
                .Select(channel => channel.ChannelLedBindingText);
            return string.Join(" / ", bindings);
        }
    }

    public string ProfileChannelOverviewText => BuildProfileChannelOverviewText();

    public string StartDisabledReasonText => CanStartScan()
        ? "ScanDebug_DisabledReasonReady".GetLocalized()
        : BuildStartDisabledReason();

    public string ExportDngDisabledReasonText => CanExportDng()
        ? "ScanDebug_DisabledReasonReady".GetLocalized()
        : BuildExportDngDisabledReason();

    public string DeviceClockDisabledReasonText => CanApplyDeviceClock()
        ? "ScanDebug_Runtime_DeviceClockReadyReason".GetLocalized()
        : BuildDeviceClockDisabledReason();

    public string ChannelParametersDisabledReasonText => CanApplyParameters()
        ? "ScanDebug_Runtime_ChannelParametersReadyReason".GetLocalized()
        : BuildChannelParametersDisabledReason();

    public string SessionIlluminationDisabledReasonText => CanApplyIllumination()
        ? "ScanDebug_Runtime_SessionIlluminationReadyReason".GetLocalized()
        : BuildSessionIlluminationDisabledReason();

    public string SaveProfileDisabledReasonText => "ScanDebug_DisabledReasonReady".GetLocalized();

    public bool IsAcquisitionRunning => IsRunning;

    public bool IsPreviewImageAvailable => HasPreviewImage;

    public bool AreScanAcquisitionSettingsEditable =>
        !IsRunning &&
        !IsConnecting &&
        !IsApplyingDeviceClock &&
        !IsApplyingParameters &&
        !IsAutoCalibrating &&
        !IsAutoFocusing &&
        !IsManualFocusing &&
        !IsApplyingIllumination &&
        !IsApplyingMotion;

    public bool CanEditCaptureMode => AreScanAcquisitionSettingsEditable;

    public bool CanEditScanLedAutoControl => false;

    public bool CanEditWaterfall => AreScanAcquisitionSettingsEditable && GetSelectedAcquisitionChannelCount() == 1;

    public Visibility WaterfallPreviewOptionsVisibility => IsWaterfallEnabled ? Visibility.Visible : Visibility.Collapsed;

    public string CaptureModeScopeText => "ScanDebug_CaptureModeScopeText".GetLocalized();

    public string CaptureModeUnavailableReasonText => BuildCaptureModeUnavailableReason();

    public bool CanEditRoiSelection => PreviewFrame is not null && !IsWaterfallEnabled && IsPreviewEnabled;

    public bool CanMutateRoiFromPreview => CanEditRoiSelection && IsRoiEditModeEnabled;

    public bool CanEditColumnSampleSelection => PreviewFrame is not null && !IsWaterfallEnabled && IsPreviewEnabled;

    public bool CanMutateColumnSampleFromPreview => CanEditColumnSampleSelection && IsColumnSampleEditModeEnabled;

    private void OnSessionTargetsChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(RefreshTargets);

    private void OnTransferSettingsChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(() =>
        {
            _isMultiBufferedBulkInEnabled = _transferSettings.Settings.ReadMode == ScanBulkInReadMode.MultiBuffered;
            StartScanCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(IsStartActionAvailable));
        });

    private void OnRuntimeOperationClaimsChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(NotifyRuntimeOperationAvailabilityChanged);

    public void AttachRuntimeBindings()
    {
        SubscribeFilmProfileWorkspace();
        if (_areRuntimeBindingsAttached)
            return;

        _session.TargetsChanged += OnSessionTargetsChanged;
        _transferSettings.BulkInReadModeChanged += OnTransferSettingsChanged;
        _runtimeOperationClaims.Changed += OnRuntimeOperationClaimsChanged;
        _areRuntimeBindingsAttached = true;
    }

    private void DetachRuntimeBindings()
    {
        if (!_areRuntimeBindingsAttached)
            return;

        _session.TargetsChanged -= OnSessionTargetsChanged;
        _transferSettings.BulkInReadModeChanged -= OnTransferSettingsChanged;
        _runtimeOperationClaims.Changed -= OnRuntimeOperationClaimsChanged;
        _areRuntimeBindingsAttached = false;
    }

    private void SubscribeFilmProfileWorkspace()
    {
        if (_isFilmProfileWorkspaceSubscribed)
            return;

        _filmProfileWorkspace.SnapshotChanged += OnFilmProfileWorkspaceSnapshotChanged;
        _isFilmProfileWorkspaceSubscribed = true;
        OnFilmProfileWorkspaceSnapshotChanged(_filmProfileWorkspace.Snapshot);
    }

    private void UnsubscribeFilmProfileWorkspace()
    {
        if (!_isFilmProfileWorkspaceSubscribed)
            return;

        _filmProfileWorkspace.SnapshotChanged -= OnFilmProfileWorkspaceSnapshotChanged;
        _isFilmProfileWorkspaceSubscribed = false;
    }

    private void OnFilmProfileWorkspaceSnapshotChanged(ScanFilmProfileWorkspaceSnapshot snapshot)
    {
        Interlocked.Increment(ref _calibrationWorkspaceGeneration);
        ApplyExternalFilmProfileWorkspaceSnapshot(snapshot);
        InvalidatePendingCalibrationIfStale();
    }

    private void ApplyExternalFilmProfileWorkspaceSnapshot(ScanFilmProfileWorkspaceSnapshot snapshot)
    {
        var currentDraftChanged = !ReferenceEquals(snapshot.CurrentDraft, _lastProjectedFilmProfileDraft);
        var channelProfileDataChanged = !HaveSameNormalizedCalibrationChannelProfiles(
            _lastProjectedFilmProfileDraft?.ChannelProfiles,
            snapshot.CurrentDraft.ChannelProfiles);
        var importResultChanged = !ReferenceEquals(snapshot.ImportResult, _lastProjectedFilmProfileImportResult);
        _lastProjectedFilmProfileDraft = snapshot.CurrentDraft;
        _lastProjectedFilmProfileImportResult = snapshot.ImportResult;

        HasStagedFilmProfileImport = snapshot.StagedImport is not null;
        StagedFilmProfileImportSummary = snapshot.StagedImport?.Draft.ProfileName ?? string.Empty;

        if (importResultChanged)
            SetStagedFilmProfileImportValidation(snapshot.ImportResult.Validation);

        if (currentDraftChanged)
        {
            InvalidateColumnSampleOwnership();
            if (channelProfileDataChanged)
                _copiedUnverifiedCalibrationProfiles.Clear();
            ApplyDraftToFields(snapshot.CurrentDraft);
            SetCurrentFilmProfileValidation(_filmProfileWorkspace.BuildExportDocument().Document.Validation);
            return;
        }

        RefreshFilmProfileWorkspaceProjection();
    }

    private void SwitchOperationalSession(IScanSessionService session)
    {
        if (ReferenceEquals(_session, session))
            return;

        var reattachTargets = _areRuntimeBindingsAttached;
        if (reattachTargets)
            _session.TargetsChanged -= OnSessionTargetsChanged;

        _session = session;

        if (reattachTargets)
            _session.TargetsChanged += OnSessionTargetsChanged;

        _session.RefreshTargets();
        RefreshLimitBlockBindings();
        RefreshTargets();
    }

    private void SwitchToConnectedSession()
    {
        if (_sessionCoordinator.ConnectedSession is { } connectedSession)
            SwitchOperationalSession(connectedSession);
    }

    private void SwitchToDiscoverySession()
        => SwitchOperationalSession(_discoverySession);

    private void RefreshTargets()
    {
        IsDevicesPresent = _session.Targets.IsDevicesPresent;
        RefreshLimitBlockBindings();

        if (!IsConnected)
        {
            StatusText = IsDevicesPresent
                ? "ScanDebug_Runtime_StatusDevicesDetected".GetLocalized()
                : "ScanDebug_Runtime_StatusWaitingForDevices".GetLocalized();
        }

        ConnectDevicesCommand.NotifyCanExecuteChanged();
        DisconnectDevicesCommand.NotifyCanExecuteChanged();
        StartScanCommand.NotifyCanExecuteChanged();
        NotifyDeviceActionAvailabilityChanged();
    }

    public ScanDebugRuntimeOperationSnapshot RuntimeOperationSnapshot
        => _runtimeOperationClaims.CreateSnapshot(CreateDerivedRuntimeOperationSnapshot());

    public ScanDebugRuntimeOperationGateResult GetRuntimeOperationGateResult(ScanDebugRuntimeCommandKind command)
        => ScanDebugRuntimeOperationGate.Evaluate(RuntimeOperationSnapshot, command);

    private bool IsDeviceConnected
        => _sessionCoordinator.Snapshot.State is ScannerSessionState.Connected or ScannerSessionState.Running;

    private ScanDebugRuntimeOperationSnapshot CreateDerivedRuntimeOperationSnapshot()
    {
        var activeOperations = new HashSet<ScanDebugRuntimeOperation>();
        if (IsRunning)
            activeOperations.Add(ScanDebugRuntimeOperation.Scan);
        if (IsOutputOperationRunning)
            activeOperations.Add(ScanDebugRuntimeOperation.OutputCapture);
        if (IsApplyingParameters)
            activeOperations.Add(ScanDebugRuntimeOperation.ParameterApplication);
        if (IsApplyingDeviceClock)
            activeOperations.Add(ScanDebugRuntimeOperation.DeviceGlobal);
        if (IsAutoCalibrating)
            activeOperations.Add(ScanDebugRuntimeOperation.AutoCalibration);
        if (IsAutoFocusing)
            activeOperations.Add(ScanDebugRuntimeOperation.AutoFocus);
        if (IsManualFocusing)
            activeOperations.Add(ScanDebugRuntimeOperation.ManualFocus);
        if (IsApplyingIllumination)
            activeOperations.Add(ScanDebugRuntimeOperation.Illumination);
        if (IsApplyingMotion)
            activeOperations.Add(ScanDebugRuntimeOperation.Motor);
        if (IsUpdatingFocusMapping)
            activeOperations.Add(ScanDebugRuntimeOperation.DeviceGlobal);
        if (IsFilmProfileOperationRunning)
            activeOperations.Add(ScanDebugRuntimeOperation.ProfileLifecycle);
        if (IsCalibrationRepositoryOperationRunning)
            activeOperations.Add(ScanDebugRuntimeOperation.CalibrationRepository);
        var sessionOperation = ScanDebugRuntimeOperationGate.ProjectSessionOperation(_sessionCoordinator.Snapshot);
        if (sessionOperation != ScanDebugRuntimeOperation.None)
            activeOperations.Add(sessionOperation);

        return new ScanDebugRuntimeOperationSnapshot(
            IsDeviceConnected,
            activeOperations);
    }

    private bool CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind command)
        => (!IsMotionProducingCommand(command) || !_motionRuntimeState.RequiresRead)
            && (!IsSelectedChannelParameterApplyCommand(command) || HasDeviceKnownTimingForSelectedChannelParameterApply())
            && !(IsTimingHazardousDeviceCommand(command) && HasUnsafeDeviceTimingForActiveRoles(GetActiveRoles(BuildDebugChannelAssignment())))
            && GetRuntimeOperationGateResult(command).CanExecute;

    private static bool IsSelectedChannelParameterApplyCommand(ScanDebugRuntimeCommandKind command)
        => command is ScanDebugRuntimeCommandKind.ApplyParameters;

    private bool HasDeviceKnownTimingForSelectedChannelParameterApply()
        => _deviceTimingState.StateKind is ScanDeviceClockStateKind.DeviceKnown;

    private static bool IsMotionProducingCommand(ScanDebugRuntimeCommandKind command)
        => command is ScanDebugRuntimeCommandKind.StartScan
            or ScanDebugRuntimeCommandKind.EnableMotor
            or ScanDebugRuntimeCommandKind.MoveMotor
            or ScanDebugRuntimeCommandKind.ApplyMotorConfig
            or ScanDebugRuntimeCommandKind.AutoBlackAdjust
            or ScanDebugRuntimeCommandKind.AutoWhiteAdjust
            or ScanDebugRuntimeCommandKind.AutoCalibrate
            or ScanDebugRuntimeCommandKind.AutoFocus
            or ScanDebugRuntimeCommandKind.StartManualFocus;

    private static bool IsTimingHazardousDeviceCommand(ScanDebugRuntimeCommandKind command)
        => command is ScanDebugRuntimeCommandKind.StartScan
            or ScanDebugRuntimeCommandKind.ApplyIllumination
            or ScanDebugRuntimeCommandKind.EnableMotor
            or ScanDebugRuntimeCommandKind.MoveMotor
            or ScanDebugRuntimeCommandKind.ApplyMotorConfig
            or ScanDebugRuntimeCommandKind.AutoBlackAdjust
            or ScanDebugRuntimeCommandKind.AutoWhiteAdjust
            or ScanDebugRuntimeCommandKind.AutoCalibrate
            or ScanDebugRuntimeCommandKind.AutoFocus
            or ScanDebugRuntimeCommandKind.StartManualFocus
            or ScanDebugRuntimeCommandKind.DeviceGlobalWrite;

    private bool HasUnsafeDeviceTimingForActiveRoles(IEnumerable<string> activeRoles)
        => _deviceTimingState.StateKind is ScanDeviceClockStateKind.Edited or ScanDeviceClockStateKind.ReadRequired
            || activeRoles.Any(_deviceTimingState.IsRevalidationRequired);

    private bool TryClaimRuntimeOperation(
        ScanDebugRuntimeCommandKind command,
        out ScanDebugRuntimeOperationClaims.ScanDebugRuntimeOperationLease? lease)
    {
        lease = null;
        if (IsMotionProducingCommand(command) && _motionRuntimeState.RequiresRead)
            return false;
        if (IsSelectedChannelParameterApplyCommand(command) && !HasDeviceKnownTimingForSelectedChannelParameterApply())
            return false;
        if (IsTimingHazardousDeviceCommand(command) && HasUnsafeDeviceTimingForActiveRoles(GetActiveRoles(BuildDebugChannelAssignment())))
            return false;

        return _runtimeOperationClaims.TryClaim(CreateDerivedRuntimeOperationSnapshot(), command, out lease, out _);
    }

    private void ClearRuntimeOperationClaims()
        => _runtimeOperationClaims.ClearDeviceBoundClaims();

    private void OnSessionCoordinatorSnapshotChanged(object? sender, ScannerDeviceSessionSnapshot snapshot)
        => _dispatcher.TryEnqueue(() => ApplySessionCoordinatorSnapshot(snapshot));

    private void ApplySessionCoordinatorSnapshot(ScannerDeviceSessionSnapshot snapshot)
    {
        if (snapshot.State == ScannerSessionState.Connecting)
        {
            IsConnecting = true;
            IsConnected = false;
            NotifyRuntimeOperationAvailabilityChanged();
            return;
        }

        if (snapshot.State is ScannerSessionState.Connected or ScannerSessionState.Running)
        {
            if (!string.Equals(_calibrationDeviceSessionId, snapshot.DeviceId, StringComparison.Ordinal))
            {
                _calibrationDeviceSessionId = snapshot.DeviceId;
                Interlocked.Increment(ref _calibrationDeviceSessionGeneration);
            }
            IsConnecting = false;
            IsConnected = true;
            SwitchToConnectedSession();
            NotifyRuntimeOperationAvailabilityChanged();
            return;
        }

        _scanCts?.Cancel();
        _autoFocusCts?.Cancel();
        _autoCalibrationCts?.Cancel();
        Interlocked.Increment(ref _calibrationDeviceSessionGeneration);
        _calibrationDeviceSessionId = null;
        InvalidatePendingCalibrationIfStale();
        _manualFocusCts?.Cancel();
        _focusMappingTestCts?.Cancel();
        _motionRuntimeState.AdvanceSession();
        NotifyMotionFreshnessChanged();
        ClearRuntimeOperationClaims();
        SwitchToDiscoverySession();
        IsConnecting = false;
        IsConnected = false;
        ResetIlluminationInputs();
        ResetMotionInputs();
        ClearDngAlignmentWarning();
        NotifyRuntimeOperationAvailabilityChanged();
    }

    private bool CanStartScan() =>
        CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.StartScan)
        && HasSelectedAcquisitionChannels()
        && IsCaptureModeCompatibleWithSelection()
        && TryParseRequestedRows(out _);

    private bool CanStopScan() => IsRunning && CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.StopScan);

    private bool CanExportDng() =>
        CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.ExportDng)
        && _hasValidScanBuffer &&
        _lineBuffer.Length > 0 &&
        CanExportCurrentDngCapture();

    private bool CanExportCurrentDngCapture()
    {
        if (_lastWorkflowResult is null)
            return true;

        var assignment = _lastWorkflowChannelAssignment ?? BuildCapturedWorkflowChannelAssignment(_lastWorkflowResult);
        var activeRoleCount = GetActiveRoles(assignment).Count();
        return activeRoleCount == 1 || activeRoleCount == ScanDebugConstants.IlluminationChannelCount;
    }

    private bool CanConnectDevices()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.ConnectDevices) && IsDevicesPresent && !IsDeviceConnected && !IsConnecting;

    private bool CanDisconnectDevices()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.DisconnectDevices) && IsDeviceConnected && !IsConnecting;

    private bool CanApplyParameters()
        => HasDeviceKnownTimingForSelectedChannelParameterApply()
            && CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.ApplyParameters);

    private bool CanApplyDeviceClock()
        => TryParseSysClockMhzText(SysClockMhz, out _)
            && CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.ApplyDeviceClock);

    private bool CanRefreshIllumination()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.RefreshIllumination);

    private bool CanApplyIllumination()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.ApplyIllumination);

    private bool CanRefreshMotion()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.RefreshMotion);

    private bool CanEnableMotor()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.EnableMotor);

    private bool CanDisableMotor()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.DisableMotor);

    private bool CanMoveMotor()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.MoveMotor);

    private bool CanStopMotor()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.StopMotor);

    private bool CanApplyMotorConfig()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.ApplyMotorConfig);

    private bool CanAutoBlackAdjust()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.AutoBlackAdjust)
            && IsRoiEditCandidateValid(ScanRoiOperationOwner.AdcCalibration);

    private bool CanAutoWhiteAdjust()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.AutoWhiteAdjust)
            && IsRoiEditCandidateValid(ScanRoiOperationOwner.AdcCalibration);

    private bool CanAutoCalibrate()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.AutoCalibrate)
            && IsRoiEditCandidateValid(ScanRoiOperationOwner.AdcCalibration);

    private bool CanRunAutoFocus()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.AutoFocus)
           && IsRoiEditCandidateValid(ScanRoiOperationOwner.AutoFocus)
           && TryBuildAutofocusRequest(out _, out _);

    private bool CanRunFilmProfileLifecycleOperation()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.NewFilmProfile);

    private bool CanSaveFilmProfile() =>
        CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.SaveFilmProfileJson)
        && IsCurrentFilmProfileValidationValid
        && !_hasInvalidFilmProfileInput;

    private bool CanLoadFilmProfile()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.LoadFilmProfileJson);

    private bool CanApplyStagedFilmProfileImportCommand()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.ApplyStagedFilmProfileImport)
            && HasStagedFilmProfileImport
            && IsStagedFilmProfileImportValid;

    private bool CanDiscardStagedFilmProfileImport()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.DiscardStagedFilmProfileImport)
            && HasPendingFilmProfileImportResult;

    private bool CanValidateFilmProfile()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.ValidateFilmProfile);

    private bool CanSaveChannelProfile()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.SaveChannelProfile);

    private bool CanClearChannelProfile()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.ClearChannelProfile);

    private bool CanSaveColumnSampleAsBlackLevel()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.SaveColumnSampleAsBlackLevel)
            && HasCurrentColumnSample();

    private bool CanSaveColumnSampleAsWhiteLevel()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.SaveColumnSampleAsWhiteLevel)
            && HasCurrentColumnSample();

    private bool CanEditProfileDraft()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.ApplySelectedRoiInputs);

    [RelayCommand(CanExecute = nameof(CanExportDng), IncludeCancelCommand = true)]
    private async Task ExportDng(CancellationToken cancellationToken)
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ExportDng, out var runtimeClaim))
            return;

        IsOutputOperationRunning = true;
        try
        {
            if (_lastWorkflowResult is not null)
            {
                var workflowAssignment = _lastWorkflowChannelAssignment ?? BuildCapturedWorkflowChannelAssignment(_lastWorkflowResult);
                var workflowRoles = GetActiveRoles(workflowAssignment).ToArray();
                if (workflowRoles.Length == 1)
                {
                    var pass = _lastWorkflowResult.Passes.FirstOrDefault();
                    if (pass is null)
                    {
                        StatusText = "ScanDebug_Runtime_StatusExportNoWorkflowPass".GetLocalized();
                        ClearDngAlignmentWarning();
                        return;
                    }

                    var monochromeFolder = await _channelImages.PickDngExportFolderAsync();
                    if (monochromeFolder is null)
                    {
                        StatusText = "ScanDebug_Runtime_StatusExportCanceled".GetLocalized();
                        ClearDngAlignmentWarning();
                        return;
                    }

                    var channelRole = workflowRoles[0];
                    var channelProfile = _calibrationProfiles.TryGetProfile(channelRole, out var profile) ? profile : null;
                    IsOutputOperationRunning = true;
                    await _channelImages.ExportMonochromeDngAsync(monochromeFolder, pass.ImageBytes, pass.Rows, _lastWorkflowResult.ExposureTicks, _lastWorkflowResult.SysClockKhz, channelRole, channelProfile, cancellationToken);
                    StatusText = "ScanDebug_Runtime_StatusMonochromeDngExported".GetLocalizedFormat(monochromeFolder.Path);
                    ClearDngAlignmentWarning();
                    return;
                }

                if (workflowRoles.Length != ScanDebugConstants.IlluminationChannelCount)
                {
                    StatusText = "ScanDebug_Runtime_StatusExportRequiresSingleOrFourChannelWorkflow".GetLocalizedFormat(workflowRoles.Length);
                    ClearDngAlignmentWarning();
                    return;
                }

                var workflowFolder = await _channelImages.PickDngExportFolderAsync();
                if (workflowFolder is null)
                {
                    StatusText = "ScanDebug_Runtime_StatusExportCanceled".GetLocalized();
                    ClearDngAlignmentWarning();
                    return;
                }

                IsOutputOperationRunning = true;
                var exportResult = await _channelImages.ExportDngChannelsAsync(workflowFolder, _lastWorkflowResult, workflowAssignment, ScanChannelAlignmentMode.Ecc, SelectedDebugDngExportMode, _calibrationProfiles.Snapshot.Profiles, cancellationToken);
                if (exportResult.HasAlignmentWarning)
                {
                    var affectedChannels = FormatDngAffectedChannelRoles(exportResult);
                    StatusText = "Scan_Runtime_StatusDngExportedWithAlignmentWarning".GetLocalizedFormat(workflowFolder.Path, affectedChannels);
                    SetDngAlignmentWarning("Scan_Runtime_DngAlignmentWarningText".GetLocalizedFormat(affectedChannels));
                }
                else
                {
                    StatusText = "Scan_Runtime_StatusDngExported".GetLocalizedFormat(workflowFolder.Path);
                    ClearDngAlignmentWarning();
                }
                return;
            }

            var dngFolder = await _channelImages.PickDngExportFolderAsync();
            if (dngFolder is null)
            {
                StatusText = "ScanDebug_Runtime_StatusExportCanceled".GetLocalized();
                ClearDngAlignmentWarning();
                return;
            }

            if (!ushort.TryParse(ExposureTicks, out var exposureTicks))
                exposureTicks = 0;
            if (!uint.TryParse(SysClockKhz, out var sysClockKhz))
                sysClockKhz = 0;

            var channelLabel = _lastMonochromeChannelRole ?? GetSingleSelectedAcquisitionChannelRole() ?? SelectedCalibrationChannel;
            var monochromeProfile = _calibrationProfiles.TryGetProfile(channelLabel, out var selectedProfile) ? selectedProfile : null;
            IsOutputOperationRunning = true;
            await _channelImages.ExportMonochromeDngAsync(dngFolder, _lineBuffer, _previewRows, exposureTicks, sysClockKhz, channelLabel, monochromeProfile, cancellationToken);
            StatusText = "ScanDebug_Runtime_StatusMonochromeDngExported".GetLocalizedFormat(dngFolder.Path);
            ClearDngAlignmentWarning();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "ScanDebug_Runtime_StatusExportCanceled".GetLocalized();
            ClearDngAlignmentWarning();
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusExportFailed".GetLocalizedFormat(ex.Message);
            ClearDngAlignmentWarning();
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsOutputOperationRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnectDevices))]
    private async Task ConnectDevices()
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ConnectDevices, out var runtimeClaim))
            return;

        try
        {
            if (_sessionCoordinator.IsConnectBlockedByUsbDebug())
            {
                await RequestNoticeAsync(
                    "Shared_Dialog_UsbBusy.Title".GetLocalized(),
                    "Shared_Dialog_UsbBusy_ScanDebugBlockedByUsbDebug.Content".GetLocalized(),
                    "Shared_Dialog_Ok.CloseButtonText".GetLocalized());
                StatusText = "ScanDebug_Runtime_StatusUsbDebugActive".GetLocalized();
                return;
            }

            IsConnecting = true;
            var result = await _sessionCoordinator.ConnectAsync(CancellationToken.None);
            if (!result.Success)
            {
                StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message);
                return;
            }

            SwitchToConnectedSession();

            IsConnected = true;
            StatusText = "ScanDebug_Runtime_StatusLoadingParameters".GetLocalized();

            var statusNotes = new List<string>();

            await EnsureDeviceSettingsInitializedAsync();
            await _calibrationProfiles.InitializeAsync(CancellationToken.None);
            var selectedCalibrationChannel = await _calibrationProfiles.GetSelectedChannelAsync(CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(selectedCalibrationChannel)
                && CalibrationChannelOptions.Contains(selectedCalibrationChannel, StringComparer.OrdinalIgnoreCase))
            {
                SelectedCalibrationChannel = selectedCalibrationChannel;
            }

            try
            {
                var snapshot = await _parameters.LoadAsync(_session, _session.ConnectionToken);
                ApplySnapshotToInputs(snapshot);
                SetDeviceTimingState(new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown));
                statusNotes.Add("ScanDebug_Runtime_StatusParametersLoaded".GetLocalized());
            }
            catch (Exception ex)
            {
                statusNotes.Add("ScanDebug_Runtime_StatusParameterLoadUnavailable".GetLocalizedFormat(ex.Message));
            }

            await LoadSelectedCalibrationProfileAsync(SelectedCalibrationChannel, ++_profileLoadVersion, Volatile.Read(ref _calibrationProjectionVersion));

            try
            {
                await LoadIlluminationStateAsync(_session.ConnectionToken);
                statusNotes.Add("ScanDebug_Runtime_StatusIlluminationLoaded".GetLocalized());
            }
            catch (Exception ex)
            {
                ResetIlluminationInputs();
                statusNotes.Add("ScanDebug_Runtime_StatusIlluminationUnavailable".GetLocalizedFormat(ex.Message));
            }

            try
            {
                await LoadMotionStateAsync(_session.ConnectionToken);
                statusNotes.Add("ScanDebug_Runtime_StatusMotionLoaded".GetLocalized());
            }
            catch (Exception ex)
            {
                ResetMotionInputs();
                statusNotes.Add("ScanDebug_Runtime_StatusMotionUnavailable".GetLocalizedFormat(ex.Message));
            }

            if (_selectedFilmAcquisitionSettings is not null)
                ApplyProfileAcquisitionSettings(_selectedFilmAcquisitionSettings);

            if (IsWarmUpEnabled)
            {
                var warmUpResult = await _sessionCoordinator.SetWarmUpAsync(true, _session.ConnectionToken);
                statusNotes.Add(warmUpResult.Success ? "ScanDebug_Runtime_StatusWarmUpEnabled".GetLocalized() : "ScanDebug_Runtime_StatusWarmUpFailed".GetLocalizedFormat(ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(warmUpResult.Message)));
            }

            StatusText = statusNotes.Count > 0
                ? "ScanDebug_Runtime_StatusConnectedWithNotes".GetLocalizedFormat(string.Join(". ", statusNotes))
                : "ScanDebug_Runtime_StatusConnected".GetLocalized();
        }
        catch (Exception ex)
        {
            await _sessionCoordinator.DisconnectAsync(CancellationToken.None);
            SwitchToDiscoverySession();
            IsConnected = false;
            StatusText = "ScanDebug_Runtime_StatusConnectFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsConnecting = false;
            ConnectDevicesCommand.NotifyCanExecuteChanged();
            DisconnectDevicesCommand.NotifyCanExecuteChanged();
            StartScanCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnectDevices))]
    private async Task DisconnectDevices()
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.DisconnectDevices, out var runtimeClaim))
            return;

        IsConnecting = true;
        try
        {
            _scanCts?.Cancel();

            if (IsWarmUpEnabled)
            {
                var warmUpResult = await _sessionCoordinator.SetWarmUpAsync(false, _session.ConnectionToken);
                _suppressWarmUpToggleCommand = true;
                try
                {
                    IsWarmUpEnabled = false;
                }
                finally
                {
                    _suppressWarmUpToggleCommand = false;
                }

                if (!warmUpResult.Success)
                    StatusText = "ScanDebug_Runtime_StatusWarmUpDisableBeforeDisconnectFailed".GetLocalizedFormat(ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(warmUpResult.Message));
            }

            var result = await _sessionCoordinator.DisconnectAsync(CancellationToken.None);
            if (!result.Success)
            {
                StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message);
                return;
            }

            SwitchToDiscoverySession();
            IsConnected = false;
            ResetIlluminationInputs();
            ResetMotionInputs();
            ClearDngAlignmentWarning();
            StatusText = IsDevicesPresent ? "ScanDebug_Runtime_StatusDisconnectedReconnect".GetLocalized() : "ScanDebug_Runtime_StatusDisconnected".GetLocalized();
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsConnecting = false;
            ConnectDevicesCommand.NotifyCanExecuteChanged();
            DisconnectDevicesCommand.NotifyCanExecuteChanged();
            StartScanCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyDeviceClock))]
    private async Task ApplyDeviceClock()
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        if (!TryParseSysClockMhzText(SysClockMhz, out var sysClockKhz))
        {
            StatusText = "ScanDebug_Runtime_StatusDeviceClockInvalid".GetLocalized();
            return;
        }

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ApplyDeviceClock, out var runtimeClaim))
            return;

        IsApplyingDeviceClock = true;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusApplyingDeviceClock".GetLocalized();
            await _sessionCoordinator.UseConnectedSessionAsync(
                async (session, token) =>
                {
                    await _parameters.ApplyGlobalClockAsync(session, sysClockKhz, token);
                    return true;
                },
                CancellationToken.None);

            if (TryParseSysClockMhzText(SysClockMhz, out var currentSysClockKhz) && currentSysClockKhz == sysClockKhz)
                SetDeviceTimingState(new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown, GetKnownCalibrationChannelRoles()));
            StatusText = "ScanDebug_Runtime_StatusDeviceClockUpdated".GetLocalized();
        }
        catch (OperationCanceledException)
        {
            SetDeviceTimingState(new ScanDeviceTimingState(ScanDeviceClockStateKind.ReadRequired, GetKnownCalibrationChannelRoles()));
            StatusText = "ScanDebug_Runtime_StatusDeviceClockUpdateCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
            SetDeviceTimingState(new ScanDeviceTimingState(ScanDeviceClockStateKind.ReadRequired, GetKnownCalibrationChannelRoles()));
            StatusText = "ScanDebug_Runtime_StatusDeviceClockUpdateFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsApplyingDeviceClock = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyParameters))]
    private async Task ApplyParameters()
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        var now = DateTime.UtcNow;
        if (now - _lastApplyParametersAtUtc < ParameterApplyDebounceWindow)
        {
            StatusText = "ScanDebug_Runtime_StatusApplyIgnoredDebounce".GetLocalized();
            return;
        }

        var appliedChannelRole = SelectedCalibrationChannel;
        var appliedTimingState = _deviceTimingState;
        if (!HasDeviceKnownTimingForSelectedChannelParameterApply())
            return;

        if (!_parameters.TryParseInput(ExposureMicroseconds, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockMhz, out var snapshot, out var parseError))
        {
            StatusText = parseError;
            return;
        }

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ApplyParameters, out var runtimeClaim))
            return;

        if (!CanSubmitCapturedSelectedChannelParameters(appliedTimingState, snapshot.SysClockKhz, appliedChannelRole))
        {
            runtimeClaim?.Dispose();
            return;
        }

        _lastApplyParametersAtUtc = now;

        IsApplyingParameters = true;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusApplyingParameters".GetLocalized();
            await _sessionCoordinator.UseConnectedSessionAsync(
                async (session, token) =>
                {
                    await _parameters.ApplyAsync(session, snapshot, token);
                    return true;
                },
                CancellationToken.None);
            if (HasCurrentChannelParameterApplyClockContext(snapshot.SysClockKhz))
            {
                var revalidationRequiredChannelRoles = _deviceTimingState.RevalidationRequiredChannelRoles
                    .Where(role => !string.Equals(role, appliedChannelRole, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                SetDeviceTimingState(new ScanDeviceTimingState(
                    ScanDeviceClockStateKind.DeviceKnown,
                    revalidationRequiredChannelRoles));
            }
            StatusText = "ScanDebug_Runtime_StatusParametersUpdated".GetLocalized();
        }
        catch (OperationCanceledException)
        {
            SetDeviceTimingState(new ScanDeviceTimingState(ScanDeviceClockStateKind.ReadRequired, GetKnownCalibrationChannelRoles()));
            StatusText = "ScanDebug_Runtime_StatusParameterUpdateCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
            SetDeviceTimingState(new ScanDeviceTimingState(ScanDeviceClockStateKind.ReadRequired, GetKnownCalibrationChannelRoles()));
            StatusText = "ScanDebug_Runtime_StatusParameterUpdateFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsApplyingParameters = false;
        }
    }

    private bool CanSubmitCapturedSelectedChannelParameters(
        ScanDeviceTimingState appliedTimingState,
        uint appliedSysClockKhz,
        string appliedChannelRole)
        => appliedTimingState.StateKind is ScanDeviceClockStateKind.DeviceKnown
            && HasCurrentChannelParameterApplyClockContext(appliedSysClockKhz)
            && string.Equals(SelectedCalibrationChannel, appliedChannelRole, StringComparison.OrdinalIgnoreCase);

    private bool HasCurrentChannelParameterApplyClockContext(uint appliedSysClockKhz)
        => _deviceTimingState.StateKind is ScanDeviceClockStateKind.DeviceKnown
            && TryParseSysClockMhzText(SysClockMhz, out var currentSysClockKhz)
            && currentSysClockKhz == appliedSysClockKhz;

    [RelayCommand(CanExecute = nameof(CanRefreshIllumination))]
    private async Task RefreshIllumination()
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.RefreshIllumination, out var runtimeClaim))
            return;

        IsApplyingIllumination = true;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusRefreshingIllumination".GetLocalized();
            await LoadIlluminationStateAsync(_session.ConnectionToken);
            StatusText = "ScanDebug_Runtime_StatusIlluminationRefreshed".GetLocalized();
        }
        catch (OperationCanceledException)
        {
            StatusText = "ScanDebug_Runtime_StatusIlluminationRefreshCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusIlluminationRefreshFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsApplyingIllumination = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyIllumination))]
    private async Task ApplyIllumination()
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        await EnsureDeviceSettingsInitializedAsync();

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ApplyIllumination, out var runtimeClaim))
            return;

        if (!TryBuildRawIlluminationState(out var state, out var error))
        {
            runtimeClaim?.Dispose();
            StatusText = error;
            return;
        }

        IsApplyingIllumination = true;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusApplyingIllumination".GetLocalized();

            await _sessionCoordinator.UseConnectedSessionAsync(
                async (session, token) =>
                {
                    await _illumination.ApplyStateWithSafeTransitionAsync(session, state, token);
                    return true;
                },
                CancellationToken.None);

            await LoadIlluminationStateAsync(_session.ConnectionToken);
            StatusText = "ScanDebug_Runtime_StatusIlluminationUpdated".GetLocalized();
        }
        catch (OperationCanceledException)
        {
            StatusText = "ScanDebug_Runtime_StatusIlluminationUpdateCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusIlluminationUpdateFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsApplyingIllumination = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefreshMotion))]
    private async Task RefreshMotion()
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.RefreshMotion, out var runtimeClaim))
            return;

        IsApplyingMotion = true;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusRefreshingMotion".GetLocalized();
            await LoadMotionStateAsync(_session.ConnectionToken);
            StatusText = "ScanDebug_Runtime_StatusMotionRefreshed".GetLocalized();
        }
        catch (OperationCanceledException)
        {
            StatusText = "ScanDebug_Runtime_StatusMotionRefreshCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusMotionRefreshFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsApplyingMotion = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEnableMotor))]
    private async Task EnableMotor(string? motorDisplayId)
    {
        await SetMotorEnabledCoreAsync(motorDisplayId, true);
    }

    [RelayCommand(CanExecute = nameof(CanDisableMotor))]
    private async Task DisableMotor(string? motorDisplayId)
    {
        await SetMotorEnabledCoreAsync(motorDisplayId, false);
    }

    [RelayCommand(CanExecute = nameof(CanMoveMotor))]
    private async Task MoveMotor(string? motorDisplayId)
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        if (!TryParseMotorTarget(motorDisplayId, out var motorId, out var motorName, out var targetError))
        {
            StatusText = targetError;
            return;
        }

        var producer = _motionRuntimeState.CaptureProducer();
        await EnsureDeviceSettingsInitializedAsync();
        if (!_motionRuntimeState.IsCurrent(producer))
            return;

        if (!TryBuildMotorMoveRequest(motorId, out var request, out var error))
        {
            StatusText = error;
            return;
        }

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.MoveMotor, out var runtimeClaim))
            return;

        if (!_motionRuntimeState.IsCurrent(producer))
        {
            runtimeClaim?.Dispose();
            return;
        }

        IsApplyingMotion = true;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusMotorMoveStarting".GetLocalizedFormat(motorName);
            await _sessionCoordinator.UseConnectedSessionAsync(
                async (session, token) =>
                {
                    producer.CancellationToken.ThrowIfCancellationRequested();
                    await session.MoveMotorStepsAndWaitForCompletionAsync(motorId, request.Direction, request.Steps, request.IntervalNs, token);
                    return true;
                },
                producer.CancellationToken);
            await LoadMotionStateAsync(_session.ConnectionToken);
            StatusText = "ScanDebug_Runtime_StatusMotorMoveCompleted".GetLocalizedFormat(motorName);
        }
        catch (OperationCanceledException)
        {
            MarkMotionStateReadRequired();
            StatusText = "ScanDebug_Runtime_StatusMotorMoveCanceled".GetLocalizedFormat(motorName);
        }
        catch (Exception ex)
        {
            MarkMotionStateReadRequired();
            StatusText = "ScanDebug_Runtime_StatusMotorMoveFailed".GetLocalizedFormat(motorName, ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsApplyingMotion = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopMotor))]
    private async Task StopMotor(string? motorDisplayId)
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        if (!TryParseMotorTarget(motorDisplayId, out var motorId, out var motorName, out var error))
        {
            StatusText = error;
            return;
        }

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.StopMotor, out var runtimeClaim))
            return;

        IsApplyingMotion = true;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusMotorStopping".GetLocalizedFormat(motorName);
            await _sessionCoordinator.UseConnectedSessionAsync(
                async (session, token) =>
                {
                    await session.StopMotorAsync(motorId, token);
                    return true;
                },
                CancellationToken.None);
            await LoadMotionStateAsync(_session.ConnectionToken);
            StatusText = "ScanDebug_Runtime_StatusMotorStopSent".GetLocalizedFormat(motorName);
        }
        catch (OperationCanceledException)
        {
            MarkMotionStateReadRequired();
            StatusText = "ScanDebug_Runtime_StatusMotorStopCanceled".GetLocalizedFormat(motorName);
        }
        catch (Exception ex)
        {
            MarkMotionStateReadRequired();
            StatusText = "ScanDebug_Runtime_StatusMotorStopFailed".GetLocalizedFormat(motorName, ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsApplyingMotion = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyMotorConfig))]
    private async Task ApplyMotorConfig(string? motorDisplayId)
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        if (!TryParseMotorTarget(motorDisplayId, out var motorId, out var motorName, out var error))
        {
            StatusText = error;
            return;
        }

        var producer = _motionRuntimeState.CaptureProducer();
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ApplyMotorConfig, out var runtimeClaim))
            return;

        IsApplyingMotion = true;
        try
        {
            if (!_motionRuntimeState.IsCurrent(producer))
                return;
            StatusText = "ScanDebug_Runtime_StatusMotorConfigApplying".GetLocalizedFormat(motorName);
            await _sessionCoordinator.UseConnectedSessionAsync(
                async (session, token) =>
                {
                    producer.CancellationToken.ThrowIfCancellationRequested();
                    await session.ApplyMotorConfigAsync(motorId, token);
                    return true;
                },
                producer.CancellationToken);
            await LoadMotionStateAsync(_session.ConnectionToken);
            StatusText = "ScanDebug_Runtime_StatusMotorConfigApplied".GetLocalizedFormat(motorName);
        }
        catch (OperationCanceledException)
        {
            MarkMotionStateReadRequired();
            StatusText = "ScanDebug_Runtime_StatusMotorConfigCanceled".GetLocalizedFormat(motorName);
        }
        catch (Exception ex)
        {
            MarkMotionStateReadRequired();
            StatusText = "ScanDebug_Runtime_StatusMotorConfigFailed".GetLocalizedFormat(motorName, ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsApplyingMotion = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAutoBlackAdjust))]
    private Task AutoBlackAdjust()
        => RunAutoCalibrationAsync((session, snapshot, roiSettings, prompt, status, applied, frame, ct) => _autoCalibration.AutoBlackAdjustCandidateAsync(session, snapshot, roiSettings, prompt, status, applied, frame, ct), "ScanDebug_Runtime_StatusAutoBlackCompleted".GetLocalized(), ScanDebugRuntimeCommandKind.AutoBlackAdjust);

    [RelayCommand(CanExecute = nameof(CanAutoWhiteAdjust))]
    private Task AutoWhiteAdjust()
        => RunAutoCalibrationAsync((session, snapshot, roiSettings, prompt, status, applied, frame, ct) => _autoCalibration.AutoWhiteAdjustCandidateAsync(session, snapshot, roiSettings, prompt, status, applied, frame, ct), "ScanDebug_Runtime_StatusAutoWhiteCompleted".GetLocalized(), ScanDebugRuntimeCommandKind.AutoWhiteAdjust);

    [RelayCommand(CanExecute = nameof(CanAutoCalibrate))]
    private Task AutoCalibrate()
        => RunAutoCalibrationAsync((session, snapshot, roiSettings, prompt, status, applied, frame, ct) => _autoCalibration.AutoCalibrateCandidateAsync(session, snapshot, roiSettings, prompt, status, applied, frame, ct), "ScanDebug_Runtime_StatusAutoCalibrationCompleted".GetLocalized(), ScanDebugRuntimeCommandKind.AutoCalibrate);

    private bool CanCopyCalibrationProfileFromChannel()
        => !string.IsNullOrWhiteSpace(SelectedCalibrationChannel)
           && !string.IsNullOrWhiteSpace(SelectedCalibrationCopySourceChannel)
           && !string.Equals(SelectedCalibrationChannel, SelectedCalibrationCopySourceChannel, StringComparison.OrdinalIgnoreCase)
           && TryGetCalibrationProfile(SelectedCalibrationCopySourceChannel, out _);

    [RelayCommand(CanExecute = nameof(CanCopyCalibrationProfileFromChannel))]
    private void CopyCalibrationProfileFromChannel()
    {
        if (!CanCopyCalibrationProfileFromChannel()
            || !TryGetCalibrationProfile(SelectedCalibrationCopySourceChannel!, out var sourceProfile))
        {
            return;
        }

        _copiedUnverifiedCalibrationProfiles[SelectedCalibrationChannel] = sourceProfile;
        AdvanceCalibrationProjectionVersion();
        ApplyCalibrationProfileProjection(sourceProfile);
        RefreshCalibrationChannelItems();
        NotifyChannelProfileOverviewChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSaveChannelProfile))]
    private async Task SaveChannelProfile()
    {
        if (!_parameters.TryParseInput(ExposureMicroseconds, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockMhz, out var snapshot, out var error))
        {
            StatusText = error;
            return;
        }

        var channelRole = SelectedCalibrationChannel;

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.SaveChannelProfile, out var runtimeClaim))
            return;

        IsCalibrationRepositoryOperationRunning = true;
        try
        {
            await SaveSelectedCalibrationProfileAsync(snapshot, channelRole);
            if (IsCurrentCalibrationChannel(channelRole))
                StatusText = "ScanDebug_Runtime_StatusCalibrationChannelProfileSaved".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole));
        }
        catch (Exception ex)
        {
            if (IsCurrentCalibrationChannel(channelRole))
                StatusText = "ScanDebug_Runtime_StatusSaveChannelProfileFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsCalibrationRepositoryOperationRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearChannelProfile))]
    private async Task ClearChannelProfile()
    {
        var channelRole = SelectedCalibrationChannel;
        if (string.IsNullOrWhiteSpace(channelRole))
        {
            StatusText = "ScanDebug_Runtime_StatusCalibrationChannelEmpty".GetLocalized();
            return;
        }

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ClearChannelProfile, out var runtimeClaim))
            return;

        IsCalibrationRepositoryOperationRunning = true;
        try
        {
            var channelDisplayName = GetCalibrationChannelDisplayName(channelRole);
            if (_calibrationProfiles.TryGetProfile(channelRole, out _)
                && !await RequestCalibrationPromptAsync(new ScanCalibrationPrompt(
                    "ScanDebug_ChannelCalibrationRemoveConfirmationTitle".GetLocalized(),
                    "ScanDebug_ChannelCalibrationRemoveConfirmationMessage".GetLocalizedFormat(channelDisplayName),
                    "ScanDebug_ChannelCalibrationRemoveConfirmationRemoveButton".GetLocalized(),
                    "ScanDebug_ChannelCalibrationRemoveConfirmationCancelButton".GetLocalized())))
            {
                if (IsCurrentCalibrationChannel(channelRole))
                    StatusText = "ScanDebug_Runtime_StatusCalibrationChannelRemoveCanceled".GetLocalizedFormat(channelDisplayName);
                return;
            }

            AdvanceCalibrationProjectionVersion();
            var removed = await _calibrationProfiles.ClearProfileAsync(channelRole, CancellationToken.None);
            AdvanceCalibrationProjectionVersion();
            _copiedUnverifiedCalibrationProfiles.Remove(channelRole);
            if (string.Equals(channelRole, _selectedCalibrationEditorReferenceRole, StringComparison.OrdinalIgnoreCase))
                ClearSelectedCalibrationEditorBaseline();
            RefreshCalibrationChannelItems();
            NotifyChannelProfileOverviewChanged();
            if (!IsCurrentCalibrationChannel(channelRole))
                return;

            if (removed)
            {
                _roiSettings = ScanCalibrationRoiSettings.CreateDefault();
                RefreshRoiStatus();
            }
            RefreshColumnSampleStatus();
            RefreshPreviewIfPossible();
            CalibrationChannelStatusText = removed
                ? "ScanDebug_Runtime_CalibrationChannel_ProfileCleared".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole))
                : "ScanDebug_Runtime_CalibrationChannel_NoSavedProfile".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole));
            StatusText = removed
                ? "ScanDebug_Runtime_StatusCalibrationChannelProfileCleared".GetLocalizedFormat(channelDisplayName)
                : "ScanDebug_Runtime_StatusCalibrationChannelNoSavedProfile".GetLocalizedFormat(channelDisplayName);
        }
        catch (Exception ex)
        {
            if (IsCurrentCalibrationChannel(channelRole))
                StatusText = "ScanDebug_Runtime_StatusClearChannelProfileFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsCalibrationRepositoryOperationRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveFilmProfile))]
    private async Task SaveFilmProfileJson()
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.SaveFilmProfileJson, out var runtimeClaim))
            return;

        try
        {
            if (!SynchronizeFilmProfileDraftFromInputs())
            {
                PublishFilmProfileOperation(
                    string.IsNullOrWhiteSpace(CurrentFilmProfileValidationSummary)
                        ? "ScanDebug_Runtime_StatusFilmProfileInvalid".GetLocalizedOrFallback("Film profile settings are invalid.")
                        : "ScanDebug_Runtime_StatusFilmProfileInvalidWithSummary".GetLocalizedFormatOrFallback("Film profile settings are invalid: {0}", CurrentFilmProfileValidationSummary),
                    InfoBarSeverity.Error);
                return;
            }

            var draft = _filmProfileWorkspace.Snapshot.CurrentDraft;
            var export = _filmProfileWorkspace.BuildExportDocument();
            SetCurrentFilmProfileValidation(export.Document.Validation);
            if (!export.Document.CanApply)
            {
                _hasInvalidFilmProfileInput = true;
                RefreshFilmProfileWorkspaceProjection();
                PublishFilmProfileOperation(
                    string.IsNullOrWhiteSpace(CurrentFilmProfileValidationSummary)
                        ? "ScanDebug_Runtime_StatusFilmProfileInvalid".GetLocalizedOrFallback("Film profile settings are invalid.")
                        : "ScanDebug_Runtime_StatusFilmProfileInvalidWithSummary".GetLocalizedFormatOrFallback("Film profile settings are invalid: {0}", CurrentFilmProfileValidationSummary),
                    InfoBarSeverity.Error);
                return;
            }

            PublishFilmProfileOperation("ScanDebug_FilmProfileWorkbenchOperationBusy".GetLocalized(), InfoBarSeverity.Informational);
            var exported = await _filmProfileFiles.ExportAsync(export.Document.Document!, CancellationToken.None);

            if (!exported)
            {
                RefreshFilmProfileWorkspaceProjection();
                PublishFilmProfileOperation("ScanDebug_Runtime_StatusFilmProfileExportCanceled".GetLocalized(), InfoBarSeverity.Informational);
                return;
            }

            _filmProfileWorkspace.MarkExported(export.Document.Document!);
            _isNewFilmProfilePendingExport = false;
            RefreshFilmProfileWorkspaceProjection();
            PublishFilmProfileOperation("ScanDebug_Runtime_StatusFilmProfileExported".GetLocalizedFormat(GetFilmProfileDisplayName(draft.ProfileName)), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            RefreshFilmProfileWorkspaceProjection();
            PublishFilmProfileOperation("ScanDebug_Runtime_StatusSaveFilmProfileFailed".GetLocalizedFormat(ex.Message), InfoBarSeverity.Error);
        }
        finally
        {
            runtimeClaim?.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadFilmProfile))]
    private async Task LoadFilmProfileJson()
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.LoadFilmProfileJson, out var runtimeClaim))
            return;

        IsFilmProfileOperationRunning = true;
        try
        {
            if (HasPendingFilmProfileImportResult && !await RequestFilmProfileImportReplacementConfirmationAsync())
            {
                PublishFilmProfileOperation("ScanDebug_FilmProfileWorkbenchOperationCanceled".GetLocalized(), InfoBarSeverity.Informational);
                return;
            }

            PublishFilmProfileOperation("ScanDebug_FilmProfileWorkbenchOperationBusy".GetLocalized(), InfoBarSeverity.Informational);
            var imported = await _filmProfileFiles.ImportAsync(CancellationToken.None);
            if (imported.WasCanceled)
            {
                PublishFilmProfileOperation("ScanDebug_Runtime_StatusLoadFilmProfileCanceled".GetLocalized(), InfoBarSeverity.Informational);
                return;
            }

            if (imported.Profile is null)
            {
                SetFilmProfileImportError(imported.Validation ?? new ScanFilmProfileValidationResult());
                PublishFilmProfileOperation("ScanDebug_Runtime_StatusFilmProfileImportInvalid".GetLocalized(), InfoBarSeverity.Error);
                return;
            }

            var staged = _filmProfileWorkspace.StageImport(imported.Profile);
            if (!staged.Staged)
                SetFilmProfileImportError(staged.Validation);
            else
                _copiedUnverifiedCalibrationProfiles.Clear();
            PublishFilmProfileOperation(
                staged.Staged
                    ? "ScanDebug_Runtime_StatusFilmProfileStaged".GetLocalizedFormat(GetFilmProfileDisplayName(imported.Profile.ProfileName))
                    : "ScanDebug_Runtime_StatusFilmProfileImportInvalid".GetLocalized(),
                staged.Staged ? InfoBarSeverity.Success : InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            PublishFilmProfileOperation("ScanDebug_Runtime_StatusLoadFilmProfileFailed".GetLocalizedFormat(ex.Message), InfoBarSeverity.Error);
        }
        finally
        {
            RefreshFilmProfileWorkspaceProjection();
            runtimeClaim?.Dispose();
            IsFilmProfileOperationRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyStagedFilmProfileImportCommand))]
    private async Task ApplyStagedFilmProfileImport()
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ApplyStagedFilmProfileImport, out var runtimeClaim))
            return;

        IsFilmProfileOperationRunning = true;
        try
        {
            PublishFilmProfileOperation("ScanDebug_FilmProfileWorkbenchOperationBusy".GetLocalized(), InfoBarSeverity.Informational);
            var result = await _filmProfileWorkspace.ApplyStagedImportAsync(CancellationToken.None);
            if (result.Status != ScanFilmProfileApplyStatus.Applied)
            {
                RefreshFilmProfileWorkspaceProjection();
                PublishFilmProfileOperation(
                    result.Status == ScanFilmProfileApplyStatus.Canceled
                        ? "ScanDebug_FilmProfileWorkbenchOperationCanceled".GetLocalized()
                        : result.Error is null
                            ? "ScanDebug_Runtime_StatusLoadFilmProfileFailed".GetLocalizedFormat("ScanDebug_Runtime_StatusFilmProfileInvalid".GetLocalizedOrFallback("Film profile settings are invalid."))
                            : "ScanDebug_Runtime_StatusLoadFilmProfileFailed".GetLocalizedFormat(result.Error.Message),
                    result.Status == ScanFilmProfileApplyStatus.Canceled ? InfoBarSeverity.Informational : InfoBarSeverity.Error);
                return;
            }

            _copiedUnverifiedCalibrationProfiles.Clear();
            _isNewFilmProfilePendingExport = false;
            RefreshFilmProfileWorkspaceProjection();
            SetFilmProfileImportNone();
            PublishFilmProfileOperation("ScanDebug_Runtime_StatusFilmProfileApplied".GetLocalizedFormat(GetFilmProfileDisplayName(FilmProfileName)), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            RefreshFilmProfileWorkspaceProjection();
            PublishFilmProfileOperation("ScanDebug_Runtime_StatusLoadFilmProfileFailed".GetLocalizedFormat(ex.Message), InfoBarSeverity.Error);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsFilmProfileOperationRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDiscardStagedFilmProfileImport))]
    private void DiscardStagedFilmProfileImport()
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.DiscardStagedFilmProfileImport, out var runtimeClaim))
            return;

        using (runtimeClaim)
        {
            _filmProfileWorkspace.DiscardStagedImport();
            SetFilmProfileImportNone();
            RefreshFilmProfileWorkspaceProjection();
            PublishFilmProfileOperation("ScanDebug_FilmProfileWorkbenchOperationSucceeded".GetLocalized(), InfoBarSeverity.Success);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunFilmProfileLifecycleOperation))]
    private async Task NewFilmProfile()
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.NewFilmProfile, out var runtimeClaim))
            return;

        IsFilmProfileOperationRunning = true;
        try
        {
            if (HasPendingFilmProfileImportResult && !await RequestFilmProfileImportDiscardConfirmationAsync())
            {
                PublishFilmProfileOperation("ScanDebug_FilmProfileWorkbenchOperationCanceled".GetLocalized(), InfoBarSeverity.Informational);
                return;
            }

            if (HasUnsavedProfileChanges && !await RequestFilmProfileDiscardConfirmationAsync())
            {
                PublishFilmProfileOperation("ScanDebug_FilmProfileWorkbenchOperationCanceled".GetLocalized(), InfoBarSeverity.Informational);
                return;
            }

            _copiedUnverifiedCalibrationProfiles.Clear();
            ClearSelectedCalibrationEditorBaseline();
            _filmProfileWorkspace.ResetToDefaultDraft();
            _isNewFilmProfilePendingExport = true;
            RefreshFilmProfileWorkspaceProjection();
            PublishFilmProfileOperation("ScanDebug_FilmProfileWorkbenchOperationSucceeded".GetLocalized(), InfoBarSeverity.Success);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsFilmProfileOperationRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanValidateFilmProfile))]
    private void ValidateFilmProfile()
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ValidateFilmProfile, out var runtimeClaim))
            return;

        using (runtimeClaim)
        {
            var isValid = SynchronizeFilmProfileDraftFromInputs();
            if (isValid)
            {
                PublishFilmProfileOperation("ScanDebug_FilmProfileWorkbenchOperationSucceeded".GetLocalized(), InfoBarSeverity.Success);
                return;
            }

            PublishFilmProfileOperation(
                string.IsNullOrWhiteSpace(CurrentFilmProfileValidationSummary)
                    ? "ScanDebug_Runtime_StatusFilmProfileInvalid".GetLocalizedOrFallback("Film profile settings are invalid.")
                    : "ScanDebug_Runtime_StatusFilmProfileInvalidWithSummary".GetLocalizedFormatOrFallback("Film profile settings are invalid: {0}", CurrentFilmProfileValidationSummary),
                InfoBarSeverity.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditProfileDraft))]
    private void ResetSelectedRoi()
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ResetSelectedRoi, out var runtimeClaim))
            return;

        using (runtimeClaim)
        {
            var defaults = ScanCalibrationRoiSettings.CreateDefault();
            _roiSettings = SelectedRoiSelection switch
            {
                RoiSelectionBwActive => _roiSettings with { EffectiveRange = defaults.EffectiveRange },
                RoiSelectionBwShield => _roiSettings with { ShieldRange = defaults.ShieldRange },
                RoiSelectionFocusOverall => _roiSettings with { FocusOverallRange = defaults.FocusOverallRange },
                RoiSelectionFocusLeft => _roiSettings with { FocusLeftRange = defaults.FocusLeftRange },
                RoiSelectionFocusRight => _roiSettings with { FocusRightRange = defaults.FocusRightRange },
                _ => _roiSettings
            };
            RefreshRoiStatus();
            SynchronizeFilmProfileDraftFromInputs();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditProfileDraft))]
    private void ApplySelectedRoiInputs()
    {
        if (!TryBuildRoiEditCandidate(out var candidate, out var issue))
        {
            RoiInputStatusText = FormatRoiEditIssue(issue);
            return;
        }

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ApplySelectedRoiInputs, out var runtimeClaim))
            return;

        using (runtimeClaim)
        {
            _roiSettings = candidate;
            RefreshRoiStatus();
            SynchronizeFilmProfileDraftFromInputs();
            RoiInputStatusText = "ScanDebug_Runtime_RoiRangeApplied".GetLocalized();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditProfileDraft))]
    private void ResetAllRois()
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ResetAllRois, out var runtimeClaim))
            return;

        using (runtimeClaim)
        {
            _roiSettings = ScanCalibrationRoiSettings.CreateDefault();
            RefreshRoiStatus();
            SynchronizeFilmProfileDraftFromInputs();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveFocusMapping))]
    private async Task SaveFocusMapping()
    {
        if (!TryBuildFocusMotorMappingFromInputs(out var mapping, out var error))
        {
            SetFocusMappingValidation(error);
            return;
        }

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.UpdateFocusMapping, out var runtimeClaim))
            return;

        IsUpdatingFocusMapping = true;
        try
        {
            await EnsureDeviceSettingsInitializedAsync();
            var settings = _deviceSettings.Settings.Normalize() with { FocusMotorMapping = mapping };
            await _deviceSettings.SetSettingsAsync(settings);
            ApplyFocusMappingInputs(_deviceSettings.Settings.FocusMotorMapping ?? mapping);
            SetFocusMappingStatus("ScanDebug_Runtime_FocusMappingSavedStatus".GetLocalized());
            RefreshAutofocusBounds();
            UpdateMotorSemanticProjection();
        }
        catch (Exception ex)
        {
            SetFocusMappingValidation("ScanDebug_Runtime_FocusMappingSaveFailed".GetLocalizedFormat(ex.Message));
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsUpdatingFocusMapping = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanTestFocusMapping))]
    private Task TestLeftFocusMapping()
        => TestFocusMappingMotorAsync(left: true);

    [RelayCommand(CanExecute = nameof(CanTestFocusMapping))]
    private Task TestRightFocusMapping()
        => TestFocusMappingMotorAsync(left: false);

    [RelayCommand]
    private async Task StopAllFocus()
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.StopAllFocus, out var runtimeClaim))
            return;

        using (runtimeClaim)
        {
            var mappings = new[] { _activeAutoFocusMappingSnapshot, _activeManualFocusMappingSnapshot }
                .Where(mapping => mapping is not null)
                .Cast<ScanFocusMotorMapping>()
                .ToArray();
            _autoFocusCts?.Cancel();
            _manualFocusCts?.Cancel();
            _manualFocusStopAllCts?.Cancel();
            _focusMappingTestCts?.Cancel();

            if (!IsDeviceConnected || mappings.Length == 0)
            {
                StatusText = "ScanDebug_Runtime_StatusStopAllFocusLocalCleanup".GetLocalized();
                return;
            }

            try
            {
                await _sessionCoordinator.UseConnectedSessionAsync(
                    async (session, _) =>
                    {
                        foreach (var motorId in mappings.SelectMany(mapping => new[] { mapping.LeftMotorId, mapping.RightMotorId }).Distinct())
                            await TryStopManualFocusMotorAsync(session, motorId);
                        return true;
                    },
                    CancellationToken.None);
                StatusText = "ScanDebug_Runtime_StatusStopAllFocusRequested".GetLocalized();
            }
            catch (Exception ex)
            {
                StatusText = "ScanDebug_Runtime_StatusStopAllFocusFailed".GetLocalizedFormat(ex.Message);
            }
        }
    }

    private bool CanStopAllMotors()
        => CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.StopAllMotors);

    [RelayCommand(CanExecute = nameof(CanStopAllMotors))]
    private async Task StopAllMotors()
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.StopAllMotors, out var runtimeClaim))
            return;

        using (runtimeClaim)
        {
            MarkMotionStateReadRequired();
            _motionRuntimeState.BeginGlobalStop();
            _scanCts?.Cancel();
            _autoFocusCts?.Cancel();
            _manualFocusCts?.Cancel();
            _manualFocusStopAllCts?.Cancel();
            _focusMappingTestCts?.Cancel();
            _autoCalibrationCts?.Cancel();

            try
            {
                var result = await _sessionCoordinator.StopAllMotionAsync(CancellationToken.None);
                if (!result.Success)
                    MarkMotionStateReadRequired();

                StatusText = LocalizeGlobalMotorStopResult(result);
            }
            catch (Exception ex)
            {
                MarkMotionStateReadRequired();
                StatusText = GetTodo18LocalizedFormat("ScanDebug_Runtime_StatusStopAllMotorsFailed", "Stop all motors failed: {0}", ex.Message);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunAutoFocus))]
    private async Task QuickFocus()
    {
        SelectedAutofocusPreset = ScanAutofocusPresetKind.Quick;
        await AutoFocus();
    }

    [RelayCommand(CanExecute = nameof(CanRunAutoFocus))]
    private async Task FineFocus()
    {
        SelectedAutofocusPreset = ScanAutofocusPresetKind.Fine;
        await AutoFocus();
    }

    [RelayCommand(CanExecute = nameof(CanRunAutoFocus))]
    private async Task AutoFocus()
    {
        var focusRoi = ScanFocusRoi.TryCreate(_roiSettings, _imageDecoder.GetDecodedPixelsPerLine());
        if (!focusRoi.IsValid)
        {
            StatusText = FormatRoiEditIssue(focusRoi.Issues[0]);
            return;
        }

        var producer = _motionRuntimeState.CaptureProducer();
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.AutoFocus, out var runtimeClaim))
            return;

        using (runtimeClaim)
        {
            if (!IsDeviceConnected)
                return;

            try
            {
                await EnsureDeviceSettingsInitializedAsync();
                if (!_motionRuntimeState.IsCurrent(producer))
                    return;
            }
            catch
            {
                throw;
            }

            if (!TryBuildAutofocusRequest(out var request, out var error))
            {
                StatusText = error;
                return;
            }

        IsAutoFocusing = true;
        using var autofocusCts = CancellationTokenSource.CreateLinkedTokenSource(_session.ConnectionToken, producer.CancellationToken);
        _autoFocusCts = autofocusCts;
        _activeAutoFocusMappingSnapshot = request.FocusMotorMapping;
        var restoreWarmUp = IsWarmUpEnabled;
        try
        {
            if (restoreWarmUp)
            {
                var disableWarmUpResult = await _sessionCoordinator.SetWarmUpAsync(false, autofocusCts.Token);
                if (!disableWarmUpResult.Success)
                    throw new IOException("ScanDebug_Runtime_ErrorAutofocusWarmUpOffRequired".GetLocalizedFormat(ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(disableWarmUpResult.Message)));

                _suppressWarmUpToggleCommand = true;
                try
                {
                    IsWarmUpEnabled = false;
                }
                finally
                {
                    _suppressWarmUpToggleCommand = false;
                }
            }

            StatusText = "ScanDebug_Runtime_StatusAutofocusStarted".GetLocalized();
            AutofocusSummaryText = $"Autofocus: sampling {request.SampleRows} rows, tilt {AutofocusTiltProbeSteps} mm ({request.TiltProbeSteps} steps), Z {AutofocusZProbeSteps} mm ({request.ZProbeSteps} steps).";

            var result = await _sessionCoordinator.RunConnectedSessionStateAsync(
                ScannerSessionState.Running,
                (session, token) => _autoFocus.AutoFocusAsync(
                    session,
                    request,
                    status => _dispatcher.TryEnqueue(() => StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(status)),
                    (imageBytes, rows, phase) => _dispatcher.TryEnqueue(() => ShowCalibrationFrame(imageBytes, rows, phase)),
                    token),
                autofocusCts.Token);

            await LoadMotionStateAsync(_session.ConnectionToken);
            AutofocusSummaryText = BuildAutofocusSummary(result);
            StatusText = "ScanDebug_Runtime_StatusAutofocusCompleted".GetLocalized();
        }
        catch (OperationCanceledException)
        {
            MarkMotionStateReadRequired();
            AutofocusSummaryText = "ScanDebug_Runtime_AutofocusCanceled".GetLocalized();
            StatusText = "ScanDebug_Runtime_StatusAutofocusCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
            MarkMotionStateReadRequired();
            AutofocusSummaryText = "ScanDebug_Runtime_AutofocusFailed".GetLocalizedFormat(ex.Message);
            StatusText = "ScanDebug_Runtime_StatusAutofocusFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            if (restoreWarmUp && IsDeviceConnected)
            {
                try
                {
                    var restoreWarmUpResult = await _sessionCoordinator.SetWarmUpAsync(true, CancellationToken.None);
                    if (restoreWarmUpResult.Success)
                    {
                        _suppressWarmUpToggleCommand = true;
                        try
                        {
                            IsWarmUpEnabled = true;
                        }
                        finally
                        {
                            _suppressWarmUpToggleCommand = false;
                        }
                    }
                    else
                    {
                        StatusText = "ScanDebug_Runtime_StatusAutofocusWarmUpRestoreFailed".GetLocalizedFormat(ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(restoreWarmUpResult.Message));
                    }
                }
                catch (Exception ex)
                {
                    StatusText = "ScanDebug_Runtime_StatusAutofocusWarmUpRestoreFailed".GetLocalizedFormat(ex.Message);
                }
            }

            if (ReferenceEquals(_autoFocusCts, autofocusCts))
                _autoFocusCts = null;
            _activeAutoFocusMappingSnapshot = null;
            IsAutoFocusing = false;
        }
        }
    }

    public void BeginManualFocusHold(bool positive)
    {
        var producer = _motionRuntimeState.CaptureProducer();
        if (_manualFocusCts is not null || IsManualFocusing)
            return;

        if (!CanUseManualFocusSurface())
        {
            StatusText = !IsConnected
                ? "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized()
                : "ScanDebug_Runtime_StatusManualFocusBusy".GetLocalized();
            return;
        }

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.StartManualFocus, out var runtimeClaim))
            return;

        if (!_motionRuntimeState.IsCurrent(producer))
        {
            runtimeClaim?.Dispose();
            return;
        }

        var holdCts = CancellationTokenSource.CreateLinkedTokenSource(_session.ConnectionToken, producer.CancellationToken);
        var stopAllCts = CancellationTokenSource.CreateLinkedTokenSource(_session.ConnectionToken, producer.CancellationToken);
        _manualFocusCts = holdCts;
        _manualFocusStopAllCts = stopAllCts;
        _manualFocusRuntimeClaim = runtimeClaim;
        IsManualFocusing = true;
        NotifyManualFocusAvailabilityChanged();
        _manualFocusTask = RunManualFocusHoldLoopAsync(positive, holdCts, stopAllCts);
    }

    public void EndManualFocusHold()
    {
        var cts = _manualFocusCts;
        if (cts is null)
            return;

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.StopManualFocus, out var runtimeClaim))
            return;

        using (runtimeClaim)
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel();

            if (_manualFocusMoveInProgress)
                _ = TryStopManualFocusMotorsAfterReleaseAsync();
        }
    }

    private async Task RunManualFocusHoldLoopAsync(
        bool positive,
        CancellationTokenSource holdCts,
        CancellationTokenSource stopAllCts)
    {
        var manualToken = holdCts.Token;
        var stopAllToken = stopAllCts.Token;
        ScanFocusMotorMapping? activeMapping = null;
        try
        {
            await EnsureDeviceSettingsInitializedAsync();
            stopAllToken.ThrowIfCancellationRequested();

            if (!TryBuildManualFocusRequest(positive, out var request, out var error))
            {
                StatusText = error;
                return;
            }

            activeMapping = request.FocusMotorMapping;
            _activeManualFocusMappingSnapshot = activeMapping;
            stopAllToken.ThrowIfCancellationRequested();
            StatusText = "ScanDebug_Runtime_StatusManualFocusStarted".GetLocalizedFormat(request.DirectionLabel, request.DistanceText, request.SampleRows);
            await _sessionCoordinator.RunConnectedSessionStateAsync(
                ScannerSessionState.Running,
                async (session, sessionToken) =>
                {
                    var completedFirstJog = false;
                    while (!completedFirstJog || !manualToken.IsCancellationRequested)
                    {
                        using var linkedStepCts = completedFirstJog
                            ? CancellationTokenSource.CreateLinkedTokenSource(sessionToken, stopAllToken, manualToken)
                            : CancellationTokenSource.CreateLinkedTokenSource(sessionToken, stopAllToken);
                        await ExecuteManualFocusJogAsync(session, request, linkedStepCts.Token);
                        completedFirstJog = true;

                        if (manualToken.IsCancellationRequested)
                            break;

                        await Task.Delay(ManualFocusHoldRepeatDelayMs, manualToken);
                    }

                    return true;
                },
                stopAllToken,
                waitForAvailability: false);

            StatusText = manualToken.IsCancellationRequested
                ? "ScanDebug_Runtime_StatusManualFocusStopped".GetLocalized()
                : "ScanDebug_Runtime_StatusManualFocusCompleted".GetLocalized();
        }
        catch (OperationCanceledException)
        {
            StatusText = "ScanDebug_Runtime_StatusManualFocusStopped".GetLocalized();
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusManualFocusFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_manualFocusCts, holdCts))
            {
                _manualFocusCts = null;
                _manualFocusTask = null;
            }

            if (ReferenceEquals(_manualFocusStopAllCts, stopAllCts))
                _manualFocusStopAllCts = null;

            _manualFocusMoveInProgress = false;
            if (activeMapping is not null && ReferenceEquals(_activeManualFocusMappingSnapshot, activeMapping))
                _activeManualFocusMappingSnapshot = null;
            _manualFocusRuntimeClaim?.Dispose();
            _manualFocusRuntimeClaim = null;
            IsManualFocusing = false;
            NotifyManualFocusAvailabilityChanged();
            holdCts.Dispose();
            stopAllCts.Dispose();

            if (IsConnected)
            {
                try
                {
                    await LoadMotionStateAsync(_session.ConnectionToken);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Manual focus motion refresh failed: {ex.Message}");
                }
            }
        }
    }

    private async Task ExecuteManualFocusJogAsync(IScanSessionService session, ManualFocusRequest request, CancellationToken ct)
    {
        try
        {
            _manualFocusMoveInProgress = true;
            ct.ThrowIfCancellationRequested();
            await session.MoveMotorStepsAsync(request.LeftMotorId, request.ZDirection, request.LeftSteps, request.IntervalNs, ct);
            ct.ThrowIfCancellationRequested();
            await session.MoveMotorStepsAsync(request.RightMotorId, request.ZDirection, request.RightSteps, request.IntervalNs, ct);
            ct.ThrowIfCancellationRequested();
            await WaitForManualFocusMotorsAsync(session, request, ct);
        }
        catch (OperationCanceledException)
        {
            await TryStopManualFocusMotorsAsync(session, request.FocusMotorMapping);
            throw;
        }
        finally
        {
            _manualFocusMoveInProgress = false;
        }

        ct.ThrowIfCancellationRequested();
        var result = await session.StartScanAsync(request.SampleRows, ct);
        ct.ThrowIfCancellationRequested();
        if (!result.Success || result.ImageBytes is null)
            throw new IOException("ScanDebug_Runtime_StatusManualFocusScanFailed".GetLocalizedFormat(ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message)));

        var phase = "ScanDebug_Runtime_ManualFocusPreviewPhase".GetLocalizedFormat(request.DirectionLabel, request.DistanceText);
        ct.ThrowIfCancellationRequested();
        await EnqueueOnUiAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            ShowCalibrationFrame(result.ImageBytes, request.SampleRows, phase);
        });
    }

    private async Task WaitForManualFocusMotorsAsync(IScanSessionService session, ManualFocusRequest request, CancellationToken ct)
    {
        try
        {
            await Task.WhenAll(
                session.WaitForMotorMotionCompleteAsync(request.LeftMotorId, request.LeftSteps, request.IntervalNs, ct),
                session.WaitForMotorMotionCompleteAsync(request.RightMotorId, request.RightSteps, request.IntervalNs, ct));
        }
        catch (IOException)
        {
            await WaitForManualFocusMotorsIdleAsync(session, request, ct);
        }
    }

    private static async Task WaitForManualFocusMotorsIdleAsync(IScanSessionService session, ManualFocusRequest request, CancellationToken ct)
    {
        var expectedTravelMs = Math.Ceiling((double)Math.Max(request.LeftSteps, request.RightSteps) * request.IntervalNs / 1000000.0);
        var maximumTimeoutMs = ManualFocusMaximumEstimatedOneWayTravelDurationMs * ManualFocusMotionTimeoutMultiplier + ManualFocusMotionTimeoutPaddingMs;
        var timeoutMs = Math.Min(maximumTimeoutMs, Math.Max(ScanDebugConstants.AckTimeoutMs, expectedTravelMs * ManualFocusMotionTimeoutMultiplier + ManualFocusMotionTimeoutPaddingMs));
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        var timeoutWatch = Stopwatch.StartNew();

        while (timeoutWatch.Elapsed <= timeout)
        {
            ct.ThrowIfCancellationRequested();
            var states = await session.GetMotionStateAsync(ct);
            var focusStates = states.Where(state => state.MotorId == request.LeftMotorId || state.MotorId == request.RightMotorId).ToArray();
            if (focusStates.Length >= 2 && focusStates.All(state => !state.Running && state.RemainingSteps == 0))
                return;

            await Task.Delay(ManualFocusMotionPollDelayMs, ct);
        }

        throw new IOException("ScanDebug_Runtime_StatusManualFocusMotionTimeout".GetLocalized());
    }

    private async Task TryStopManualFocusMotorsAfterReleaseAsync()
    {
        try
        {
            await _sessionCoordinator.UseConnectedSessionAsync(
                async (session, _) =>
                {
                    await TryStopManualFocusMotorsAsync(session);
                    return true;
                },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Manual focus release stop failed: {ex.Message}");
        }
    }

    private async Task TryStopManualFocusMotorsAsync(IScanSessionService session)
    {
        if (_activeManualFocusMappingSnapshot is { } activeMapping)
        {
            await TryStopManualFocusMotorsAsync(session, activeMapping);
            return;
        }

        if (TryGetValidatedFocusMotorMapping(_deviceSettings.Settings, out var mapping, out _))
            await TryStopManualFocusMotorsAsync(session, mapping);
    }

    private async Task TryStopManualFocusMotorsAsync(IScanSessionService session, ScanFocusMotorMapping mapping)
    {
        await TryStopManualFocusMotorAsync(session, mapping.LeftMotorId);
        await TryStopManualFocusMotorAsync(session, mapping.RightMotorId);
    }

    private async Task TryStopManualFocusMotorAsync(IScanSessionService session, byte motorId)
    {
        try
        {
            await session.StopMotorAsync(motorId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            MarkMotionStateReadRequired();
            Debug.WriteLine($"Manual focus stop motor {motorId} failed: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartScan))]
    private async Task StartScan()
    {
        var producer = _motionRuntimeState.CaptureProducer();
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.StartScan, out var runtimeClaim))
            return;

        try
        {
            if (!_motionRuntimeState.IsCurrent(producer))
                return;

            await Task.Yield();
            if (!_motionRuntimeState.IsCurrent(producer))
                return;

            if (!TryParseRequestedRows(out var rows))
            {
                var singleTransferMaxRows = _session.SingleTransferMaxRows;
                StatusText = IsWarmUpEnabled
                    ? "ScanDebug_Runtime_ErrorRowsWarmUpPositive".GetLocalized()
                    : "ScanDebug_Runtime_ErrorRowsRange".GetLocalizedFormat(singleTransferMaxRows);
                return;
            }

            if (!IsConnected)
            {
                StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
                return;
            }

            if (!HasSelectedAcquisitionChannels())
            {
                StatusText = "ScanDebug_DisabledReasonNoAcquisitionChannels".GetLocalized();
                return;
            }

            if (!IsCaptureModeCompatibleWithSelection())
            {
                StatusText = BuildCaptureModeUnavailableReason();
                return;
            }

            var shouldUseWorkflowScan = ShouldUseDebugWorkflowScan();
            if (shouldUseWorkflowScan && IsContinuousScanEnabled)
            {
                StatusText = "ScanDebug_Runtime_ErrorWorkflowContinuousUnsupported".GetLocalized();
                return;
            }

            var activeChannelCount = Math.Max(1, GetSelectedAcquisitionChannelCount());
            var workflowProgressMaxRows = int.MaxValue / ScanDebugConstants.BytesPerLine / activeChannelCount;
            if (shouldUseWorkflowScan && rows > workflowProgressMaxRows)
            {
                StatusText = "ScanDebug_Runtime_ErrorRowsRange".GetLocalizedFormat(workflowProgressMaxRows);
                return;
            }

            ScanWorkflowRequest? workflowRequest = null;
            if (shouldUseWorkflowScan)
                await EnsureDeviceSettingsInitializedAsync();

            if (!_motionRuntimeState.IsCurrent(producer))
                return;

            if (shouldUseWorkflowScan && !TryBuildDebugWorkflowRequest(rows, out workflowRequest, out var workflowError))
            {
                StatusText = workflowError;
                return;
            }

            var workflowPassCount = workflowRequest is null ? 0 : CountActiveWorkflowPasses(workflowRequest);

            ClearDngAlignmentWarning();
            _scanCts = CancellationTokenSource.CreateLinkedTokenSource(producer.CancellationToken);
            IsRunning = true;
            IsScanReadProgressVisible = true;
            ScanReadProgressValue = 0;
            ScanReadProgressMaximum = Math.Max(1, (double)rows * ScanDebugConstants.BytesPerLine * Math.Max(1, workflowPassCount > 0 ? workflowPassCount : 1));
            StatusText = activeChannelCount > 1
                ? "ScanDebug_Runtime_StatusStartingMultiChannelScan".GetLocalized()
                : IsContinuousScanEnabled ? "ScanDebug_Runtime_StatusStartingContinuousScan".GetLocalized() : "ScanDebug_Runtime_StatusStartingScan".GetLocalized();

            try
            {
                if (workflowRequest is not null)
                    await RunWorkflowScanAsync(workflowRequest, _scanCts.Token);
                else if (IsContinuousScanEnabled)
                    await RunContinuousScanLoopAsync(rows, _scanCts.Token);
                else
                    await RunSingleScanAsync(rows, _scanCts.Token);
            }
            catch (Exception ex)
            {
                MarkMotionStateReadRequired();
                StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(ex.Message);
            }
            finally
            {
                IsRunning = false;
                IsScanReadProgressVisible = false;
                _scanCts?.Dispose();
                _scanCts = null;
            }
        }
        finally
        {
            runtimeClaim?.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopScan))]
    private async Task StopScan()
    {
        if (!IsRunning)
            return;

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.StopScan, out var runtimeClaim))
            return;

        _scanCts?.Cancel();
        try
        {
            var result = await _sessionCoordinator.UseConnectedSessionAsync(
                (session, _) => session.StopScanAsync(CancellationToken.None),
                CancellationToken.None);
            StatusText = result.Success ? "ScanDebug_Runtime_StatusStopRequested".GetLocalized() : ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
        }
    }

    private bool TryParseRows(out int rows)
    {
        if (!int.TryParse(SelectedRows, out rows))
            return false;

        if (CanRunExtendedScan())
            return rows > 0;

        return rows > 0 && rows <= _session.SingleTransferMaxRows;
    }

    private bool TryParseRequestedRows(out int rows)
    {
        if (!int.TryParse(SelectedRows, out rows))
            return false;

        return rows > 0;
    }

    private async Task<ScanStartResult> RunScanAsync(int rows, CancellationToken ct)
    {
        if (CanRunExtendedScan() || rows <= _session.SingleTransferMaxRows)
        {
            return await _sessionCoordinator.RunConnectedSessionStateAsync(
                ScannerSessionState.Running,
                (session, token) => session.StartScanAsync(
                    rows,
                    token,
                    status => _dispatcher.TryEnqueue(() => StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(status)),
                    diagnostic => _debugOutputMirror.Mirror("ScanDebug.Diagnostic", diagnostic),
                    ReportScanReadProgress),
                ct,
                waitForAvailability: false);
        }

        return await _sessionCoordinator.RunConnectedSessionStateAsync(
            ScannerSessionState.Running,
            (session, token) => session.StartSegmentedScanAsync(
                rows,
                token,
                status => _dispatcher.TryEnqueue(() => StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(status)),
                diagnostic => _debugOutputMirror.Mirror("ScanDebug.Diagnostic", diagnostic),
                ReportScanReadProgress),
            ct,
            waitForAvailability: false);
    }

    private void MirrorOutput(string source, string message)
        => _debugOutputMirror.Mirror(source, message);

    private async Task RunSingleScanAsync(int rows, CancellationToken ct)
    {
        ScanStartResult result;
        if (CanRunExtendedScan() || rows <= _session.SingleTransferMaxRows)
        {
            var previewSessionVersion = BeginStreamingScanPreview(rows);
            try
            {
                result = await _sessionCoordinator.RunConnectedSessionStateAsync(
                    ScannerSessionState.Running,
                    (session, token) => session.StartScanAsync(
                        rows,
                        token,
                        status => _dispatcher.TryEnqueue(() => StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(status)),
                        diagnostic => _debugOutputMirror.Mirror("ScanDebug.Diagnostic", diagnostic),
                        ReportScanReadProgress,
                        (imageBytes, completedRows) => QueueStreamingPreviewFrame(previewSessionVersion, imageBytes, completedRows),
                        null),
                    ct,
                    waitForAvailability: false);
            }
            finally
            {
                EndStreamingScanPreview(previewSessionVersion);
            }
        }
        else
        {
            result = await RunScanAsync(rows, ct);
        }

        StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message);
        if (!result.Success || result.ImageBytes is null)
            return;

        _streamingWorkflowPreviewResult = null;
        _streamingWorkflowPreviewAssignment = null;
        _streamingWorkflowPreviewCompletedRowsByPassIndex = null;
        _lastWorkflowChannelAssignment = null;
        _lastMonochromeChannelRole = GetSingleSelectedAcquisitionChannelRole();
        _lastWorkflowResult = null;
        ApplyScanFrame(result.ImageBytes, rows, ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message));
    }

    private async Task RunContinuousScanLoopAsync(int rows, CancellationToken ct)
    {
        var frameCount = 0;
        while (!ct.IsCancellationRequested)
        {
            var result = await RunScanAsync(rows, ct);
            if (!result.Success)
            {
                StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message);
                return;
            }

            if (result.ImageBytes is null)
            {
                StatusText = "ScanDebug_Runtime_StatusContinuousScanNoImageData".GetLocalized();
                return;
            }

            frameCount++;
            _streamingWorkflowPreviewResult = null;
            _streamingWorkflowPreviewAssignment = null;
            _streamingWorkflowPreviewCompletedRowsByPassIndex = null;
            _lastWorkflowChannelAssignment = null;
            _lastMonochromeChannelRole = GetSingleSelectedAcquisitionChannelRole();
            _lastWorkflowResult = null;
            ApplyScanFrame(result.ImageBytes, rows, "ScanDebug_Runtime_StatusContinuousPreviewUpdated".GetLocalizedFormat(frameCount));
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveColumnSampleAsBlackLevel))]
    private async Task SaveColumnSampleAsBlackLevel()
    {
        var channelRole = SelectedCalibrationChannel;
        if (!HasCurrentColumnSample())
        {
            StatusText = "ScanDebug_Runtime_ColumnSampleUnavailable".GetLocalized();
            return;
        }

        if (!TryGetCurrentColumnSampleMean(out var mean, out var error))
        {
            StatusText = error;
            return;
        }

        if (!TryResolveSnapshotForLevelSave(channelRole, out var snapshot, out error))
        {
            StatusText = error;
            return;
        }

        var existingProfile = TryGetCalibrationProfile(channelRole, out var profile) ? profile : null;
        var whiteLevel = existingProfile?.WhiteLevel;
        ushort? blackLevel = whiteLevel is not null ? (ushort)Math.Min(mean, Math.Max(0, whiteLevel.Value - 1)) : mean;

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.SaveColumnSampleAsBlackLevel, out var runtimeClaim))
            return;

        IsCalibrationRepositoryOperationRunning = true;
        try
        {
            await SaveCalibrationLevelsAsync(snapshot, blackLevel, whiteLevel, channelRole);
            if (IsCurrentCalibrationChannel(channelRole))
            {
                RefreshColumnSampleStatus();
                StatusText = $"Saved black level {blackLevel} for {GetCalibrationChannelDisplayName(channelRole)}.";
            }
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsCalibrationRepositoryOperationRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveColumnSampleAsWhiteLevel))]
    private async Task SaveColumnSampleAsWhiteLevel()
    {
        var channelRole = SelectedCalibrationChannel;
        if (!HasCurrentColumnSample())
        {
            StatusText = "ScanDebug_Runtime_ColumnSampleUnavailable".GetLocalized();
            return;
        }

        if (!TryGetCurrentColumnSampleMean(out var mean, out var error))
        {
            StatusText = error;
            return;
        }

        if (!TryResolveSnapshotForLevelSave(channelRole, out var snapshot, out error))
        {
            StatusText = error;
            return;
        }

        var existingProfile = TryGetCalibrationProfile(channelRole, out var profile) ? profile : null;
        var blackLevel = existingProfile?.BlackLevel;
        ushort? whiteLevel = blackLevel is not null ? (ushort)Math.Min(ushort.MaxValue, Math.Max(mean, blackLevel.Value + 1)) : mean;

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.SaveColumnSampleAsWhiteLevel, out var runtimeClaim))
            return;

        IsCalibrationRepositoryOperationRunning = true;
        try
        {
            await SaveCalibrationLevelsAsync(snapshot, blackLevel, whiteLevel, channelRole);
            if (IsCurrentCalibrationChannel(channelRole))
            {
                RefreshColumnSampleStatus();
                RefreshPreviewIfPossible();
                StatusText = $"Saved white level {whiteLevel} for {GetCalibrationChannelDisplayName(channelRole)}.";
            }
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsCalibrationRepositoryOperationRunning = false;
        }
    }

    private async Task RunWorkflowScanAsync(ScanWorkflowRequest request, CancellationToken ct)
    {
        var previewSessionVersion = BeginStreamingScanPreview(request.Rows);
        var requestAssignment = BuildResultChannelAssignment(request.PassChannelRoles);
        try
        {
            var result = await _sessionCoordinator.RunConnectedSessionStateAsync(
                ScannerSessionState.Running,
                (session, token) => _workflow.ExecuteAsync(
                    session,
                    request,
                    token,
                    progress => _dispatcher.TryEnqueue(() => StatusText = "ScanDebug_Runtime_StatusMultiChannelProgress".GetLocalizedFormat(progress.CurrentPass, progress.TotalPasses, ScanRuntimeMessageLocalizer.LocalizeScanWorkflowStage(progress.Stage), progress.LedChannelIndex + 1)),
                    status => _dispatcher.TryEnqueue(() => StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(status)),
                    diagnostic => _debugOutputMirror.Mirror("ScanDebug.WorkflowDiagnostic", diagnostic),
                    ReportScanReadProgress,
                    snapshot => QueueStreamingWorkflowPreviewFrame(previewSessionVersion, request, snapshot)),
                ct,
                waitForAvailability: false);

            var previewPass = result.Passes.FirstOrDefault();
            if (previewPass is null || previewPass.ImageBytes.Length == 0)
            {
                EndStreamingScanPreview(previewSessionVersion);
                _streamingWorkflowPreviewResult = null;
                _streamingWorkflowPreviewAssignment = null;
                _streamingWorkflowPreviewCompletedRowsByPassIndex = null;
                StatusText = "ScanDebug_Runtime_StatusMultiChannelNoPassData".GetLocalized();
                return;
            }

            EndStreamingScanPreview(previewSessionVersion);
            _streamingWorkflowPreviewResult = null;
            _streamingWorkflowPreviewAssignment = null;
            _streamingWorkflowPreviewCompletedRowsByPassIndex = null;
            _lastWorkflowChannelAssignment = requestAssignment;
            _lastMonochromeChannelRole = GetSingleActiveRole(requestAssignment);
            _lastWorkflowResult = result;
            ApplyScanFrame(
                previewPass.ImageBytes,
                previewPass.Rows,
                "ScanDebug_Runtime_StatusMultiChannelScanCompleted".GetLocalizedFormat(result.Passes.Count, previewPass.PassIndex));
        }
        catch (OperationCanceledException)
        {
            EndStreamingScanPreview(previewSessionVersion);
            _streamingWorkflowPreviewResult = null;
            _streamingWorkflowPreviewAssignment = null;
            _streamingWorkflowPreviewCompletedRowsByPassIndex = null;
            StatusText = "ScanDebug_Runtime_StatusMultiChannelScanCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
            EndStreamingScanPreview(previewSessionVersion);
            _streamingWorkflowPreviewResult = null;
            _streamingWorkflowPreviewAssignment = null;
            _streamingWorkflowPreviewCompletedRowsByPassIndex = null;
            StatusText = "ScanDebug_Runtime_StatusMultiChannelScanFailed".GetLocalizedFormat(ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(ex.Message));
        }
    }

    private bool TryBuildDebugWorkflowRequest(int rows, out ScanWorkflowRequest request, out string error)
    {
        request = new ScanWorkflowRequest(0, false, Array.Empty<ushort>(), Array.Empty<string>(), Array.Empty<ScanParameterSnapshot>(), 0, 0, false, false, 0, 0);

        var led1 = (ushort)0;
        var led2 = (ushort)0;
        var led3 = (ushort)0;
        var led4 = (ushort)0;
        if (!TryParseLedLevel(Led1Level, "ScanDebug_Runtime_FieldLed1Level".GetLocalized(), out led1, out error)
            || !TryParseLedLevel(Led2Level, "ScanDebug_Runtime_FieldLed2Level".GetLocalized(), out led2, out error)
            || !TryParseLedLevel(Led3Level, "ScanDebug_Runtime_FieldLed3Level".GetLocalized(), out led3, out error)
            || !TryParseLedLevel(Led4Level, "ScanDebug_Runtime_FieldLed4Level".GetLocalized(), out led4, out error))
        {
            return false;
        }

        if (!_parameters.TryParseInput(ExposureMicroseconds, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockMhz, out var fallbackSnapshot, out error))
            return false;

        var motorIntervalNs = ScanDebugConstants.MotionDefaultIntervalNs;
        if (IsScanMotorTransportEnabled && !TryGetEffectiveScanMotorIntervalNs(fallbackSnapshot.ExposureTicks, fallbackSnapshot.SysClockKhz, out motorIntervalNs, out error))
            return false;

        var channelRoles = BuildDebugChannelAssignment().Roles.ToArray();
        var activeRoleCount = channelRoles.Count(role => !string.Equals(role, "Unused", StringComparison.OrdinalIgnoreCase));
        if (activeRoleCount == 0)
        {
            error = "ScanDebug_Runtime_ErrorNoActiveScanChannels".GetLocalized();
            return false;
        }

        if (HasUnsafeDeviceTimingForActiveRoles(channelRoles.Where(ScanChannelRoleHelper.IsActiveRole)))
        {
            error = "Scan_Runtime_ReadinessParametersMissing".GetLocalized();
            return false;
        }

        if (!TryParseSelectedScanMotor(out var scanMotorId, out error))
            return false;

        if (!TryBuildIlluminationRequest(out var illuminationRequest, out error))
            return false;

        var acquisitionSettings = new ScanFilmAcquisitionSettings(
            illuminationRequest.Led1Level,
            illuminationRequest.Led2Level,
            illuminationRequest.Led3Level,
            illuminationRequest.Led4Level,
            illuminationRequest.SteadyMask,
            illuminationRequest.SyncMask,
            illuminationRequest.Led1PulseClock,
            illuminationRequest.Led2PulseClock,
            illuminationRequest.Led3PulseClock,
            illuminationRequest.Led4PulseClock,
            motorIntervalNs).Normalize();

        var passProfiles = new ScanParameterSnapshot[channelRoles.Length];
        for (var passIndex = 0; passIndex < channelRoles.Length; passIndex++)
        {
            var channelRole = channelRoles[passIndex];
            if (string.Equals(channelRole, "Unused", StringComparison.OrdinalIgnoreCase))
            {
                passProfiles[passIndex] = fallbackSnapshot;
                continue;
            }

            if (!_calibrationProfiles.TryGetProfile(channelRole, out var profile))
            {
                error = "ScanDebug_Runtime_CalibrationChannel_NoSavedProfile".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole));
                return false;
            }

            passProfiles[passIndex] = profile.Parameters;
        }

        ScanWorkflowLinePitchInput? linePitchInput = null;
        ScanLinePitchPlanResult? linePitchPlan = null;
        if (IsScanMotorTransportEnabled)
        {
            var motorSettings = GetCurrentScanMotorSettings();
            double targetLinePitchMillimeters;
            if (_isMotorDistanceDerivedFromInterval)
            {
                targetLinePitchMillimeters = ScanTimingMath.ConvertMotorIntervalToLineDistanceMillimeters(
                    motorIntervalNs,
                    fallbackSnapshot.ExposureTicks,
                    fallbackSnapshot.SysClockKhz,
                    motorSettings);
            }
            else if (!ScanMotorDistanceText.TryParseMillimeters(
                MotorDistancePerLineValue,
                MotorDistancePerLineUnit,
                motorSettings,
                out targetLinePitchMillimeters))
            {
                error = "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
                return false;
            }

            linePitchInput = new ScanWorkflowLinePitchInput(
                targetLinePitchMillimeters * 1_000.0,
                motorSettings,
                ScanDebugConstants.MotionMinIntervalNs,
                new uint?[channelRoles.Length]);
            linePitchPlan = ScanTimingMath.BuildLinePitchPlan(new ScanLinePitchPlanRequest(
                rows,
                linePitchInput.TargetLinePitchMicrometers,
                linePitchInput.MotorMechanics,
                linePitchInput.MinimumMotorIntervalNanoseconds,
                channelRoles.Select((channelRole, passIndex) => new ScanLinePitchPassInput(
                    passIndex,
                    channelRole,
                    ScanChannelRoleHelper.IsActiveRole(channelRole),
                    passProfiles[passIndex].ExposureTicks,
                    fallbackSnapshot.SysClockKhz,
                    ParameterProfile: passProfiles[passIndex])).ToArray()));
            if (!linePitchPlan.CanScan
                || (IsAlternateMotorDirectionEnabled && !linePitchPlan.CanUseAlternateDirection))
            {
                error = linePitchPlan.Issues.FirstOrDefault(issue => issue.Severity != ScanLinePitchIssueSeverity.Warning
                    && (issue.Scope == ScanLinePitchIssueScope.Plan
                        || (IsAlternateMotorDirectionEnabled && issue.Scope == ScanLinePitchIssueScope.AlternateDirection)))?.Message
                    ?? "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
                return false;
            }
        }

        request = new ScanWorkflowRequest(
            rows,
            IsWarmUpEnabled,
            new[] { led1, led2, led3, led4 },
            channelRoles,
            passProfiles,
            scanMotorId,
            motorIntervalNs,
            string.Equals(SelectedStartingDirection, ForwardDirection, StringComparison.OrdinalIgnoreCase),
            IsAlternateMotorDirectionEnabled,
            fallbackSnapshot.ExposureTicks,
            fallbackSnapshot.SysClockKhz,
            acquisitionSettings,
            IsScanMotorTransportEnabled,
            true,
            LinePitchInput: linePitchInput);

        error = string.Empty;
        return true;
    }

    private ScanChannelAssignment BuildDebugChannelAssignment()
    {
        var roles = GetEffectiveDeviceChannelRoles();
        var selectedLedIndexes = AcquisitionChannels
            .Where(channel => channel.IsSelected)
            .Select(channel => channel.LedIndex)
            .ToHashSet();

        return new(
            selectedLedIndexes.Contains(0) ? roles[0] : ScanChannelRoleHelper.UnusedRole,
            selectedLedIndexes.Contains(1) ? roles[1] : ScanChannelRoleHelper.UnusedRole,
            selectedLedIndexes.Contains(2) ? roles[2] : ScanChannelRoleHelper.UnusedRole,
            selectedLedIndexes.Contains(3) ? roles[3] : ScanChannelRoleHelper.UnusedRole,
            false,
            false,
            false,
            false);
    }

    private static ScanChannelAssignment BuildDeviceIndexedChannelAssignment(IReadOnlyList<string> roles)
        => new(
            roles.Count > 0 ? roles[0] : ScanChannelRoleHelper.UnusedRole,
            roles.Count > 1 ? roles[1] : ScanChannelRoleHelper.UnusedRole,
            roles.Count > 2 ? roles[2] : ScanChannelRoleHelper.UnusedRole,
            roles.Count > 3 ? roles[3] : ScanChannelRoleHelper.UnusedRole,
            false,
            false,
            false,
            false);

    private static ScanChannelAssignment BuildResultChannelAssignment(IReadOnlyList<string> roles)
    {
        var activeRoles = roles
            .Where(ScanChannelRoleHelper.IsActiveRole)
            .Take(ScanDebugConstants.IlluminationChannelCount)
            .ToArray();

        return new(
            activeRoles.Length > 0 ? activeRoles[0] : ScanChannelRoleHelper.UnusedRole,
            activeRoles.Length > 1 ? activeRoles[1] : ScanChannelRoleHelper.UnusedRole,
            activeRoles.Length > 2 ? activeRoles[2] : ScanChannelRoleHelper.UnusedRole,
            activeRoles.Length > 3 ? activeRoles[3] : ScanChannelRoleHelper.UnusedRole,
            false,
            false,
            false,
            false);
    }

    private ScanChannelAssignment BuildCapturedWorkflowChannelAssignment(ScanWorkflowResult result)
    {
        var roles = GetEffectiveDeviceChannelRoles();
        var capturedRoles = result.Passes
            .Select(capture => capture.LedChannelIndex < roles.Length ? roles[capture.LedChannelIndex] : ScanChannelRoleHelper.UnusedRole)
            .ToArray();
        return BuildResultChannelAssignment(capturedRoles);
    }

    private static IEnumerable<string> GetActiveRoles(ScanChannelAssignment assignment)
        => assignment.Roles.Where(ScanChannelRoleHelper.IsActiveRole);

    private static string? GetSingleActiveRole(ScanChannelAssignment assignment)
    {
        var roles = GetActiveRoles(assignment).Take(2).ToArray();
        return roles.Length == 1 ? roles[0] : null;
    }

    private static int GetSingleActiveRoleIndex(ScanChannelAssignment assignment)
    {
        var indexes = assignment.Roles
            .Select((role, index) => new { role, index })
            .Where(entry => ScanChannelRoleHelper.IsActiveRole(entry.role))
            .Take(2)
            .ToArray();
        return indexes.Length == 1 ? indexes[0].index : -1;
    }

    private static bool HasRgbRoles(ScanChannelAssignment assignment)
        => assignment.Roles.Any(role => string.Equals(role, "Red", StringComparison.OrdinalIgnoreCase))
            && assignment.Roles.Any(role => string.Equals(role, "Green", StringComparison.OrdinalIgnoreCase))
            && assignment.Roles.Any(role => string.Equals(role, "Blue", StringComparison.OrdinalIgnoreCase));

    private ScanChannelAssignment BuildAuthoredChannelAssignment()
    {
        var roles = GetEffectiveDeviceChannelRoles();
        return new(
            roles[0],
            roles[1],
            roles[2],
            roles[3],
            IsChannel1Reversed,
            IsChannel2Reversed,
            IsChannel3Reversed,
            IsChannel4Reversed);
    }

    private bool TryBuildScanRecipeSettings(out ScanFilmScanRecipeSettings settings, List<ScanFilmProfileValidationIssue> issues)
    {
        settings = new ScanFilmScanRecipeSettings();
        var issueCount = issues.Count;
        var redWavelength = ParseScanRecipeDouble(ScanRecipeRedWavelengthNm, "ScanRecipeSettings.ColorManagement.RedWavelengthNm", "Scan_Runtime_FieldRedWavelengthNm", issues);
        var greenWavelength = ParseScanRecipeDouble(ScanRecipeGreenWavelengthNm, "ScanRecipeSettings.ColorManagement.GreenWavelengthNm", "Scan_Runtime_FieldGreenWavelengthNm", issues);
        var blueWavelength = ParseScanRecipeDouble(ScanRecipeBlueWavelengthNm, "ScanRecipeSettings.ColorManagement.BlueWavelengthNm", "Scan_Runtime_FieldBlueWavelengthNm", issues);
        var outputGamma = ParseScanRecipeDouble(ScanRecipeOutputGamma, "ScanRecipeSettings.ColorManagement.OutputGamma", "Scan_Runtime_FieldOutputGamma", issues);
        var manualWhitePointColorTemperature = ParseScanRecipeDouble(ScanRecipeManualWhitePointColorTemperatureK, "ScanRecipeSettings.ColorManagement.ManualWhitePointColorTemperatureK", "Scan_Runtime_FieldManualWhitePointColorTemperatureK", issues);

        if (!TryParseTargetWhitePointMode(SelectedScanRecipeTargetWhitePointMode, out var targetWhitePointMode))
        {
            issues.Add(CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidColorManagement, "ScanRecipeSettings.ColorManagement.TargetWhitePointMode", "FilmProfile.Validation.ScanRecipeInputInvalid"));
            targetWhitePointMode = ScanTargetWhitePointMode.D65;
        }

        if (issues.Count != issueCount)
            return false;

        settings = new ScanFilmScanRecipeSettings(
            BuildAuthoredChannelAssignment(),
            new ScanColorManagementOptions(IsScanRecipeColorManagementEnabled, redWavelength, greenWavelength, blueWavelength, outputGamma, targetWhitePointMode, manualWhitePointColorTemperature),
            SelectedProfileAlignmentMode,
            SelectedProfileDngExportMode);
        return true;
    }

    private static double ParseScanRecipeDouble(string text, string fieldPath, string fieldNameResourceKey, List<ScanFilmProfileValidationIssue> issues)
    {
        if (TryParseColorDouble(text, fieldNameResourceKey.GetLocalized(), out var value, out _))
            return value;

        issues.Add(new ScanFilmProfileValidationIssue(
            ScanFilmProfileValidationCode.InvalidColorManagement,
            fieldPath,
            ScanFilmProfileValidationSeverity.Error,
            "FilmProfile.Validation.NumberInvalid",
            [fieldNameResourceKey.GetLocalized()]));
        return 0;
    }

    private void ApplyScanRecipeSettings(ScanFilmScanRecipeSettings? settings)
    {
        if (settings?.ChannelAssignment is { } assignment)
        {
            IsChannel1Reversed = assignment.Channel1Reversed;
            IsChannel2Reversed = assignment.Channel2Reversed;
            IsChannel3Reversed = assignment.Channel3Reversed;
            IsChannel4Reversed = assignment.Channel4Reversed;
        }

        if (settings?.ColorManagement is { } colorManagement)
        {
            IsScanRecipeColorManagementEnabled = colorManagement.IsEnabled;
            ScanRecipeRedWavelengthNm = FormatColorDouble(colorManagement.RedWavelengthNm);
            ScanRecipeGreenWavelengthNm = FormatColorDouble(colorManagement.GreenWavelengthNm);
            ScanRecipeBlueWavelengthNm = FormatColorDouble(colorManagement.BlueWavelengthNm);
            ScanRecipeOutputGamma = FormatColorDouble(colorManagement.OutputGamma);
            SelectedScanRecipeTargetWhitePointMode = colorManagement.TargetWhitePointMode.ToString();
            ScanRecipeManualWhitePointColorTemperatureK = FormatColorDouble(colorManagement.ManualWhitePointColorTemperatureK);
        }

        if (settings?.AlignmentMode is { } alignmentMode && AlignmentModeOptions.Contains(alignmentMode))
            SelectedProfileAlignmentMode = alignmentMode;

        if (settings?.DngExportMode is { } dngExportMode && DngExportModeOptions.Contains(dngExportMode))
            SelectedProfileDngExportMode = dngExportMode;
    }

    private static bool TryParseColorDouble(string text, string fieldName, out double value, out string error)
    {
        if (!InvariantNumericText.TryParseDouble(text, out value))
        {
            error = "Shared_Runtime_ErrorNumber".GetLocalizedFormat(fieldName);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string FormatColorDouble(double value)
        => InvariantNumericText.FormatCompactDouble(value);

    private static bool TryParseTargetWhitePointMode(string value, out ScanTargetWhitePointMode mode)
        => Enum.TryParse(value, out mode)
            && mode is ScanTargetWhitePointMode.D65 or ScanTargetWhitePointMode.D50 or ScanTargetWhitePointMode.ManualColorTemperature;

    private static int CountActiveWorkflowPasses(ScanWorkflowRequest request)
        => ScanChannelRoleHelper.CountActiveRoles(request.PassChannelRoles);

    private void UpdateComputedMotorSummary()
    {
        if (!int.TryParse(SelectedRows, out var rows) || rows <= 0)
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorUnavailableUntilRowsValid".GetLocalized();
            return;
        }

        if (!TryParseSelectedScanMotor(out var motorId, out _))
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorUnavailableUntilParametersLoaded".GetLocalized();
            return;
        }

        if (!_parameters.TryParseInput(ExposureMicroseconds, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockMhz, out var snapshot, out _)
            || snapshot.SysClockKhz < ScanDebugConstants.MinSysClockKhz)
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorUnavailableUntilParametersLoaded".GetLocalized();
            return;
        }

        var motorSettings = _deviceSettings.Settings.GetMotorSettings(motorId);
        if (!TryGetEffectiveScanMotorIntervalNs(snapshot.ExposureTicks, snapshot.SysClockKhz, out var intervalNs, out _))
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
            return;
        }

        var computedSteps = ScanTimingMath.ComputeMotorStepsPerPass(rows, snapshot.ExposureTicks, snapshot.SysClockKhz, intervalNs);
        var distanceMm = ScanTimingMath.ConvertMotorStepsToMillimeters(computedSteps, motorSettings);
        var speedMmPerSecond = ScanTimingMath.ConvertMotorIntervalToMillimetersPerSecond(intervalNs, motorSettings);
        ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorSummary".GetLocalizedFormat(motorId + 1, computedSteps, FormatMotorIntervalInput(intervalNs), rows, distanceMm.ToString("0.###", CultureInfo.InvariantCulture), speedMmPerSecond.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private bool TryParseSelectedScanMotor(out byte motorId, out string error)
    {
        motorId = 0;
        if (string.IsNullOrWhiteSpace(SelectedScanMotor)
            || !SelectedScanMotor.StartsWith("Motor", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(SelectedScanMotor[5..], out var displayIndex)
            || displayIndex < 1
            || displayIndex > ScanDebugConstants.MotionMotorCount)
        {
            error = "Scan_Runtime_ErrorScanMotorRange".GetLocalizedFormat(ScanDebugConstants.MotionMotorCount);
            return false;
        }

        motorId = (byte)(displayIndex - 1);
        error = string.Empty;
        return true;
    }

    private bool TryGetEffectiveScanMotorIntervalNs(ushort exposureTicks, uint sysClockKhz, out uint intervalNs, out string error)
    {
        intervalNs = 0;

        if (_isMotorDistanceDerivedFromInterval)
        {
            if (ScanMotorIntervalText.TryParseMicroseconds(MotorIntervalUs, out intervalNs)
                && intervalNs >= ScanDebugConstants.MotionMinIntervalNs)
            {
                error = string.Empty;
                return true;
            }

            error = "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
            return false;
        }

        if (!ScanMotorDistanceText.TryParseMillimeters(MotorDistancePerLineValue, MotorDistancePerLineUnit, GetCurrentScanMotorSettings(), out var lineDistanceMm)
            || !ScanTimingMath.TryConvertLineDistanceMillimetersToMotorIntervalNs(lineDistanceMm, exposureTicks, sysClockKhz, GetCurrentScanMotorSettings(), ScanDebugConstants.MotionMinIntervalNs, out intervalNs)
            || !ScanMotorIntervalText.TryFormatMicroseconds(intervalNs, out var intervalUs))
        {
            MotorIntervalUs = string.Empty;
            error = "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
            return false;
        }

        MotorIntervalUs = intervalUs;
        error = string.Empty;
        return true;
    }

    private void RefreshDerivedMotorDistanceFromCurrentInterval()
    {
        if (!_isMotorDistanceDerivedFromInterval)
            return;

        if (!ScanMotorIntervalText.TryParseMicroseconds(MotorIntervalUs, out var intervalNs)
            || intervalNs < ScanDebugConstants.MotionMinIntervalNs
            || !_parameters.TryParseInput(ExposureMicroseconds, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockMhz, out var snapshot, out _)
            || snapshot.SysClockKhz < ScanDebugConstants.MinSysClockKhz)
        {
            ApplyDerivedMotorDistance(string.Empty);
            return;
        }

        var lineDistanceMm = ScanTimingMath.ConvertMotorIntervalToLineDistanceMillimeters(intervalNs, snapshot.ExposureTicks, snapshot.SysClockKhz, GetCurrentScanMotorSettings());
        if (!ScanMotorDistanceText.TryFormatDisplayValue(lineDistanceMm, MotorDistancePerLineUnit, GetCurrentScanMotorSettings(), out var displayValue))
        {
            ApplyDerivedMotorDistance(string.Empty);
            return;
        }

        ApplyDerivedMotorDistance(displayValue);
    }

    private void ApplyDerivedMotorDistance(string value)
    {
        _isApplyingDerivedMotorDistance = true;
        try
        {
            MotorDistancePerLineValue = value;
        }
        finally
        {
            _isApplyingDerivedMotorDistance = false;
        }
    }

    private ScanMotorMechanicalSettings GetCurrentScanMotorSettings()
        => TryParseSelectedScanMotor(out var motorId, out _) ? _deviceSettings.Settings.GetMotorSettings(motorId) : ScanMotorMechanicalSettings.CreateDefault();

    private ScanColorManagementOptions BuildDebugColorManagementOptions()
    {
        var defaults = ScanColorManagementOptions.CreateDefault();
        return new ScanColorManagementOptions(IsGammaCorrectionEnabled, defaults.RedWavelengthNm, defaults.GreenWavelengthNm, defaults.BlueWavelengthNm, TryParsePreviewGamma(out var gamma) ? gamma : defaults.OutputGamma, defaults.TargetWhitePointMode, defaults.ManualWhitePointColorTemperatureK);
    }

    private void ApplyScanFrame(byte[] imageBytes, int rows, string successStatus)
    {
        _lineBuffer = imageBytes;
        _previewRows = rows;
        _hasValidScanBuffer = true;
        MarkColumnSampleSourceCurrentOwner();
        ExportDngCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsExportDngActionAvailable));

        if (IsPreviewForcedOffForRows(rows))
        {
            ClearPreview();
            StatusText = "ScanDebug_Runtime_StatusPreviewSkippedAuto".GetLocalizedFormat(successStatus, ScanDebugConstants.MaxPreviewRows);
            return;
        }

        if (!IsPreviewEnabled)
        {
            ClearPreview();
            StatusText = "ScanDebug_Runtime_StatusPreviewSkipped".GetLocalizedFormat(successStatus);
            return;
        }

        if (!RenderPreview(rows))
            return;

        StatusText = successStatus;
    }

    private async Task HandleWarmUpToggleChangedAsync(bool enabled)
    {
        if (!IsConnected)
        {
            StatusText = enabled
                ? "ScanDebug_Runtime_StatusWarmUpWillEnableAfterConnect".GetLocalized()
                : "ScanDebug_Runtime_StatusWarmUpDisabled".GetLocalized();
            return;
        }

        var runtimeCommand = enabled
            ? ScanDebugRuntimeCommandKind.DeviceGlobalWrite
            : ScanDebugRuntimeCommandKind.StopScan;
        if (!TryClaimRuntimeOperation(runtimeCommand, out var runtimeClaim))
            return;

        try
        {
            var result = await _sessionCoordinator.SetWarmUpAsync(enabled, _session.ConnectionToken);
            StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message);
        }
        catch (OperationCanceledException)
        {
            StatusText = enabled ? "ScanDebug_Runtime_StatusWarmUpEnableCanceled".GetLocalized() : "ScanDebug_Runtime_StatusWarmUpDisableCanceled".GetLocalized();
        }
        finally
        {
            runtimeClaim?.Dispose();
        }
    }

    private async Task RunAutoCalibrationAsync(Func<IScanSessionService, ScanParameterSnapshot, ScanCalibrationRoiSettings, Func<ScanCalibrationPrompt, Task<bool>>, Action<string>, Action<ScanParameterSnapshot>, Action<byte[], int, string>, CancellationToken, Task<ScanAutoCalibrationCandidateResult>> operation, string successMessage, ScanDebugRuntimeCommandKind command)
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        var roiSettings = ScanAdcCalibrationRoi.TryCreate(_roiSettings, GetRoiEditingWidth());
        if (roiSettings.Value is null)
        {
            StatusText = FormatRoiEditIssue(roiSettings.Issues.FirstOrDefault());
            return;
        }

        var validatedRoiSettings = _roiSettings;
        var context = CreateCalibrationCandidateContext();
        var capturedProfile = TryGetCalibrationProfile(context.SelectedChannel, out var profile) ? profile : null;
        var persistedProfile = _calibrationProfiles.TryGetProfile(context.SelectedChannel, out var persisted) ? persisted : null;
        var capturedBlackLevel = capturedProfile?.BlackLevel ?? persistedProfile?.BlackLevel;
        var capturedWhiteLevel = capturedProfile?.WhiteLevel ?? persistedProfile?.WhiteLevel;

        var producer = _motionRuntimeState.CaptureProducer();
        if (!TryClaimRuntimeOperation(command, out var runtimeClaim))
            return;

        if (!_motionRuntimeState.IsCurrent(producer))
        {
            runtimeClaim?.Dispose();
            return;
        }

        IsAutoCalibrating = true;
        var calibrationCts = CancellationTokenSource.CreateLinkedTokenSource(producer.CancellationToken);
        _autoCalibrationCts = calibrationCts;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusAutoCalibrationStarted".GetLocalized();
            var captured = await _sessionCoordinator.RunConnectedSessionStateAsync(
                ScannerSessionState.Running,
                async (session, token) =>
                {
                    var originalSnapshot = await _parameters.LoadAsync(session, token);
                    ScanIlluminationState? originalIllumination = null;
                    try
                    {
                        originalIllumination = await _illumination.GetStateAsync(session, token);
                        var calibrated = await operation(
                            session,
                            originalSnapshot,
                            validatedRoiSettings,
                            RequestCalibrationPromptAsync,
                            status => _dispatcher.TryEnqueue(() =>
                            {
                                if (IsCurrentCalibrationContext(context))
                                    StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(status);
                            }),
                            applied => _dispatcher.TryEnqueue(() =>
                            {
                                if (IsCurrentCalibrationContext(context))
                                    _ = applied;
                            }),
                            (imageBytes, rows, phase) => _dispatcher.TryEnqueue(() =>
                            {
                                if (IsCurrentCalibrationContext(context))
                                    ShowCalibrationFrame(imageBytes, rows, phase);
                            }),
                            token);
                        return (originalSnapshot, calibrated);
                    }
                    finally
                    {
                        await RestoreCalibrationStateAsync(session, originalSnapshot, originalIllumination);
                    }
                },
                calibrationCts.Token);
            var published = PendingCalibrationResult.Create(
                    captured.originalSnapshot,
                    captured.calibrated.CandidateSnapshot,
                    captured.calibrated.BeforeMetrics,
                    captured.calibrated.AfterMetrics,
                    context,
                    captured.calibrated.Validation,
                    validatedRoiSettings,
                    capturedBlackLevel,
                    capturedWhiteLevel)
                .InvalidateIfStale(CreateCalibrationCandidateContext());
            PendingCalibrationResult = published;
            if (published.State == PendingCalibrationResultState.Pending)
            {
                StatusText = successMessage;
            }
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentCalibrationContext(context))
                StatusText = "ScanDebug_Runtime_StatusAutoCalibrationCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
            if (IsCurrentCalibrationContext(context))
                StatusText = "ScanDebug_Runtime_StatusAutoCalibrationFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_autoCalibrationCts, calibrationCts))
                _autoCalibrationCts = null;
            calibrationCts.Dispose();
            runtimeClaim?.Dispose();
            IsAutoCalibrating = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyIllumination))]
    private Task TestIlluminationOff()
        => ApplySessionIlluminationTestAsync(SessionIlluminationTestKind.Off);

    [RelayCommand(CanExecute = nameof(CanApplyIllumination))]
    private Task TestIlluminationSteady()
        => ApplySessionIlluminationTestAsync(SessionIlluminationTestKind.Steady);

    [RelayCommand(CanExecute = nameof(CanApplyIllumination))]
    private Task TestIlluminationAcquisitionSync()
        => ApplySessionIlluminationTestAsync(SessionIlluminationTestKind.AcquisitionSync);

    [RelayCommand]
    private void CopyRawIlluminationToDraft()
    {
        if (!TryBuildRawIlluminationState(out var state, out var error))
        {
            StatusText = error;
            return;
        }

        ApplyDraftIlluminationStateToInputs(state);
        SynchronizeFilmProfileDraftFromInputs();
    }

    private async Task RestoreCalibrationStateAsync(IScanSessionService session, ScanParameterSnapshot originalSnapshot, ScanIlluminationState? originalIllumination)
    {
        Exception? parameterFailure = null;
        try
        {
            await _parameters.ApplyAsync(session, originalSnapshot, CancellationToken.None);
        }
        catch (Exception ex)
        {
            parameterFailure = ex;
        }

        Exception? illuminationFailure = null;
        if (originalIllumination is not null)
        {
            try
            {
                await _illumination.RestoreStateAsync(session, originalIllumination, CancellationToken.None);
            }
            catch (Exception ex)
            {
                illuminationFailure = ex;
            }
        }

        if (parameterFailure is null && illuminationFailure is null)
            return;

        throw new InvalidOperationException(
            "Calibration completed but original device state could not be restored.",
            parameterFailure ?? illuminationFailure);
    }

    private CalibrationCandidateContext CreateCalibrationCandidateContext()
        => new(
            string.IsNullOrWhiteSpace(SelectedCalibrationChannel) ? "NONE" : SelectedCalibrationChannel,
            $"WORKSPACE-{Volatile.Read(ref _calibrationWorkspaceGeneration)}",
            $"{_sessionCoordinator.Snapshot.DeviceId ?? "UNKNOWN"}:{Volatile.Read(ref _calibrationDeviceSessionGeneration)}");

    private bool IsCurrentCalibrationContext(CalibrationCandidateContext context)
        => context.GetStaleness(CreateCalibrationCandidateContext()) == CalibrationCandidateStaleness.None;

    private void InvalidatePendingCalibrationIfStale()
    {
        if (PendingCalibrationResult is { State: PendingCalibrationResultState.Pending } pending)
            PendingCalibrationResult = pending.InvalidateIfStale(CreateCalibrationCandidateContext());
    }

    private bool CanAcceptPendingCalibration() => PendingCalibrationResult?.CanAccept == true;
    private bool CanCancelPendingCalibration() => PendingCalibrationResult?.State == PendingCalibrationResultState.Pending;

    private static string FormatPendingCalibrationMetric(decimal? before, decimal? after, string suffix)
    {
        if (before is null || after is null)
            return string.Empty;

        return "ScanDebug_Runtime_PendingCalibrationMetric".GetLocalizedFormat(
            FormatPendingCalibrationMetricValue(before.Value, suffix),
            FormatPendingCalibrationMetricValue(after.Value, suffix));
    }

    private static string FormatPendingCalibrationMetricValue(decimal value, string suffix)
        => string.IsNullOrEmpty(suffix)
            ? value.ToString("0.###", CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{value:0.###}{suffix}");

    private static string FormatPendingCalibrationValidationIssue(ScanCalibrationCandidateValidationIssue issue)
        => $"{issue.Severity}: {issue.Detail}";

    [RelayCommand(CanExecute = nameof(CanAcceptPendingCalibration))]
    private Task AcceptPendingCalibration() => ExecutePendingCalibrationAsync(false);

    [RelayCommand(CanExecute = nameof(CanAcceptPendingCalibration))]
    private Task AcceptAndSavePendingCalibration() => ExecutePendingCalibrationAsync(true);

    [RelayCommand(CanExecute = nameof(CanCancelPendingCalibration))]
    private void CancelPendingCalibration()
    {
        if (PendingCalibrationResult is { State: PendingCalibrationResultState.Pending } pending)
            PendingCalibrationResult = pending.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanCancelPendingCalibration))]
    private void RestorePendingCalibration()
    {
        if (PendingCalibrationResult is { State: PendingCalibrationResultState.Pending } pending)
            PendingCalibrationResult = pending.Restore(CreateCalibrationCandidateContext());
    }

    private async Task ExecutePendingCalibrationAsync(bool save)
    {
        if (PendingCalibrationResult is not { State: PendingCalibrationResultState.Pending } pending)
            return;

        var command = save ? ScanDebugRuntimeCommandKind.AcceptAndSaveCalibrationCandidate : ScanDebugRuntimeCommandKind.AcceptCalibrationCandidate;
        if (!TryClaimRuntimeOperation(command, out var claim))
            return;

        using (claim)
        {
            var transitioned = save ? pending.AcceptAndSave(CreateCalibrationCandidateContext()) : pending.Accept(CreateCalibrationCandidateContext());
            PendingCalibrationResult = transitioned;
            if (transitioned.State == PendingCalibrationResultState.Invalidated)
                return;

            var deviceApplied = false;
            try
            {
                await _sessionCoordinator.RunConnectedSessionStateAsync(ScannerSessionState.Running, async (session, token) =>
                {
                    await _parameters.ApplyAsync(session, transitioned.CandidateSnapshot, token);
                    return 0;
                    }, CancellationToken.None);
                deviceApplied = true;
                ApplyCalibrationSnapshotProjection(transitioned.CandidateSnapshot);
                if (save)
                    await SaveSelectedCalibrationProfileAsync(
                        transitioned.CandidateSnapshot,
                        transitioned.SelectedChannel,
                        transitioned.RoiSettings,
                        transitioned.BlackLevel,
                        transitioned.WhiteLevel,
                        useCapturedLevels: true);
            }
            catch (Exception ex)
            {
                Exception? rollbackFailure = null;
                try
                {
                    await _sessionCoordinator.RunConnectedSessionStateAsync(ScannerSessionState.Running, async (session, _) =>
                    {
                        await _parameters.ApplyAsync(session, transitioned.OriginalSnapshot, CancellationToken.None);
                        return 0;
                    }, CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    rollbackFailure = rollbackException;
                    SetDeviceTimingState(ScanDeviceTimingState.Unknown);
                }

                var failure = rollbackFailure is null
                    ? new ScanCalibrationCandidateFailure(
                        deviceApplied && save ? "persistence.failed-after-device-apply" : "device-apply.failed",
                        ex.Message)
                    : new ScanCalibrationCandidateFailure(
                        "device-state.unknown-after-rollback-failure",
                        $"{ex.Message} Original parameter rollback failed: {rollbackFailure.Message}");
                PendingCalibrationResult = PendingCalibrationResult.Create(
                    transitioned.OriginalSnapshot,
                    transitioned.CandidateSnapshot,
                    transitioned.BeforeMetrics,
                    transitioned.AfterMetrics,
                    transitioned.Context,
                    transitioned.Validation,
                    transitioned.RoiSettings,
                    transitioned.BlackLevel,
                    transitioned.WhiteLevel)
                    .Fail(failure);
            }
        }
    }

    private async Task<bool> LoadSelectedCalibrationProfileAsync(string channelRole, int loadVersion, int projectionVersion)
    {
        if (string.IsNullOrWhiteSpace(channelRole))
            return false;

        await _selectedCalibrationChannelPersistenceGate.WaitAsync();
        try
        {
            if (!IsCurrentCalibrationChannel(channelRole, loadVersion) || IsCalibrationProjectionStale(projectionVersion))
                return false;

            await _calibrationProfiles.SetSelectedChannelAsync(channelRole, CancellationToken.None);
            if (!IsCurrentCalibrationChannel(channelRole, loadVersion) || IsCalibrationProjectionStale(projectionVersion))
                return false;
        }
        finally
        {
            _selectedCalibrationChannelPersistenceGate.Release();
        }

        if (!IsCurrentCalibrationChannel(channelRole, loadVersion) || IsCalibrationProjectionStale(projectionVersion))
            return false;

        if (_copiedUnverifiedCalibrationProfiles.TryGetValue(channelRole, out var copiedProfile))
        {
            if (!CanApplyCalibrationProfileProjection(loadVersion, projectionVersion))
                return false;
            ApplyCalibrationProfileProjection(copiedProfile);
            RefreshPreviewIfPossible();
            RefreshCalibrationChannelItems();
            NotifyChannelProfileOverviewChanged();
            return true;
        }

        if (_filmProfileWorkspace.Snapshot.CurrentDraft.ChannelProfiles.TryGetValue(channelRole, out var draftProfile))
        {
            if (!CanApplyCalibrationProfileProjection(loadVersion, projectionVersion))
                return false;
            ApplyCalibrationProfileProjection(draftProfile);
            CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_NoSavedProfile".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole));
            RefreshPreviewIfPossible();
            RefreshCalibrationChannelItems();
            NotifyChannelProfileOverviewChanged();
            return true;
        }

        if (_calibrationProfiles.TryGetProfile(channelRole, out var profile))
        {
            if (!CanApplyCalibrationProfileProjection(loadVersion, projectionVersion))
                return false;
            ApplyCalibrationProfileProjection(profile);
            if (!CanApplyCalibrationProfileProjection(loadVersion, projectionVersion)
                || !IsCurrentCalibrationChannel(channelRole, loadVersion))
            {
                return false;
            }
            _selectedCalibrationEditorReferenceRole = channelRole;
            _selectedCalibrationEditorReferenceProfile = profile;
            CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_SavedProfileLoaded".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole));
            RefreshPreviewIfPossible();
            RefreshCalibrationChannelItems();
            NotifyChannelProfileOverviewChanged();
            return true;
        }

        ClearSelectedCalibrationEditorBaseline();
        _roiSettings = ScanCalibrationRoiSettings.CreateDefault();
        RefreshRoiStatus();
        RefreshColumnSampleStatus();
        CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_NoSavedProfile".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole));
        RefreshPreviewIfPossible();
        RefreshCalibrationChannelItems();
        NotifyChannelProfileOverviewChanged();
        RefreshPreviewIfPossible();
        RefreshCalibrationChannelItems();
        NotifyChannelProfileOverviewChanged();
        return true;
    }

    private bool IsCurrentCalibrationChannel(string channelRole)
        => string.Equals(channelRole, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase);

    private bool IsCurrentCalibrationChannel(string channelRole, int loadVersion)
        => loadVersion == _profileLoadVersion && IsCurrentCalibrationChannel(channelRole);

    private void AdvanceCalibrationProjectionVersion()
        => Interlocked.Increment(ref _calibrationProjectionVersion);

    private bool IsCalibrationProjectionStale(int projectionVersion)
        => projectionVersion != Volatile.Read(ref _calibrationProjectionVersion);

    private bool CanApplyCalibrationProfileProjection(int loadVersion, int projectionVersion)
        => !IsCalibrationRepositoryOperationRunning
            && loadVersion == _profileLoadVersion
            && !IsCalibrationProjectionStale(projectionVersion);

    private async Task<bool> HandleSelectedCalibrationChannelChangedAsync(string channelRole, int projectionVersion)
    {
        var loadVersion = ++_profileLoadVersion;
        var loadTask = CompleteSelectedCalibrationChannelLoadAsync(channelRole, loadVersion, projectionVersion);
        _selectedCalibrationChannelLoad = new CalibrationChannelSelectionLoad(channelRole, loadVersion, projectionVersion, loadTask);
        return await loadTask;
    }

    private async Task<bool> CompleteSelectedCalibrationChannelLoadAsync(string channelRole, int loadVersion, int projectionVersion)
    {
        try
        {
            return await LoadSelectedCalibrationProfileAsync(channelRole, loadVersion, projectionVersion);
        }
        catch (Exception ex)
        {
            if (loadVersion == _profileLoadVersion && string.Equals(channelRole, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
                CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_LoadFailed".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole), ex.Message);
            return false;
        }
    }

    private Task SaveSelectedCalibrationProfileAsync(ScanParameterSnapshot snapshot)
        => SaveSelectedCalibrationProfileAsync(
            snapshot,
            SelectedCalibrationChannel,
            _roiSettings);

    private Task SaveSelectedCalibrationProfileAsync(ScanParameterSnapshot snapshot, string channelRole)
        => SaveSelectedCalibrationProfileAsync(snapshot, channelRole, _roiSettings);

    private async Task SaveSelectedCalibrationProfileAsync(
        ScanParameterSnapshot snapshot,
        string channelRole,
        ScanCalibrationRoiSettings roiSettings,
        ushort? blackLevel = null,
        ushort? whiteLevel = null,
        bool useCapturedLevels = false)
    {
        if (string.IsNullOrWhiteSpace(channelRole))
            return;

        var existingProfile = TryGetCalibrationProfile(channelRole, out var profile) ? profile : null;
        AdvanceCalibrationProjectionVersion();
        try
        {
            await _calibrationProfiles.SaveProfileAsync(
                channelRole,
                new ScanChannelCalibrationProfile(
                    snapshot,
                    roiSettings,
                    useCapturedLevels ? blackLevel : existingProfile?.BlackLevel,
                    useCapturedLevels ? whiteLevel : existingProfile?.WhiteLevel),
                CancellationToken.None);
        }
        finally
        {
        }
        _copiedUnverifiedCalibrationProfiles.Remove(channelRole);
        if (IsCurrentCalibrationChannel(channelRole))
        {
            CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_SavedAt".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole), DateTime.Now.ToString("HH:mm:ss"));
            RefreshColumnSampleStatus();
        }
        RefreshCalibrationChannelItems();
        NotifyChannelProfileOverviewChanged();
    }

    private async Task SaveCalibrationLevelsAsync(ScanParameterSnapshot snapshot, ushort? blackLevel, ushort? whiteLevel, string channelRole)
    {
        if (string.IsNullOrWhiteSpace(channelRole))
            return;

        AdvanceCalibrationProjectionVersion();
        try
        {
            await _calibrationProfiles.SaveProfileAsync(
                channelRole,
                new ScanChannelCalibrationProfile(
                    snapshot,
                    _roiSettings,
                    blackLevel,
                    whiteLevel),
                CancellationToken.None);
        }
        finally
        {
            AdvanceCalibrationProjectionVersion();
        }
        _copiedUnverifiedCalibrationProfiles.Remove(channelRole);
        if (IsCurrentCalibrationChannel(channelRole))
            CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_SavedAt".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole), DateTime.Now.ToString("HH:mm:ss"));
        RefreshCalibrationChannelItems();
        NotifyChannelProfileOverviewChanged();
    }

    private bool TryResolveSnapshotForLevelSave(string channelRole, out ScanParameterSnapshot snapshot, out string error)
    {
        if (_parameters.TryParseInput(ExposureMicroseconds, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockMhz, out snapshot, out error))
            return true;

        if (TryGetCalibrationProfile(channelRole, out var existingProfile))
        {
            snapshot = existingProfile.Parameters;
            error = string.Empty;
            return true;
        }

        snapshot = new ScanParameterSnapshot(ScanDebugConstants.MinExposureTicks, 0, 0, 0, 0, ScanDebugConstants.MinSysClockKhz);
        return false;
    }

    private string ResolveProfileChannelToLoad(ScanFilmParameterProfileSet imported)
    {
        var selectedChannel = imported.ChannelProfiles.Keys.FirstOrDefault(role =>
            !string.IsNullOrWhiteSpace(imported.SelectedCalibrationChannel)
            && string.Equals(role, imported.SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase)
            && CalibrationChannelOptions.Contains(role, StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(selectedChannel))
        {
            return selectedChannel;
        }

        var firstKnown = CalibrationChannelOptions.FirstOrDefault(option => imported.ChannelProfiles.Keys.Any(role =>
            string.Equals(role, option, StringComparison.OrdinalIgnoreCase)));
        if (!string.IsNullOrWhiteSpace(firstKnown))
            return firstKnown;

        return string.Empty;
    }

    private bool TryBuildFilmAcquisitionSettings(out ScanFilmAcquisitionSettings settings, out ScanFilmProfileValidationIssue? issue)
    {
        settings = ScanFilmAcquisitionSettings.CreateDefault();

        if (!TryBuildIlluminationRequest(out var illuminationRequest, out var error, clearUnusedInputs: false))
        {
            issue = CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidAcquisitionInput, "AcquisitionSettings.Illumination", "FilmProfile.Validation.AcquisitionInputInvalid");
            return false;
        }

        if (!TryParseRequestedRows(out var rows))
        {
            issue = CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidAcquisitionInput, "AcquisitionSettings.Rows", "FilmProfile.Validation.AcquisitionInputInvalid");
            return false;
        }

        if (!TryParseSelectedScanMotor(out var motorId, out error))
        {
            issue = CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidAcquisitionInput, "AcquisitionSettings.ScanMotor", "FilmProfile.Validation.AcquisitionInputInvalid");
            return false;
        }

        var motorSettings = _deviceSettings.Settings.GetMotorSettings(motorId);
        uint motorIntervalNs;
        if (!_isMotorDistanceDerivedFromInterval && !string.IsNullOrWhiteSpace(MotorDistancePerLineValue))
        {
            if (!ScanMotorDistanceText.TryParseMillimeters(MotorDistancePerLineValue, MotorDistancePerLineUnit, motorSettings, out var lineDistanceMm)
                || !_parameters.TryParseInput(ExposureMicroseconds, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockMhz, out var snapshot, out _)
                || !ScanTimingMath.TryConvertLineDistanceMillimetersToMotorIntervalNs(lineDistanceMm, snapshot.ExposureTicks, snapshot.SysClockKhz, motorSettings, ScanDebugConstants.MotionMinIntervalNs, out motorIntervalNs))
            {
                issue = CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidAcquisitionInput, "AcquisitionSettings.TargetLinePitchMicrometers", "FilmProfile.Validation.AcquisitionInputInvalid");
                return false;
            }
        }
        else
        {
            var (_, _, _, speedValueText, speedUnitText) = GetMotorMoveInputs(motorId);
            if (!TryBuildMotorIntervalFromInputs(motorId, speedValueText, speedUnitText, out motorIntervalNs, out error))
            {
                issue = CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidAcquisitionInput, "AcquisitionSettings.MotorIntervalNs", "FilmProfile.Validation.AcquisitionInputInvalid");
                return false;
            }
        }

        double? targetLinePitchMicrometers = null;
        if (!_isMotorDistanceDerivedFromInterval
            && !string.IsNullOrWhiteSpace(MotorDistancePerLineValue)
            && ScanMotorDistanceText.TryParseMillimeters(MotorDistancePerLineValue, MotorDistancePerLineUnit, motorSettings, out var targetLinePitchMm))
        {
            targetLinePitchMicrometers = targetLinePitchMm * 1000.0;
        }

        var acquisitionProjection = _selectedFilmAcquisitionSettings?.Normalize() ?? ScanFilmAcquisitionSettings.CreateDefault();
        settings = new ScanFilmAcquisitionSettings(
            illuminationRequest.Led1Level,
            illuminationRequest.Led2Level,
            illuminationRequest.Led3Level,
            illuminationRequest.Led4Level,
            illuminationRequest.SteadyMask,
            illuminationRequest.SyncMask,
            illuminationRequest.Led1PulseClock,
            illuminationRequest.Led2PulseClock,
            illuminationRequest.Led3PulseClock,
            illuminationRequest.Led4PulseClock,
            motorIntervalNs,
            acquisitionProjection.Led1ChannelColor,
            acquisitionProjection.Led2ChannelColor,
            acquisitionProjection.Led3ChannelColor,
            acquisitionProjection.Led4ChannelColor,
            Rows: rows,
            ScanMotorId: motorId,
            TargetLinePitchMicrometers: targetLinePitchMicrometers,
            StartingDirectionPositive: string.Equals(SelectedStartingDirection, ForwardDirection, StringComparison.OrdinalIgnoreCase),
            WarmUpEnabled: IsWarmUpEnabled,
            TransportStrategy: IsAlternateMotorDirectionEnabled ? ScanFilmTransportStrategy.AlternateDirection : ScanFilmTransportStrategy.ReturnToStart,
            AcquisitionChannelAssignment: BuildDebugChannelAssignment()).Normalize();
        issue = null;
        return true;
    }

    private bool TryBuildCurrentFilmProfileDraft(out ScanFilmProfileDraft draft, out ScanFilmProfileValidationResult validation, string? channelToPatch = null)
    {
        var issues = new List<ScanFilmProfileValidationIssue>();
        var profileName = FilmProfileName.Trim();
        if (profileName.Length == 0)
            issues.Add(CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidProfileName, "profileName", "FilmProfile.Validation.ProfileNameInvalid", ScanFilmProfileValidationSeverity.Warning));

        if (!_parameters.TryParseInput(ExposureMicroseconds, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockMhz, out var snapshot, out var parameterError))
            issues.Add(CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidChannelParameters, "ChannelProfiles.Selected.Parameters", "FilmProfile.Validation.ChannelParametersInvalid"));

        var hasAcquisition = TryBuildFilmAcquisitionSettings(out var acquisitionSettings, out var acquisitionIssue);
        if (!hasAcquisition && acquisitionIssue is not null)
            issues.Add(acquisitionIssue);

        if (!HasSelectedAcquisitionChannels())
            issues.Add(CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidAcquisitionInput, "AcquisitionSettings.ChannelAssignment", "FilmProfile.Validation.AcquisitionInputInvalid"));

        if (!TryValidateRoiInputs())
            issues.Add(CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode.InvalidRoiInput, "ChannelProfiles.Selected.RoiSettings", "FilmProfile.Validation.RoiInputInvalid"));

        TryBuildScanRecipeSettings(out var recipeSettings, issues);

        validation = new ScanFilmProfileValidationResult(issues);
        if (!validation.IsValid)
        {
            draft = _filmProfileWorkspace.Snapshot.CurrentDraft;
            return false;
        }

        var current = _filmProfileWorkspace.Snapshot.CurrentDraft;
        var profiles = current.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var patchChannel = channelToPatch ?? SelectedCalibrationChannel;
        if (!string.IsNullOrWhiteSpace(patchChannel))
            profiles[patchChannel] = BuildCurrentChannelPatch(snapshot, patchChannel);

        draft = new ScanFilmProfileDraft(profileName, current.SavedAtUtc, profiles, SelectedCalibrationChannel, acquisitionSettings, recipeSettings);
        return true;
    }

    private ScanChannelCalibrationProfile BuildCurrentChannelPatch(ScanParameterSnapshot snapshot, string channelRole)
    {
        if (!string.Equals(channelRole, _selectedCalibrationEditorReferenceRole, StringComparison.OrdinalIgnoreCase))
            ClearSelectedCalibrationEditorBaseline();

        ScanChannelCalibrationProfile? existing = null;
        if (_copiedUnverifiedCalibrationProfiles.TryGetValue(channelRole, out var copiedProfile))
            existing = copiedProfile;
        else if (_filmProfileWorkspace.Snapshot.CurrentDraft.ChannelProfiles.TryGetValue(channelRole, out var draftProfile))
            existing = draftProfile;
        else if (string.Equals(channelRole, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase)
            && string.Equals(channelRole, _selectedCalibrationEditorReferenceRole, StringComparison.OrdinalIgnoreCase))
            existing = _selectedCalibrationEditorReferenceProfile;
        else
            ClearSelectedCalibrationEditorBaseline();

        return new ScanChannelCalibrationProfile(snapshot, _roiSettings, existing?.BlackLevel, existing?.WhiteLevel);
    }

    private void ClearSelectedCalibrationEditorBaseline()
    {
        _selectedCalibrationEditorReferenceProfile = null;
        _selectedCalibrationEditorReferenceRole = null;
    }

    private bool SynchronizeFilmProfileDraftFromInputs(string? channelToPatch = null)
    {
        if (_isSynchronizingFilmProfileWorkspace)
            return false;

        var isValid = TryBuildCurrentFilmProfileDraft(out var draft, out var validation, channelToPatch);
        if (isValid)
        {
            _hasInvalidFilmProfileInput = false;
            _selectedFilmAcquisitionSettings = draft.AcquisitionSettings?.Normalize();
            _lastProjectedFilmProfileDraft = draft;
            _filmProfileWorkspace.SetCurrentDraft(draft);
        }
        else
        {
            _hasInvalidFilmProfileInput = true;
        }

        SetCurrentFilmProfileValidation(validation);
        RefreshFilmProfileWorkspaceProjection();
        SaveFilmProfileJsonCommand.NotifyCanExecuteChanged();
        return isValid;
    }

    private void PublishFilmProfileOperation(string message, InfoBarSeverity severity)
    {
        var publicationId = Interlocked.Increment(ref _filmProfileOperationPublicationId);
        CancelFilmProfileOperationAutoCloseTimer();
        FilmProfileOperationMessage = message;
        FilmProfileOperationSeverity = severity;
        FilmProfileOperationIsOpen = true;
        FilmProfileOperationVisibility = Visibility.Visible;
        if (ShouldAutoCloseFilmProfileOperation(severity))
            ScheduleFilmProfileOperationAutoCloseTimer(publicationId);
    }

    private void ScheduleFilmProfileOperationAutoCloseTimer(long publicationId)
        => _filmProfileOperationAutoCloseTimer = _operationTimeProvider.CreateTimer(
            _ => _dispatcher.TryEnqueue(() => CloseFilmProfileOperation(publicationId)),
            null,
            FilmProfileOperationAutoCloseDelay,
            Timeout.InfiniteTimeSpan);

    private void CloseFilmProfileOperation(long publicationId)
    {
        if (publicationId != Volatile.Read(ref _filmProfileOperationPublicationId))
            return;

        CancelFilmProfileOperationAutoCloseTimer();
        FilmProfileOperationIsOpen = false;
    }

    private void CancelFilmProfileOperationAutoCloseTimer()
    {
        _filmProfileOperationAutoCloseTimer?.Dispose();
        _filmProfileOperationAutoCloseTimer = null;
    }

    private static bool ShouldAutoCloseFilmProfileOperation(InfoBarSeverity severity)
        => severity is InfoBarSeverity.Success or InfoBarSeverity.Informational;

    private void RefreshFilmProfileWorkspaceProjection()
    {
        if (_isSynchronizingFilmProfileWorkspace)
            return;

        HasUnsavedProfileChanges = _isNewFilmProfilePendingExport
            || ScanFilmProfileDirtyState.IsDirty(_filmProfileWorkspace.Snapshot.IsDirty, _hasInvalidFilmProfileInput);
        RefreshCalibrationChannelItems();
        SaveFilmProfileJsonCommand.NotifyCanExecuteChanged();
    }

    private void InitializeCalibrationChannelItems()
    {
        foreach (var role in CalibrationChannelOptions)
            CalibrationChannelItems.Add(new ScanDebugCalibrationChannelItemViewModel(this, role));

        OnPropertyChanged(nameof(SelectedCalibrationChannelItem));
    }

    private void RefreshCalibrationChannelItems()
    {
        foreach (var channel in CalibrationChannelItems)
            channel.Refresh();

        OnPropertyChanged(nameof(SelectedCalibrationChannelItem));
        CopyCalibrationProfileFromChannelCommand.NotifyCanExecuteChanged();
    }

    internal CalibrationChannelStatusKind GetCalibrationChannelStatusKind(string channelRole)
    {
        var isSelected = string.Equals(channelRole, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase);
        var persistedProfile = _calibrationProfiles.TryGetProfile(channelRole, out var persisted) ? persisted : null;
        var draftProfile = _filmProfileWorkspace.Snapshot.CurrentDraft.ChannelProfiles.TryGetValue(channelRole, out var draft)
            ? draft
            : null;
        var hasState = persistedProfile is not null
            || draftProfile is not null
            || _copiedUnverifiedCalibrationProfiles.ContainsKey(channelRole);
        ScanChannelCalibrationProfile? profile = null;
        var hasCurrentEditorProfile = isSelected && TryBuildCurrentCalibrationProfile(out profile!);
        var hasInvalidEditorValues = isSelected && hasState && !hasCurrentEditorProfile;
        var editorProfile = isSelected
            ? hasCurrentEditorProfile ? profile : null
            : draftProfile;
        return ResolveCalibrationChannelStatus(
            persistedProfile,
            editorProfile,
            hasInvalidEditorValues,
            _copiedUnverifiedCalibrationProfiles.ContainsKey(channelRole));
    }

    private static CalibrationChannelStatusKind ResolveCalibrationChannelStatus(
        ScanChannelCalibrationProfile? persistedProfile,
        ScanChannelCalibrationProfile? editorProfile,
        bool hasInvalidEditorValues,
        bool isCopiedUnverified)
    {
        if (isCopiedUnverified)
            return CalibrationChannelStatusKind.CopiedUnverified;

        if (hasInvalidEditorValues)
            return CalibrationChannelStatusKind.Invalid;

        if (persistedProfile is null)
            return editorProfile is null
                ? CalibrationChannelStatusKind.Missing
                : CalibrationChannelStatusKind.Modified;

        if (editorProfile is null)
            return CalibrationChannelStatusKind.Saved;

        return NormalizeCalibrationProfile(persistedProfile) == NormalizeCalibrationProfile(editorProfile)
            ? CalibrationChannelStatusKind.Saved
            : CalibrationChannelStatusKind.Modified;
    }

    internal string GetCalibrationChannelStatusText(string channelRole)
        => GetCalibrationChannelStatusKind(channelRole) switch
        {
            CalibrationChannelStatusKind.Saved => "ScanDebug_Runtime_ChannelStatusSaved".GetLocalized(),
            CalibrationChannelStatusKind.Invalid => "ScanDebug_Runtime_ChannelStatusInvalid".GetLocalized(),
            CalibrationChannelStatusKind.Modified => "ScanDebug_Runtime_ChannelStatusModified".GetLocalized(),
            CalibrationChannelStatusKind.CopiedUnverified => "ScanDebug_Runtime_ChannelStatusCopiedUnverified".GetLocalized(),
            CalibrationChannelStatusKind.Missing => "ScanDebug_Runtime_ChannelStatusMissing".GetLocalized(),
            _ => "ScanDebug_Runtime_ChannelStatusMissing".GetLocalized()
        };

    private bool TryBuildCurrentCalibrationProfile(out ScanChannelCalibrationProfile profile)
    {
        if (_parameters.TryParseInput(ExposureMicroseconds, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockMhz, out var snapshot, out _)
            && TryValidateRoiInputs())
        {
            var existingProfile = TryGetCalibrationProfile(SelectedCalibrationChannel, out var persisted) ? persisted : null;
            profile = new ScanChannelCalibrationProfile(
                snapshot,
                _roiSettings,
                existingProfile?.BlackLevel,
                existingProfile?.WhiteLevel);
            return true;
        }

        profile = null!;
        return false;
    }

    private bool TryGetCalibrationProfile(string channelRole, out ScanChannelCalibrationProfile profile)
        => _copiedUnverifiedCalibrationProfiles.TryGetValue(channelRole, out profile!)
           || _filmProfileWorkspace.Snapshot.CurrentDraft.ChannelProfiles.TryGetValue(channelRole, out profile!)
           || _calibrationProfiles.TryGetProfile(channelRole, out profile!);

    private static ScanChannelCalibrationProfile NormalizeCalibrationProfile(ScanChannelCalibrationProfile profile)
    {
        ScanDebugValidation.TryNormalizeSnapshot(profile.Parameters, out var parameters);
        return new ScanChannelCalibrationProfile(
            parameters,
            profile.RoiSettings.Normalize(),
            profile.BlackLevel,
            profile.WhiteLevel is > 0 ? profile.WhiteLevel : null);
    }

    private static bool HaveSameNormalizedCalibrationChannelProfiles(
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? previous,
        IReadOnlyDictionary<string, ScanChannelCalibrationProfile> current)
    {
        if (previous is null || previous.Count != current.Count)
            return false;

        foreach (var pair in current)
        {
            if (!previous.TryGetValue(pair.Key, out var previousProfile)
                || NormalizeCalibrationProfile(previousProfile) != NormalizeCalibrationProfile(pair.Value))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasCurrentCalibrationEditorValues()
        => !string.IsNullOrWhiteSpace(ExposureTicks)
           || !string.IsNullOrWhiteSpace(Adc1Offset)
           || !string.IsNullOrWhiteSpace(Adc1Gain)
           || !string.IsNullOrWhiteSpace(Adc2Offset)
           || !string.IsNullOrWhiteSpace(Adc2Gain)
           || !string.IsNullOrWhiteSpace(SysClockKhz)
           || !string.IsNullOrWhiteSpace(RoiStartInput)
           || !string.IsNullOrWhiteSpace(RoiEndInput);

    private static ScanFilmProfileValidationIssue CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode code, string fieldPath, string messageKey)
        => CreateFilmProfileValidationIssue(code, fieldPath, messageKey, ScanFilmProfileValidationSeverity.Error);

    private static ScanFilmProfileValidationIssue CreateFilmProfileValidationIssue(ScanFilmProfileValidationCode code, string fieldPath, string messageKey, ScanFilmProfileValidationSeverity severity)
        => new(code, fieldPath, severity, messageKey);

    private bool TryValidateRoiInputs()
    {
        return int.TryParse(RoiStartInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && int.TryParse(RoiEndInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && ScanAdcCalibrationRoi.TryCreate(_roiSettings, GetRoiEditingWidth()).IsValid
            && ScanFocusRoi.TryCreate(_roiSettings, GetRoiEditingWidth()).IsValid;
    }

    private void SetCurrentFilmProfileValidation(ScanFilmProfileValidationResult validation)
    {
        CurrentFilmProfileValidationIssues = validation.Issues;
        SaveFilmProfileJsonCommand.NotifyCanExecuteChanged();
    }

    private void SetStagedFilmProfileImportValidation(ScanFilmProfileValidationResult validation)
        => StagedFilmProfileImportValidationIssues = validation.Issues;

    private void ClearStagedFilmProfileImportValidation()
        => SetStagedFilmProfileImportValidation(new ScanFilmProfileValidationResult());

    private void SetFilmProfileImportError(ScanFilmProfileValidationResult validation)
    {
        _filmProfileWorkspace.SetImportError(validation);
        SetStagedFilmProfileImportValidation(validation);
        RefreshFilmProfileWorkspaceProjection();
    }

    private void SetFilmProfileImportNone()
        => ClearStagedFilmProfileImportValidation();

    private static string FormatFilmProfileValidationIssues(IReadOnlyList<ScanFilmProfileValidationIssue> issues)
        => string.Join(Environment.NewLine, issues.Select(issue => string.IsNullOrWhiteSpace(issue.FieldPath)
            ? FilmProfileValidationTextPresenter.GetValidationIssueText(issue)
            : $"{issue.FieldPath}: {FilmProfileValidationTextPresenter.GetValidationIssueText(issue)}"));

    private static string FormatFilmProfileValidationCount(int count)
        => count == 0
            ? "ScanDebug_FilmProfileWorkbenchValidationValid".GetLocalized()
            : "ScanDebug_FilmProfileWorkbenchValidationIssues".GetLocalizedFormat(count);

    private IReadOnlyList<ScanFilmProfileValidationIssueDisplay> BuildCurrentFilmProfileValidationIssueDisplays(ScanFilmProfileIssueNavigationSection section)
        => CurrentFilmProfileValidationIssues
            .Select(issue => new
            {
                Issue = issue,
                Request = ScanFilmProfileIssueNavigation.Resolve(issue, ScanFilmProfileIssueSource.CurrentDraft, CalibrationChannelOptions)
            })
            .Where(row => row.Request?.Section == section)
            .Select(row => BuildFilmProfileValidationIssueDisplay(ScanFilmProfileIssueSource.CurrentDraft, row.Issue, row.Request))
            .ToArray();

    private InfoBarSeverity GetCurrentFilmProfileValidationSeverity(ScanFilmProfileIssueNavigationSection section)
    {
        var sectionIssues = BuildCurrentFilmProfileValidationIssueDisplays(section)
            .Select(display => display.Issue)
            .ToArray();
        return sectionIssues.Any(issue => issue.Severity == ScanFilmProfileValidationSeverity.Error)
            ? InfoBarSeverity.Error
            : sectionIssues.Length > 0
                ? InfoBarSeverity.Warning
                : InfoBarSeverity.Success;
    }

    private IReadOnlyList<ScanFilmProfileValidationIssueDisplay> BuildFilmProfileValidationIssueDisplays(
        ScanFilmProfileIssueSource source,
        IReadOnlyList<ScanFilmProfileValidationIssue> issues)
        => issues.Select(issue => BuildFilmProfileValidationIssueDisplay(
            source,
            issue,
            ScanFilmProfileIssueNavigation.Resolve(issue, source, CalibrationChannelOptions))).ToArray();

    private static ScanFilmProfileValidationIssueDisplay BuildFilmProfileValidationIssueDisplay(
        ScanFilmProfileIssueSource source,
        ScanFilmProfileValidationIssue issue,
        ScanFilmProfileIssueNavigationRequest? request)
    {
        var message = FilmProfileValidationTextPresenter.GetValidationIssueText(issue);
        var fieldPath = issue.FieldPath;
        return new ScanFilmProfileValidationIssueDisplay
        {
            Source = source,
            Issue = issue,
            FieldPath = fieldPath,
            Message = message,
            AutomationId = FormatFilmProfileIssueAutomationId(source, issue),
            AutomationName = string.IsNullOrWhiteSpace(fieldPath)
                ? message
                : $"{fieldPath}: {message}",
            CanNavigate = source == ScanFilmProfileIssueSource.CurrentDraft && request is not null
        };
    }

    private static string FormatFilmProfileIssueAutomationId(ScanFilmProfileIssueSource source, ScanFilmProfileValidationIssue issue)
    {
        var path = string.IsNullOrWhiteSpace(issue.FieldPath) ? "Document" : issue.FieldPath.Replace('.', '_');
        return $"FilmProfileIssue_{source}_{issue.Code}_{path}";
    }

    private void ApplyDraftToFields(ScanFilmProfileDraft draft)
    {
        var wasSynchronizing = _isSynchronizingFilmProfileWorkspace;
        _isSynchronizingFilmProfileWorkspace = true;
        try
        {
            ResetProfileEditorFields();
            FilmProfileName = draft.ProfileName;
            _selectedFilmAcquisitionSettings = draft.AcquisitionSettings?.Normalize();
            if (_selectedFilmAcquisitionSettings is not null)
                ApplyProfileAcquisitionSettings(_selectedFilmAcquisitionSettings);
            ApplyScanRecipeSettings(draft.ScanRecipeSettings);

            var channel = ResolveProfileChannelToLoad(new ScanFilmParameterProfileSet(
                ScanFilmProfileDocumentService.CurrentSchemaVersionValue,
                draft.ProfileName,
                draft.SavedAtUtc,
                draft.ChannelProfiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                draft.SelectedCalibrationChannel,
                draft.AcquisitionSettings,
                draft.ScanRecipeSettings));
            if (string.IsNullOrWhiteSpace(channel) && draft.ChannelProfiles.Count > 0)
                channel = draft.ChannelProfiles.Keys.First();
            SelectedCalibrationChannel = channel;
            RefreshCalibrationChannelItems();
            if (!string.IsNullOrWhiteSpace(channel))
            {
                if (draft.ChannelProfiles.TryGetValue(channel, out var profile))
                {
                    ApplyCalibrationSnapshotProjection(profile.Parameters);
                    _roiSettings = profile.RoiSettings.Normalize();
                }
            }

            if (_isMotorDistanceDerivedFromInterval)
                RefreshDerivedMotorDistanceFromCurrentInterval();

            RefreshRoiStatus();
            RefreshColumnSampleStatus();
            NotifyChannelProfileOverviewChanged();
        }
        finally
        {
            _isSynchronizingFilmProfileWorkspace = wasSynchronizing;
            _hasInvalidFilmProfileInput = false;
            RefreshFilmProfileWorkspaceProjection();
        }
    }

    private void ResetProfileEditorFields()
    {
        ClearSelectedCalibrationEditorBaseline();
        FilmProfileName = string.Empty;
        SelectedCalibrationChannel = string.Empty;
        ExposureTicks = string.Empty;
        Adc1Offset = string.Empty;
        Adc1Gain = string.Empty;
        Adc2Offset = string.Empty;
        Adc2Gain = string.Empty;
        SysClockKhz = string.Empty;
        _roiSettings = ScanCalibrationRoiSettings.CreateDefault();

        var defaultAcquisitionSettings = ScanFilmAcquisitionSettings.CreateDefault();
        _selectedFilmAcquisitionSettings = defaultAcquisitionSettings;
        ApplyProfileAcquisitionSettings(defaultAcquisitionSettings);

        IsChannel1Reversed = false;
        IsChannel2Reversed = false;
        IsChannel3Reversed = false;
        IsChannel4Reversed = false;

        var defaultColorManagement = ScanColorManagementOptions.CreateDefault();
        IsScanRecipeColorManagementEnabled = defaultColorManagement.IsEnabled;
        ScanRecipeRedWavelengthNm = FormatColorDouble(defaultColorManagement.RedWavelengthNm);
        ScanRecipeGreenWavelengthNm = FormatColorDouble(defaultColorManagement.GreenWavelengthNm);
        ScanRecipeBlueWavelengthNm = FormatColorDouble(defaultColorManagement.BlueWavelengthNm);
        ScanRecipeOutputGamma = FormatColorDouble(defaultColorManagement.OutputGamma);
        SelectedScanRecipeTargetWhitePointMode = defaultColorManagement.TargetWhitePointMode.ToString();
        ScanRecipeManualWhitePointColorTemperatureK = FormatColorDouble(defaultColorManagement.ManualWhitePointColorTemperatureK);
        SelectedProfileAlignmentMode = AlignmentModeOptions[0];
        SelectedProfileDngExportMode = DngExportModeOptions[0];
    }

    private Task<bool> RequestCalibrationPromptAsync(ScanCalibrationPrompt prompt)
    {
        var request = new ScanCalibrationPromptRequest(prompt);
        CalibrationPromptRequested?.Invoke(this, request);
        return request.CompletionSource.Task;
    }

    private Task<bool> RequestFilmProfileDiscardConfirmationAsync()
    {
        var handler = FilmProfileDiscardConfirmationRequested;
        if (handler is null)
            return Task.FromResult(false);

        var request = new ScanFilmProfileDiscardConfirmationRequest();
        handler(this, request);
        return request.CompletionSource.Task;
    }

    private Task<bool> RequestFilmProfileImportDiscardConfirmationAsync()
        => RequestFilmProfileImportConfirmationAsync(new ScanFilmProfileDiscardConfirmationRequest(
            "ScanDebug_Runtime_FilmProfileImportDiscardConfirmationTitle",
            "ScanDebug_Runtime_FilmProfileImportDiscardConfirmationMessage",
            "ScanDebug_Runtime_FilmProfileDirtyConfirmationDiscardButton",
            "ScanDebug_Runtime_FilmProfileDirtyConfirmationStayButton"));

    private Task<bool> RequestFilmProfileImportReplacementConfirmationAsync()
        => RequestFilmProfileImportConfirmationAsync(new ScanFilmProfileDiscardConfirmationRequest(
            "ScanDebug_Runtime_FilmProfileImportReplacementConfirmationTitle",
            "ScanDebug_Runtime_FilmProfileImportReplacementConfirmationMessage",
            "ScanDebug_Runtime_FilmProfileImportReplacementConfirmationOpenButton",
            "ScanDebug_Runtime_FilmProfileDirtyConfirmationStayButton"));

    private Task<bool> RequestFilmProfileImportConfirmationAsync(ScanFilmProfileDiscardConfirmationRequest request)
    {
        var handler = FilmProfileDiscardConfirmationRequested;
        if (handler is null)
            return Task.FromResult(false);

        handler(this, request);
        return request.CompletionSource.Task;
    }

    private Task RequestNoticeAsync(string title, string content, string closeButtonText)
    {
        var request = new ScanNoticeRequest(title, content, closeButtonText);
        NoticeRequested?.Invoke(this, request);
        return request.CompletionSource.Task;
    }

    private bool CanRunExtendedScan() =>
        _transferSettings.Settings.ReadMode == ScanBulkInReadMode.MultiBuffered &&
        _transferSettings.Settings.RawIoEnabled;

    private void ReportScanReadProgress(int transferredBytes, int totalBytes)
        => _dispatcher.TryEnqueue(() =>
        {
            IsScanReadProgressVisible = true;
            ScanReadProgressMaximum = Math.Max(1, totalBytes);
            ScanReadProgressValue = Math.Clamp(transferredBytes, 0, totalBytes);
        });

    private int BeginStreamingScanPreview(int targetRows)
    {
        lock (_streamingPreviewLock)
        {
            _streamingPreviewSessionVersion++;
            _isStreamingPreviewActive = true;
            _streamingPreviewTargetRows = targetRows;
            _pendingStreamingPreviewRows = 0;
            _isStreamingPreviewQueued = false;
            _lastStreamingPreviewEnqueueTick = 0;
            _streamingWorkflowPreviewRequest = null;
            _streamingWorkflowPreviewPassBuffers = null;
            _streamingWorkflowPreviewPassCompletedRows = null;
            _streamingWorkflowPreviewPassDirections = null;
            _streamingWorkflowPreviewPassMotorSteps = null;
            _streamingWorkflowPreviewPassMotorIntervals = null;
            return _streamingPreviewSessionVersion;
        }
    }

    private void EndStreamingScanPreview(int previewSessionVersion)
    {
        lock (_streamingPreviewLock)
        {
            if (!_isStreamingPreviewActive || _streamingPreviewSessionVersion != previewSessionVersion)
                return;

            _isStreamingPreviewActive = false;
            _streamingPreviewSessionVersion++;
            _pendingStreamingPreviewRows = 0;
            _isStreamingPreviewQueued = false;
            _streamingWorkflowPreviewRequest = null;
            _streamingWorkflowPreviewPassBuffers = null;
            _streamingWorkflowPreviewPassCompletedRows = null;
            _streamingWorkflowPreviewPassDirections = null;
            _streamingWorkflowPreviewPassMotorSteps = null;
            _streamingWorkflowPreviewPassMotorIntervals = null;
        }
    }

    private void QueueStreamingWorkflowPreviewFrame(int previewSessionVersion, ScanWorkflowRequest request, ScanWorkflowRowsAvailable snapshot)
    {
        if (snapshot.CompletedRows <= 0)
            return;

        var shouldQueue = false;
        var delayMs = 0;
        lock (_streamingPreviewLock)
        {
            if (!_isStreamingPreviewActive
                || _streamingPreviewSessionVersion != previewSessionVersion
                || snapshot.CompletedRows > _streamingPreviewTargetRows
                || IsPreviewForcedOffForRows(_streamingPreviewTargetRows)
                || !IsPreviewEnabled)
            {
                return;
            }

            var passCount = request.PassChannelRoles.Length;
            _streamingWorkflowPreviewRequest = request;
            _streamingWorkflowPreviewPassBuffers ??= CreateWorkflowPreviewPassBuffers(request.Rows, passCount);
            _streamingWorkflowPreviewPassCompletedRows ??= new int[passCount];
            _streamingWorkflowPreviewPassDirections ??= new bool[passCount];
            _streamingWorkflowPreviewPassMotorSteps ??= new uint[passCount];
            _streamingWorkflowPreviewPassMotorIntervals ??= new uint[passCount];

            if (snapshot.PassIndex < 0 || snapshot.PassIndex >= _streamingWorkflowPreviewPassBuffers.Length)
                return;

            var passBuffer = _streamingWorkflowPreviewPassBuffers[snapshot.PassIndex];
            var currentRows = _streamingWorkflowPreviewPassCompletedRows[snapshot.PassIndex];
            int destinationOffset;
            int copyLength;
            try
            {
                if (snapshot.RowCount >= 0)
                {
                    if (snapshot.StartRow != currentRows
                        || checked(snapshot.StartRow + snapshot.RowCount) != snapshot.CompletedRows
                        || snapshot.CompletedRows > _streamingPreviewTargetRows
                        || snapshot.ImageBytes.Length != checked(snapshot.RowCount * ScanDebugConstants.BytesPerLine))
                        return;

                    destinationOffset = checked(snapshot.StartRow * ScanDebugConstants.BytesPerLine);
                    copyLength = snapshot.ImageBytes.Length;
                }
                else
                {
                    if (snapshot.CompletedRows <= currentRows || snapshot.CompletedRows > _streamingPreviewTargetRows)
                        return;

                    copyLength = checked(snapshot.CompletedRows * ScanDebugConstants.BytesPerLine);
                    if (snapshot.ImageBytes.Length < copyLength)
                        return;

                    destinationOffset = 0;
                }
            }
            catch (OverflowException)
            {
                return;
            }

            if (destinationOffset < 0 || copyLength < 0 || destinationOffset > passBuffer.Length - copyLength)
                return;

            Buffer.BlockCopy(snapshot.ImageBytes, 0, passBuffer, destinationOffset, copyLength);
            _streamingWorkflowPreviewPassCompletedRows[snapshot.PassIndex] = snapshot.CompletedRows;
            _streamingWorkflowPreviewPassDirections[snapshot.PassIndex] = snapshot.DirectionPositive;
            _streamingWorkflowPreviewPassMotorSteps[snapshot.PassIndex] = snapshot.MotorSteps;
            _streamingWorkflowPreviewPassMotorIntervals[snapshot.PassIndex] = snapshot.MotorIntervalNanoseconds;
            _pendingStreamingPreviewRows = _streamingPreviewTargetRows;

            if (_isStreamingPreviewQueued)
                return;

            var now = Environment.TickCount64;
            if (_lastStreamingPreviewEnqueueTick != 0 && now - _lastStreamingPreviewEnqueueTick < ScanPreviewStreamThrottleMs)
                delayMs = (int)Math.Max(1, ScanPreviewStreamThrottleMs - (now - _lastStreamingPreviewEnqueueTick));

            _isStreamingPreviewQueued = true;
            if (delayMs == 0)
                _lastStreamingPreviewEnqueueTick = now;

            shouldQueue = true;
        }

        if (shouldQueue)
        {
            if (delayMs > 0)
                _ = QueueDelayedStreamingPreviewFrameAsync(previewSessionVersion, delayMs);
            else
                _dispatcher.TryEnqueue(() => ApplyStreamingPreviewFrame(previewSessionVersion));
        }
    }

    private void QueueStreamingPreviewFrame(int previewSessionVersion, byte[] imageBytes, int completedRows)
    {
        if (completedRows <= 0)
            return;

        var shouldQueue = false;
        var delayMs = 0;
        lock (_streamingPreviewLock)
        {
            if (!_isStreamingPreviewActive
                || _streamingPreviewSessionVersion != previewSessionVersion
                || completedRows > _streamingPreviewTargetRows
                || IsPreviewForcedOffForRows(_streamingPreviewTargetRows)
                || !IsPreviewEnabled)
            {
                return;
            }

            _lineBuffer = imageBytes;
            _pendingStreamingPreviewRows = Math.Max(_pendingStreamingPreviewRows, completedRows);
            if (_isStreamingPreviewQueued)
                return;

            var now = Environment.TickCount64;
            if (_lastStreamingPreviewEnqueueTick != 0 && now - _lastStreamingPreviewEnqueueTick < ScanPreviewStreamThrottleMs)
                delayMs = (int)Math.Max(1, ScanPreviewStreamThrottleMs - (now - _lastStreamingPreviewEnqueueTick));

            _isStreamingPreviewQueued = true;
            if (delayMs == 0)
                _lastStreamingPreviewEnqueueTick = now;

            shouldQueue = true;
        }

        if (shouldQueue)
        {
            if (delayMs > 0)
                _ = QueueDelayedStreamingPreviewFrameAsync(previewSessionVersion, delayMs);
            else
                _dispatcher.TryEnqueue(() => ApplyStreamingPreviewFrame(previewSessionVersion));
        }
    }

    private async Task QueueDelayedStreamingPreviewFrameAsync(int previewSessionVersion, int delayMs)
    {
        await Task.Delay(delayMs);
        lock (_streamingPreviewLock)
        {
            if (!_isStreamingPreviewActive || _streamingPreviewSessionVersion != previewSessionVersion)
                return;

            _lastStreamingPreviewEnqueueTick = Environment.TickCount64;
        }

        _dispatcher.TryEnqueue(() => ApplyStreamingPreviewFrame(previewSessionVersion));
    }

    private void ApplyStreamingPreviewFrame(int previewSessionVersion)
    {
        int completedRows;
        int targetRows;
        ScanWorkflowRequest? workflowRequest;
        byte[][]? workflowPassBuffers;
        int[]? workflowCompletedRows;
        bool[]? workflowPassDirections;
        uint[]? workflowPassMotorSteps;
        uint[]? workflowPassMotorIntervals;
        lock (_streamingPreviewLock)
        {
            if (!_isStreamingPreviewActive || _streamingPreviewSessionVersion != previewSessionVersion)
                return;

            completedRows = _pendingStreamingPreviewRows;
            targetRows = _streamingPreviewTargetRows;
            _isStreamingPreviewQueued = false;
            workflowRequest = _streamingWorkflowPreviewRequest;
            workflowPassBuffers = _streamingWorkflowPreviewPassBuffers?.Select(buffer => (byte[])buffer.Clone()).ToArray();
            workflowCompletedRows = _streamingWorkflowPreviewPassCompletedRows is null ? null : (int[])_streamingWorkflowPreviewPassCompletedRows.Clone();
            workflowPassDirections = _streamingWorkflowPreviewPassDirections is null ? null : (bool[])_streamingWorkflowPreviewPassDirections.Clone();
            workflowPassMotorSteps = _streamingWorkflowPreviewPassMotorSteps is null ? null : (uint[])_streamingWorkflowPreviewPassMotorSteps.Clone();
            workflowPassMotorIntervals = _streamingWorkflowPreviewPassMotorIntervals is null ? null : (uint[])_streamingWorkflowPreviewPassMotorIntervals.Clone();
        }

        if (completedRows <= 0 || completedRows > targetRows || !IsPreviewEnabled || IsPreviewForcedOffForRows(targetRows))
            return;

        ScanChannelAssignment? workflowAssignment = null;
        ScanWorkflowResult? workflowPreviewResult = null;
        Dictionary<int, int>? workflowCompletedRowsByPassIndex = null;
        if (workflowRequest is not null
            && workflowPassBuffers is not null
            && workflowCompletedRows is not null
            && workflowPassDirections is not null
            && workflowPassMotorSteps is not null
            && workflowPassMotorIntervals is not null)
        {
            workflowAssignment = BuildDeviceIndexedChannelAssignment(workflowRequest.PassChannelRoles);
            workflowPreviewResult = BuildStreamingWorkflowPreviewResult(
                workflowRequest,
                workflowPassBuffers,
                workflowCompletedRows,
                workflowPassDirections,
                workflowPassMotorSteps,
                workflowPassMotorIntervals);
            workflowCompletedRowsByPassIndex = workflowCompletedRows
                .Select((rows, index) => new KeyValuePair<int, int>(index, rows))
                .ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        lock (_streamingPreviewLock)
        {
            if (!_isStreamingPreviewActive || _streamingPreviewSessionVersion != previewSessionVersion)
                return;

            if (workflowPreviewResult is not null)
            {
                _streamingWorkflowPreviewAssignment = workflowAssignment;
                _streamingWorkflowPreviewResult = workflowPreviewResult;
                _streamingWorkflowPreviewCompletedRowsByPassIndex = workflowCompletedRowsByPassIndex;
                _previewRows = targetRows;
            }
            else
            {
                _streamingWorkflowPreviewAssignment = null;
                _previewRows = completedRows;
            }

            _lastWorkflowChannelAssignment = null;
            _lastWorkflowResult = null;
        }

        RenderPreview(workflowPreviewResult is null ? completedRows : targetRows);
    }

    private async Task InitializeTransferSettingsAsync()
    {
        await _transferSettings.InitializeAsync();
        await EnqueueOnUiAsync(() =>
        {
            _isMultiBufferedBulkInEnabled = _transferSettings.Settings.ReadMode == ScanBulkInReadMode.MultiBuffered;
            StartScanCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(IsStartActionAvailable));
        });
    }

    private Task EnqueueOnUiAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }))
        {
            completion.SetException(new InvalidOperationException("ScanDebug_Runtime_DispatchFailed".GetLocalized()));
        }

        return completion.Task;
    }

    private void ApplySnapshotToInputs(ScanParameterSnapshot snapshot)
    {
        var wasSynchronizing = _isSynchronizingTimingInputs;
        _isSynchronizingTimingInputs = true;
        try
        {
            ExposureTicks = snapshot.ExposureTicks.ToString(CultureInfo.InvariantCulture);
            ExposureMicroseconds = ScanTimingMath.ExposureTicksToMicroseconds(snapshot.ExposureTicks, snapshot.SysClockKhz).ToString("0.###", CultureInfo.InvariantCulture);
            Adc1Offset = _parameters.FormatOffsetForInput(snapshot.Adc1Offset);
            Adc1Gain = snapshot.Adc1Gain.ToString(CultureInfo.InvariantCulture);
            Adc2Offset = _parameters.FormatOffsetForInput(snapshot.Adc2Offset);
            Adc2Gain = snapshot.Adc2Gain.ToString(CultureInfo.InvariantCulture);
            SysClockKhz = snapshot.SysClockKhz.ToString(CultureInfo.InvariantCulture);
            if (snapshot.SysClockKhz >= ScanDebugConstants.MinSysClockKhz && snapshot.SysClockKhz <= ScanDebugConstants.MaxSysClockKhz)
                SysClockMhz = (snapshot.SysClockKhz / 1000m).ToString("0.###", CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSynchronizingTimingInputs = wasSynchronizing;
        }

        if (snapshot.SysClockKhz >= ScanDebugConstants.MinSysClockKhz && snapshot.SysClockKhz <= ScanDebugConstants.MaxSysClockKhz)
            UpdateTimingProjectionFromInputs(false);
        SetDeviceTimingState(new ScanDeviceTimingState(ScanDeviceClockStateKind.DeviceKnown));
    }

    private void ApplyCalibrationSnapshotProjection(ScanParameterSnapshot snapshot)
    {
        var wasSynchronizing = _isSynchronizingFilmProfileWorkspace;
        _isSynchronizingFilmProfileWorkspace = true;
        try
        {
            var exposureMicroseconds = ScanTimingMath.ExposureTicksToMicroseconds(snapshot.ExposureTicks, snapshot.SysClockKhz).ToString("0.###", CultureInfo.InvariantCulture);
            var hasLiveClock = TryParseSysClockMhzText(SysClockMhz, out _);
            _isSynchronizingTimingInputs = true;
            try
            {
                ExposureMicroseconds = exposureMicroseconds;
                Adc1Offset = _parameters.FormatOffsetForInput(snapshot.Adc1Offset);
                Adc1Gain = snapshot.Adc1Gain.ToString(CultureInfo.InvariantCulture);
                Adc2Offset = _parameters.FormatOffsetForInput(snapshot.Adc2Offset);
                Adc2Gain = snapshot.Adc2Gain.ToString(CultureInfo.InvariantCulture);
                if (!hasLiveClock && snapshot.SysClockKhz >= ScanDebugConstants.MinSysClockKhz && snapshot.SysClockKhz <= ScanDebugConstants.MaxSysClockKhz)
                    SysClockMhz = (snapshot.SysClockKhz / 1000m).ToString("0.###", CultureInfo.InvariantCulture);

                if (TryParseTimingProjectionInputs(ExposureMicroseconds, SysClockMhz, out var exposureTicks, out var sysClockKhz))
                {
                    ExposureTicks = exposureTicks.ToString(CultureInfo.InvariantCulture);
                    SysClockKhz = sysClockKhz.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    ExposureTicks = string.Empty;
                    if (!hasLiveClock)
                        SysClockKhz = string.Empty;
                }
            }
            finally
            {
                _isSynchronizingTimingInputs = false;
            }
        }
        finally
        {
            _isSynchronizingFilmProfileWorkspace = wasSynchronizing;
            RefreshFilmProfileWorkspaceProjection();
        }
    }

    private void ApplyCalibrationProfileProjection(ScanChannelCalibrationProfile profile)
    {
        var wasSynchronizing = _isSynchronizingFilmProfileWorkspace;
        _isSynchronizingFilmProfileWorkspace = true;
        try
        {
            ApplyCalibrationSnapshotProjection(profile.Parameters);
            _roiSettings = profile.RoiSettings;
            RefreshRoiStatus();
            RefreshColumnSampleStatus();
        }
        finally
        {
            _isSynchronizingFilmProfileWorkspace = wasSynchronizing;
            RefreshFilmProfileWorkspaceProjection();
        }
    }

    private void ShowCalibrationFrame(byte[] imageBytes, int rows, string phase)
    {
        ApplyScanFrame(imageBytes, rows, "ScanDebug_Runtime_StatusPhasePreviewUpdated".GetLocalizedFormat(phase));
    }

    public bool TryGetPreviewSample16(int x, int y, out ushort sample)
    {
        sample = 0;

        if (!_hasValidScanBuffer || PreviewFrame is null || _lineBuffer.Length == 0 || !IsPreviewEnabled || IsWaterfallEnabled)
            return false;

        if (x < 0 || y < 0 || x >= PreviewFrame.Width || y >= PreviewFrame.Height)
            return false;

        return _imageDecoder.TryGetSample16(_lineBuffer, _previewRows, x, y, out sample);
    }

    public IReadOnlyList<(string Key, string Label, ScanColumnRange Range, bool IsSelected)> GetPreviewRoiOverlays(int imageWidth)
    {
        var clamped = _roiSettings.Clamp(imageWidth);
        return new[]
        {
            (Key: RoiSelectionBwActive, Label: GetRoiSelectionDisplayName(RoiSelectionBwActive), Range: clamped.EffectiveRange, IsSelected: SelectedRoiSelection == RoiSelectionBwActive),
            (Key: RoiSelectionBwShield, Label: GetRoiSelectionDisplayName(RoiSelectionBwShield), Range: clamped.ShieldRange, IsSelected: SelectedRoiSelection == RoiSelectionBwShield),
            (Key: RoiSelectionFocusOverall, Label: GetRoiSelectionDisplayName(RoiSelectionFocusOverall), Range: clamped.FocusOverallRange, IsSelected: SelectedRoiSelection == RoiSelectionFocusOverall),
            (Key: RoiSelectionFocusLeft, Label: GetRoiSelectionDisplayName(RoiSelectionFocusLeft), Range: clamped.FocusLeftRange, IsSelected: SelectedRoiSelection == RoiSelectionFocusLeft),
            (Key: RoiSelectionFocusRight, Label: GetRoiSelectionDisplayName(RoiSelectionFocusRight), Range: clamped.FocusRightRange, IsSelected: SelectedRoiSelection == RoiSelectionFocusRight)
        }
        .Where(overlay => IsRoiOverlayVisible(overlay.Key))
        .ToArray();
    }

    public bool TryGetSelectedRoiRange(int imageWidth, out ScanColumnRange range)
    {
        _ = imageWidth;
        switch (SelectedRoiSelection)
        {
            case RoiSelectionBwActive:
                range = _roiSettings.EffectiveRange;
                return true;
            case RoiSelectionBwShield:
                range = _roiSettings.ShieldRange;
                return true;
            case RoiSelectionFocusOverall:
                range = _roiSettings.FocusOverallRange;
                return true;
            case RoiSelectionFocusLeft:
                range = _roiSettings.FocusLeftRange;
                return true;
            case RoiSelectionFocusRight:
                range = _roiSettings.FocusRightRange;
                return true;
            default:
                range = new ScanColumnRange(0, -1);
                return false;
        }
    }

    public void UpdateSelectedRoiRange(int start, int endInclusive, int imageWidth)
    {
        var requestedRange = new ScanColumnRange(start, endInclusive);
        if (!TryBuildRoiEditCandidate(requestedRange, imageWidth, out var candidate, out var issue))
        {
#if PRISM_VISUAL_QA
            RecordVisualQaRoiEditAttempt("updateSelected", requestedRange, imageWidth, candidate, issue);
#endif
            RoiInputStatusText = FormatRoiEditIssue(issue);
            return;
        }

#if PRISM_VISUAL_QA
        RecordVisualQaRoiEditAttempt("updateSelected", requestedRange, imageWidth, candidate, issue);
#endif
        _roiSettings = candidate;
        RefreshRoiStatus();
        SynchronizeFilmProfileDraftFromInputs();
    }

    public void ShiftSelectedRoiRange(int deltaColumns, int imageWidth)
    {
        if (!TryGetSelectedRoiRange(imageWidth, out var range))
            return;

        var width = range.Width;
        if (width <= 0)
            return;

        var start = Math.Clamp(range.Start + deltaColumns, 0, Math.Max(0, imageWidth - width));
        UpdateSelectedRoiRange(start, start + width - 1, imageWidth);
    }

    public bool TryGetColumnSampleRange(int imageWidth, out ScanColumnRange range)
    {
        if (imageWidth <= 0)
        {
            range = new ScanColumnRange(0, 0);
            return false;
        }

        if (ScanImageReferenceColumnRange.TryCreate(_columnSampleRange, imageWidth).Value is not { } referenceRange)
        {
            range = new ScanColumnRange(0, -1);
            return false;
        }

        range = referenceRange.ColumnRange;
        return true;
    }

    public void UpdateColumnSampleRange(int start, int endInclusive, int imageWidth)
    {
        _columnSampleRange = new ScanColumnRange(start, endInclusive);
#if PRISM_VISUAL_QA
        RecordVisualQaColumnSampleEditAttempt("updateColumnSample", _columnSampleRange, imageWidth);
#endif
        InvalidateColumnSampleCache();
        RefreshColumnSampleStatus();
    }

    private void ApplyColumnSampleInputs()
    {
        if (!int.TryParse(ColumnSampleStartInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(ColumnSampleEndInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var endInclusive))
        {
            InvalidateColumnSampleCache();
            ColumnSampleStatusText = "ScanDebug_Runtime_ColumnSampleInvalidInput".GetLocalized();
            ColumnSampleOverlayVersion++;
            NotifyColumnSampleAvailabilityChanged();
            NotifyRoiValidationIssuesChanged();
            return;
        }

        UpdateColumnSampleRange(start, endInclusive, GetRoiEditingWidth());
    }

    public void ShiftColumnSampleRange(int deltaColumns, int imageWidth)
    {
        if (!TryGetColumnSampleRange(imageWidth, out var range))
            return;

        var width = range.Width;
        if (width <= 0)
            return;

        var start = Math.Clamp(range.Start + deltaColumns, 0, Math.Max(0, imageWidth - width));
        UpdateColumnSampleRange(start, start + width - 1, imageWidth);
    }

    private int GetRoiEditingWidth()
        => PreviewFrame?.Width > 0 ? PreviewFrame.Width : ScanDebugConstants.DecodedPixelsPerLine;

    private bool IsRoiOverlayVisible(string roiKey)
        => roiKey switch
        {
            RoiSelectionBwActive => IsBwActiveRoiOverlayVisible,
            RoiSelectionBwShield => IsBwShieldRoiOverlayVisible,
            RoiSelectionFocusOverall => IsFocusOverallRoiOverlayVisible,
            RoiSelectionFocusLeft => IsFocusLeftRoiOverlayVisible,
            RoiSelectionFocusRight => IsFocusRightRoiOverlayVisible,
            _ => true
        };

    private void EnsureRoiEditModeAvailability()
    {
        if (!CanEditRoiSelection && IsRoiEditModeEnabled)
            IsRoiEditModeEnabled = false;
    }

    private void EnsureColumnSampleEditModeAvailability()
    {
        if (!CanEditColumnSampleSelection && IsColumnSampleEditModeEnabled)
            IsColumnSampleEditModeEnabled = false;
    }

    private void RefreshRoiOverlayVisibility()
        => RoiOverlayVersion++;

    private void RefreshRoiStatus()
    {
        EnsureRoiEditModeAvailability();
        EnsureColumnSampleEditModeAvailability();
        RefreshRoiInputTexts();
        RoiStatusText = BuildRoiStatusText();
        RoiOverlayVersion++;
        NotifyRoiValidationIssuesChanged();
    }

    private void RefreshColumnSampleStatus()
    {
        if (AreColumnSampleInputsParseable())
            RefreshColumnSampleInputTexts();
        NotifyRoiValidationIssuesChanged();

        if (PreviewFrame is null || PreviewFrame.Width <= 0 || !_hasValidScanBuffer || _previewRows <= 0)
        {
            InvalidateColumnSampleCache();
            ColumnSampleStatusText = "ScanDebug_Runtime_ColumnSampleUnavailable".GetLocalized();
            ColumnSampleOverlayVersion++;
            NotifyColumnSampleAvailabilityChanged();
            return;
        }

        if (!IsColumnSampleSourceCurrentOwner())
        {
            InvalidateColumnSampleCache();
            ColumnSampleStatusText = "ScanDebug_Runtime_ColumnSampleUnavailable".GetLocalized();
            ColumnSampleOverlayVersion++;
            NotifyColumnSampleAvailabilityChanged();
            return;
        }

        if (ScanImageReferenceColumnRange.TryCreate(_columnSampleRange, PreviewFrame.Width).Value is not { } referenceRange)
        {
            InvalidateColumnSampleCache();
            ColumnSampleStatusText = "ScanDebug_Runtime_ColumnSampleUnavailable".GetLocalized();
            ColumnSampleOverlayVersion++;
            NotifyColumnSampleAvailabilityChanged();
            return;
        }

        if (!TryGetCurrentColumnSampleMean(out var mean, out var error))
        {
            InvalidateColumnSampleCache();
            ColumnSampleStatusText = "ScanDebug_Runtime_ColumnSampleUnavailableWithError".GetLocalizedFormat(_columnSampleRange.Start, _columnSampleRange.EndInclusive, error);
            ColumnSampleOverlayVersion++;
            NotifyColumnSampleAvailabilityChanged();
            return;
        }

        _columnSampleMean = mean;
        _columnSampleFrameVersion = PreviewFrame.Version;
        _columnSampleMeanRange = referenceRange.ColumnRange;
        _columnSampleMeanChannelRole = SelectedCalibrationChannel;
        _columnSampleMeanOwnerVersion = _columnSampleOwnerVersion;
        var savedLevels = _calibrationProfiles.TryGetProfile(SelectedCalibrationChannel, out var profile)
            ? "ScanDebug_Runtime_ColumnSampleSavedLevels".GetLocalizedFormat(FormatOptionalLevel(profile.BlackLevel), FormatOptionalLevel(profile.WhiteLevel))
            : string.Empty;
        ColumnSampleStatusText = "ScanDebug_Runtime_ColumnSampleSummary".GetLocalizedFormat(_columnSampleRange.Start, _columnSampleRange.EndInclusive, _columnSampleRange.Width, mean, savedLevels);
        ColumnSampleOverlayVersion++;
        NotifyColumnSampleAvailabilityChanged();
    }

    private void RefreshColumnSampleInputTexts()
    {
        _isUpdatingColumnSampleInputs = true;
        try
        {
            ColumnSampleStartInput = _columnSampleRange.Start.ToString(CultureInfo.InvariantCulture);
            ColumnSampleEndInput = _columnSampleRange.EndInclusive.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _isUpdatingColumnSampleInputs = false;
        }
    }

    private bool AreColumnSampleInputsParseable()
        => int.TryParse(ColumnSampleStartInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && int.TryParse(ColumnSampleEndInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private static string FormatOptionalLevel(ushort? level)
        => level?.ToString(CultureInfo.InvariantCulture) ?? "-";

    private void RefreshPreviewIfPossible()
    {
        if (!_hasValidScanBuffer || _previewRows <= 0 || !IsPreviewEnabled || IsPreviewForcedOffForRows(_previewRows))
            return;

        RenderPreview(_previewRows);
    }

    private bool TryGetCurrentColumnSampleMean(out ushort mean, out string error)
    {
        if (_lastWorkflowResult is not null)
        {
            var assignment = _lastWorkflowChannelAssignment ?? BuildCapturedWorkflowChannelAssignment(_lastWorkflowResult);
            return _channelImages.TryComputeAlignedChannelColumnAverage(_lastWorkflowResult, assignment, ScanChannelAlignmentMode.Ecc, SelectedCalibrationChannel, _columnSampleRange, out mean, out error);
        }

        return TryComputeMonochromeColumnSampleMean(_columnSampleRange, out mean, out error);
    }

    private bool TryComputeMonochromeColumnSampleMean(ScanColumnRange range, out ushort mean, out string error)
    {
        mean = 0;
        error = string.Empty;

        var width = PreviewFrame?.Width ?? _imageDecoder.GetDecodedPixelsPerLine();
        if (width <= 0 || _previewRows <= 0)
        {
            error = "ScanDebug_Runtime_ColumnSampleNoPreviewSamples".GetLocalized();
            return false;
        }

        if (ScanImageReferenceColumnRange.TryCreate(range, width).Value is not { } referenceRange)
        {
            error = "ScanDebug_Runtime_ColumnSampleNoPreviewSamples".GetLocalized();
            return false;
        }

        var validatedRange = referenceRange.ColumnRange;
        ulong sum = 0;
        long count = 0;
        for (var y = 0; y < _previewRows; y++)
        {
            for (var x = validatedRange.Start; x <= validatedRange.EndInclusive; x++)
            {
                if (!_imageDecoder.TryGetSample16(_lineBuffer, _previewRows, x, y, out var sample))
                    continue;

                sum += sample;
                count++;
            }
        }

        if (count == 0)
        {
            error = "ScanDebug_Runtime_ColumnSampleNoValidPixels".GetLocalized();
            return false;
        }

        mean = (ushort)Math.Clamp((int)Math.Round(sum / (double)count), 0, ushort.MaxValue);
        return true;
    }

    private void RefreshRoiInputTexts()
    {
        var width = GetRoiEditingWidth();
        if (!TryGetSelectedRoiRange(width, out var range))
            return;

        _isUpdatingRoiInputs = true;
        try
        {
            RoiStartInput = range.Start.ToString();
            RoiEndInput = range.EndInclusive.ToString();
        }
        finally
        {
            _isUpdatingRoiInputs = false;
        }

        RoiInputStatusText = "ScanDebug_Runtime_RoiNumericMirror".GetLocalizedFormat(GetRoiSelectionDisplayName(SelectedRoiSelection), range.Start, range.EndInclusive);
    }

    public bool TryNavigateToRoiIssue(ScanRoiValidationIssue issue)
    {
        if (issue is { Owner: ScanRoiOperationOwner.ImageReferenceSampling, FieldPath: "ColumnRange" })
        {
            RoiIssueNavigationRequested?.Invoke(new ScanRoiIssueNavigationRequest(issue.Owner, ScanRoiEditorTarget.ImageReferenceColumn, issue.FieldPath));
            return true;
        }

        if (!TryResolveRoiEditorTarget(issue, out var target, out var selection))
            return false;

        SelectedRoiSelection = selection;
        RoiIssueNavigationRequested?.Invoke(new ScanRoiIssueNavigationRequest(issue.Owner, target, issue.FieldPath));
        return true;
    }

    [RelayCommand]
    private void NavigateToRoiIssue(ScanRoiValidationIssue issue)
        => TryNavigateToRoiIssue(issue);

    public async Task<bool> TryRequestCurrentFilmProfileIssueNavigationAsync(ScanFilmProfileValidationIssueDisplay? display)
    {
        if (!TryResolveCurrentFilmProfileNavigation(display, out var request))
            return false;

        var context = await EnsureCurrentFilmProfileNavigationRoleReadyAsync(request.ChannelRole);
        if (context is null)
            return false;

        var currentIssue = CurrentFilmProfileValidationIssues.FirstOrDefault(issue => AreStructurallyEquivalentIssues(issue, display!.Issue));
        if (currentIssue is null
            || !TryResolveCurrentFilmProfileNavigation(currentIssue, out var refreshedRequest)
            || refreshedRequest != request
            || !IsCurrentFilmProfileNavigationContext(context, refreshedRequest.ChannelRole))
        {
            return false;
        }

        CurrentFilmProfileIssueNavigationRequested?.Invoke(refreshedRequest);
        return true;
    }

    [RelayCommand]
    private async Task NavigateToCurrentFilmProfileValidationIssue(ScanFilmProfileValidationIssueDisplay display)
        => await TryRequestCurrentFilmProfileIssueNavigationAsync(display);

    private bool TryResolveCurrentFilmProfileNavigation(
        ScanFilmProfileValidationIssueDisplay? display,
        out ScanFilmProfileIssueNavigationRequest request)
    {
        request = default!;
        if (display is null
            || display.Source != ScanFilmProfileIssueSource.CurrentDraft)
            return false;

        return TryResolveCurrentFilmProfileNavigation(display.Issue, out request);
    }

    private bool TryResolveCurrentFilmProfileNavigation(
        ScanFilmProfileValidationIssue issue,
        out ScanFilmProfileIssueNavigationRequest request)
    {
        request = default!;
        if (HasPendingFilmProfileImportResult
            || !ContainsStructurallyEquivalentIssue(CurrentFilmProfileValidationIssues, issue))
        {
            return false;
        }

        var resolved = ScanFilmProfileIssueNavigation.Resolve(issue, ScanFilmProfileIssueSource.CurrentDraft, CalibrationChannelOptions);
        if (resolved is null)
            return false;

        request = resolved;
        return true;
    }

    private async Task<CurrentFilmProfileIssueNavigationContext?> EnsureCurrentFilmProfileNavigationRoleReadyAsync(string? channelRole)
    {
        if (channelRole is null)
            return new CurrentFilmProfileIssueNavigationContext(_filmProfileWorkspace.Snapshot.CurrentDraft, null);

        if (!IsCurrentCalibrationChannel(channelRole))
            SelectedCalibrationChannel = channelRole;

        var load = _selectedCalibrationChannelLoad;
        var currentDraft = _filmProfileWorkspace.Snapshot.CurrentDraft;
        if (load is null || !string.Equals(load.Role, channelRole, StringComparison.OrdinalIgnoreCase))
        {
            return IsCurrentCalibrationChannel(channelRole)
                ? new CurrentFilmProfileIssueNavigationContext(currentDraft, null)
                : null;
        }

        return await load.Completion
            ? new CurrentFilmProfileIssueNavigationContext(currentDraft, load)
            : null;
    }

    private bool IsCurrentFilmProfileNavigationContext(
        CurrentFilmProfileIssueNavigationContext context,
        string? channelRole)
    {
        if (!ReferenceEquals(context.CurrentDraft, _filmProfileWorkspace.Snapshot.CurrentDraft))
            return false;

        if (channelRole is null)
            return true;

        if (!IsCurrentCalibrationChannel(channelRole))
            return false;

        if (context.SelectionLoad is not { } load)
            return true;

        return ReferenceEquals(load, _selectedCalibrationChannelLoad)
            && load.LoadVersion == _profileLoadVersion
            && !IsCalibrationProjectionStale(load.ProjectionVersion);
    }

    private static bool ContainsStructurallyEquivalentIssue(
        IReadOnlyList<ScanFilmProfileValidationIssue> issues,
        ScanFilmProfileValidationIssue expected)
        => issues.Any(issue => AreStructurallyEquivalentIssues(issue, expected));

    private static bool AreStructurallyEquivalentIssues(
        ScanFilmProfileValidationIssue left,
        ScanFilmProfileValidationIssue right)
        => left.Code == right.Code
            && string.Equals(left.FieldPath, right.FieldPath, StringComparison.Ordinal)
            && left.Severity == right.Severity
            && string.Equals(left.MessageKey, right.MessageKey, StringComparison.Ordinal)
            && left.MessageArguments.SequenceEqual(right.MessageArguments, StringComparer.Ordinal);

    private static bool TryResolveRoiEditorTarget(ScanRoiValidationIssue issue, out ScanRoiEditorTarget target, out string selection)
    {
        (target, selection) = (issue.Owner, issue.FieldPath) switch
        {
            (ScanRoiOperationOwner.AdcCalibration, "EffectiveRange") => (ScanRoiEditorTarget.AdcEffective, RoiSelectionBwActive),
            (ScanRoiOperationOwner.AdcCalibration, "ShieldRange") => (ScanRoiEditorTarget.AdcShield, RoiSelectionBwShield),
            (ScanRoiOperationOwner.AutoFocus, "FocusLeftRange") => (ScanRoiEditorTarget.FocusLeft, RoiSelectionFocusLeft),
            (ScanRoiOperationOwner.AutoFocus, "FocusRightRange") => (ScanRoiEditorTarget.FocusRight, RoiSelectionFocusRight),
            (ScanRoiOperationOwner.AutoFocus, "FocusOverallRange") => (ScanRoiEditorTarget.FocusOverall, RoiSelectionFocusOverall),
            _ => default
        };
        return !string.IsNullOrEmpty(selection);
    }

    private bool TryBuildRoiEditCandidate(out ScanCalibrationRoiSettings candidate, out ScanRoiValidationIssue? issue)
    {
        candidate = _roiSettings;
        issue = null;
        if (!int.TryParse(RoiStartInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(RoiEndInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var endInclusive))
        {
            issue = new ScanRoiValidationIssue(GetSelectedRoiOwner(), ScanRoiValidationCode.Missing, GetSelectedRoiFieldPath());
            return false;
        }

        return TryBuildRoiEditCandidate(new ScanColumnRange(start, endInclusive), GetRoiEditingWidth(), out candidate, out issue);
    }

    private bool TryBuildRoiEditCandidate(ScanColumnRange range, int imageWidth, out ScanCalibrationRoiSettings candidate, out ScanRoiValidationIssue? issue)
    {
        candidate = SelectedRoiSelection switch
        {
            RoiSelectionBwActive => _roiSettings with { EffectiveRange = range },
            RoiSelectionBwShield => _roiSettings with { ShieldRange = range },
            RoiSelectionFocusOverall => _roiSettings with { FocusOverallRange = range },
            RoiSelectionFocusLeft => _roiSettings with { FocusLeftRange = range },
            RoiSelectionFocusRight => _roiSettings with { FocusRightRange = range },
            _ => _roiSettings
        };

        if (SelectedRoiSelection is not (RoiSelectionBwActive or RoiSelectionBwShield or RoiSelectionFocusOverall or RoiSelectionFocusLeft or RoiSelectionFocusRight))
        {
            issue = new ScanRoiValidationIssue(ScanRoiOperationOwner.AdcCalibration, ScanRoiValidationCode.Missing, "RoiSelection");
            return false;
        }

        return TryValidateRoiOperation(candidate, imageWidth, GetSelectedRoiOwner(), out issue);
    }

    private IReadOnlyList<ScanRoiValidationIssue> BuildAdcRoiValidationIssues()
    {
        if (GetSelectedRoiOwner() == ScanRoiOperationOwner.AdcCalibration)
        {
            if (!TryBuildRoiEditCandidate(out var candidate, out var issue))
                return issue is null ? [] : [issue];

            return ScanAdcCalibrationRoi.TryCreate(candidate, GetRoiEditingWidth()).Issues;
        }

        var settings = _roiSettings;
        return ScanAdcCalibrationRoi.TryCreate(settings, GetRoiEditingWidth()).Issues;
    }

    private IReadOnlyList<ScanRoiValidationIssue> BuildFocusRoiValidationIssues()
    {
        if (GetSelectedRoiOwner() == ScanRoiOperationOwner.AutoFocus)
        {
            if (!TryBuildRoiEditCandidate(out var candidate, out var issue))
                return issue is null ? [] : [issue];

            return ScanFocusRoi.TryCreate(candidate, GetRoiEditingWidth()).Issues;
        }

        var settings = _roiSettings;
        return ScanFocusRoi.TryCreate(settings, GetRoiEditingWidth()).Issues;
    }

    private IReadOnlyList<ScanRoiValidationIssue> BuildImageReferenceRoiValidationIssues()
    {
        if (!AreColumnSampleInputsParseable())
            return [new ScanRoiValidationIssue(ScanRoiOperationOwner.ImageReferenceSampling, ScanRoiValidationCode.Missing, "ColumnRange")];

        return ScanImageReferenceColumnRange.TryCreate(_columnSampleRange, GetRoiEditingWidth()).Issues;
    }

    private bool IsRoiEditCandidateValid(ScanRoiOperationOwner owner)
        => GetSelectedRoiOwner() == owner
            ? TryBuildRoiEditCandidate(out _, out _)
            : TryValidateRoiOperation(_roiSettings, GetRoiEditingWidth(), owner, out _);

    private static bool TryValidateRoiOperation(ScanCalibrationRoiSettings settings, int imageWidth, ScanRoiOperationOwner owner, out ScanRoiValidationIssue? issue)
    {
        IReadOnlyList<ScanRoiValidationIssue> issues = owner switch
        {
            ScanRoiOperationOwner.AdcCalibration => ScanAdcCalibrationRoi.TryCreate(settings, imageWidth).Issues,
            ScanRoiOperationOwner.AutoFocus => ScanFocusRoi.TryCreate(settings, imageWidth).Issues,
            _ => []
        };
        issue = issues.FirstOrDefault();
        return issue is null;
    }

#if PRISM_VISUAL_QA
    internal object GetVisualQaRoiDiagnostics(int imageWidth)
    {
        var adcResult = ScanAdcCalibrationRoi.TryCreate(_roiSettings, imageWidth);
        var focusResult = ScanFocusRoi.TryCreate(_roiSettings, imageWidth);
        var referenceResult = ScanImageReferenceColumnRange.TryCreate(_columnSampleRange, imageWidth);
        return new
        {
            imageWidth,
            settings = ToVisualQaRoiSettings(_roiSettings),
            columnSampleRange = ToVisualQaRange(_columnSampleRange),
            validation = new
            {
                adcIssues = adcResult.Issues.Select(ToVisualQaIssue).ToArray(),
                focusIssues = focusResult.Issues.Select(ToVisualQaIssue).ToArray(),
                imageReferenceIssues = referenceResult.Issues.Select(ToVisualQaIssue).ToArray()
            },
            lastRoiEditAttempt = _visualQaLastRoiEditAttempt,
            lastColumnSampleEditAttempt = _visualQaLastColumnSampleEditAttempt
        };
    }

    private void RecordVisualQaRoiEditAttempt(string operation, ScanColumnRange requestedRange, int imageWidth, ScanCalibrationRoiSettings candidate, ScanRoiValidationIssue? issue)
    {
        _visualQaLastRoiEditAttempt = new
        {
            operation,
            selectedRoiSelection = SelectedRoiSelection,
            owner = GetSelectedRoiOwner().ToString(),
            requestedRange = ToVisualQaRange(requestedRange),
            imageWidth,
            candidate = ToVisualQaRoiSettings(candidate),
            accepted = issue is null,
            issue = issue is null ? null : ToVisualQaIssue(issue)
        };
    }

    private void RecordVisualQaColumnSampleEditAttempt(string operation, ScanColumnRange requestedRange, int imageWidth)
    {
        var result = ScanImageReferenceColumnRange.TryCreate(requestedRange, imageWidth);
        _visualQaLastColumnSampleEditAttempt = new
        {
            operation,
            requestedRange = ToVisualQaRange(requestedRange),
            imageWidth,
            accepted = result.IsValid,
            issues = result.Issues.Select(ToVisualQaIssue).ToArray()
        };
    }

    private static object ToVisualQaRoiSettings(ScanCalibrationRoiSettings settings)
        => new
        {
            effectiveRange = ToVisualQaRange(settings.EffectiveRange),
            shieldRange = ToVisualQaRange(settings.ShieldRange),
            focusLeftRange = ToVisualQaRange(settings.FocusLeftRange),
            focusRightRange = ToVisualQaRange(settings.FocusRightRange),
            focusOverallRange = ToVisualQaRange(settings.FocusOverallRange)
        };

    private static object ToVisualQaRange(ScanColumnRange range)
        => new { start = range.Start, endInclusive = range.EndInclusive, width = range.Width };

    private static object ToVisualQaIssue(ScanRoiValidationIssue issue)
        => new { owner = issue.Owner.ToString(), code = issue.Code.ToString(), issue.FieldPath };
#endif

    private ScanRoiOperationOwner GetSelectedRoiOwner()
        => SelectedRoiSelection is RoiSelectionBwActive or RoiSelectionBwShield
            ? ScanRoiOperationOwner.AdcCalibration
            : ScanRoiOperationOwner.AutoFocus;

    private string GetSelectedRoiFieldPath()
        => SelectedRoiSelection switch
        {
            RoiSelectionBwActive => "EffectiveRange",
            RoiSelectionBwShield => "ShieldRange",
            RoiSelectionFocusLeft => "FocusLeftRange",
            RoiSelectionFocusRight => "FocusRightRange",
            RoiSelectionFocusOverall => "FocusOverallRange",
            _ => "RoiSelection"
        };

    private static string FormatRoiEditIssue(ScanRoiValidationIssue? issue)
        => issue is null
            ? "ScanDebug_Runtime_RoiRangeChanged".GetLocalized()
            : "ScanDebug_Runtime_RoiIssueStatusFormat".GetLocalizedFormat(FormatRoiIssueMessage(issue), FormatRoiIssueTechnicalPath(issue));

    private static IReadOnlyList<ScanRoiValidationIssueDisplay> BuildRoiValidationIssueDisplays(IReadOnlyList<ScanRoiValidationIssue> issues)
        => issues.Select(issue => new ScanRoiValidationIssueDisplay
        {
            Issue = issue,
            Message = FormatRoiIssueMessage(issue),
            TechnicalPath = FormatRoiIssueTechnicalPath(issue),
            AutomationId = FormatRoiIssueAutomationId(issue),
            AutomationName = "ScanDebug_Runtime_RoiIssueAutomationName".GetLocalizedFormat(FormatRoiIssueMessage(issue), FormatRoiIssueTechnicalPath(issue))
        }).ToArray();

    private static string FormatRoiIssueMessage(ScanRoiValidationIssue issue)
        => "ScanDebug_Runtime_RoiIssueMessage".GetLocalizedFormat(
            GetRoiIssueOwnerDisplayName(issue.Owner),
            GetRoiIssueFieldDisplayName(issue.FieldPath),
            GetRoiIssueCodeDisplayName(issue.Code));

    private static string FormatRoiIssueTechnicalPath(ScanRoiValidationIssue issue)
        => "ScanDebug_Runtime_RoiIssueTechnicalPath".GetLocalizedFormat(issue.Owner, issue.FieldPath, issue.Code);

    private static string FormatRoiIssueAutomationId(ScanRoiValidationIssue issue)
        => $"RoiIssue_{issue.Owner}_{issue.Code}_{issue.FieldPath}";

    private static string GetRoiIssueOwnerDisplayName(ScanRoiOperationOwner owner)
        => owner switch
        {
            ScanRoiOperationOwner.AdcCalibration => "ScanDebug_Runtime_RoiIssueOwner_AdcCalibration".GetLocalized(),
            ScanRoiOperationOwner.AutoFocus => "ScanDebug_Runtime_RoiIssueOwner_AutoFocus".GetLocalized(),
            ScanRoiOperationOwner.ImageReferenceSampling => "ScanDebug_Runtime_RoiIssueOwner_ImageReferenceSampling".GetLocalized(),
            _ => "ScanDebug_Runtime_RoiIssueOwner_Unknown".GetLocalized()
        };

    private static string GetRoiIssueFieldDisplayName(string fieldPath)
        => fieldPath switch
        {
            "EffectiveRange" => "ScanDebug_Runtime_RoiIssueField_EffectiveRange".GetLocalized(),
            "ShieldRange" => "ScanDebug_Runtime_RoiIssueField_ShieldRange".GetLocalized(),
            "FocusLeftRange" => "ScanDebug_Runtime_RoiIssueField_FocusLeftRange".GetLocalized(),
            "FocusRightRange" => "ScanDebug_Runtime_RoiIssueField_FocusRightRange".GetLocalized(),
            "FocusOverallRange" => "ScanDebug_Runtime_RoiIssueField_FocusOverallRange".GetLocalized(),
            "ColumnRange" => "ScanDebug_Runtime_RoiIssueField_ColumnRange".GetLocalized(),
            "RoiSelection" => "ScanDebug_Runtime_RoiIssueField_RoiSelection".GetLocalized(),
            "RoiSettings" => "ScanDebug_Runtime_RoiIssueField_RoiSettings".GetLocalized(),
            _ => "ScanDebug_Runtime_RoiIssueField_Unknown".GetLocalizedFormat(fieldPath)
        };

    private static string GetRoiIssueCodeDisplayName(ScanRoiValidationCode code)
        => code switch
        {
            ScanRoiValidationCode.Missing => "ScanDebug_Runtime_RoiIssueCode_Missing".GetLocalized(),
            ScanRoiValidationCode.Empty => "ScanDebug_Runtime_RoiIssueCode_Empty".GetLocalized(),
            ScanRoiValidationCode.Inverted => "ScanDebug_Runtime_RoiIssueCode_Inverted".GetLocalized(),
            ScanRoiValidationCode.OutOfBounds => "ScanDebug_Runtime_RoiIssueCode_OutOfBounds".GetLocalized(),
            ScanRoiValidationCode.TooNarrow => "ScanDebug_Runtime_RoiIssueCode_TooNarrow".GetLocalized(),
            ScanRoiValidationCode.Overlap => "ScanDebug_Runtime_RoiIssueCode_Overlap".GetLocalized(),
            ScanRoiValidationCode.DoesNotContain => "ScanDebug_Runtime_RoiIssueCode_DoesNotContain".GetLocalized(),
            _ => "ScanDebug_Runtime_RoiIssueCode_Unknown".GetLocalized()
        };

    private void NotifyRoiEditCandidateAvailabilityChanged()
    {
        AutoBlackAdjustCommand.NotifyCanExecuteChanged();
        AutoWhiteAdjustCommand.NotifyCanExecuteChanged();
        AutoCalibrateCommand.NotifyCanExecuteChanged();
        AutoFocusCommand.NotifyCanExecuteChanged();
        QuickFocusCommand.NotifyCanExecuteChanged();
        FineFocusCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRunAutoFocusAction));
    }

    private void NotifyRoiValidationIssuesChanged()
    {
        OnPropertyChanged(nameof(AdcRoiValidationIssues));
        OnPropertyChanged(nameof(AdcRoiValidationIssueDisplays));
        OnPropertyChanged(nameof(FocusRoiValidationIssues));
        OnPropertyChanged(nameof(FocusRoiValidationIssueDisplays));
        OnPropertyChanged(nameof(ImageReferenceRoiValidationIssues));
        OnPropertyChanged(nameof(ImageReferenceRoiValidationIssueDisplays));
    }

    private bool HasCurrentColumnSample()
        => _columnSampleMean is not null
            && PreviewFrame is { } previewFrame
            && _columnSampleFrameVersion == previewFrame.Version
            && _columnSampleMeanRange == _columnSampleRange
            && string.Equals(_columnSampleMeanChannelRole, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase)
            && _columnSampleMeanOwnerVersion == _columnSampleOwnerVersion
            && IsColumnSampleSourceCurrentOwner()
            && ScanImageReferenceColumnRange.TryCreate(_columnSampleRange, previewFrame.Width).IsValid;

    private void InvalidateColumnSampleCache()
    {
        _columnSampleMean = null;
        _columnSampleFrameVersion = -1;
        _columnSampleMeanRange = null;
        _columnSampleMeanChannelRole = null;
        _columnSampleMeanOwnerVersion = -1;
    }

    private void InvalidateColumnSampleOwnership()
    {
        unchecked
        {
            _columnSampleOwnerVersion++;
        }

        InvalidateColumnSampleCache();
        NotifyColumnSampleAvailabilityChanged();
    }

    private void MarkColumnSampleSourceCurrentOwner()
    {
        _columnSampleSourceChannelRole = SelectedCalibrationChannel;
        _columnSampleSourceOwnerVersion = _columnSampleOwnerVersion;
    }

    private bool IsColumnSampleSourceCurrentOwner()
        => string.Equals(_columnSampleSourceChannelRole, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase)
            && _columnSampleSourceOwnerVersion == _columnSampleOwnerVersion;

    private void NotifyColumnSampleAvailabilityChanged()
    {
        SaveColumnSampleAsBlackLevelCommand.NotifyCanExecuteChanged();
        SaveColumnSampleAsWhiteLevelCommand.NotifyCanExecuteChanged();
    }

    private string BuildRoiStatusText()
    {
        var range = SelectedRoiSelection switch
        {
            RoiSelectionBwActive => _roiSettings.EffectiveRange,
            RoiSelectionBwShield => _roiSettings.ShieldRange,
            RoiSelectionFocusOverall => _roiSettings.FocusOverallRange,
            RoiSelectionFocusLeft => _roiSettings.FocusLeftRange,
            RoiSelectionFocusRight => _roiSettings.FocusRightRange,
            _ => _roiSettings.EffectiveRange
        };

        var editState = IsRoiEditModeEnabled ? "ScanDebug_Runtime_RoiEditModeOn".GetLocalized() : "ScanDebug_Runtime_RoiEditModeOff".GetLocalized();
        return "ScanDebug_Runtime_RoiStatus".GetLocalizedFormat(GetRoiSelectionDisplayName(SelectedRoiSelection), range.Start, range.EndInclusive, range.Width, editState);
    }

    private void UpdateTimingProjectionFromInputs(bool markEdited)
    {
        var clockIsValid = TryParseSysClockMhzText(SysClockMhz, out _);
        if (!TryParseTimingProjectionInputs(ExposureMicroseconds, SysClockMhz, out var exposureTicks, out var sysClockKhz))
        {
            _isSynchronizingTimingInputs = true;
            try
            {
                ExposureTicks = string.Empty;
                if (!clockIsValid)
                    SysClockKhz = string.Empty;
            }
            finally
            {
                _isSynchronizingTimingInputs = false;
            }

            UpdateComputedParameterDisplays();
            RefreshDerivedMotorDistanceFromCurrentInterval();
            UpdateComputedMotorSummary();
            RefreshCalibrationChannelItems();
            SynchronizeFilmProfileDraftFromInputs();
            return;
        }

        _isSynchronizingTimingInputs = true;
        try
        {
            SysClockKhz = sysClockKhz.ToString(CultureInfo.InvariantCulture);
            ExposureTicks = exposureTicks.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSynchronizingTimingInputs = false;
        }

        if (markEdited && _deviceTimingState.StateKind != ScanDeviceClockStateKind.ReadRequired)
            SetDeviceTimingState(new ScanDeviceTimingState(ScanDeviceClockStateKind.Edited, GetKnownCalibrationChannelRoles()));
        UpdateComputedParameterDisplays();
        RefreshDerivedMotorDistanceFromCurrentInterval();
        UpdateComputedMotorSummary();
        RefreshCalibrationChannelItems();
        SynchronizeFilmProfileDraftFromInputs();
    }

    private static bool TryParseTimingProjectionInputs(string exposureMicrosecondsText, string sysClockMhzText, out ushort exposureTicks, out uint sysClockKhz)
    {
        exposureTicks = 0;
        sysClockKhz = 0;

        if (!TryParseSysClockMhzText(sysClockMhzText, out sysClockKhz))
            return false;

        if (string.IsNullOrWhiteSpace(exposureMicrosecondsText)
            || !double.TryParse(exposureMicrosecondsText, NumberStyles.Number, CultureInfo.InvariantCulture, out var exposureMicroseconds)
            || !ScanTimingMath.TryConvertMicrosecondsToExposureTicks(exposureMicroseconds, sysClockKhz, out exposureTicks))
        {
            return false;
        }

        return true;
    }

    private static bool TryParseSysClockMhzText(string text, out uint sysClockKhz)
    {
        sysClockKhz = 0;
        var trimmed = text?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)
            || GetFractionalDigits(trimmed).Length > 3
            || !decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var sysClockMhz)
            || sysClockMhz < 30m
            || sysClockMhz > 200m)
        {
            return false;
        }

        var khz = sysClockMhz * 1000m;
        if (decimal.Truncate(khz) != khz
            || khz < ScanDebugConstants.MinSysClockKhz
            || khz > ScanDebugConstants.MaxSysClockKhz)
        {
            return false;
        }

        sysClockKhz = (uint)khz;
        return true;
    }

    private static string GetFractionalDigits(string text)
    {
        var decimalSeparatorIndex = text.IndexOf('.');
        if (decimalSeparatorIndex < 0 || decimalSeparatorIndex == text.Length - 1)
            return string.Empty;

        return text[(decimalSeparatorIndex + 1)..];
    }

    private void SetDeviceTimingState(ScanDeviceTimingState timingState)
    {
        _deviceTimingState = timingState;
        OnPropertyChanged(nameof(DeviceTimingState));
        OnPropertyChanged(nameof(RequiresDeviceRead));
        OnPropertyChanged(nameof(DeviceClockDisabledReasonText));
        OnPropertyChanged(nameof(ChannelParametersDisabledReasonText));
        OnPropertyChanged(nameof(SessionIlluminationDisabledReasonText));
        NotifyDeviceActionAvailabilityChanged();
        NotifyRuntimeOperationAvailabilityChanged();
    }

    private IReadOnlyList<string> GetKnownCalibrationChannelRoles()
        => CalibrationChannelOptions.ToArray();

    private void UpdateComputedParameterDisplays()
    {
        var displays = _parameters.BuildDisplays(ExposureMicroseconds, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockMhz);
        ExposureTimeDisplay = LocalizeExposureTimeDisplay(displays.ExposureTimeDisplay);
        Adc1OffsetMvDisplay = LocalizeOffsetAmplitudeDisplay(displays.Adc1OffsetMvDisplay);
        Adc2OffsetMvDisplay = LocalizeOffsetAmplitudeDisplay(displays.Adc2OffsetMvDisplay);
        Adc1GainVvDisplay = LocalizeGainDisplay(displays.Adc1GainVvDisplay);
        Adc2GainVvDisplay = LocalizeGainDisplay(displays.Adc2GainVvDisplay);
        SysClockMhzDisplay = LocalizeSysClockMhzDisplay(displays.SysClockMhzDisplay);
    }

    private static string LocalizeExposureTimeDisplay(string exposurePayload)
        => string.Equals(exposurePayload, "-", StringComparison.Ordinal)
            ? "ScanDebug_Runtime_ExposureTimeIdle".GetLocalized()
            : "ScanDebug_Runtime_ExposureTimeFormat".GetLocalizedFormat(exposurePayload);

    private static string LocalizeSysClockMhzDisplay(string sysClockPayload)
        => string.Equals(sysClockPayload, "-", StringComparison.Ordinal)
            ? "ScanDebug_Runtime_SystemClockIdle".GetLocalized()
            : "ScanDebug_Runtime_SystemClockFormat".GetLocalizedFormat(sysClockPayload);

    private static string LocalizeOffsetAmplitudeDisplay(string offsetPayload)
        => string.Equals(offsetPayload, "-", StringComparison.Ordinal)
            ? "ScanDebug_Runtime_OffsetAmplitudeIdle".GetLocalized()
            : "ScanDebug_Runtime_OffsetAmplitudeFormat".GetLocalizedFormat(offsetPayload);

    private static string LocalizeGainDisplay(string gainPayload)
        => string.Equals(gainPayload, "-", StringComparison.Ordinal)
            ? "ScanDebug_Runtime_GainIdle".GetLocalized()
            : "ScanDebug_Runtime_GainFormat".GetLocalizedFormat(gainPayload);

    private static string BuildBoundedLimitText(string text, int min, int max, string label)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "ScanDebug_Runtime_LimitBounded".GetLocalizedFormat(GetLimitLabelDisplayName(label), min, max);

        if (!int.TryParse(text, out var value))
            return "ScanDebug_Runtime_LimitBoundedInvalidInteger".GetLocalizedFormat(GetLimitLabelDisplayName(label), min, max);

        return value < min || value > max
            ? "ScanDebug_Runtime_LimitBoundedCurrentOutOfRange".GetLocalizedFormat(GetLimitLabelDisplayName(label), min, max, value)
            : "ScanDebug_Runtime_LimitBoundedCurrent".GetLocalizedFormat(GetLimitLabelDisplayName(label), min, max, value);
    }

    private static string BuildLowerBoundLimitText(string text, uint min, string label)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "ScanDebug_Runtime_LimitLowerBound".GetLocalizedFormat(GetLimitLabelDisplayName(label), min);

        if (!uint.TryParse(text, out var value))
            return "ScanDebug_Runtime_LimitLowerBoundInvalidInteger".GetLocalizedFormat(GetLimitLabelDisplayName(label), min);

        return value < min
            ? "ScanDebug_Runtime_LimitLowerBoundCurrentBelowMinimum".GetLocalizedFormat(GetLimitLabelDisplayName(label), min, value)
            : "ScanDebug_Runtime_LimitLowerBoundCurrent".GetLocalizedFormat(GetLimitLabelDisplayName(label), min, value);
    }

    private static string BuildPositiveDistanceLimitText(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "ScanDebug_Runtime_LimitDistanceMinimum".GetLocalizedFormat(GetLimitLabelDisplayName(label), AutofocusDistanceMinMm);

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return "ScanDebug_Runtime_LimitDistanceInvalidNumber".GetLocalizedFormat(GetLimitLabelDisplayName(label));

        return value < AutofocusDistanceMinMm
            ? "ScanDebug_Runtime_LimitDistanceCurrentBelowMinimum".GetLocalizedFormat(GetLimitLabelDisplayName(label), AutofocusDistanceMinMm, value)
            : "ScanDebug_Runtime_LimitDistanceCurrent".GetLocalizedFormat(GetLimitLabelDisplayName(label), value);
    }

    private static Brush BuildBoundedLimitBrush(string text, int min, int max)
        => string.IsNullOrWhiteSpace(text) || int.TryParse(text, out var value) && value >= min && value <= max
            ? LimitBlockNormalBrush.Value
            : LimitBlockAlertBrush.Value;

    private static Brush BuildBoundedLimitTextBrush(string text, int min, int max)
        => string.IsNullOrWhiteSpace(text) || int.TryParse(text, out var value) && value >= min && value <= max
            ? LimitBlockNormalTextBrush.Value
            : LimitBlockAlertTextBrush.Value;

    private static Brush BuildLowerBoundLimitBrush(string text, uint min)
        => string.IsNullOrWhiteSpace(text) || uint.TryParse(text, out var value) && value >= min
            ? LimitBlockNormalBrush.Value
            : LimitBlockAlertBrush.Value;

    private static Brush BuildLowerBoundLimitTextBrush(string text, uint min)
        => string.IsNullOrWhiteSpace(text) || uint.TryParse(text, out var value) && value >= min
            ? LimitBlockNormalTextBrush.Value
            : LimitBlockAlertTextBrush.Value;

    private static Brush BuildPositiveDistanceLimitBrush(string text)
        => string.IsNullOrWhiteSpace(text) || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= AutofocusDistanceMinMm
            ? LimitBlockNormalBrush.Value
            : LimitBlockAlertBrush.Value;

    private static Brush BuildPositiveDistanceLimitTextBrush(string text)
        => string.IsNullOrWhiteSpace(text) || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= AutofocusDistanceMinMm
            ? LimitBlockNormalTextBrush.Value
            : LimitBlockAlertTextBrush.Value;

    private static Brush GetThemeBrush(string resourceKey, Color fallbackColor)
        => Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true && resource is Brush brush
            ? brush
            : new SolidColorBrush(fallbackColor);

    private void RefreshLimitBlockBindings()
    {
        OnPropertyChanged(nameof(Adc1OffsetLimitText));
        OnPropertyChanged(nameof(Adc2OffsetLimitText));
        OnPropertyChanged(nameof(Adc1GainLimitText));
        OnPropertyChanged(nameof(Adc2GainLimitText));
        OnPropertyChanged(nameof(AutofocusSampleRowsLimitText));
        OnPropertyChanged(nameof(AutofocusTiltProbeStepsLimitText));
        OnPropertyChanged(nameof(AutofocusZProbeStepsLimitText));
        OnPropertyChanged(nameof(ManualFocusDistanceLimitText));
        OnPropertyChanged(nameof(AutofocusMotorIntervalLimitText));
        OnPropertyChanged(nameof(Adc1OffsetLimitBrush));
        OnPropertyChanged(nameof(Adc2OffsetLimitBrush));
        OnPropertyChanged(nameof(Adc1GainLimitBrush));
        OnPropertyChanged(nameof(Adc2GainLimitBrush));
        OnPropertyChanged(nameof(AutofocusSampleRowsLimitBrush));
        OnPropertyChanged(nameof(AutofocusTiltProbeStepsLimitBrush));
        OnPropertyChanged(nameof(AutofocusZProbeStepsLimitBrush));
        OnPropertyChanged(nameof(ManualFocusDistanceLimitBrush));
        OnPropertyChanged(nameof(AutofocusMotorIntervalLimitBrush));
        OnPropertyChanged(nameof(Adc1OffsetLimitTextBrush));
        OnPropertyChanged(nameof(Adc2OffsetLimitTextBrush));
        OnPropertyChanged(nameof(Adc1GainLimitTextBrush));
        OnPropertyChanged(nameof(Adc2GainLimitTextBrush));
        OnPropertyChanged(nameof(AutofocusSampleRowsLimitTextBrush));
        OnPropertyChanged(nameof(AutofocusTiltProbeStepsLimitTextBrush));
        OnPropertyChanged(nameof(AutofocusZProbeStepsLimitTextBrush));
        OnPropertyChanged(nameof(ManualFocusDistanceLimitTextBrush));
        OnPropertyChanged(nameof(AutofocusMotorIntervalLimitTextBrush));
    }

    private void NotifyManualFocusAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanStartManualFocusAction));
    }

    private void NotifyPreviewStatePropertiesChanged()
    {
        OnPropertyChanged(nameof(HasPreviewImage));
        OnPropertyChanged(nameof(IsPreviewImageAvailable));
        OnPropertyChanged(nameof(PreviewEmptyStateVisibility));
        OnPropertyChanged(nameof(PreviewEmptyStateTitleText));
        OnPropertyChanged(nameof(PreviewEmptyStateDescriptionText));
    }

    private void NotifyScanAcquisitionSettingsEditabilityChanged()
    {
        OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));
        NotifyScanWorkflowDependencyEditabilityChanged();
        RefreshAcquisitionChannelEditability();
    }

    private void NotifyScanWorkflowDependencyEditabilityChanged()
    {
        OnPropertyChanged(nameof(CanEditCaptureMode));
        OnPropertyChanged(nameof(CanEditScanLedAutoControl));
        OnPropertyChanged(nameof(CanEditWaterfall));
    }

    private void NotifyCaptureModeAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CaptureModeUnavailableReasonText));
        NotifyActionAvailabilityChanged();
    }

    private void NotifyDeviceActionAvailabilityChanged()
    {
        OnPropertyChanged(nameof(IsDeviceConnectActionAvailable));
        OnPropertyChanged(nameof(IsDeviceDisconnectActionAvailable));
        NotifyActionAvailabilityChanged();
    }

    private void NotifyActionAvailabilityChanged()
    {
        OnPropertyChanged(nameof(IsStartActionAvailable));
        OnPropertyChanged(nameof(IsStopActionAvailable));
        OnPropertyChanged(nameof(IsExportDngActionAvailable));
        OnPropertyChanged(nameof(CanStartManualFocusAction));
        OnPropertyChanged(nameof(CanRunAutoFocusAction));
        OnPropertyChanged(nameof(StartDisabledReasonText));
        OnPropertyChanged(nameof(ExportDngDisabledReasonText));
        OnPropertyChanged(nameof(DeviceClockDisabledReasonText));
        OnPropertyChanged(nameof(ChannelParametersDisabledReasonText));
        OnPropertyChanged(nameof(SessionIlluminationDisabledReasonText));
        NotifyRuntimeOperationAvailabilityChanged();
    }

    private void NotifyRuntimeOperationAvailabilityChanged()
    {
        ConnectDevicesCommand.NotifyCanExecuteChanged();
        DisconnectDevicesCommand.NotifyCanExecuteChanged();
        StartScanCommand.NotifyCanExecuteChanged();
        StopScanCommand.NotifyCanExecuteChanged();
        ExportDngCommand.NotifyCanExecuteChanged();
        ApplyDeviceClockCommand.NotifyCanExecuteChanged();
        ApplyParametersCommand.NotifyCanExecuteChanged();
        RefreshIlluminationCommand.NotifyCanExecuteChanged();
        ApplyIlluminationCommand.NotifyCanExecuteChanged();
        RefreshMotionCommand.NotifyCanExecuteChanged();
        EnableMotorCommand.NotifyCanExecuteChanged();
        DisableMotorCommand.NotifyCanExecuteChanged();
        MoveMotorCommand.NotifyCanExecuteChanged();
        StopMotorCommand.NotifyCanExecuteChanged();
        StopAllMotorsCommand.NotifyCanExecuteChanged();
        ApplyMotorConfigCommand.NotifyCanExecuteChanged();
        AutoBlackAdjustCommand.NotifyCanExecuteChanged();
        AutoWhiteAdjustCommand.NotifyCanExecuteChanged();
        AutoCalibrateCommand.NotifyCanExecuteChanged();
        AutoFocusCommand.NotifyCanExecuteChanged();
        QuickFocusCommand.NotifyCanExecuteChanged();
        FineFocusCommand.NotifyCanExecuteChanged();
        SaveFocusMappingCommand.NotifyCanExecuteChanged();
        TestLeftFocusMappingCommand.NotifyCanExecuteChanged();
        TestRightFocusMappingCommand.NotifyCanExecuteChanged();
        SaveChannelProfileCommand.NotifyCanExecuteChanged();
        ClearChannelProfileCommand.NotifyCanExecuteChanged();
        SaveColumnSampleAsBlackLevelCommand.NotifyCanExecuteChanged();
        SaveColumnSampleAsWhiteLevelCommand.NotifyCanExecuteChanged();
        SaveFilmProfileJsonCommand.NotifyCanExecuteChanged();
        LoadFilmProfileJsonCommand.NotifyCanExecuteChanged();
        ApplyStagedFilmProfileImportCommand.NotifyCanExecuteChanged();
        DiscardStagedFilmProfileImportCommand.NotifyCanExecuteChanged();
        NewFilmProfileCommand.NotifyCanExecuteChanged();
        ValidateFilmProfileCommand.NotifyCanExecuteChanged();
        ResetSelectedRoiCommand.NotifyCanExecuteChanged();
        ApplySelectedRoiInputsCommand.NotifyCanExecuteChanged();
        ResetAllRoisCommand.NotifyCanExecuteChanged();
    }

    private void MarkMotionStateReadRequired()
    {
        _motionRuntimeState.MarkReadRequired();
        NotifyMotionFreshnessChanged();
    }

    private void NotifyMotionFreshnessChanged()
    {
        MotionStateReadRequiredText = _motionRuntimeState.RequiresRead
            ? GetTodo18Localized("ScanDebug_Runtime_MotionStateReadRequired", "Motion state is unknown. Read device motion state before starting another motion command.")
            : string.Empty;
        OnPropertyChanged(nameof(IsMotionStateReadRequired));
        OnPropertyChanged(nameof(MotionStateReadRequiredVisibility));
        NotifyRuntimeOperationAvailabilityChanged();
        NotifyAutofocusAvailabilityChanged();
    }

    private void NotifyProfileStateChanged()
    {
        OnPropertyChanged(nameof(CurrentProfileNameText));
        OnPropertyChanged(nameof(ProfileSaveStateText));
        OnPropertyChanged(nameof(SaveProfileDisabledReasonText));
    }

    private void NotifyStagedFilmProfileImportStateChanged()
    {
        OnPropertyChanged(nameof(StagedFilmProfileImportDisplayNameText));
        OnPropertyChanged(nameof(StagedFilmProfileImportChannelCountText));
        OnPropertyChanged(nameof(HasPendingFilmProfileImportResult));
        OnPropertyChanged(nameof(FilmProfileImportResultReviewVisibility));
        OnPropertyChanged(nameof(FilmProfileWorkbenchContentVisibility));
        OnPropertyChanged(nameof(StagedFilmProfileImportReviewVisibility));
        OnPropertyChanged(nameof(StagedFilmProfileImportSeverity));
        OnPropertyChanged(nameof(StagedFilmProfileDirtyReplacementWarningText));
        OnPropertyChanged(nameof(StagedFilmProfileDirtyReplacementWarningVisibility));
        OnPropertyChanged(nameof(CanApplyStagedFilmProfileImport));
        ApplyStagedFilmProfileImportCommand.NotifyCanExecuteChanged();
        DiscardStagedFilmProfileImportCommand.NotifyCanExecuteChanged();
    }

    private void NotifyAcquisitionPlanChanged()
    {
        if (GetSelectedAcquisitionChannelCount() > 1 && IsWaterfallEnabled)
            IsWaterfallEnabled = false;

        foreach (var channel in AcquisitionChannels)
            channel.RefreshStatus();

        IsMultiChannelScanEnabled = GetSelectedAcquisitionChannelCount() > 1;
        OnPropertyChanged(nameof(AcquisitionPlanSummaryText));
        OnPropertyChanged(nameof(AcquisitionChannelOrderText));
        OnPropertyChanged(nameof(ChannelLedBindingSummaryText));
        OnPropertyChanged(nameof(ProfileChannelOverviewText));
        OnPropertyChanged(nameof(CaptureModeUnavailableReasonText));
        NotifyScanWorkflowDependencyEditabilityChanged();
        NotifyActionAvailabilityChanged();
        StartScanCommand.NotifyCanExecuteChanged();
    }

    private void NotifyChannelProfileOverviewChanged()
    {
        OnPropertyChanged(nameof(ProfileChannelOverviewText));
        NotifyPreviewStatePropertiesChanged();
    }

    private string BuildProfileChannelOverviewText()
    {
        var roles = GetEffectiveDeviceChannelRoles();
        var items = new List<string>(ScanDebugConstants.IlluminationChannelCount);
        for (var index = 0; index < roles.Length; index++)
        {
            var role = roles[index];
            if (!IsActiveIlluminationRole(role))
            {
                items.Add("ScanDebug_ProfileChannelOverviewUnusedItem".GetLocalizedFormat($"LED{index + 1}"));
                continue;
            }

            var calibrationState = _calibrationProfiles.TryGetProfile(role, out _)
                ? "ScanDebug_ProfileChannelOverviewCalibrationSaved".GetLocalized()
                : "ScanDebug_ProfileChannelOverviewCalibrationMissing".GetLocalized();
            items.Add("ScanDebug_ProfileChannelOverviewItem".GetLocalizedFormat($"LED{index + 1}", GetCalibrationChannelDisplayName(role), calibrationState));
        }

        return string.Join(" / ", items);
    }

    private string BuildPreviewEmptyStateTitle()
    {
        if (IsRunning)
            return "ScanDebug_PreviewEmptyTitleScanning".GetLocalized();
        if (!IsConnected)
            return IsDevicesPresent
                ? "ScanDebug_PreviewEmptyTitleDeviceDetected".GetLocalized()
                : "ScanDebug_PreviewEmptyTitleNoDevice".GetLocalized();
        if (!HasSelectedCalibrationProfile())
            return "ScanDebug_PreviewEmptyTitleChannelUncalibrated".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel));
        if (_hasValidScanBuffer)
            return "ScanDebug_PreviewEmptyTitleCapturedNoPreview".GetLocalized();
        return "ScanDebug_PreviewEmptyTitleReady".GetLocalized();
    }

    private string BuildPreviewEmptyStateDescription()
    {
        if (IsRunning)
            return "ScanDebug_PreviewEmptyDescriptionScanning".GetLocalized();
        if (!IsConnected)
            return IsDevicesPresent
                ? "ScanDebug_PreviewEmptyDescriptionDeviceDetected".GetLocalized()
                : "ScanDebug_PreviewEmptyDescriptionNoDevice".GetLocalized();
        if (!HasSelectedCalibrationProfile())
            return "ScanDebug_PreviewEmptyDescriptionChannelUncalibrated".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel), GetBoundLedName(SelectedCalibrationChannel));
        if (_hasValidScanBuffer)
            return "ScanDebug_PreviewEmptyDescriptionCapturedNoPreview".GetLocalized();
        return "ScanDebug_PreviewEmptyDescriptionReady".GetLocalizedFormat(AcquisitionChannelOrderText);
    }

    private bool HasSelectedCalibrationProfile()
        => !string.IsNullOrWhiteSpace(SelectedCalibrationChannel)
            && _calibrationProfiles.TryGetProfile(SelectedCalibrationChannel, out _);

    private string BuildStartDisabledReason()
    {
        if (IsRunning)
            return "ScanDebug_DisabledReasonScanRunning".GetLocalized();
        if (IsOutputOperationRunning)
            return "ScanDebug_DisabledReasonOutputRunning".GetLocalized();
        if (IsAutoFocusing || IsManualFocusing || IsApplyingIllumination || IsApplyingMotion)
            return "ScanDebug_DisabledReasonDeviceBusy".GetLocalized();
        if (!IsConnected)
            return "ScanDebug_DisabledReasonConnectDevice".GetLocalized();
        if (!HasSelectedAcquisitionChannels())
            return "ScanDebug_DisabledReasonNoAcquisitionChannels".GetLocalized();
        if (!IsCaptureModeCompatibleWithSelection())
            return BuildCaptureModeUnavailableReason();
        if (!TryParseRequestedRows(out _))
            return "ScanDebug_DisabledReasonInvalidRows".GetLocalized();
        return "ScanDebug_DisabledReasonUnavailable".GetLocalized();
    }

    private string BuildExportDngDisabledReason()
    {
        if (IsRunning)
            return "ScanDebug_DisabledReasonScanRunning".GetLocalized();
        if (IsOutputOperationRunning)
            return "ScanDebug_DisabledReasonOutputRunning".GetLocalized();
        if (IsManualFocusing)
            return "ScanDebug_DisabledReasonDeviceBusy".GetLocalized();
        if (!_hasValidScanBuffer || _lineBuffer.Length == 0)
            return "ScanDebug_DisabledReasonNoCapture".GetLocalized();
        if (_lastWorkflowResult is not null && !CanExportCurrentDngCapture())
            return "ScanDebug_DisabledReasonRequiresSingleOrFourChannelCapture".GetLocalized();
        return "ScanDebug_DisabledReasonUnavailable".GetLocalized();
    }

    private string BuildDeviceClockDisabledReason()
    {
        if (!TryParseSysClockMhzText(SysClockMhz, out _))
            return "ScanDebug_Runtime_DeviceClockInvalidReason".GetLocalized();

        return BuildRuntimeCommandDisabledReason(ScanDebugRuntimeCommandKind.ApplyDeviceClock);
    }

    private string BuildChannelParametersDisabledReason()
    {
        var runtimeReason = BuildRuntimeCommandDisabledReason(ScanDebugRuntimeCommandKind.ApplyParameters);
        if (!string.Equals(runtimeReason, "ScanDebug_DisabledReasonUnavailable".GetLocalized(), StringComparison.Ordinal))
            return runtimeReason;

        if (!HasDeviceKnownTimingForSelectedChannelParameterApply())
            return "ScanDebug_Runtime_DeviceClockReadRequiredReason".GetLocalized();

        return runtimeReason;
    }

    private string BuildSessionIlluminationDisabledReason()
    {
        var runtimeReason = BuildRuntimeCommandDisabledReason(ScanDebugRuntimeCommandKind.ApplyIllumination);
        if (!string.Equals(runtimeReason, "ScanDebug_DisabledReasonUnavailable".GetLocalized(), StringComparison.Ordinal))
            return runtimeReason;

        if (HasUnsafeDeviceTimingForActiveRoles(GetActiveRoles(BuildDebugChannelAssignment())))
            return "ScanDebug_Runtime_DeviceClockReadRequiredReason".GetLocalized();

        return runtimeReason;
    }

    private string BuildRuntimeCommandDisabledReason(ScanDebugRuntimeCommandKind command)
    {
        var gate = GetRuntimeOperationGateResult(command);
        return gate.ReasonCode switch
        {
            ScanDebugRuntimeOperationGateReason.DeviceDisconnected => "ScanDebug_DisabledReasonConnectDevice".GetLocalized(),
            ScanDebugRuntimeOperationGateReason.RuntimeOperationActive => "ScanDebug_DisabledReasonDeviceBusy".GetLocalized(),
            _ => "ScanDebug_DisabledReasonUnavailable".GetLocalized()
        };
    }

    private async Task LoadIlluminationStateAsync(CancellationToken ct)
    {
        var state = await _illumination.GetStateAsync(_session, ct);
        ApplyDeviceIlluminationSnapshot(state);
    }

    private static ScanIlluminationState BuildIlluminationState(IlluminationRequest request)
        => new(
            request.Led1Level,
            request.Led2Level,
            request.Led3Level,
            request.Led4Level,
            request.SteadyMask,
            request.SyncMask,
            0,
            request.Led1PulseClock,
            request.Led2PulseClock,
            request.Led3PulseClock,
            request.Led4PulseClock);

    private async Task LoadMotionStateAsync(CancellationToken ct)
    {
        var read = _motionRuntimeState.CaptureRead();
        try
        {
            var states = await _session.GetMotionStateAsync(ct);
            if (_motionRuntimeState.TryAcceptCompleteRead(read, states))
            {
                ApplyMotionStateToInputs(states);
                NotifyMotionFreshnessChanged();
                return;
            }

            if (_motionRuntimeState.IsCurrent(read))
                MarkMotionStateReadRequired();
        }
        catch
        {
            if (_motionRuntimeState.IsCurrent(read))
                MarkMotionStateReadRequired();
            throw;
        }
    }

    private void ApplyDeviceIlluminationSnapshot(ScanIlluminationState state)
    {
        _deviceIlluminationSnapshot = state;
        OnPropertyChanged(nameof(DeviceIlluminationSnapshot));
        OnPropertyChanged(nameof(DeviceLed1Level));
        OnPropertyChanged(nameof(DeviceLed2Level));
        OnPropertyChanged(nameof(DeviceLed3Level));
        OnPropertyChanged(nameof(DeviceLed4Level));
        OnPropertyChanged(nameof(DeviceLed1PulseClock));
        OnPropertyChanged(nameof(DeviceLed2PulseClock));
        OnPropertyChanged(nameof(DeviceLed3PulseClock));
        OnPropertyChanged(nameof(DeviceLed4PulseClock));
        OnPropertyChanged(nameof(IsDeviceLed1SteadyEnabled));
        OnPropertyChanged(nameof(IsDeviceLed2SteadyEnabled));
        OnPropertyChanged(nameof(IsDeviceLed3SteadyEnabled));
        OnPropertyChanged(nameof(IsDeviceLed4SteadyEnabled));
        OnPropertyChanged(nameof(IsDeviceLed1SyncEnabled));
        OnPropertyChanged(nameof(IsDeviceLed2SyncEnabled));
        OnPropertyChanged(nameof(IsDeviceLed3SyncEnabled));
        OnPropertyChanged(nameof(IsDeviceLed4SyncEnabled));
        IlluminationSummaryText = BuildIlluminationSummary(state);
    }

    private void ApplyDraftIlluminationStateToInputs(ScanIlluminationState state)
    {
        var wasSynchronizing = _isSynchronizingFilmProfileWorkspace;
        _isSynchronizingFilmProfileWorkspace = true;
        try
        {
            Led1Level = state.Led1Level.ToString();
            Led2Level = state.Led2Level.ToString();
            Led3Level = state.Led3Level.ToString();
            Led4Level = state.Led4Level.ToString();
            Led1PulseClock = state.Led1PulseClock.ToString();
            Led2PulseClock = state.Led2PulseClock.ToString();
            Led3PulseClock = state.Led3PulseClock.ToString();
            Led4PulseClock = state.Led4PulseClock.ToString();
            IsLed1SteadyEnabled = (state.SteadyMask & 0x01) != 0;
            IsLed2SteadyEnabled = (state.SteadyMask & 0x02) != 0;
            IsLed3SteadyEnabled = (state.SteadyMask & 0x04) != 0;
            IsLed4SteadyEnabled = (state.SteadyMask & 0x08) != 0;
            IsLed1SyncEnabled = (state.SyncMask & 0x01) != 0;
            IsLed2SyncEnabled = (state.SyncMask & 0x02) != 0;
            IsLed3SyncEnabled = (state.SyncMask & 0x04) != 0;
            IsLed4SyncEnabled = (state.SyncMask & 0x08) != 0;
            RefreshActiveIlluminationChannels();
        }
        finally
        {
            _isSynchronizingFilmProfileWorkspace = wasSynchronizing;
            RefreshFilmProfileWorkspaceProjection();
        }
    }

    private void ApplyProfileAcquisitionSettings(ScanFilmAcquisitionSettings settings)
    {
        var normalized = settings.Normalize();
        _selectedFilmAcquisitionSettings = normalized;
        SelectedRows = normalized.Rows.ToString(CultureInfo.InvariantCulture);
        if (normalized.ScanMotorId < MotorOptions.Count)
            SelectedScanMotor = MotorOptions[normalized.ScanMotorId];
        SelectedStartingDirection = normalized.StartingDirectionPositive ? ForwardDirection : ReverseDirection;
        IsWarmUpEnabled = normalized.WarmUpEnabled;
        IsAlternateMotorDirectionEnabled = normalized.TransportStrategy == ScanFilmTransportStrategy.AlternateDirection;

        ApplyDraftIlluminationStateToInputs(new ScanIlluminationState(
            normalized.Led1Level,
            normalized.Led2Level,
            normalized.Led3Level,
            normalized.Led4Level,
            normalized.SteadyMask,
            normalized.SyncMask,
            0,
            normalized.Led1PulseClock,
            normalized.Led2PulseClock,
            normalized.Led3PulseClock,
            normalized.Led4PulseClock));

        string? targetLinePitchDisplayValue = null;
        var hasTargetLinePitch = normalized.TargetLinePitchMicrometers is double targetLinePitchMicrometers
            && ScanMotorDistanceText.TryFormatDisplayValue(targetLinePitchMicrometers / 1000.0, MotorDistancePerLineUnit, GetCurrentScanMotorSettings(), out targetLinePitchDisplayValue);
        _isMotorDistanceDerivedFromInterval = !hasTargetLinePitch;
        MotorIntervalUs = FormatMotorIntervalInput(normalized.MotorIntervalNs);
        if (hasTargetLinePitch && targetLinePitchDisplayValue is not null)
        {
            MotorDistancePerLineValue = targetLinePitchDisplayValue;
        }
        else
        {
            RefreshDerivedMotorDistanceFromCurrentInterval();
        }

        var selectedSlots = normalized.AcquisitionChannelAssignment?.Roles.ToArray() ?? [];
        foreach (var channel in AcquisitionChannels)
        {
            var isSelected = channel.LedIndex < selectedSlots.Length
                && ScanChannelRoleHelper.IsActiveRole(selectedSlots[channel.LedIndex]);
            channel.SetSelectedFromOwner(isSelected);
        }

        NotifyAcquisitionPlanChanged();
        if (TryParseSelectedScanMotor(out var motorId, out _))
            ApplyMotorSpeedFromIntervalNs(motorId, normalized.MotorIntervalNs);

        UpdateComputedMotorSummary();
    }

    private void ResetIlluminationInputs()
    {
        Led1Level = "0";
        Led2Level = "0";
        Led3Level = "0";
        Led4Level = "0";
        Led1PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        Led2PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        Led3PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        Led4PulseClock = ScanDebugConstants.IlluminationMinSyncPulseClock.ToString();
        IsLed1SteadyEnabled = false;
        IsLed2SteadyEnabled = false;
        IsLed3SteadyEnabled = false;
        IsLed4SteadyEnabled = false;
        IsLed1SyncEnabled = false;
        IsLed2SyncEnabled = false;
        IsLed3SyncEnabled = false;
        IsLed4SyncEnabled = false;
        RefreshActiveIlluminationChannels();
        IlluminationSummaryText = "ScanDebug_Runtime_IlluminationSummaryIdle".GetLocalized();
    }

    private void ApplyMotionStateToInputs(IReadOnlyList<ScanMotorState> states)
    {
        var indexedStates = new ScanMotorState?[ScanDebugConstants.MotionMotorCount];
        foreach (var state in states)
        {
            if (state.MotorId < indexedStates.Length)
                indexedStates[state.MotorId] = state;
        }

        Motor1StatusText = BuildMotorStatusText(indexedStates[0]);
        Motor2StatusText = BuildMotorStatusText(indexedStates[1]);
        Motor3StatusText = BuildMotorStatusText(indexedStates[2]);
        MotionSummaryText = BuildMotionSummary(indexedStates);
    }

    private void ResetMotionInputs()
    {
        Motor1StatusText = "ScanDebug_Runtime_MotorStatusIdle".GetLocalized();
        Motor2StatusText = "ScanDebug_Runtime_MotorStatusIdle".GetLocalized();
        Motor3StatusText = "ScanDebug_Runtime_MotorStatusIdle".GetLocalized();
        Motor1MoveDirection = MotorDirectionLabels[0];
        Motor2MoveDirection = MotorDirectionLabels[0];
        Motor3MoveDirection = MotorDirectionLabels[0];
        Motor1MoveValue = "200";
        Motor2MoveValue = "200";
        Motor3MoveValue = "200";
        Motor1MoveUnit = MotorUnitSteps;
        Motor2MoveUnit = MotorUnitSteps;
        Motor3MoveUnit = MotorUnitSteps;
        Motor1MoveSteps = "200";
        Motor2MoveSteps = "200";
        Motor3MoveSteps = "200";
        ApplyMotorSpeedFromIntervalNs(0, ScanDebugConstants.MotionDefaultIntervalNs);
        ApplyMotorSpeedFromIntervalNs(1, ScanDebugConstants.MotionDefaultIntervalNs);
        ApplyMotorSpeedFromIntervalNs(2, ScanDebugConstants.MotionDefaultIntervalNs);
        MotorIntervalUs = FormatMotorIntervalInput(ScanDebugConstants.MotionDefaultIntervalNs);
        _isMotorDistanceDerivedFromInterval = true;
        RefreshDerivedMotorDistanceFromCurrentInterval();
        MotionSummaryText = "ScanDebug_Runtime_MotionSummaryIdle".GetLocalized();
        UpdateComputedMotorSummary();
        UpdateMotorSemanticProjection();
    }

    private void UpdateMotorSemanticProjection()
    {
        for (byte motorId = 0; motorId < ScanDebugConstants.MotionMotorCount; motorId++)
        {
            var roleText = BuildMotorRoleText(motorId);
            var summary = BuildMotorMoveSummary(motorId, roleText);
            var applyEffect = GetTodo18LocalizedFormat(
                "ScanDebug_Runtime_MotorApplyConfigEffect",
                "Applies the device motor configuration to {0}, then reads motion state. Editor speed, distance, and direction fields are not sent by this command.",
                FormatMotorOption(motorId));
            switch (motorId)
            {
                case 0:
                    Motor1RoleText = roleText;
                    Motor1MoveSummaryText = summary;
                    Motor1ApplyConfigEffectText = applyEffect;
                    break;
                case 1:
                    Motor2RoleText = roleText;
                    Motor2MoveSummaryText = summary;
                    Motor2ApplyConfigEffectText = applyEffect;
                    break;
                case 2:
                    Motor3RoleText = roleText;
                    Motor3MoveSummaryText = summary;
                    Motor3ApplyConfigEffectText = applyEffect;
                    break;
            }
        }
    }

    private string BuildMotorRoleText(byte motorId)
    {
        var roles = new List<string>();
        var settings = _deviceSettings.Settings.Normalize();
        if (TryGetValidatedFocusMotorMapping(settings, out var mapping, out _))
        {
            if (mapping.LeftMotorId == motorId)
                roles.Add(GetTodo18Localized("ScanDebug_Runtime_MotorRoleLeftFocus", "left focus motor"));
            if (mapping.RightMotorId == motorId)
                roles.Add(GetTodo18Localized("ScanDebug_Runtime_MotorRoleRightFocus", "right focus motor"));
        }

        if (TryParseSelectedScanMotor(out var scanMotorId, out _) && scanMotorId == motorId)
            roles.Add(GetTodo18Localized("ScanDebug_Runtime_MotorRoleScanTransport", "film transport motor"));

        var roleText = roles.Count == 0 ? GetTodo18Localized("ScanDebug_Runtime_MotorRoleNone", "no configured semantic role") : string.Join("; ", roles);
        return GetTodo18LocalizedFormat("ScanDebug_Runtime_MotorRoleSummary", "{0}: {1}", FormatMotorOption(motorId), roleText);
    }

    private string BuildMotorMoveSummary(byte motorId, string roleText)
    {
        if (!TryBuildMotorMoveRequest(motorId, out var request, out var error))
            return GetTodo18LocalizedFormat("ScanDebug_Runtime_MotorMoveSummaryInvalid", "{0}: {1}; requested move is invalid: {2}", FormatMotorOption(motorId), roleText, error);

        var settings = _deviceSettings.Settings.Normalize();
        var distanceMm = ScanTimingMath.ConvertMotorStepsToMillimeters(request.Steps, settings.GetMotorSettings(motorId));
        var durationSeconds = request.Steps * (double)request.IntervalNs / 1_000_000_000.0;
        var rawDirection = request.Direction
            ? "ScanDebug_Runtime_MotorDirectionDir1".GetLocalizedOrFallback("Dir1")
            : "ScanDebug_Runtime_MotorDirectionDir0".GetLocalizedOrFallback("Dir0");
        var logicalDirections = new List<string>();
        if (TryGetValidatedFocusMotorMapping(settings, out var mapping, out _)
            && (mapping.LeftMotorId == motorId || mapping.RightMotorId == motorId))
        {
            logicalDirections.Add(request.Direction == mapping.ZPositiveDirection
                ? GetTodo18Localized("ScanDebug_Runtime_MotorLogicalDirectionFocusZPositive", "focus Z+")
                : GetTodo18Localized("ScanDebug_Runtime_MotorLogicalDirectionFocusZNegative", "focus Z-"));
        }

        if (TryParseSelectedScanMotor(out var scanMotorId, out _) && scanMotorId == motorId)
            logicalDirections.Add(GetTodo18LocalizedFormat("ScanDebug_Runtime_MotorLogicalDirectionTransport", "film transport {0}", GetLocalizedDirectionLabel(SelectedStartingDirection)));

        var logicalDirection = logicalDirections.Count == 0 ? GetTodo18Localized("ScanDebug_Runtime_MotorLogicalDirectionUnmapped", "unmapped") : string.Join("; ", logicalDirections);
        return GetTodo18LocalizedFormat(
            "ScanDebug_Runtime_MotorMoveSummary",
            "{0}: {1}; distance {2} mm; estimated steps {3}; estimated duration {4} s; logical direction {5}; raw direction {6}; physical direction unknown.",
            FormatMotorOption(motorId),
            roleText,
            FormatMotionDecimal(distanceMm),
            request.Steps,
            FormatMotionDecimal(durationSeconds),
            logicalDirection,
            rawDirection);
    }

    private static string LocalizeGlobalMotorStopResult(ScanOperationResult result)
    {
        var message = result.Message?.Trim() ?? string.Empty;
        if (result.Success && string.Equals(message, "Motor stop commands completed for IDs 0, 1, and 2. Hardware motion state was not verified.", StringComparison.Ordinal))
            return GetTodo18Localized("ScanDebug_Runtime_StatusStopAllMotorsSucceeded", "All motor stop commands were dispatched for Motor1, Motor2, and Motor3. Hardware motion state was not verified; read motion state before resuming motion.");

        if (message.StartsWith("Motor stop commands completed with failures: ", StringComparison.Ordinal))
        {
            var details = message["Motor stop commands completed with failures: ".Length..];
            const string suffix = ". Hardware motion state was not verified.";
            if (details.EndsWith(suffix, StringComparison.Ordinal))
                details = details[..^suffix.Length];
            return GetTodo18LocalizedFormat("ScanDebug_Runtime_StatusStopAllMotorsPartial", "Stop all motors completed with failures: {0}. Hardware motion state was not verified; read motion state before resuming motion.", details);
        }

        if (message.StartsWith("Global motor stop timed out with an unknown outcome. The current command transaction remains quarantined. Unattempted motors: ", StringComparison.Ordinal))
        {
            var details = message["Global motor stop timed out with an unknown outcome. The current command transaction remains quarantined. Unattempted motors: ".Length..];
            const string suffix = ". Hardware motion state was not verified.";
            if (details.EndsWith(suffix, StringComparison.Ordinal))
                details = details[..^suffix.Length];
            return GetTodo18LocalizedFormat("ScanDebug_Runtime_StatusStopAllMotorsQuarantined", "Global motor stop outcome is unknown. The command transaction remains quarantined. Unattempted motors: {0}. Hardware motion state was not verified; read motion state before resuming motion.", details);
        }

        if (string.Equals(message, "No connected scanner session is available for a global motor stop.", StringComparison.Ordinal))
            return GetTodo18LocalizedFormat("ScanDebug_Runtime_StatusStopAllMotorsFailed", "Stop all motors failed: {0}", "ScanDebug_Runtime_ServiceScannerNotConnectedShort".GetLocalized());

        return result.Success
            ? GetTodo18Localized("ScanDebug_Runtime_StatusStopAllMotorsSucceeded", "All motor stop commands were dispatched for Motor1, Motor2, and Motor3. Hardware motion state was not verified; read motion state before resuming motion.")
            : GetTodo18LocalizedFormat("ScanDebug_Runtime_StatusStopAllMotorsFailed", "Stop all motors failed: {0}", ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(message));
    }

    private static string GetTodo18Localized(string resourceKey, string fallback)
        => resourceKey.GetLocalizedOrFallback(fallback);

    private static string GetTodo18LocalizedFormat(string resourceKey, string fallbackFormat, params object[] args)
        => resourceKey.GetLocalizedFormatOrFallback(fallbackFormat, args);

    private static string FormatMotionDecimal(double value)
        => value.ToString("0.###", CultureInfo.CurrentCulture);

    private static string GetLocalizedDirectionLabel(string direction)
        => string.Equals(direction, ForwardDirection, StringComparison.Ordinal)
            ? "Scan_Runtime_DirectionForward".GetLocalized()
            : "Scan_Runtime_DirectionReverse".GetLocalized();

    private async Task ApplySessionIlluminationTestAsync(SessionIlluminationTestKind kind)
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        if (!TryBuildSessionIlluminationTestState(kind, out var state, out var error))
        {
            StatusText = error;
            return;
        }

        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.ApplyIllumination, out var runtimeClaim))
            return;

        IsApplyingIllumination = true;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusApplyingIllumination".GetLocalized();
            await _sessionCoordinator.UseConnectedSessionAsync(
                async (session, token) =>
                {
                    await _illumination.ApplyStateWithSafeTransitionAsync(session, state, token);
                    return true;
                },
                CancellationToken.None);

            await LoadIlluminationStateAsync(_session.ConnectionToken);
            StatusText = "ScanDebug_Runtime_StatusIlluminationUpdated".GetLocalized();
        }
        catch (OperationCanceledException)
        {
            StatusText = "ScanDebug_Runtime_StatusIlluminationUpdateCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusIlluminationUpdateFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsApplyingIllumination = false;
        }
    }

    private bool TryBuildSessionIlluminationTestState(SessionIlluminationTestKind kind, out ScanIlluminationState state, out string error)
    {
        state = new ScanIlluminationState(0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2);
        if (!int.TryParse(SessionTestLedIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ledNumber)
            || ledNumber < 1
            || ledNumber > ScanDebugConstants.IlluminationChannelCount)
        {
            error = "rawIllumination.session.ledIndex";
            return false;
        }

        if (kind == SessionIlluminationTestKind.Off)
        {
            error = string.Empty;
            return true;
        }

        if (!TryParseLedLevel(SessionTestLedLevel, "rawIllumination.session.level", out var level, out error)
            || !TryParsePulseClock(SessionTestPulseClock, "rawIllumination.session.pulseClock", out var pulseClock, out error))
        {
            return false;
        }

        var levels = new ushort[ScanDebugConstants.IlluminationChannelCount];
        var pulses = Enumerable.Repeat(ScanDebugConstants.IlluminationMinSyncPulseClock, ScanDebugConstants.IlluminationChannelCount).ToArray();
        var ledIndex = ledNumber - 1;
        levels[ledIndex] = level;
        pulses[ledIndex] = pulseClock;
        var bit = (byte)(1 << ledIndex);
        state = new ScanIlluminationState(
            levels[0],
            levels[1],
            levels[2],
            levels[3],
            kind == SessionIlluminationTestKind.Steady ? bit : (byte)0,
            kind == SessionIlluminationTestKind.AcquisitionSync ? bit : (byte)0,
            0,
            pulses[0],
            pulses[1],
            pulses[2],
            pulses[3]);
        var validation = ScanIlluminationValidator.ValidateState(state, "rawIllumination.session");
        if (validation.IsValid)
        {
            error = string.Empty;
            return true;
        }

        error = FormatIlluminationValidation(validation);
        return false;
    }

    private bool TryBuildRawIlluminationState(out ScanIlluminationState state, out string error)
    {
        RawIlluminationValidationIssues = Array.Empty<ScanIlluminationValidationIssue>();
        state = new ScanIlluminationState(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        if (!TryParseRawLedLevel(RawLed1Level, "rawIllumination.led1.level", "ScanDebug_Runtime_FieldLed1Level".GetLocalized(), out var led1Level, out error)
            || !TryParseRawLedLevel(RawLed2Level, "rawIllumination.led2.level", "ScanDebug_Runtime_FieldLed2Level".GetLocalized(), out var led2Level, out error)
            || !TryParseRawLedLevel(RawLed3Level, "rawIllumination.led3.level", "ScanDebug_Runtime_FieldLed3Level".GetLocalized(), out var led3Level, out error)
            || !TryParseRawLedLevel(RawLed4Level, "rawIllumination.led4.level", "ScanDebug_Runtime_FieldLed4Level".GetLocalized(), out var led4Level, out error)
            || !TryParseRawPulseClock(RawLed1PulseClock, "rawIllumination.led1.pulseClock", "ScanDebug_Runtime_FieldLed1PulseClock".GetLocalized(), out var led1PulseClock, out error)
            || !TryParseRawPulseClock(RawLed2PulseClock, "rawIllumination.led2.pulseClock", "ScanDebug_Runtime_FieldLed2PulseClock".GetLocalized(), out var led2PulseClock, out error)
            || !TryParseRawPulseClock(RawLed3PulseClock, "rawIllumination.led3.pulseClock", "ScanDebug_Runtime_FieldLed3PulseClock".GetLocalized(), out var led3PulseClock, out error)
            || !TryParseRawPulseClock(RawLed4PulseClock, "rawIllumination.led4.pulseClock", "ScanDebug_Runtime_FieldLed4PulseClock".GetLocalized(), out var led4PulseClock, out error))
        {
            return false;
        }

        state = new ScanIlluminationState(
            led1Level,
            led2Level,
            led3Level,
            led4Level,
            BuildMask(IsRawLed1SteadyEnabled, IsRawLed2SteadyEnabled, IsRawLed3SteadyEnabled, IsRawLed4SteadyEnabled),
            BuildMask(IsRawLed1SyncEnabled, IsRawLed2SyncEnabled, IsRawLed3SyncEnabled, IsRawLed4SyncEnabled),
            0,
            led1PulseClock,
            led2PulseClock,
            led3PulseClock,
            led4PulseClock);
        var validation = ScanIlluminationValidator.ValidateState(state, "rawIllumination");
        RawIlluminationValidationIssues = validation.Issues;
        if (validation.IsValid)
        {
            error = string.Empty;
            return true;
        }

        error = FormatIlluminationValidation(validation);
        return false;
    }

    private bool TryParseRawLedLevel(string text, string fieldPath, string fieldName, out ushort value, out string error)
    {
        if (TryParseLedLevel(text, fieldName, out value, out var detail))
        {
            error = string.Empty;
            return true;
        }

        error = SetRawIlluminationParseFailure(fieldPath, detail);
        return false;
    }

    private bool TryParseRawPulseClock(string text, string fieldPath, string fieldName, out uint value, out string error)
    {
        if (TryParsePulseClock(text, fieldName, out value, out var detail))
        {
            error = string.Empty;
            return true;
        }

        error = SetRawIlluminationParseFailure(fieldPath, detail);
        return false;
    }

    private string SetRawIlluminationParseFailure(string fieldPath, string detail)
    {
        RawIlluminationValidationIssues = new[]
        {
            new ScanIlluminationValidationIssue(fieldPath, ScanIlluminationValidationCode.InvalidNumericInput)
        };
        return $"{fieldPath}: {detail}";
    }

    private static string FormatIlluminationValidation(ScanIlluminationValidationResult validation)
        => string.Join("; ", validation.Issues.Select(issue => $"{issue.FieldPath}:{issue.Code}"));

    private bool TryBuildIlluminationRequest(out IlluminationRequest request, out string error, bool clearUnusedInputs = true)
    {
        request = new IlluminationRequest(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        if (clearUnusedInputs)
            ClearUnusedIlluminationInputs(GetEffectiveDeviceChannelRoles());

        if (!TryParseLedLevel(Led1Level, "ScanDebug_Runtime_FieldLed1Level".GetLocalized(), out var led1Level, out error)
            || !TryParseLedLevel(Led2Level, "ScanDebug_Runtime_FieldLed2Level".GetLocalized(), out var led2Level, out error)
            || !TryParseLedLevel(Led3Level, "ScanDebug_Runtime_FieldLed3Level".GetLocalized(), out var led3Level, out error)
            || !TryParseLedLevel(Led4Level, "ScanDebug_Runtime_FieldLed4Level".GetLocalized(), out var led4Level, out error)
            || !TryParsePulseClock(Led1PulseClock, "ScanDebug_Runtime_FieldLed1PulseClock".GetLocalized(), out var led1PulseClock, out error)
            || !TryParsePulseClock(Led2PulseClock, "ScanDebug_Runtime_FieldLed2PulseClock".GetLocalized(), out var led2PulseClock, out error)
            || !TryParsePulseClock(Led3PulseClock, "ScanDebug_Runtime_FieldLed3PulseClock".GetLocalized(), out var led3PulseClock, out error)
            || !TryParsePulseClock(Led4PulseClock, "ScanDebug_Runtime_FieldLed4PulseClock".GetLocalized(), out var led4PulseClock, out error))
        {
            return false;
        }

        var steadyMask = BuildMask(IsLed1SteadyEnabled, IsLed2SteadyEnabled, IsLed3SteadyEnabled, IsLed4SteadyEnabled);
        var syncMask = BuildMask(IsLed1SyncEnabled, IsLed2SyncEnabled, IsLed3SyncEnabled, IsLed4SyncEnabled);

        if ((steadyMask & syncMask) != 0)
        {
            error = "ScanDebug_Runtime_ErrorSteadySyncOverlap".GetLocalized();
            return false;
        }

        if (!ValidateSyncPulse(syncMask, 0x01, led1PulseClock, "ScanDebug_Runtime_FieldLed1PulseClock".GetLocalized(), out error)
            || !ValidateSyncPulse(syncMask, 0x02, led2PulseClock, "ScanDebug_Runtime_FieldLed2PulseClock".GetLocalized(), out error)
            || !ValidateSyncPulse(syncMask, 0x04, led3PulseClock, "ScanDebug_Runtime_FieldLed3PulseClock".GetLocalized(), out error)
            || !ValidateSyncPulse(syncMask, 0x08, led4PulseClock, "ScanDebug_Runtime_FieldLed4PulseClock".GetLocalized(), out error))
        {
            return false;
        }

        request = new IlluminationRequest(
            led1Level,
            led2Level,
            led3Level,
            led4Level,
            steadyMask,
            syncMask,
            led1PulseClock,
            led2PulseClock,
            led3PulseClock,
            led4PulseClock);
        error = string.Empty;
        return true;
    }

    private async Task SetMotorEnabledCoreAsync(string? motorDisplayId, bool enabled)
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        if (!TryParseMotorTarget(motorDisplayId, out var motorId, out var motorName, out var error))
        {
            StatusText = error;
            return;
        }

        var command = enabled
            ? ScanDebugRuntimeCommandKind.EnableMotor
            : ScanDebugRuntimeCommandKind.DisableMotor;
        var producer = _motionRuntimeState.CaptureProducer();
        if (!TryClaimRuntimeOperation(command, out var runtimeClaim))
            return;

        IsApplyingMotion = true;
        try
        {
            if (!_motionRuntimeState.IsCurrent(producer))
                return;
            StatusText = enabled ? "ScanDebug_Runtime_StatusMotorEnabling".GetLocalizedFormat(motorName) : "ScanDebug_Runtime_StatusMotorDisabling".GetLocalizedFormat(motorName);
            await _sessionCoordinator.UseConnectedSessionAsync(
                async (session, token) =>
                {
                    if (enabled)
                        producer.CancellationToken.ThrowIfCancellationRequested();
                    await session.SetMotorEnabledAsync(motorId, enabled, token);
                    return true;
                },
                enabled ? producer.CancellationToken : CancellationToken.None);
            await LoadMotionStateAsync(_session.ConnectionToken);
            StatusText = enabled ? "ScanDebug_Runtime_StatusMotorEnabled".GetLocalizedFormat(motorName) : "ScanDebug_Runtime_StatusMotorDisabled".GetLocalizedFormat(motorName);
        }
        catch (OperationCanceledException)
        {
            MarkMotionStateReadRequired();
            StatusText = enabled ? "ScanDebug_Runtime_StatusMotorEnableCanceled".GetLocalizedFormat(motorName) : "ScanDebug_Runtime_StatusMotorDisableCanceled".GetLocalizedFormat(motorName);
        }
        catch (Exception ex)
        {
            MarkMotionStateReadRequired();
            StatusText = enabled ? "ScanDebug_Runtime_StatusMotorEnableFailed".GetLocalizedFormat(motorName, ex.Message) : "ScanDebug_Runtime_StatusMotorDisableFailed".GetLocalizedFormat(motorName, ex.Message);
        }
        finally
        {
            runtimeClaim?.Dispose();
            IsApplyingMotion = false;
        }
    }

    private bool TryParseMotorTarget(string? motorDisplayId, out byte motorId, out string motorName, out string error)
    {
        motorId = 0;
        motorName = string.Empty;

        if (!int.TryParse(motorDisplayId, out var displayIndex) || displayIndex < 1 || displayIndex > ScanDebugConstants.MotionMotorCount)
        {
            error = "ScanDebug_Runtime_ErrorMotorSelectionRange".GetLocalizedFormat(ScanDebugConstants.MotionMotorCount);
            return false;
        }

        motorId = (byte)(displayIndex - 1);
        motorName = $"Motor{displayIndex}";
        error = string.Empty;
        return true;
    }

    private bool TryBuildMotorMoveRequest(byte motorId, out MotorMoveRequest request, out string error)
    {
        request = new MotorMoveRequest(false, 0, 0);

        var (directionText, moveValueText, moveUnitText, speedValueText, speedUnitText) = GetMotorMoveInputs(motorId);
        var direction = string.Equals(directionText, MotorDirectionLabels[1], StringComparison.Ordinal);

        if (!TryBuildMotorMoveSteps(motorId, moveValueText, moveUnitText, out var steps, out error)
            || !TryBuildMotorIntervalFromInputs(motorId, speedValueText, speedUnitText, out var intervalNs, out error))
        {
            return false;
        }

        request = new MotorMoveRequest(direction, steps, intervalNs);
        error = string.Empty;
        return true;
    }

    private (string DirectionText, string MoveValueText, string MoveUnitText, string SpeedValueText, string SpeedUnitText) GetMotorMoveInputs(byte motorId)
        => motorId switch
        {
            0 => (Motor1MoveDirection, Motor1MoveValue, Motor1MoveUnit, Motor1SpeedValue, Motor1SpeedUnit),
            1 => (Motor2MoveDirection, Motor2MoveValue, Motor2MoveUnit, Motor2SpeedValue, Motor2SpeedUnit),
            2 => (Motor3MoveDirection, Motor3MoveValue, Motor3MoveUnit, Motor3SpeedValue, Motor3SpeedUnit),
            _ => throw new ArgumentOutOfRangeException(nameof(motorId))
        };

    private bool TryBuildMotorMoveSteps(byte motorId, string moveValueText, string moveUnitText, out uint steps, out string error)
    {
        steps = 0;
        var unit = NormalizeMotorUnit(moveUnitText);
        var displayMotorId = motorId + 1;

        if (unit == MotorUnitSteps)
        {
            if (!uint.TryParse(moveValueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out steps) || steps == 0)
            {
                error = "ScanDebug_Runtime_ErrorMotorStepsPositive".GetLocalizedFormat(displayMotorId);
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (!double.TryParse(moveValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var distanceValue) || !double.IsFinite(distanceValue) || distanceValue <= 0.0)
        {
            error = "ScanDebug_Runtime_ErrorMotorMoveValuePositive".GetLocalizedFormat(displayMotorId, unit);
            return false;
        }

        var distanceMm = unit == MotorUnitMicrometers ? distanceValue / 1000.0 : distanceValue;
        if (!ScanTimingMath.TryConvertMillimetersToMotorSteps(distanceMm, _deviceSettings.Settings.GetMotorSettings(motorId), out steps))
        {
            error = "ScanDebug_Runtime_ErrorMotorMoveDistanceTooSmall".GetLocalizedFormat(displayMotorId, unit);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryBuildMotorIntervalFromInputs(byte motorId, string speedValueText, string speedUnitText, out uint intervalNs, out string error)
    {
        intervalNs = 0;
        var unit = NormalizeMotorUnit(speedUnitText);
        var displayMotorId = motorId + 1;

        if (unit == MotorUnitSteps && TryGetDerivedMotorIntervalNs(motorId, out var preservedIntervalNs))
        {
            intervalNs = preservedIntervalNs;
            error = string.Empty;
            return true;
        }

        if (!double.TryParse(speedValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var speedValue) || !double.IsFinite(speedValue) || speedValue <= 0.0)
        {
            error = "ScanDebug_Runtime_ErrorMotorSpeedValuePositive".GetLocalizedFormat(displayMotorId, unit);
            return false;
        }

        if (unit == MotorUnitSteps)
        {
            var computed = Math.Ceiling(1_000_000_000.0 / speedValue);
            if (!double.IsFinite(computed) || computed < ScanDebugConstants.MotionMinIntervalNs || computed > uint.MaxValue)
            {
                error = "ScanDebug_Runtime_ErrorMotorSpeedTooHigh".GetLocalizedFormat(displayMotorId, ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
                return false;
            }

            intervalNs = (uint)computed;
            error = string.Empty;
            return true;
        }

        var speedMmPerSecond = unit == MotorUnitMicrometers ? speedValue / 1000.0 : speedValue;
        if (!ScanTimingMath.TryConvertMillimetersPerSecondToMotorIntervalNs(speedMmPerSecond, _deviceSettings.Settings.GetMotorSettings(motorId), ScanDebugConstants.MotionMinIntervalNs, out intervalNs))
        {
            error = "ScanDebug_Runtime_ErrorMotorSpeedTooHigh".GetLocalizedFormat(displayMotorId, ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string NormalizeMotorUnit(string? unitText)
        => unitText?.Trim().ToLowerInvariant() switch
        {
            MotorUnitMicrometers => MotorUnitMicrometers,
            MotorUnitMillimeters => MotorUnitMillimeters,
            _ => MotorUnitSteps
        };

    private static string FormatMotorSpeedStepsPerSecond(uint intervalNs)
        => ScanTimingMath.ConvertMotorIntervalToStepsPerSecond(intervalNs).ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatMotorIntervalInput(uint intervalNs)
        => ScanMotorIntervalText.TryFormatMicroseconds(intervalNs, out var intervalUs) ? intervalUs : string.Empty;

    private async Task EnsureDeviceSettingsInitializedAsync()
    {
        await _deviceSettingsInitializationTask;
        ApplyInitializedDeviceSettingsProjection();
        RefreshActiveIlluminationChannels();
    }

    private async Task InitializeDeviceSettingsProjectionAsync()
    {
        await _deviceSettingsInitializationTask;
        _dispatcher.TryEnqueue(() =>
        {
            ApplyInitializedDeviceSettingsProjection();
            RefreshActiveIlluminationChannels();
        });
    }

    private void ApplyInitializedDeviceSettingsProjection()
    {
        if (_hasAppliedDeviceSettingsFocusMapping)
            return;

        _hasAppliedDeviceSettingsFocusMapping = true;
        var settings = _deviceSettings.Settings.Normalize();
        if (TryGetValidatedFocusMotorMapping(settings, out var mapping, out var error))
        {
            ApplyFocusMappingInputs(mapping);
            SetFocusMappingStatus("ScanDebug_Runtime_FocusMappingReadyStatus".GetLocalized());
            RefreshAutofocusBounds();
        }
        else
        {
            SetFocusMappingValidation(error);
        }

        OnPropertyChanged(nameof(CanStartManualFocusAction));
        OnPropertyChanged(nameof(CanRunAutoFocusAction));
        OnPropertyChanged(nameof(CanSaveFocusMappingAction));
        OnPropertyChanged(nameof(CanTestFocusMappingAction));
        UpdateMotorSemanticProjection();
    }

    private string[] GetEffectiveDeviceChannelRoles()
        => _deviceSettings.Settings.Normalize().ChannelRoles.ToArray();

    internal string GetBoundLedName(string channelRole)
    {
        var roles = GetEffectiveDeviceChannelRoles();
        for (var index = 0; index < roles.Length; index++)
        {
            if (string.Equals(roles[index], channelRole, StringComparison.OrdinalIgnoreCase))
                return $"LED{index + 1}";
        }

        return "ScanDebug_ChannelLedBindingUnassigned".GetLocalized();
    }

    internal string GetIlluminationLevelInput(int ledIndex)
        => ledIndex switch
        {
            0 => Led1Level,
            1 => Led2Level,
            2 => Led3Level,
            3 => Led4Level,
            _ => throw new ArgumentOutOfRangeException(nameof(ledIndex))
        };

    internal bool SetIlluminationLevelInput(int ledIndex, string value)
    {
        if (string.Equals(GetIlluminationLevelInput(ledIndex), value, StringComparison.Ordinal))
            return false;

        switch (ledIndex)
        {
            case 0:
                Led1Level = value;
                break;
            case 1:
                Led2Level = value;
                break;
            case 2:
                Led3Level = value;
                break;
            case 3:
                Led4Level = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ledIndex));
        }

        NotifyCurrentCalibrationIlluminationInputChanged(ledIndex);
        return true;
    }

    internal string GetIlluminationPulseClockInput(int ledIndex)
        => ledIndex switch
        {
            0 => Led1PulseClock,
            1 => Led2PulseClock,
            2 => Led3PulseClock,
            3 => Led4PulseClock,
            _ => throw new ArgumentOutOfRangeException(nameof(ledIndex))
        };

    internal bool SetIlluminationPulseClockInput(int ledIndex, string value)
    {
        if (string.Equals(GetIlluminationPulseClockInput(ledIndex), value, StringComparison.Ordinal))
            return false;

        switch (ledIndex)
        {
            case 0:
                Led1PulseClock = value;
                break;
            case 1:
                Led2PulseClock = value;
                break;
            case 2:
                Led3PulseClock = value;
                break;
            case 3:
                Led4PulseClock = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ledIndex));
        }

        NotifyCurrentCalibrationIlluminationInputChanged(ledIndex);
        return true;
    }

    internal bool GetIlluminationSteadyInput(int ledIndex)
        => ledIndex switch
        {
            0 => IsLed1SteadyEnabled,
            1 => IsLed2SteadyEnabled,
            2 => IsLed3SteadyEnabled,
            3 => IsLed4SteadyEnabled,
            _ => throw new ArgumentOutOfRangeException(nameof(ledIndex))
        };

    internal bool SetIlluminationSteadyInput(int ledIndex, bool value)
    {
        if (GetIlluminationSteadyInput(ledIndex) == value)
            return false;

        switch (ledIndex)
        {
            case 0:
                IsLed1SteadyEnabled = value;
                break;
            case 1:
                IsLed2SteadyEnabled = value;
                break;
            case 2:
                IsLed3SteadyEnabled = value;
                break;
            case 3:
                IsLed4SteadyEnabled = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ledIndex));
        }

        if (value)
            SetIlluminationSyncInput(ledIndex, false);

        NotifyCurrentCalibrationIlluminationInputChanged(ledIndex);
        return true;
    }

    internal bool GetIlluminationSyncInput(int ledIndex)
        => ledIndex switch
        {
            0 => IsLed1SyncEnabled,
            1 => IsLed2SyncEnabled,
            2 => IsLed3SyncEnabled,
            3 => IsLed4SyncEnabled,
            _ => throw new ArgumentOutOfRangeException(nameof(ledIndex))
        };

    internal bool SetIlluminationSyncInput(int ledIndex, bool value)
    {
        if (GetIlluminationSyncInput(ledIndex) == value)
            return false;

        switch (ledIndex)
        {
            case 0:
                IsLed1SyncEnabled = value;
                break;
            case 1:
                IsLed2SyncEnabled = value;
                break;
            case 2:
                IsLed3SyncEnabled = value;
                break;
            case 3:
                IsLed4SyncEnabled = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ledIndex));
        }

        if (value)
            SetIlluminationSteadyInput(ledIndex, false);

        NotifyCurrentCalibrationIlluminationInputChanged(ledIndex);
        return true;
    }

    internal string GetIlluminationWorkModeInput(int ledIndex)
    {
        if (GetIlluminationSyncInput(ledIndex))
            return GetIlluminationWorkModeDisplayName(IlluminationWorkModeSync);
        if (GetIlluminationSteadyInput(ledIndex))
            return GetIlluminationWorkModeDisplayName(IlluminationWorkModeSteady);
        return GetIlluminationWorkModeDisplayName(IlluminationWorkModeOff);
    }

    internal bool SetIlluminationWorkModeInput(int ledIndex, string value)
    {
        var mode = ParseIlluminationWorkModeDisplayName(value);
        var steady = mode == IlluminationWorkModeSteady;
        var sync = mode == IlluminationWorkModeSync;
        var changed = false;
        changed |= SetIlluminationSteadyInput(ledIndex, steady);
        changed |= SetIlluminationSyncInput(ledIndex, sync);
        return changed;
    }

    private static string GetIlluminationWorkModeDisplayName(string mode)
        => mode switch
        {
            IlluminationWorkModeSteady => "ScanDebug_IlluminationWorkModeSteady".GetLocalized(),
            IlluminationWorkModeSync => "ScanDebug_IlluminationWorkModeSync".GetLocalized(),
            _ => "ScanDebug_IlluminationWorkModeOff".GetLocalized()
        };

    private static string ParseIlluminationWorkModeDisplayName(string value)
    {
        if (string.Equals(value, "ScanDebug_IlluminationWorkModeSteady".GetLocalized(), StringComparison.Ordinal))
            return IlluminationWorkModeSteady;
        if (string.Equals(value, "ScanDebug_IlluminationWorkModeSync".GetLocalized(), StringComparison.Ordinal))
            return IlluminationWorkModeSync;
        return IlluminationWorkModeOff;
    }

    private void RefreshActiveIlluminationChannels()
    {
        var roles = GetEffectiveDeviceChannelRoles();
        ClearUnusedIlluminationInputs(roles);
        RefreshAcquisitionChannels(roles);

        for (var index = ActiveIlluminationChannels.Count - 1; index >= 0; index--)
        {
            if (IsActiveIlluminationRole(roles[ActiveIlluminationChannels[index].LedIndex]))
                continue;

            ActiveIlluminationChannels.RemoveAt(index);
        }

        for (var index = 0; index < ScanDebugConstants.IlluminationChannelCount; index++)
        {
            var role = roles[index];
            if (!IsActiveIlluminationRole(role))
                continue;

            var existing = ActiveIlluminationChannels.FirstOrDefault(channel => channel.LedIndex == index);
            if (existing is null)
            {
                ActiveIlluminationChannels.Add(new ScanDebugIlluminationChannelViewModel(this, index, role));
                continue;
            }

            existing.UpdateRole(role);
        }

        var orderedChannels = ActiveIlluminationChannels.OrderBy(channel => channel.LedIndex).ToArray();
        for (var index = 0; index < orderedChannels.Length; index++)
        {
            if (!ReferenceEquals(ActiveIlluminationChannels[index], orderedChannels[index]))
                ActiveIlluminationChannels.Move(ActiveIlluminationChannels.IndexOf(orderedChannels[index]), index);
        }

        RefreshActiveIlluminationChannelBindings();
        RefreshCalibrationChannelItems();
        NotifyCurrentCalibrationIlluminationChannelChanged();
    }

    private void NotifyCurrentCalibrationIlluminationChannelChanged()
    {
        OnPropertyChanged(nameof(CurrentCalibrationIlluminationChannel));
        OnPropertyChanged(nameof(HasCurrentCalibrationIlluminationChannel));
        OnPropertyChanged(nameof(CurrentCalibrationChannelReversed));
        NotifyCurrentCalibrationIlluminationInputsChanged();
    }

    private void NotifyCurrentCalibrationChannelReversedChanged(int ledIndex)
    {
        if (CurrentCalibrationIlluminationChannel?.LedIndex == ledIndex)
            OnPropertyChanged(nameof(CurrentCalibrationChannelReversed));
    }

    private void NotifyCurrentCalibrationIlluminationInputChanged(int ledIndex)
    {
        if (CurrentCalibrationIlluminationChannel?.LedIndex == ledIndex)
            NotifyCurrentCalibrationIlluminationInputsChanged();

        SynchronizeFilmProfileDraftFromInputs();
    }

    private void NotifyCurrentCalibrationIlluminationInputsChanged()
    {
        OnPropertyChanged(nameof(CurrentCalibrationIlluminationLevel));
        OnPropertyChanged(nameof(CurrentCalibrationIlluminationPulseClock));
        OnPropertyChanged(nameof(CurrentCalibrationIlluminationWorkMode));
    }

    private void RefreshAcquisitionChannels(IReadOnlyList<string> roles)
    {
        var selectedLedIndexes = AcquisitionChannels
            .Where(channel => channel.IsSelected)
            .Select(channel => channel.LedIndex)
            .ToHashSet();
        var hadExistingChannels = AcquisitionChannels.Count > 0;

        for (var index = AcquisitionChannels.Count - 1; index >= 0; index--)
        {
            if (IsActiveIlluminationRole(roles[AcquisitionChannels[index].LedIndex]))
                continue;

            AcquisitionChannels.RemoveAt(index);
        }

        for (var index = 0; index < ScanDebugConstants.IlluminationChannelCount; index++)
        {
            var role = roles[index];
            if (!IsActiveIlluminationRole(role))
                continue;

            var existing = AcquisitionChannels.FirstOrDefault(channel => channel.LedIndex == index);
            var isSelected = !hadExistingChannels || selectedLedIndexes.Contains(index);
            if (existing is null)
            {
                AcquisitionChannels.Add(new ScanDebugAcquisitionChannelViewModel(this, index, role, isSelected));
                continue;
            }

            existing.UpdateRole(role);
            existing.SetSelectedFromOwner(isSelected);
            existing.RefreshStatus();
        }

        var orderedChannels = AcquisitionChannels.OrderBy(channel => channel.LedIndex).ToArray();
        for (var index = 0; index < orderedChannels.Length; index++)
        {
            if (!ReferenceEquals(AcquisitionChannels[index], orderedChannels[index]))
                AcquisitionChannels.Move(AcquisitionChannels.IndexOf(orderedChannels[index]), index);
        }

        NotifyAcquisitionPlanChanged();
    }

    internal void SetAcquisitionChannelSelection(ScanDebugAcquisitionChannelViewModel channel, bool isSelected)
    {
        NotifyAcquisitionPlanChanged();
        SynchronizeFilmProfileDraftFromInputs();
    }

    internal string GetAcquisitionChannelCalibrationStatusText(string role)
        => _calibrationProfiles.TryGetProfile(role, out _)
            ? "ScanDebug_ProfileChannelOverviewCalibrationSaved".GetLocalized()
            : "ScanDebug_ProfileChannelOverviewCalibrationMissing".GetLocalized();

    internal void RequestCalibrationForAcquisitionChannel(string role)
    {
        SelectedCalibrationChannel = role;
        CalibrationSectionRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool HasSelectedAcquisitionChannels()
        => AcquisitionChannels.Any(channel => channel.IsSelected);

    private int GetSelectedAcquisitionChannelCount()
        => AcquisitionChannels.Count(channel => channel.IsSelected);

    private bool ShouldUseDebugWorkflowScan()
        => IsScanMotorTransportEnabled || GetSelectedAcquisitionChannelCount() > 1;

    private bool IsCaptureModeCompatibleWithSelection()
        => SelectedCaptureMode != ScanDebugCaptureMode.Continuous || GetSelectedAcquisitionChannelCount() == 1;

    private string BuildCaptureModeUnavailableReason()
        => SelectedCaptureMode == ScanDebugCaptureMode.Continuous && GetSelectedAcquisitionChannelCount() != 1
            ? "ScanDebug_Runtime_ErrorContinuousRequiresSingleChannel".GetLocalized()
            : string.Empty;

    private void RefreshAcquisitionChannelEditability()
    {
        foreach (var channel in AcquisitionChannels)
            channel.RefreshSelectionEditability();
    }

    private string? GetSingleSelectedAcquisitionChannelRole()
    {
        var selected = AcquisitionChannels.Where(channel => channel.IsSelected).Take(2).ToArray();
        return selected.Length == 1 ? selected[0].Role : null;
    }

    private ScanDebugAcquisitionChannelViewModel[] GetSelectedAcquisitionChannelsForSummary()
    {
        return AcquisitionChannels
            .Where(channel => channel.IsSelected)
            .OrderBy(channel => channel.LedIndex)
            .ToArray();
    }

    private void RefreshActiveIlluminationChannelBindings()
    {
        foreach (var channel in ActiveIlluminationChannels)
        {
            channel.RefreshInputBindings();
        }
    }

    private void ClearUnusedIlluminationInputs(IReadOnlyList<string> roles)
    {
        var wasSynchronizing = _isSynchronizingFilmProfileWorkspace;
        _isSynchronizingFilmProfileWorkspace = true;
        try
        {
            for (var index = 0; index < ScanDebugConstants.IlluminationChannelCount; index++)
            {
                if (IsActiveIlluminationRole(roles[index]))
                    continue;

                SetIlluminationLevelInput(index, "0");
                SetIlluminationPulseClockInput(index, ScanDebugConstants.IlluminationMinSyncPulseClock.ToString(CultureInfo.InvariantCulture));
                SetIlluminationSteadyInput(index, false);
                SetIlluminationSyncInput(index, false);
            }
        }
        finally
        {
            _isSynchronizingFilmProfileWorkspace = wasSynchronizing;
        }
    }

    private static bool IsActiveIlluminationRole(string role)
        => !string.Equals(role, "Unused", StringComparison.OrdinalIgnoreCase);

    private void OnMotorSpeedInputChanged(byte motorId)
    {
        if (_isApplyingDerivedMotorSpeed)
            return;

        SetMotorSpeedDerivedFromInterval(motorId, false);
        if (TryParseSelectedScanMotor(out var selectedMotorId, out _) && motorId == selectedMotorId)
            SynchronizeFilmProfileDraftFromInputs();
    }

    private void ApplyMotorSpeedFromIntervalNs(byte motorId, uint intervalNs)
    {
        _isApplyingDerivedMotorSpeed = true;
        try
        {
            var speedValue = FormatMotorSpeedStepsPerSecond(intervalNs);
            switch (motorId)
            {
                case 0:
                    Motor1SpeedValue = speedValue;
                    Motor1SpeedUnit = MotorUnitSteps;
                    Motor1IntervalNs = intervalNs.ToString(CultureInfo.InvariantCulture);
                    break;
                case 1:
                    Motor2SpeedValue = speedValue;
                    Motor2SpeedUnit = MotorUnitSteps;
                    Motor2IntervalNs = intervalNs.ToString(CultureInfo.InvariantCulture);
                    break;
                case 2:
                    Motor3SpeedValue = speedValue;
                    Motor3SpeedUnit = MotorUnitSteps;
                    Motor3IntervalNs = intervalNs.ToString(CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(motorId));
            }
        }
        finally
        {
            _isApplyingDerivedMotorSpeed = false;
        }

        SetMotorSpeedDerivedFromInterval(motorId, true);
    }

    private void ClearMotorIntervalInput(byte motorId)
    {
        switch (motorId)
        {
            case 0:
                Motor1IntervalNs = string.Empty;
                break;
            case 1:
                Motor2IntervalNs = string.Empty;
                break;
            case 2:
                Motor3IntervalNs = string.Empty;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(motorId));
        }
    }

    private bool TryGetDerivedMotorIntervalNs(byte motorId, out uint intervalNs)
    {
        intervalNs = 0;
        if (!IsMotorSpeedDerivedFromInterval(motorId))
            return false;

        var intervalText = motorId switch
        {
            0 => Motor1IntervalNs,
            1 => Motor2IntervalNs,
            2 => Motor3IntervalNs,
            _ => throw new ArgumentOutOfRangeException(nameof(motorId))
        };

        return uint.TryParse(intervalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out intervalNs)
            && intervalNs >= ScanDebugConstants.MotionMinIntervalNs;
    }

    private bool IsMotorSpeedDerivedFromInterval(byte motorId)
        => motorId switch
        {
            0 => _isMotor1SpeedDerivedFromInterval,
            1 => _isMotor2SpeedDerivedFromInterval,
            2 => _isMotor3SpeedDerivedFromInterval,
            _ => throw new ArgumentOutOfRangeException(nameof(motorId))
        };

    private void SetMotorSpeedDerivedFromInterval(byte motorId, bool value)
    {
        switch (motorId)
        {
            case 0:
                _isMotor1SpeedDerivedFromInterval = value;
                break;
            case 1:
                _isMotor2SpeedDerivedFromInterval = value;
                break;
            case 2:
                _isMotor3SpeedDerivedFromInterval = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(motorId));
        }
    }

    private bool CanUseManualFocusSurface() =>
        CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.StartManualFocus) &&
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsOutputOperationRunning &&
        !IsApplyingParameters &&
        !IsAutoCalibrating &&
        !IsAutoFocusing &&
        !IsApplyingIllumination &&
        !IsApplyingMotion &&
        !IsUpdatingFocusMapping;

    private bool CanSaveFocusMapping()
        => !IsUpdatingFocusMapping
           && CanExecuteRuntimeCommand(ScanDebugRuntimeCommandKind.UpdateFocusMapping)
           && TryBuildFocusMotorMappingFromInputs(out _, out _);

    private bool CanTestFocusMapping()
        => CanUseManualFocusSurface()
           && TryBuildFocusMotorMappingFromInputs(out _, out _)
           && ScanMotorIntervalText.TryParseMicroseconds(AutofocusMotorIntervalUs, out var intervalNs)
           && intervalNs >= ScanDebugConstants.MotionMinIntervalNs;

    private async Task TestFocusMappingMotorAsync(bool left)
    {
        if (!TryBuildFocusMotorMappingFromInputs(out var mapping, out var error))
        {
            SetFocusMappingValidation(error);
            return;
        }

        var producer = _motionRuntimeState.CaptureProducer();
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.MoveMotor, out var runtimeClaim))
            return;

        if (!_motionRuntimeState.IsCurrent(producer))
        {
            runtimeClaim?.Dispose();
            return;
        }
        var mappingTestCts = CancellationTokenSource.CreateLinkedTokenSource(_session.ConnectionToken, producer.CancellationToken);
        _focusMappingTestCts = mappingTestCts;
        IsManualFocusing = true;
        _activeManualFocusMappingSnapshot = mapping;
        try
        {
            await EnsureDeviceSettingsInitializedAsync();
            if (!_motionRuntimeState.IsCurrent(producer))
                return;
            mappingTestCts.Token.ThrowIfCancellationRequested();
            var settings = _deviceSettings.Settings.Normalize();
            var motorId = left ? mapping.LeftMotorId : mapping.RightMotorId;
            if (!ScanTimingMath.TryConvertMillimetersToMotorSteps(FocusMappingTestMoveMm, settings.GetMotorSettings(motorId), out var steps) || steps == 0)
            {
                SetFocusMappingValidation("ScanDebug_Runtime_ErrorFocusMappingTestMoveTooSmall".GetLocalized());
                return;
            }

            if (!ScanMotorIntervalText.TryParseMicroseconds(AutofocusMotorIntervalUs, out var intervalNs) || intervalNs < ScanDebugConstants.MotionMinIntervalNs)
            {
                SetFocusMappingValidation("ScanDebug_Runtime_ErrorAutofocusIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs)));
                return;
            }

            var motorLabel = left ? "ScanDebug_Runtime_FocusMappingLeftMotorLabel".GetLocalized() : "ScanDebug_Runtime_FocusMappingRightMotorLabel".GetLocalized();
            SetFocusMappingStatus("ScanDebug_Runtime_FocusMappingTestMoveStarted".GetLocalizedFormat(motorLabel, FocusMappingTestMoveMm.ToString("0.###", CultureInfo.InvariantCulture)));
            await _sessionCoordinator.RunConnectedSessionStateAsync(
                ScannerSessionState.Running,
                async (session, token) =>
                {
                    var direction = mapping.ZPositiveDirection;
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        await session.MoveMotorStepsAndWaitForCompletionAsync(motorId, direction, steps, intervalNs, token);
                        token.ThrowIfCancellationRequested();
                        await session.MoveMotorStepsAndWaitForCompletionAsync(motorId, !direction, steps, intervalNs, token);
                    }
                    finally
                    {
                        await TryStopManualFocusMotorAsync(session, motorId);
                    }

                    return true;
                },
                mappingTestCts.Token,
                waitForAvailability: false);
            mappingTestCts.Token.ThrowIfCancellationRequested();
            SetFocusMappingStatus("ScanDebug_Runtime_FocusMappingTestMoveCompleted".GetLocalizedFormat(motorLabel));
            await LoadMotionStateAsync(mappingTestCts.Token);
        }
        catch (OperationCanceledException)
        {
            MarkMotionStateReadRequired();
            SetFocusMappingStatus("ScanDebug_Runtime_FocusMappingTestMoveCanceled".GetLocalized());
        }
        catch (Exception ex)
        {
            MarkMotionStateReadRequired();
            SetFocusMappingValidation("ScanDebug_Runtime_FocusMappingTestMoveFailed".GetLocalizedFormatOrFallback("Focus mapping test failed: {0}", ex.Message));
        }
        finally
        {
            runtimeClaim?.Dispose();
            if (ReferenceEquals(_focusMappingTestCts, mappingTestCts))
                _focusMappingTestCts = null;
            if (ReferenceEquals(_activeManualFocusMappingSnapshot, mapping))
                _activeManualFocusMappingSnapshot = null;
            IsManualFocusing = false;
            mappingTestCts.Dispose();
        }
    }

    private void OnFocusMappingInputChanged()
    {
        if (_isApplyingFocusMappingInputs)
            return;

        if (TryBuildFocusMotorMappingFromInputs(out _, out var error))
            SetFocusMappingStatus("ScanDebug_Runtime_FocusMappingReadyStatus".GetLocalized());
        else
            SetFocusMappingValidation(error);

        RefreshAutofocusBounds();
        NotifyAutofocusAvailabilityChanged();
        OnPropertyChanged(nameof(CanSaveFocusMappingAction));
        OnPropertyChanged(nameof(CanTestFocusMappingAction));
    }

    private void NotifyAutofocusAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanStartManualFocusAction));
        OnPropertyChanged(nameof(CanRunAutoFocusAction));
        AutoFocusCommand.NotifyCanExecuteChanged();
        QuickFocusCommand.NotifyCanExecuteChanged();
        FineFocusCommand.NotifyCanExecuteChanged();
    }

    private void ApplyFocusMappingInputs(ScanFocusMotorMapping mapping)
    {
        _isApplyingFocusMappingInputs = true;
        try
        {
            FocusMappingLeftMotor = FormatMotorOption(mapping.LeftMotorId);
            FocusMappingRightMotor = FormatMotorOption(mapping.RightMotorId);
            FocusMappingZPositiveDirection = FormatMotorDirectionOption(mapping.ZPositiveDirection);
            FocusMappingTiltPositiveDirection = FormatMotorDirectionOption(mapping.TiltPositiveDirection);
        }
        finally
        {
            _isApplyingFocusMappingInputs = false;
        }
    }

    private bool TryBuildFocusMotorMappingFromInputs(out ScanFocusMotorMapping mapping, out string error)
    {
        mapping = new ScanFocusMotorMapping();
        if (!TryParseMotorOption(FocusMappingLeftMotor, out var leftMotorId)
            || !TryParseMotorOption(FocusMappingRightMotor, out var rightMotorId))
        {
            error = "ScanDebug_Runtime_ErrorFocusMappingMotorSelection".GetLocalizedFormat(ScanDebugConstants.MotionMotorCount);
            return false;
        }

        mapping = new ScanFocusMotorMapping(
            leftMotorId,
            rightMotorId,
            ParseMotorDirectionOption(FocusMappingZPositiveDirection),
            ParseMotorDirectionOption(FocusMappingTiltPositiveDirection));

        var validation = mapping.Validate();
        if (!validation.IsValid)
        {
            error = LocalizeFocusMappingValidation(validation);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryGetValidatedFocusMotorMapping(ScanDeviceSettings settings, out ScanFocusMotorMapping mapping, out string error)
    {
        mapping = settings.Normalize().FocusMotorMapping ?? new ScanFocusMotorMapping();
        var validation = mapping.Validate();
        if (!validation.IsValid)
        {
            error = LocalizeFocusMappingValidation(validation);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string LocalizeFocusMappingValidation(ScanFocusMotorMappingValidationResult validation)
        => validation.Error switch
        {
            ScanFocusMotorMappingValidationError.LeftMotorIdOutOfRange => "ScanDebug_Runtime_ErrorFocusMappingLeftMotorRange".GetLocalizedFormat(ScanDebugConstants.MotionMotorCount),
            ScanFocusMotorMappingValidationError.RightMotorIdOutOfRange => "ScanDebug_Runtime_ErrorFocusMappingRightMotorRange".GetLocalizedFormat(ScanDebugConstants.MotionMotorCount),
            ScanFocusMotorMappingValidationError.DuplicateMotorIds => "ScanDebug_Runtime_ErrorFocusMappingDuplicateMotors".GetLocalized(),
            _ => validation.Message
        };

    private static string FormatMotorOption(byte motorId)
        => motorId < ScanMotorLabels.Length ? ScanMotorLabels[motorId] : ScanMotorLabels[0];

    private static bool TryParseMotorOption(string? motorText, out byte motorId)
    {
        motorId = 0;
        if (string.IsNullOrWhiteSpace(motorText))
            return false;

        for (var index = 0; index < ScanMotorLabels.Length; index++)
        {
            if (string.Equals(motorText, ScanMotorLabels[index], StringComparison.Ordinal))
            {
                motorId = (byte)index;
                return true;
            }
        }

        return false;
    }

    private static string FormatMotorDirectionOption(bool direction)
        => direction ? MotorDirectionLabels[1] : MotorDirectionLabels[0];

    private static bool ParseMotorDirectionOption(string? directionText)
        => string.Equals(directionText, MotorDirectionLabels[1], StringComparison.Ordinal);

    private void SetFocusMappingStatus(string status)
    {
        FocusMappingStatusText = status;
        FocusMappingValidationVisibility = Visibility.Collapsed;
    }

    private void SetFocusMappingValidation(string error)
    {
        FocusMappingStatusText = error;
        FocusMappingValidationVisibility = Visibility.Visible;
    }

    private void ApplyAutofocusPresetToInputs(ScanAutofocusPresetDefinition definition)
    {
        AutofocusSampleRows = definition.SampleRows.ToString(CultureInfo.InvariantCulture);
        AutofocusTiltProbeSteps = definition.TiltProbeMillimeters.ToString("0.###", CultureInfo.InvariantCulture);
        AutofocusZProbeSteps = definition.ZProbeMillimeters.ToString("0.###", CultureInfo.InvariantCulture);
        AutofocusMotorIntervalUs = FormatMotorIntervalInput(definition.MotorIntervalNs);
        AutofocusMaxTiltIterations = definition.MaxTiltIterations.ToString(CultureInfo.InvariantCulture);
        AutofocusMaxZIterations = definition.MaxZIterations.ToString(CultureInfo.InvariantCulture);
    }

    private void RefreshAutofocusBounds()
    {
        if (!TryBuildAutofocusDefinitionFromInputs(out var definition, out var error))
        {
            AutofocusBoundsText = error;
            return;
        }

        if (!TryBuildFocusMotorMappingFromInputs(out var mapping, out error))
        {
            AutofocusBoundsText = error;
            return;
        }

        var settings = _deviceSettings.Settings.Normalize();
        var mechanics = settings.GetMotorSettings(mapping.LeftMotorId);
        try
        {
            var resolved = ScanAutofocusPresetResolver.Resolve(definition, mechanics);
            AutofocusBoundsText = "ScanDebug_Runtime_AutofocusBounds".GetLocalizedFormat(
                resolved.TiltProbeSteps,
                resolved.ZProbeSteps,
                resolved.Bounds.MaxTiltSteps,
                resolved.Bounds.MaxZSteps,
                resolved.Bounds.CaptureCount,
                resolved.Bounds.TotalMovementMillimeters.ToString("0.###", CultureInfo.InvariantCulture),
                TimeSpan.FromMilliseconds(resolved.Bounds.EstimatedDurationMs).ToString(@"m\:ss", CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            AutofocusBoundsText = ex.Message;
        }
    }

    private bool TryBuildAutofocusDefinitionFromInputs(out ScanAutofocusPresetDefinition definition, out string error)
    {
        definition = new ScanAutofocusPresetDefinition(ScanAutofocusPresetKind.Custom, 0, 0, 0, 0, false, false, 0, 0);
        if (!int.TryParse(AutofocusSampleRows, out var sampleRows) || sampleRows <= 0 || sampleRows > _session.SingleTransferMaxRows)
        {
            error = "ScanDebug_Runtime_ErrorAutofocusRowsRange".GetLocalizedFormat(_session.SingleTransferMaxRows);
            return false;
        }

        if (!double.TryParse(AutofocusTiltProbeSteps, NumberStyles.Float, CultureInfo.InvariantCulture, out var tiltProbeMm)
            || !double.IsFinite(tiltProbeMm)
            || tiltProbeMm < AutofocusDistanceMinMm)
        {
            error = "ScanDebug_Runtime_ErrorAutofocusTiltPositive".GetLocalized();
            return false;
        }

        if (!double.TryParse(AutofocusZProbeSteps, NumberStyles.Float, CultureInfo.InvariantCulture, out var zProbeMm)
            || !double.IsFinite(zProbeMm)
            || zProbeMm < AutofocusDistanceMinMm)
        {
            error = "ScanDebug_Runtime_ErrorAutofocusZPositive".GetLocalized();
            return false;
        }

        if (!ScanMotorIntervalText.TryParseMicroseconds(AutofocusMotorIntervalUs, out var intervalNs) || intervalNs < ScanDebugConstants.MotionMinIntervalNs)
        {
            error = "ScanDebug_Runtime_ErrorAutofocusIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
            return false;
        }

        if (!int.TryParse(AutofocusMaxTiltIterations, out var maxTiltIterations) || maxTiltIterations <= 0
            || !int.TryParse(AutofocusMaxZIterations, out var maxZIterations) || maxZIterations <= 0)
        {
            error = "ScanDebug_Runtime_ErrorAutofocusIterationsPositive".GetLocalized();
            return false;
        }

        definition = new ScanAutofocusPresetDefinition(
            SelectedAutofocusPreset,
            sampleRows,
            tiltProbeMm,
            zProbeMm,
            intervalNs,
            false,
            false,
            maxTiltIterations,
            maxZIterations);
        error = string.Empty;
        return true;
    }

    private bool TryBuildManualFocusRequest(bool positive, out ManualFocusRequest request, out string error)
    {
        request = default;

        if (!int.TryParse(AutofocusSampleRows, out var sampleRows) || sampleRows <= 0 || sampleRows > _session.SingleTransferMaxRows)
        {
            error = "ScanDebug_Runtime_ErrorAutofocusRowsRange".GetLocalizedFormat(_session.SingleTransferMaxRows);
            return false;
        }

        var settingsSnapshot = _deviceSettings.Settings.Normalize();
        if (!TryGetValidatedFocusMotorMapping(settingsSnapshot, out var mapping, out error))
            return false;

        if (!double.TryParse(ManualFocusDistanceMm, NumberStyles.Float, CultureInfo.InvariantCulture, out var distanceMm)
            || !double.IsFinite(distanceMm)
            || distanceMm <= 0.0)
        {
            error = "ScanDebug_Runtime_ErrorManualFocusDistancePositive".GetLocalized();
            return false;
        }

        if (distanceMm > ManualFocusMaximumDistanceMm)
        {
            error = "ScanDebug_Runtime_ErrorManualFocusDistanceMaximum".GetLocalizedFormat(ManualFocusMaximumDistanceMm.ToString("0.0", CultureInfo.InvariantCulture));
            return false;
        }

        if (!ScanTimingMath.TryConvertMillimetersToMotorSteps(distanceMm, settingsSnapshot.GetMotorSettings(mapping.LeftMotorId), out var leftSteps)
            || !ScanTimingMath.TryConvertMillimetersToMotorSteps(distanceMm, settingsSnapshot.GetMotorSettings(mapping.RightMotorId), out var rightSteps)
            || leftSteps is 0 or > ManualFocusMaximumStepsPerMappedMotor
            || rightSteps is 0 or > ManualFocusMaximumStepsPerMappedMotor)
        {
            error = "ScanDebug_Runtime_ErrorManualFocusStepRange".GetLocalizedFormat(ManualFocusMaximumStepsPerMappedMotor);
            return false;
        }

        if (!ScanMotorIntervalText.TryParseMicroseconds(AutofocusMotorIntervalUs, out var intervalNs) || intervalNs < ScanDebugConstants.MotionMinIntervalNs)
        {
            error = "ScanDebug_Runtime_ErrorAutofocusIntervalMinimum".GetLocalizedFormat(ScanMotorIntervalText.MinimumWholeMicroseconds(ScanDebugConstants.MotionMinIntervalNs));
            return false;
        }

        var estimatedOneWayTravelDurationMs = (double)Math.Max(leftSteps, rightSteps) * intervalNs / 1_000_000.0;
        if (!double.IsFinite(estimatedOneWayTravelDurationMs)
            || estimatedOneWayTravelDurationMs > ManualFocusMaximumEstimatedOneWayTravelDurationMs)
        {
            error = "ScanDebug_Runtime_ErrorManualFocusDurationMaximum".GetLocalizedFormat(ManualFocusMaximumEstimatedOneWayTravelDurationMs);
            return false;
        }

        var direction = positive ? mapping.ZPositiveDirection : !mapping.ZPositiveDirection;
        var directionLabel = positive ? "Z+" : "Z-";
        request = new ManualFocusRequest(
            sampleRows,
            mapping.LeftMotorId,
            mapping.RightMotorId,
            leftSteps,
            rightSteps,
            intervalNs,
            direction,
            directionLabel,
            distanceMm.ToString("0.###", CultureInfo.InvariantCulture),
            mapping);
        error = string.Empty;
        return true;
    }

    private bool TryBuildAutofocusRequest(out ScanAutofocusRequest request, out string error)
    {
        request = new ScanAutofocusRequest(0, 0, 0, 0, 0, 0, ScanCalibrationRoiSettings.CreateDefault(), new ScanFocusMotorMapping());

        var settings = _deviceSettings.Settings.Normalize();
        if (!TryBuildAutofocusDefinitionFromInputs(out var definition, out error))
            return false;

        if (!TryBuildFocusMotorMappingFromInputs(out var mapping, out error))
            return false;

        ScanAutofocusResolvedOptions resolved;
        try
        {
            var leftMechanics = settings.GetMotorSettings(mapping.LeftMotorId);
            var rightMechanics = settings.GetMotorSettings(mapping.RightMotorId);
            if (leftMechanics != rightMechanics)
            {
                error = "ScanDebug_Runtime_ErrorAutofocusMappedMechanicsMismatch".GetLocalized();
                return false;
            }

            resolved = ScanAutofocusPresetResolver.Resolve(definition, leftMechanics);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        request = new ScanAutofocusRequest(
            definition.SampleRows,
            resolved.TiltProbeSteps,
            resolved.ZProbeSteps,
            definition.MotorIntervalNs,
            definition.MaxTiltIterations,
            definition.MaxZIterations,
            _roiSettings,
            mapping);
        error = string.Empty;
        return true;
    }

    private static string BuildAutofocusSummary(ScanAutofocusResult result)
        => "ScanDebug_Runtime_AutofocusSummary".GetLocalizedFormat(result.SampleRows, result.FinalTiltOffsetSteps.ToString("+#;-#;0"), result.FinalZOffsetSteps.ToString("+#;-#;0"), result.FinalOverallSharpness.ToString("0.0000"), result.FinalLeftSharpness.ToString("0.0000"), result.FinalRightSharpness.ToString("0.0000"), result.FinalTiltImbalance.ToString("+0.0000;-0.0000;0.0000"));

    private static bool TryParseLedLevel(string text, string fieldName, out ushort value, out string error)
    {
        if (!ushort.TryParse(text, out value))
        {
            error = "Shared_Runtime_ErrorIntegerRange0To65535".GetLocalizedFormat(fieldName);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryParsePulseClock(string text, string fieldName, out uint value, out string error)
    {
        if (!uint.TryParse(text, out value))
        {
            error = "ScanDebug_Runtime_ErrorNonNegativeInteger".GetLocalizedFormat(fieldName);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateSyncPulse(byte syncMask, byte channelBit, uint pulseClock, string fieldName, out string error)
    {
        if ((syncMask & channelBit) != 0 && pulseClock < ScanDebugConstants.IlluminationMinSyncPulseClock)
        {
            error = "ScanDebug_Runtime_ErrorSyncPulseMinimum".GetLocalizedFormat(fieldName, ScanDebugConstants.IlluminationMinSyncPulseClock);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static byte BuildMask(bool bit0, bool bit1, bool bit2, bool bit3)
        => (byte)((bit0 ? 0x01 : 0x00) |
                  (bit1 ? 0x02 : 0x00) |
                  (bit2 ? 0x04 : 0x00) |
                  (bit3 ? 0x08 : 0x00));

    private static string BuildIlluminationSummary(ScanIlluminationState state)
        => "ScanDebug_Runtime_IlluminationSummary".GetLocalizedFormat(FormatMask(state.SteadyMask), FormatMask(state.SyncMask), FormatMask(state.SyncActiveMask));

    private static string BuildMotorStatusText(ScanMotorState? state)
    {
        if (state is null)
            return "ScanDebug_Runtime_MotorStatusUnavailable".GetLocalized();

        return "ScanDebug_Runtime_MotorStatus".GetLocalizedFormat(FormatBool(state.Enabled), FormatBool(state.Running), FormatDirection(state.Direction), state.Diag != 0 ? "ScanDebug_Runtime_DiagHigh".GetLocalized() : "ScanDebug_Runtime_DiagLow".GetLocalized(), FormatMotorIntervalStatus(state.IntervalNs), state.RemainingSteps);
    }

    private static string FormatMotorIntervalStatus(uint intervalNs)
        => (intervalNs / 1_000m).ToString("0.###", CultureInfo.InvariantCulture);

    private static string BuildMotionSummary(IReadOnlyList<ScanMotorState?> states)
    {
        var parts = new List<string>(ScanDebugConstants.MotionMotorCount);
        for (var index = 0; index < ScanDebugConstants.MotionMotorCount; index++)
        {
            var state = states[index];
            parts.Add(state is null
                ? "ScanDebug_Runtime_MotionSummaryItemUnavailable".GetLocalizedFormat(index + 1)
                : "ScanDebug_Runtime_MotionSummaryItem".GetLocalizedFormat(index + 1, state.Enabled ? (state.Running ? "ScanDebug_Runtime_MotionStateRunning".GetLocalized() : "ScanDebug_Runtime_MotionStateEnabled".GetLocalized()) : "ScanDebug_Runtime_MotionStateDisabled".GetLocalized()));
        }

        return "ScanDebug_Runtime_MotionSummary".GetLocalizedFormat(string.Join(", ", parts));
    }

    private static string FormatBool(bool value)
        => value ? "ScanDebug_Runtime_Yes".GetLocalized() : "ScanDebug_Runtime_No".GetLocalized();

    private static string FormatDirection(bool direction)
        => ScanRuntimeMessageLocalizer.GetLocalizedDirection(direction);

    private static string FormatMask(byte mask)
    {
        if ((mask & ScanDebugConstants.IlluminationValidMask) == 0)
            return "ScanDebug_Runtime_None".GetLocalized();

        var labels = new List<string>(ScanDebugConstants.IlluminationChannelCount);
        for (var index = 0; index < ScanDebugConstants.IlluminationChannelCount; index++)
        {
            if (((mask >> index) & 0x01) != 0)
                labels.Add(IlluminationChannelLabels[index]);
        }

        return string.Join(", ", labels);
    }

    private void SetDngAlignmentWarning(string message)
    {
        DngAlignmentWarningMessage = message;
        DngAlignmentWarningVisibility = Visibility.Visible;
    }

    private void ClearDngAlignmentWarning()
    {
        DngAlignmentWarningMessage = string.Empty;
        DngAlignmentWarningVisibility = Visibility.Collapsed;
    }

    private static string FormatDngAffectedChannelRoles(ScanDngExportResult exportResult)
        => exportResult.AffectedChannelRoles.Count == 0
            ? "Scan_Runtime_DngAlignmentWarningUnknownChannels".GetLocalized()
            : string.Join(", ", exportResult.AffectedChannelRoles.Select(GetCalibrationChannelDisplayName));

    internal static string GetCalibrationChannelDisplayName(string channelRole)
        => channelRole switch
        {
            "Red" => "Scan_Runtime_ChannelRoleRed".GetLocalized(),
            "Green" => "Scan_Runtime_ChannelRoleGreen".GetLocalized(),
            "Blue" => "Scan_Runtime_ChannelRoleBlue".GetLocalized(),
            "White" => "Scan_Runtime_ChannelRoleWhite".GetLocalized(),
            "IR" => "Scan_Runtime_ChannelRoleIr".GetLocalized(),
            _ => channelRole
        };

    private static string GetRoiSelectionDisplayName(string roiSelection)
        => roiSelection switch
        {
            RoiSelectionBwActive => "ScanDebug_Runtime_RoiSelectionBwActive".GetLocalized(),
            RoiSelectionBwShield => "ScanDebug_Runtime_RoiSelectionBwShield".GetLocalized(),
            RoiSelectionFocusOverall => "ScanDebug_Runtime_RoiSelectionFocusOverall".GetLocalized(),
            RoiSelectionFocusLeft => "ScanDebug_Runtime_RoiSelectionFocusLeft".GetLocalized(),
            RoiSelectionFocusRight => "ScanDebug_Runtime_RoiSelectionFocusRight".GetLocalized(),
            _ => roiSelection
        };

    private static string GetLimitLabelDisplayName(string label)
        => label switch
        {
            "ADC1 offset" => "ScanDebug_Runtime_LimitLabelAdc1Offset".GetLocalized(),
            "ADC2 offset" => "ScanDebug_Runtime_LimitLabelAdc2Offset".GetLocalized(),
            "ADC1 gain" => "ScanDebug_Runtime_LimitLabelAdc1Gain".GetLocalized(),
            "ADC2 gain" => "ScanDebug_Runtime_LimitLabelAdc2Gain".GetLocalized(),
            "Sample rows" => "ScanDebug_Runtime_LimitLabelSampleRows".GetLocalized(),
            "Tilt probe distance" => "ScanDebug_Runtime_LimitLabelTiltProbeDistance".GetLocalized(),
            "Z probe distance" => "ScanDebug_Runtime_LimitLabelZProbeDistance".GetLocalized(),
            "Manual focus distance" => "ScanDebug_Runtime_LimitLabelManualFocusDistance".GetLocalized(),
            "Tilt probe steps" => "ScanDebug_Runtime_LimitLabelTiltProbeSteps".GetLocalized(),
            "Z probe steps" => "ScanDebug_Runtime_LimitLabelZProbeSteps".GetLocalized(),
            "Motor interval" => "ScanDebug_Runtime_LimitLabelMotorInterval".GetLocalized(),
            _ => label
        };

    private sealed record IlluminationRequest(
        ushort Led1Level,
        ushort Led2Level,
        ushort Led3Level,
        ushort Led4Level,
        byte SteadyMask,
        byte SyncMask,
        uint Led1PulseClock,
        uint Led2PulseClock,
        uint Led3PulseClock,
        uint Led4PulseClock);

    private enum SessionIlluminationTestKind
    {
        Off,
        Steady,
        AcquisitionSync
    }

    private sealed record MotorMoveRequest(bool Direction, uint Steps, uint IntervalNs);

    private readonly record struct ManualFocusRequest(
        int SampleRows,
        byte LeftMotorId,
        byte RightMotorId,
        uint LeftSteps,
        uint RightSteps,
        uint IntervalNs,
        bool ZDirection,
        string DirectionLabel,
        string DistanceText,
        ScanFocusMotorMapping FocusMotorMapping);

    private bool RenderPreview(int rows)
    {
        if (_streamingWorkflowPreviewResult is not null && _streamingWorkflowPreviewCompletedRowsByPassIndex is not null)
        {
            var assignment = _streamingWorkflowPreviewAssignment ?? BuildDebugChannelAssignment();
            var singleActiveIndex = GetSingleActiveRoleIndex(assignment);
            if (singleActiveIndex >= 0)
            {
                if (singleActiveIndex >= _streamingWorkflowPreviewResult.Passes.Count)
                {
                    StatusText = "ScanDebug_Runtime_StatusWorkflowPreviewChannelUnavailable".GetLocalized();
                    return false;
                }

                _lineBuffer = _streamingWorkflowPreviewResult.Passes[singleActiveIndex].ImageBytes;
            }
            else
            {
                if (!_channelImages.TryBuildPartialRgbComposite(_streamingWorkflowPreviewResult, assignment, BuildDebugColorManagementOptions(), _streamingWorkflowPreviewCompletedRowsByPassIndex, null, out var streamingCompositeFrame, out var streamingCompositeError, _calibrationProfiles.Snapshot.Profiles, IsWhiteLevelPreviewEnabled) || streamingCompositeFrame is null)
                {
                    StatusText = streamingCompositeError;
                    return false;
                }

                PreviewFrame = CreatePreviewFrame(streamingCompositeFrame.Buffer);
                OnPropertyChanged(nameof(CanEditRoiSelection));
                OnPropertyChanged(nameof(CanEditColumnSampleSelection));
                RefreshRoiStatus();
                RefreshColumnSampleStatus();
                return true;
            }
        }

        if (_lastWorkflowResult is not null)
        {
            var assignment = _lastWorkflowChannelAssignment ?? BuildCapturedWorkflowChannelAssignment(_lastWorkflowResult);
            if (GetSingleActiveRoleIndex(assignment) < 0)
            {
                if (HasRgbRoles(assignment))
                {
                    if (!_channelImages.TryBuildRgbComposite(_lastWorkflowResult, assignment, BuildDebugColorManagementOptions(), ScanChannelAlignmentMode.Ecc, null, out var compositeFrame, out var compositeError, _calibrationProfiles.Snapshot.Profiles, IsWhiteLevelPreviewEnabled) || compositeFrame is null)
                    {
                        StatusText = compositeError;
                        return false;
                    }

                    PreviewFrame = CreatePreviewFrame(compositeFrame.Buffer);
                }
                else
                {
                    var completedRowsByPassIndex = Enumerable.Range(0, _lastWorkflowResult.Passes.Count)
                        .ToDictionary(index => index, _ => _lastWorkflowResult.Rows);
                    if (!_channelImages.TryBuildPartialRgbComposite(_lastWorkflowResult, assignment, BuildDebugColorManagementOptions(), completedRowsByPassIndex, null, out var partialCompositeFrame, out var partialCompositeError, _calibrationProfiles.Snapshot.Profiles, IsWhiteLevelPreviewEnabled) || partialCompositeFrame is null)
                    {
                        StatusText = partialCompositeError;
                        return false;
                    }

                    PreviewFrame = CreatePreviewFrame(partialCompositeFrame.Buffer);
                }

                OnPropertyChanged(nameof(CanEditRoiSelection));
                OnPropertyChanged(nameof(CanEditColumnSampleSelection));
                RefreshRoiStatus();
                RefreshColumnSampleStatus();
                return true;
            }
        }

        var gamma = 1.0;
        if (IsGammaCorrectionEnabled && !TryParsePreviewGamma(out gamma))
            gamma = double.NaN;

        var whiteLevel = (ushort)0;
        var whiteLevelEnabled = IsWhiteLevelPreviewEnabled && TryGetSelectedPreviewWhiteLevel(out whiteLevel);

        if (!_previewPresenter.TryRender(
                _lineBuffer,
                rows,
                new ScanPreviewRenderOptions(IsWaterfallEnabled, IsWaterfallCompressedEnabled, IsGammaCorrectionEnabled, gamma, whiteLevelEnabled, whiteLevelEnabled ? whiteLevel : (ushort)0),
                PreviewFrame,
                out var previewFrame,
                out var error))
        {
            StatusText = error;
            return false;
        }

        PreviewFrame = previewFrame;
        OnPropertyChanged(nameof(CanEditRoiSelection));
        OnPropertyChanged(nameof(CanEditColumnSampleSelection));
        RefreshRoiStatus();
        RefreshColumnSampleStatus();
        return true;
    }

    private static byte[][] CreateWorkflowPreviewPassBuffers(int rows, int passCount)
    {
        var bufferLength = ScanDebugConstants.BytesPerLine * rows;
        var buffers = new byte[passCount][];
        for (var index = 0; index < passCount; index++)
            buffers[index] = new byte[bufferLength];

        return buffers;
    }

    private static ScanWorkflowResult BuildStreamingWorkflowPreviewResult(
        ScanWorkflowRequest request,
        byte[][] passBuffers,
        int[] completedRows,
        bool[] passDirections,
        uint[] passMotorSteps,
        uint[] passMotorIntervals)
    {
        var firstCapturedPassIndex = Array.FindIndex(completedRows, rows => rows > 0);
        var legacyMotorSteps = firstCapturedPassIndex >= 0 ? passMotorSteps[firstCapturedPassIndex] : 0u;
        var legacyMotorInterval = firstCapturedPassIndex >= 0 ? passMotorIntervals[firstCapturedPassIndex] : 0u;
        return new ScanWorkflowResult(
            request.Rows,
            Enumerable.Range(0, request.PassChannelRoles.Length)
                .Select(passIndex => new ScanPassCapture(
                    passIndex + 1,
                    (byte)passIndex,
                    passDirections[passIndex],
                    request.Rows,
                    passMotorSteps[passIndex],
                    passBuffers[passIndex]))
                .ToArray(),
            legacyMotorSteps,
            legacyMotorInterval,
            request.ExposureTicks,
            request.SysClockKhz);
    }

    private bool TryParsePreviewGamma(out double gamma)
        => double.TryParse(PreviewGamma, NumberStyles.Float, CultureInfo.InvariantCulture, out gamma);

    private bool TryGetSelectedPreviewWhiteLevel(out ushort whiteLevel)
    {
        whiteLevel = 0;
        return _calibrationProfiles.TryGetProfile(SelectedCalibrationChannel, out var profile)
            && profile.WhiteLevel is ushort configuredWhiteLevel
            && configuredWhiteLevel > 0
            && (profile.BlackLevel is not ushort blackLevel || configuredWhiteLevel > blackLevel);
    }

    private ScanPreviewFrame CreatePreviewFrame(ScanCompositePixelBuffer buffer)
    {
        unchecked
        {
            return new ScanPreviewFrame(buffer.Pixels, buffer.Width, buffer.Height, buffer.Width * 4, ScanPreviewPixelFormat.Bgra8, ++_previewFrameVersion);
        }
    }

    private void ClearPreview()
    {
        _previewPresenter.Reset();
        PreviewFrame = null;
        OnPropertyChanged(nameof(CanEditRoiSelection));
        OnPropertyChanged(nameof(CanEditColumnSampleSelection));
        RefreshRoiStatus();
        RefreshColumnSampleStatus();
    }

    private void RefreshPreviewSelectionState()
    {
        OnPropertyChanged(nameof(IsPreviewToggleEnabled));
        OnPropertyChanged(nameof(IsPreviewEnabledForCurrentRows));
        OnPropertyChanged(nameof(CanEditRoiSelection));
        OnPropertyChanged(nameof(CanEditColumnSampleSelection));
        EnsureColumnSampleEditModeAvailability();
    }

    private bool IsPreviewForcedOffForSelectedRows()
        => int.TryParse(SelectedRows, out var rows) && IsPreviewForcedOffForRows(rows);

    private static bool IsPreviewForcedOffForRows(int rows)
        => rows > ScanDebugConstants.MaxPreviewRows;

    public async Task RefreshDeviceSettingsBindingsAsync()
    {
        await EnsureDeviceSettingsInitializedAsync();
        await _filmProfileWorkspace.InitializeAsync(CancellationToken.None);
    }

    public async Task DeactivateAsync()
    {
        await Task.CompletedTask;
        DetachRuntimeBindings();
        UnsubscribeFilmProfileWorkspace();
        CancelFilmProfileOperationAutoCloseTimer();
        ClearPreview();
        IsScanReadProgressVisible = false;
    }

    public async Task CleanupAsync()
    {
        await DeactivateAsync();
    }
}
