using Xunit;
using static PrismUtility.Core.Tests.ScanDebugFilmProfileOrchestrationSourceTests;

namespace PrismUtility.Core.Tests;

public sealed class ScanDngWarningSourceContractTests
{
    [Fact]
    public void ScanExportClearsStaleBannerBeforeFolderSelection()
    {
        var export = ExtractMethod(ReadAppSource("ViewModels", "ScanViewModel.cs"), "ExportDngChannels");
        var clearIndex = export.IndexOf("ClearTopRiskBanner();", StringComparison.Ordinal);
        var pickerIndex = export.IndexOf("_channelImages.PickDngExportFolderAsync()", StringComparison.Ordinal);

        Assert.True(clearIndex >= 0, "DNG export must clear any stale output banner.");
        Assert.True(pickerIndex > clearIndex, "The stale banner must be cleared before folder selection can be canceled.");
    }

    [Fact]
    public void DngExportMirrorsFatalDiagnosticsBeforeRejectingUnusableBuffers()
    {
        var export = ExtractMethod(ReadAppSource("Services", "ScanChannelImageService.cs"), "ExportDngChannelsAsync");
        var reportIndex = export.IndexOf("ReportAlignmentDiagnostics(alignmentResult);", StringComparison.Ordinal);
        var rejectionIndex = export.IndexOf("if (!alignmentResult.HasUsableBuffers)", StringComparison.Ordinal);

        Assert.True(reportIndex >= 0, "DNG export must mirror alignment diagnostics.");
        Assert.True(rejectionIndex > reportIndex, "Diagnostics must be mirrored before unusable buffers are rejected.");
    }

    private static string ReadAppSource(params string[] relativePath)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", Path.Combine(relativePath)));

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the Host Software source root.");
    }
}
