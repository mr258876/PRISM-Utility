using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanAutoFocusService
{
    Task<ScanAutofocusResult> AutoFocusAsync(IScanSessionService session, ScanAutofocusRequest request, Action<string>? onStatus, Action<byte[], int, string>? onFrameCaptured, CancellationToken ct);
}
