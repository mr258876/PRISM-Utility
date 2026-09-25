using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanWorkbenchPreviewLayoutTests
{
    [Fact]
    public void Calculate_UsesCompactMediumAndWideAtContentWidthBoundaries()
    {
        var compact = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(899, HasImage: false, IsNarrowPreviewOpen: false));
        var medium = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(900, HasImage: false, IsNarrowPreviewOpen: false));
        var wide = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(1280, HasImage: false, IsNarrowPreviewOpen: false, IsInspectionOpen: true));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.NarrowEditor, compact.Mode);
        Assert.Equal(899, compact.EditorWidth);
        Assert.False(compact.IsPreviewVisible);
        Assert.True(compact.IsPreviewOpenButtonVisible);

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.MediumImage, medium.Mode);
        Assert.Equal(300, medium.EditorWidth);
        Assert.Equal(588, medium.PreviewWidth);
        Assert.False(medium.IsInspectionVisible);
        Assert.True(medium.IsPreviewVisible);

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.WideCompact, wide.Mode);
        Assert.Equal(300, wide.EditorWidth);
        Assert.Equal(696, wide.PreviewWidth);
        Assert.Equal(260, wide.InspectionWidth);
        Assert.Equal(12, wide.InspectionGapWidth);
        Assert.True(wide.IsInspectionVisible);
        Assert.True(wide.IsPreviewVisible);
        Assert.Equal(1280, wide.EditorWidth + wide.SeparatorWidth + wide.PreviewWidth + wide.InspectionGapWidth + wide.InspectionWidth);
    }

    [Fact]
    public void Calculate_WithoutImageStillProvidesCentralPreviewAndHidesOnlySplitter()
    {
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(1280, HasImage: false, IsNarrowPreviewOpen: false, IsInspectionOpen: true));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.WideCompact, layout.Mode);
        Assert.Equal(696, layout.PreviewWidth);
        Assert.Equal(12, layout.SeparatorWidth);
        Assert.False(layout.IsSplitterVisible);
        Assert.False(layout.HideThumbnail);
    }

    [Fact]
    public void Calculate_WideImageAllocatesPreviewBeforeInspectionAndOneContentRow()
    {
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(1280, HasImage: true, IsNarrowPreviewOpen: false, IsInspectionOpen: true));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.WideImage, layout.Mode);
        Assert.Equal(300, layout.EditorWidth);
        Assert.Equal(696, layout.PreviewWidth);
        Assert.Equal(12, layout.SeparatorWidth);
        Assert.Equal(1, layout.ContentRowCount);
        Assert.True(layout.IsEditorVisible);
        Assert.True(layout.IsPreviewVisible);
        Assert.True(layout.IsSplitterVisible);
        Assert.False(layout.HideThumbnail);
        Assert.Equal(1280, layout.EditorWidth + layout.SeparatorWidth + layout.PreviewWidth + layout.InspectionGapWidth + layout.InspectionWidth);
    }

    [Fact]
    public void Calculate_NarrowExplicitOpenSwitchesBetweenEditorAndPreviewFullWidth()
    {
        var editor = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(720, HasImage: true, IsNarrowPreviewOpen: false));
        var preview = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(720, HasImage: true, IsNarrowPreviewOpen: true));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.NarrowEditor, editor.Mode);
        Assert.Equal(720, editor.EditorWidth);
        Assert.Equal(0, editor.PreviewWidth);
        Assert.True(editor.IsEditorVisible);
        Assert.False(editor.IsPreviewVisible);
        Assert.True(editor.IsPreviewOpenButtonVisible);
        Assert.False(editor.IsBackToEditorButtonVisible);

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.NarrowPreview, preview.Mode);
        Assert.Equal(0, preview.EditorWidth);
        Assert.Equal(720, preview.PreviewWidth);
        Assert.False(preview.IsEditorVisible);
        Assert.True(preview.IsPreviewVisible);
        Assert.False(preview.IsPreviewOpenButtonVisible);
        Assert.True(preview.IsBackToEditorButtonVisible);
        Assert.Equal(1, preview.ContentRowCount);
    }

    [Fact]
    public void Calculate_InspectionOnMediumReplacesParametersNotPreviewAndCompactReusesOneRail()
    {
        var medium = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(900, HasImage: true, IsNarrowPreviewOpen: false, IsInspectionOpen: true));
        var compact = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(720, HasImage: true, IsNarrowPreviewOpen: true, IsInspectionOpen: true));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.MediumInspection, medium.Mode);
        Assert.Equal(0, medium.EditorWidth);
        Assert.Equal(628, medium.PreviewWidth);
        Assert.Equal(260, medium.InspectionWidth);
        Assert.True(medium.IsPreviewVisible);
        Assert.True(medium.IsInspectionVisible);

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.NarrowInspection, compact.Mode);
        Assert.Equal(720, compact.InspectionWidth);
        Assert.False(compact.IsPreviewVisible);
        Assert.False(compact.IsEditorVisible);
        Assert.True(compact.IsInspectionVisible);
    }

    [Fact]
    public void Calculate_ConfigurationEditorTakesTheWorkAreaWithoutDuplicatingPreview()
    {
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(720, HasImage: true, IsNarrowPreviewOpen: true, IsInspectionOpen: true, IsConfigurationOpen: true));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.ConfigurationEditor, layout.Mode);
        Assert.Equal(720, layout.EditorWidth);
        Assert.Equal(0, layout.PreviewWidth);
        Assert.Equal(0, layout.InspectionWidth);
        Assert.False(layout.IsPreviewOpenButtonVisible);
        Assert.True(layout.IsEditorVisible);
    }

    [Fact]
    public void Calculate_WideImageClampsTaskRailAndPreviewMinimums()
    {
        var lowRatio = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(1280, HasImage: true, IsNarrowPreviewOpen: false, EditorRatio: 0.01, IsInspectionOpen: true));
        var highRatio = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(1280, HasImage: true, IsNarrowPreviewOpen: false, EditorRatio: 0.99, IsInspectionOpen: true));

        Assert.Equal(300, lowRatio.EditorWidth);
        Assert.Equal(696, lowRatio.PreviewWidth);
        Assert.Equal(516, highRatio.EditorWidth);
        Assert.Equal(480, highRatio.PreviewWidth);
        Assert.Equal(1280, highRatio.EditorWidth + highRatio.SeparatorWidth + highRatio.PreviewWidth + highRatio.InspectionGapWidth + highRatio.InspectionWidth);
    }

    [Fact]
    public void ResizeWideImageSplit_AppliesKeyboardEquivalentDeltasFromRatioAuthority()
    {
        var current = new ScanWorkbenchPreviewResizeInput(1280, ScanWorkbenchPreviewLayout.DefaultEditorRatio, ScanWorkbenchPreviewLayout.KeyboardResizeDelta, IsInspectionOpen: true);
        var expanded = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(current);
        var contracted = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(current with { EditorDelta = -ScanWorkbenchPreviewLayout.KeyboardResizeDelta });

        Assert.Equal(308, expanded.EditorWidth);
        Assert.Equal(688, expanded.PreviewWidth);
        Assert.Equal(308d / 996d, expanded.EditorRatio, 12);

        Assert.Equal(300, contracted.EditorWidth);
        Assert.Equal(696, contracted.PreviewWidth);
        Assert.Equal(300d / 996d, contracted.EditorRatio, 12);
    }

    [Fact]
    public void ResizeWideImageSplit_ClampsLargePointerDeltasToValidWidths()
    {
        var expanded = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(new ScanWorkbenchPreviewResizeInput(1280, ScanWorkbenchPreviewLayout.DefaultEditorRatio, 10_000, IsInspectionOpen: true));
        var contracted = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(new ScanWorkbenchPreviewResizeInput(1280, ScanWorkbenchPreviewLayout.DefaultEditorRatio, -10_000, IsInspectionOpen: true));

        Assert.Equal(516, expanded.EditorWidth);
        Assert.Equal(480, expanded.PreviewWidth);
        Assert.Equal(516d / 996d, expanded.EditorRatio, 12);

        Assert.Equal(300, contracted.EditorWidth);
        Assert.Equal(696, contracted.PreviewWidth);
        Assert.Equal(300d / 996d, contracted.EditorRatio, 12);
    }

    [Fact]
    public void CalculateAndResize_HandleZeroAvailableWidthWithoutNonFiniteValues()
    {
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(0, HasImage: true, IsNarrowPreviewOpen: true));
        var split = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(new ScanWorkbenchPreviewResizeInput(0, ScanWorkbenchPreviewLayout.DefaultEditorRatio, ScanWorkbenchPreviewLayout.KeyboardResizeDelta));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.NarrowPreview, layout.Mode);
        Assert.Equal(0, layout.EditorWidth);
        Assert.Equal(0, layout.PreviewWidth);
        Assert.Equal(0, layout.SeparatorWidth);
        Assert.True(double.IsFinite(layout.EditorWidth));
        Assert.True(double.IsFinite(layout.PreviewWidth));

        Assert.Equal(0, split.EditorWidth);
        Assert.Equal(0, split.PreviewWidth);
        Assert.Equal(ScanWorkbenchPreviewLayout.DefaultEditorRatio, split.EditorRatio);
        Assert.True(double.IsFinite(split.EditorRatio));
    }

    [Fact]
    public void PublicDescriptors_AreImmutablePlainCoreTypesWithoutUiDependency()
    {
        var resultType = typeof(ScanWorkbenchPreviewLayoutResult);
        var layoutAssembly = typeof(ScanWorkbenchPreviewLayout).Assembly.GetReferencedAssemblies();

        Assert.True(resultType.IsValueType);
        Assert.All(resultType.GetProperties(), property => Assert.True(PropertyIsInitOnly(property.SetMethod), $"{property.Name} must be init-only."));
        Assert.DoesNotContain(layoutAssembly, assembly => assembly.Name is "Microsoft.WinUI" or "Microsoft.UI.Xaml" or "WindowsAppSDK");
    }

    private static bool PropertyIsInitOnly(System.Reflection.MethodInfo? setMethod) => setMethod is not null
        && setMethod.ReturnParameter.GetRequiredCustomModifiers().Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
}
