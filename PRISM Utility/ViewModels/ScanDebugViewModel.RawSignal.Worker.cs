using System.Globalization;
using PRISM_Utility.Core.Services;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;

public partial class ScanDebugViewModel
{
    private void StartRawWorker()
    {
        if (_rawPending is not { } work || Interlocked.CompareExchange(ref _rawWorkerRunning, 1, 0) != 0)
            return;
        _rawPending = null;
        _ = Task.Run(() => new ScanRawSignalAnalyzer(_imageDecoder).Analyze(
                work.PackedRow, 1, 0, 0, work.Columns, 0))
            .ContinueWith(task =>
            {
                _ = task.Exception;
                if (!_dispatcher.TryEnqueue(() =>
                {
                    Interlocked.Exchange(ref _rawWorkerRunning, 0);
                    if (_rawPageActive && work.PageEpoch == _rawPageEpoch
                        && work.Revision == _rawRevision && work.CaptureId == _rawCaptureId
                        && work.PassIndex == _rawPassIndex && work.Role == _rawRole
                        && work.ResultGeneration == _rawResultGeneration && work.RoiRevision == _rawRoiRevision)
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            RawSignalResult = task.Result with { StartRow = work.Row, EndRowInclusive = work.Row, ProfileRow = work.Row };
                            RawSignalSourceText = "ScanDebug_RawSignalSource".GetLocalizedFormatOrFallback(
                                "Capture {0}, pass {1}, role {2}, version {3}, row {4}, columns {5}-{6}, completed {7}",
                                work.CaptureId, work.PassIndex, work.Role,
                                work.PassIndex < 0 ? "ScanDebug_CaptureUnknown".GetLocalized() : work.ResultGeneration.ToString(CultureInfo.InvariantCulture), work.Row,
                                work.Columns.Start, work.Columns.EndInclusive, _rawCompletedRows);
                            if (_lastWorkflowResult is not null && GetSingleActiveRoleIndex(_lastWorkflowChannelAssignment ?? BuildCapturedWorkflowChannelAssignment(_lastWorkflowResult)) < 0)
                                RawSignalSourceText += "ScanDebug_RawSignalCompositeCoordinates".GetLocalized();
                            RawSignalStatisticsText = "ScanDebug_RawSignalStatistics".GetLocalizedFormatOrFallback(
                                "N={0}; min={1}; max={2}; mean={3:F2} ADU; saturation={4}/{0} (65535)",
                                task.Result.SampleCount, task.Result.Minimum, task.Result.Maximum,
                                task.Result.Mean, task.Result.FullScaleCount);
                            RawSignalStatusText = "ScanDebug_RawSignalReady".GetLocalized();
                        }
                        else
                            RawSignalStatusText = "ScanDebug_RawSignalUnavailableData".GetLocalized();
                    }
                    StartRawWorker();
                }))
                    Interlocked.Exchange(ref _rawWorkerRunning, 0);
            }, TaskScheduler.Default);
    }
}
