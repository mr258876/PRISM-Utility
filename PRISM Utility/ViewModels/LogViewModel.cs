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
    private bool _disposed;

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial Visibility EmptyStateVisibility { get; set; }

    public LogViewModel(IDebugOutputMirrorService debugOutputMirror, IUiDispatcher dispatcher)
    {
        _debugOutputMirror = debugOutputMirror;
        _dispatcher = dispatcher;
        StatusText = string.Empty;
        EmptyStateVisibility = Visibility.Visible;

        foreach (var entry in _debugOutputMirror.RecentEntries)
            Entries.Add(entry);

        _debugOutputMirror.EntryMirrored += OnEntryMirrored;
        RefreshStatus();
    }

    public ObservableCollection<DebugOutputMirrorEntry> Entries { get; } = new();

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        _debugOutputMirror.ClearRecentEntries();
        Entries.Clear();
        RefreshStatus();
    }

    private bool CanClear()
        => Entries.Count > 0;

    private void OnEntryMirrored(object? sender, DebugOutputMirrorEntry entry)
    {
        _dispatcher.TryEnqueue(() => AppendEntry(entry));
    }

    private void AppendEntry(DebugOutputMirrorEntry entry)
    {
        Entries.Add(entry);
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        StatusText = Entries.Count == 0
            ? "Log_StatusEmpty".GetLocalizedOrFallback("No mirrored log entries yet.")
            : "Log_StatusFormat".GetLocalizedFormatOrFallback("{0} mirrored log entries.", Entries.Count);
        EmptyStateVisibility = Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _debugOutputMirror.EntryMirrored -= OnEntryMirrored;
        _disposed = true;
    }
}
