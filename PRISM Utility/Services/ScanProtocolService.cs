using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Models;

namespace PRISM_Utility.Services;

public class ScanProtocolService : IScanProtocolService
{
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

            if (ackReadBuffer.Count < 4)
                break;

            if (TryParseU16LengthFrame(ackReadBuffer, out frame))
                return true;

            break;
        }

        frame = default!;
        return false;
    }

    private static void WritePayloadLength(byte[] frame, ushort payloadLength)
    {
        frame[2] = (byte)(payloadLength & 0xFF);
        frame[3] = (byte)(payloadLength >> 8);
    }

    private static bool TryParseU16LengthFrame(List<byte> ackReadBuffer, out ScanControlFrame frame)
    {
        frame = default!;
        if (ackReadBuffer.Count < 5)
            return false;

        var payloadLength = ackReadBuffer[3] | (ackReadBuffer[4] << 8);
        var frameLength = 5 + payloadLength;
        if (ackReadBuffer.Count < frameLength)
            return false;

        var opcode = ackReadBuffer[1];
        var status = ackReadBuffer[2];
        var payload = ackReadBuffer.GetRange(5, payloadLength).ToArray();
        ackReadBuffer.RemoveRange(0, frameLength);
        frame = new ScanControlFrame(opcode, status, payload);
        return true;
    }

}
