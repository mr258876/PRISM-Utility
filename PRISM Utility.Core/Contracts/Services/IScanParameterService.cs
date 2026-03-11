using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanParameterService
{
    IReadOnlyList<ScanParameterDefinition> Definitions
    {
        get;
    }

    bool TryParseInput(string exposureTicks, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockKhz, out ScanParameterSnapshot snapshot, out string error);
    ScanParameterDisplays BuildDisplays(string exposureTicks, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockKhz);
    string FormatOffsetForInput(int offset);

    Task<ScanParameterSnapshot> LoadAsync(IScanSessionService session, CancellationToken ct);
    Task ApplyAsync(IScanSessionService session, ScanParameterSnapshot snapshot, CancellationToken ct);
}
