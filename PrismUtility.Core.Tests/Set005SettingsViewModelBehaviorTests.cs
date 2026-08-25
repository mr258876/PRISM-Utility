using Microsoft.UI.Xaml;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Configuration;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Set005")]
[Trait("Category", "Settings")]
public sealed class Set005SettingsViewModelBehaviorTests
{
    [Fact]
    public async Task Set005_SettingsViewModel_FlushCompletesWhenInitialLoadsCompleteSynchronously()
    {
        var originalContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new InlinePostSynchronizationContext());
            var services = SettingsViewModelHarness.Create();

            var viewModel = await services.CreateInitializedViewModelAsync();

            await viewModel.FlushSettingsPersistenceAsync().WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
    }

    [Fact]
    public async Task Set005_SettingsViewModel_OldOwnerCancelDoesNotCancelReplacementLanguageSave()
    {
        var coordinator = new SettingsSaveCoordinator();
        var oldServices = SettingsViewModelHarness.Create(coordinator: coordinator);
        var newServices = SettingsViewModelHarness.Create(coordinator: coordinator);
        var oldViewModel = await oldServices.CreateInitializedViewModelAsync();
        var newViewModel = await newServices.CreateInitializedViewModelAsync();
        var newLanguageSaveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseNewLanguageSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        newServices.Language.BeforeSetLanguageAsync = async cancellationToken =>
        {
            newLanguageSaveEntered.SetResult();
            await releaseNewLanguageSave.Task.WaitAsync(cancellationToken);
        };

        newViewModel.SelectedLanguage = "zh-CN";
        await newLanguageSaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));

        oldViewModel.CancelSettingsPersistence();
        await oldViewModel.FlushSettingsPersistenceAsync().WaitAsync(TimeSpan.FromSeconds(1));
        releaseNewLanguageSave.SetResult();
        await newViewModel.FlushSettingsPersistenceAsync().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("zh-CN", newServices.Language.CurrentLanguage);
        Assert.False(newViewModel.HasSettingsPersistenceError);
    }

    [Fact]
    public async Task Set005_SettingsViewModel_RollsBackTransferRestoreDefaultsWhenSaveFails()
    {
        var services = SettingsViewModelHarness.Create();
        services.Transfer.Settings = ScanTransferDefaults.Settings with { RequestBytes = 65536 };
        var viewModel = await services.CreateInitializedViewModelAsync();
        services.Transfer.FailSetSettings = new IOException("restore transfer failed");

        await viewModel.RestoreScanTransferDefaultsCommand.ExecuteAsync(null);

        Assert.Equal("65536", viewModel.MultiBufferedRequestBytes);
        Assert.True(viewModel.HasSettingsPersistenceError);
        Assert.Contains("restore transfer failed", viewModel.SettingsPersistenceErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Set005_SettingsViewModel_RollsBackEachSettingsFamilyWhenSaveFails()
    {
        var services = SettingsViewModelHarness.Create();
        var viewModel = await services.CreateInitializedViewModelAsync();

        services.Language.FailSetLanguage = new IOException("language failed");
        viewModel.SelectedLanguage = "zh-CN";
        await viewModel.FlushSettingsPersistenceAsync();
        Assert.Equal("en-US", viewModel.SelectedLanguage);

        services.Theme.FailSetTheme = new IOException("theme failed");
        viewModel.SwitchThemeCommand.Execute(ElementTheme.Dark);
        await viewModel.FlushSettingsPersistenceAsync();
        Assert.Equal(ElementTheme.Default, viewModel.ElementTheme);

        services.Debug.FailSetConsole = new IOException("debug failed");
        viewModel.IsDebugConsoleMirrorEnabled = true;
        await viewModel.FlushSettingsPersistenceAsync();
        Assert.False(viewModel.IsDebugConsoleMirrorEnabled);

        services.Transfer.FailSetSettings = new IOException("transfer failed");
        viewModel.MultiBufferedRequestBytes = "131072";
        await viewModel.FlushSettingsPersistenceAsync();
        Assert.Equal(ScanTransferDefaults.Settings.RequestBytes.ToString(), viewModel.MultiBufferedRequestBytes);

        services.Color.FailSetSettings = new IOException("color failed");
        viewModel.OutputGamma = "1.8";
        await viewModel.FlushSettingsPersistenceAsync();
        Assert.Equal("2.2", viewModel.OutputGamma);
    }

    [Fact]
    public async Task Set005_SettingsViewModel_SuccessClearsOnlyRecoveredScope()
    {
        var services = SettingsViewModelHarness.Create();
        var viewModel = await services.CreateInitializedViewModelAsync();

        services.Language.FailSetLanguage = new IOException("language still failed");
        services.Debug.FailSetConsole = new IOException("debug first failed");
        viewModel.SelectedLanguage = "zh-CN";
        viewModel.IsDebugConsoleMirrorEnabled = true;
        await viewModel.FlushSettingsPersistenceAsync();
        Assert.Contains("language still failed", viewModel.SettingsPersistenceErrorMessage, StringComparison.Ordinal);
        Assert.Contains("debug first failed", viewModel.SettingsPersistenceErrorMessage, StringComparison.Ordinal);

        services.Debug.FailSetConsole = null;
        viewModel.IsDebugConsoleMirrorEnabled = true;
        await viewModel.FlushSettingsPersistenceAsync();

        Assert.Contains("language still failed", viewModel.SettingsPersistenceErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("debug first failed", viewModel.SettingsPersistenceErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Set005_SettingsViewModel_StaleFailureDoesNotRollbackNewerVisibleTransferState()
    {
        var services = SettingsViewModelHarness.Create();
        var viewModel = await services.CreateInitializedViewModelAsync();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        services.Transfer.BeforeSetSettingsAsync = async cancellationToken =>
        {
            firstEntered.TrySetResult();
            await releaseFirst.Task.WaitAsync(cancellationToken);
            services.Transfer.BeforeSetSettingsAsync = null;
            throw new IOException("stale transfer failed");
        };

        viewModel.MultiBufferedRequestBytes = "65536";
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        viewModel.MultiBufferedRequestBytes = "131072";
        releaseFirst.SetResult();
        await viewModel.FlushSettingsPersistenceAsync();

        Assert.Equal("131072", viewModel.MultiBufferedRequestBytes);
        Assert.False(viewModel.HasSettingsPersistenceError);
        Assert.Equal(131072, services.Transfer.Settings.RequestBytes);
    }

    [Fact]
    public async Task Set005_SettingsViewModel_DispatchesRollbackToUiDispatcher()
    {
        var services = SettingsViewModelHarness.Create();
        var viewModel = await services.CreateInitializedViewModelAsync();
        services.Dispatcher.AutoDrain = false;
        services.Transfer.FailSetSettings = new IOException("dispatch transfer failed");

        viewModel.MultiBufferedRequestBytes = "65536";
        var flush = viewModel.FlushSettingsPersistenceAsync();
        await services.Dispatcher.WaitForPendingAsync();
        Assert.Equal("65536", viewModel.MultiBufferedRequestBytes);

        services.Dispatcher.DrainAll();
        await flush.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(ScanTransferDefaults.Settings.RequestBytes.ToString(), viewModel.MultiBufferedRequestBytes);
        Assert.True(services.Dispatcher.EnqueueCount > 0);
    }

    [Fact]
    public async Task Set005_SettingsViewModel_CancelSettingsPersistenceCancelsMidFlightSave()
    {
        var services = SettingsViewModelHarness.Create();
        var viewModel = await services.CreateInitializedViewModelAsync();
        var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        services.Transfer.BeforeSetSettingsAsync = async cancellationToken =>
        {
            saveEntered.SetResult();
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        };

        viewModel.MultiBufferedRequestBytes = "65536";
        await saveEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        viewModel.CancelSettingsPersistence();
        await viewModel.FlushSettingsPersistenceAsync();

        Assert.Equal("65536", viewModel.MultiBufferedRequestBytes);
        Assert.Equal(ScanTransferDefaults.Settings, services.Transfer.Settings);
        Assert.False(viewModel.HasSettingsPersistenceError);
    }

    [Fact]
    public async Task Set005_SettingsViewModel_ReentrantDiagnosticsRemainObserved()
    {
        var services = SettingsViewModelHarness.Create();
        var viewModel = await services.CreateInitializedViewModelAsync();
        services.Transfer.FailSetSettings = new IOException("transfer diagnostic failed");
        services.Mirror.OnMirror = () => viewModel.IsDebugFileLogEnabled = true;

        viewModel.MultiBufferedRequestBytes = "65536";
        await viewModel.FlushSettingsPersistenceAsync();

        Assert.True(services.Debug.IsFileLogEnabled);
        Assert.Contains(services.Mirror.Messages, message => message.Contains("transfer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Set005_SettingsViewModel_NeverExecutingDispatcherDoesNotHangTeardown()
    {
        var services = SettingsViewModelHarness.Create();
        var viewModel = await services.CreateInitializedViewModelAsync();
        services.Dispatcher.AutoDrain = false;
        services.Transfer.FailSetSettings = new IOException("never drained dispatch failed");

        viewModel.MultiBufferedRequestBytes = "65536";
        await services.Dispatcher.WaitForPendingAsync();

        await viewModel.TeardownSettingsPersistenceAsync().WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Contains(services.Mirror.Messages, message => message.Contains("never drained dispatch failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Set005_SettingsViewModel_QueuedUiCallbackDoesNotMutateAfterTeardown()
    {
        var services = SettingsViewModelHarness.Create();
        var viewModel = await services.CreateInitializedViewModelAsync();
        services.Dispatcher.AutoDrain = false;
        services.Transfer.FailSetSettings = new IOException("disposed rollback failed");

        viewModel.MultiBufferedRequestBytes = "65536";
        await services.Dispatcher.WaitForPendingAsync();
        await viewModel.TeardownSettingsPersistenceAsync().WaitAsync(TimeSpan.FromSeconds(3));
        services.Dispatcher.DrainAll();

        Assert.Equal("65536", viewModel.MultiBufferedRequestBytes);
        Assert.False(viewModel.HasSettingsPersistenceError);
    }

    [Fact]
    public async Task Set005_SettingsViewModel_RepeatedTeardownIsIdempotentAndBlocksLaterSaves()
    {
        var services = SettingsViewModelHarness.Create();
        var viewModel = await services.CreateInitializedViewModelAsync();

        var first = viewModel.TeardownSettingsPersistenceAsync();
        var second = viewModel.TeardownSettingsPersistenceAsync();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(1));

        viewModel.MultiBufferedRequestBytes = "65536";
        await viewModel.FlushSettingsPersistenceAsync().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(0, services.Transfer.SetSettingsCallCount);
        Assert.Equal(ScanTransferDefaults.Settings, services.Transfer.Settings);
    }

    [Fact]
    public async Task Set005_SettingsViewModel_PostCommitResultSurvivesOwnerCancelBeforeUiObserverRuns()
    {
        var services = SettingsViewModelHarness.Create();
        var viewModel = await services.CreateInitializedViewModelAsync();

        services.Debug.FailSetConsole = new IOException("debug first failed");
        viewModel.IsDebugConsoleMirrorEnabled = true;
        await viewModel.FlushSettingsPersistenceAsync();
        Assert.True(viewModel.HasSettingsPersistenceError);

        services.Debug.FailSetConsole = null;
        services.Dispatcher.AutoDrain = false;
        viewModel.IsDebugConsoleMirrorEnabled = true;
        await services.Dispatcher.WaitForPendingAsync();
        viewModel.CancelSettingsPersistence();
        services.Dispatcher.DrainAll();
        await viewModel.FlushSettingsPersistenceAsync().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(services.Debug.IsDebugConsoleEnabled);
        Assert.False(viewModel.HasSettingsPersistenceError);
    }

    [Fact]
    public async Task Set005_SettingsViewModel_FailureDiagnosticsMirrorBeforeInfoBarDispatchRuns()
    {
        var services = SettingsViewModelHarness.Create();
        var viewModel = await services.CreateInitializedViewModelAsync();
        services.Dispatcher.AutoDrain = false;
        services.Transfer.FailSetSettings = new IOException("independent diagnostic failed");

        viewModel.MultiBufferedRequestBytes = "65536";
        await services.Dispatcher.WaitForPendingAsync();

        Assert.Contains(services.Mirror.Messages, message => message.Contains("independent diagnostic failed", StringComparison.Ordinal));
        Assert.False(viewModel.HasSettingsPersistenceError);

        services.Dispatcher.DrainAll();
        await viewModel.FlushSettingsPersistenceAsync().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(viewModel.HasSettingsPersistenceError);
    }
}

internal sealed class SettingsViewModelHarness
{
    private SettingsViewModelHarness(bool autoDrainDispatcher, SettingsSaveCoordinator? coordinator)
    {
        AppContext.SetSwitch("PRISM.Utility.UseResourceFallbacks", true);
        Dispatcher = new RecordingUiDispatcher(autoDrainDispatcher);
        Coordinator = coordinator ?? new SettingsSaveCoordinator();
        Theme = new RecordingThemeSelectorService();
        Language = new RecordingLanguageSelectorService();
        Debug = new RecordingDebugOutputSettingsService();
        Mirror = new RecordingDebugOutputMirrorService();
        Transfer = new RecordingTransferSettingsService();
        Color = new RecordingColorManagementSettingsService();
    }

    public RecordingUiDispatcher Dispatcher { get; }
    public SettingsSaveCoordinator Coordinator { get; }
    public RecordingThemeSelectorService Theme { get; }
    public RecordingLanguageSelectorService Language { get; }
    public RecordingDebugOutputSettingsService Debug { get; }
    public RecordingDebugOutputMirrorService Mirror { get; }
    public RecordingTransferSettingsService Transfer { get; }
    public RecordingColorManagementSettingsService Color { get; }

    public static SettingsViewModelHarness Create(bool autoDrainDispatcher = true, SettingsSaveCoordinator? coordinator = null)
        => new(autoDrainDispatcher, coordinator);

    public async Task<SettingsViewModel> CreateInitializedViewModelAsync(bool drainInitialLoads = false)
    {
        var viewModel = new SettingsViewModel(Theme, Language, Debug, Mirror, Transfer, Color, Coordinator, Dispatcher);
        var flush = viewModel.FlushSettingsPersistenceAsync();
        if (drainInitialLoads)
            Dispatcher.DrainAll();
        await flush.WaitAsync(TimeSpan.FromSeconds(1));
        return viewModel;
    }
}

internal sealed class InlinePostSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state)
        => d(state);

    public override void Send(SendOrPostCallback d, object? state)
        => d(state);
}

internal sealed class RecordingUiDispatcher : IUiDispatcher
{
    private readonly Queue<Action> _pending = new();
    public bool AutoDrain { get; set; }
    private TaskCompletionSource _pendingSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RecordingUiDispatcher(bool autoDrain)
    {
        AutoDrain = autoDrain;
    }

    public int EnqueueCount { get; private set; }

    public bool TryEnqueue(Action action)
    {
        EnqueueCount++;
        if (AutoDrain)
        {
            action();
            return true;
        }

        _pending.Enqueue(action);
        _pendingSignal.TrySetResult();
        return true;
    }

    public Task WaitForPendingAsync()
        => _pending.Count > 0 ? Task.CompletedTask : _pendingSignal.Task.WaitAsync(TimeSpan.FromSeconds(1));

    public void DrainAll()
    {
        while (_pending.Count > 0)
            _pending.Dequeue()();

        _pendingSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

internal sealed class RecordingThemeSelectorService : IThemeSelectorService
{
    public ElementTheme Theme { get; private set; } = ElementTheme.Default;
    public Exception? FailSetTheme { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetRequestedThemeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SetThemeAsync(ElementTheme theme, CancellationToken cancellationToken = default)
    {
        if (FailSetTheme is not null)
            return Task.FromException(FailSetTheme);

        Theme = theme;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingLanguageSelectorService : ILanguageSelectorService
{
    public string CurrentLanguage { get; private set; } = "en-US";
    public Exception? FailSetLanguage { get; set; }
    public Func<CancellationToken, Task>? BeforeSetLanguageAsync { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ApplyLanguageAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task SetLanguageAsync(string languageTag, CancellationToken cancellationToken = default)
    {
        if (BeforeSetLanguageAsync is not null)
            await BeforeSetLanguageAsync(cancellationToken);

        if (FailSetLanguage is not null)
            throw FailSetLanguage;

        CurrentLanguage = languageTag;
    }
}

internal sealed class RecordingDebugOutputSettingsService : IDebugOutputSettingsService
{
    public bool IsDebugConsoleEnabled { get; private set; }
    public bool IsFileLogEnabled { get; private set; }
    public Exception? FailSetConsole { get; set; }
    public int SetConsoleCallCount { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SetDebugConsoleEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        SetConsoleCallCount++;
        if (FailSetConsole is not null)
            return Task.FromException(FailSetConsole);

        IsDebugConsoleEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task SetFileLogEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        IsFileLogEnabled = enabled;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingDebugOutputMirrorService : IDebugOutputMirrorService
{
    public IReadOnlyList<DebugOutputMirrorEntry> RecentEntries => [];
    public List<string> Messages { get; } = [];
    public Action? OnMirror { get; set; }
    public event EventHandler<DebugOutputMirrorEntry>? EntryMirrored;

    public void Mirror(string source, string message)
    {
        Messages.Add($"{source}:{message}");
        OnMirror?.Invoke();
        EntryMirrored?.Invoke(this, new DebugOutputMirrorEntry(DateTimeOffset.UtcNow, source, message, message));
    }

    public void ClearRecentEntries() { }
    public Task FlushAsync() => Task.CompletedTask;
    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class RecordingTransferSettingsService : IScanTransferSettingsService
{
    public event EventHandler? BulkInReadModeChanged;
    public ScanBulkInReadMode BulkInReadMode => Settings.ReadMode;
    public ScanBulkInTransferOptions DefaultSettings => ScanTransferDefaults.Settings;
    public ScanBulkInTransferOptions Settings { get; set; } = ScanTransferDefaults.Settings;
    public Exception? FailSetSettings { get; set; }
    public Func<CancellationToken, Task>? BeforeSetSettingsAsync { get; set; }
    public int SetSettingsCallCount { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetBulkInReadModeAsync(ScanBulkInReadMode mode, CancellationToken cancellationToken = default) => SetSettingsAsync(Settings with { ReadMode = mode }, cancellationToken);

    public async Task SetSettingsAsync(ScanBulkInTransferOptions settings, CancellationToken cancellationToken = default)
    {
        SetSettingsCallCount++;
        if (BeforeSetSettingsAsync is not null)
            await BeforeSetSettingsAsync(cancellationToken);

        if (FailSetSettings is not null)
            throw FailSetSettings;

        Settings = settings;
        BulkInReadModeChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class RecordingColorManagementSettingsService : IScanColorManagementSettingsService
{
    public ScanColorManagementOptions DefaultSettings => ScanColorManagementOptions.CreateDefault();
    public ScanColorManagementOptions Settings { get; private set; } = ScanColorManagementOptions.CreateDefault();
    public Exception? FailSetSettings { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SetSettingsAsync(ScanColorManagementOptions settings, CancellationToken cancellationToken = default)
    {
        if (FailSetSettings is not null)
            return Task.FromException(FailSetSettings);

        Settings = settings;
        return Task.CompletedTask;
    }
}
