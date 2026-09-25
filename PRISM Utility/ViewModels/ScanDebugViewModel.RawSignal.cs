using System.Globalization;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;

public partial class ScanDebugViewModel
{
    private byte[]? _rawFinalRows;
    private byte[]? _rawStreamingRow;
    private readonly object _rawStreamingPublicationLock = new();
    private (int Session, Guid Capture, int Row, int Completed, byte[] Bytes)? _rawStreamingPublication;
    private int _rawPageEpoch;
    private int _rawCapturePageEpoch = -1;
    private bool _rawPageActive;
    private int _rawCompletedRows;
    private int _rawStreamingRowIndex = -1;
    private int _rawRevision;
    private int _rawRoiRevision;
    private ScanColumnRange? _rawLastColumns;
    private int _rawResultGeneration;
    private Guid _rawCaptureId;
    private Guid? _rawRenderedCaptureId;
    private int _rawPassIndex = -1;
    private string? _rawRole;
    private int _rawWorkerRunning;
    private RawSignalWork? _rawPending;
    private string _rawRowInput = string.Empty;
    private bool _rawRowIsAutomatic = true;
    private ScanRawSignalResult? _rawSignalResult;
    private string _rawSignalStatusText = "ScanDebug_RawSignalUnavailable".GetLocalized();
    private string _rawSignalSourceText = string.Empty;
    private string _rawSignalStatisticsText = string.Empty;

    public ScanRawSignalResult? RawSignalResult
    {
        get => _rawSignalResult;
        private set => SetProperty(ref _rawSignalResult, value);
    }

    public string RawSignalStatusText
    {
        get => _rawSignalStatusText;
        private set => SetProperty(ref _rawSignalStatusText, value);
    }

    public string RawSignalSourceText
    {
        get => _rawSignalSourceText;
        private set => SetProperty(ref _rawSignalSourceText, value);
    }

    public string RawSignalStatisticsText
    {
        get => _rawSignalStatisticsText;
        private set => SetProperty(ref _rawSignalStatisticsText, value);
    }

    public string RawSignalRowInput
    {
        get => _rawRowInput;
        set
        {
            if (!SetProperty(ref _rawRowInput, value))
                return;
            _rawRowIsAutomatic = false;
            RefreshRawSignal();
        }
    }

    private sealed record RawSignalWork(
        int Revision, int PageEpoch, Guid CaptureId, int PassIndex, string Role, int ResultGeneration,
        int Row, int RoiRevision, ScanColumnRange Columns, byte[] PackedRow);

    private void ActivateRawPage()
    {
        _rawPageEpoch++;
        _rawPageActive = true;
    }

    private void DeactivateRawPage()
    {
        _rawPageActive = false;
        _rawPageEpoch++;
        ClearRawSignal("ScanDebug_RawSignalUnavailable");
    }

    private bool IsRawPageCaptureCurrent()
        => (_rawPageActive || _rawPageEpoch == 0)
            && (_rawCapturePageEpoch < 0 || _rawCapturePageEpoch == _rawPageEpoch);

    private void ClearRawSignal(string reason)
    {
        _rawRevision++;
        _rawPending = null;
        _rawFinalRows = null;
        _rawStreamingRow = null;
        lock (_rawStreamingPublicationLock)
            _rawStreamingPublication = null;
        _rawCompletedRows = 0;
        _rawStreamingRowIndex = -1;
        _rawCaptureId = Guid.Empty;
        _rawPassIndex = -1;
        _rawRole = null;
        RawSignalResult = null;
        RawSignalStatisticsText = string.Empty;
        RawSignalSourceText = string.Empty;
        RawSignalStatusText = reason.GetLocalized();
    }

    private void BeginRawCapture()
    {
        ClearRawSignal("ScanDebug_RawSignalCalculating");
        _rawRenderedCaptureId = null;
        _rawCapturePageEpoch = _rawPageActive ? _rawPageEpoch : -1;
        _rawRowIsAutomatic = true;
    }

    private void PublishRawFinal(byte[] imageBytes, int rows, Guid? captureId)
    {
        ClearRawSignal("ScanDebug_RawSignalUnavailableSource");
        if (!_rawPageActive || _rawCapturePageEpoch != _rawPageEpoch
            || captureId is null || captureId == Guid.Empty || _rawRenderedCaptureId != captureId
            || _displayedCaptureEvidence?.Id != captureId
            || rows <= 0)
            return;

        var passIndex = -1;
        var role = _displayedCaptureEvidence.Role;
        var resultGeneration = 0;
        if (_lastWorkflowResult is { } workflow)
        {
            var matches = workflow.Passes.Where(candidate => candidate.Provenance is { } provenance
                && provenance.CaptureId == captureId && candidate.Rows > 0
                && candidate.ImageBytes.Length == (long)candidate.Rows * ScanDebugConstants.BytesPerLine
                && string.Equals(provenance.ChannelRole, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1)
            {
                RawSignalStatusText = "ScanDebug_RawSignalUnavailableComposite".GetLocalized();
                return;
            }
            var pass = matches[0];
            passIndex = pass.PassIndex;
            role = pass.Provenance!.ChannelRole;
            resultGeneration = pass.Provenance.CompletedResultVersion;
            imageBytes = pass.ImageBytes;
            rows = pass.Rows;
        }
        else if (role is null || !string.Equals(role, SelectedCalibrationChannel, StringComparison.OrdinalIgnoreCase)
            || !ScanChannelRoleHelper.IsActiveRole(role)
            || imageBytes.Length != (long)rows * ScanDebugConstants.BytesPerLine)
            return;

        // Completed scan results own their buffers until the next scan replaces them; only the selected row is copied for the worker.
        _rawFinalRows = imageBytes;
        _rawCompletedRows = rows;
        _rawCaptureId = captureId.Value;
        _rawPassIndex = passIndex;
        _rawRole = role;
        _rawResultGeneration = resultGeneration;
        if (_rawRowIsAutomatic)
            SetRawRowInput(rows - 1);
        RefreshRawSignal();
    }

    private void CopyRawStreamingRow(int sessionVersion, Guid captureId, byte[] imageBytes, int completedRows)
    {
        if (!_rawPageActive || _rawCapturePageEpoch != _rawPageEpoch || completedRows <= 0
            || imageBytes.Length < (long)completedRows * ScanDebugConstants.BytesPerLine)
            return;
        var requestedRow = _rawRowIsAutomatic ? completedRows - 1 :
            int.TryParse(_rawRowInput, NumberStyles.None, CultureInfo.InvariantCulture, out var selected) ? selected : -1;
        if (requestedRow < 0 || requestedRow >= completedRows)
            return;
        var row = new byte[ScanDebugConstants.BytesPerLine];
        Buffer.BlockCopy(imageBytes, requestedRow * row.Length, row, 0, row.Length);
        lock (_rawStreamingPublicationLock)
        {
            _rawStreamingPublication = (sessionVersion, captureId, requestedRow, completedRows, row);
        }
    }

    private void ApplyRawStreamingPublication(int sessionVersion, int renderedRows)
    {
        (int Session, Guid Capture, int Row, int Completed, byte[] Bytes)? publication;
        lock (_rawStreamingPublicationLock)
        {
            publication = _rawStreamingPublication;
            if (publication is { } candidate && candidate.Session == sessionVersion && candidate.Completed == renderedRows)
                _rawStreamingPublication = null;
        }
        if (publication is not { } snapshot || snapshot.Session != sessionVersion || snapshot.Completed != renderedRows
            || !_rawPageActive || _rawCapturePageEpoch != _rawPageEpoch || !_isStreamingPreviewActive
            || _streamingPreviewSessionVersion != snapshot.Session
            || _pendingCaptureEvidence?.Id != snapshot.Capture || _rawFinalRows is not null)
            return;
        if (!string.Equals(_pendingCaptureEvidence.Role, GetSingleSelectedAcquisitionChannelRole(), StringComparison.OrdinalIgnoreCase))
            return;
        _rawStreamingRow = snapshot.Bytes;
        _rawStreamingRowIndex = snapshot.Row;
        _rawCompletedRows = snapshot.Completed;
        _rawCaptureId = snapshot.Capture;
        _rawPassIndex = -1;
        _rawRole = _pendingCaptureEvidence.Role;
        _rawResultGeneration = 0;
        if (_rawRowIsAutomatic)
            SetRawRowInput(snapshot.Row);
        RefreshRawSignal();
    }

    private void SetRawRowInput(int row)
    {
        var text = row.ToString(CultureInfo.InvariantCulture);
        SetProperty(ref _rawRowInput, text, nameof(RawSignalRowInput));
    }

    private void RefreshRawRoiIfChanged()
    {
        var width = _imageDecoder.GetDecodedPixelsPerLine();
        var columns = TryGetSelectedRoiRange(width, out var selected) ? selected.Clamp(width) : null;
        if (Equals(columns, _rawLastColumns))
            return;
        _rawLastColumns = columns;
        _rawRoiRevision++;
        if (_rawCompletedRows > 0)
            RefreshRawSignal();
    }

    private void RefreshRawSignal()
    {
        _rawRevision++;
        _rawPending = null;
        RawSignalResult = null;
        RawSignalStatisticsText = string.Empty;
        RawSignalSourceText = string.Empty;
        if (_rawCaptureId == Guid.Empty || _rawRole is null || _rawCompletedRows == 0)
        {
            RawSignalStatusText = "ScanDebug_RawSignalUnavailableSource".GetLocalized();
            return;
        }
        if (!int.TryParse(_rawRowInput, NumberStyles.None, CultureInfo.InvariantCulture, out var row)
            || row < 0 || row >= _rawCompletedRows)
        {
            RawSignalStatusText = "ScanDebug_RawSignalInvalidRow".GetLocalized();
            return;
        }
        if (!TryGetSelectedRoiRange(_imageDecoder.GetDecodedPixelsPerLine(), out var selectedRange))
        {
            RawSignalStatusText = "ScanDebug_RawSignalInvalidRoi".GetLocalized();
            return;
        }
        var columns = selectedRange.Clamp(_imageDecoder.GetDecodedPixelsPerLine());
        if (columns.Width == 0)
        {
            RawSignalStatusText = "ScanDebug_RawSignalInvalidRoi".GetLocalized();
            return;
        }

        byte[] packedRow;
        if (_rawFinalRows is not null)
        {
            packedRow = new byte[ScanDebugConstants.BytesPerLine];
            Buffer.BlockCopy(_rawFinalRows, row * packedRow.Length, packedRow, 0, packedRow.Length);
        }
        else if (_rawStreamingRowIndex == row && _rawStreamingRow is not null)
            packedRow = _rawStreamingRow;
        else
        {
            RawSignalStatusText = "ScanDebug_RawSignalCalculating".GetLocalized();
            return;
        }

        RawSignalStatusText = "ScanDebug_RawSignalCalculating".GetLocalized();
        _rawPending = new RawSignalWork(_rawRevision, _rawPageEpoch, _rawCaptureId, _rawPassIndex, _rawRole,
            _rawResultGeneration, row, _rawRoiRevision, columns, packedRow);
        StartRawWorker();
    }

}
