using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Helpers;

namespace PRISM_Utility.ViewModels;

public partial class LogViewModel : ObservableRecipient, IDisposable
{
    private readonly IDebugOutputMirrorService _debugOutputMirror;
    private readonly IUiDispatcher _dispatcher;
    private const int MaximumEntries = 500;
    private readonly List<LogDisplayEntry> _recentEntries = new();
    private bool _isActive;
    private int _generation;
    private bool _disposed;

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial Visibility EmptyStateVisibility { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LevelFilter { get; set; } = "All";

    [ObservableProperty]
    public partial bool IsFollowing { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedEntry))]
    public partial LogDisplayEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial string CopyStatusText { get; set; } = string.Empty;

    public bool HasSelectedEntry => SelectedEntry is not null;
    public bool HasVisibleErrors => Entries.Any(entry => entry.Level == LogEntryLevel.Error);
    public string EmptyStateText => _recentEntries.Count == 0
        ? "Log_StatusEmpty".GetLocalizedOrFallback("No log entries yet.")
        : "Log_NoMatches".GetLocalizedOrFallback("No entries match these filters.");
    public event EventHandler? VisibleEntriesChanged;

    public LogViewModel(IDebugOutputMirrorService debugOutputMirror, IUiDispatcher dispatcher)
    {
        _debugOutputMirror = debugOutputMirror;
        _dispatcher = dispatcher;
        StatusText = string.Empty;
        EmptyStateVisibility = Visibility.Visible;

        RefreshStatus();
    }

    public ObservableCollection<LogDisplayEntry> Entries { get; } = new();

    public void Activate()
    {
        if (_disposed || _isActive)
            return;
        _isActive = true;
        Interlocked.Increment(ref _generation);
        _debugOutputMirror.EntryMirrored += OnEntryMirrored;
        _recentEntries.Clear();
        foreach (var entry in _debugOutputMirror.RecentEntries.TakeLast(MaximumEntries))
            _recentEntries.Add(new(entry, LogEntryPresentation.Classify(entry)));
        RefreshVisibleEntries();
    }

    public void Deactivate()
    {
        _isActive = false;
        Interlocked.Increment(ref _generation);
        _debugOutputMirror.EntryMirrored -= OnEntryMirrored;
    }

    partial void OnSearchTextChanged(string value) => RefreshVisibleEntries();
    partial void OnLevelFilterChanged(string value) => RefreshVisibleEntries();
    partial void OnIsFollowingChanged(bool value)
    {
        if (value)
            VisibleEntriesChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetVisibleErrorsText()
        => string.Join(Environment.NewLine, Entries.Where(entry => entry.Level == LogEntryLevel.Error).Select(entry => entry.Display));

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        Interlocked.Increment(ref _generation);
        _debugOutputMirror.ClearRecentEntries();
        _recentEntries.Clear();
        RefreshVisibleEntries();
    }

    private bool CanClear()
        => _recentEntries.Count > 0;

    private void OnEntryMirrored(object? sender, DebugOutputMirrorEntry entry)
    {
        var generation = Volatile.Read(ref _generation);
        _dispatcher.TryEnqueue(() =>
        {
            if (_isActive && !_disposed && generation == Volatile.Read(ref _generation))
                AppendEntry(entry);
        });
    }

    private void AppendEntry(DebugOutputMirrorEntry entry)
    {
        // Mirror publishes after recording. Clear may remove an entry before its notification arrives.
        if (!_debugOutputMirror.RecentEntries.Any(retained => ReferenceEquals(retained, entry)))
            return;
        // An entry may be in both the activation snapshot and an already queued notification.
        if (_recentEntries.Any(existing => ReferenceEquals(existing.Entry, entry)))
            return;
        var display = new LogDisplayEntry(entry, LogEntryPresentation.Classify(entry));
        _recentEntries.Add(display);
        if (_recentEntries.Count > MaximumEntries)
        {
            var expired = _recentEntries[0];
            _recentEntries.RemoveAt(0);
            Entries.Remove(expired);
            if (ReferenceEquals(SelectedEntry, expired))
                SelectedEntry = null;
        }
        if (LogEntryPresentation.Matches(display, SearchText, LevelFilter))
            Entries.Add(display);
        RefreshStatus();
        VisibleEntriesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshVisibleEntries()
    {
        Entries.Clear();
        foreach (var entry in _recentEntries.Where(entry => LogEntryPresentation.Matches(entry, SearchText, LevelFilter)))
            Entries.Add(entry);
        if (SelectedEntry is not null && !Entries.Contains(SelectedEntry))
            SelectedEntry = null;
        CopyStatusText = string.Empty;
        RefreshStatus();
        VisibleEntriesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshStatus()
    {
        StatusText = "Log_FilteredStatus".GetLocalizedFormatOrFallback("Showing {0} of {1} entries · latest {2} retained", Entries.Count, _recentEntries.Count, MaximumEntries);
        EmptyStateVisibility = Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(HasVisibleErrors));
        ClearCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Deactivate();
        _disposed = true;
    }
}
