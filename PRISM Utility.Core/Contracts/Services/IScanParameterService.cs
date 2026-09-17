using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanParameterService
{
    IReadOnlyList<ScanParameterDefinition> Definitions
    {
        get;
    }

    bool TryParseInput(string exposureMicroseconds, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockMhz, out ScanParameterSnapshot snapshot, out string error);
    ScanParameterDisplays BuildDisplays(string exposureMicroseconds, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockMhz);
    string FormatOffsetForInput(int offset);

    Task<ScanParameterSnapshot> LoadAsync(IScanSessionService session, CancellationToken ct);
    Task ApplyGlobalClockAsync(IScanSessionService session, uint sysClockKhz, CancellationToken ct);
    Task ApplyAsync(IScanSessionService session, ScanParameterSnapshot snapshot, CancellationToken ct);
}
