using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public class ScanProtocolService : IScanProtocolService
{
    public byte[] BuildGetIlluminationStateCommand()
    {
        var frame = new byte[4];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdIlluminationGetState;
        WritePayloadLength(frame, 0);
        return frame;
    }

    public byte[] BuildSetIlluminationLevelsCommand(ushort led1Level, ushort led2Level, ushort led3Level, ushort led4Level)
    {
        var frame = new byte[4 + ScanDebugConstants.IlluminationSetLevelsPayloadLength];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdIlluminationSetLevels;
        WritePayloadLength(frame, ScanDebugConstants.IlluminationSetLevelsPayloadLength);
        WriteUInt16(frame, 4, led1Level);
        WriteUInt16(frame, 6, led2Level);
        WriteUInt16(frame, 8, led3Level);
        WriteUInt16(frame, 10, led4Level);
        return frame;
    }

    public byte[] BuildSetSteadyIlluminationCommand(byte steadyMask)
    {
        EnsureLedMask(steadyMask, nameof(steadyMask));

        var frame = new byte[4 + ScanDebugConstants.IlluminationMaskPayloadLength];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdIlluminationSetSteady;
        WritePayloadLength(frame, ScanDebugConstants.IlluminationMaskPayloadLength);
        frame[4] = steadyMask;
        return frame;
    }

    public byte[] BuildConfigureExposureLightingCommand(byte syncMask)
    {
        EnsureLedMask(syncMask, nameof(syncMask));

        var frame = new byte[4 + ScanDebugConstants.IlluminationMaskPayloadLength];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdIlluminationConfigSync;
        WritePayloadLength(frame, ScanDebugConstants.IlluminationMaskPayloadLength);
        frame[4] = syncMask;
        return frame;
    }

    public byte[] BuildSetSyncPulseClocksCommand(uint led1PulseClock, uint led2PulseClock, uint led3PulseClock, uint led4PulseClock)
    {
        var frame = new byte[4 + ScanDebugConstants.IlluminationSetSyncPulsePayloadLength];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdIlluminationSetSyncPulse;
        WritePayloadLength(frame, ScanDebugConstants.IlluminationSetSyncPulsePayloadLength);
        WriteUInt32(frame, 4, led1PulseClock);
        WriteUInt32(frame, 8, led2PulseClock);
        WriteUInt32(frame, 12, led3PulseClock);
        WriteUInt32(frame, 16, led4PulseClock);
        return frame;
    }

    public byte[] BuildGetMotionStateCommand()
    {
        var frame = new byte[4];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdMotionGetState;
        WritePayloadLength(frame, 0);
        return frame;
    }

    public byte[] BuildSetMotorEnableCommand(byte motorId, bool enabled)
    {
        EnsureMotorId(motorId, nameof(motorId));

        var frame = new byte[4 + ScanDebugConstants.MotionSetEnablePayloadLength];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdMotionSetEnable;
        WritePayloadLength(frame, ScanDebugConstants.MotionSetEnablePayloadLength);
        frame[4] = motorId;
        frame[5] = enabled ? (byte)1 : (byte)0;
        return frame;
    }

    public byte[] BuildMoveMotorStepsCommand(byte motorId, bool direction, uint steps, uint intervalUs)
    {
        EnsureMotorId(motorId, nameof(motorId));

        var frame = new byte[4 + ScanDebugConstants.MotionMoveStepsPayloadLength];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdMotionMoveSteps;
        WritePayloadLength(frame, ScanDebugConstants.MotionMoveStepsPayloadLength);
        frame[4] = motorId;
        frame[5] = direction ? (byte)1 : (byte)0;
        WriteUInt32(frame, 6, steps);
        WriteUInt32(frame, 10, intervalUs);
        return frame;
    }

    public byte[] BuildPrepareMotorOnSyncCommand(byte motorId, bool direction, uint steps, uint intervalUs)
    {
        EnsureMotorId(motorId, nameof(motorId));

        var frame = new byte[4 + ScanDebugConstants.MotionMoveStepsPayloadLength];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdMotionPrepareOnSync;
        WritePayloadLength(frame, ScanDebugConstants.MotionMoveStepsPayloadLength);
        frame[4] = motorId;
        frame[5] = direction ? (byte)1 : (byte)0;
        WriteUInt32(frame, 6, steps);
        WriteUInt32(frame, 10, intervalUs);
        return frame;
    }

    public byte[] BuildStopMotorCommand(byte motorId)
    {
        EnsureMotorId(motorId, nameof(motorId));

        var frame = new byte[4 + ScanDebugConstants.MotionSingleMotorPayloadLength];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdMotionStop;
        WritePayloadLength(frame, ScanDebugConstants.MotionSingleMotorPayloadLength);
        frame[4] = motorId;
        return frame;
    }

    public byte[] BuildApplyMotorConfigCommand(byte motorId)
    {
        EnsureMotorId(motorId, nameof(motorId));

        var frame = new byte[4 + ScanDebugConstants.MotionSingleMotorPayloadLength];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdMotionApplyConfig;
        WritePayloadLength(frame, ScanDebugConstants.MotionSingleMotorPayloadLength);
        frame[4] = motorId;
        return frame;
    }

    public byte[] BuildSetScanLinesCommand(int rows)
    {
        var payload = new byte[8];
        payload[0] = ScanDebugConstants.HostFrameSof;
        payload[1] = ScanDebugConstants.UsbCmdSetScanLines;
        WritePayloadLength(payload, 4);
        var rowBytes = BitConverter.GetBytes((uint)rows);
        Buffer.BlockCopy(rowBytes, 0, payload, 4, rowBytes.Length);
        return payload;
    }

    public byte[] BuildStartScanCommand()
    {
        var frame = new byte[4];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdStartScan;
        WritePayloadLength(frame, 0);
        return frame;
    }

    public byte[] BuildStopScanCommand()
    {
        var frame = new byte[4];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdStopScan;
        WritePayloadLength(frame, 0);
        return frame;
    }

    public byte[] BuildWarmUpCommand()
    {
        var frame = new byte[4];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdWarmUp;
        WritePayloadLength(frame, 0);
        return frame;
    }

    public byte[] BuildGetParamByHashCommand(uint keyHash)
    {
        var frame = new byte[8];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdGetParamByHash;
        WritePayloadLength(frame, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(keyHash), 0, frame, 4, 4);
        return frame;
    }

    public byte[] BuildSetParamByHashCommand(uint keyHash, ushort value)
    {
        var frame = new byte[12];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdSetParamByHash;
        WritePayloadLength(frame, 8);
        Buffer.BlockCopy(BitConverter.GetBytes(keyHash), 0, frame, 4, 4);
        frame[8] = ScanDebugConstants.PrismParamTypeU16;
        frame[9] = ScanDebugConstants.PrismParamValueLenU16;
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, frame, 10, 2);
        return frame;
    }

    public byte[] BuildSetParamByHashCommand(uint keyHash, uint value)
    {
        var frame = new byte[14];
        frame[0] = ScanDebugConstants.HostFrameSof;
        frame[1] = ScanDebugConstants.UsbCmdSetParamByHash;
        WritePayloadLength(frame, 10);
        Buffer.BlockCopy(BitConverter.GetBytes(keyHash), 0, frame, 4, 4);
        frame[8] = ScanDebugConstants.PrismParamTypeU32;
        frame[9] = ScanDebugConstants.PrismParamValueLenU32;
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, frame, 10, 4);
        return frame;
    }

    public ScanIlluminationState ParseIlluminationStatePayload(byte[] payload)
    {
        if (payload.Length != ScanDebugConstants.IlluminationGetStatePayloadLength)
            throw new IOException($"Illumination state payload length invalid: {payload.Length}");

        return new ScanIlluminationState(
            BitConverter.ToUInt16(payload, 0),
            BitConverter.ToUInt16(payload, 2),
            BitConverter.ToUInt16(payload, 4),
            BitConverter.ToUInt16(payload, 6),
            payload[8],
            payload[9],
            payload[10],
            BitConverter.ToUInt32(payload, 12),
            BitConverter.ToUInt32(payload, 16),
            BitConverter.ToUInt32(payload, 20),
            BitConverter.ToUInt32(payload, 24));
    }

    public IReadOnlyList<ScanMotorState> ParseMotionStatePayload(byte[] payload)
    {
        if (payload.Length != ScanDebugConstants.MotionGetStatePayloadLength)
            throw new IOException($"Motion state payload length invalid: {payload.Length}");

        var motors = new List<ScanMotorState>(ScanDebugConstants.MotionMotorCount);
        for (var index = 0; index < ScanDebugConstants.MotionMotorCount; index++)
        {
            var offset = index * ScanDebugConstants.MotionMotorStatePayloadLength;
            motors.Add(ParseMotionEntry(payload, offset));
        }

        return motors;
    }

    public ScanMotorState ParseMotionEventPayload(byte[] payload)
    {
        if (payload.Length != ScanDebugConstants.MotionMotorStatePayloadLength)
            throw new IOException($"Motion event payload length invalid: {payload.Length}");

        return ParseMotionEntry(payload, 0);
    }

    public ScanAck ParseScanAck(ScanControlFrame frame)
    {
        if (frame.Payload.Length != 8)
            throw new IOException($"Scan ACK payload length invalid: {frame.Payload.Length}");

        var target = BitConverter.ToUInt32(frame.Payload, 0);
        var completed = BitConverter.ToUInt32(frame.Payload, 4);
        return new ScanAck(frame.Opcode, frame.Status, target, completed);
    }

    public ushort ParseU16ParamPayload(byte[] payload, uint expectedKeyHash, string paramName)
    {
        if (payload.Length != 8)
            throw new IOException($"{paramName}: payload length invalid ({payload.Length})");

        var keyHash = BitConverter.ToUInt32(payload, 0);
        if (keyHash != expectedKeyHash)
            throw new IOException($"{paramName}: key hash mismatch. expected 0x{expectedKeyHash:X8}, actual 0x{keyHash:X8}");

        var valueType = payload[4];
        if (valueType != ScanDebugConstants.PrismParamTypeU16)
            throw new IOException($"{paramName}: type mismatch. expected {ScanDebugConstants.PrismParamTypeU16}, actual {valueType}");

        var valueLen = payload[5];
        if (valueLen != ScanDebugConstants.PrismParamValueLenU16)
            throw new IOException($"{paramName}: value length mismatch. expected {ScanDebugConstants.PrismParamValueLenU16}, actual {valueLen}");

        return BitConverter.ToUInt16(payload, 6);
    }

    public uint ParseU32ParamPayload(byte[] payload, uint expectedKeyHash, string paramName)
    {
        if (payload.Length != 10)
            throw new IOException($"{paramName}: payload length invalid ({payload.Length})");

        var keyHash = BitConverter.ToUInt32(payload, 0);
        if (keyHash != expectedKeyHash)
            throw new IOException($"{paramName}: key hash mismatch. expected 0x{expectedKeyHash:X8}, actual 0x{keyHash:X8}");

        var valueType = payload[4];
        if (valueType != ScanDebugConstants.PrismParamTypeU32)
            throw new IOException($"{paramName}: type mismatch. expected {ScanDebugConstants.PrismParamTypeU32}, actual {valueType}");

        var valueLen = payload[5];
        if (valueLen != ScanDebugConstants.PrismParamValueLenU32)
            throw new IOException($"{paramName}: value length mismatch. expected {ScanDebugConstants.PrismParamValueLenU32}, actual {valueLen}");

        return BitConverter.ToUInt32(payload, 6);
    }

    public uint ComputeParamKeyHash(string key)
    {
        uint hash = 2166136261;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(key))
        {
            hash ^= b;
            hash *= 16777619;
        }

        return hash;
    }

    public void EnsureAckOk(ScanAck ack, int expectedRows, string commandName)
    {
        if (ack.Status != 0x00)
            throw new IOException($"{commandName} ACK status: {MapStatus(ack.Status)} (0x{ack.Status:X2})");

        if (ack.Target != (uint)expectedRows)
            throw new IOException($"{commandName} ACK target mismatch: {ack.Target} (expected {expectedRows})");
    }

    public string MapStatus(byte status) => status switch
    {
        0x00 => "USB_STATUS_OK",
        0xE1 => "USB_STATUS_QUEUE_FULL",
        0xE2 => "USB_STATUS_BAD_FRAME",
        0xE3 => "USB_STATUS_FLASH_FAIL",
        0xE4 => "USB_STATUS_PARAM_NOT_FOUND",
        0xE5 => "USB_STATUS_SCAN_LINES_INVALID",
        0xE6 => "USB_STATUS_DEVICE_BUSY",
        0xE7 => "USB_STATUS_PARAM_TYPE_MISMATCH",
        0xE8 => "USB_STATUS_PARAM_LEN_INVALID",
        0xE9 => "USB_STATUS_PAYLOAD_INVALID",
        0xEA => "USB_STATUS_DEBUG_TARGET_UNSUPPORTED",
        0xEB => "USB_STATUS_SUBORDINATE_TIMEOUT",
        0xEC => "USB_STATUS_SUBORDINATE_LINK_ERROR",
        0xED => "USB_STATUS_RANGE_INVALID",
        0xEE => "USB_STATUS_HW_ERROR",
        _ => "USB_STATUS_UNKNOWN"
    };

    public bool IsIoTimeout(IOException ex)
        => ex.Message.Contains("IoTimedOut", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("Timed out waiting ACK frame", StringComparison.OrdinalIgnoreCase);

    public bool TryDequeueControlFrame(List<byte> ackReadBuffer, out ScanControlFrame frame)
    {
        while (ackReadBuffer.Count > 0)
        {
            var headerIndex = ackReadBuffer.IndexOf(ScanDebugConstants.DeviceFrameSof);
            if (headerIndex < 0)
            {
                ackReadBuffer.Clear();
                break;
            }

            if (headerIndex > 0)
                ackReadBuffer.RemoveRange(0, headerIndex);

            if (ackReadBuffer.Count < 5)
                break;

            var payloadLength = ackReadBuffer[3] | (ackReadBuffer[4] << 8);
            if (payloadLength > ScanDebugConstants.ControlFrameMaxPayloadBytes)
            {
                ackReadBuffer.RemoveAt(0);
                continue;
            }

            var frameLength = 5 + payloadLength;
            if (ackReadBuffer.Count < frameLength)
                break;

            var opcode = ackReadBuffer[1];
            var status = ackReadBuffer[2];
            var payload = ackReadBuffer.GetRange(5, payloadLength).ToArray();
            ackReadBuffer.RemoveRange(0, frameLength);
            frame = new ScanControlFrame(opcode, status, payload);
            return true;
        }

        frame = new ScanControlFrame(0, 0, Array.Empty<byte>());
        return false;
    }

    private static void WritePayloadLength(byte[] frame, ushort payloadLength)
    {
        frame[2] = (byte)(payloadLength & 0xFF);
        frame[3] = (byte)(payloadLength >> 8);
    }

    private static void EnsureLedMask(byte mask, string parameterName)
    {
        if ((mask & ~ScanDebugConstants.IlluminationValidMask) != 0)
            throw new ArgumentOutOfRangeException(parameterName, $"LED mask must only contain bits 0..3. Actual: 0x{mask:X2}");
    }

    private static void EnsureMotorId(byte motorId, string parameterName)
    {
        if (motorId >= ScanDebugConstants.MotionMotorCount)
            throw new ArgumentOutOfRangeException(parameterName, $"Motor id must be in [0, {ScanDebugConstants.MotionMotorCount - 1}]. Actual: {motorId}");
    }

    private static ScanMotorState ParseMotionEntry(byte[] payload, int offset)
    {
        var motorId = payload[offset];
        EnsureMotorId(motorId, $"payload[{offset}]");
        return new ScanMotorState(
            motorId,
            payload[offset + 1] != 0,
            payload[offset + 2] != 0,
            payload[offset + 3] != 0,
            payload[offset + 4],
            BitConverter.ToUInt16(payload, offset + 6),
            BitConverter.ToUInt32(payload, offset + 8));
    }

    private static void WriteUInt16(byte[] frame, int offset, ushort value)
        => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, frame, offset, sizeof(ushort));

    private static void WriteUInt32(byte[] frame, int offset, uint value)
        => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, frame, offset, sizeof(uint));
}
