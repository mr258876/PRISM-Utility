using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;
using PRISM_Utility.Models;

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

public partial class ScanDebugViewModel : ObservableRecipient
{
    private static readonly TimeSpan ParameterApplyDebounceWindow = TimeSpan.FromSeconds(1);
    private const double DefaultPreviewGamma = 2.2;
    private static readonly string[] IlluminationChannelLabels = { "LED1", "LED2", "LED3", "LED4" };
    private static readonly string[] MotorDirectionLabels = { "Dir0", "Dir1" };
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
    private const uint AutofocusStepsMin = 1;
    private static readonly Brush LimitBlockNormalBrush = new SolidColorBrush(Colors.DarkSeaGreen);
    private static readonly Brush LimitBlockAlertBrush = new SolidColorBrush(Colors.IndianRed);

    private readonly IScanSessionService _session;
    private readonly IScanParameterService _parameters;
    private readonly IScanImageDecoder _imageDecoder;
    private readonly IScanPreviewPresenter _previewPresenter;
    private readonly IScanBufferExportService _bufferExportService;
    private readonly IScanAutoCalibrationService _autoCalibration;
    private readonly IScanAutoFocusService _autoFocus;
    private readonly IScanTransferSettingsService _transferSettings;
    private readonly IScanChannelParameterProfileService _channelProfiles;
    private readonly IDebugOutputMirrorService _debugOutputMirror;
    private readonly IUsbUsageCoordinator _usbUsageCoordinator;
    private readonly IScanDebugSessionCoordinator _sessionCoordinator;
    private readonly IUiDispatcher _dispatcher;

    private CancellationTokenSource? _scanCts;
    private byte[] _lineBuffer = Array.Empty<byte>();
    private bool _hasValidScanBuffer;
    private DateTime _lastApplyParametersAtUtc = DateTime.MinValue;
    private bool _isDisposed;
    private bool _isMultiBufferedBulkInEnabled;
    private bool _suppressWarmUpToggleCommand;
    private bool _isNormalizingLimitInput;
    private bool _isUpdatingRoiInputs;
    private int _previewRows;
    private int _profileLoadVersion;
    private ScanFilmAcquisitionSettings? _selectedFilmAcquisitionSettings;
    private ScanCalibrationRoiSettings _roiSettings = ScanCalibrationRoiSettings.CreateDefault();

    public ObservableCollection<string> RowOptions { get; } = new() { "64", "128", "256", "512", "1024", "2048", "4096" };

    public ObservableCollection<string> MotorDirectionOptions { get; } = new(MotorDirectionLabels);

    public ObservableCollection<string> CalibrationChannelOptions { get; } = new() { "Red", "Green", "Blue", "White", "IR" };

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
    public partial bool IsWaterfallEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsWaterfallCompressedEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsGammaCorrectionEnabled { get; set; }

    [ObservableProperty]
    public partial string PreviewGamma { get; set; }

    [ObservableProperty]
    public partial string SelectedCalibrationChannel { get; set; }

    [ObservableProperty]
    public partial string CalibrationChannelStatusText { get; set; }

    [ObservableProperty]
    public partial string FilmProfileName { get; set; }

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
    [NotifyCanExecuteChangedFor(nameof(ExportBufferCommand))]
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
    public partial WriteableBitmap? PreviewImage { get; set; }

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

    public string AutofocusTiltProbeStepsLimitText => BuildLowerBoundLimitText(AutofocusTiltProbeSteps, AutofocusStepsMin, "Tilt probe steps");

    public string AutofocusZProbeStepsLimitText => BuildLowerBoundLimitText(AutofocusZProbeSteps, AutofocusStepsMin, "Z probe steps");

    public string AutofocusMotorIntervalLimitText => BuildLowerBoundLimitText(AutofocusMotorIntervalUs, ScanDebugConstants.MotionMinIntervalUs, "Motor interval");

    public Brush Adc1OffsetLimitBrush => BuildBoundedLimitBrush(Adc1Offset, CalibrationOffsetMin, CalibrationOffsetMax);

    public Brush Adc2OffsetLimitBrush => BuildBoundedLimitBrush(Adc2Offset, CalibrationOffsetMin, CalibrationOffsetMax);

    public Brush Adc1GainLimitBrush => BuildBoundedLimitBrush(Adc1Gain, CalibrationGainMin, CalibrationGainMax);

    public Brush Adc2GainLimitBrush => BuildBoundedLimitBrush(Adc2Gain, CalibrationGainMin, CalibrationGainMax);

    public Brush AutofocusSampleRowsLimitBrush => BuildBoundedLimitBrush(AutofocusSampleRows, AutofocusRowsMin, _session.SingleTransferMaxRows);

    public Brush AutofocusTiltProbeStepsLimitBrush => BuildLowerBoundLimitBrush(AutofocusTiltProbeSteps, AutofocusStepsMin);

    public Brush AutofocusZProbeStepsLimitBrush => BuildLowerBoundLimitBrush(AutofocusZProbeSteps, AutofocusStepsMin);

    public Brush AutofocusMotorIntervalLimitBrush => BuildLowerBoundLimitBrush(AutofocusMotorIntervalUs, ScanDebugConstants.MotionMinIntervalUs);

    public event EventHandler<ScanCalibrationPromptRequest>? CalibrationPromptRequested;

    public event EventHandler<ScanNoticeRequest>? NoticeRequested;

    public ScanDebugViewModel(IScanSessionService session, IScanParameterService parameters, IScanImageDecoder imageDecoder, IScanPreviewPresenter previewPresenter, IScanBufferExportService bufferExportService, IScanAutoCalibrationService autoCalibration, IScanAutoFocusService autoFocus, IScanTransferSettingsService transferSettings, IScanChannelParameterProfileService channelProfiles, IDebugOutputMirrorService debugOutputMirror, IUsbUsageCoordinator usbUsageCoordinator, IScanDebugSessionCoordinator sessionCoordinator, IUiDispatcher dispatcher)
    {
        _session = session;
        _parameters = parameters;
        _imageDecoder = imageDecoder;
        _previewPresenter = previewPresenter;
        _bufferExportService = bufferExportService;
        _autoCalibration = autoCalibration;
        _autoFocus = autoFocus;
        _transferSettings = transferSettings;
        _channelProfiles = channelProfiles;
        _debugOutputMirror = debugOutputMirror;
        _usbUsageCoordinator = usbUsageCoordinator;
        _sessionCoordinator = sessionCoordinator;
        _dispatcher = dispatcher;
        SelectedRows = "128";
        IsPreviewEnabled = true;
        IsWaterfallCompressedEnabled = true;
        IsGammaCorrectionEnabled = true;
        PreviewGamma = DefaultPreviewGamma.ToString("0.0");
        SelectedCalibrationChannel = CalibrationChannelOptions[0];
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
        IlluminationSummaryText = "ScanDebug_Runtime_IlluminationSummaryIdle".GetLocalized();
        MotionSummaryText = "ScanDebug_Runtime_MotionSummaryIdle".GetLocalized();
        Motor1StatusText = "ScanDebug_Runtime_MotorStatusIdle".GetLocalized();
        Motor2StatusText = "ScanDebug_Runtime_MotorStatusIdle".GetLocalized();
        Motor3StatusText = "ScanDebug_Runtime_MotorStatusIdle".GetLocalized();
        Motor1MoveDirection = MotorDirectionLabels[0];
        Motor2MoveDirection = MotorDirectionLabels[0];
        Motor3MoveDirection = MotorDirectionLabels[0];
        Motor1MoveSteps = "200";
        Motor2MoveSteps = "200";
        Motor3MoveSteps = "200";
        Motor1IntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString();
        Motor2IntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString();
        Motor3IntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString();
        AutofocusSampleRows = "128";
        AutofocusTiltProbeSteps = "40";
        AutofocusZProbeSteps = "20";
        AutofocusMotorIntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString();
        AutofocusZDirection = MotorDirectionLabels[0];
        AutofocusTiltDirection = MotorDirectionLabels[0];
        AutofocusSummaryText = "ScanDebug_Runtime_AutofocusIdle".GetLocalized();
        RefreshRoiStatus();

        _session.TargetsChanged += OnSessionTargetsChanged;
        _transferSettings.BulkInReadModeChanged += OnTransferSettingsChanged;
        _session.RefreshTargets();
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
        RefreshPreviewSelectionState();
        RefreshTargets();
        _ = InitializeTransferSettingsAsync();
    }

    partial void OnExposureTicksChanged(string value)
        => UpdateComputedParameterDisplays();

    partial void OnAdc1OffsetChanged(string value)
    {
        NormalizeBoundedIntInput(value, CalibrationOffsetMin, CalibrationOffsetMax, v => Adc1Offset = v);
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
    }

    partial void OnAdc2OffsetChanged(string value)
    {
        NormalizeBoundedIntInput(value, CalibrationOffsetMin, CalibrationOffsetMax, v => Adc2Offset = v);
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
    }

    partial void OnAdc1GainChanged(string value)
    {
        NormalizeBoundedIntInput(value, CalibrationGainMin, CalibrationGainMax, v => Adc1Gain = v);
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
    }

    partial void OnAdc2GainChanged(string value)
    {
        NormalizeBoundedIntInput(value, CalibrationGainMin, CalibrationGainMax, v => Adc2Gain = v);
        UpdateComputedParameterDisplays();
        RefreshLimitBlockBindings();
    }

    partial void OnSysClockKhzChanged(string value)
        => UpdateComputedParameterDisplays();

    partial void OnAutofocusSampleRowsChanged(string value)
    {
        NormalizeBoundedIntInput(value, AutofocusRowsMin, _session.SingleTransferMaxRows, v => AutofocusSampleRows = v);
        RefreshLimitBlockBindings();
    }

    partial void OnAutofocusTiltProbeStepsChanged(string value)
    {
        NormalizeLowerBoundUIntInput(value, AutofocusStepsMin, v => AutofocusTiltProbeSteps = v);
        RefreshLimitBlockBindings();
    }

    partial void OnAutofocusZProbeStepsChanged(string value)
    {
        NormalizeLowerBoundUIntInput(value, AutofocusStepsMin, v => AutofocusZProbeSteps = v);
        RefreshLimitBlockBindings();
    }

    partial void OnAutofocusMotorIntervalUsChanged(string value)
    {
        NormalizeLowerBoundUIntInput(value, ScanDebugConstants.MotionMinIntervalUs, v => AutofocusMotorIntervalUs = v);
        RefreshLimitBlockBindings();
    }

    partial void OnIsWarmUpEnabledChanged(bool value)
    {
        if (_suppressWarmUpToggleCommand)
            return;

        _ = HandleWarmUpToggleChangedAsync(value);
    }

    partial void OnSelectedRowsChanged(string value)
        => RefreshPreviewSelectionState();

    partial void OnSelectedCalibrationChannelChanged(string value)
    {
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
        => OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));

    partial void OnIsApplyingIlluminationChanged(bool value)
        => OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));

    partial void OnIsApplyingMotionChanged(bool value)
        => OnPropertyChanged(nameof(AreScanAcquisitionSettingsEditable));

    partial void OnIsPreviewEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPreviewToggleEnabled));
        OnPropertyChanged(nameof(IsPreviewEnabledForCurrentRows));
        OnPropertyChanged(nameof(CanEditRoiSelection));

        EnsureRoiEditModeAvailability();

        if (!value)
            ClearPreview();
        else if (_hasValidScanBuffer && _previewRows > 0 && !IsPreviewForcedOffForRows(_previewRows))
            RenderPreview(_previewRows);
    }

    partial void OnIsWaterfallEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditRoiSelection));
        EnsureRoiEditModeAvailability();
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

    public bool AreScanAcquisitionSettingsEditable =>
        !IsRunning &&
        !IsConnecting &&
        !IsApplyingParameters &&
        !IsAutoCalibrating &&
        !IsAutoFocusing &&
        !IsApplyingIllumination &&
        !IsApplyingMotion;

    public bool CanEditRoiSelection => PreviewImage is not null && !IsWaterfallEnabled && IsPreviewEnabled;

    public bool CanMutateRoiFromPreview => CanEditRoiSelection && IsRoiEditModeEnabled;

    private void OnSessionTargetsChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(RefreshTargets);

    private void OnTransferSettingsChanged(object? sender, EventArgs e)
        => _dispatcher.TryEnqueue(() =>
        {
            _isMultiBufferedBulkInEnabled = _transferSettings.Settings.ReadMode == ScanBulkInReadMode.MultiBuffered;
            StartScanCommand.NotifyCanExecuteChanged();
        });

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
    }

    private bool CanStartScan() =>
        !IsRunning &&
        !IsAutoFocusing &&
        !IsApplyingIllumination &&
        !IsApplyingMotion &&
        IsConnected &&
        TryParseRequestedRows(out _);

    private bool CanStopScan() => IsRunning;

    private bool CanExportBuffer() => !IsRunning && _hasValidScanBuffer && _lineBuffer.Length > 0;

    private bool CanConnectDevices() => IsDevicesPresent && !IsConnected && !IsConnecting;

    private bool CanDisconnectDevices() => IsConnected && !IsConnecting && !IsApplyingIllumination && !IsApplyingMotion && !IsAutoFocusing;

    private bool CanApplyParameters() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsApplyingParameters &&
        !IsApplyingIllumination &&
        !IsApplyingMotion &&
        !IsAutoCalibrating &&
        !IsAutoFocusing;

    private bool CanManageIllumination() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsApplyingParameters &&
        !IsAutoCalibrating &&
        !IsAutoFocusing &&
        !IsApplyingIllumination &&
        !IsApplyingMotion;

    private bool CanManageMotion() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsApplyingParameters &&
        !IsAutoCalibrating &&
        !IsAutoFocusing &&
        !IsApplyingIllumination &&
        !IsApplyingMotion;

    private bool CanRunAutoCalibration() =>
        IsConnected &&
        !IsConnecting &&
        !IsRunning &&
        !IsApplyingParameters &&
        !IsApplyingIllumination &&
        !IsApplyingMotion &&
        !IsAutoCalibrating &&
        !IsAutoFocusing;

    private bool CanRunAutoFocus() => CanRunAutoCalibration();

    [RelayCommand(CanExecute = nameof(CanExportBuffer))]
    private async Task ExportBuffer()
    {
        try
        {
            var file = await _bufferExportService.PickExportFileAsync(BuildExportBufferFileName());
            if (file is null)
            {
                StatusText = "ScanDebug_Runtime_StatusExportCanceled".GetLocalized();
                return;
            }

            await _bufferExportService.WriteBufferAsync(file, _lineBuffer);
            StatusText = "ScanDebug_Runtime_StatusBufferExported".GetLocalizedFormat(_lineBuffer.Length, file.Path);
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusExportFailed".GetLocalizedFormat(ex.Message);
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
            var result = await _sessionCoordinator.ConnectAsync(_session, CancellationToken.None);
            if (!result.Success)
            {
                StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message);
                return;
            }

            IsConnected = true;
            StatusText = "ScanDebug_Runtime_StatusLoadingParameters".GetLocalized();

            var statusNotes = new List<string>();

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
                var warmUpResult = await _sessionCoordinator.SetWarmUpAsync(_session, true, _session.ConnectionToken);
                statusNotes.Add(warmUpResult.Success ? "ScanDebug_Runtime_StatusWarmUpEnabled".GetLocalized() : "ScanDebug_Runtime_StatusWarmUpFailed".GetLocalizedFormat(ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(warmUpResult.Message)));
            }

            StatusText = statusNotes.Count > 0
                ? "ScanDebug_Runtime_StatusConnectedWithNotes".GetLocalizedFormat(string.Join(". ", statusNotes))
                : "ScanDebug_Runtime_StatusConnected".GetLocalized();
        }
        catch (Exception ex)
        {
            await _sessionCoordinator.DisconnectAsync(_session, CancellationToken.None);
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
                var warmUpResult = await _sessionCoordinator.SetWarmUpAsync(_session, false, _session.ConnectionToken);
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

            await _sessionCoordinator.DisconnectAsync(_session, CancellationToken.None);
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
            await _parameters.ApplyAsync(_session, snapshot, _session.ConnectionToken);
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

        if (!TryBuildIlluminationRequest(out var request, out var error))
        {
            StatusText = error;
            return;
        }

        IsApplyingIllumination = true;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusApplyingIllumination".GetLocalized();

            var currentState = await _session.GetIlluminationStateAsync(_session.ConnectionToken);
            if (currentState.SyncMask != 0)
                await _session.ConfigureExposureLightingAsync(0, _session.ConnectionToken);

            if (currentState.SteadyMask != 0)
                await _session.SetSteadyIlluminationAsync(0, _session.ConnectionToken);

            await _session.SetIlluminationLevelsAsync(request.Led1Level, request.Led2Level, request.Led3Level, request.Led4Level, _session.ConnectionToken);
            await _session.SetSyncPulseClocksAsync(request.Led1PulseClock, request.Led2PulseClock, request.Led3PulseClock, request.Led4PulseClock, _session.ConnectionToken);
            await _session.SetSteadyIlluminationAsync(request.SteadyMask, _session.ConnectionToken);
            await _session.ConfigureExposureLightingAsync(request.SyncMask, _session.ConnectionToken);

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

        if (!TryBuildMotorMoveRequest(motorId, out var request, out var error))
        {
            StatusText = error;
            return;
        }

        IsApplyingMotion = true;
        try
        {
            StatusText = "ScanDebug_Runtime_StatusMotorMoveStarting".GetLocalizedFormat(motorName);
            await _session.MoveMotorStepsAndWaitForCompletionAsync(motorId, request.Direction, request.Steps, request.IntervalUs, _session.ConnectionToken);
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
            await _session.StopMotorAsync(motorId, _session.ConnectionToken);
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
            await _session.ApplyMotorConfigAsync(motorId, _session.ConnectionToken);
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
            CalibrationChannelStatusText = removed
                ? "ScanDebug_Runtime_CalibrationChannel_ProfileCleared".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel))
                : "ScanDebug_Runtime_CalibrationChannel_NoSavedProfile".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel));
            StatusText = removed
                ? "ScanDebug_Runtime_StatusCalibrationChannelProfileCleared".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel))
                : "ScanDebug_Runtime_StatusCalibrationChannelNoSavedProfile".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel));
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
            return;
        }

        try
        {
            await SaveSelectedCalibrationProfileAsync(snapshot);
            if (!TryBuildFilmAcquisitionSettings(out var acquisitionSettings, out error))
            {
                StatusText = error;
                return;
            }

            await _channelProfiles.ExportProfilesAsync(new ScanFilmParameterProfileSet(
                2,
                string.IsNullOrWhiteSpace(FilmProfileName) ? "ScanDebug_Runtime_FilmProfileUntitled".GetLocalized() : FilmProfileName.Trim(),
                DateTimeOffset.Now,
                _channelProfiles.Profiles.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                SelectedCalibrationChannel,
                acquisitionSettings));
            StatusText = "ScanDebug_Runtime_StatusFilmProfileExported".GetLocalizedFormat(FilmProfileName);
        }
        catch (Exception ex)
        {
            StatusText = "ScanDebug_Runtime_StatusSaveFilmProfileFailed".GetLocalizedFormat(ex.Message);
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

            var channelToLoad = ResolveProfileChannelToLoad(imported);
            if (!string.IsNullOrWhiteSpace(channelToLoad))
            {
                SelectedCalibrationChannel = channelToLoad;
                await LoadSelectedCalibrationProfileAsync(channelToLoad, ++_profileLoadVersion);
            }

            StatusText = "ScanDebug_Runtime_StatusFilmProfileLoaded".GetLocalizedFormat(imported.ProfileName);
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
                var disableWarmUpResult = await _session.SetWarmUpEnabledAsync(false, _session.ConnectionToken);
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
            AutofocusSummaryText = "ScanDebug_Runtime_AutofocusSampling".GetLocalizedFormat(request.SampleRows, request.TiltProbeSteps, request.ZProbeSteps);

            var result = await _autoFocus.AutoFocusAsync(
                _session,
                request,
                status => _dispatcher.TryEnqueue(() => StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(status)),
                (imageBytes, rows, phase) => _dispatcher.TryEnqueue(() => ShowCalibrationFrame(imageBytes, rows, phase)),
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
                    var restoreWarmUpResult = await _session.SetWarmUpEnabledAsync(true, _session.ConnectionToken);
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

        if (rows > _session.SingleTransferMaxRows && !CanRunExtendedScan())
        {
            await RequestNoticeAsync(
                "ScanDebug_Runtime_RowsLimitExceeded.Title".GetLocalized(),
                "ScanDebug_Runtime_RowsLimitExceeded.Content".GetLocalizedFormat(_session.SingleTransferMaxRows),
                "Shared_Dialog_Ok.CloseButtonText".GetLocalized());
            StatusText = "ScanDebug_Runtime_StatusRowsLimitExceeded".GetLocalizedFormat(_session.SingleTransferMaxRows);
            return;
        }

        if (!IsConnected)
        {
            StatusText = "ScanDebug_Runtime_StatusScannerNotConnected".GetLocalized();
            return;
        }

        _scanCts = new CancellationTokenSource();
        IsRunning = true;
        IsScanReadProgressVisible = true;
        ScanReadProgressValue = 0;
        ScanReadProgressMaximum = Math.Max(1, rows * ScanDebugConstants.BytesPerLine);
        StatusText = IsContinuousScanEnabled ? "ScanDebug_Runtime_StatusStartingContinuousScan".GetLocalized() : "ScanDebug_Runtime_StatusStartingScan".GetLocalized();

        try
        {
            if (IsContinuousScanEnabled)
                await RunContinuousScanLoopAsync(rows, _scanCts.Token);
            else
                await RunSingleScanAsync(rows, _scanCts.Token);
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
        var result = await _session.StopScanAsync(CancellationToken.None);
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

        if (IsWarmUpEnabled)
            return rows > 0;

        if (CanRunExtendedScan())
            return rows > 0;

        return rows > 0 && rows <= _session.SingleTransferMaxRows;
    }

    private async Task<ScanStartResult> RunScanAsync(int rows, CancellationToken ct)
    {
        if (CanRunExtendedScan() || !IsWarmUpEnabled || rows <= _session.SingleTransferMaxRows)
        {
            return await _session.StartScanAsync(
                rows,
                ct,
                status => _dispatcher.TryEnqueue(() => StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(status)),
                diagnostic => _debugOutputMirror.Mirror("ScanDebug.Diagnostic", diagnostic),
                ReportScanReadProgress);
        }

        return await _session.StartWarmUpSegmentedScanAsync(
            rows,
            ct,
                status => _dispatcher.TryEnqueue(() => StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(status)),
            diagnostic => _debugOutputMirror.Mirror("ScanDebug.Diagnostic", diagnostic),
            ReportScanReadProgress);
    }

    private void MirrorOutput(string source, string message)
        => _debugOutputMirror.Mirror(source, message);

    private async Task RunSingleScanAsync(int rows, CancellationToken ct)
    {
        var result = await RunScanAsync(rows, ct);

        StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(result.Message);
        if (!result.Success || result.ImageBytes is null)
            return;

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
            ApplyScanFrame(result.ImageBytes, rows, "ScanDebug_Runtime_StatusContinuousPreviewUpdated".GetLocalizedFormat(frameCount));
        }
    }

    private void ApplyScanFrame(byte[] imageBytes, int rows, string successStatus)
    {
        _lineBuffer = imageBytes;
        _previewRows = rows;
        _hasValidScanBuffer = true;
        ExportBufferCommand.NotifyCanExecuteChanged();

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
        if (_isDisposed)
            return;

        if (!IsConnected)
        {
            StatusText = enabled
                ? "ScanDebug_Runtime_StatusWarmUpWillEnableAfterConnect".GetLocalized()
                : "ScanDebug_Runtime_StatusWarmUpDisabled".GetLocalized();
            return;
        }

        try
        {
            var result = await _sessionCoordinator.SetWarmUpAsync(_session, enabled, _session.ConnectionToken);
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
            var calibrated = await operation(
                _session,
                snapshot,
                _roiSettings.Normalize(),
                RequestCalibrationPromptAsync,
                status => _dispatcher.TryEnqueue(() => StatusText = ScanRuntimeMessageLocalizer.LocalizeScanDebugStatus(status)),
                applied => _dispatcher.TryEnqueue(() => ApplySnapshotToInputs(applied)),
                (imageBytes, rows, phase) => _dispatcher.TryEnqueue(() => ShowCalibrationFrame(imageBytes, rows, phase)),
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
            CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_NoSavedProfile".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole));
            return;
        }

        ApplySnapshotToInputs(profile.Parameters);
        _roiSettings = profile.RoiSettings.Normalize();
        RefreshRoiStatus();
        CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_SavedProfileLoaded".GetLocalizedFormat(GetCalibrationChannelDisplayName(channelRole));
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

        await _channelProfiles.SaveProfileAsync(SelectedCalibrationChannel, new ScanChannelCalibrationProfile(snapshot, _roiSettings.Normalize()));
        CalibrationChannelStatusText = "ScanDebug_Runtime_CalibrationChannel_SavedAt".GetLocalizedFormat(GetCalibrationChannelDisplayName(SelectedCalibrationChannel), DateTime.Now.ToString("HH:mm:ss"));
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

        if (!uint.TryParse(Motor2IntervalUs, out var motorIntervalUs) || motorIntervalUs < ScanDebugConstants.MotionMinIntervalUs)
        {
            error = "ScanDebug_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat("ScanDebug_Motor2IntervalUsTextBox.Header".GetLocalized(), ScanDebugConstants.MotionMinIntervalUs);
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

        if (!_hasValidScanBuffer || PreviewImage is null || _lineBuffer.Length == 0 || !IsPreviewEnabled || IsWaterfallEnabled)
            return false;

        if (x < 0 || y < 0 || x >= PreviewImage.PixelWidth || y >= PreviewImage.PixelHeight)
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

    private int GetRoiEditingWidth()
        => PreviewImage?.PixelWidth > 0 ? PreviewImage.PixelWidth : ScanDebugConstants.DecodedPixelsPerLine;

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

    private string BuildExportBufferFileName()
        => _bufferExportService.BuildExportBufferFileName(SelectedRows, _lineBuffer.Length, DateTimeOffset.Now);

    private void NormalizeCurrentRoiSettings()
        => _roiSettings = _roiSettings.Normalize();

    private void EnsureRoiEditModeAvailability()
    {
        if (!CanEditRoiSelection && IsRoiEditModeEnabled)
            IsRoiEditModeEnabled = false;
    }

    private void RefreshRoiOverlayVisibility()
        => RoiOverlayVersion++;

    private void RefreshRoiStatus()
    {
        NormalizeCurrentRoiSettings();
        EnsureRoiEditModeAvailability();
        RefreshRoiInputTexts();
        RoiStatusText = BuildRoiStatusText();
        RoiOverlayVersion++;
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

    private void NormalizeBoundedIntInput(string text, int min, int max, Action<string> assign)
    {
        if (_isNormalizingLimitInput || string.IsNullOrWhiteSpace(text) || !int.TryParse(text, out var value))
            return;

        var clamped = Math.Clamp(value, min, max);
        if (clamped == value)
            return;

        _isNormalizingLimitInput = true;
        try
        {
            assign(clamped.ToString());
        }
        finally
        {
            _isNormalizingLimitInput = false;
        }
    }

    private void NormalizeLowerBoundUIntInput(string text, uint min, Action<string> assign)
    {
        if (_isNormalizingLimitInput || string.IsNullOrWhiteSpace(text) || !uint.TryParse(text, out var value) || value >= min)
            return;

        _isNormalizingLimitInput = true;
        try
        {
            assign(min.ToString());
        }
        finally
        {
            _isNormalizingLimitInput = false;
        }
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

    private static Brush BuildBoundedLimitBrush(string text, int min, int max)
        => int.TryParse(text, out var value) && value >= min && value <= max
            ? LimitBlockNormalBrush
            : LimitBlockAlertBrush;

    private static Brush BuildLowerBoundLimitBrush(string text, uint min)
        => uint.TryParse(text, out var value) && value >= min
            ? LimitBlockNormalBrush
            : LimitBlockAlertBrush;

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
    }

    private async Task LoadIlluminationStateAsync(CancellationToken ct)
    {
        var state = await _session.GetIlluminationStateAsync(ct);
        ApplyIlluminationStateToInputs(state);
    }

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
        Motor2IntervalUs = normalized.MotorIntervalUs.ToString();
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
        Motor1MoveSteps = "200";
        Motor2MoveSteps = "200";
        Motor3MoveSteps = "200";
        Motor1IntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString();
        Motor2IntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString();
        Motor3IntervalUs = ScanDebugConstants.MotionDefaultIntervalUs.ToString();
        MotionSummaryText = "ScanDebug_Runtime_MotionSummaryIdle".GetLocalized();
    }

    private bool TryBuildIlluminationRequest(out IlluminationRequest request, out string error)
    {
        request = new IlluminationRequest(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        if (!TryParseLedLevel(Led1Level, "ScanDebug_Led1LevelTextBox.Header".GetLocalized(), out var led1Level, out error)
            || !TryParseLedLevel(Led2Level, "ScanDebug_Led2LevelTextBox.Header".GetLocalized(), out var led2Level, out error)
            || !TryParseLedLevel(Led3Level, "ScanDebug_Led3LevelTextBox.Header".GetLocalized(), out var led3Level, out error)
            || !TryParseLedLevel(Led4Level, "ScanDebug_Led4LevelTextBox.Header".GetLocalized(), out var led4Level, out error)
            || !TryParsePulseClock(Led1PulseClock, "ScanDebug_Led1PulseTextBox.Header".GetLocalized(), out var led1PulseClock, out error)
            || !TryParsePulseClock(Led2PulseClock, "ScanDebug_Led2PulseTextBox.Header".GetLocalized(), out var led2PulseClock, out error)
            || !TryParsePulseClock(Led3PulseClock, "ScanDebug_Led3PulseTextBox.Header".GetLocalized(), out var led3PulseClock, out error)
            || !TryParsePulseClock(Led4PulseClock, "ScanDebug_Led4PulseTextBox.Header".GetLocalized(), out var led4PulseClock, out error))
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

        if (!ValidateSyncPulse(syncMask, 0x01, led1PulseClock, "ScanDebug_Led1PulseTextBox.Header".GetLocalized(), out error)
            || !ValidateSyncPulse(syncMask, 0x02, led2PulseClock, "ScanDebug_Led2PulseTextBox.Header".GetLocalized(), out error)
            || !ValidateSyncPulse(syncMask, 0x04, led3PulseClock, "ScanDebug_Led3PulseTextBox.Header".GetLocalized(), out error)
            || !ValidateSyncPulse(syncMask, 0x08, led4PulseClock, "ScanDebug_Led4PulseTextBox.Header".GetLocalized(), out error))
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
            await _session.SetMotorEnabledAsync(motorId, enabled, _session.ConnectionToken);
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

        var (directionText, stepsText, intervalText) = GetMotorMoveInputs(motorId);
        var direction = string.Equals(directionText, MotorDirectionLabels[1], StringComparison.Ordinal);

        if (!uint.TryParse(stepsText, out var steps) || steps == 0)
        {
            error = "ScanDebug_Runtime_ErrorMotorStepsPositive".GetLocalizedFormat(motorId + 1);
            return false;
        }

        if (!uint.TryParse(intervalText, out var intervalUs) || intervalUs < ScanDebugConstants.MotionMinIntervalUs)
        {
            error = "ScanDebug_Runtime_ErrorMotorIntervalMinimum".GetLocalizedFormat(motorId + 1, ScanDebugConstants.MotionMinIntervalUs);
            return false;
        }

        request = new MotorMoveRequest(direction, steps, intervalUs);
        error = string.Empty;
        return true;
    }

    private (string DirectionText, string StepsText, string IntervalText) GetMotorMoveInputs(byte motorId)
        => motorId switch
        {
            0 => (Motor1MoveDirection, Motor1MoveSteps, Motor1IntervalUs),
            1 => (Motor2MoveDirection, Motor2MoveSteps, Motor2IntervalUs),
            2 => (Motor3MoveDirection, Motor3MoveSteps, Motor3IntervalUs),
            _ => throw new ArgumentOutOfRangeException(nameof(motorId))
        };

    private bool TryBuildAutofocusRequest(out ScanAutofocusRequest request, out string error)
    {
        request = new ScanAutofocusRequest(0, 0, 0, 0, false, false, 0, 0, ScanCalibrationRoiSettings.CreateDefault());

        if (!int.TryParse(AutofocusSampleRows, out var sampleRows) || sampleRows <= 0 || sampleRows > _session.SingleTransferMaxRows)
        {
            error = "ScanDebug_Runtime_ErrorAutofocusRowsRange".GetLocalizedFormat(_session.SingleTransferMaxRows);
            return false;
        }

        if (!uint.TryParse(AutofocusTiltProbeSteps, out var tiltSteps) || tiltSteps == 0)
        {
            error = "ScanDebug_Runtime_ErrorAutofocusTiltPositive".GetLocalized();
            return false;
        }

        if (!uint.TryParse(AutofocusZProbeSteps, out var zSteps) || zSteps == 0)
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

    private static string GetCalibrationChannelDisplayName(string channelRole)
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
        var gamma = 1.0;
        if (IsGammaCorrectionEnabled && !double.TryParse(PreviewGamma, out gamma))
            gamma = double.NaN;

        if (!_previewPresenter.TryRender(
                _lineBuffer,
                rows,
                new ScanPreviewRenderOptions(IsWaterfallEnabled, IsWaterfallCompressedEnabled, IsGammaCorrectionEnabled, gamma),
                PreviewImage,
                out var bitmap,
                out var error))
        {
            StatusText = error;
            return false;
        }

        PreviewImage = bitmap;
        OnPropertyChanged(nameof(CanEditRoiSelection));
        RefreshRoiStatus();
        return true;
    }

    private void ClearPreview()
    {
        _previewPresenter.Reset();
        PreviewImage = null;
        OnPropertyChanged(nameof(CanEditRoiSelection));
        RefreshRoiStatus();
    }

    private void RefreshPreviewSelectionState()
    {
        OnPropertyChanged(nameof(IsPreviewToggleEnabled));
        OnPropertyChanged(nameof(IsPreviewEnabledForCurrentRows));
        OnPropertyChanged(nameof(CanEditRoiSelection));
    }

    private bool IsPreviewForcedOffForSelectedRows()
        => int.TryParse(SelectedRows, out var rows) && IsPreviewForcedOffForRows(rows);

    private static bool IsPreviewForcedOffForRows(int rows)
        => rows > ScanDebugConstants.MaxPreviewRows;

    public async Task CleanupAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _scanCts?.Cancel();

        if (IsConnected && IsWarmUpEnabled)
        {
            try
            {
                await _session.SetWarmUpEnabledAsync(false, CancellationToken.None);
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
            catch
            {
            }
        }

        await _session.DisposeAsync();
        _usbUsageCoordinator.SetScanDebugInUse(false);
        _session.TargetsChanged -= OnSessionTargetsChanged;
        _transferSettings.BulkInReadModeChanged -= OnTransferSettingsChanged;
        IsConnected = false;
        IsConnecting = false;
        IsRunning = false;
        IsApplyingParameters = false;
        IsApplyingIllumination = false;
        IsApplyingMotion = false;
        ResetIlluminationInputs();
        ResetMotionInputs();
    }
}
