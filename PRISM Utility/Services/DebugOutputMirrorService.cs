using System.Diagnostics;
using Microsoft.Extensions.Options;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Models;

namespace PRISM_Utility.Services;

public sealed class DebugOutputMirrorService : IDebugOutputMirrorService
{
    private const int MaxRecentEntryCount = 500;

    [ThreadStatic]
    private static Stack<DebugOutputMirrorService>? s_diagnosticDispatchStack;

    private readonly IDebugOutputSettingsService _settingsService;
    private readonly object _defaultAppendGate = new();
    private readonly SemaphoreSlim _logFileGate = new(1, 1);
    private readonly object _appendWorkGate = new();
    private readonly object _recentEntriesGate = new();
    private readonly List<AppendWork> _appendWork = new();
    private readonly List<DebugOutputMirrorEntry> _recentEntries = new();
    private readonly string _logFilePath;
    private readonly Func<string, Task> _fileAppendScheduler;
    private readonly Action<string> _diagnosticSink;
    private bool _acceptFileAppends = true;
    private Task _defaultAppendTail = Task.CompletedTask;
    private Task? _shutdownTask;

    public DebugOutputMirrorService(
        IDebugOutputSettingsService settingsService,
        IOptions<LocalSettingsOptions> options,
        Func<string, Task>? fileAppendScheduler = null,
        Action<string>? diagnosticSink = null)
    {
        _settingsService = settingsService;

        _logFilePath = ApplicationDataPathResolver.Resolve(options.Value.ApplicationDataFolder).DebugOutputLogPath;
        _fileAppendScheduler = fileAppendScheduler ?? ScheduleDefaultAppendAsync;
        _diagnosticSink = diagnosticSink ?? WriteDefaultDiagnostic;
    }

    public event EventHandler<DebugOutputMirrorEntry>? EntryMirrored;

    public IReadOnlyList<DebugOutputMirrorEntry> RecentEntries
    {
        get
        {
            lock (_recentEntriesGate)
            {
                return _recentEntries.ToArray();
            }
        }
    }

    public void Mirror(string source, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var timestamp = DateTimeOffset.Now;
        var line = $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {message}";
        var entry = new DebugOutputMirrorEntry(timestamp, source, message, line);
        AddRecentEntry(entry);

        if (_settingsService.IsDebugConsoleEnabled)
        {
            Debug.WriteLine(line);
            Trace.WriteLine(line);
        }

        if (_settingsService.IsFileLogEnabled)
            QueueFileAppend(line);

        NotifySubscribers(entry);
    }

    public Task FlushAsync()
    {
        Task[] appendCompletions;
        lock (_appendWorkGate)
        {
            appendCompletions = _appendWork.Select(work => work.Completion.Task).ToArray();
        }

        return Task.WhenAll(appendCompletions);
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (IsDispatchingDiagnosticOnCurrentThread)
            return Task.CompletedTask;

        Task shutdownTask;
        lock (_appendWorkGate)
        {
            if (_shutdownTask is not null)
                shutdownTask = _shutdownTask;
            else
            {
                _acceptFileAppends = false;
                _shutdownTask = Task.WhenAll(_appendWork.Select(work => work.Completion.Task));
                shutdownTask = _shutdownTask;
            }
        }

        return ct.CanBeCanceled ? shutdownTask.WaitAsync(ct) : shutdownTask;
    }

    public void ClearRecentEntries()
    {
        lock (_recentEntriesGate)
        {
            _recentEntries.Clear();
        }
    }

    private void AddRecentEntry(DebugOutputMirrorEntry entry)
    {
        lock (_recentEntriesGate)
        {
            _recentEntries.Add(entry);
            if (_recentEntries.Count > MaxRecentEntryCount)
                _recentEntries.RemoveRange(0, _recentEntries.Count - MaxRecentEntryCount);
        }
    }

    private void NotifySubscribers(DebugOutputMirrorEntry entry)
    {
        var subscribers = EntryMirrored?.GetInvocationList();
        if (subscribers is null)
            return;

        foreach (var subscriber in subscribers)
        {
            try
            {
                ((EventHandler<DebugOutputMirrorEntry>)subscriber)(this, entry);
            }
            catch (Exception ex)
            {
                ReportSubscriberFailure(ex);
            }
        }
    }

    private void ReportSubscriberFailure(Exception exception)
    {
        ReportDiagnostic($"[DebugOutputMirror] EntryMirrored subscriber failed: {exception.Message}");
    }

    private bool IsDispatchingDiagnosticOnCurrentThread
        => s_diagnosticDispatchStack?.Contains(this) == true;

    private void ReportDiagnostic(string diagnostic)
    {
        var dispatchStack = s_diagnosticDispatchStack ??= new Stack<DebugOutputMirrorService>();
        dispatchStack.Push(this);
        try
        {
            TryWriteDiagnostic(_diagnosticSink, diagnostic);
        }
        finally
        {
            dispatchStack.Pop();
            if (dispatchStack.Count == 0)
                s_diagnosticDispatchStack = null;
        }
    }

    private static bool TryWriteDiagnostic(Action<string> write, string diagnostic)
    {
        try
        {
            write(diagnostic);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void WriteDefaultDiagnostic(string diagnostic)
        => Debugger.Log(0, "DebugOutputMirror", diagnostic + Environment.NewLine);

    private void QueueFileAppend(string line)
    {
        AppendWork? appendWork = null;
        Exception? schedulingFailure = null;
        var rejectedAfterShutdown = false;

        lock (_appendWorkGate)
        {
            if (!_acceptFileAppends)
            {
                rejectedAfterShutdown = true;
            }
            else
            {
                try
                {
                    appendWork = new AppendWork(_fileAppendScheduler(line));
                    _appendWork.Add(appendWork);
                }
                catch (Exception ex)
                {
                    schedulingFailure = ex;
                }
            }
        }

        if (rejectedAfterShutdown)
        {
            ReportDiagnostic($"[DebugOutputMirror] Skipped log file append after shutdown: {line}");
            return;
        }

        if (schedulingFailure is not null)
        {
            ReportAppendFailure(schedulingFailure);
            return;
        }

        if (appendWork is not null)
            _ = ObserveAppendAsync(appendWork);
    }

    private async Task ObserveAppendAsync(AppendWork appendWork)
    {
        Exception? appendFailure = null;
        try
        {
            await appendWork.AppendTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            appendFailure = ex;
        }
        finally
        {
            lock (_appendWorkGate)
            {
                _appendWork.Remove(appendWork);
            }
        }

        if (appendFailure is not null)
            ReportAppendFailure(appendFailure);

        appendWork.Completion.TrySetResult();
    }

    private void ReportAppendFailure(Exception exception)
        => ReportDiagnostic($"[DebugOutputMirror] Failed to append log file: {exception.Message}");

    private Task ScheduleDefaultAppendAsync(string line)
    {
        lock (_defaultAppendGate)
        {
            _defaultAppendTail = _defaultAppendTail
                .ContinueWith(
                    _ => Task.Run(() => AppendLineAsync(line)),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default)
                .Unwrap();

            return _defaultAppendTail;
        }
    }

    private async Task AppendLineAsync(string line)
    {
        var directoryPath = Path.GetDirectoryName(_logFilePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
            return;

        Directory.CreateDirectory(directoryPath);

        await _logFileGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine).ConfigureAwait(false);
        }
        finally
        {
            _logFileGate.Release();
        }
    }

    private sealed class AppendWork(Task appendTask)
    {
        public Task AppendTask { get; } = appendTask;

        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
