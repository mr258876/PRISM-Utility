namespace PRISM_Utility.Core.Contracts.Models;
public sealed record UsbEndpointDto(byte Address, bool IsIn, string TransferType, int MaxPacketSize, string Display);
