using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Helpers;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class LogViewModelTests
{
    [Theory]
    [InlineData("Settings.Persistence", "Color save failed: IOException: disk full", LogEntryLevel.Error)]
    [InlineData("Scan.Status", "加载配置失败：文件格式无效", LogEntryLevel.Error)]
    [InlineData("Scan.Diagnostic", "ACK monitor timed out. busy=0", LogEntryLevel.Warning)]
    [InlineData("Scan", "[WARN] Retry requested", LogEntryLevel.Warning)]
    [InlineData("Scan", "[INFO] Previous failed request recovered", LogEntryLevel.Information)]
    [InlineData("Scan.Diagnostic", "START_SCAN sent", LogEntryLevel.Unknown)]
    [InlineData("Scan.Diagnostic", "error=0, timeoutSlices=12", LogEntryLevel.Unknown)]
    public void Classify_UsesKnownSignalsAndPreservesUnclassifiedEntries(string source, string message, LogEntryLevel expected)
        => Assert.Equal(expected, LogEntryPresentation.Classify(Entry(source, message)));

    [Fact]
    public void Filters_CombineSearchAndLevel_AndCopyOnlyVisibleErrors()
    {
        var mirror = new FakeMirror();
        mirror.Mirror("Scan.Status", "load failed: red");
        mirror.Mirror("Scan.Status", "load failed: blue");
        mirror.Mirror("Scan.Diagnostic", "red channel ready");
        using var viewModel = new LogViewModel(mirror, new QueuedDispatcher());
        viewModel.Activate();

        viewModel.SearchText = " RED ";
        viewModel.LevelFilter = "Error";

        Assert.Single(viewModel.Entries);
        Assert.Contains("load failed: red", viewModel.GetVisibleErrorsText(), StringComparison.Ordinal);
        Assert.DoesNotContain("blue", viewModel.GetVisibleErrorsText(), StringComparison.Ordinal);
        Assert.True(viewModel.HasVisibleErrors);

        viewModel.LevelFilter = "Unknown";
        Assert.Equal("red channel ready", Assert.Single(viewModel.Entries).Entry.Message);
        Assert.False(viewModel.HasVisibleErrors);
        Assert.Empty(viewModel.GetVisibleErrorsText());

        viewModel.SearchText = "no match";
        Assert.Empty(viewModel.Entries);
        Assert.True(viewModel.ClearCommand.CanExecute(null));
    }

    [Fact]
    public void Reentry_ResubscribesOnceAndRejectsQueuedEventsFromPreviousActivation()
    {
        var mirror = new FakeMirror();
        var dispatcher = new QueuedDispatcher();
        using var viewModel = new LogViewModel(mirror, dispatcher);
        viewModel.Activate();
        viewModel.Activate();
        Assert.Equal(1, mirror.SubscriberCount);

        mirror.Mirror("Scan", "before leaving");
        viewModel.Deactivate();
        Assert.Equal(0, mirror.SubscriberCount);
        mirror.Mirror("Scan", "while away");
        viewModel.Activate();
        dispatcher.Drain();

        Assert.Equal(new[] { "before leaving", "while away" }, viewModel.Entries.Select(entry => entry.Entry.Message));
        Assert.Equal(1, mirror.SubscriberCount);
        viewModel.Dispose();
        Assert.Equal(0, mirror.SubscriberCount);
    }

    [Fact]
    public void Clear_DoesNotRestoreEntriesAlreadyQueuedForTheUi()
    {
        var mirror = new FakeMirror();
        var dispatcher = new QueuedDispatcher();
        mirror.Mirror("Scan", "initial");
        using var viewModel = new LogViewModel(mirror, dispatcher);
        viewModel.Activate();
        mirror.Mirror("Scan", "queued before clear");

        viewModel.ClearCommand.Execute(null);
        dispatcher.Drain();

        Assert.Empty(viewModel.Entries);
        Assert.Empty(mirror.RecentEntries);
        Assert.False(viewModel.ClearCommand.CanExecute(null));
        mirror.Mirror("Scan", "new after clear");
        dispatcher.Drain();
        Assert.Equal("new after clear", Assert.Single(viewModel.Entries).Entry.Message);
    }

    [Fact]
    public void Clear_RejectsAnEntryRecordedBeforeClearButNotifiedAfterwards()
    {
        var mirror = new FakeMirror();
        var dispatcher = new QueuedDispatcher();
        mirror.Mirror("Scan", "initial");
        using var viewModel = new LogViewModel(mirror, dispatcher);
        viewModel.Activate();
        var delayedEntry = mirror.RecordWithoutNotification("Scan", "recorded before clear");

        viewModel.ClearCommand.Execute(null);
        mirror.Notify(delayedEntry);
        dispatcher.Drain();

        Assert.Empty(viewModel.Entries);
        Assert.Empty(mirror.RecentEntries);
        mirror.Mirror("Scan", "recorded after clear");
        dispatcher.Drain();
        Assert.Equal("recorded after clear", Assert.Single(viewModel.Entries).Entry.Message);
    }

    [Fact]
    public void PausedFollowing_StillReceivesLogsAndKeepsOnlyLatestFiveHundred()
    {
        var mirror = new FakeMirror();
        var dispatcher = new QueuedDispatcher();
        using var viewModel = new LogViewModel(mirror, dispatcher);
        viewModel.Activate();
        viewModel.IsFollowing = false;
        mirror.Mirror("Scan", "0");
        dispatcher.Drain();
        viewModel.SelectedEntry = Assert.Single(viewModel.Entries);
        for (var index = 1; index <= 510; index++)
            mirror.Mirror("Scan", index.ToString());
        dispatcher.Drain();

        Assert.False(viewModel.IsFollowing);
        Assert.Equal(500, viewModel.Entries.Count);
        Assert.Equal("11", viewModel.Entries[0].Entry.Message);
        Assert.Equal("510", viewModel.Entries[^1].Entry.Message);
        Assert.Null(viewModel.SelectedEntry);
    }

    [Fact]
    public void NewEntries_RespectActiveFiltersWithoutDiscardingOtherRetainedEntries()
    {
        var mirror = new FakeMirror();
        var dispatcher = new QueuedDispatcher();
        using var viewModel = new LogViewModel(mirror, dispatcher);
        viewModel.Activate();
        viewModel.LevelFilter = "Error";
        mirror.Mirror("Scan", "ready");
        mirror.Mirror("Scan", "scan failed");
        dispatcher.Drain();
        Assert.Single(viewModel.Entries);

        viewModel.LevelFilter = "All";
        Assert.Equal(2, viewModel.Entries.Count);
    }

    private static DebugOutputMirrorEntry Entry(string source, string message)
        => new(DateTimeOffset.UnixEpoch, source, message, $"[2026-10-13 12:00:00] [{source}] {message}");

    private sealed class QueuedDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _actions = new();
        public bool TryEnqueue(Action action) { _actions.Enqueue(action); return true; }
        public void Drain() { while (_actions.TryDequeue(out var action)) action(); }
    }

    private sealed class FakeMirror : IDebugOutputMirrorService
    {
        private readonly List<DebugOutputMirrorEntry> _entries = new();
        public event EventHandler<DebugOutputMirrorEntry>? EntryMirrored;
        public int SubscriberCount => EntryMirrored?.GetInvocationList().Length ?? 0;
        public IReadOnlyList<DebugOutputMirrorEntry> RecentEntries => _entries.ToArray();
        public void Mirror(string source, string message)
            => Notify(RecordWithoutNotification(source, message));
        public DebugOutputMirrorEntry RecordWithoutNotification(string source, string message)
        {
            var entry = Entry(source, message);
            _entries.Add(entry);
            if (_entries.Count > 500) _entries.RemoveAt(0);
            return entry;
        }
        public void Notify(DebugOutputMirrorEntry entry) => EntryMirrored?.Invoke(this, entry);
        public void ClearRecentEntries() => _entries.Clear();
        public Task FlushAsync() => Task.CompletedTask;
        public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
