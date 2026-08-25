using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanDngExportResultTests
{
    [Fact]
    public void CleanAlignedResultHasNoAlignmentWarning()
    {
        var result = new ScanDngExportResult(
            ScanChannelAlignmentStatus.Aligned,
            Array.Empty<ScanChannelAlignmentDiagnostic>(),
            Array.Empty<string>());

        Assert.Equal(ScanChannelAlignmentStatus.Aligned, result.AlignmentStatus);
        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.AffectedChannelRoles);
        Assert.False(result.HasAlignmentWarning);
    }

    [Theory]
    [InlineData(ScanChannelAlignmentStatus.Fallback)]
    [InlineData(ScanChannelAlignmentStatus.Disabled)]
    public void FallbackOrDisabledResultHasAlignmentWarning(ScanChannelAlignmentStatus alignmentStatus)
    {
        var result = new ScanDngExportResult(
            alignmentStatus,
            Array.Empty<ScanChannelAlignmentDiagnostic>(),
            new[] { "Red" });

        Assert.True(result.HasAlignmentWarning);
        Assert.Equal(new[] { "Red" }, result.AffectedChannelRoles);
    }

    [Fact]
    public void DiagnosticsCauseAlignmentWarningAndAreSnapshotted()
    {
        var diagnostics = new List<ScanChannelAlignmentDiagnostic>
        {
            new(ScanChannelAlignmentDiagnosticKind.NonConverged, 0, "Blue", "native diagnostic")
        };

        var result = new ScanDngExportResult(
            ScanChannelAlignmentStatus.Aligned,
            diagnostics,
            Array.Empty<string>());
        diagnostics.Clear();

        Assert.True(result.HasAlignmentWarning);
        Assert.Single(result.Diagnostics);
        Assert.Equal("Blue", result.Diagnostics[0].ChannelRole);
    }

    [Fact]
    public void AffectedChannelRolesDeduplicateCaseInsensitivelyInFirstSeenOrder()
    {
        var result = new ScanDngExportResult(
            ScanChannelAlignmentStatus.Fallback,
            Array.Empty<ScanChannelAlignmentDiagnostic>(),
            new[] { "Red", "green", "RED", "Blue", "Green", " blue " });

        Assert.Equal(new[] { "Red", "green", "Blue" }, result.AffectedChannelRoles);
    }
}
