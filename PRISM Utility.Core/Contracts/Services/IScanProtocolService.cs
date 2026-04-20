using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanProtocolService
{
    byte[] BuildSetScanLinesCommand(int rows);
    byte[] BuildStartScanCommand();
    byte[] BuildStopScanCommand();
    byte[] BuildWarmUpCommand();
    byte[] BuildGetIlluminationStateCommand();
    byte[] BuildSetIlluminationLevelsCommand(ushort led1Level, ushort led2Level, ushort led3Level, ushort led4Level);
    byte[] BuildSetSteadyIlluminationCommand(byte steadyMask);
    byte[] BuildConfigureExposureLightingCommand(byte syncMask);
    byte[] BuildSetSyncPulseClocksCommand(uint led1PulseClock, uint led2PulseClock, uint led3PulseClock, uint led4PulseClock);
    byte[] BuildGetMotionStateCommand();
    byte[] BuildSetMotorEnableCommand(byte motorId, bool enabled);
    byte[] BuildMoveMotorStepsCommand(byte motorId, bool direction, uint steps, uint intervalUs);
    byte[] BuildStopMotorCommand(byte motorId);
    byte[] BuildApplyMotorConfigCommand(byte motorId);
    byte[] BuildGetParamByHashCommand(uint keyHash);
    byte[] BuildSetParamByHashCommand(uint keyHash, ushort value);
    byte[] BuildSetParamByHashCommand(uint keyHash, uint value);

    ScanIlluminationState ParseIlluminationStatePayload(byte[] payload);
    IReadOnlyList<ScanMotorState> ParseMotionStatePayload(byte[] payload);
    ScanAck ParseScanAck(ScanControlFrame frame);
    ushort ParseU16ParamPayload(byte[] payload, uint expectedKeyHash, string paramName);
    uint ParseU32ParamPayload(byte[] payload, uint expectedKeyHash, string paramName);
    uint ComputeParamKeyHash(string key);
    void EnsureAckOk(ScanAck ack, int expectedRows, string commandName);
    string MapStatus(byte status);
    bool IsIoTimeout(IOException ex);
    bool TryDequeueControlFrame(List<byte> ackReadBuffer, out ScanControlFrame frame);
}
