using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Models;
using Windows.Storage;

namespace PRISM_Utility.Contracts.Services;

public interface IScanChannelImageService
{
    bool TryBuildRawPreview(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanChannelAlignmentMode alignmentMode, int channelIndex, WriteableBitmap? currentBitmap, out WriteableBitmap? bitmap, out string error);
    bool TryBuildRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, ScanChannelAlignmentMode alignmentMode, WriteableBitmap? currentBitmap, out ScanCompositeFrame? frame, out string error);
    Task<StorageFile?> PickRgbImageFileAsync(string suggestedFileName);
    Task SaveRgbImageAsync(StorageFile file, ScanCompositeFrame frame);
    Task<StorageFolder?> PickDngExportFolderAsync();
    Task ExportDngChannelsAsync(StorageFolder folder, ScanWorkflowResult result, ScanChannelAssignment assignment, ScanChannelAlignmentMode alignmentMode, ScanDngExportMode exportMode = ScanDngExportMode.LinearRaw4);
}
