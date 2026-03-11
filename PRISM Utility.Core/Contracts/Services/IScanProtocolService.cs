using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanProtocolService
{
    byte[] BuildSetScanLinesCommand(int rows);
    byte[] BuildStartScanCommand();
    byte[] BuildStopScanCommand();
    byte[] BuildWarmUpCommand();
    byte[] BuildGetParamByHashCommand(uint keyHash);
    byte[] BuildSetParamByHashCommand(uint keyHash, ushort value);
    byte[] BuildSetParamByHashCommand(uint keyHash, uint value);

    ScanAck ParseScanAck(ScanControlFrame frame);
    ushort ParseU16ParamPayload(byte[] payload, uint expectedKeyHash, string paramName);
    uint ParseU32ParamPayload(byte[] payload, uint expectedKeyHash, string paramName);
    uint ComputeParamKeyHash(string key);
    void EnsureAckOk(ScanAck ack, int expectedRows, string commandName);
    string MapStatus(byte status);
    bool IsIoTimeout(IOException ex);
    bool TryDequeueControlFrame(List<byte> ackReadBuffer, out ScanControlFrame frame);
}
