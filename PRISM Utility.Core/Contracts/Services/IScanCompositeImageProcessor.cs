using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanCompositeImageProcessor
{
    byte[] NormalizePassBuffer(ScanPassCapture capture, bool manuallyReverse, CancellationToken cancellationToken);

    bool TryBuildRgbComposite(
        ScanWorkflowResult result,
        ScanChannelAssignment assignment,
        ScanColorManagementOptions colorManagement,
        CancellationToken cancellationToken,
        out ScanCompositePixelBuffer? frame,
        out string error);

    bool TryBuildPartialRgbComposite(
        ScanWorkflowResult result,
        ScanChannelAssignment assignment,
        ScanColorManagementOptions colorManagement,
        IReadOnlyDictionary<string, ScanRowAvailability> availableRowsByRole,
        CancellationToken cancellationToken,
        out ScanCompositePixelBuffer? frame,
        out string error);
}
