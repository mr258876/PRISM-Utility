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
        Assert.Equal(426.24, medium.EditorWidth, 6);
        Assert.Equal(461.76, medium.PreviewWidth, 6);
        Assert.False(medium.IsInspectionVisible);
        Assert.True(medium.IsPreviewVisible);

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.WideCompact, wide.Mode);
        Assert.Equal(478.08, wide.EditorWidth, 6);
        Assert.Equal(517.92, wide.PreviewWidth, 6);
        Assert.Equal(260, wide.InspectionWidth);
        Assert.Equal(12, wide.InspectionGapWidth);
        Assert.True(wide.IsInspectionVisible);
        Assert.False(wide.IsInspectionOverlay);
        Assert.True(wide.IsPreviewVisible);
        Assert.Equal(1280, wide.EditorWidth + wide.SeparatorWidth + wide.PreviewWidth + wide.InspectionGapWidth + wide.InspectionWidth);
    }

    [Fact]
    public void Calculate_WithoutImageBalancesEditingSpaceAndKeepsPreviewResizable()
    {
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(1280, HasImage: false, IsNarrowPreviewOpen: false, IsInspectionOpen: true));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.WideCompact, layout.Mode);
        Assert.Equal(478.08, layout.EditorWidth, 6);
        Assert.Equal(517.92, layout.PreviewWidth, 6);
        Assert.Equal(12, layout.SeparatorWidth);
        Assert.True(layout.IsSplitterVisible);
        Assert.False(layout.HideThumbnail);
    }

    [Fact]
    public void Calculate_WithoutImageRespectsExplicitRatioAndEditorMinimum()
    {
        var lowRatio = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            900, HasImage: false, IsNarrowPreviewOpen: false, EditorRatio: 0.01));
        var explicitRatio = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            1536, HasImage: false, IsNarrowPreviewOpen: false, EditorRatio: 0.4));
        var highRatio = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            900, HasImage: false, IsNarrowPreviewOpen: false, EditorRatio: 0.99));

        Assert.Equal(360, lowRatio.EditorWidth);
        Assert.Equal(528, lowRatio.PreviewWidth);
        Assert.Equal(609.6, explicitRatio.EditorWidth, 6);
        Assert.Equal(914.4, explicitRatio.PreviewWidth, 6);
        Assert.Equal(468, highRatio.EditorWidth);
        Assert.Equal(420, highRatio.PreviewWidth);
    }

    [Theory]
    [InlineData(900, false)]
    [InlineData(900, true)]
    [InlineData(1279, false)]
    [InlineData(1279, true)]
    [InlineData(1280, false)]
    [InlineData(1280, true)]
    [InlineData(3840, false)]
    [InlineData(3840, true)]
    public void Calculate_ImageAndEmptyPreviewAccountForAllAvailableWidth(double availableWidth, bool inspectionOpen)
    {
        foreach (var hasImage in new[] { false, true })
        {
            var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
                availableWidth, hasImage, IsNarrowPreviewOpen: false, IsInspectionOpen: inspectionOpen));

            Assert.Equal(availableWidth,
                layout.EditorWidth + layout.SeparatorWidth + layout.PreviewWidth + layout.InspectionGapWidth + layout.InspectionWidth, 9);
            Assert.True(layout.PreviewWidth >= (hasImage
                ? ScanWorkbenchPreviewLayout.PreviewMinimumWidth : ScanWorkbenchPreviewLayout.EmptyPreviewMinimumWidth));
            Assert.True(layout.EditorWidth >= 0);
        }
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

    [Theory]
    [InlineData(900, false)]
    [InlineData(900, true)]
    [InlineData(1100, false)]
    [InlineData(1100, true)]
    [InlineData(1279, false)]
    [InlineData(1279, true)]
    public void Calculate_InspectionOnMediumPreservesEditorAndPreviewWidths(double availableWidth, bool hasImage)
    {
        var input = new ScanWorkbenchPreviewLayoutInput(
            availableWidth, hasImage, IsNarrowPreviewOpen: false, EditorRatio: 0.4);
        var closed = ScanWorkbenchPreviewLayout.Calculate(input);
        var opened = ScanWorkbenchPreviewLayout.Calculate(input with { IsInspectionOpen = true });

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.MediumInspection, opened.Mode);
        Assert.Equal(closed.EditorWidth, opened.EditorWidth);
        Assert.Equal(closed.PreviewWidth, opened.PreviewWidth);
        Assert.Equal(closed.SeparatorWidth, opened.SeparatorWidth);
        Assert.True(opened.IsEditorVisible);
        Assert.True(opened.IsPreviewVisible);
        Assert.True(opened.IsSplitterVisible);
        Assert.True(opened.IsInspectionVisible);
        Assert.True(opened.IsInspectionOverlay);
        Assert.False(closed.IsInspectionOverlay);
        Assert.Equal(0, opened.InspectionWidth);
        Assert.Equal(0, opened.InspectionGapWidth);
        Assert.Equal(availableWidth,
            opened.EditorWidth + opened.SeparatorWidth + opened.PreviewWidth + opened.InspectionGapWidth + opened.InspectionWidth, 9);
    }

    [Theory]
    [InlineData(900, false, -10000)]
    [InlineData(900, true, 10000)]
    [InlineData(1100, false, 80)]
    [InlineData(1100, true, -80)]
    [InlineData(1279, false, 10000)]
    [InlineData(1279, true, -10000)]
    public void ResizeWideImageSplit_MediumInspectionPreservesResizeBehavior(
        double availableWidth, bool hasImage, double editorDelta)
    {
        var input = new ScanWorkbenchPreviewResizeInput(availableWidth, 0.4, editorDelta, HasImage: hasImage);
        var closed = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(input);
        var opened = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(input with { IsInspectionOpen = true });
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            availableWidth, hasImage, IsNarrowPreviewOpen: false, EditorRatio: opened.EditorRatio, IsInspectionOpen: true));

        Assert.Equal(closed, opened);
        Assert.Equal(opened.EditorWidth, layout.EditorWidth, 9);
        Assert.Equal(opened.PreviewWidth, layout.PreviewWidth, 9);
        Assert.True(layout.IsInspectionOverlay);
        Assert.True(layout.EditorWidth >= (hasImage
            ? ScanWorkbenchPreviewLayout.EditorMinimumWidth : ScanWorkbenchPreviewLayout.EmptyPreviewEditorMinimumWidth));
        Assert.True(layout.PreviewWidth >= (hasImage
            ? ScanWorkbenchPreviewLayout.PreviewMinimumWidth : ScanWorkbenchPreviewLayout.EmptyPreviewMinimumWidth));
        Assert.Equal(availableWidth, layout.EditorWidth + layout.SeparatorWidth + layout.PreviewWidth, 9);
    }

    [Fact]
    public void Calculate_InspectionOnCompactReusesOneRail()
    {
        var compact = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(720, HasImage: true, IsNarrowPreviewOpen: true, IsInspectionOpen: true));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.NarrowInspection, compact.Mode);
        Assert.Equal(720, compact.InspectionWidth);
        Assert.False(compact.IsPreviewVisible);
        Assert.False(compact.IsEditorVisible);
        Assert.True(compact.IsInspectionVisible);
        Assert.False(compact.IsInspectionOverlay);
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

    [Theory]
    [InlineData(900)]
    [InlineData(1280)]
    [InlineData(3840)]
    public void Calculate_CollapsedPreviewGivesEditorFullWidthAndExposesRestoreAction(double availableWidth)
    {
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            availableWidth, HasImage: false, IsNarrowPreviewOpen: false, IsPreviewCollapsed: true));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.PreviewCollapsed, layout.Mode);
        Assert.Equal(availableWidth, layout.EditorWidth);
        Assert.Equal(0, layout.PreviewWidth);
        Assert.Equal(0, layout.SeparatorWidth);
        Assert.True(layout.IsEditorVisible);
        Assert.False(layout.IsPreviewVisible);
        Assert.False(layout.IsSplitterVisible);
        Assert.True(layout.IsPreviewOpenButtonVisible);
        Assert.False(layout.IsBackToEditorButtonVisible);
        Assert.True(layout.HideThumbnail);
    }

    [Theory]
    [InlineData(900)]
    [InlineData(1280)]
    public void Calculate_CollapsedPreviewKeepsExplicitInspectionAndAccountsForItsWidth(double availableWidth)
    {
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            availableWidth, HasImage: false, IsNarrowPreviewOpen: false, IsInspectionOpen: true, IsPreviewCollapsed: true));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.PreviewCollapsed, layout.Mode);
        Assert.Equal(availableWidth - 272, layout.EditorWidth);
        Assert.Equal(260, layout.InspectionWidth);
        Assert.Equal(12, layout.InspectionGapWidth);
        Assert.True(layout.IsEditorVisible);
        Assert.True(layout.IsInspectionVisible);
        Assert.False(layout.IsInspectionOverlay);
        Assert.Equal(availableWidth,
            layout.EditorWidth + layout.SeparatorWidth + layout.PreviewWidth + layout.InspectionGapWidth + layout.InspectionWidth);
    }

    [Fact]
    public void Calculate_CollapsedPreviewOverridesNarrowPreviewRequestButKeepsInspectionReachable()
    {
        var input = new ScanWorkbenchPreviewLayoutInput(
            720, HasImage: false, IsNarrowPreviewOpen: true, IsPreviewCollapsed: true);
        var editor = ScanWorkbenchPreviewLayout.Calculate(input);
        var inspection = ScanWorkbenchPreviewLayout.Calculate(input with { IsInspectionOpen = true });

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.NarrowEditor, editor.Mode);
        Assert.Equal(720, editor.EditorWidth);
        Assert.True(editor.IsPreviewOpenButtonVisible);
        Assert.False(editor.IsPreviewVisible);
        Assert.Equal(ScanWorkbenchPreviewLayoutMode.NarrowInspection, inspection.Mode);
        Assert.Equal(720, inspection.InspectionWidth);
        Assert.False(inspection.IsPreviewVisible);
    }

    [Fact]
    public void Calculate_RestoringPreviewRetainsExplicitEditorRatio()
    {
        var input = new ScanWorkbenchPreviewLayoutInput(
            1536, HasImage: false, IsNarrowPreviewOpen: false, EditorRatio: 0.4, IsPreviewCollapsed: true);
        var collapsed = ScanWorkbenchPreviewLayout.Calculate(input);
        var restored = ScanWorkbenchPreviewLayout.Calculate(input with { IsPreviewCollapsed = false });

        Assert.Equal(1536, collapsed.EditorWidth);
        Assert.Equal(609.6, restored.EditorWidth, 6);
        Assert.Equal(914.4, restored.PreviewWidth, 6);
        Assert.True(restored.IsSplitterVisible);
        Assert.False(restored.IsPreviewOpenButtonVisible);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(719, false)]
    [InlineData(719, true)]
    [InlineData(720, false)]
    [InlineData(720, true)]
    [InlineData(900, false)]
    [InlineData(900, true)]
    [InlineData(1280, false)]
    [InlineData(1280, true)]
    [InlineData(1600, false)]
    [InlineData(1600, true)]
    public void Calculate_ReviewAndActiveContentShareTheAvailableWidthWithoutOverlap(double availableWidth, bool hasImage)
    {
        var input = new ScanWorkbenchPreviewLayoutInput(
            availableWidth, hasImage, IsNarrowPreviewOpen: false, IsReviewOpen: true);
        var states = new[]
        {
            input,
            input with { IsNarrowPreviewOpen = true },
            input with { IsInspectionOpen = true },
            input with { IsConfigurationOpen = true, IsInspectionOpen = true, IsNarrowPreviewOpen = true },
            input with { IsPreviewCollapsed = true, IsNarrowPreviewOpen = true },
            input with { IsPreviewCollapsed = true, IsInspectionOpen = true }
        };

        foreach (var state in states)
        {
            var layout = ScanWorkbenchPreviewLayout.Calculate(state);
            var widths = new[]
            {
                layout.EditorWidth, layout.PreviewWidth, layout.SeparatorWidth,
                layout.InspectionWidth, layout.InspectionGapWidth,
                layout.ReviewWidth, layout.ReviewGapWidth
            };

            Assert.All(widths, width => Assert.True(double.IsFinite(width) && width >= 0));
            Assert.Equal(availableWidth, widths.Sum(), 9);
            Assert.True(layout.IsReviewVisible);
            Assert.Equal(layout.IsReviewFullWidth ? availableWidth : 320, layout.ReviewWidth);
            Assert.Equal(layout.IsReviewFullWidth ? 0 : 12, layout.ReviewGapWidth);
            if (!layout.IsEditorVisible)
                Assert.Equal(0, layout.EditorWidth);
            if (!layout.IsPreviewVisible)
                Assert.Equal(0, layout.PreviewWidth);
            if (!layout.IsInspectionVisible)
                Assert.Equal(0, layout.InspectionWidth);
            if (!layout.IsSplitterVisible)
                Assert.Equal(0, layout.SeparatorWidth);
            if (layout.IsEditorVisible && layout.IsPreviewVisible)
            {
                Assert.True(layout.EditorWidth >= ScanWorkbenchPreviewLayout.EditorMinimumWidth);
                Assert.True(layout.PreviewWidth >= ScanWorkbenchPreviewLayout.PreviewMinimumWidth);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(719)]
    public void Calculate_NarrowReviewReplacesAllContentEvenWhenOtherPanesAreRequested(double availableWidth)
    {
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            availableWidth, HasImage: true, IsNarrowPreviewOpen: true,
            IsInspectionOpen: true, IsConfigurationOpen: true, IsReviewOpen: true));

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.ReviewOnly, layout.Mode);
        Assert.True(layout.IsReviewFullWidth);
        Assert.True(layout.IsReviewVisible);
        Assert.Equal(availableWidth, layout.ReviewWidth);
        Assert.Equal(0, layout.ReviewGapWidth);
        Assert.False(layout.IsEditorVisible);
        Assert.False(layout.IsPreviewVisible);
        Assert.False(layout.IsInspectionVisible);
        Assert.False(layout.IsSplitterVisible);
        Assert.False(layout.IsPreviewOpenButtonVisible);
        Assert.False(layout.IsBackToEditorButtonVisible);
    }

    [Theory]
    [InlineData(720, ScanWorkbenchPreviewLayoutMode.NarrowEditor)]
    [InlineData(900, ScanWorkbenchPreviewLayoutMode.NarrowEditor)]
    [InlineData(1231, ScanWorkbenchPreviewLayoutMode.NarrowEditor)]
    [InlineData(1232, ScanWorkbenchPreviewLayoutMode.MediumImage)]
    [InlineData(1280, ScanWorkbenchPreviewLayoutMode.MediumImage)]
    [InlineData(1600, ScanWorkbenchPreviewLayoutMode.MediumImage)]
    public void Calculate_DockedReviewUsesRemainingWidthToChooseReadableContent(
        double availableWidth, ScanWorkbenchPreviewLayoutMode expectedMode)
    {
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            availableWidth, HasImage: true, IsNarrowPreviewOpen: false, IsReviewOpen: true));

        Assert.Equal(expectedMode, layout.Mode);
        Assert.False(layout.IsReviewFullWidth);
        Assert.Equal(320, layout.ReviewWidth);
        Assert.Equal(12, layout.ReviewGapWidth);
        Assert.True(layout.IsEditorVisible);
        Assert.Equal(expectedMode == ScanWorkbenchPreviewLayoutMode.MediumImage, layout.IsPreviewVisible);
        if (!layout.IsPreviewVisible)
            Assert.Equal(availableWidth - 332, layout.EditorWidth);
    }

    [Fact]
    public void Calculate_DockedReviewKeepsPropertiesReadableAndRespectsPreviewDisclosure()
    {
        var input = new ScanWorkbenchPreviewLayoutInput(
            1280, HasImage: true, IsNarrowPreviewOpen: true, IsReviewOpen: true);
        var properties = ScanWorkbenchPreviewLayout.Calculate(input with
        {
            IsConfigurationOpen = true,
            IsInspectionOpen = true
        });
        var collapsed = ScanWorkbenchPreviewLayout.Calculate(input with { IsPreviewCollapsed = true });
        var restored = ScanWorkbenchPreviewLayout.Calculate(input);

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.ConfigurationEditor, properties.Mode);
        Assert.Equal(948, properties.EditorWidth);
        Assert.False(properties.IsPreviewVisible);
        Assert.False(properties.IsInspectionVisible);
        Assert.False(properties.IsPreviewOpenButtonVisible);

        Assert.Equal(ScanWorkbenchPreviewLayoutMode.PreviewCollapsed, collapsed.Mode);
        Assert.Equal(948, collapsed.EditorWidth);
        Assert.True(collapsed.IsPreviewOpenButtonVisible);
        Assert.False(collapsed.IsPreviewVisible);
        Assert.True(restored.IsPreviewVisible);
        Assert.True(restored.IsSplitterVisible);
        Assert.Equal(300, restored.EditorWidth);
        Assert.Equal(636, restored.PreviewWidth);
    }

    [Fact]
    public void Calculate_ClosedReviewReservesNoWidthAndLeavesExistingLayoutAvailable()
    {
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            1280, HasImage: true, IsNarrowPreviewOpen: false, IsInspectionOpen: true));

        Assert.False(layout.IsReviewVisible);
        Assert.False(layout.IsReviewFullWidth);
        Assert.Equal(0, layout.ReviewWidth);
        Assert.Equal(0, layout.ReviewGapWidth);
        Assert.Equal(ScanWorkbenchPreviewLayoutMode.WideImage, layout.Mode);
        Assert.True(layout.IsEditorVisible);
        Assert.True(layout.IsPreviewVisible);
        Assert.True(layout.IsInspectionVisible);
        Assert.Equal(1280,
            layout.EditorWidth + layout.PreviewWidth + layout.SeparatorWidth
            + layout.InspectionWidth + layout.InspectionGapWidth);
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
    public void ResizeWideImageSplit_EmptyPreviewUsesItsDefaultAndRetainsDraggedWidth()
    {
        var input = new ScanWorkbenchPreviewResizeInput(
            1280, double.NaN, ScanWorkbenchPreviewLayout.KeyboardResizeDelta, IsInspectionOpen: true, HasImage: false);
        var expanded = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(input);
        var contracted = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(input with { EditorDelta = -ScanWorkbenchPreviewLayout.KeyboardResizeDelta });
        var layout = ScanWorkbenchPreviewLayout.Calculate(new ScanWorkbenchPreviewLayoutInput(
            1280, HasImage: false, IsNarrowPreviewOpen: false, EditorRatio: expanded.EditorRatio, IsInspectionOpen: true));

        Assert.Equal(486.08, expanded.EditorWidth, 6);
        Assert.Equal(509.92, expanded.PreviewWidth, 6);
        Assert.Equal(470.08, contracted.EditorWidth, 6);
        Assert.Equal(525.92, contracted.PreviewWidth, 6);
        Assert.Equal(expanded.EditorWidth, layout.EditorWidth, 9);
        Assert.Equal(expanded.PreviewWidth, layout.PreviewWidth, 9);
        Assert.Equal(1280, expanded.EditorWidth + expanded.PreviewWidth + 12 + 12 + 260, 9);
    }

    [Theory]
    [InlineData(900, false, 468, 420, 360, 528)]
    [InlineData(1280, true, 576, 420, 360, 636)]
    public void ResizeWideImageSplit_EmptyPreviewClampsToEditorAndPreviewMinimums(
        double availableWidth, bool inspectionOpen, double maximumEditor, double minimumPreview, double minimumEditor, double maximumPreview)
    {
        var input = new ScanWorkbenchPreviewResizeInput(
            availableWidth, double.NaN, 10_000, IsInspectionOpen: inspectionOpen, HasImage: false);
        var expanded = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(input);
        var contracted = ScanWorkbenchPreviewLayout.ResizeWideImageSplit(input with { EditorDelta = -10_000 });

        Assert.Equal(maximumEditor, expanded.EditorWidth);
        Assert.Equal(minimumPreview, expanded.PreviewWidth);
        Assert.Equal(minimumEditor, contracted.EditorWidth);
        Assert.Equal(maximumPreview, contracted.PreviewWidth);
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
