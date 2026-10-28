using System.Collections.ObjectModel;

namespace PRISM_Utility.Core.Models;

/// <summary>
/// An editing document. Raw text is deliberately kept apart from the typed,
/// last-usable values, so incomplete input survives saving and reopening.
/// </summary>
public sealed record ScanFilmProfileDraftFile
{
    public ScanFilmProfileDraftFile(
        ScanFilmProfileDraft currentDraft,
        ScanFilmProfileDraft baselineDraft,
        IReadOnlyDictionary<string, string?>? rawFields = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>>? channelRawFields = null)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        ArgumentNullException.ThrowIfNull(baselineDraft);
        CurrentDraft = currentDraft;
        BaselineDraft = baselineDraft;
        RawFields = CopyFields(rawFields);
        var channels = new Dictionary<string, IReadOnlyDictionary<string, string?>>(StringComparer.OrdinalIgnoreCase);
        if (channelRawFields is not null)
        {
            foreach (var pair in channelRawFields)
                channels.Add(pair.Key, CopyFields(pair.Value));
        }

        ChannelRawFields = new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>>(channels);
    }

    public ScanFilmProfileDraft CurrentDraft { get; }
    public ScanFilmProfileDraft BaselineDraft { get; }
    public IReadOnlyDictionary<string, string?> RawFields { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> ChannelRawFields { get; }

    private static IReadOnlyDictionary<string, string?> CopyFields(IReadOnlyDictionary<string, string?>? source)
    {
        var copy = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (source is not null)
        {
            foreach (var pair in source)
                copy.Add(pair.Key, pair.Value);
        }

        return new ReadOnlyDictionary<string, string?>(copy);
    }
}

public enum ScanFilmProfileDraftFileError
{
    None,
    InvalidJson,
    UnsupportedKind,
    UnsupportedVersion,
    InvalidShape,
    TooLarge
}

public sealed record ScanFilmProfileDraftFileParseResult(
    ScanFilmProfileDraftFile? DraftFile,
    ScanFilmProfileDraftFileError Error,
    string? FieldPath = null)
{
    public bool CanOpen => DraftFile is not null && Error == ScanFilmProfileDraftFileError.None;
}
