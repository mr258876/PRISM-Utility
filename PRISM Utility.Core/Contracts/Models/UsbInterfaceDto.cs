namespace PRISM_Utility.Core.Contracts.Models;
public sealed record UsbInterfaceDto(byte InterfaceId, byte AlternateId, int EndpointCount, string Display);
