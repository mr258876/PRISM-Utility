namespace PRISM_Utility.Core.Contracts.Models;
public sealed record UsbDeviceDto(string Id, ushort Vid, ushort Pid, ushort Rev, string Display);
