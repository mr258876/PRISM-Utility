using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;

public partial class ScanDebugViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentProfileNameText))]
    [NotifyPropertyChangedFor(nameof(ProfileSaveStateText))]
    [NotifyPropertyChangedFor(nameof(FilmProfileSaveHintText))]
    public partial string? EditingDraftFileName { get; set; }

    partial void OnEditingDraftFileNameChanged(string? value)
    {
        NotifyCalibrationGuideChanged();
        NotifyWorkflowGuideChanged();
    }

    public bool CanUseEditingDraftFiles => CanRunFilmProfileLifecycleOperation();

    public async Task SaveEditingDraftAsync(IScanFilmProfileFileGateway gateway)
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.NewFilmProfile, out var claim))
            return;
        IsFilmProfileOperationRunning = true;
        var pageOwner = _attachedPageOwner;
        try
        {
            var inputs = CaptureFilmProfileEditorInputs();
            var snapshot = _filmProfileWorkspace.Snapshot;
            var raw = inputs.GlobalInputs.ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal);
            raw["$SelectedCalibrationChannel"] = inputs.SelectedCalibrationChannel;
            var channels = inputs.ChannelInputs.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyDictionary<string, string?>)pair.Value.ToDictionary(field => field.Key, field => (string?)field.Value, StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase);
            var text = new ScanFilmProfileDraftFileService().Serialize(new ScanFilmProfileDraftFile(snapshot.CurrentDraft, snapshot.BaselineDraft, raw, channels));
            var suggestedName = EditingDraftFileName ?? (string.IsNullOrWhiteSpace(FilmProfileName)
                ? "ScanDebug_DraftSuggestedName".GetLocalizedOrFallback("Film configuration draft")
                : FilmProfileName);
            var result = await gateway.WriteDraftAsync(suggestedName, _ => text);
            if (!IsEditingDraftOwnerCurrent(pageOwner))
                return;
            if (result.WasCanceled)
            {
                return;
            }

            // Saving a checkpoint must keep edits made while the picker was open.
            _filmProfileWorkspace.MarkEditingDraftSaved(snapshot.CurrentDraft);
            AcceptFilmProfileEditorInputBaseline(inputs);
            EditingDraftFileName = result.FileName;
            _isNewFilmProfilePendingExport = false;
            RefreshFilmProfileWorkspaceProjection();
            PublishFilmProfileOperation("ScanDebug_DraftSaved".GetLocalizedFormatOrFallback("Draft saved: {0}. Unfinished input was preserved.", result.FileName ?? suggestedName), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            if (IsEditingDraftOwnerCurrent(pageOwner))
                PublishFilmProfileOperation("ScanDebug_DraftSaveFailed".GetLocalizedFormatOrFallback("Could not save draft: {0}", ex.Message), InfoBarSeverity.Error);
        }
        finally
        {
            claim?.Dispose();
            IsFilmProfileOperationRunning = false;
        }
    }

    public async Task LoadEditingDraftAsync(IScanFilmProfileFileGateway gateway)
    {
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.LoadFilmProfileJson, out var claim))
            return;
        IsFilmProfileOperationRunning = true;
        var pageOwner = _attachedPageOwner;
        var snapshot = _filmProfileWorkspace.Snapshot;
        var originalInputs = CaptureFilmProfileEditorInputs();
        bool CanRestore()
        {
            if (!IsEditingDraftOwnerCurrent(pageOwner)) return false;
            if (ReferenceEquals(snapshot, _filmProfileWorkspace.Snapshot)
                && HaveIdenticalEditorInputs(originalInputs, CaptureFilmProfileEditorInputs())) return true;
            PublishFilmProfileOperation("ScanDebug_DraftOpenSuperseded".GetLocalizedOrFallback(
                "Your configuration changed while opening the draft. The current input was kept."), InfoBarSeverity.Informational);
            return false;
        }
        try
        {
            if (HasPendingFilmProfileImportResult && !await RequestFilmProfileImportDiscardConfirmationAsync())
            {
                return;
            }
            if (!CanRestore()) return;
            if (HasUnsavedProfileChanges && !await RequestFilmProfileDiscardConfirmationAsync())
            {
                return;
            }
            if (!CanRestore()) return;

            var result = await gateway.ReadDraftAsync();
            if (result.WasCanceled)
            {
                return;
            }
            if (!CanRestore()) return;
            var parsed = new ScanFilmProfileDraftFileService().Parse(result.Text);
            if (!parsed.CanOpen)
            {
                PublishDraftOpenError(parsed.Error == ScanFilmProfileDraftFileError.TooLarge);
                return;
            }
            var file = parsed.DraftFile!;
            var raw = file.RawFields.ToDictionary(pair => pair.Key, pair => pair.Value ?? string.Empty, StringComparer.Ordinal);
            var inputs = new ScanFilmProfileEditorInputs(raw,
                file.ChannelRawFields.ToDictionary(pair => pair.Key,
                    pair => (IReadOnlyDictionary<string, string>)pair.Value.ToDictionary(field => field.Key, field => field.Value ?? string.Empty, StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase),
                file.RawFields.GetValueOrDefault("$SelectedCalibrationChannel") ?? file.CurrentDraft.SelectedCalibrationChannel);
            if (!TryValidateFilmProfileEditorInputs(inputs, out _))
            {
                PublishDraftOpenError();
                return;
            }

            // Restoring edits is local. It neither applies settings to hardware nor writes the calibration library.
            _copiedUnverifiedCalibrationProfiles.Clear();
            ClearSelectedCalibrationEditorBaseline();
            _filmProfileWorkspace.RestoreEditingDraft(file.CurrentDraft);
            RestoreFilmProfileEditorInputs(inputs);
            _filmProfileWorkspace.MarkEditingDraftSaved(_filmProfileWorkspace.Snapshot.CurrentDraft);
            RestoreFilmProfileManualReferenceInputs(inputs.GlobalInputs);
            AcceptFilmProfileEditorInputBaseline();
            FilmProfileFileName = null;
            EditingDraftFileName = result.FileName;
            _stagedFilmProfileFileName = null;
            _isNewFilmProfilePendingExport = false;
            SetFilmProfileImportNone();
            RefreshFilmProfileWorkspaceProjection();
            PublishFilmProfileOperation("ScanDebug_DraftOpened".GetLocalizedFormatOrFallback("Draft restored: {0}. Check and save a complete configuration before scanning.", result.FileName ?? string.Empty), InfoBarSeverity.Success);
        }
        catch (InvalidDataException)
        {
            if (IsEditingDraftOwnerCurrent(pageOwner)) PublishDraftOpenError(tooLarge: true);
        }
        catch (Exception)
        {
            if (IsEditingDraftOwnerCurrent(pageOwner)) PublishDraftOpenError();
        }
        finally
        {
            claim?.Dispose();
            IsFilmProfileOperationRunning = false;
        }
    }

    private bool IsEditingDraftOwnerCurrent(object? pageOwner)
        => !_isCleanedUp && (pageOwner is null || IsCurrentPageOwner(pageOwner));

    private void PublishDraftOpenError(bool tooLarge = false)
        => PublishFilmProfileOperation(tooLarge
            ? "ScanDebug_DraftTooLarge".GetLocalizedOrFallback("This draft exceeds the 2 MB limit.")
            : "ScanDebug_DraftOpenFailed".GetLocalizedOrFallback("This draft is damaged or uses an unsupported format."), InfoBarSeverity.Error);

    private static bool HaveIdenticalEditorInputs(ScanFilmProfileEditorInputs left, ScanFilmProfileEditorInputs right)
        => left.SelectedCalibrationChannel == right.SelectedCalibrationChannel
            && SameFields(left.GlobalInputs, right.GlobalInputs)
            && left.ChannelInputs.Count == right.ChannelInputs.Count
            && left.ChannelInputs.All(pair => right.ChannelInputs.TryGetValue(pair.Key, out var values) && SameFields(pair.Value, values));

    private static bool SameFields(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
        => left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);
}
