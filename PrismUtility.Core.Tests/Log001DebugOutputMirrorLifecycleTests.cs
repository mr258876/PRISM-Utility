using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Models;
using PRISM_Utility.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Log001")]
public sealed class Log001DebugOutputMirrorLifecycleTests
{
    [Fact]
    public async Task Log001Baseline_MirrorPublishesCoreStateAndSchedulesAppendImmediately()
    {
        var scheduler = new ControlledAppendScheduler();
        await using var fixture = CreateFixture(fileAppendScheduler: scheduler.Schedule);
        try
        {
            var notifications = new List<DebugOutputMirrorEntry>();
            fixture.Service.EntryMirrored += (_, entry) => notifications.Add(entry);

            fixture.Service.Mirror("Log001", "immediate");

            Assert.Equal(["immediate"], fixture.Service.RecentEntries.Select(entry => entry.Message));
            Assert.Equal(["immediate"], notifications.Select(entry => entry.Message));
            Assert.Single(scheduler.Lines);
            Assert.False(fixture.Service.FlushAsync().IsCompleted);
        }
        finally
        {
            scheduler.CompleteAll();
        }
    }

    [Fact]
    public async Task Log001_FlushAsync_AwaitsAllAppendsQueuedBeforeTheSnapshot()
    {
        var scheduler = new ControlledAppendScheduler();
        await using var fixture = CreateFixture(fileAppendScheduler: scheduler.Schedule);
        try
        {
            fixture.Service.Mirror("Log001", "first");
            fixture.Service.Mirror("Log001", "second");
            var flush = fixture.Service.FlushAsync();

            Assert.False(flush.IsCompleted);
            Assert.Equal(["first", "second"], scheduler.Messages);

            scheduler.CompleteNext();
            Assert.False(flush.IsCompleted);

            scheduler.CompleteNext();
            await flush;
            Assert.Equal(0, scheduler.PendingCount);
        }
        finally
        {
            scheduler.CompleteAll();
        }
    }

    [Fact]
    public async Task Log001_FlushAsync_DoesNotWaitForAnAppendQueuedAfterItsSnapshot()
    {
        var scheduler = new ControlledAppendScheduler();
        await using var fixture = CreateFixture(fileAppendScheduler: scheduler.Schedule);
        try
        {
            fixture.Service.Mirror("Log001", "before-flush");
            var firstFlush = fixture.Service.FlushAsync();
            fixture.Service.Mirror("Log001", "after-flush");

            scheduler.CompleteNext();
            await firstFlush;
            Assert.Equal(1, scheduler.PendingCount);

            scheduler.CompleteNext();
            await fixture.Service.FlushAsync();
        }
        finally
        {
            scheduler.CompleteAll();
        }
    }

    [Fact]
    public async Task Log001_ConcurrentFlushAsyncCalls_AwaitTheSameInFlightAppend()
    {
        var scheduler = new ControlledAppendScheduler();
        await using var fixture = CreateFixture(fileAppendScheduler: scheduler.Schedule);
        try
        {
            fixture.Service.Mirror("Log001", "concurrent");
            var flushes = Enumerable.Range(0, 8).Select(_ => fixture.Service.FlushAsync()).ToArray();

            Assert.All(flushes, flush => Assert.False(flush.IsCompleted));

            scheduler.CompleteNext();
            await Task.WhenAll(flushes);
            Assert.Equal(0, scheduler.PendingCount);
        }
        finally
        {
            scheduler.CompleteAll();
        }
    }

    [Fact]
    public async Task Log001_WhenAppendFails_FlushAsyncReportsTheFailureWithoutThrowing()
    {
        var diagnostics = new ConcurrentQueue<string>();
        await using var fixture = CreateFixture(
            fileAppendScheduler: _ => Task.FromException(new IOException("LOG001 append failure")),
            diagnosticSink: diagnostics.Enqueue);

        fixture.Service.Mirror("Log001", "failure");

        var flushException = await Record.ExceptionAsync(fixture.Service.FlushAsync);

        Assert.Null(flushException);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("LOG001 append failure", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Log001_WhenDiagnosticSinkReentersFlushAndShutdown_AppendCompletionDoesNotWaitOnItself()
    {
        var diagnostics = new ConcurrentQueue<string>();
        var scheduler = new ControlledAppendScheduler();
        var sinkReentered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DebugOutputMirrorService? service = null;
        await using var fixture = CreateFixture(scheduler.Schedule, diagnostic =>
        {
            diagnostics.Enqueue(diagnostic);
            service!.FlushAsync().GetAwaiter().GetResult();
            service.ShutdownAsync().GetAwaiter().GetResult();
            sinkReentered.TrySetResult();
        });
        service = fixture.Service;
        try
        {
            service.Mirror("Log001", "reentrant-diagnostic");
            scheduler.FailNext(new IOException("LOG001 reentrant failure"));

            await sinkReentered.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("LOG001 reentrant failure", StringComparison.Ordinal));
            Assert.True(service.FlushAsync().IsCompleted);
        }
        finally
        {
            scheduler.CompleteAll();
        }
    }

    [Fact]
    public async Task Log001_WhenDiagnosticSinkReentersShutdownDuringExternalShutdown_DoesNotWaitOnItsOwnAppend()
    {
        var diagnostics = new ConcurrentQueue<string>();
        var scheduler = new ControlledAppendScheduler();
        var sinkReentered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDiagnosticReturn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reentrantShutdownCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        DebugOutputMirrorService? service = null;
        await using var fixture = CreateFixture(scheduler.Schedule, diagnostic =>
        {
            diagnostics.Enqueue(diagnostic);
            var reentrantShutdown = service!.ShutdownAsync();
            var completed = reentrantShutdown.IsCompleted;
            if (completed)
                reentrantShutdown.GetAwaiter().GetResult();

            reentrantShutdownCompleted.TrySetResult(completed);
            sinkReentered.TrySetResult();
            allowDiagnosticReturn.Task.GetAwaiter().GetResult();
        });
        service = fixture.Service;
        Task? externalShutdown = null;
        try
        {
            service.Mirror("Log001", "external-shutdown-reentrant-diagnostic");
            externalShutdown = service.ShutdownAsync();
            scheduler.FailNext(new IOException("LOG001 external shutdown reentrant failure"));

            await sinkReentered.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.True(await reentrantShutdownCompleted.Task);
            Assert.False(externalShutdown.IsCompleted);

            allowDiagnosticReturn.TrySetResult();
            await externalShutdown;

            Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("LOG001 external shutdown reentrant failure", StringComparison.Ordinal));
        }
        finally
        {
            allowDiagnosticReturn.TrySetResult();
            if (externalShutdown is not null)
                await externalShutdown.WaitAsync(TimeSpan.FromSeconds(5));

            scheduler.CompleteAll();
        }
    }

    [Fact]
    public async Task Log001_WhenAppendFailureDiagnosticSinkThrows_ShutdownStillCompletes()
    {
        var scheduler = new ControlledAppendScheduler();
        var sinkCalls = 0;
        await using var fixture = CreateFixture(scheduler.Schedule, _ =>
        {
            Interlocked.Increment(ref sinkCalls);
            throw new InvalidOperationException("LOG001 diagnostic sink failure");
        });
        try
        {
            fixture.Service.Mirror("Log001", "throwing-diagnostic-sink");
            var shutdown = fixture.Service.ShutdownAsync();
            scheduler.FailNext(new IOException("LOG001 append failure with throwing diagnostic sink"));

            await shutdown.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(1, Volatile.Read(ref sinkCalls));
        }
        finally
        {
            scheduler.CompleteAll();
        }
    }

    [Fact]
    public async Task Log001_ShutdownAsync_AwaitsInFlightAppendAndIsIdempotent()
    {
        var scheduler = new ControlledAppendScheduler();
        await using var fixture = CreateFixture(fileAppendScheduler: scheduler.Schedule);
        try
        {
            fixture.Service.Mirror("Log001", "in-flight");
            var firstShutdown = fixture.Service.ShutdownAsync();
            var repeatedShutdown = fixture.Service.ShutdownAsync();

            Assert.Same(firstShutdown, repeatedShutdown);
            Assert.False(firstShutdown.IsCompleted);

            scheduler.CompleteNext();
            await firstShutdown;
            await fixture.Service.FlushAsync();
            Assert.Equal(0, scheduler.PendingCount);
        }
        finally
        {
            scheduler.CompleteAll();
        }
    }

    [Fact]
    public async Task Log001_HungAppend_ShutdownCancellationBoundsCallerWhileOwnedCleanupContinues()
    {
        var scheduler = new ControlledAppendScheduler();
        await using var fixture = CreateFixture(fileAppendScheduler: scheduler.Schedule);
        try
        {
            fixture.Service.Mirror("Log001", "hung-append");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.ShutdownAsync(cancellation.Token));

            var ownedShutdown = fixture.Service.ShutdownAsync();
            Assert.False(ownedShutdown.IsCompleted);

            scheduler.CompleteNext();
            await ownedShutdown;
        }
        finally
        {
            scheduler.CompleteAll();
        }
    }

    [Fact]
    public async Task Log001_AfterShutdown_MirrorKeepsCoreOutputButRejectsNewFileAppend()
    {
        var diagnostics = new ConcurrentQueue<string>();
        var scheduler = new ControlledAppendScheduler();
        await using var fixture = CreateFixture(scheduler.Schedule, diagnostics.Enqueue);

        await fixture.Service.ShutdownAsync();
        fixture.Service.Mirror("Log001", "after-shutdown");

        Assert.Equal(["after-shutdown"], fixture.Service.RecentEntries.Select(entry => entry.Message));
        Assert.Empty(scheduler.Lines);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("after-shutdown", StringComparison.Ordinal));
        Assert.True(fixture.Service.FlushAsync().IsCompleted);
    }

    [Fact]
    public async Task Log001_FaultedAppendIsObservedWithoutAnUnobservedTaskException()
    {
        var diagnostics = new ConcurrentQueue<string>();
        var scheduler = new ControlledAppendScheduler();
        var unobservedExceptions = new ConcurrentQueue<AggregateException>();
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, eventArgs) =>
        {
            unobservedExceptions.Enqueue(eventArgs.Exception);
            eventArgs.SetObserved();
        };
        TaskScheduler.UnobservedTaskException += handler;
        await using var fixture = CreateFixture(scheduler.Schedule, diagnostics.Enqueue);
        try
        {
            fixture.Service.Mirror("Log001", "faulted-background-append");
            var appendTaskReference = scheduler.LastTaskReference;
            scheduler.FailNext(new IOException("LOG001 background failure"));

            await WaitForAsync(() => diagnostics.Any(diagnostic => diagnostic.Contains("LOG001 background failure", StringComparison.Ordinal)));

            Assert.Equal(0, scheduler.PendingCount);
            await fixture.Service.FlushAsync();
            await WaitForCollectionAsync(appendTaskReference);
            Assert.Empty(unobservedExceptions);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
            scheduler.CompleteAll();
        }
    }

    [Fact]
    public async Task Log001_ActualTempFileShutdownHarness_PersistsOrderedContentAndShutdownDiagnostic()
    {
        var diagnostics = new ConcurrentQueue<string>();
        await using var fixture = CreateFixture(diagnosticSink: diagnostics.Enqueue);
        var firstMarker = $"LOG001_FIRST_{Guid.NewGuid():N}";
        var secondMarker = $"LOG001_SECOND_{Guid.NewGuid():N}";
        var rejectedMarker = $"LOG001_REJECTED_{Guid.NewGuid():N}";

        fixture.Service.Mirror("Log001", firstMarker);
        fixture.Service.Mirror("Log001", secondMarker);
        await fixture.Service.ShutdownAsync();
        fixture.Service.Mirror("Log001", rejectedMarker);

        var lines = await File.ReadAllLinesAsync(fixture.Paths.DebugOutputLogPath);

        Assert.Equal(2, lines.Length);
        Assert.Contains(firstMarker, lines[0], StringComparison.Ordinal);
        Assert.Contains(secondMarker, lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain(rejectedMarker, lines, StringComparer.Ordinal);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Contains(rejectedMarker, StringComparison.Ordinal));
        Assert.True(fixture.Service.FlushAsync().IsCompleted);
    }

    [Fact]
    public void Log001_AppClose_BoundsMirrorShutdownAfterScannerCleanupWithoutSynchronouslyBlocking()
    {
        var source = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "App.xaml.cs"));
        var handler = ExtractMethod(source, "private async void MainWindow_Closed");

        Assert.Contains("using var mirrorShutdownTimeout = new CancellationTokenSource(MirrorShutdownTimeout);", handler, StringComparison.Ordinal);
        Assert.Contains("await GetService<IDebugOutputMirrorService>().ShutdownAsync(mirrorShutdownTimeout.Token);", handler, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException) when (mirrorShutdownTimeout.IsCancellationRequested)", handler, StringComparison.Ordinal);
        Assert.Contains("Debugger.Log", handler, StringComparison.Ordinal);
        Assert.True(
            handler.IndexOf("_scannerDeviceSessionManager.ShutdownAsync", StringComparison.Ordinal) <
            handler.IndexOf("GetService<IDebugOutputMirrorService>().ShutdownAsync", StringComparison.Ordinal),
            "Scanner cleanup must begin before bounded mirror shutdown.");
        Assert.DoesNotContain("GetAwaiter().GetResult", handler, StringComparison.Ordinal);
        Assert.DoesNotContain(".Wait()", handler, StringComparison.Ordinal);
    }

    private static Log001Fixture CreateFixture(
        Func<string, Task>? fileAppendScheduler = null,
        Action<string>? diagnosticSink = null)
    {
        var relativeRoot = Path.Combine("PRISM_LOG001", Guid.NewGuid().ToString("N"));
        var paths = ApplicationDataPathResolver.Resolve(relativeRoot);
        var service = new DebugOutputMirrorService(
            new TestDebugOutputSettingsService(fileLogEnabled: true),
            Options.Create(new LocalSettingsOptions { ApplicationDataFolder = relativeRoot }),
            fileAppendScheduler,
            diagnosticSink);

        return new Log001Fixture(service, paths);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(20);
        }

        Assert.True(condition(), "Timed out waiting for the expected asynchronous diagnostic.");
    }

    private static async Task WaitForCollectionAsync(WeakReference taskReference)
    {
        for (var attempt = 0; taskReference.IsAlive && attempt < 20; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Yield();
        }

        Assert.False(taskReference.IsAlive, "The faulted append task should be collectible after service observation.");
    }

    private static string ExtractMethod(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Could not find {signature} in App.xaml.cs.");

        var bodyStart = source.IndexOf('{', signatureIndex);
        Assert.True(bodyStart >= 0, $"Could not find the body for {signature} in App.xaml.cs.");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[bodyStart..(index + 1)];
        }

        throw new InvalidOperationException($"Could not find the end of {signature} in App.xaml.cs.");
    }

    private static string FindHostSoftwareRoot()
    {
        foreach (var startDirectory in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                    return directory.FullName;

                var nestedHostSoftwareRoot = Path.Combine(directory.FullName, "Host Software");
                if (Directory.Exists(Path.Combine(nestedHostSoftwareRoot, "PRISM Utility")))
                    return nestedHostSoftwareRoot;
            }
        }

        throw new DirectoryNotFoundException("Could not locate Host Software source root.");
    }

    private sealed class Log001Fixture(DebugOutputMirrorService service, ApplicationDataPaths paths) : IAsyncDisposable
    {
        public DebugOutputMirrorService Service { get; } = service;

        public ApplicationDataPaths Paths { get; } = paths;

        public async ValueTask DisposeAsync()
        {
            await Service.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(5));

            if (File.Exists(Paths.RootPath))
                File.Delete(Paths.RootPath);
            else if (Directory.Exists(Paths.RootPath))
                Directory.Delete(Paths.RootPath, recursive: true);
        }
    }

    private sealed class TestDebugOutputSettingsService(bool fileLogEnabled) : IDebugOutputSettingsService
    {
        public bool IsDebugConsoleEnabled => false;

        public bool IsFileLogEnabled => fileLogEnabled;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetDebugConsoleEnabledAsync(bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetFileLogEnabledAsync(bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ControlledAppendScheduler
    {
        private readonly object _gate = new();
        private readonly List<PendingAppend> _pending = [];
        private readonly List<string> _lines = [];
        private WeakReference? _lastTaskReference;

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_gate)
                    return _lines.ToArray();
            }
        }

        public IReadOnlyList<string> Messages
            => Lines.Select(line => line[(line.LastIndexOf("] ", StringComparison.Ordinal) + 2)..]).ToArray();

        public int PendingCount
        {
            get
            {
                lock (_gate)
                    return _pending.Count;
            }
        }

        public WeakReference LastTaskReference
        {
            get
            {
                lock (_gate)
                    return _lastTaskReference ?? throw new InvalidOperationException("No append task has been scheduled.");
            }
        }

        public Task Schedule(string line)
        {
            var append = new PendingAppend();
            lock (_gate)
            {
                _lines.Add(line);
                _pending.Add(append);
                _lastTaskReference = new WeakReference(append.Task);
            }

            return append.Task;
        }

        public void CompleteNext()
            => TakeNext().SetResult();

        public void FailNext(Exception exception)
            => TakeNext().SetException(exception);

        public void CompleteAll()
        {
            PendingAppend[] pending;
            lock (_gate)
            {
                pending = _pending.ToArray();
                _pending.Clear();
            }

            foreach (var append in pending)
                append.SetResult();
        }

        private PendingAppend TakeNext()
        {
            lock (_gate)
            {
                var append = _pending[0];
                _pending.RemoveAt(0);
                return append;
            }
        }

        private sealed class PendingAppend
        {
            private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Task => _completion.Task;

            public void SetResult() => _completion.TrySetResult();

            public void SetException(Exception exception) => _completion.TrySetException(exception);
        }
    }
}
