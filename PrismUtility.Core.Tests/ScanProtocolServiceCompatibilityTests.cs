using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanProtocolServiceCompatibilityTests
{
    [Fact]
    public void MotorMoveFrame_PreservesEstablishedBytes()
    {
        var frame = new ScanProtocolService().BuildMoveMotorStepsCommand(2, false, 0x11223344, 0x55667788);

        Assert.Equal(new byte[] { 0xA5, 0x52, 0x0A, 0x00, 0x02, 0x00, 0x44, 0x33, 0x22, 0x11, 0x88, 0x77, 0x66, 0x55 }, frame);
    }

    [Fact]
    public void MotorPrepareOnSyncFrame_PreservesEstablishedBytes()
    {
        var frame = new ScanProtocolService().BuildPrepareMotorOnSyncCommand(1, true, 0xA1B2C3D4, 0x10203040);

        Assert.Equal(new byte[] { 0xA5, 0x57, 0x0A, 0x00, 0x01, 0x01, 0xD4, 0xC3, 0xB2, 0xA1, 0x40, 0x30, 0x20, 0x10 }, frame);
    }
}
