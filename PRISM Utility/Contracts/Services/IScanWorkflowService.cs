using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Models;

namespace PRISM_Utility.Contracts.Services;

public interface IScanWorkflowService
{
    Task<ScanWorkflowResult> ExecuteAsync(
        IScanSessionService session,
        ScanWorkflowRequest request,
        CancellationToken ct,
        Action<ScanWorkflowProgress>? onProgress = null,
        Action<string>? onStatus = null,
        Action<string>? onDiagnostic = null,
        Action<int, int>? onByteProgress = null);
}
