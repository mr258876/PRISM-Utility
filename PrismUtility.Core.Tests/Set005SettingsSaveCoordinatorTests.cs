using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using PRISM_Utility.Services;
using PRISM_Utility.Core.Configuration;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Set005")]
[Trait("Category", "Settings")]
public sealed class Set005SettingsSaveCoordinatorTests
{
    [Fact]
    public async Task Set005_SaveCoordinatorSerializesRepeatedChangesAndMarksStaleFailure()
    {
        var coordinator = new SettingsSaveCoordinator();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = false;

        var first = coordinator.EnqueueAsync("language", async ct =>
        {
            firstStarted.TrySetResult();
            await releaseFirst.Task.WaitAsync(ct);
            throw new IOException("SET005 first save failure");
        });
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var second = coordinator.EnqueueAsync("language", _ =>
        {
            secondStarted = true;
            return Task.CompletedTask;
        });

        Assert.False(secondStarted);
        releaseFirst.TrySetResult();

        var firstResult = await first.WaitAsync(TimeSpan.FromSeconds(1));
        var secondResult = await second.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(SettingsSaveOperationStatus.Failed, firstResult.Status);
        Assert.Equal(SettingsSaveOperationStatus.Succeeded, secondResult.Status);
        Assert.False(coordinator.IsLatest(firstResult));
        Assert.True(coordinator.IsLatest(secondResult));
        await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Set005_SaveCoordinatorConvertsCancellationIntoOwnedResult()
    {
        var coordinator = new SettingsSaveCoordinator();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await coordinator.EnqueueAsync("theme", _ => throw new InvalidOperationException("Should not run."), cancellation.Token);

        Assert.Equal(SettingsSaveOperationStatus.Canceled, result.Status);
        Assert.True(coordinator.IsLatest(result));
        await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Set005_SaveCoordinatorCanceledQueuedOperationReturnsPromptlyWithoutBypassingPredecessor()
    {
        var coordinator = new SettingsSaveCoordinator();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = false;
        var thirdStarted = false;
        var first = coordinator.EnqueueAsync("transfer", async _ =>
        {
            firstEntered.SetResult();
            await releaseFirst.Task;
        });
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        using var secondCancellation = new CancellationTokenSource();

        var second = coordinator.EnqueueAsync("transfer", _ =>
        {
            secondStarted = true;
            return Task.CompletedTask;
        }, secondCancellation.Token);
        secondCancellation.Cancel();

        var secondResult = await second.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(SettingsSaveOperationStatus.Canceled, secondResult.Status);
        Assert.False(secondStarted);

        var third = coordinator.EnqueueAsync("transfer", _ =>
        {
            thirdStarted = true;
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<TimeoutException>(() => coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromMilliseconds(50)));
        Assert.False(thirdStarted);

        releaseFirst.SetResult();
        Assert.Equal(SettingsSaveOperationStatus.Succeeded, (await first.WaitAsync(TimeSpan.FromSeconds(1))).Status);
        Assert.Equal(SettingsSaveOperationStatus.Succeeded, (await third.WaitAsync(TimeSpan.FromSeconds(1))).Status);
        Assert.True(thirdStarted);
    }

    [Fact]
    public async Task Set005_SaveCoordinatorReleaseOwnerCancelsQueuedWorkAndKeepsResultObservedUntilCompletion()
    {
        var coordinator = new SettingsSaveCoordinator();
        using var owner = coordinator.CreateOwner();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownerSaveStarted = false;
        var first = coordinator.EnqueueAsync("transfer", async _ =>
        {
            firstEntered.SetResult();
            await releaseFirst.Task;
        });
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var queued = coordinator.EnqueueAsync(owner.OwnerId, "transfer", _ =>
        {
            ownerSaveStarted = true;
            return Task.CompletedTask;
        });
        Assert.Equal(1, coordinator.GetTrackedOwnerResultCount(owner.OwnerId));

        owner.Dispose();

        var queuedResult = await queued.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(SettingsSaveOperationStatus.Canceled, queuedResult.Status);
        Assert.False(ownerSaveStarted);
        await WaitForConditionAsync(() => coordinator.GetTrackedOwnerResultCount(owner.OwnerId) == 0);

        releaseFirst.SetResult();
        Assert.Equal(SettingsSaveOperationStatus.Succeeded, (await first.WaitAsync(TimeSpan.FromSeconds(1))).Status);
    }

    [Fact]
    public async Task Set005_SaveCoordinatorReleaseOwnerCancelsActiveWorkBeforeDisposal()
    {
        var coordinator = new SettingsSaveCoordinator();
        using var owner = coordinator.CreateOwner();
        var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedCancellation = false;

        var active = coordinator.EnqueueAsync(owner.OwnerId, "language", async cancellationToken =>
        {
            saveEntered.SetResult();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                observedCancellation = true;
                throw;
            }
        });
        await saveEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));

        owner.Dispose();

        var result = await active.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(SettingsSaveOperationStatus.Canceled, result.Status);
        Assert.True(observedCancellation);
        await WaitForConditionAsync(() => coordinator.GetTrackedOwnerResultCount(owner.OwnerId) == 0);
    }

    [Fact]
    public async Task Set005_SaveCoordinatorReleasedOwnerRejectsFutureWorkWithoutRunningIt()
    {
        var coordinator = new SettingsSaveCoordinator();
        var owner = coordinator.CreateOwner();
        var ownerId = owner.OwnerId;
        owner.Dispose();
        var ran = false;

        var result = await coordinator.EnqueueAsync(ownerId, "debug", _ =>
        {
            ran = true;
            return Task.CompletedTask;
        }).WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(SettingsSaveOperationStatus.Canceled, result.Status);
        Assert.False(ran);
        Assert.Equal(0, coordinator.GetTrackedOwnerResultCount(ownerId));
    }

    [Fact]
    public void Set005_SaveCoordinatorCleanupIsIdempotentAfterDispose()
    {
        var coordinator = new SettingsSaveCoordinator();
        var owner = coordinator.CreateOwner();

        coordinator.Dispose();
        coordinator.CancelPendingOperations();
        coordinator.CancelPendingOperations(owner.OwnerId);
        owner.Dispose();
        coordinator.Dispose();
    }

    [Fact]
    public async Task Set005_SettingsServicesPublishCommittedValuesWhenTokenCancelsAfterDurableSave()
    {
        await AssertPostCommitThemeCancellationAsync();
        await AssertPostCommitLanguageCancellationAsync();
        await AssertPostCommitDebugCancellationAsync();
        await AssertPostCommitTransferCancellationAsync();
        await AssertPostCommitColorCancellationAsync();
    }

    [Fact]
    public async Task Set005_LanguageRollbackUsesBoundedRefreshAndRestoresDurableCurrentAndRuntimeState()
    {
        AppContext.SetSwitch("PRISM.Utility.SkipWinGlobalizationApply", true);
        var settings = new PostCommitCancelLocalSettings();
        settings.Seed("AppRequestedLanguage", "en-US");
        var refreshCount = 0;
        CancellationToken rollbackToken = default;
        var language = new LanguageSelectorService(
            settings,
            new RecordingThemeSelectorService(),
            new RecordingUiDispatcher(true),
            refreshShellWithCancellationAsync: cancellationToken =>
            {
                refreshCount++;
                if (refreshCount == 1)
                    throw new InvalidOperationException("refresh failed");

                rollbackToken = cancellationToken;
                return Task.CompletedTask;
            });
        await language.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => language.SetLanguageAsync("zh-CN"));

        Assert.Equal("en-US", language.CurrentLanguage);
        Assert.Equal("en-US", settings.Get<string>("AppRequestedLanguage"));
        Assert.True(rollbackToken.CanBeCanceled);
        Assert.Equal(2, refreshCount);
    }

    [Fact]
    public async Task Set005_LanguageRollbackHangingRefreshExitsBoundedAndRestoresDurableCurrentState()
    {
        AppContext.SetSwitch("PRISM.Utility.SkipWinGlobalizationApply", true);
        var settings = new PostCommitCancelLocalSettings();
        settings.Seed("AppRequestedLanguage", "en-US");
        var refreshCount = 0;
        var stopwatch = Stopwatch.StartNew();
        var language = new LanguageSelectorService(
            settings,
            new RecordingThemeSelectorService(),
            new RecordingUiDispatcher(true),
            refreshShellWithCancellationAsync: cancellationToken =>
            {
                refreshCount++;
                return refreshCount == 1
                    ? Task.FromException(new InvalidOperationException("refresh failed"))
                    : Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            });
        await language.InitializeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => language.SetLanguageAsync("zh-CN"));
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Equal("en-US", language.CurrentLanguage);
        Assert.Equal("en-US", settings.Get<string>("AppRequestedLanguage"));
        Assert.Equal(2, refreshCount);
    }

    [Fact]
    public async Task Set005_FaultedSaveIsObservedWithoutTaskSchedulerUnobservedTaskException()
    {
        var coordinator = new SettingsSaveCoordinator();
        var unobservedExceptions = new ConcurrentQueue<AggregateException>();
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, eventArgs) =>
        {
            unobservedExceptions.Enqueue(eventArgs.Exception);
            eventArgs.SetObserved();
        };
        TaskScheduler.UnobservedTaskException += handler;

        try
        {
            var resultTask = coordinator.EnqueueAsync("debug", _ => Task.FromException(new IOException("SET005 observed failure")));
            await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(1));
            var resultReference = new WeakReference(resultTask);
            resultTask = null!;

            await WaitForCollectionAsync(resultReference);

            Assert.Empty(unobservedExceptions);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }

    [Fact]
    public async Task Set005_ActualSettingsServiceHarness_FailureThenRecoveryPublishesOnlyPersistedValue()
    {
        const string documentKey = "ScanTransferSettingsDocument";
        var settings = new Set003RecordingLocalSettings();
        settings.Seed(documentKey, new VersionedSettingsDocument<ScanBulkInTransferOptions>(1, ScanTransferDefaults.Settings));
        var transferSettings = new ScanTransferSettingsService(settings);
        await transferSettings.InitializeAsync();
        var coordinator = new SettingsSaveCoordinator();
        var unobservedExceptions = new ConcurrentQueue<AggregateException>();
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, eventArgs) =>
        {
            unobservedExceptions.Enqueue(eventArgs.Exception);
            eventArgs.SetObserved();
        };
        TaskScheduler.UnobservedTaskException += handler;

        try
        {
            var failedValue = ScanTransferDefaults.Settings with { RequestBytes = 64 * 1024 };
            settings.FailSave(documentKey, new IOException("SET005 runtime save failure"));

            var failure = await coordinator.EnqueueAsync("transfer", _ => transferSettings.SetSettingsAsync(failedValue));

            Assert.Equal(SettingsSaveOperationStatus.Failed, failure.Status);
            Assert.Equal(ScanTransferDefaults.Settings, transferSettings.Settings);

            var recoveredValue = ScanTransferDefaults.Settings with { RequestBytes = 128 * 1024 };
            settings.ClearSaveFailure(documentKey);

            var recovery = await coordinator.EnqueueAsync("transfer", _ => transferSettings.SetSettingsAsync(recoveredValue));

            Assert.Equal(SettingsSaveOperationStatus.Succeeded, recovery.Status);
            Assert.True(coordinator.IsLatest(recovery));
            Assert.Equal(recoveredValue, transferSettings.Settings);
            Assert.Equal(new VersionedSettingsDocument<ScanBulkInTransferOptions>(1, recoveredValue), settings.Get<VersionedSettingsDocument<ScanBulkInTransferOptions>>(documentKey));
            await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Empty(unobservedExceptions);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }

    [Fact]
    public void Set005_SettingsViewModelSourceOwnsSettingsPersistenceTasks()
    {
        var source = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", "SettingsViewModel.cs"));

        Assert.DoesNotContain("_ = _languageSelectorService.SetLanguageAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = _debugOutputSettingsService.SetDebugConsoleEnabledAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = _debugOutputSettingsService.SetFileLogEnabledAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = SaveTransferSettingsAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = SaveColorManagementSettingsAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_settingsSaveCoordinator.CancelPendingOperations();", source, StringComparison.Ordinal);
        Assert.Contains("SettingsSaveCoordinator", source, StringComparison.Ordinal);
        Assert.Contains("_settingsSaveCoordinator.CreateOwner()", source, StringComparison.Ordinal);
        Assert.Contains("_settingsSaveCoordinator.EnqueueAsync(_settingsSaveOwner.OwnerId", source, StringComparison.Ordinal);
        Assert.Contains("_settingsSaveCoordinator.WhenIdleAsync(_settingsSaveOwner.OwnerId)", source, StringComparison.Ordinal);
        Assert.Contains("_settingsSaveCoordinator.CancelPendingOperations(_settingsSaveOwner.OwnerId)", source, StringComparison.Ordinal);
        Assert.Contains("FlushSettingsPersistenceAsync", source, StringComparison.Ordinal);
        Assert.Contains("TeardownSettingsPersistenceAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Set005_SettingsPageUsesExistingInfoBarPatternForPersistenceFailures()
    {
        var source = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "Views", "SettingsPage.xaml"));

        Assert.Contains("Settings_PersistenceErrorInfoBar", source, StringComparison.Ordinal);
        Assert.Contains("Severity=\"Error\"", source, StringComparison.Ordinal);
        Assert.Contains("ViewModel.SettingsPersistenceErrorMessage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("#", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Set005_SettingsLifecycleOwnersFlushAndCancelAcceptedCoordinatorTasks()
    {
        var root = FindHostSoftwareRoot();
        var appSource = File.ReadAllText(Path.Combine(root, "PRISM Utility", "App.xaml.cs"));
        var pageSource = File.ReadAllText(Path.Combine(root, "PRISM Utility", "Views", "SettingsPage.xaml.cs"));

        Assert.Contains("services.AddSingleton<SettingsSaveCoordinator>()", appSource, StringComparison.Ordinal);
        Assert.Contains("_settingsSaveCoordinator.CancelPendingOperations()", appSource, StringComparison.Ordinal);
        Assert.Contains("_settingsSaveCoordinator.WhenIdleAsync()", appSource, StringComparison.Ordinal);
        Assert.Contains("_settingsSaveCoordinator.Dispose()", appSource, StringComparison.Ordinal);
        Assert.Contains("Unloaded += OnUnloaded", pageSource, StringComparison.Ordinal);
        Assert.Contains("ViewModel.TeardownSettingsPersistenceAsync", pageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.FlushSettingsPersistenceAsync", pageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.CancelSettingsPersistence()", pageSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_settingsSaveCoordinator.CancelPendingOperations();", pageSource, StringComparison.Ordinal);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(25);
        }

        Assert.True(condition());
    }

    private static async Task AssertPostCommitThemeCancellationAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var settings = new PostCommitCancelLocalSettings(cancellation);
        var service = new ThemeSelectorService(settings, new RecordingUiDispatcher(true));

        await service.SetThemeAsync(ElementTheme.Dark, cancellation.Token);

        Assert.Equal(ElementTheme.Dark, service.Theme);
        Assert.Equal(ElementTheme.Dark.ToString(), settings.Get<string>("AppBackgroundRequestedTheme"));
    }

    private static async Task AssertPostCommitLanguageCancellationAsync()
    {
        AppContext.SetSwitch("PRISM.Utility.SkipWinGlobalizationApply", true);
        using var cancellation = new CancellationTokenSource();
        var settings = new PostCommitCancelLocalSettings(cancellation);
        settings.Seed("AppRequestedLanguage", "en-US");
        var service = new LanguageSelectorService(
            settings,
            new RecordingThemeSelectorService(),
            new RecordingUiDispatcher(true),
            refreshShellWithCancellationAsync: _ => Task.CompletedTask);
        await service.InitializeAsync();

        await service.SetLanguageAsync("zh-CN", cancellation.Token);

        Assert.Equal("zh-CN", service.CurrentLanguage);
        Assert.Equal("zh-CN", settings.Get<string>("AppRequestedLanguage"));
    }

    private static async Task AssertPostCommitDebugCancellationAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var settings = new PostCommitCancelLocalSettings(cancellation);
        var service = new DebugOutputSettingsService(settings);

        await service.SetDebugConsoleEnabledAsync(true, cancellation.Token);

        Assert.True(service.IsDebugConsoleEnabled);
        Assert.True(settings.Get<bool>("DebugConsoleMirrorEnabled"));
    }

    private static async Task AssertPostCommitTransferCancellationAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var settings = new PostCommitCancelLocalSettings(cancellation);
        settings.Seed("ScanTransferSettingsDocument", new VersionedSettingsDocument<ScanBulkInTransferOptions>(1, ScanTransferDefaults.Settings));
        var service = new ScanTransferSettingsService(settings);
        var committed = ScanTransferDefaults.Settings with { RequestBytes = 65536 };

        await service.SetSettingsAsync(committed, cancellation.Token);

        Assert.Equal(committed, service.Settings);
        Assert.Equal(new VersionedSettingsDocument<ScanBulkInTransferOptions>(1, committed), settings.Get<VersionedSettingsDocument<ScanBulkInTransferOptions>>("ScanTransferSettingsDocument"));
    }

    private static async Task AssertPostCommitColorCancellationAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var settings = new PostCommitCancelLocalSettings(cancellation);
        var defaults = ScanColorManagementOptions.CreateDefault();
        settings.Seed("ScanColorManagementSettingsDocument", new VersionedSettingsDocument<ScanColorManagementOptions>(1, defaults));
        var service = new ScanColorManagementSettingsService(settings);
        var committed = defaults with { OutputGamma = 1.8 };

        await service.SetSettingsAsync(committed, cancellation.Token);

        Assert.Equal(committed, service.Settings);
        Assert.Equal(new VersionedSettingsDocument<ScanColorManagementOptions>(1, committed), settings.Get<VersionedSettingsDocument<ScanColorManagementOptions>>("ScanColorManagementSettingsDocument"));
    }

    private static async Task WaitForCollectionAsync(WeakReference reference)
    {
        for (var attempt = 0; reference.IsAlive && attempt < 20; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Yield();
        }

        Assert.False(reference.IsAlive, "The owned save result task should be collectible after coordinator observation.");
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
}

internal sealed class PostCommitCancelLocalSettings : ILocalSettingsService
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource? _postCommitCancellation;

    public PostCommitCancelLocalSettings(CancellationTokenSource? postCommitCancellation = null)
    {
        _postCommitCancellation = postCommitCancellation;
    }

    public void Seed<T>(string key, T value)
        => _values[key] = Newtonsoft.Json.JsonConvert.SerializeObject(value);

    public T? Get<T>(string key)
        => _values.TryGetValue(key, out var value) ? Newtonsoft.Json.JsonConvert.DeserializeObject<T>(value) : default;

    public Task<T?> ReadSettingAsync<T>(string key)
        => Task.FromResult(Get<T>(key));

    public Task SaveSettingAsync<T>(string key, T value)
    {
        Seed(key, value);
        _postCommitCancellation?.Cancel();
        return Task.CompletedTask;
    }
}
