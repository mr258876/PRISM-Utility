using System.Runtime.CompilerServices;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "LifecycleRegression")]
public sealed class ScannerLifecycleIntegrationRegressionTests
{
    [Fact]
    public async Task NavigationLifecycleRegression_ManagerOwnershipPersistsAcrossObserverAndCompetingClients()
    {
        var factory = new RegressionScanSessionServiceFactory();
        var coordinator = new UsbUsageCoordinator();
        await using var manager = new ScannerDeviceSessionManager(factory, coordinator);
        var scanOwner = CreateOwner("scan-workflow", ScannerSessionOwnerType.ScanWorkflow, ScannerSessionOperation.Scan, "scan-workflow-lease");

        var connectResult = await manager.ConnectAsync(scanOwner, CancellationToken.None);
        var runningBlock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runningOperation = manager.RunWithSessionStateAsync(
            scanOwner.LeaseId,
            ScannerSessionState.Running,
            async session =>
            {
                await runningBlock.Task;
                return session.IsConnected;
            },
            CancellationToken.None);

        await WaitForConditionAsync(() => manager.Snapshot.State == ScannerSessionState.Running);

        var observer = manager.GrantObserverPermission(
            "usb-debug-observer",
            ScannerSessionObserverScope.SessionState | ScannerSessionObserverScope.DeviceCatalog,
            DateTimeOffset.UtcNow);
        var rawUsbAcquire = await coordinator.TryAcquireLeaseAsync("usb-debug", UsbUsageOwnerType.RawUsb, "Bulk OUT");
        var scanDebugCoordinator = new ScanDebugSessionCoordinator(coordinator, manager);
        var scanDebugConnect = await scanDebugCoordinator.ConnectAsync(CancellationToken.None);

        Assert.True(connectResult.Success);
        Assert.True(observer.Allows(ScannerSessionObserverScope.SessionState));
        Assert.False(rawUsbAcquire.Success);
        Assert.False(scanDebugConnect.Success);
        Assert.Contains("read-only", scanDebugConnect.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ScannerSessionState.Running, manager.Snapshot.State);
        Assert.Equal(scanOwner.LeaseId, manager.Snapshot.ActiveOwner?.LeaseId);
        Assert.Single(factory.CreatedSessions);
        Assert.Equal(0, factory.CreatedSessions[0].DisconnectCallCount);
        Assert.Equal(0, factory.CreatedSessions[0].DisposeAsyncCallCount);

        runningBlock.SetResult();
        Assert.True(await runningOperation);
        Assert.Equal(ScannerSessionState.Connected, manager.Snapshot.State);
        Assert.Equal(scanOwner.LeaseId, manager.Snapshot.ActiveOwner?.LeaseId);
    }

    [Fact]
    public void LifecycleRegression_PageUnloadAndCleanupPaths_DoNotReleaseSharedScannerLifecycle()
    {
        var scanPage = ReadHostFile("PRISM Utility", "Views", "ScanPage.xaml.cs");
        var scanPageXaml = ReadHostFile("PRISM Utility", "Views", "ScanPage.xaml");
        var scanDebugPage = ReadHostFile("PRISM Utility", "Views", "ScanDebugPage.xaml.cs");
        var scanDebugPageXaml = ReadHostFile("PRISM Utility", "Views", "ScanDebugPage.xaml");
        var usbDebugPage = ReadHostFile("PRISM Utility", "Views", "UsbDebugPage.xaml.cs");
        var scanViewModel = ReadHostFile("PRISM Utility", "ViewModels", "ScanViewModel.cs");
        var scanDebugViewModel = ReadHostFile("PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        var usbDebugViewModel = ReadHostFile("PRISM Utility", "ViewModels", "UsbDebugViewModel.cs");

        Assert.Contains("NavigationCacheMode=\"Enabled\"", scanPageXaml, StringComparison.Ordinal);
        Assert.Contains("NavigationCacheMode=\"Enabled\"", scanDebugPageXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.CleanupAsync()", scanPage, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.CleanupAsync()", scanDebugPage, StringComparison.Ordinal);
        Assert.DoesNotContain("DisconnectAsync", scanPage, StringComparison.Ordinal);
        Assert.DoesNotContain("ShutdownAsync", scanPage, StringComparison.Ordinal);
        Assert.DoesNotContain("DisconnectAsync", scanDebugPage, StringComparison.Ordinal);
        Assert.DoesNotContain("ShutdownAsync", scanDebugPage, StringComparison.Ordinal);
        Assert.DoesNotContain("_scannerManager", usbDebugPage, StringComparison.Ordinal);

        var scanCleanup = ExtractMemberBody(scanViewModel, "CleanupAsync");
        Assert.DoesNotContain("DisconnectAsync", scanCleanup, StringComparison.Ordinal);
        Assert.DoesNotContain("ShutdownAsync", scanCleanup, StringComparison.Ordinal);
        Assert.DoesNotContain("DisposeAsync", scanCleanup, StringComparison.Ordinal);

        var scanDebugCleanup = ExtractMemberBody(scanDebugViewModel, "CleanupAsync");
        Assert.DoesNotContain("DisconnectAsync", scanDebugCleanup, StringComparison.Ordinal);
        Assert.DoesNotContain("SetWarmUp", scanDebugCleanup, StringComparison.Ordinal);
        Assert.DoesNotContain("DisposeAsync", scanDebugCleanup, StringComparison.Ordinal);

        var usbDispose = ExtractMemberBody(usbDebugViewModel, "Dispose");
        Assert.DoesNotContain("_scannerManager.DisconnectAsync", usbDispose, StringComparison.Ordinal);
        Assert.DoesNotContain("_scannerManager.ShutdownAsync", usbDispose, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenBulkDuplexSession", usbDispose, StringComparison.Ordinal);
    }

    [Fact]
    public void LifecycleRegression_ScanViewModel_BindsOwnershipCommandsToSharedWorkflowCoordinator()
    {
        var scanViewModel = ReadHostFile("PRISM Utility", "ViewModels", "ScanViewModel.cs");

        Assert.Contains("_scanSessionCoordinator.OwnsSnapshot(snapshot)", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("_scanSessionCoordinator.DisconnectAsync(CancellationToken.None)", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("_scanSessionCoordinator.StopAsync(CancellationToken.None)", scanViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("_sessionOwner", scanViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void LifecycleRegression_ScanViewModel_UsesFallbackLocalizationForMissingRuntimeMessages()
    {
        var scanViewModel = ReadHostFile("PRISM Utility", "ViewModels", "ScanViewModel.cs");

        Assert.Contains("\"Scan_Runtime_ReadinessNoDevices\".GetLocalizedOrFallback(", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("\"Scan_Runtime_ReadinessConnectFirst\".GetLocalizedOrFallback(", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("\"Scan_Runtime_ErrorConfigProfileRequired\".GetLocalizedOrFallback(", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("\"Scan_Runtime_OutputActionNoResult\".GetLocalizedOrFallback(", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("\"Scan_Runtime_OutputActionReady\".GetLocalizedOrFallback(", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("\"Scan_Runtime_OutputActionScanRunning\".GetLocalizedOrFallback(", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("\"Scan_Runtime_PreviewModeRawRole\".GetLocalizedFormatOrFallback(", scanViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void LifecycleRegression_ResourceExtensions_UsesFallbackMarkerWhenResourceLoadThrows()
    {
        var resourceExtensions = ReadHostFile("PRISM Utility", "Helpers", "ResourceExtensions.cs");

        Assert.Contains("private static string GetMissingResourceFallback(string resourceKey)", resourceExtensions, StringComparison.Ordinal);
        Assert.Contains("return GetMissingResourceFallback(resourceKey);", resourceExtensions, StringComparison.Ordinal);
        Assert.DoesNotContain("throw new InvalidOperationException(message, ex);", resourceExtensions, StringComparison.Ordinal);
        Assert.Contains("=> string.Format(CultureInfo.CurrentCulture, resourceKey.GetLocalized(), args);", resourceExtensions, StringComparison.Ordinal);
    }

    private static ScannerSessionOwner CreateOwner(string ownerId, ScannerSessionOwnerType ownerType, ScannerSessionOperation operation, string leaseId)
        => new(ownerId, ownerType, operation, new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero), leaseId);

    private static async Task WaitForConditionAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!predicate())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeout.Token);
        }
    }

    private static string ReadHostFile(params string[] pathSegments)
        => File.ReadAllText(Path.Combine([GetHostSoftwareRoot(), .. pathSegments]));

    private static string GetHostSoftwareRoot([CallerFilePath] string sourceFilePath = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, ".."));

    private static string ExtractMemberBody(string source, string memberName)
    {
        var declarationIndex = source.IndexOf(memberName + "(", StringComparison.Ordinal);
        Assert.True(declarationIndex >= 0, $"Could not find member '{memberName}'.");

        var openBraceIndex = source.IndexOf('{', declarationIndex);
        Assert.True(openBraceIndex >= 0, $"Could not find body for member '{memberName}'.");

        var depth = 0;
        for (var index = openBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}')
                depth--;

            if (depth == 0)
                return source[openBraceIndex..(index + 1)];
        }

        throw new InvalidOperationException($"Could not extract body for member '{memberName}'.");
    }

    private sealed class RegressionScanSessionServiceFactory : IScanSessionServiceFactory
    {
        public List<RegressionScanSessionService> CreatedSessions { get; } = [];

        public IScanSessionService CreateSession()
        {
            var session = new RegressionScanSessionService();
            CreatedSessions.Add(session);
            return session;
        }
    }

    private sealed class RegressionScanSessionService : IScanSessionService
    {
        private readonly CancellationTokenSource _connectionCts = new();

        public event EventHandler? TargetsChanged;
        public event Action<ScanMotorState>? MotionEventReceived;

        public ScanTargetState Targets { get; private set; } = new(true, "bulk-in-1", "bulk-out-1");

        public bool IsConnected { get; private set; }

        public int SingleTransferMaxRows => 1;

        public CancellationToken ConnectionToken => _connectionCts.Token;

        public int DisconnectCallCount { get; private set; }

        public int DisposeAsyncCallCount { get; private set; }

        public void RefreshTargets()
            => TargetsChanged?.Invoke(this, EventArgs.Empty);

        public Task<ScanOperationResult> ConnectAsync(CancellationToken ct)
        {
            IsConnected = true;
            return Task.FromResult(new ScanOperationResult(true, "Connected."));
        }

        public Task DisconnectAsync()
        {
            DisconnectCallCount++;
            IsConnected = false;
            _connectionCts.Cancel();
            return Task.CompletedTask;
        }

        public Task<ScanIlluminationState> GetIlluminationStateAsync(CancellationToken ct)
            => Task.FromResult(new ScanIlluminationState(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        public Task SetIlluminationLevelsAsync(ushort led1Level, ushort led2Level, ushort led3Level, ushort led4Level, CancellationToken ct)
            => Task.CompletedTask;

        public Task SetSteadyIlluminationAsync(byte steadyMask, CancellationToken ct)
            => Task.CompletedTask;

        public Task ConfigureExposureLightingAsync(byte syncMask, CancellationToken ct)
            => Task.CompletedTask;

        public Task SetSyncPulseClocksAsync(uint led1PulseClock, uint led2PulseClock, uint led3PulseClock, uint led4PulseClock, CancellationToken ct)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ScanMotorState>> GetMotionStateAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ScanMotorState>>([]);

        public Task SetMotorEnabledAsync(byte motorId, bool enabled, CancellationToken ct)
            => Task.CompletedTask;

        public Task MoveMotorStepsAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.CompletedTask;

        public Task PrepareMotorOnExposureSyncAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.CompletedTask;

        public Task<ScanMotorState> WaitForMotorMotionCompleteAsync(byte motorId, uint steps, uint intervalNs, CancellationToken ct)
            => Task.FromResult(new ScanMotorState(motorId, false, false, false, 0, intervalNs, 0));

        public Task<ScanMotorState> MoveMotorStepsAndWaitForCompletionAsync(byte motorId, bool direction, uint steps, uint intervalNs, CancellationToken ct)
            => Task.FromResult(new ScanMotorState(motorId, false, false, direction, 0, intervalNs, 0));

        public Task StopMotorAsync(byte motorId, CancellationToken ct)
            => Task.CompletedTask;

        public Task ApplyMotorConfigAsync(byte motorId, CancellationToken ct)
            => Task.CompletedTask;

        public Task<ScanOperationResult> SetWarmUpEnabledAsync(bool enabled, CancellationToken ct)
            => Task.FromResult(new ScanOperationResult(true, enabled ? "Warm-up enabled." : "Warm-up disabled."));

        public Task<ScanStartResult> StartScanAsync(int rows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, uint? expectedLineTimeUs = null)
            => Task.FromResult(new ScanStartResult(true, "Started.", []));

        public Task<ScanStartResult> StartSegmentedScanAsync(int totalRows, CancellationToken ct, Action<string>? onStatus = null, Action<string>? onDiagnostic = null, Action<int, int>? onProgress = null, uint? expectedLineTimeUs = null)
            => Task.FromResult(new ScanStartResult(true, "Started.", []));

        public Task<ScanStopResult> StopScanAsync(CancellationToken ct)
            => Task.FromResult(new ScanStopResult(true, "Stopped."));

        public Task<ScanControlFrame> SendControlCommandAndWaitAckAsync(byte[] command, byte expectedCommand, int totalTimeoutMs, CancellationToken ct, bool ignoreForeignCommands = true)
            => Task.FromException<ScanControlFrame>(new NotSupportedException());

        public void Dispose()
        {
            DisposeAsyncCallCount++;
            IsConnected = false;
            _ = MotionEventReceived;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
