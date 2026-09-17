using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanWorkbenchPreviewLayoutTests
{
    [Fact]
    public void Calculate_UsesNarrowEditorBelowWideThresholdAndWideCompactAtThresholdWithoutImage()
    {
        var below = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(959, HasImage: false, IsNarrowPreviewOpen: false));
        var exact = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(960, HasImage: false, IsNarrowPreviewOpen: false));
        var above = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(961, HasImage: false, IsNarrowPreviewOpen: false));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.NarrowEditor, below.Mode);
        Assert.Equal(959, below.EditorWidth);
        Assert.Equal(0, below.PreviewWidth);
        Assert.True(below.IsEditorVisible);
        Assert.False(below.IsPreviewVisible);
        Assert.True(below.IsPreviewOpenButtonVisible);

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.WideCompact, exact.Mode);
        Assert.Equal(588, exact.EditorWidth);
        Assert.Equal(ScanWorkbenchPreviewLayout.CompactPreviewWidth, exact.PreviewWidth);
        Assert.Equal(ScanWorkbenchPreviewLayout.WideGapWidth, exact.SeparatorWidth);
        Assert.True(exact.HideThumbnail);

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.WideCompact, above.Mode);
        Assert.Equal(589, above.EditorWidth);
    }

    [Fact]
    public void Calculate_WideCompactIgnoresImageRatioAndHidesSplitterAndThumbnail()
    {
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(1200, HasImage: false, IsNarrowPreviewOpen: false, EditorRatio: 0.9));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.WideCompact, layout.Mode);
        Assert.Equal(828, layout.EditorWidth);
        Assert.Equal(360, layout.PreviewWidth);
        Assert.Equal(12, layout.SeparatorWidth);
        Assert.False(layout.IsSplitterVisible);
        Assert.True(layout.HideThumbnail);
        Assert.Equal(1200, layout.EditorWidth + layout.SeparatorWidth + layout.PreviewWidth);
    }

    [Fact]
    public void Calculate_WideImageUsesDefaultFiveToSevenSplitAndOneContentRow()
    {
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(1212, HasImage: true, IsNarrowPreviewOpen: false));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.WideImage, layout.Mode);
        Assert.Equal(500, layout.EditorWidth);
        Assert.Equal(700, layout.PreviewWidth);
        Assert.Equal(12, layout.SeparatorWidth);
        Assert.Equal(1, layout.ContentRowCount);
        Assert.True(layout.IsEditorVisible);
        Assert.True(layout.IsPreviewVisible);
        Assert.True(layout.IsSplitterVisible);
        Assert.False(layout.HideThumbnail);
        Assert.Equal(1212, layout.EditorWidth + layout.SeparatorWidth + layout.PreviewWidth);
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
    public void Calculate_WideImageClampsEditorAndPreviewMinimums()
    {
        var lowEditorRatio = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(1000, HasImage: true, IsNarrowPreviewOpen: false, EditorRatio: 0.01));
        var highEditorRatio = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(1000, HasImage: true, IsNarrowPreviewOpen: false, EditorRatio: 0.99));

        Assert.Equal(320, lowEditorRatio.EditorWidth);
        Assert.Equal(668, lowEditorRatio.PreviewWidth);
        Assert.Equal(12, lowEditorRatio.SeparatorWidth);

        Assert.Equal(628, highEditorRatio.EditorWidth);
        Assert.Equal(360, highEditorRatio.PreviewWidth);
        Assert.Equal(1000, highEditorRatio.EditorWidth + highEditorRatio.SeparatorWidth + highEditorRatio.PreviewWidth);
    }

    [Fact]
    public void ResizeWideImageSplit_AppliesKeyboardEquivalentDeltasFromRatioAuthority()
    {
        var current = new ScanWorkbenchPreviewResizeInput(1212, ScanWorkbenchPreviewLayout.DefaultEditorRatio, ScanWorkbenchPreviewLayout.KeyboardResizeDelta);
        var expanded = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(current);
        var contracted = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(current with { EditorDelta = -ScanWorkbenchPreviewLayout.KeyboardResizeDelta });

        Assert.Equal(508, expanded.EditorWidth);
        Assert.Equal(692, expanded.PreviewWidth);
        Assert.Equal(508d / 1200d, expanded.EditorRatio, 12);

        Assert.Equal(492, contracted.EditorWidth);
        Assert.Equal(708, contracted.PreviewWidth);
        Assert.Equal(492d / 1200d, contracted.EditorRatio, 12);
    }

    [Fact]
    public void ResizeWideImageSplit_ClampsLargePointerDeltasToValidWidths()
    {
        var expanded = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(new ScanWorkbenchPreviewResizeInput(1000, ScanWorkbenchPreviewLayout.DefaultEditorRatio, 10_000));
        var contracted = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(new ScanWorkbenchPreviewResizeInput(1000, ScanWorkbenchPreviewLayout.DefaultEditorRatio, -10_000));

        Assert.Equal(628, expanded.EditorWidth);
        Assert.Equal(360, expanded.PreviewWidth);
        Assert.Equal(628d / 988d, expanded.EditorRatio, 12);

        Assert.Equal(320, contracted.EditorWidth);
        Assert.Equal(668, contracted.PreviewWidth);
        Assert.Equal(320d / 988d, contracted.EditorRatio, 12);
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
