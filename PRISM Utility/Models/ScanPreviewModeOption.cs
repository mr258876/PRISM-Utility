using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PRISM_Utility.Models;

public sealed class ScanPreviewModeOption : ObservableObject
{
    public const string RgbCompositeKey = "RGB Composite";
    private const string RawChannelPrefix = "Raw Channel ";

    private string _displayName;

    public string Key { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public ScanPreviewModeOption(string key, string displayName)
    {
        Key = key;
        _displayName = displayName;
    }

    public static IReadOnlyList<string> CreateKeys(int channelCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(channelCount);
        return CreateKeys(Enumerable.Range(0, channelCount));
    }

    public static IReadOnlyList<string> CreateKeys(IEnumerable<int> channelSlots, bool includeRgbComposite = true)
    {
        ArgumentNullException.ThrowIfNull(channelSlots);
        var slots = channelSlots.ToArray();
        if (slots.Any(slot => slot < 0) || slots.Distinct().Count() != slots.Length)
            throw new ArgumentException("Preview channels must identify distinct non-negative slots.", nameof(channelSlots));
        return (includeRgbComposite ? new[] { RgbCompositeKey } : Array.Empty<string>())
            .Concat(slots.Select(slot => $"{RawChannelPrefix}{slot + 1}"))
            .ToArray();
    }

    public static bool TryGetRawChannelIndex(string? key, int channelCount, out int channelIndex)
    {
        channelIndex = -1;
        return key is not null
            && key.StartsWith(RawChannelPrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(key[RawChannelPrefix.Length..], out var oneBasedIndex)
            && (channelIndex = oneBasedIndex - 1) >= 0
            && channelIndex < channelCount;
    }

    public static void Synchronize(
        ObservableCollection<ScanPreviewModeOption> options,
        IReadOnlyList<string> modes,
        Func<string, string> getDisplayName)
    {
        for (var i = 0; i < modes.Count; i++)
        {
            var mode = modes[i];
            var displayName = getDisplayName(mode);
            if (i < options.Count && string.Equals(options[i].Key, mode, StringComparison.Ordinal))
                options[i].DisplayName = displayName;
            else if (i < options.Count)
                options[i] = new ScanPreviewModeOption(mode, displayName);
            else
                options.Add(new ScanPreviewModeOption(mode, displayName));
        }

        while (options.Count > modes.Count)
            options.RemoveAt(options.Count - 1);
    }
}
