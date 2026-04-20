using PRISM_Utility.Core.Contracts.Services;

namespace PRISM_Utility.Core.Models;

public static class ScanDebugConstants
{
    public const ushort BulkInVid = 0x1D50;
    public const ushort BulkInPid = 0x619C;
    public const ushort BulkOutVid = 0x1D50;
    public const ushort BulkOutPid = 0x619D;
    public const byte BulkInEndpoint = 0x82;
    public const byte BulkOutEndpoint = 0x01;
    public const byte BulkOutAckEndpoint = 0x81;

    public const int BytesPerLine = 15206;
    public const int PackedGroupBytes = 4;
    public const int PackedGroupPixels = 2;
    public const int MaxRows = 137;
    public const int MaxPreviewRows = 8192;
    public const int WaterfallPreviewHeight = 512;
    public const int CalibrationSampleRows = 512;
    public const int ShieldPixelStart = 26;
    public const int ShieldPixelEnd = 115;
    public const int EffectivePixelStart = 128;
    public const int EffectivePixelEnd = 7577;
    public const int WhiteProbeSampleRows = 512;
    public const ushort MinExposureTicks = 0;
    public const uint MinSysClockKhz = 30_000;

    public const int AckTimeoutMs = 2000;
    public const int ImageReadTimeoutMs = 30000;
    public const int StartScanDoneAckWaitAfterImageMs = 5000;
    public const int AckReadSliceTimeoutMs = 1000;
    public const int AckReadChunkSize = 64;
    public const int ControlFrameMaxPayloadBytes = 64;
    public const int PreScanAckDrainWindowMs = 1000;
    public const int SetRowsAckTotalTimeoutMs = 4000;
    public const int StopAckTotalTimeoutMs = 4000;
    public const int FailureDoneAckWaitMs = 1500;

    public const int LineBufferMarginLeft = 2 + 4 * PackedGroupBytes;
    public const int LineBufferMarginRight = 4;
    public const int StartScanAckMonitorTotalTimeoutMs = 12000;
    public const int StartScanBusyMaxCount = 20;
    public const int BusyBackoffInitialMs = 100;
    public const int BusyBackoffMaxMs = 500;
    public const int IdleProbeWindowMs = 250;
    public const int IdleProbeRetryDelayMs = 200;
    public const int IdleProbeAttempts = 3;

    public const int ImageRingBufferSize = 2 * 1024 * 1024;
    public const int ImageReadChunkSize = 16 * 1024;
    public const int ImagePreStartDrainWindowMs = 300;
    public const int ImagePreStartDrainSliceTimeoutMs = 80;
    public const int ImageReadArmDelayMs = 30;
    public const int ImageReadArmMaxAttempts = 4;
    public const int ImageMultiBufferRequestLines = 16;
    public const int ImageMultiBufferRequestBytes = BytesPerLine * ImageMultiBufferRequestLines;
    public const int ImageMultiBufferOutstandingReads = 8;

    public const byte HostFrameSof = 0xA5;
    public const byte DeviceFrameSof = 0x5A;
    public const byte UsbCmdGetParamByHash = 0x20;
    public const byte UsbCmdSetParamByHash = 0x21;
    public const byte UsbCmdStartScan = 0x30;
    public const byte UsbCmdSetScanLines = 0x31;
    public const byte UsbCmdStopScan = 0x32;
    public const byte UsbCmdWarmUp = 0x33;
    public const byte UsbCmdIlluminationGetState = 0x40;
    public const byte UsbCmdIlluminationSetLevels = 0x41;
    public const byte UsbCmdIlluminationSetSteady = 0x42;
    public const byte UsbCmdIlluminationConfigSync = 0x43;
    public const byte UsbCmdIlluminationSetSyncPulse = 0x44;
    public const byte UsbCmdMotionGetState = 0x50;
    public const byte UsbCmdMotionSetEnable = 0x51;
    public const byte UsbCmdMotionMoveSteps = 0x52;
    public const byte UsbCmdMotionStop = 0x53;
    public const byte UsbCmdMotionApplyConfig = 0x54;
    public const byte PrismParamTypeU16 = 2;
    public const byte PrismParamValueLenU16 = 2;
    public const byte PrismParamTypeU32 = 3;
    public const byte PrismParamValueLenU32 = 4;

    public const int IlluminationChannelCount = 4;
    public const int IlluminationGetStatePayloadLength = 28;
    public const int IlluminationSetLevelsPayloadLength = 8;
    public const int IlluminationMaskPayloadLength = 4;
    public const int IlluminationSetSyncPulsePayloadLength = 16;
    public const byte IlluminationValidMask = 0x0F;
    public const uint IlluminationMinSyncPulseClock = 2;

    public const int MotionMotorCount = 3;
    public const int MotionMotorStatePayloadLength = 12;
    public const int MotionGetStatePayloadLength = MotionMotorCount * MotionMotorStatePayloadLength;
    public const int MotionSetEnablePayloadLength = 2;
    public const int MotionMoveStepsPayloadLength = 10;
    public const int MotionSingleMotorPayloadLength = 1;
    public const uint MotionMinIntervalUs = 10;
    public const uint MotionDefaultIntervalUs = 500;
}

public sealed record ScanControlFrame(byte Opcode, byte Status, byte[] Payload);

public sealed record ScanAck(byte Command, byte Status, uint Target, uint Completed);

public sealed record ScanTargetState(bool IsDevicesPresent, string? BulkInDeviceId, string? BulkOutDeviceId);

public sealed record ScanOperationResult(bool Success, string Message);

public sealed record ScanStartResult(bool Success, string Message, byte[]? ImageBytes);

public sealed record ScanStopResult(bool Success, string Message);

public sealed record ScanParameterDefinition(string DisplayName, string Key);

public sealed record ScanParameterSnapshot(ushort ExposureTicks, int Adc1Offset, ushort Adc1Gain, int Adc2Offset, ushort Adc2Gain, uint SysClockKhz);

public sealed record ScanIlluminationState(
    ushort Led1Level,
    ushort Led2Level,
    ushort Led3Level,
    ushort Led4Level,
    byte SteadyMask,
    byte SyncMask,
    byte SyncActiveMask,
    uint Led1PulseClock,
    uint Led2PulseClock,
    uint Led3PulseClock,
    uint Led4PulseClock);

public sealed record ScanMotorState(
    byte MotorId,
    bool Enabled,
    bool Running,
    bool Direction,
    byte Diag,
    ushort IntervalUs,
    uint RemainingSteps);

public sealed record ScanParameterDisplays(string ExposureTimeDisplay, string Adc1OffsetMvDisplay, string Adc2OffsetMvDisplay, string Adc1GainVvDisplay, string Adc2GainVvDisplay, string SysClockMhzDisplay);

public sealed record ScanCalibrationPrompt(string Title, string Content, string PrimaryButtonText, string CloseButtonText);

public sealed record ScanAutofocusRequest(
    int SampleRows,
    uint TiltProbeSteps,
    uint ZProbeSteps,
    uint MotorIntervalUs,
    bool ZPositiveDirection,
    bool TiltPositiveDirection,
    int MaxTiltIterations,
    int MaxZIterations);

public sealed record ScanAutofocusResult(
    int SampleRows,
    int FinalTiltOffsetSteps,
    int FinalZOffsetSteps,
    double FinalOverallSharpness,
    double FinalLeftSharpness,
    double FinalRightSharpness,
    double FinalTiltImbalance);

public sealed record ScanCalibrationStatistics(
    int Rows,
    int EffectivePixelCount,
    double[] ColumnMeans,
    double ShieldEvenMean,
    double ShieldOddMean,
    double EvenMean,
    double OddMean,
    double EffectiveMean,
    double MinColumnMean,
    double MaxColumnMean,
    double SaturationRatio,
    double DarkPixelRatio);

public sealed record ScanChannelMapping(bool IsAdc1Even, double Adc1Delta, double Adc2Delta);

public sealed record UsbPipeSelection(string DeviceId, byte ConfigId, byte InterfaceId, byte AltId, byte EndpointAddress);
