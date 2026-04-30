using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Contracts.Services;

public interface IScanCompositeImageProcessor
{
    byte[] NormalizePassBuffer(ScanPassCapture capture, bool manuallyReverse);

    bool TryBuildRgbComposite(
        ScanWorkflowResult result,
        ScanChannelAssignment assignment,
        ScanColorManagementOptions colorManagement,
        out ScanCompositePixelBuffer? frame,
        out string error);
}
