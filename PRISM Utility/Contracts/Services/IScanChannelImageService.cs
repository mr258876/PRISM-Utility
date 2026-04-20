using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Models;
using Windows.Storage;

namespace PRISM_Utility.Contracts.Services;

public interface IScanChannelImageService
{
    bool TryBuildRawPreview(ScanPassCapture capture, WriteableBitmap? currentBitmap, out WriteableBitmap? bitmap, out string error);
    bool TryBuildRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, WriteableBitmap? currentBitmap, out ScanCompositeFrame? frame, out string error);
    Task<StorageFile?> PickRgbImageFileAsync(string suggestedFileName);
    Task SaveRgbImageAsync(StorageFile file, ScanCompositeFrame frame);
    Task<StorageFolder?> PickRawExportFolderAsync();
    Task ExportRawChannelsAsync(StorageFolder folder, ScanWorkflowResult result, ScanChannelAssignment assignment);
}
