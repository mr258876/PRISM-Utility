using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Models;
using PRISM_Utility.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Log002")]
public sealed class Log002DebugOutputMirrorTests
{
    [Fact]
    public void Log002Baseline_MirrorKeepsRecentEntriesAndNotifiesSubscribers()
    {
        var fixture = CreateFixture(debugEnabled: false, fileEnabled: false);
        try
        {
            var notifications = new List<DebugOutputMirrorEntry>();
            fixture.Service.EntryMirrored += (_, entry) => notifications.Add(entry);

            fixture.Service.Mirror("Baseline", "first");
            fixture.Service.Mirror("Baseline", "   ");
            fixture.Service.Mirror("Baseline", "second");

            var recentEntries = fixture.Service.RecentEntries;
            Assert.Equal(2, recentEntries.Count);
            Assert.Equal(["first", "second"], recentEntries.Select(entry => entry.Message));
            Assert.Equal(recentEntries, notifications);

            fixture.Service.ClearRecentEntries();

            Assert.Empty(fixture.Service.RecentEntries);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task Log002_WhenSubscriberThrows_CoreOutputsAndLaterSubscribersStillRun()
    {
        var diagnostics = new ConcurrentQueue<string>();
        var fixture = CreateFixture(debugEnabled: true, fileEnabled: true, diagnosticSink: diagnostics.Enqueue);
        using var debugListener = fixture.AttachDebugListener();
        using var traceListener = fixture.AttachTraceListener();
        var marker = $"LOG002_{Guid.NewGuid():N}";
        var laterSubscriberNotified = false;

        try
        {
            fixture.Service.EntryMirrored += (_, _) => throw new InvalidOperationException("LOG002 subscriber failure");
            fixture.Service.EntryMirrored += (_, _) =>
            {
                laterSubscriberNotified = true;
                Assert.Contains(marker, debugListener.Output, StringComparison.Ordinal);
                Assert.Contains(marker, traceListener.Output, StringComparison.Ordinal);
                Console.WriteLine("CORE_FIRST");
                Console.WriteLine("SECOND_NOTIFIED");
            };

            var mirrorException = Record.Exception(() => fixture.Service.Mirror("Log002", marker));

            Assert.Null(mirrorException);
            Assert.True(laterSubscriberNotified);
            Assert.Single(diagnostics);
            Assert.Contains("LOG002 subscriber failure", Assert.Single(diagnostics), StringComparison.Ordinal);
            Console.WriteLine("NO_THROW");

            await fixture.Service.FlushAsync();
            await WaitForFileLineAsync(fixture.Paths.DebugOutputLogPath, marker);

            Assert.Contains(marker, await File.ReadAllTextAsync(fixture.Paths.DebugOutputLogPath), StringComparison.Ordinal);
            Console.WriteLine("FILE_WRITTEN");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void Log002_CoreStateAndFileSchedulingAreCompleteBeforeNotification()
    {
        var scheduledLines = new List<string>();
        var fixture = CreateFixture(debugEnabled: false, fileEnabled: true, line =>
        {
            scheduledLines.Add(line);
            return Task.CompletedTask;
        });
        try
        {
            fixture.Service.EntryMirrored += (_, entry) =>
            {
                Assert.Single(fixture.Service.RecentEntries);
                Assert.Equal(entry.Display, Assert.Single(scheduledLines));
            };

            var mirrorException = Record.Exception(() => fixture.Service.Mirror("Log002", "scheduled"));

            Assert.Null(mirrorException);
            Assert.Single(fixture.Service.RecentEntries);
            Assert.Single(scheduledLines);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void Log002_WhenDiagnosticListenerThrows_NotificationStillDoesNotEscape()
    {
        var fixture = CreateFixture(debugEnabled: true, fileEnabled: false);
        using var listenerRegistration = fixture.AttachListener(new ThrowingDiagnosticListener());
        var laterSubscriberNotifications = 0;
        try
        {
            fixture.Service.EntryMirrored += (_, _) => throw new InvalidOperationException("LOG002 subscriber failure");
            fixture.Service.EntryMirrored += (_, _) => laterSubscriberNotifications++;

            var mirrorException = Record.Exception(() => fixture.Service.Mirror("Log002", "throwing-listener"));

            Assert.Null(mirrorException);
            Assert.Equal(1, laterSubscriberNotifications);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void Log002_WhenDiagnosticListenerReentersMirror_NotificationRemainsBounded()
    {
        var fixture = CreateFixture(debugEnabled: true, fileEnabled: false);
        var listener = new RecursiveDiagnosticListener(fixture.Service);
        using var listenerRegistration = fixture.AttachListener(listener);
        var laterSubscriberNotifications = 0;
        try
        {
            fixture.Service.EntryMirrored += (_, _) => throw new InvalidOperationException("LOG002 subscriber failure");
            fixture.Service.EntryMirrored += (_, _) => laterSubscriberNotifications++;

            var mirrorException = Record.Exception(() => fixture.Service.Mirror("Log002", "recursive-listener"));

            Assert.Null(mirrorException);
            Assert.Equal(1, laterSubscriberNotifications);
            Assert.Equal(0, listener.DiagnosticCallbacks);
            Assert.True(listener.Writes > 0);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void Log002_WhenTwoSubscribersFail_EachFailureReachesTheInjectedDiagnosticSink()
    {
        var diagnostics = new ConcurrentQueue<string>();
        var fixture = CreateFixture(debugEnabled: false, fileEnabled: false, diagnosticSink: diagnostics.Enqueue);
        var laterSubscriberNotifications = 0;
        try
        {
            fixture.Service.EntryMirrored += (_, _) => throw new InvalidOperationException("first subscriber failure");
            fixture.Service.EntryMirrored += (_, _) => throw new InvalidOperationException("second subscriber failure");
            fixture.Service.EntryMirrored += (_, _) => laterSubscriberNotifications++;

            var mirrorException = Record.Exception(() => fixture.Service.Mirror("Log002", "two-failures"));

            Assert.Null(mirrorException);
            Assert.Equal(1, laterSubscriberNotifications);
            Assert.Equal(2, diagnostics.Count);
            var recordedDiagnostics = diagnostics.ToArray();
            Assert.Contains("first subscriber failure", recordedDiagnostics[0], StringComparison.Ordinal);
            Assert.Contains("second subscriber failure", recordedDiagnostics[1], StringComparison.Ordinal);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task Log002_WhenFileAppendFails_ReportsThroughTheInjectedDiagnosticSink()
    {
        var diagnostics = new ConcurrentQueue<string>();
        var fixture = CreateFixture(debugEnabled: false, fileEnabled: true, diagnosticSink: diagnostics.Enqueue);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fixture.Paths.RootPath)!);
            await File.WriteAllTextAsync(fixture.Paths.RootPath, "blocking file");

            var mirrorException = Record.Exception(() => fixture.Service.Mirror("Log002", "append-failure"));

            Assert.Null(mirrorException);
            await fixture.Service.FlushAsync();
            Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("Failed to append log file", StringComparison.Ordinal));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task Log002_FlushAsyncDoesNotCompleteBeforeAppendFailureDiagnosticIsPublished()
    {
        var appendCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnosticStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDiagnosticPublication = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnosticPublished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnostics = new ConcurrentQueue<string>();
        Log002Fixture? fixture = null;
        Task? flushTask = null;
        Task? reentrantFlushTask = null;

        try
        {
            fixture = CreateFixture(
                debugEnabled: false,
                fileEnabled: true,
                scheduleFileAppend: _ => appendCompletion.Task,
                diagnosticSink: diagnostic =>
                {
                    reentrantFlushTask = fixture!.Service.FlushAsync();
                    diagnosticStarted.TrySetResult();
                    allowDiagnosticPublication.Task.GetAwaiter().GetResult();
                    diagnostics.Enqueue(diagnostic);
                    diagnosticPublished.TrySetResult();
                });

            fixture.Service.Mirror("Log002", "controlled-append-failure");
            flushTask = fixture.Service.FlushAsync();
            Assert.True(appendCompletion.TrySetException(new IOException("LOG002 controlled append failure")));

            await diagnosticStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(reentrantFlushTask?.IsCompleted);
            Assert.False(flushTask.IsCompleted, "FlushAsync returned before the injected diagnostic sink published the append failure.");

            allowDiagnosticPublication.TrySetResult();
            await flushTask;

            Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("LOG002 controlled append failure", StringComparison.Ordinal));
        }
        finally
        {
            allowDiagnosticPublication.TrySetResult();
            if (diagnosticStarted.Task.IsCompleted)
                await diagnosticPublished.Task.WaitAsync(TimeSpan.FromSeconds(5));

            fixture?.Dispose();
        }
    }

    private static async Task WaitForFileLineAsync(string path, string marker)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(path) && (await File.ReadAllTextAsync(path)).Contains(marker, StringComparison.Ordinal))
                return;

            await Task.Delay(25);
        }

        Assert.Fail($"Timed out waiting for {path} to contain {marker}.");
    }

    private static Log002Fixture CreateFixture(
        bool debugEnabled,
        bool fileEnabled,
        Func<string, Task>? scheduleFileAppend = null,
        Action<string>? diagnosticSink = null)
    {
        var relativeRoot = Path.Combine("PRISM_LOG002", Guid.NewGuid().ToString("N"));
        var paths = ApplicationDataPathResolver.Resolve(relativeRoot);
        var settings = new TestDebugOutputSettingsService(debugEnabled, fileEnabled);
        var service = new DebugOutputMirrorService(
            settings,
            Options.Create(new LocalSettingsOptions { ApplicationDataFolder = relativeRoot }),
            scheduleFileAppend,
            diagnosticSink);

        return new Log002Fixture(service, paths);
    }

    private sealed class Log002Fixture(DebugOutputMirrorService service, ApplicationDataPaths paths) : IDisposable
    {
        public DebugOutputMirrorService Service { get; } = service;

        public ApplicationDataPaths Paths { get; } = paths;

        public RecordingTraceListener AttachDebugListener()
        {
            var listener = new RecordingTraceListener(Trace.Listeners);
            Trace.Listeners.Add(listener);
            return listener;
        }

        public RecordingTraceListener AttachTraceListener()
        {
            var listener = new RecordingTraceListener(Trace.Listeners);
            Trace.Listeners.Add(listener);
            return listener;
        }

        public IDisposable AttachListener(TraceListener listener)
        {
            Trace.Listeners.Add(listener);
            return new TraceListenerRegistration(listener);
        }

        public void Dispose()
        {
            Service.ClearRecentEntries();
            if (File.Exists(Paths.RootPath))
                File.Delete(Paths.RootPath);
            else if (Directory.Exists(Paths.RootPath))
                Directory.Delete(Paths.RootPath, recursive: true);
        }
    }

    private sealed class TestDebugOutputSettingsService(bool debugEnabled, bool fileEnabled) : IDebugOutputSettingsService
    {
        public bool IsDebugConsoleEnabled { get; } = debugEnabled;

        public bool IsFileLogEnabled { get; } = fileEnabled;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetDebugConsoleEnabledAsync(bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetFileLogEnabledAsync(bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingTraceListener(TraceListenerCollection collection) : TraceListener
    {
        private readonly StringBuilder _output = new();
        private readonly object _gate = new();
        private readonly TraceListenerCollection _collection = collection;

        public string Output
        {
            get
            {
                lock (_gate)
                {
                    return _output.ToString();
                }
            }
        }

        public override void Write(string? message)
        {
            lock (_gate)
            {
                _output.Append(message);
            }
        }

        public override void WriteLine(string? message)
        {
            lock (_gate)
            {
                _output.AppendLine(message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _collection.Remove(this);

            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingDiagnosticListener : TraceListener
    {
        public int Writes { get; private set; }

        public override void Write(string? message)
        {
            Writes++;
        }

        public override void WriteLine(string? message)
        {
            if (message?.Contains("subscriber failed", StringComparison.Ordinal) == true)
                throw new InvalidOperationException("LOG002 diagnostic listener failure");
        }
    }

    private sealed class RecursiveDiagnosticListener(DebugOutputMirrorService service) : TraceListener
    {
        public int DiagnosticCallbacks { get; private set; }

        public int Writes { get; private set; }

        public override void Write(string? message)
        {
            Writes++;
        }

        public override void WriteLine(string? message)
        {
            Writes++;
            if (message?.Contains("subscriber failed", StringComparison.Ordinal) != true)
                return;

            DiagnosticCallbacks++;
            service.Mirror("Log002", "nested-diagnostic");
        }
    }

    private sealed class TraceListenerRegistration(TraceListener listener) : IDisposable
    {
        public void Dispose()
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }
}
