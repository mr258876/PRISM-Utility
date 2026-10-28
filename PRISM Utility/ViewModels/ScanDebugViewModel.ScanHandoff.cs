using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;

public sealed record ScanFilmProfileEditRequest(ScanFilmParameterProfileSet Profile, string? FileName);

public partial class ScanDebugViewModel
{
    public async Task<bool> EditLoadedScanProfileAsync(ScanFilmProfileEditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.LoadFilmProfileJson, out var claim))
            return false;

        IsFilmProfileOperationRunning = true;
        var pageOwner = _attachedPageOwner;
        var snapshot = _filmProfileWorkspace.Snapshot;
        var originalInputs = CaptureFilmProfileEditorInputs();
        bool IsCurrent() => IsEditingDraftOwnerCurrent(pageOwner)
            && ReferenceEquals(snapshot, _filmProfileWorkspace.Snapshot)
            && HaveIdenticalEditorInputs(originalInputs, CaptureFilmProfileEditorInputs());
        try
        {
            if (HasPendingFilmProfileImportResult && !await RequestFilmProfileImportDiscardConfirmationAsync())
                return false;
            if (!IsCurrent())
                return false;

            var incomingDraft = ScanFilmProfileDraft.FromDocument(request.Profile).Draft;
            // Returning to the same file must retain edits already made in its editor.
            if (snapshot.CurrentDraft.HasSameContentAs(incomingDraft) || snapshot.BaselineDraft.HasSameContentAs(incomingDraft))
            {
                _filmProfileWorkspace.DiscardStagedImport();
                _stagedFilmProfileFileName = null;
                if (string.IsNullOrWhiteSpace(FilmProfileFileName) && string.IsNullOrWhiteSpace(EditingDraftFileName))
                    FilmProfileFileName = request.FileName;
                RefreshFilmProfileWorkspaceProjection();
                return true;
            }

            if (HasUnsavedProfileChanges && !await RequestFilmProfileDiscardConfirmationAsync())
                return false;
            if (!IsCurrent())
                return false;

            var staged = _filmProfileWorkspace.StageImport(request.Profile);
            if (!staged.Staged)
            {
                SetFilmProfileImportError(staged.Validation);
                return false;
            }
            var result = await _filmProfileWorkspace.ApplyStagedImportAsync(_terminalCleanupCts.Token);
            if (result.Status != ScanFilmProfileApplyStatus.Applied)
                return false;

            _copiedUnverifiedCalibrationProfiles.Clear();
            _isNewFilmProfilePendingExport = false;
            FilmProfileFileName = request.FileName;
            EditingDraftFileName = null;
            _stagedFilmProfileFileName = null;
            SetFilmProfileImportNone();
            RefreshFilmProfileWorkspaceProjection();
            return true;
        }
        catch (OperationCanceledException) when (_terminalCleanupCts.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            if (IsEditingDraftOwnerCurrent(pageOwner))
                PublishFilmProfileOperation("ScanDebug_Runtime_StatusLoadFilmProfileFailed".GetLocalizedFormat(ex.Message), InfoBarSeverity.Error);
            return false;
        }
        finally
        {
            claim?.Dispose();
            IsFilmProfileOperationRunning = false;
        }
    }
}
