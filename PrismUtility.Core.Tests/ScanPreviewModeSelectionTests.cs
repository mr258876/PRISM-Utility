using System.Collections.ObjectModel;
using System.Collections.Specialized;
using PRISM_Utility.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanPreviewModeSelectionTests
{
    private static readonly string[] Modes =
        ["RGB Composite", "Raw Channel 1", "Raw Channel 2", "Raw Channel 3", "Raw Channel 4"];

    [Theory]
    [InlineData("RGB Composite")]
    [InlineData("Raw Channel 2")]
    public void ReapplyingProfileProjectionPreservesTheSelectedItemAndUpdatesItsLabel(string selectedKey)
    {
        var options = new ObservableCollection<ScanPreviewModeOption>();
        ScanPreviewModeOption.Synchronize(options, Modes, mode => $"Before: {mode}");
        var originalOptions = options.ToArray();
        var selectedOption = options.Single(option => option.Key == selectedKey);
        var collectionChanges = new List<NotifyCollectionChangedAction>();
        var selectedProperties = new List<string?>();
        options.CollectionChanged += (_, change) => collectionChanges.Add(change.Action);
        selectedOption.PropertyChanged += (_, change) => selectedProperties.Add(change.PropertyName);

        // Loading a profile reprojects the same mode keys with current channel display names.
        ScanPreviewModeOption.Synchronize(options, Modes, mode => $"After: {mode}");

        Assert.Empty(collectionChanges);
        Assert.Equal(originalOptions.Length, options.Count);
        for (var i = 0; i < originalOptions.Length; i++)
            Assert.Same(originalOptions[i], options[i]);
        Assert.Same(selectedOption, options.Single(option => option.Key == selectedKey));
        Assert.Equal($"After: {selectedKey}", selectedOption.DisplayName);
        Assert.Equal(new[] { nameof(ScanPreviewModeOption.DisplayName) }, selectedProperties);

        ScanPreviewModeOption.Synchronize(options, Modes, mode => $"After: {mode}");

        Assert.Empty(collectionChanges);
        Assert.Single(selectedProperties);
        Assert.Contains(selectedOption, options);
        Assert.Equal(selectedKey, selectedOption.Key);
    }

    [Fact]
    public void FifthWhiteCaptureHasItsOwnRawModeAndKeepsExistingSelectionsStable()
    {
        var options = new ObservableCollection<ScanPreviewModeOption>();
        ScanPreviewModeOption.Synchronize(options, ScanPreviewModeOption.CreateKeys(4), mode => mode);
        var rgb = options[0];
        var existingRaw = options[3];

        ScanPreviewModeOption.Synchronize(options, ScanPreviewModeOption.CreateKeys(5), mode => mode);

        Assert.Equal(6, options.Count);
        Assert.Same(rgb, options[0]);
        Assert.Same(existingRaw, options[3]);
        Assert.Equal("Raw Channel 5", options[5].Key);
        Assert.True(ScanPreviewModeOption.TryGetRawChannelIndex(options[5].Key, 5, out var index));
        Assert.Equal(4, index);
        Assert.False(ScanPreviewModeOption.TryGetRawChannelIndex(ScanPreviewModeOption.RgbCompositeKey, 5, out _));

        ScanPreviewModeOption.Synchronize(options, ScanPreviewModeOption.CreateKeys(4), mode => mode);

        Assert.Equal(5, options.Count);
        Assert.Same(rgb, options[0]);
        Assert.False(ScanPreviewModeOption.TryGetRawChannelIndex("Raw Channel 5", 4, out _));
    }

    [Theory]
    [InlineData("Raw Channel 0")]
    [InlineData("Raw Channel 6")]
    [InlineData("Raw Channel White")]
    [InlineData(null)]
    public void RawModeRejectsUnavailableOrInvalidCaptureIndexes(string? key)
        => Assert.False(ScanPreviewModeOption.TryGetRawChannelIndex(key, 5, out _));

    [Fact]
    public void WhiteOnlyResultOffersTheRawSlotWithoutInventingRgbData()
        => Assert.Equal(new[] { "Raw Channel 5" }, ScanPreviewModeOption.CreateKeys(new[] { 4 }, includeRgbComposite: false));
}
