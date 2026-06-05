using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
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
    private const double AutofocusDistanceMinMm = 0.001;
    private const double DefaultAutofocusTiltProbeMm = 0.5;
    private const double DefaultAutofocusZProbeMm = 1.0;
    private static readonly Brush LimitBlockNormalBrush = GetThemeBrush("SystemFillColorTransparentBrush", Colors.Transparent);
    private static readonly Brush LimitBlockAlertBrush = GetThemeBrush("SystemFillColorCriticalBrush", Colors.IndianRed);
    private static readonly Brush LimitBlockNormalTextBrush = GetThemeBrush("TextFillColorSecondaryBrush", Colors.Gray);
    private static readonly Brush LimitBlockAlertTextBrush = GetThemeBrush("TextOnAccentFillColorPrimaryBrush", Colors.White);

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
    private readonly IScanChannelParameterProfileService _channelProfiles;
    private readonly IDebugOutputMirrorService _debugOutputMirror;
    private readonly IScanDebugSessionCoordinator _sessionCoordinator;
    private readonly IUiDispatcher _dispatcher;

    private CancellationTokenSource? _scanCts;
    private byte[] _lineBuffer = Array.Empty<byte>();
    private ScanWorkflowResult? _lastWorkflowResult;
    private bool _hasValidScanBuffer;
    private DateTime _lastApplyParametersAtUtc = DateTime.MinValue;
    private bool _areRuntimeBindingsAttached;
    private bool _isMultiBufferedBulkInEnabled;
    private bool _suppressWarmUpToggleCommand;
    private bool _isUpdatingRoiInputs;
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
    private ScanFilmAcquisitionSettings? _selectedFilmAcquisitionSettings;
    private ScanCalibrationRoiSettings _roiSettings = ScanCalibrationRoiSettings.CreateDefault();
    private ScanColumnRange _columnSampleRange = new(ScanDebugConstants.EffectivePixelStart, ScanDebugConstants.EffectivePixelEnd);
    private ushort? _columnSampleMean;
    private readonly Task _deviceSettingsInitializationTask;

    public ObservableCollection<string> RowOptions { get; } = new() { "64", "128", "256", "512", "1024", "2048", "4096" };

    public ObservableCollection<ScanDebugIlluminationChannelViewModel> ActiveIlluminationChannels { get; } = new();

    public ObservableCollection<ScanDebugAcquisitionChannelViewModel> AcquisitionChannels { get; } = new();

    public ObservableCollection<string> DirectionOptions { get; } = new(DirectionLabels);

    public ObservableCollection<string> MotorDirectionOptions { get; } = new(MotorDirectionLabels);

    public ObservableCollection<string> MotorOptions { get; } = new(ScanMotorLabels);

    public ObservableCollection<string> MotorUnitOptions { get; } = new(MotorUnitLabels);

    public ObservableCollection<string> MotorDistancePerLineUnitOptions { get; } = new(MotorUnitLabels);

    public ObservableCollection<string> CalibrationChannelOptions { get; } = new() { "Red", "Green", "Blue", "White", "IR" };

    public IReadOnlyList<string> IlluminationWorkModeOptions { get; } = new[]
    {
        "ScanDebug_IlluminationWorkModeOff".GetLocalized(),
        "ScanDebug_IlluminationWorkModeSteady".GetLocalized(),
        "ScanDebug_IlluminationWorkModeSync".GetLocalized()
    };

    public ObservableCollection<ScanDngExportMode> DngExportModeOptions { get; } = new() { ScanDngExportMode.LinearRaw4, ScanDngExportMode.LinearRgbIrw };

    public ObservableCollection<ScanChannelAlignmentMode> AlignmentModeOptions { get; } = new() { ScanChannelAlignmentMode.Ecc, ScanChannelAlignmentMode.MutualInformation, ScanChannelAlignmentMode.EccThenMutualInformation };

    public ObservableCollection<string> RoiSelectionOptions { get; } = new(RoiSelectionLabels);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    public partial string SelectedRows { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    public partial bool IsWarmUpEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsPreviewEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsContinuousScanEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsMultiChannelScanEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsAlternateMotorDirectionEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsScanMotorTransportEnabled { get; set; }

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
    public partial int ColumnSampleOverlayVersion { get; set; }

    [ObservableProperty]
    public partial string SelectedCalibrationChannel { get; set; }

    [ObservableProperty]
    public partial bool IsChannel1Reversed { get; set; }

    [ObservableProperty]
    public partial bool IsChannel2Reversed { get; set; }

    [ObservableProperty]
    public partial bool IsChannel3Reversed { get; set; }

    [ObservableProperty]
    public partial bool IsChannel4Reversed { get; set; }

    [ObservableProperty]
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
    public partial ScanChannelAlignmentMode SelectedProfileAlignmentMode { get; set; }

    [ObservableProperty]
    public partial ScanDngExportMode SelectedProfileDngExportMode { get; set; }

    [ObservableProperty]
    public partial string CalibrationChannelStatusText { get; set; }

    [ObservableProperty]
    public partial string FilmProfileName { get; set; }

    [ObservableProperty]
    public partial bool HasUnsavedProfileChanges { get; set; }

    [ObservableProperty]
    public partial string SelectedRoiSelection { get; set; }

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

    public string DeviceStateText => IsConnecting
        ? "ScanDebug_Runtime_DeviceStateConnecting".GetLocalized()
        : IsConnected
            ? "ScanDebug_Runtime_DeviceStateConnected".GetLocalized()
            : IsDevicesPresent
                ? "ScanDebug_Runtime_DeviceStateDetected".GetLocalized()
                : "ScanDebug_Runtime_DeviceStateWaiting".GetLocalized();

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

    partial void OnPreviewFrameChanged(ScanPreviewFrame? value)
        => NotifyPreviewStatePropertiesChanged();

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
    public partial bool IsAutoCalibrating { get; set; }

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
    public partial bool IsAutoFocusing { get; set; }

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
    public partial string ExposureTicks { get; set; }

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
    public partial string SysClockMhzDisplay { get; set; }

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
    public partial string IlluminationSummaryText { get; set; }

    [ObservableProperty]
    public partial string MotionSummaryText { get; set; }

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
    public partial string Motor1IntervalUs { get; set; }

    [ObservableProperty]
    public partial string Motor2IntervalUs { get; set; }

    [ObservableProperty]
    public partial string Motor3IntervalUs { get; set; }

    [ObservableProperty]
    public partial string AutofocusSampleRows { get; set; }

    [ObservableProperty]
    public partial string AutofocusTiltProbeSteps { get; set; }

    [ObservableProperty]
    public partial string AutofocusZProbeSteps { get; set; }

    [ObservableProperty]
    public partial string AutofocusMotorIntervalUs { get; set; }

    [ObservableProperty]
    public partial string AutofocusZDirection { get; set; }

    [ObservableProperty]
    public partial string AutofocusTiltDirection { get; set; }

    [ObservableProperty]
    public partial string AutofocusSummaryText { get; set; }

    public string Adc1OffsetLimitText => BuildBoundedLimitText(Adc1Offset, CalibrationOffsetMin, CalibrationOffsetMax, "ADC1 offset");

    public string Adc2OffsetLimitText => BuildBoundedLimitText(Adc2Offset, CalibrationOffsetMin, CalibrationOffsetMax, "ADC2 offset");

    public string Adc1GainLimitText => BuildBoundedLimitText(Adc1Gain, CalibrationGainMin, CalibrationGainMax, "ADC1 gain");

    public string Adc2GainLimitText => BuildBoundedLimitText(Adc2Gain, CalibrationGainMin, CalibrationGainMax, "ADC2 gain");

    public string AutofocusSampleRowsLimitText => BuildBoundedLimitText(AutofocusSampleRows, AutofocusRowsMin, _session.SingleTransferMaxRows, "Sample rows");

    public string AutofocusTiltProbeStepsLimitText => BuildPositiveDistanceLimitText(AutofocusTiltProbeSteps, "Tilt probe distance");

    public string AutofocusZProbeStepsLimitText => BuildPositiveDistanceLimitText(AutofocusZProbeSteps, "Z probe distance");

    public string AutofocusMotorIntervalLimitText => BuildLowerBoundLimitText(AutofocusMotorIntervalUs, ScanDebugConstants.MotionMinIntervalUs, "Motor interval");

    public Brush Adc1OffsetLimitBrush => BuildBoundedLimitBrush(Adc1Offset, CalibrationOffsetMin, CalibrationOffsetMax);

    public Brush Adc2OffsetLimitBrush => BuildBoundedLimitBrush(Adc2Offset, CalibrationOffsetMin, CalibrationOffsetMax);

    public Brush Adc1GainLimitBrush => BuildBoundedLimitBrush(Adc1Gain, CalibrationGainMin, CalibrationGainMax);

    public Brush Adc2GainLimitBrush => BuildBoundedLimitBrush(Adc2Gain, CalibrationGainMin, CalibrationGainMax);

    public Brush AutofocusSampleRowsLimitBrush => BuildBoundedLimitBrush(AutofocusSampleRows, AutofocusRowsMin, _session.SingleTransferMaxRows);

    public Brush AutofocusTiltProbeStepsLimitBrush => BuildPositiveDistanceLimitBrush(AutofocusTiltProbeSteps);

    public Brush AutofocusZProbeStepsLimitBrush => BuildPositiveDistanceLimitBrush(AutofocusZProbeSteps);

    public Brush AutofocusMotorIntervalLimitBrush => BuildLowerBoundLimitBrush(AutofocusMotorIntervalUs, ScanDebugConstants.MotionMinIntervalUs);

    public Brush Adc1OffsetLimitTextBrush => BuildBoundedLimitTextBrush(Adc1Offset, CalibrationOffsetMin, CalibrationOffsetMax);

    public Brush Adc2OffsetLimitTextBrush => BuildBoundedLimitTextBrush(Adc2Offset, CalibrationOffsetMin, CalibrationOffsetMax);

    public Brush Adc1GainLimitTextBrush => BuildBoundedLimitTextBrush(Adc1Gain, CalibrationGainMin, CalibrationGainMax);

    public Brush Adc2GainLimitTextBrush => BuildBoundedLimitTextBrush(Adc2Gain, CalibrationGainMin, CalibrationGainMax);

    public Brush AutofocusSampleRowsLimitTextBrush => BuildBoundedLimitTextBrush(AutofocusSampleRows, AutofocusRowsMin, _session.SingleTransferMaxRows);

    public Brush AutofocusTiltProbeStepsLimitTextBrush => BuildPositiveDistanceLimitTextBrush(AutofocusTiltProbeSteps);

    public Brush AutofocusZProbeStepsLimitTextBrush => BuildPositiveDistanceLimitTextBrush(AutofocusZProbeSteps);

    public Brush AutofocusMotorIntervalLimitTextBrush => BuildLowerBoundLimitTextBrush(AutofocusMotorIntervalUs, ScanDebugConstants.MotionMinIntervalUs);

    public event EventHandler<ScanCalibrationPromptRequest>? CalibrationPromptRequested;

    public event EventHandler<ScanNoticeRequest>? NoticeRequested;

    public event EventHandler? CalibrationSectionRequested;

    public ScanDebugViewModel(IScanSessionService session, IScanParameterService parameters, IScanImageDecoder imageDecoder, IScanPreviewPresenter previewPresenter, IScanChannelImageService channelImages, IScanAutoCalibrationService autoCalibration, IScanAutoFocusService autoFocus, IScanIlluminationService illumination, IScanTransferSettingsService transferSettings, IScanWorkflowService workflow, IScanDeviceSettingsService deviceSettings, IScanChannelParameterProfileService channelProfiles, IDebugOutputMirrorService debugOutputMirror, IScanDebugSessionCoordinator sessionCoordinator, IUiDispatcher dispatcher)
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
        _channelProfiles = channelProfiles;
        _debugOutputMirror = debugOutputMirror;
        _sessionCoordinator = sessionCoordinator;
        _dispatcher = dispatcher;
        _deviceSettingsInitializationTask = _deviceSettings.InitializeAsync();
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor dependencies={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        SelectedRows = "128";
        IsPreviewEnabled = true;
        IsAlternateMotorDirectionEnabled = true;
        IsScanMotorTransportEnabled = true;
        IsScanLedAutoControlEnabled = true;
        IsWaterfallCompressedEnabled = true;
        IsGammaCorrectionEnabled = true;
        IsWhiteLevelPreviewEnabled = true;
        PreviewGamma = DefaultPreviewGamma.ToString("0.0");
        SelectedStartingDirection = DirectionOptions[0];
        SelectedScanMotor = MotorOptions[Math.Min(1, MotorOptions.Count - 1)];
        MotorDistancePerLineValue = string.Empty;
        MotorDistancePerLineUnit = MotorUnitMillimeters;
        MotorIntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString(CultureInfo.InvariantCulture);
        ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorUnavailableUntilParametersLoaded".GetLocalized();
        SelectedCalibrationChannel = CalibrationChannelOptions[0];
        IsScanRecipeColorManagementEnabled = true;
        ScanRecipeRedWavelengthNm = "680";
        ScanRecipeGreenWavelengthNm = "525";
        ScanRecipeBlueWavelengthNm = "450";
        ScanRecipeOutputGamma = "2.2";
        SelectedProfileAlignmentMode = AlignmentModeOptions[0];
        SelectedProfileDngExportMode = DngExportModeOptions[0];
        CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_NoSavedProfileLoadedYet".GetLocalizedFormat(GetCalibrationChannelDisplayName(CalibrationChannelOptions[0]));
        FilmProfileName = "ScanDebug_Runtime_FilmProfileUntitled".GetLocalized();
        SelectedRoiSelection = RoiSelectionOptions[0];
        IsBwActiveRoiOverlayVisible = true;
        IsBwShieldRoiOverlayVisible = true;
        IsFocusOverallRoiOverlayVisible = true;
        IsFocusLeftRoiOverlayVisible = true;
        IsFocusRightRoiOverlayVisible = true;
        RoiStatusText = string.Empty;
        RoiStartInput = "0";
        RoiEndInput = "0";
        RoiInputStatusText = "ScanDebug_Runtime_RoiInputsSynchronized".GetLocalized();
        ColumnSampleStatusText = "ScanDebug_Runtime_ColumnSampleUnavailable".GetLocalized();
        StatusText = "ScanDebug_Runtime_StatusWaitingForDevicesShort".GetLocalized();
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
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor defaultProperties={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        RefreshActiveIlluminationChannels();
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor RefreshActiveIlluminationChannels={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        IlluminationSummaryText = "ScanDebug_Runtime_IlluminationSummaryIdle".GetLocalized();
        MotionSummaryText = "ScanDebug_Runtime_MotionSummaryIdle".GetLocalized();
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
        ApplyMotorSpeedFromInterval(0, ScanDebugConstants.MotionDefaultIntervalUs);
        ApplyMotorSpeedFromInterval(1, ScanDebugConstants.MotionDefaultIntervalUs);
        ApplyMotorSpeedFromInterval(2, ScanDebugConstants.MotionDefaultIntervalUs);
        AutofocusSampleRows = "128";
        AutofocusTiltProbeSteps = DefaultAutofocusTiltProbeMm.ToString("0.###", CultureInfo.InvariantCulture);
        AutofocusZProbeSteps = DefaultAutofocusZProbeMm.ToString("0.###", CultureInfo.InvariantCulture);
        AutofocusMotorIntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString();
        AutofocusZDirection = MotorDirectionLabels[0];
        AutofocusTiltDirection = MotorDirectionLabels[0];
        AutofocusSummaryText = "ScanDebug_Runtime_AutofocusIdle".GetLocalized();
        RefreshRoiStatus();
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor hardwareDefaults={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        if (_sessionCoordinator.ConnectedSession is not null)
        {
            _session = _sessionCoordinator.ConnectedSession;
            IsConnected = true;
        }
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor connectedSessionCheck={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        stepStopwatch.Restart();
        AttachRuntimeBindings();
        _session.RefreshTargets();
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
        RefreshPreviewSelectionState();
        RefreshTargets();
        HasUnsavedProfileChanges = false;
        _ = InitializeTransferSettingsAsync();
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor runtimeRefresh={stepStopwatch.Elapsed.TotalMilliseconds:0.0} ms");

        totalStopwatch.Stop();
        NavigationTimingLogger.Write($"ScanDebugViewModel.ctor total={totalStopwatch.Elapsed.TotalMilliseconds:0.0} ms");
    }

    partial void OnMotor1SpeedValueChanged(string value)
        => OnMotorSpeedInputChanged(0);

    partial void OnMotor2SpeedValueChanged(string value)
        => OnMotorSpeedInputChanged(1);

    partial void OnMotor3SpeedValueChanged(string value)
        => OnMotorSpeedInputChanged(2);

    partial void OnMotor1SpeedUnitChanged(string value)
        => OnMotorSpeedInputChanged(0);

    partial void OnMotor2SpeedUnitChanged(string value)
        => OnMotorSpeedInputChanged(1);

    partial void OnMotor3SpeedUnitChanged(string value)
        => OnMotorSpeedInputChanged(2);

    partial void OnExposureTicksChanged(string value)
    {
        UpdateComputedParameterDisplays();
        RefreshDerivedMotorDistanceFromCurrentInterval();
        UpdateComputedMotorSummary();
    }

    partial void OnAdc1OffsetChanged(string value)
    {
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
    }

    partial void OnAdc2OffsetChanged(string value)
    {
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
    }

    partial void OnAdc1GainChanged(string value)
    {
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
    }

    partial void OnAdc2GainChanged(string value)
    {
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
    }

    partial void OnSysClockKhzChanged(string value)
    {
        UpdateComputedParameterDisplays();
        RefreshDerivedMotorDistanceFromCurrentInterval();
        UpdateComputedMotorSummary();
    }

    partial void OnAutofocusSampleRowsChanged(string value)
    {
        RefreshLimitBlockBindings();
    }

    partial void OnAutofocusTiltProbeStepsChanged(string value)
    {
        RefreshLimitBlockBindings();
    }

    partial void OnAutofocusZProbeStepsChanged(string value)
    {
        RefreshLimitBlockBindings();
    }

    partial void OnAutofocusMotorIntervalUsChanged(string value)
    {
        RefreshLimitBlockBindings();
    }

    partial void OnIsWarmUpEnabledChanged(bool value)
    {
        if (_suppressWarmUpToggleCommand)
            return;

        _ = HandleWarmUpToggleChangedAsync(value);
    }

    partial void OnSelectedRowsChanged(string value)
    {
        RefreshPreviewSelectionState();
        NotifyActionAvailabilityChanged();
        UpdateComputedMotorSummary();
    }

    partial void OnIsMultiChannelScanEnabledChanged(bool value)
    {
        MarkProfileDirty();
        NotifyAcquisitionPlanChanged();
    }

    partial void OnFilmProfileNameChanged(string value)
    {
        MarkProfileDirty();
        NotifyProfileStateChanged();
    }

    partial void OnHasUnsavedProfileChangesChanged(bool value)
        => NotifyProfileStateChanged();

    partial void OnMotorDistancePerLineValueChanged(string value)
    {
        if (_isApplyingDerivedMotorDistance)
        {
            UpdateComputedMotorSummary();
            return;
        }

        _isMotorDistanceDerivedFromInterval = false;
        UpdateComputedMotorSummary();
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
            return;
        }

        if (!string.Equals(previousUnit, normalizedUnit, StringComparison.Ordinal)
            && ScanMotorDistanceText.TryParseMillimeters(MotorDistancePerLineValue, previousUnit, GetCurrentScanMotorSettings(), out var lineDistanceMm)
            && ScanMotorDistanceText.TryFormatDisplayValue(lineDistanceMm, normalizedUnit, GetCurrentScanMotorSettings(), out var convertedValue))
        {
            ApplyDerivedMotorDistance(convertedValue);
        }

        UpdateComputedMotorSummary();
    }

    partial void OnMotorIntervalUsChanged(string value)
    {
        if (_isMotorDistanceDerivedFromInterval)
            RefreshDerivedMotorDistanceFromCurrentInterval();

        UpdateComputedMotorSummary();
    }

    partial void OnSelectedScanMotorChanged(string value)
    {
        if (_isMotorDistanceDerivedFromInterval)
            RefreshDerivedMotorDistanceFromCurrentInterval();

        UpdateComputedMotorSummary();
    }

    partial void OnIsWhiteLevelPreviewEnabledChanged(bool value)
    {
        if (_hasValidScanBuffer && _previewRows > 0 && IsPreviewEnabled && !IsPreviewForcedOffForRows(_previewRows))
            RenderPreview(_previewRows);
    }

    partial void OnSelectedCalibrationChannelChanged(string value)
    {
        MarkProfileDirty();
        NotifyAcquisitionPlanChanged();
        OnPropertyChanged(nameof(CurrentCalibrationChannelSummaryText));
        NotifyPreviewStatePropertiesChanged();
        _ = HandleSelectedCalibrationChannelChangedAsync(value);
    }

    partial void OnSelectedRoiSelectionChanged(string value)
    {
        RefreshRoiInputTexts();
        RefreshRoiStatus();
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

    partial void OnRoiStartInputChanged(string value)
    {
        if (!_isUpdatingRoiInputs)
            RoiInputStatusText = "ScanDebug_Runtime_RoiRangeChanged".GetLocalized();
    }

    partial void OnRoiEndInputChanged(string value)
    {
        if (!_isUpdatingRoiInputs)
            RoiInputStatusText = "ScanDebug_Runtime_RoiRangeChanged".GetLocalized();
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));
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
        NotifyDeviceActionAvailabilityChanged();
        NotifyPreviewStatePropertiesChanged();
    }

    partial void OnIsConnectingChanged(bool value)
    {
        OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));
        OnPropertyChanged(nameof(DeviceStateText));
        NotifyDeviceActionAvailabilityChanged();
    }

    partial void OnIsApplyingParametersChanged(bool value)
    {
        OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));
        NotifyActionAvailabilityChanged();
    }

    partial void OnIsAutoCalibratingChanged(bool value)
    {
        OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));
        NotifyActionAvailabilityChanged();
    }

    partial void OnIsAutoFocusingChanged(bool value)
    {
        OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));
        NotifyDeviceActionAvailabilityChanged();
    }

    partial void OnIsApplyingIlluminationChanged(bool value)
    {
        OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));
        NotifyDeviceActionAvailabilityChanged();
    }

    partial void OnIsApplyingMotionChanged(bool value)
    {
        OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));
        NotifyDeviceActionAvailabilityChanged();
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

    public string CurrentProfileNameText => string.IsNullOrWhiteSpace(FilmProfileName)
        ? "ScanDebug_ProfileStateNoProfile".GetLocalized()
        : FilmProfileName;

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

            return IsMultiChannelScanEnabled
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

    public string SaveProfileDisabledReasonText => "ScanDebug_DisabledReasonReady".GetLocalized();

    public bool IsAcquisitionRunning => IsRunning;

    public bool IsPreviewImageAvailable => HasPreviewImage;

    public bool AreScanAcquisitionSettingsEditable =>
        !IsRunning &&
        !IsConnecting &&
        !IsApplyingParameters &&
        !IsAutoCalibrating &&
        !IsAutoFocusing &&
        !IsApplyingIllumination &&
        !IsApplyingMotion;

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

    public void AttachRuntimeBindings()
    {
        if (_areRuntimeBindingsAttached)
            return;

        _session.TargetsChanged += OnSessionTargetsChanged;
        _transferSettings.BulkInReadModeChanged += OnTransferSettingsChanged;
        _areRuntimeBindingsAttached = true;
    }

    private void DetachRuntimeBindings()
    {
        if (!_areRuntimeBindingsAttached)
            return;

        _session.TargetsChanged -= OnSessionTargetsChanged;
        _transferSettings.BulkInReadModeChanged -= OnTransferSettingsChanged;
        _areRuntimeBindingsAttached = false;
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

    private bool CanStartScan() =>
        !IsRunning &&
        !IsOutputOperationRunning &&
        !IsAutoFocusing &&
        !IsApplyingIllumination &&
        !IsApplyingMotion &&
        IsConnected &&
        AreAllActiveAcquisitionChannelsConfirmed() &&
        TryParseRequestedRows(out _);

    private bool CanStopScan() => IsRunning;

    private bool CanExportDng() =>
        !IsRunning &&
        !IsOutputOperationRunning &&
        _hasValidScanBuffer &&
        _lineBuffer.Length > 0 &&
        (_lastWorkflowResult is null || _lastWorkflowResult.Passes.Count == ScanDebugConstants.IlluminationChannelCount);

    private bool CanConnectDevices() => IsDevicesPresent && !IsConnected && !IsConnecting;

    private bool CanDisconnectDevices() => IsConnected && !IsConnecting && !IsOutputOperationRunning && !IsApplyingIllumination && !IsApplyingMotion && !IsAutoFocusing;

    private bool CanApplyParameters() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsOutputOperationRunning &&
        !IsApplyingParameters &&
        !IsApplyingIllumination &&
        !IsApplyingMotion &&
        !IsAutoCalibrating &&
        !IsAutoFocusing;

    private bool CanManageIllumination() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsOutputOperationRunning &&
        !IsApplyingParameters &&
        !IsAutoCalibrating &&
        !IsAutoFocusing &&
        !IsApplyingIllumination &&
        !IsApplyingMotion;

    private bool CanManageMotion() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsOutputOperationRunning &&
        !IsApplyingParameters &&
        !IsAutoCalibrating &&
        !IsAutoFocusing &&
        !IsApplyingIllumination &&
        !IsApplyingMotion;

    private bool CanRunAutoCalibration() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsOutputOperationRunning &&
        !IsApplyingParameters &&
        !IsApplyingIllumination &&
        !IsApplyingMotion &&
        !IsAutoCalibrating &&
        !IsAutoFocusing;

    private bool CanRunAutoFocus() => CanRunAutoCalibration();

    [RelayCommand(CanExecute = nameof(CanExportDng))]
    private async Task ExportDng()
    {
        try
        {
            if (_lastWorkflowResult is not null)
            {
                if (_lastWorkflowResult.Passes.Count != ScanDebugConstants.IlluminationChannelCount)
                {
                    StatusText = "ScanDebug_Runtime_StatusExportRequiresFourChannelWorkflow".GetLocalizedFormat(_lastWorkflowResult.Passes.Count, ScanDebugConstants.IlluminationChannelCount);
                    return;
                }

                var folder = await _channelImages.PickDngExportFolderAsync();
                if (folder is null)
                {
                    StatusText = "ScanDebug_Runtime_StatusExportCanceled".GetLocalized();
                    return;
                }

                IsOutputOperationRunning = true;
                await _channelImages.ExportDngChannelsAsync(folder, _lastWorkflowResult, BuildDebugChannelAssignment(), ScanChannelAlignmentMode.Ecc, ScanDngExportMode.LinearRaw4, _channelProfiles.Profiles);
                StatusText = "Scan_Runtime_StatusDngExported".GetLocalizedFormat(folder.Path);
                return;
            }

            var dngFolder = await _channelImages.PickDngExportFolderAsync();
            if (dngFolder is null)
            {
                StatusText = "ScanDebug_Runtime_StatusExportCanceled".GetLocalized();
                return;
            }

            if (!ushort.TryParse(ExposureTicks, out var exposureTicks))
                exposureTicks = 0;
            if (!uint.TryParse(SysClockKhz, out var sysClockKhz))
                sysClockKhz = 0;

            var channelLabel = BuildDebugChannelAssignment().Channel1Role;
            var monochromeProfile = _channelProfiles.TryGetProfile(SelectedCalibrationChannel, out var selectedProfile) ? selectedProfile : null;
            IsOutputOperationRunning = true;
            await _channelImages.ExportMonochromeDngAsync(dngFolder, _lineBuffer, _previewRows, exposureTicks, sysClockKhz, channelLabel, monochromeProfile);
            StatusText = "ScanDebug_Runtime_StatusMonochromeDngExported".GetLocalizedFormat(dngFolder.Path);
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusExportFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            IsOutputOperationRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnectDevices))]
    private async Task ConnectDevices()
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
        try
        {
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
            await _channelProfiles.InitializeAsync();
            var selectedCalibrationChannel = await _channelProfiles.GetSelectedCalibrationChannelAsync();
            if (!string.IsNullOrWhiteSpace(selectedCalibrationChannel)
                && CalibrationChannelOptions.Contains(selectedCalibrationChannel, StringComparer.OrdinalIgnoreCase))
            {
                SelectedCalibrationChannel = selectedCalibrationChannel;
            }

            try
            {
                var snapshot = await _parameters.LoadAsync(_session, _session.ConnectionToken);
                ExposureTicks = snapshot.ExposureTicks.ToString();
                Adc1Offset = _parameters.FormatOffsetForInput(snapshot.Adc1Offset);
                Adc1Gain = snapshot.Adc1Gain.ToString();
                Adc2Offset = _parameters.FormatOffsetForInput(snapshot.Adc2Offset);
                Adc2Gain = snapshot.Adc2Gain.ToString();
                SysClockKhz = snapshot.SysClockKhz.ToString();
                UpdateComputedParameterDisplays();
                statusNotes.Add("ScanDebug_Runtime_StatusParametersLoaded".GetLocalized());
            }
            catch (Exception ex)
            {
                statusNotes.Add("ScanDebug_Runtime_StatusParameterLoadUnavailable".GetLocalizedFormat(ex.Message));
            }

            await LoadSelectedCalibrationProfileAsync(SelectedCalibrationChannel, ++_profileLoadVersion);

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
            IsConnecting = false;
            ConnectDevicesCommand.NotifyCanExecuteChanged();
            DisconnectDevicesCommand.NotifyCanExecuteChanged();
            StartScanCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnectDevices))]
    private async Task DisconnectDevices()
    {
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

            await _sessionCoordinator.DisconnectAsync(CancellationToken.None);
            SwitchToDiscoverySession();
            IsConnected = false;
            ResetIlluminationInputs();
            ResetMotionInputs();
            StatusText = IsDevicesPresent ? "ScanDebug_Runtime_StatusDisconnectedReconnect".GetLocalized() : "ScanDebug_Runtime_StatusDisconnected".GetLocalized();
        }
        finally
        {
            IsConnecting = false;
            ConnectDevicesCommand.NotifyCanExecuteChanged();
            DisconnectDevicesCommand.NotifyCanExecuteChanged();
            StartScanCommand.NotifyCanExecuteChanged();
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

        if (!_parameters.TryParseInput(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockKhz, out var snapshot, out var parseError))
        {
            StatusText = parseError;
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
            StatusText = "ScanDebug_Runtime_StatusParametersUpdated".GetLocalized();
        }
        catch (OperationCanceledException)
        {
            StatusText = "ScanDebug_Runtime_StatusParameterUpdateCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusParameterUpdateFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            IsApplyingParameters = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageIllumination))]
    private async Task RefreshIllumination()
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

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
            IsApplyingIllumination = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageIllumination))]
    private async Task ApplyIllumination()
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        await EnsureDeviceSettingsInitializedAsync();

        if (!TryBuildIlluminationRequest(out var request, out var error))
        {
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
                    await _illumination.ApplyStateWithSafeTransitionAsync(session, BuildIlluminationState(request), token);
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
            IsApplyingIllumination = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageMotion))]
    private async Task RefreshMotion()
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

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
            IsApplyingMotion = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageMotion))]
    private async Task EnableMotor(string? motorDisplayId)
    {
        await SetMotorEnabledCoreAsync(motorDisplayId, true);
    }

    [RelayCommand(CanExecute = nameof(CanManageMotion))]
    private async Task DisableMotor(string? motorDisplayId)
    {
        await SetMotorEnabledCoreAsync(motorDisplayId, false);
    }

    [RelayCommand(CanExecute = nameof(CanManageMotion))]
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

        await EnsureDeviceSettingsInitializedAsync();

        if (!TryBuildMotorMoveRequest(motorId, out var request, out var error))
        {
            StatusText = error;
            return;
        }

        IsApplyingMotion = true;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusMotorMoveStarting".GetLocalizedFormat(motorName);
            await _sessionCoordinator.UseConnectedSessionAsync(
                async (session, token) =>
                {
                    await session.MoveMotorStepsAndWaitForCompletionAsync(motorId, request.Direction, request.Steps, request.IntervalUs, token);
                    return true;
                },
                CancellationToken.None);
            await LoadMotionStateAsync(_session.ConnectionToken);
            StatusText = "ScanDebug_Runtime_StatusMotorMoveCompleted".GetLocalizedFormat(motorName);
        }
        catch (OperationCanceledException)
        {
            StatusText = "ScanDebug_Runtime_StatusMotorMoveCanceled".GetLocalizedFormat(motorName);
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusMotorMoveFailed".GetLocalizedFormat(motorName, ex.Message);
        }
        finally
        {
            IsApplyingMotion = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageMotion))]
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
            StatusText = "ScanDebug_Runtime_StatusMotorStopCanceled".GetLocalizedFormat(motorName);
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusMotorStopFailed".GetLocalizedFormat(motorName, ex.Message);
        }
        finally
        {
            IsApplyingMotion = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageMotion))]
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

        IsApplyingMotion = true;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusMotorConfigApplying".GetLocalizedFormat(motorName);
            await _sessionCoordinator.UseConnectedSessionAsync(
                async (session, token) =>
                {
                    await session.ApplyMotorConfigAsync(motorId, token);
                    return true;
                },
                CancellationToken.None);
            await LoadMotionStateAsync(_session.ConnectionToken);
            StatusText = "ScanDebug_Runtime_StatusMotorConfigApplied".GetLocalizedFormat(motorName);
        }
        catch (OperationCanceledException)
        {
            StatusText = "ScanDebug_Runtime_StatusMotorConfigCanceled".GetLocalizedFormat(motorName);
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusMotorConfigFailed".GetLocalizedFormat(motorName, ex.Message);
        }
        finally
        {
            IsApplyingMotion = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunAutoCalibration))]
    private Task AutoBlackAdjust()
        => RunAutoCalibrationAsync((session, snapshot, roiSettings, prompt, status, applied, frame, ct) => _autoCalibration.AutoBlackAdjustAsync(session, snapshot, roiSettings, prompt, status, applied, frame, ct), "ScanDebug_Runtime_StatusAutoBlackCompleted".GetLocalized());

    [RelayCommand(CanExecute = nameof(CanRunAutoCalibration))]
    private Task AutoWhiteAdjust()
        => RunAutoCalibrationAsync((session, snapshot, roiSettings, prompt, status, applied, frame, ct) => _autoCalibration.AutoWhiteAdjustAsync(session, snapshot, roiSettings, prompt, status, applied, frame, ct), "ScanDebug_Runtime_StatusAutoWhiteCompleted".GetLocalized());

    [RelayCommand(CanExecute = nameof(CanRunAutoCalibration))]
    private Task AutoCalibrate()
        => RunAutoCalibrationAsync((session, snapshot, roiSettings, prompt, status, applied, frame, ct) => _autoCalibration.AutoCalibrateAsync(session, snapshot, roiSettings, prompt, status, applied, frame, ct), "ScanDebug_Runtime_StatusAutoCalibrationCompleted".GetLocalized());

    [RelayCommand]
    private async Task SaveChannelProfile()
    {
        if (!_parameters.TryParseInput(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockKhz, out var snapshot, out var error))
        {
            StatusText = error;
            return;
        }

        try
        {
            await SaveSelectedCalibrationProfileAsync(snapshot);
            StatusText = "ScanDebug_Runtime_StatusCalibrationChannelProfileSaved".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel));
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusSaveChannelProfileFailed".GetLocalizedFormat(ex.Message);
        }
    }

    [RelayCommand]
    private async Task ClearChannelProfile()
    {
        if (string.IsNullOrWhiteSpace(SelectedCalibrationChannel))
        {
            StatusText = "ScanDebug_Runtime_StatusCalibrationChannelEmpty".GetLocalized();
            return;
        }

        try
        {
            var removed = await _channelProfiles.ClearProfileAsync(SelectedCalibrationChannel);
            if (removed)
            {
                _roiSettings = ScanCalibrationRoiSettings.CreateDefault();
                RefreshRoiStatus();
            }
            RefreshColumnSampleStatus();
            RefreshPreviewIfPossible();
            CalibrationChannelStatusText = removed
                ? "ScanDebug_Runtime_CalibrationChannel_ProfileCleared".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel))
                : "ScanDebug_Runtime_CalibrationChannel_NoSavedProfile".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel));
            StatusText = removed
                ? "ScanDebug_Runtime_StatusCalibrationChannelProfileCleared".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel))
                : "ScanDebug_Runtime_StatusCalibrationChannelNoSavedProfile".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel));
            NotifyChannelProfileOverviewChanged();
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusClearChannelProfileFailed".GetLocalizedFormat(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveFilmProfileJson()
    {
        if (!_parameters.TryParseInput(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockKhz, out var snapshot, out var error))
        {
            StatusText = error;
            MirrorOutput("ScanDebug.SaveFilmProfileJson", $"Input validation failed: {error}");
            return;
        }

        var stage = "initialization";
        try
        {
            stage = "device settings initialization";
            await EnsureDeviceSettingsInitializedAsync();

            stage = "calibration profile save";
            await SaveSelectedCalibrationProfileAsync(snapshot);

            stage = "film acquisition settings validation";
            if (!TryBuildFilmAcquisitionSettings(out var acquisitionSettings, out error))
            {
                StatusText = error;
                MirrorOutput("ScanDebug.SaveFilmProfileJson", $"Film acquisition settings validation failed: {error}");
                return;
            }

            stage = "film profile name resolution";
            var profileName = string.IsNullOrWhiteSpace(FilmProfileName)
                ? "ScanDebug_Runtime_FilmProfileUntitled".GetLocalizedOrFallback("Untitled Film Profile")
                : FilmProfileName.Trim();

            stage = "scan recipe settings validation";
            if (!TryBuildScanRecipeSettings(out var scanRecipeSettings, out error))
            {
                StatusText = error;
                MirrorOutput("ScanDebug.SaveFilmProfileJson", $"Scan recipe settings validation failed: {error}");
                return;
            }

            stage = "JSON export";
            var exported = await _channelProfiles.ExportProfilesAsync(new ScanFilmParameterProfileSet(
                5,
                profileName,
                DateTimeOffset.Now,
                _channelProfiles.Profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                SelectedCalibrationChannel,
                acquisitionSettings,
                scanRecipeSettings));

            if (!exported)
            {
                StatusText = "ScanDebug_Runtime_StatusFilmProfileExportCanceled".GetLocalizedOrFallback("Save film profile canceled.");
                MirrorOutput("ScanDebug.SaveFilmProfileJson", $"JSON export canceled during {stage}.");
                return;
            }

            HasUnsavedProfileChanges = false;
            StatusText = "ScanDebug_Runtime_StatusFilmProfileExported".GetLocalizedFormatOrFallback("Film profile '{0}' exported.", profileName);
            MirrorOutput("ScanDebug.SaveFilmProfileJson", $"JSON export completed for profile '{profileName}'.");
        }
        catch (Exception ex)
        {
            MirrorOutput("ScanDebug.SaveFilmProfileJson", $"Save JSON failed during {stage}.{Environment.NewLine}{ex}");
            StatusText = "ScanDebug_Runtime_StatusSaveFilmProfileFailed".GetLocalizedFormatOrFallback("Save film profile failed: {0}", ex.Message);
        }
    }

    [RelayCommand]
    private async Task LoadFilmProfileJson()
    {
        try
        {
            var imported = await _channelProfiles.ImportProfilesAsync();
            if (imported is null)
            {
                StatusText = "ScanDebug_Runtime_StatusLoadFilmProfileCanceled".GetLocalized();
                return;
            }

            await _channelProfiles.ReplaceProfilesAsync(imported);
            FilmProfileName = imported.ProfileName;
            _selectedFilmAcquisitionSettings = imported.AcquisitionSettings?.Normalize();

            if (_selectedFilmAcquisitionSettings is not null)
                ApplyProfileAcquisitionSettings(_selectedFilmAcquisitionSettings);

            ApplyScanRecipeSettings(imported.ScanRecipeSettings);

            var channelToLoad = ResolveProfileChannelToLoad(imported);
            if (!string.IsNullOrWhiteSpace(channelToLoad))
            {
                SelectedCalibrationChannel = channelToLoad;
                await LoadSelectedCalibrationProfileAsync(channelToLoad, ++_profileLoadVersion);
            }

            StatusText = "ScanDebug_Runtime_StatusFilmProfileLoaded".GetLocalizedFormat(imported.ProfileName);
            HasUnsavedProfileChanges = false;
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusLoadFilmProfileFailed".GetLocalizedFormat(ex.Message);
        }
    }

    [RelayCommand]
    private void ResetSelectedRoi()
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
        NormalizeCurrentRoiSettings();
        RefreshRoiStatus();
    }

    [RelayCommand]
    private void ApplySelectedRoiInputs()
    {
        if (!int.TryParse(RoiStartInput, out var start))
        {
            RoiInputStatusText = "ScanDebug_Runtime_RoiStartIntegerRequired".GetLocalized();
            return;
        }

        if (!int.TryParse(RoiEndInput, out var endInclusive))
        {
            RoiInputStatusText = "ScanDebug_Runtime_RoiEndIntegerRequired".GetLocalized();
            return;
        }

        UpdateSelectedRoiRange(start, endInclusive, GetRoiEditingWidth());
        RoiInputStatusText = "ScanDebug_Runtime_RoiRangeApplied".GetLocalized();
    }

    [RelayCommand]
    private void ResetAllRois()
    {
        _roiSettings = ScanCalibrationRoiSettings.CreateDefault();
        NormalizeCurrentRoiSettings();
        RefreshRoiStatus();
    }

    [RelayCommand(CanExecute = nameof(CanRunAutoFocus))]
    private async Task AutoFocus()
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        await EnsureDeviceSettingsInitializedAsync();

        if (!TryBuildAutofocusRequest(out var request, out var error))
        {
            StatusText = error;
            return;
        }

        IsAutoFocusing = true;
        using var autofocusCts = CancellationTokenSource.CreateLinkedTokenSource(_session.ConnectionToken);
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
            AutofocusSummaryText = "ScanDebug_Runtime_AutofocusCanceled".GetLocalized();
            StatusText = "ScanDebug_Runtime_StatusAutofocusCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
            AutofocusSummaryText = "ScanDebug_Runtime_AutofocusFailed".GetLocalizedFormat(ex.Message);
            StatusText = "ScanDebug_Runtime_StatusAutofocusFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            if (restoreWarmUp && IsConnected)
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

            IsAutoFocusing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartScan))]
    private async Task StartScan()
    {
        await Task.Yield();

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

        var shouldUseWorkflowScan = IsMultiChannelScanEnabled || IsScanMotorTransportEnabled;
        if (shouldUseWorkflowScan && IsContinuousScanEnabled)
        {
            StatusText = IsMultiChannelScanEnabled
                ? "ScanDebug_Runtime_ErrorMultiChannelContinuousUnsupported".GetLocalized()
                : "ScanDebug_Runtime_ErrorMotorTransportContinuousUnsupported".GetLocalized();
            return;
        }

        var multiChannelProgressMaxRows = int.MaxValue / ScanDebugConstants.BytesPerLine / ScanDebugConstants.IlluminationChannelCount;
        if (shouldUseWorkflowScan && rows > multiChannelProgressMaxRows)
        {
            StatusText = "ScanDebug_Runtime_ErrorRowsRange".GetLocalizedFormat(multiChannelProgressMaxRows);
            return;
        }

        ScanWorkflowRequest? workflowRequest = null;
        if (shouldUseWorkflowScan)
            await EnsureDeviceSettingsInitializedAsync();

        if (shouldUseWorkflowScan && !TryBuildDebugWorkflowRequest(rows, out workflowRequest, out var workflowError))
        {
            StatusText = workflowError;
            return;
        }

        var workflowPassCount = workflowRequest is null ? 0 : CountActiveWorkflowPasses(workflowRequest);

        _scanCts = new CancellationTokenSource();
        IsRunning = true;
        IsScanReadProgressVisible = true;
        ScanReadProgressValue = 0;
        ScanReadProgressMaximum = Math.Max(1, (double)rows * ScanDebugConstants.BytesPerLine * Math.Max(1, workflowPassCount > 0 ? workflowPassCount : 1));
        StatusText = IsMultiChannelScanEnabled
            ? "ScanDebug_Runtime_StatusStartingMultiChannelScan".GetLocalized()
            : IsContinuousScanEnabled ? "ScanDebug_Runtime_StatusStartingContinuousScan".GetLocalized() : "ScanDebug_Runtime_StatusStartingScan".GetLocalized();

        try
        {
            if (workflowRequest is not null)
                await RunWorkflowScanAsync(workflowRequest!, _scanCts.Token);
            else if (IsContinuousScanEnabled)
                await RunContinuousScanLoopAsync(rows, _scanCts.Token);
            else
                await RunSingleScanAsync(rows, _scanCts.Token);
        }
        catch (Exception ex)
        {
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

    [RelayCommand(CanExecute = nameof(CanStopScan))]
    private async Task StopScan()
    {
        if (!IsRunning)
            return;

        _scanCts?.Cancel();
        var result = await _sessionCoordinator.UseConnectedSessionAsync(
            (session, _) => session.StopScanAsync(CancellationToken.None),
            CancellationToken.None);
        StatusText = result.Success ? "ScanDebug_Runtime_StatusStopRequested".GetLocalized() : ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message);
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
        var result = await RunScanAsync(rows, ct);

        StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message);
        if (!result.Success || result.ImageBytes is null)
            return;

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
            _lastWorkflowResult = null;
            ApplyScanFrame(result.ImageBytes, rows, "ScanDebug_Runtime_StatusContinuousPreviewUpdated".GetLocalizedFormat(frameCount));
        }
    }

    [RelayCommand]
    private async Task SaveColumnSampleAsBlackLevel()
    {
        if (!TryGetCurrentColumnSampleMean(out var mean, out var error))
        {
            StatusText = error;
            return;
        }

        if (!TryResolveSnapshotForLevelSave(out var snapshot, out error))
        {
            StatusText = error;
            return;
        }

        var existingProfile = _channelProfiles.TryGetProfile(SelectedCalibrationChannel, out var profile) ? profile : null;
        var whiteLevel = existingProfile?.WhiteLevel;
        ushort? blackLevel = whiteLevel is not null ? (ushort)Math.Min(mean, Math.Max(0, whiteLevel.Value - 1)) : mean;

        await SaveCalibrationLevelsAsync(snapshot, blackLevel, whiteLevel);
        RefreshColumnSampleStatus();
        StatusText = $"Saved black level {blackLevel} for {GetCalibrationChannelDisplayName(SelectedCalibrationChannel)}.";
    }

    [RelayCommand]
    private async Task SaveColumnSampleAsWhiteLevel()
    {
        if (!TryGetCurrentColumnSampleMean(out var mean, out var error))
        {
            StatusText = error;
            return;
        }

        if (!TryResolveSnapshotForLevelSave(out var snapshot, out error))
        {
            StatusText = error;
            return;
        }

        var existingProfile = _channelProfiles.TryGetProfile(SelectedCalibrationChannel, out var profile) ? profile : null;
        var blackLevel = existingProfile?.BlackLevel;
        ushort? whiteLevel = blackLevel is not null ? (ushort)Math.Min(ushort.MaxValue, Math.Max(mean, blackLevel.Value + 1)) : mean;

        await SaveCalibrationLevelsAsync(snapshot, blackLevel, whiteLevel);
        RefreshColumnSampleStatus();
        RefreshPreviewIfPossible();
        StatusText = $"Saved white level {whiteLevel} for {GetCalibrationChannelDisplayName(SelectedCalibrationChannel)}.";
    }

    private async Task RunWorkflowScanAsync(ScanWorkflowRequest request, CancellationToken ct)
    {
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
                    ReportScanReadProgress),
                ct,
                waitForAvailability: false);

            var previewPass = result.Passes.FirstOrDefault();
            if (previewPass is null || previewPass.ImageBytes.Length == 0)
            {
                StatusText = "ScanDebug_Runtime_StatusMultiChannelNoPassData".GetLocalized();
                return;
            }

            _lastWorkflowResult = result;
            ApplyScanFrame(
                previewPass.ImageBytes,
                previewPass.Rows,
                "ScanDebug_Runtime_StatusMultiChannelScanCompleted".GetLocalizedFormat(result.Passes.Count, previewPass.PassIndex));
        }
        catch (OperationCanceledException)
        {
            StatusText = "ScanDebug_Runtime_StatusMultiChannelScanCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
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
        if (IsScanLedAutoControlEnabled
            && (!TryParseLedLevel(Led1Level, "ScanDebug_Runtime_FieldLed1Level".GetLocalized(), out led1, out error)
                || !TryParseLedLevel(Led2Level, "ScanDebug_Runtime_FieldLed2Level".GetLocalized(), out led2, out error)
                || !TryParseLedLevel(Led3Level, "ScanDebug_Runtime_FieldLed3Level".GetLocalized(), out led3, out error)
                || !TryParseLedLevel(Led4Level, "ScanDebug_Runtime_FieldLed4Level".GetLocalized(), out led4, out error)))
        {
            return false;
        }

        if (!_parameters.TryParseInput(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockKhz, out var fallbackSnapshot, out error))
            return false;

        var motorIntervalUs = ScanDebugConstants.MotionDefaultIntervalUs;
        if (IsScanMotorTransportEnabled && !TryGetEffectiveScanMotorIntervalUs(fallbackSnapshot.ExposureTicks, fallbackSnapshot.SysClockKhz, out motorIntervalUs, out error))
            return false;

        var channelRoles = BuildDebugChannelAssignment().Roles.ToArray();
        var activeRoleCount = channelRoles.Count(role => !string.Equals(role, "Unused", StringComparison.OrdinalIgnoreCase));
        if (activeRoleCount == 0)
        {
            error = "ScanDebug_Runtime_ErrorNoActiveScanChannels".GetLocalized();
            return false;
        }

        if (!IsMultiChannelScanEnabled && activeRoleCount > 1)
        {
            error = "ScanDebug_Runtime_ErrorSingleChannelRequiresExactlyOnePass".GetLocalized();
            return false;
        }

        if (!TryParseSelectedScanMotor(out var scanMotorId, out error))
            return false;

        ScanFilmAcquisitionSettings? acquisitionSettings = null;
        if (IsScanLedAutoControlEnabled)
        {
            if (!TryBuildIlluminationRequest(out var illuminationRequest, out error))
                return false;

            acquisitionSettings = new ScanFilmAcquisitionSettings(
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
                motorIntervalUs).Normalize();
        }

        var passProfiles = channelRoles
            .Select(role => string.Equals(role, "Unused", StringComparison.OrdinalIgnoreCase)
                ? fallbackSnapshot
                : _channelProfiles.TryGetProfile(role, out var profile) ? profile.Parameters : fallbackSnapshot)
            .ToArray();

        request = new ScanWorkflowRequest(
            rows,
            IsWarmUpEnabled,
            new[] { led1, led2, led3, led4 },
            channelRoles,
            passProfiles,
            scanMotorId,
            motorIntervalUs,
            string.Equals(SelectedStartingDirection, ForwardDirection, StringComparison.OrdinalIgnoreCase),
            IsAlternateMotorDirectionEnabled,
            fallbackSnapshot.ExposureTicks,
            fallbackSnapshot.SysClockKhz,
            acquisitionSettings,
            IsScanMotorTransportEnabled,
            IsScanLedAutoControlEnabled);

        error = string.Empty;
        return true;
    }

    private ScanChannelAssignment BuildDebugChannelAssignment()
    {
        var roles = GetEffectiveDeviceChannelRoles();
        return new(roles[0], roles[1], roles[2], roles[3], false, false, false, false);
    }

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

    private bool TryBuildScanRecipeSettings(out ScanFilmScanRecipeSettings settings, out string error)
    {
        settings = new ScanFilmScanRecipeSettings();
        if (!TryParseColorDouble(ScanRecipeRedWavelengthNm, "Scan_Runtime_FieldRedWavelengthNm".GetLocalized(), out var redWavelength, out error)
            || !TryParseColorDouble(ScanRecipeGreenWavelengthNm, "Scan_Runtime_FieldGreenWavelengthNm".GetLocalized(), out var greenWavelength, out error)
            || !TryParseColorDouble(ScanRecipeBlueWavelengthNm, "Scan_Runtime_FieldBlueWavelengthNm".GetLocalized(), out var blueWavelength, out error)
            || !TryParseColorDouble(ScanRecipeOutputGamma, "Scan_Runtime_FieldOutputGamma".GetLocalized(), out var outputGamma, out error))
        {
            return false;
        }

        settings = new ScanFilmScanRecipeSettings(
            BuildAuthoredChannelAssignment(),
            new ScanColorManagementOptions(IsScanRecipeColorManagementEnabled, redWavelength, greenWavelength, blueWavelength, outputGamma),
            SelectedProfileAlignmentMode,
            SelectedProfileDngExportMode);
        error = string.Empty;
        return true;
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

        if (!_parameters.TryParseInput(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockKhz, out var snapshot, out _)
            || snapshot.SysClockKhz < ScanDebugConstants.MinSysClockKhz)
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorUnavailableUntilParametersLoaded".GetLocalized();
            return;
        }

        var motorSettings = _deviceSettings.Settings.GetMotorSettings(motorId);
        if (!TryGetEffectiveScanMotorIntervalUs(snapshot.ExposureTicks, snapshot.SysClockKhz, out var intervalUs, out _))
        {
            ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorIntervalMinimum".GetLocalizedFormat(ScanDebugConstants.MotionMinIntervalUs);
            return;
        }

        var computedSteps = ScanTimingMath.ComputeMotorStepsPerPass(rows, snapshot.ExposureTicks, snapshot.SysClockKhz, intervalUs);
        var distanceMm = ScanTimingMath.ConvertMotorStepsToMillimeters(computedSteps, motorSettings);
        var speedMmPerSecond = ScanTimingMath.ConvertMotorIntervalToMillimetersPerSecond(intervalUs, motorSettings);
        ComputedMotorSummaryText = "Scan_Runtime_ComputedMotorSummary".GetLocalizedFormat(motorId + 1, computedSteps, intervalUs, rows, distanceMm.ToString("0.###", CultureInfo.InvariantCulture), speedMmPerSecond.ToString("0.###", CultureInfo.InvariantCulture));
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

    private bool TryGetEffectiveScanMotorIntervalUs(ushort exposureTicks, uint sysClockKhz, out uint intervalUs, out string error)
    {
        intervalUs = 0;

        if (_isMotorDistanceDerivedFromInterval)
        {
            if (uint.TryParse(MotorIntervalUs, NumberStyles.Integer, CultureInfo.InvariantCulture, out intervalUs)
                && intervalUs >= ScanDebugConstants.MotionMinIntervalUs)
            {
                error = string.Empty;
                return true;
            }

            error = "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanDebugConstants.MotionMinIntervalUs);
            return false;
        }

        if (!ScanMotorDistanceText.TryParseMillimeters(MotorDistancePerLineValue, MotorDistancePerLineUnit, GetCurrentScanMotorSettings(), out var lineDistanceMm)
            || !ScanTimingMath.TryConvertLineDistanceMillimetersToMotorIntervalUs(lineDistanceMm, exposureTicks, sysClockKhz, GetCurrentScanMotorSettings(), ScanDebugConstants.MotionMinIntervalUs, out intervalUs))
        {
            error = "Scan_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(ScanDebugConstants.MotionMinIntervalUs);
            return false;
        }

        MotorIntervalUs = intervalUs.ToString(CultureInfo.InvariantCulture);
        error = string.Empty;
        return true;
    }

    private void RefreshDerivedMotorDistanceFromCurrentInterval()
    {
        if (!_isMotorDistanceDerivedFromInterval)
            return;

        if (!uint.TryParse(MotorIntervalUs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intervalUs)
            || intervalUs < ScanDebugConstants.MotionMinIntervalUs
            || !_parameters.TryParseInput(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockKhz, out var snapshot, out _)
            || snapshot.SysClockKhz < ScanDebugConstants.MinSysClockKhz)
        {
            ApplyDerivedMotorDistance(string.Empty);
            return;
        }

        var lineDistanceMm = ScanTimingMath.ConvertMotorIntervalToLineDistanceMillimeters(intervalUs, snapshot.ExposureTicks, snapshot.SysClockKhz, GetCurrentScanMotorSettings());
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
        return new ScanColorManagementOptions(IsGammaCorrectionEnabled, defaults.RedWavelengthNm, defaults.GreenWavelengthNm, defaults.BlueWavelengthNm, TryParsePreviewGamma(out var gamma) ? gamma : defaults.OutputGamma);
    }

    private void ApplyScanFrame(byte[] imageBytes, int rows, string successStatus)
    {
        _lineBuffer = imageBytes;
        _previewRows = rows;
        _hasValidScanBuffer = true;
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

        try
        {
            var result = await _sessionCoordinator.SetWarmUpAsync(enabled, _session.ConnectionToken);
            StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message);
        }
        catch (OperationCanceledException)
        {
            StatusText = enabled ? "ScanDebug_Runtime_StatusWarmUpEnableCanceled".GetLocalized() : "ScanDebug_Runtime_StatusWarmUpDisableCanceled".GetLocalized();
        }
    }

    private async Task RunAutoCalibrationAsync(Func<IScanSessionService, ScanParameterSnapshot, ScanCalibrationRoiSettings, Func<ScanCalibrationPrompt, Task<bool>>, Action<string>, Action<ScanParameterSnapshot>, Action<byte[], int, string>, CancellationToken, Task<ScanParameterSnapshot>> operation, string successMessage)
    {
        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        if (!_parameters.TryParseInput(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockKhz, out var snapshot, out var error))
        {
            StatusText = error;
            return;
        }

        IsAutoCalibrating = true;
        var calibrationCts = new CancellationTokenSource();
        try
        {
            StatusText = "ScanDebug_Runtime_StatusAutoCalibrationStarted".GetLocalized();
            var calibrated = await _sessionCoordinator.RunConnectedSessionStateAsync(
                ScannerSessionState.Running,
                (session, token) => operation(
                    session,
                    snapshot,
                    _roiSettings.Normalize(),
                    RequestCalibrationPromptAsync,
                    status => _dispatcher.TryEnqueue(() => StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(status)),
                    applied => _dispatcher.TryEnqueue(() => ApplySnapshotToInputs(applied)),
                    (imageBytes, rows, phase) => _dispatcher.TryEnqueue(() => ShowCalibrationFrame(imageBytes, rows, phase)),
                    token),
                calibrationCts.Token);

            ApplySnapshotToInputs(calibrated);
            await SaveSelectedCalibrationProfileAsync(calibrated);
            StatusText = successMessage;
        }
        catch (OperationCanceledException)
        {
            StatusText = "ScanDebug_Runtime_StatusAutoCalibrationCanceled".GetLocalized();
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusAutoCalibrationFailed".GetLocalizedFormat(ex.Message);
        }
        finally
        {
            calibrationCts.Dispose();
            IsAutoCalibrating = false;
        }
    }

    private async Task LoadSelectedCalibrationProfileAsync(string channelRole, int loadVersion)
    {
        if (string.IsNullOrWhiteSpace(channelRole))
            return;

        await _channelProfiles.SetSelectedCalibrationChannelAsync(channelRole);

        if (loadVersion != _profileLoadVersion || !string.Equals(channelRole, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
            return;

        if (!_channelProfiles.TryGetProfile(channelRole, out var profile))
        {
            _roiSettings = ScanCalibrationRoiSettings.CreateDefault();
            RefreshRoiStatus();
            RefreshColumnSampleStatus();
            CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_NoSavedProfile".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole));
            RefreshPreviewIfPossible();
            NotifyChannelProfileOverviewChanged();
            return;
        }

        ApplySnapshotToInputs(profile.Parameters);
        _roiSettings = profile.RoiSettings.Normalize();
        RefreshRoiStatus();
        RefreshColumnSampleStatus();
        CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_SavedProfileLoaded".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole));
        RefreshPreviewIfPossible();
        NotifyChannelProfileOverviewChanged();
    }

    private async Task HandleSelectedCalibrationChannelChangedAsync(string channelRole)
    {
        var loadVersion = ++_profileLoadVersion;
        try
        {
            await LoadSelectedCalibrationProfileAsync(channelRole, loadVersion);
        }
        catch (Exception ex)
        {
            if (loadVersion == _profileLoadVersion && string.Equals(channelRole, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
                CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_LoadFailed".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole), ex.Message);
        }
    }

    private async Task SaveSelectedCalibrationProfileAsync(ScanParameterSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(SelectedCalibrationChannel))
            return;

        var existingProfile = _channelProfiles.TryGetProfile(SelectedCalibrationChannel, out var profile) ? profile : null;
        await _channelProfiles.SaveProfileAsync(
            SelectedCalibrationChannel,
            new ScanChannelCalibrationProfile(
                snapshot,
                _roiSettings.Normalize(),
                existingProfile?.BlackLevel,
                existingProfile?.WhiteLevel));
        CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_SavedAt".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel), DateTime.Now.ToString("HH:mm:ss"));
        RefreshColumnSampleStatus();
        NotifyChannelProfileOverviewChanged();
    }

    private async Task SaveCalibrationLevelsAsync(ScanParameterSnapshot snapshot, ushort? blackLevel, ushort? whiteLevel)
    {
        if (string.IsNullOrWhiteSpace(SelectedCalibrationChannel))
            return;

        await _channelProfiles.SaveProfileAsync(
            SelectedCalibrationChannel,
            new ScanChannelCalibrationProfile(
                snapshot,
                _roiSettings.Normalize(),
                blackLevel,
                whiteLevel));
        CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_SavedAt".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel), DateTime.Now.ToString("HH:mm:ss"));
        NotifyChannelProfileOverviewChanged();
    }

    private bool TryResolveSnapshotForLevelSave(out ScanParameterSnapshot snapshot, out string error)
    {
        if (_parameters.TryParseInput(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockKhz, out snapshot, out error))
            return true;

        if (_channelProfiles.TryGetProfile(SelectedCalibrationChannel, out var existingProfile))
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
        if (!string.IsNullOrWhiteSpace(imported.SelectedCalibrationChannel)
            && CalibrationChannelOptions.Contains(imported.SelectedCalibrationChannel, StringComparer.OrdinalIgnoreCase))
        {
            return imported.SelectedCalibrationChannel;
        }

        var firstKnown = imported.ChannelProfiles.Keys.FirstOrDefault(role => CalibrationChannelOptions.Contains(role, StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(firstKnown))
            return firstKnown;

        return SelectedCalibrationChannel;
    }

    private bool TryBuildFilmAcquisitionSettings(out ScanFilmAcquisitionSettings settings, out string error)
    {
        settings = ScanFilmAcquisitionSettings.CreateDefault();

        if (!TryBuildIlluminationRequest(out var illuminationRequest, out error))
            return false;

        if (!TryBuildMotorIntervalFromInputs(1, Motor2SpeedValue, Motor2SpeedUnit, out var motorIntervalUs, out error))
        {
            return false;
        }

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
            motorIntervalUs).Normalize();
        error = string.Empty;
        return true;
    }

    private Task<bool> RequestCalibrationPromptAsync(ScanCalibrationPrompt prompt)
    {
        var request = new ScanCalibrationPromptRequest(prompt);
        CalibrationPromptRequested?.Invoke(this, request);
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
        ExposureTicks = snapshot.ExposureTicks.ToString();
        Adc1Offset = _parameters.FormatOffsetForInput(snapshot.Adc1Offset);
        Adc1Gain = snapshot.Adc1Gain.ToString();
        Adc2Offset = _parameters.FormatOffsetForInput(snapshot.Adc2Offset);
        Adc2Gain = snapshot.Adc2Gain.ToString();
        SysClockKhz = snapshot.SysClockKhz.ToString();
        UpdateComputedParameterDisplays();
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
        var clamped = _roiSettings.Clamp(imageWidth);
        switch (SelectedRoiSelection)
        {
            case RoiSelectionBwActive:
                range = clamped.EffectiveRange;
                return true;
            case RoiSelectionBwShield:
                range = clamped.ShieldRange;
                return true;
            case RoiSelectionFocusOverall:
                range = clamped.FocusOverallRange;
                return true;
            case RoiSelectionFocusLeft:
                range = clamped.FocusLeftRange;
                return true;
            case RoiSelectionFocusRight:
                range = clamped.FocusRightRange;
                return true;
            default:
                range = new ScanColumnRange(0, -1);
                return false;
        }
    }

    public void UpdateSelectedRoiRange(int start, int endInclusive, int imageWidth)
    {
        var nextRange = new ScanColumnRange(start, endInclusive).Clamp(imageWidth);
        _roiSettings = SelectedRoiSelection switch
        {
            RoiSelectionBwActive => _roiSettings with { EffectiveRange = nextRange },
            RoiSelectionBwShield => _roiSettings with { ShieldRange = nextRange },
            RoiSelectionFocusOverall => _roiSettings with { FocusOverallRange = nextRange },
            RoiSelectionFocusLeft => _roiSettings with { FocusLeftRange = nextRange },
            RoiSelectionFocusRight => _roiSettings with { FocusRightRange = nextRange },
            _ => _roiSettings
        };

        NormalizeCurrentRoiSettings();
        RefreshRoiStatus();
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

        range = _columnSampleRange.Clamp(imageWidth);
        return true;
    }

    public void UpdateColumnSampleRange(int start, int endInclusive, int imageWidth)
    {
        _columnSampleRange = new ScanColumnRange(start, endInclusive).Clamp(imageWidth);
        RefreshColumnSampleStatus();
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

    private void NormalizeCurrentRoiSettings()
        => _roiSettings = _roiSettings.Normalize();

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
        NormalizeCurrentRoiSettings();
        EnsureRoiEditModeAvailability();
        EnsureColumnSampleEditModeAvailability();
        RefreshRoiInputTexts();
        RoiStatusText = BuildRoiStatusText();
        RoiOverlayVersion++;
    }

    private void RefreshColumnSampleStatus()
    {
        if (PreviewFrame is null || PreviewFrame.Width <= 0 || !_hasValidScanBuffer || _previewRows <= 0)
        {
            _columnSampleMean = null;
            ColumnSampleStatusText = "ScanDebug_Runtime_ColumnSampleUnavailable".GetLocalized();
            ColumnSampleOverlayVersion++;
            return;
        }

        _columnSampleRange = _columnSampleRange.Clamp(PreviewFrame.Width);
        if (!TryGetCurrentColumnSampleMean(out var mean, out var error))
        {
            _columnSampleMean = null;
            ColumnSampleStatusText = "ScanDebug_Runtime_ColumnSampleUnavailableWithError".GetLocalizedFormat(_columnSampleRange.Start, _columnSampleRange.EndInclusive, error);
            ColumnSampleOverlayVersion++;
            return;
        }

        _columnSampleMean = mean;
        var savedLevels = _channelProfiles.TryGetProfile(SelectedCalibrationChannel, out var profile)
            ? "ScanDebug_Runtime_ColumnSampleSavedLevels".GetLocalizedFormat(FormatOptionalLevel(profile.BlackLevel), FormatOptionalLevel(profile.WhiteLevel))
            : string.Empty;
        ColumnSampleStatusText = "ScanDebug_Runtime_ColumnSampleSummary".GetLocalizedFormat(_columnSampleRange.Start, _columnSampleRange.EndInclusive, _columnSampleRange.Width, mean, savedLevels);
        ColumnSampleOverlayVersion++;
    }

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
            return _channelImages.TryComputeAlignedChannelColumnAverage(_lastWorkflowResult, BuildDebugChannelAssignment(), ScanChannelAlignmentMode.Ecc, SelectedCalibrationChannel, _columnSampleRange, out mean, out error);

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

        var clamped = range.Clamp(width);
        ulong sum = 0;
        long count = 0;
        for (var y = 0; y < _previewRows; y++)
        {
            for (var x = clamped.Start; x <= clamped.EndInclusive; x++)
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

    private void UpdateComputedParameterDisplays()
    {
        var displays = _parameters.BuildDisplays(ExposureTicks, Adc1Offset, Adc1Gain, Adc2Offset, Adc2Gain, SysClockKhz);
        ExposureTimeDisplay = displays.ExposureTimeDisplay;
        Adc1OffsetMvDisplay = displays.Adc1OffsetMvDisplay;
        Adc2OffsetMvDisplay = displays.Adc2OffsetMvDisplay;
        Adc1GainVvDisplay = displays.Adc1GainVvDisplay;
        Adc2GainVvDisplay = displays.Adc2GainVvDisplay;
        SysClockMhzDisplay = displays.SysClockMhzDisplay;
    }

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
            ? LimitBlockNormalBrush
            : LimitBlockAlertBrush;

    private static Brush BuildBoundedLimitTextBrush(string text, int min, int max)
        => string.IsNullOrWhiteSpace(text) || int.TryParse(text, out var value) && value >= min && value <= max
            ? LimitBlockNormalTextBrush
            : LimitBlockAlertTextBrush;

    private static Brush BuildLowerBoundLimitBrush(string text, uint min)
        => string.IsNullOrWhiteSpace(text) || uint.TryParse(text, out var value) && value >= min
            ? LimitBlockNormalBrush
            : LimitBlockAlertBrush;

    private static Brush BuildLowerBoundLimitTextBrush(string text, uint min)
        => string.IsNullOrWhiteSpace(text) || uint.TryParse(text, out var value) && value >= min
            ? LimitBlockNormalTextBrush
            : LimitBlockAlertTextBrush;

    private static Brush BuildPositiveDistanceLimitBrush(string text)
        => string.IsNullOrWhiteSpace(text) || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= AutofocusDistanceMinMm
            ? LimitBlockNormalBrush
            : LimitBlockAlertBrush;

    private static Brush BuildPositiveDistanceLimitTextBrush(string text)
        => string.IsNullOrWhiteSpace(text) || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= AutofocusDistanceMinMm
            ? LimitBlockNormalTextBrush
            : LimitBlockAlertTextBrush;

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
        OnPropertyChanged(nameof(AutofocusMotorIntervalLimitText));
        OnPropertyChanged(nameof(Adc1OffsetLimitBrush));
        OnPropertyChanged(nameof(Adc2OffsetLimitBrush));
        OnPropertyChanged(nameof(Adc1GainLimitBrush));
        OnPropertyChanged(nameof(Adc2GainLimitBrush));
        OnPropertyChanged(nameof(AutofocusSampleRowsLimitBrush));
        OnPropertyChanged(nameof(AutofocusTiltProbeStepsLimitBrush));
        OnPropertyChanged(nameof(AutofocusZProbeStepsLimitBrush));
        OnPropertyChanged(nameof(AutofocusMotorIntervalLimitBrush));
        OnPropertyChanged(nameof(Adc1OffsetLimitTextBrush));
        OnPropertyChanged(nameof(Adc2OffsetLimitTextBrush));
        OnPropertyChanged(nameof(Adc1GainLimitTextBrush));
        OnPropertyChanged(nameof(Adc2GainLimitTextBrush));
        OnPropertyChanged(nameof(AutofocusSampleRowsLimitTextBrush));
        OnPropertyChanged(nameof(AutofocusTiltProbeStepsLimitTextBrush));
        OnPropertyChanged(nameof(AutofocusZProbeStepsLimitTextBrush));
        OnPropertyChanged(nameof(AutofocusMotorIntervalLimitTextBrush));
    }

    private void NotifyPreviewStatePropertiesChanged()
    {
        OnPropertyChanged(nameof(HasPreviewImage));
        OnPropertyChanged(nameof(IsPreviewImageAvailable));
        OnPropertyChanged(nameof(PreviewEmptyStateVisibility));
        OnPropertyChanged(nameof(PreviewEmptyStateTitleText));
        OnPropertyChanged(nameof(PreviewEmptyStateDescriptionText));
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
        OnPropertyChanged(nameof(StartDisabledReasonText));
        OnPropertyChanged(nameof(ExportDngDisabledReasonText));
    }

    private void NotifyProfileStateChanged()
    {
        OnPropertyChanged(nameof(CurrentProfileNameText));
        OnPropertyChanged(nameof(ProfileSaveStateText));
        OnPropertyChanged(nameof(SaveProfileDisabledReasonText));
    }

    private void NotifyAcquisitionPlanChanged()
    {
        foreach (var channel in AcquisitionChannels)
            channel.RefreshStatus();

        OnPropertyChanged(nameof(AcquisitionPlanSummaryText));
        OnPropertyChanged(nameof(AcquisitionChannelOrderText));
        OnPropertyChanged(nameof(ChannelLedBindingSummaryText));
        OnPropertyChanged(nameof(ProfileChannelOverviewText));
        NotifyActionAvailabilityChanged();
        StartScanCommand.NotifyCanExecuteChanged();
    }

    private void NotifyChannelProfileOverviewChanged()
    {
        OnPropertyChanged(nameof(ProfileChannelOverviewText));
        NotifyPreviewStatePropertiesChanged();
    }

    private void MarkProfileDirty()
    {
        HasUnsavedProfileChanges = true;
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

            var calibrationState = _channelProfiles.TryGetProfile(role, out _)
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
            && _channelProfiles.TryGetProfile(SelectedCalibrationChannel, out _);

    private string BuildStartDisabledReason()
    {
        if (IsRunning)
            return "ScanDebug_DisabledReasonScanRunning".GetLocalized();
        if (IsOutputOperationRunning)
            return "ScanDebug_DisabledReasonOutputRunning".GetLocalized();
        if (IsAutoFocusing || IsApplyingIllumination || IsApplyingMotion)
            return "ScanDebug_DisabledReasonDeviceBusy".GetLocalized();
        if (!IsConnected)
            return "ScanDebug_DisabledReasonConnectDevice".GetLocalized();
        if (!HasSelectedAcquisitionChannels())
            return "ScanDebug_DisabledReasonNoAcquisitionChannels".GetLocalized();
        if (!AreAllActiveAcquisitionChannelsConfirmed())
            return "ScanDebug_DisabledReasonConfirmAllAcquisitionChannels".GetLocalized();
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
        if (!_hasValidScanBuffer || _lineBuffer.Length == 0)
            return "ScanDebug_DisabledReasonNoCapture".GetLocalized();
        if (_lastWorkflowResult is not null && _lastWorkflowResult.Passes.Count != ScanDebugConstants.IlluminationChannelCount)
            return "ScanDebug_DisabledReasonRequiresFourChannelCapture".GetLocalized();
        return "ScanDebug_DisabledReasonUnavailable".GetLocalized();
    }

    private async Task LoadIlluminationStateAsync(CancellationToken ct)
    {
        var state = await _illumination.GetStateAsync(_session, ct);
        ApplyIlluminationStateToInputs(state);
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
        var states = await _session.GetMotionStateAsync(ct);
        ApplyMotionStateToInputs(states);
    }

    private void ApplyIlluminationStateToInputs(ScanIlluminationState state)
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
        IlluminationSummaryText = BuildIlluminationSummary(state);
    }

    private void ApplyProfileAcquisitionSettings(ScanFilmAcquisitionSettings settings)
    {
        var normalized = settings.Normalize();
        ApplyIlluminationStateToInputs(new ScanIlluminationState(
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
        MotorIntervalUs = normalized.MotorIntervalUs.ToString(CultureInfo.InvariantCulture);
        _isMotorDistanceDerivedFromInterval = true;
        RefreshDerivedMotorDistanceFromCurrentInterval();
        ApplyMotorSpeedFromInterval(1, normalized.MotorIntervalUs);
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
        ApplyMotorSpeedFromInterval(0, ScanDebugConstants.MotionDefaultIntervalUs);
        ApplyMotorSpeedFromInterval(1, ScanDebugConstants.MotionDefaultIntervalUs);
        ApplyMotorSpeedFromInterval(2, ScanDebugConstants.MotionDefaultIntervalUs);
        MotorIntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString(CultureInfo.InvariantCulture);
        _isMotorDistanceDerivedFromInterval = true;
        RefreshDerivedMotorDistanceFromCurrentInterval();
        MotionSummaryText = "ScanDebug_Runtime_MotionSummaryIdle".GetLocalized();
        UpdateComputedMotorSummary();
    }

    private bool TryBuildIlluminationRequest(out IlluminationRequest request, out string error)
    {
        request = new IlluminationRequest(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
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

        IsApplyingMotion = true;
        try
        {
            StatusText = enabled ? "ScanDebug_Runtime_StatusMotorEnabling".GetLocalizedFormat(motorName) : "ScanDebug_Runtime_StatusMotorDisabling".GetLocalizedFormat(motorName);
            await _sessionCoordinator.UseConnectedSessionAsync(
                async (session, token) =>
                {
                    await session.SetMotorEnabledAsync(motorId, enabled, token);
                    return true;
                },
                CancellationToken.None);
            await LoadMotionStateAsync(_session.ConnectionToken);
            StatusText = enabled ? "ScanDebug_Runtime_StatusMotorEnabled".GetLocalizedFormat(motorName) : "ScanDebug_Runtime_StatusMotorDisabled".GetLocalizedFormat(motorName);
        }
        catch (OperationCanceledException)
        {
            StatusText = enabled ? "ScanDebug_Runtime_StatusMotorEnableCanceled".GetLocalizedFormat(motorName) : "ScanDebug_Runtime_StatusMotorDisableCanceled".GetLocalizedFormat(motorName);
        }
        catch (Exception ex)
        {
            StatusText = enabled ? "ScanDebug_Runtime_StatusMotorEnableFailed".GetLocalizedFormat(motorName, ex.Message) : "ScanDebug_Runtime_StatusMotorDisableFailed".GetLocalizedFormat(motorName, ex.Message);
        }
        finally
        {
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
            || !TryBuildMotorIntervalFromInputs(motorId, speedValueText, speedUnitText, out var intervalUs, out error))
        {
            return false;
        }

        request = new MotorMoveRequest(direction, steps, intervalUs);
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

    private bool TryBuildMotorIntervalFromInputs(byte motorId, string speedValueText, string speedUnitText, out uint intervalUs, out string error)
    {
        intervalUs = 0;
        var unit = NormalizeMotorUnit(speedUnitText);
        var displayMotorId = motorId + 1;

        if (unit == MotorUnitSteps && TryGetDerivedMotorIntervalUs(motorId, out var preservedIntervalUs))
        {
            intervalUs = preservedIntervalUs;
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
            if (!double.IsFinite(computed) || computed < ScanDebugConstants.MotionMinIntervalUs || computed > uint.MaxValue)
            {
                error = "ScanDebug_Runtime_ErrorMotorSpeedTooHigh".GetLocalizedFormat(displayMotorId, ScanDebugConstants.MotionMinIntervalUs);
                return false;
            }

            intervalUs = (uint)computed;
            error = string.Empty;
            return true;
        }

        var speedMmPerSecond = unit == MotorUnitMicrometers ? speedValue / 1000.0 : speedValue;
        if (!ScanTimingMath.TryConvertMillimetersPerSecondToMotorIntervalUs(speedMmPerSecond, _deviceSettings.Settings.GetMotorSettings(motorId), ScanDebugConstants.MotionMinIntervalUs, out intervalUs))
        {
            error = "ScanDebug_Runtime_ErrorMotorSpeedTooHigh".GetLocalizedFormat(displayMotorId, ScanDebugConstants.MotionMinIntervalUs);
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

    private static string FormatMotorSpeedStepsPerSecond(uint intervalUs)
        => ScanTimingMath.ConvertMotorIntervalToStepsPerSecond(intervalUs).ToString("0.###", CultureInfo.InvariantCulture);

    private async Task EnsureDeviceSettingsInitializedAsync()
    {
        await _deviceSettingsInitializationTask;
        RefreshActiveIlluminationChannels();
    }

    private string[] GetEffectiveDeviceChannelRoles()
        => _deviceSettings.Settings.Normalize().ChannelRoles.ToArray();

    private string GetBoundLedName(string channelRole)
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
    }

    internal string GetAcquisitionChannelCalibrationStatusText(string role)
        => _channelProfiles.TryGetProfile(role, out _)
            ? "ScanDebug_ProfileChannelOverviewCalibrationSaved".GetLocalized()
            : "ScanDebug_ProfileChannelOverviewCalibrationMissing".GetLocalized();

    internal void RequestCalibrationForAcquisitionChannel(string role)
    {
        SelectedCalibrationChannel = role;
        CalibrationSectionRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool HasSelectedAcquisitionChannels()
        => AcquisitionChannels.Any(channel => channel.IsSelected);

    private bool AreAllActiveAcquisitionChannelsConfirmed()
        => AcquisitionChannels.Count > 0 && AcquisitionChannels.All(channel => channel.IsSelected);

    private ScanDebugAcquisitionChannelViewModel[] GetSelectedAcquisitionChannelsForSummary()
    {
        var selected = AcquisitionChannels
            .Where(channel => channel.IsSelected)
            .OrderBy(channel => channel.LedIndex)
            .ToArray();

        if (IsMultiChannelScanEnabled || selected.Length <= 1)
            return selected;

        return selected
            .Where(channel => string.Equals(channel.Role, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase))
            .DefaultIfEmpty(selected[0])
            .Take(1)
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

    private static bool IsActiveIlluminationRole(string role)
        => !string.Equals(role, "Unused", StringComparison.OrdinalIgnoreCase);

    private void OnMotorSpeedInputChanged(byte motorId)
    {
        if (_isApplyingDerivedMotorSpeed)
            return;

        SetMotorSpeedDerivedFromInterval(motorId, false);
    }

    private void ApplyMotorSpeedFromInterval(byte motorId, uint intervalUs)
    {
        _isApplyingDerivedMotorSpeed = true;
        try
        {
            var speedValue = FormatMotorSpeedStepsPerSecond(intervalUs);
            switch (motorId)
            {
                case 0:
                    Motor1SpeedValue = speedValue;
                    Motor1SpeedUnit = MotorUnitSteps;
                    Motor1IntervalUs = intervalUs.ToString(CultureInfo.InvariantCulture);
                    break;
                case 1:
                    Motor2SpeedValue = speedValue;
                    Motor2SpeedUnit = MotorUnitSteps;
                    Motor2IntervalUs = intervalUs.ToString(CultureInfo.InvariantCulture);
                    break;
                case 2:
                    Motor3SpeedValue = speedValue;
                    Motor3SpeedUnit = MotorUnitSteps;
                    Motor3IntervalUs = intervalUs.ToString(CultureInfo.InvariantCulture);
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

    private bool TryGetDerivedMotorIntervalUs(byte motorId, out uint intervalUs)
    {
        intervalUs = 0;
        if (!IsMotorSpeedDerivedFromInterval(motorId))
            return false;

        var intervalText = motorId switch
        {
            0 => Motor1IntervalUs,
            1 => Motor2IntervalUs,
            2 => Motor3IntervalUs,
            _ => throw new ArgumentOutOfRangeException(nameof(motorId))
        };

        return uint.TryParse(intervalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out intervalUs)
            && intervalUs >= ScanDebugConstants.MotionMinIntervalUs;
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

    private bool TryBuildAutofocusRequest(out ScanAutofocusRequest request, out string error)
    {
        request = new ScanAutofocusRequest(0, 0, 0, 0, false, false, 0, 0, ScanCalibrationRoiSettings.CreateDefault());

        if (!int.TryParse(AutofocusSampleRows, out var sampleRows) || sampleRows <= 0 || sampleRows > _session.SingleTransferMaxRows)
        {
            error = "ScanDebug_Runtime_ErrorAutofocusRowsRange".GetLocalizedFormat(_session.SingleTransferMaxRows);
            return false;
        }

        if (!double.TryParse(AutofocusTiltProbeSteps, NumberStyles.Float, CultureInfo.InvariantCulture, out var tiltProbeMm)
            || !ScanTimingMath.TryConvertMillimetersToMotorSteps(tiltProbeMm, _deviceSettings.Settings.GetMotorSettings(0), out var tiltSteps))
        {
            error = "ScanDebug_Runtime_ErrorAutofocusTiltPositive".GetLocalized();
            return false;
        }

        if (!double.TryParse(AutofocusZProbeSteps, NumberStyles.Float, CultureInfo.InvariantCulture, out var zProbeMm)
            || !ScanTimingMath.TryConvertMillimetersToMotorSteps(zProbeMm, _deviceSettings.Settings.GetMotorSettings(2), out var zSteps))
        {
            error = "ScanDebug_Runtime_ErrorAutofocusZPositive".GetLocalized();
            return false;
        }

        if (!uint.TryParse(AutofocusMotorIntervalUs, out var intervalUs) || intervalUs < ScanDebugConstants.MotionMinIntervalUs)
        {
            error = "ScanDebug_Runtime_ErrorAutofocusIntervalMinimum".GetLocalizedFormat(ScanDebugConstants.MotionMinIntervalUs);
            return false;
        }

        request = new ScanAutofocusRequest(
            sampleRows,
            tiltSteps,
            zSteps,
            intervalUs,
            string.Equals(AutofocusZDirection, MotorDirectionLabels[1], StringComparison.Ordinal),
            string.Equals(AutofocusTiltDirection, MotorDirectionLabels[1], StringComparison.Ordinal),
            MaxTiltIterations: 8,
            MaxZIterations: 10,
            RoiSettings: _roiSettings.Normalize());
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

        return "ScanDebug_Runtime_MotorStatus".GetLocalizedFormat(FormatBool(state.Enabled), FormatBool(state.Running), FormatDirection(state.Direction), state.Diag != 0 ? "ScanDebug_Runtime_DiagHigh".GetLocalized() : "ScanDebug_Runtime_DiagLow".GetLocalized(), state.IntervalUs, state.RemainingSteps);
    }

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

    private sealed record MotorMoveRequest(bool Direction, uint Steps, uint IntervalUs);

    private bool RenderPreview(int rows)
    {
        if (_lastWorkflowResult is not null)
        {
            if (!_channelImages.TryBuildRgbComposite(_lastWorkflowResult, BuildDebugChannelAssignment(), BuildDebugColorManagementOptions(), ScanChannelAlignmentMode.Ecc, null, out var compositeFrame, out var compositeError, _channelProfiles.Profiles, IsWhiteLevelPreviewEnabled) || compositeFrame is null)
            {
                StatusText = compositeError;
                return false;
            }

            PreviewFrame = CreatePreviewFrame(compositeFrame.Buffer);
            OnPropertyChanged(nameof(CanEditRoiSelection));
            OnPropertyChanged(nameof(CanEditColumnSampleSelection));
            RefreshRoiStatus();
            RefreshColumnSampleStatus();
            return true;
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

    private bool TryParsePreviewGamma(out double gamma)
        => double.TryParse(PreviewGamma, NumberStyles.Float, CultureInfo.InvariantCulture, out gamma);

    private bool TryGetSelectedPreviewWhiteLevel(out ushort whiteLevel)
    {
        whiteLevel = 0;
        return _channelProfiles.TryGetProfile(SelectedCalibrationChannel, out var profile)
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
        => await EnsureDeviceSettingsInitializedAsync();

    public async Task DeactivateAsync()
    {
        await Task.CompletedTask;
        DetachRuntimeBindings();
        ClearPreview();
        IsScanReadProgressVisible = false;
    }

    public async Task CleanupAsync()
    {
        await DeactivateAsync();
    }
}
