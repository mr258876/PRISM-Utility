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
    public const byte UsbCmdMotionPrepareOnSync = 0x57;
    public const byte UsbEvtMotionComplete = 0x58;
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

    public static int DecodedPixelsPerLine
    {
        get
        {
            var usableBytes = BytesPerLine - LineBufferMarginLeft - LineBufferMarginRight;
            if (usableBytes < PackedGroupBytes)
                return 0;

            var packedGroupCount = usableBytes / PackedGroupBytes;
            return packedGroupCount * PackedGroupPixels;
        }
    }
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

public sealed record ScanColumnRange(int Start, int EndInclusive)
{
    public int Width => Math.Max(0, EndInclusive - Start + 1);

    public ScanColumnRange Normalize()
        => Start <= EndInclusive ? this : new ScanColumnRange(EndInclusive, Start);

    public ScanColumnRange Clamp(int width, int minSpan = 4)
    {
        if (width <= 0)
            return new ScanColumnRange(0, -1);

        var normalized = Normalize();
        var clampedStart = Math.Clamp(normalized.Start, 0, width - 1);
        var clampedEnd = Math.Clamp(normalized.EndInclusive, clampedStart, width - 1);
        var requiredEnd = Math.Min(width - 1, clampedStart + Math.Max(minSpan - 1, 0));
        if (clampedEnd < requiredEnd)
        {
            clampedEnd = requiredEnd;
            if (clampedEnd >= width)
            {
                clampedEnd = width - 1;
                clampedStart = Math.Max(0, clampedEnd - Math.Max(minSpan - 1, 0));
            }
        }

        return new ScanColumnRange(clampedStart, clampedEnd);
    }
}

public static class ScanDebugValidation
{
    public static bool TryNormalizeSnapshot(ScanParameterSnapshot snapshot, out ScanParameterSnapshot normalized)
    {
        var isValid = snapshot.Adc1Offset is >= -255 and <= 255
            && snapshot.Adc2Offset is >= -255 and <= 255
            && snapshot.Adc1Gain <= 63
            && snapshot.Adc2Gain <= 63
            && snapshot.SysClockKhz >= ScanDebugConstants.MinSysClockKhz
            && snapshot.ExposureTicks >= ScanDebugConstants.MinExposureTicks;

        normalized = new ScanParameterSnapshot(
            (ushort)Math.Clamp(snapshot.ExposureTicks, ScanDebugConstants.MinExposureTicks, ushort.MaxValue),
            Math.Clamp(snapshot.Adc1Offset, -255, 255),
            (ushort)Math.Clamp((int)snapshot.Adc1Gain, 0, 63),
            Math.Clamp(snapshot.Adc2Offset, -255, 255),
            (ushort)Math.Clamp((int)snapshot.Adc2Gain, 0, 63),
            Math.Max(snapshot.SysClockKhz, ScanDebugConstants.MinSysClockKhz));

        return isValid;
    }
}

public sealed record ScanCalibrationRoiSettings(
    ScanColumnRange EffectiveRange,
    ScanColumnRange ShieldRange,
    ScanColumnRange FocusLeftRange,
    ScanColumnRange FocusRightRange,
    ScanColumnRange FocusOverallRange)
{
    public static ScanCalibrationRoiSettings CreateDefault()
    {
        var effective = new ScanColumnRange(ScanDebugConstants.EffectivePixelStart, ScanDebugConstants.EffectivePixelEnd);
        return new ScanCalibrationRoiSettings(
            effective,
            new ScanColumnRange(ScanDebugConstants.ShieldPixelStart, ScanDebugConstants.ShieldPixelEnd),
            BuildRangeByRatio(effective, 0.14, 0.42),
            BuildRangeByRatio(effective, 0.58, 0.86),
            BuildRangeByRatio(effective, 0.14, 0.86));
    }

    public ScanCalibrationRoiSettings Clamp(int width)
    {
        var effective = EffectiveRange.Clamp(width);
        var shield = NormalizeShieldRange(ShieldRange.Clamp(width), effective, width);
        var left = ClampWithin(FocusLeftRange, effective);
        var right = ClampWithin(FocusRightRange, effective);

        if (left.Start > right.Start)
            (left, right) = (right, left);

        if (left.EndInclusive >= right.Start)
        {
            var seam = Math.Clamp((left.Start + right.EndInclusive) / 2, effective.Start, Math.Max(effective.EndInclusive - 1, effective.Start));
            left = new ScanColumnRange(left.Start, Math.Max(left.Start, seam)).Clamp(width);
            right = new ScanColumnRange(Math.Min(right.EndInclusive, seam + 1), right.EndInclusive).Clamp(width);
            left = ClampWithin(left, effective);
            right = ClampWithin(right, effective);

            if (left.EndInclusive >= right.Start)
            {
                var leftWidth = Math.Max(4, effective.Width / 5);
                var rightWidth = Math.Max(4, effective.Width / 5);
                left = new ScanColumnRange(effective.Start, Math.Min(effective.EndInclusive - 1, effective.Start + leftWidth - 1));
                right = new ScanColumnRange(Math.Max(left.EndInclusive + 1, effective.EndInclusive - rightWidth + 1), effective.EndInclusive);
            }
        }

        var overall = ClampWithin(FocusOverallRange, effective);
        overall = new ScanColumnRange(
            Math.Min(overall.Start, Math.Min(left.Start, right.Start)),
            Math.Max(overall.EndInclusive, Math.Max(left.EndInclusive, right.EndInclusive)));
        overall = ClampWithin(overall, effective);

        return new ScanCalibrationRoiSettings(effective, shield, left, right, overall);
    }

    public ScanCalibrationRoiSettings Normalize()
        => Clamp(ScanDebugConstants.DecodedPixelsPerLine);

    private static ScanColumnRange ClampWithin(ScanColumnRange range, ScanColumnRange container)
    {
        var targetWidth = Math.Max(range.Width, 4);
        var maxStart = Math.Max(container.Start, container.EndInclusive - targetWidth + 1);
        var start = Math.Clamp(range.Normalize().Start, container.Start, maxStart);
        var end = Math.Min(container.EndInclusive, start + targetWidth - 1);
        return new ScanColumnRange(start, end).Clamp(container.EndInclusive + 1);
    }

    private static ScanColumnRange NormalizeShieldRange(ScanColumnRange shield, ScanColumnRange effective, int width)
    {
        if (width <= 0)
            return new ScanColumnRange(0, -1);

        var preferredWidth = Math.Max(shield.Width, 4);
        var beforeEnd = effective.Start - 1;
        if (beforeEnd >= 0)
        {
            var end = Math.Min(beforeEnd, shield.EndInclusive);
            var start = Math.Max(0, end - preferredWidth + 1);
            return new ScanColumnRange(start, end).Clamp(width);
        }

        var afterStart = effective.EndInclusive + 1;
        if (afterStart < width)
        {
            var start = Math.Clamp(shield.Start, afterStart, Math.Max(afterStart, width - preferredWidth));
            var end = Math.Min(width - 1, start + preferredWidth - 1);
            return new ScanColumnRange(start, end).Clamp(width);
        }

        return shield.Clamp(width);
    }

    private static ScanColumnRange BuildRangeByRatio(ScanColumnRange basis, double startRatio, double endRatio)
    {
        var normalized = basis.Normalize();
        var width = normalized.Width;
        if (width <= 0)
            return normalized;

        var start = normalized.Start + Math.Clamp((int)Math.Round(width * startRatio, MidpointRounding.ToZero), 0, Math.Max(width - 1, 0));
        var end = normalized.Start + Math.Clamp((int)Math.Round(width * endRatio, MidpointRounding.ToZero), 3, width - 1);
        return new ScanColumnRange(start, end).Clamp(normalized.EndInclusive + 1);
    }
}

public sealed record ScanChannelCalibrationProfile(ScanParameterSnapshot Parameters, ScanCalibrationRoiSettings RoiSettings);

public sealed record ScanAutofocusRequest(
    int SampleRows,
    uint TiltProbeSteps,
    uint ZProbeSteps,
    uint MotorIntervalUs,
    bool ZPositiveDirection,
    bool TiltPositiveDirection,
    int MaxTiltIterations,
    int MaxZIterations,
    ScanCalibrationRoiSettings RoiSettings);

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
