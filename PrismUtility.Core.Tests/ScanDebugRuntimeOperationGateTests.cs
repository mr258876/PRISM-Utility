using Xunit;
using PRISM_Utility.Core.Models;

namespace PrismUtility.Core.Tests;

[Trait("Category", "RuntimeGate")]
public sealed class ScanDebugRuntimeOperationGateTests
{
    [Fact]
    public void RuntimeGate_EveryCommandHasAnExplicitPolicyAndExactPairwiseConflictSemantics()
    {
        var connectedIdle = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);
        var disconnectedIdle = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false);

        foreach (var command in Enum.GetValues<ScanDebugRuntimeCommandKind>())
        {
            Assert.True(ExpectedPolicies.TryGetValue(command, out var expected), $"{command} must have an expected policy row.");
            var actual = ScanDebugRuntimeOperationGate.GetPolicy(command);

            Assert.Equal(expected.RequiresDeviceConnection, actual.RequiresDeviceConnection);
            Assert.Equal(expected.ClaimedOperation, actual.ClaimedOperation);
            Assert.True(actual.ConflictingActiveOperations.SetEquals(expected.ConflictingActiveOperations), $"{command} has an unexpected conflict set.");
            Assert.True(ScanDebugRuntimeOperationGate.Evaluate(connectedIdle, command).CanExecute, $"{command} must be allowed while connected and idle.");
            var offline = ScanDebugRuntimeOperationGate.Evaluate(disconnectedIdle, command);
            Assert.Equal(
                expected.RequiresDeviceConnection
                    ? ScanDebugRuntimeOperationGateReason.DeviceDisconnected
                    : ScanDebugRuntimeOperationGateReason.Allowed,
                offline.ReasonCode);

            foreach (var activeOperation in Enum.GetValues<ScanDebugRuntimeOperation>().Where(operation => operation != ScanDebugRuntimeOperation.None))
            {
                var result = ScanDebugRuntimeOperationGate.Evaluate(
                    new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true, [activeOperation]),
                    command);
                var conflicts = expected.ConflictingActiveOperations.Contains(activeOperation);

                Assert.Equal(!conflicts, result.CanExecute);
                Assert.Equal(
                    conflicts ? ScanDebugRuntimeOperationGateReason.RuntimeOperationActive : ScanDebugRuntimeOperationGateReason.Allowed,
                    result.ReasonCode);
            }
        }

        Assert.Equal(Enum.GetValues<ScanDebugRuntimeCommandKind>().Length, ExpectedPolicies.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => ScanDebugRuntimeOperationGate.GetPolicy((ScanDebugRuntimeCommandKind)(-1)));
    }

    [Fact]
    public void RuntimeGate_StopAllFocusIsLocalStopAndLeavesClaimsUnchanged()
    {
        Assert.True(ScanDebugRuntimeOperationGate.IsStopOrCancellation(ScanDebugRuntimeCommandKind.StopAllFocus));

        var claims = new ScanDebugRuntimeOperationClaims();
        var offline = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false);
        Assert.True(claims.TryClaim(offline, ScanDebugRuntimeCommandKind.StopAllFocus, out var offlineLease, out _));
        Assert.Null(offlineLease);
        Assert.Equal(offline.ActiveOperations, claims.CreateSnapshot(offline).ActiveOperations);

        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.AutoFocus, out var autofocusLease, out _));
        try
        {
            var before = claims.CreateSnapshot(connected);
            Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.StopAllFocus, out var stopLease, out _));
            Assert.Null(stopLease);
            var after = claims.CreateSnapshot(connected);

            Assert.Equal(before.IsDeviceConnected, after.IsDeviceConnected);
            Assert.Equal(before.ActiveOperations, after.ActiveOperations);
            foreach (var activeOperation in Enum.GetValues<ScanDebugRuntimeOperation>().Where(operation => operation != ScanDebugRuntimeOperation.None))
            {
                Assert.True(ScanDebugRuntimeOperationGate.Evaluate(
                    new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true, [activeOperation]),
                    ScanDebugRuntimeCommandKind.StopAllFocus).CanExecute);
            }
        }
        finally
        {
            autofocusLease?.Dispose();
        }
    }

    [Fact]
    public void RuntimeGate_StopAllMotorsRequiresConnectionButIsAllowedDuringEveryActiveOperation()
    {
        Assert.True(ScanDebugRuntimeOperationGate.IsStopOrCancellation(ScanDebugRuntimeCommandKind.StopAllMotors));
        foreach (var operation in Enum.GetValues<ScanDebugRuntimeOperation>().Where(operation => operation != ScanDebugRuntimeOperation.None))
        {
            Assert.True(ScanDebugRuntimeOperationGate.Evaluate(
                new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true, [operation]),
                ScanDebugRuntimeCommandKind.StopAllMotors).CanExecute);
        }

        Assert.Equal(
            ScanDebugRuntimeOperationGateReason.DeviceDisconnected,
            ScanDebugRuntimeOperationGate.Evaluate(
                new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false),
                ScanDebugRuntimeCommandKind.StopAllMotors).ReasonCode);
    }

    [Theory]
    [InlineData(ScanDebugRuntimeOperation.Scan, ScanDebugRuntimeCommandKind.NewFilmProfile)]
    [InlineData(ScanDebugRuntimeOperation.Scan, ScanDebugRuntimeCommandKind.LoadFilmProfileJson)]
    [InlineData(ScanDebugRuntimeOperation.Scan, ScanDebugRuntimeCommandKind.ApplyStagedFilmProfileImport)]
    [InlineData(ScanDebugRuntimeOperation.AutoCalibration, ScanDebugRuntimeCommandKind.MoveMotor)]
    [InlineData(ScanDebugRuntimeOperation.AutoFocus, ScanDebugRuntimeCommandKind.DeviceGlobalWrite)]
    [InlineData(ScanDebugRuntimeOperation.AutoFocus, ScanDebugRuntimeCommandKind.ApplyDeviceClock)]
    [InlineData(ScanDebugRuntimeOperation.Motor, ScanDebugRuntimeCommandKind.StartScan)]
    public void RuntimeGate_Requirement225Conflicts_ReturnStableConflictReason(
        ScanDebugRuntimeOperation activeOperation,
        ScanDebugRuntimeCommandKind command)
    {
        var result = ScanDebugRuntimeOperationGate.Evaluate(
            new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true, [activeOperation]),
            command);

        Assert.False(result.CanExecute);
        Assert.Equal(ScanDebugRuntimeOperationGateReason.RuntimeOperationActive, result.ReasonCode);
    }

    [Theory]
    [InlineData(ScanDebugRuntimeOperation.Scan)]
    [InlineData(ScanDebugRuntimeOperation.AutoCalibration)]
    [InlineData(ScanDebugRuntimeOperation.AutoFocus)]
    [InlineData(ScanDebugRuntimeOperation.Motor)]
    [InlineData(ScanDebugRuntimeOperation.Illumination)]
    public void RuntimeGate_ProfileJsonSaveAndValidation_RemainAvailableDuringRuntimeWork(
        ScanDebugRuntimeOperation activeOperation)
    {
        var snapshot = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true, [activeOperation]);

        Assert.True(ScanDebugRuntimeOperationGate.Evaluate(snapshot, ScanDebugRuntimeCommandKind.SaveFilmProfileJson).CanExecute);
        Assert.True(ScanDebugRuntimeOperationGate.Evaluate(snapshot, ScanDebugRuntimeCommandKind.ValidateFilmProfile).CanExecute);
    }

    [Fact]
    public void RuntimeGate_ManualReferenceApplyAllowsOfflineDraftClaimWithoutClearingOnDisconnect()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var offline = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false);
        var command = ScanDebugRuntimeCommandKind.ApplyManualReferenceLevels;

        Assert.False(ScanDebugRuntimeOperationGate.RequiresDeviceConnection(command));
        Assert.False(ScanDebugRuntimeOperationGate.IsStopOrCancellation(command));
        Assert.Equal(ScanDebugRuntimeOperation.ProfileLifecycle, ScanDebugRuntimeOperationGate.ClaimedOperationFor(command));
        Assert.True(claims.TryClaim(offline, command, out var lease, out var result));
        Assert.Equal(ScanDebugRuntimeOperationGateReason.Allowed, result.ReasonCode);
        Assert.NotNull(lease);
        try
        {
            claims.ClearDeviceBoundClaims();
            Assert.Contains(ScanDebugRuntimeOperation.ProfileLifecycle, claims.CreateSnapshot(offline).ActiveOperations);
            Assert.False(claims.TryClaim(offline, command, out _, out var conflict));
            Assert.Equal(ScanDebugRuntimeOperationGateReason.RuntimeOperationActive, conflict.ReasonCode);
        }
        finally
        {
            lease?.Dispose();
        }

        Assert.True(claims.TryClaim(offline, command, out var nextLease, out _));
        nextLease?.Dispose();
    }

    [Theory]
    [InlineData(ScanDebugRuntimeCommandKind.AutoCalibrate)]
    [InlineData(ScanDebugRuntimeCommandKind.ApplyParameters)]
    [InlineData(ScanDebugRuntimeCommandKind.SaveChannelProfile)]
    [InlineData(ScanDebugRuntimeCommandKind.SaveFilmProfileJson)]
    [InlineData(ScanDebugRuntimeCommandKind.LoadFilmProfileJson)]
    [InlineData(ScanDebugRuntimeCommandKind.ApplyStagedFilmProfileImport)]
    [InlineData(ScanDebugRuntimeCommandKind.StartScan)]
    public void RuntimeGate_ManualReferenceApplyConflictsWithActiveMutationsInBothClaimOrders(
        ScanDebugRuntimeCommandKind otherCommand)
    {
        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);
        var manualCommand = ScanDebugRuntimeCommandKind.ApplyManualReferenceLevels;

        foreach (var firstCommand in new[] { manualCommand, otherCommand })
        {
            var claims = new ScanDebugRuntimeOperationClaims();
            var secondCommand = firstCommand == manualCommand ? otherCommand : manualCommand;
            Assert.True(claims.TryClaim(connected, firstCommand, out var firstLease, out _));
            try
            {
                Assert.False(claims.TryClaim(connected, secondCommand, out var rejectedLease, out var result));
                Assert.Null(rejectedLease);
                Assert.Equal(ScanDebugRuntimeOperationGateReason.RuntimeOperationActive, result.ReasonCode);
            }
            finally
            {
                firstLease?.Dispose();
            }
        }
    }

    [Fact]
    public async Task RuntimeGate_SimultaneousManualReferenceClaimsAdmitExactlyOne()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        using var start = new Barrier(2);
        var accepted = 0;
        var leases = new List<ScanDebugRuntimeOperationClaims.ScanDebugRuntimeOperationLease>();
        var leasesGate = new object();

        await Task.WhenAll(Enumerable.Range(0, 2).Select(taskIndex => Task.Run(() =>
        {
            start.SignalAndWait();
            if (!claims.TryClaim(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false),
                    ScanDebugRuntimeCommandKind.ApplyManualReferenceLevels, out var lease, out _))
                return;

            Interlocked.Increment(ref accepted);
            lock (leasesGate)
            {
                if (lease is not null)
                    leases.Add(lease);
            }
        })));

        foreach (var lease in leases)
            lease.Dispose();
        Assert.Equal(1, accepted);
    }

    [Fact]
    public void RuntimeGate_ManualReferenceLeaseDoesNotDisableExistingStopGates()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.ApplyManualReferenceLevels, out var lease, out _));
        try
        {
            var active = claims.CreateSnapshot(connected);
            foreach (var stop in new[]
            {
                ScanDebugRuntimeCommandKind.StopScan,
                ScanDebugRuntimeCommandKind.StopMotor,
                ScanDebugRuntimeCommandKind.StopManualFocus,
                ScanDebugRuntimeCommandKind.StopAllFocus,
                ScanDebugRuntimeCommandKind.StopAllMotors
            })
            {
                Assert.True(ScanDebugRuntimeOperationGate.IsStopOrCancellation(stop));
                Assert.True(ScanDebugRuntimeOperationGate.Evaluate(active, stop).CanExecute);
            }

            Assert.True(ScanDebugRuntimeOperationGate.Evaluate(
                claims.CreateSnapshot(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false)),
                ScanDebugRuntimeCommandKind.StopScan).CanExecute);
            Assert.Equal(ScanDebugRuntimeOperationGateReason.DeviceDisconnected,
                ScanDebugRuntimeOperationGate.Evaluate(
                    claims.CreateSnapshot(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false)),
                    ScanDebugRuntimeCommandKind.StopAllMotors).ReasonCode);
        }
        finally
        {
            lease?.Dispose();
        }
    }

    [Fact]
    public async Task RuntimeGate_ConcurrentConflictingClaims_AllowExactlyOneBoundaryEntry()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var start = new Barrier(2);
        var accepted = 0;
        var leases = new List<ScanDebugRuntimeOperationClaims.ScanDebugRuntimeOperationLease>();
        var leasesGate = new object();

        var tasks = Enumerable.Range(0, 2).Select(taskIndex => Task.Run(() =>
        {
            start.SignalAndWait();
            if (!claims.TryClaim(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true), ScanDebugRuntimeCommandKind.StartScan, out var lease, out _))
                return;

            Interlocked.Increment(ref accepted);
            lock (leasesGate)
            {
                if (lease is not null)
                    leases.Add(lease);
            }
        }));

        await Task.WhenAll(tasks);
        foreach (var lease in leases)
            lease.Dispose();

        Assert.Equal(1, accepted);
    }

    [Fact]
    public void RuntimeGate_RejectedDirectBoundary_LeavesDraftRepositoryAndDeviceProbesUntouched()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var probe = new MutationProbe();
        Assert.True(claims.TryClaim(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true), ScanDebugRuntimeCommandKind.StartScan, out var scanLease, out _));

        try
        {
            if (claims.TryClaim(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true), ScanDebugRuntimeCommandKind.NewFilmProfile, out var draftLease, out _))
            {
                using (draftLease)
                    probe.MutateDraft();
            }

            if (claims.TryClaim(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true), ScanDebugRuntimeCommandKind.SaveChannelProfile, out var repositoryLease, out _))
            {
                using (repositoryLease)
                    probe.WriteRepository();
            }

            if (claims.TryClaim(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true), ScanDebugRuntimeCommandKind.ApplyParameters, out var deviceLease, out _))
            {
                using (deviceLease)
                    probe.WriteDevice();
            }
        }
        finally
        {
            scanLease?.Dispose();
        }

        Assert.Equal(0, probe.DraftMutations);
        Assert.Equal(0, probe.RepositoryWrites);
        Assert.Equal(0, probe.DeviceWrites);
    }

    [Fact]
    public void RuntimeGate_EveryConflictPairRejectsBeforeBoundaryMutation()
    {
        foreach (var (command, policy) in ExpectedPolicies)
        {
            foreach (var activeOperation in policy.ConflictingActiveOperations)
            {
                var claims = new ScanDebugRuntimeOperationClaims();
                var probe = new MutationProbe();
                var accepted = claims.TryClaim(
                    new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true, [activeOperation]),
                    command,
                    out var lease,
                    out var result);
                if (accepted)
                    probe.MutateDraft();
                lease?.Dispose();

                Assert.False(accepted, $"{command} must reject active {activeOperation} at the shared execution boundary.");
                Assert.Equal(ScanDebugRuntimeOperationGateReason.RuntimeOperationActive, result.ReasonCode);
                Assert.Equal(0, probe.DraftMutations);
            }
        }
    }

    [Fact]
    public void RuntimeGate_RejectedStartScanClaimLeavesPreparationActionUntouched()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var probe = new MutationProbe();
        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.AutoFocus, out var activeLease, out _));

        try
        {
            if (claims.TryClaim(connected, ScanDebugRuntimeCommandKind.StartScan, out var startLease, out _))
            {
                using (startLease)
                    probe.MutateDraft();
            }
        }
        finally
        {
            activeLease?.Dispose();
        }

        Assert.Equal(0, probe.DraftMutations);
    }

    [Fact]
    public void RuntimeGate_ProfileExportAndScanAreIntentionallyPairwiseCompatible()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);

        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.SaveFilmProfileJson, out var exportLease, out _));
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.StartScan, out var scanLease, out _));
        scanLease?.Dispose();
        exportLease?.Dispose();

        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.StartScan, out scanLease, out _));
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.SaveFilmProfileJson, out exportLease, out _));
        exportLease?.Dispose();
        scanLease?.Dispose();
    }

    [Fact]
    public void RuntimeGate_ExceptionCancellationAndDisconnect_ClearClaims()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        Assert.True(claims.TryClaim(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true), ScanDebugRuntimeCommandKind.AutoFocus, out var lease, out _));

        try
        {
            throw new OperationCanceledException();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lease?.Dispose();
        }

        Assert.True(ScanDebugRuntimeOperationGate.Evaluate(claims.CreateSnapshot(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true)), ScanDebugRuntimeCommandKind.StartScan).CanExecute);

        Assert.True(claims.TryClaim(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true), ScanDebugRuntimeCommandKind.StartScan, out _, out _));
        claims.Clear();

        var disconnected = claims.CreateSnapshot(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false));
        Assert.False(disconnected.HasActiveOperation);
        Assert.Equal(ScanDebugRuntimeOperationGateReason.DeviceDisconnected, ScanDebugRuntimeOperationGate.Evaluate(disconnected, ScanDebugRuntimeCommandKind.StartScan).ReasonCode);
        Assert.True(ScanDebugRuntimeOperationGate.Evaluate(disconnected, ScanDebugRuntimeCommandKind.PreviewOnlyEdit).CanExecute);
        Assert.True(ScanDebugRuntimeOperationGate.Evaluate(disconnected, ScanDebugRuntimeCommandKind.ValidateFilmProfile).CanExecute);
        Assert.True(ScanDebugRuntimeOperationGate.Evaluate(disconnected, ScanDebugRuntimeCommandKind.SaveFilmProfileJson).CanExecute);
    }

    [Fact]
    public void RuntimeGate_ClearThenReclaimingSameOperation_IgnoresStaleLeaseDisposal()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.AutoFocus, out var staleLease, out _));

        claims.Clear();
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.AutoFocus, out var currentLease, out _));

        staleLease!.Dispose();
        var thirdClaimAccepted = claims.TryClaim(connected, ScanDebugRuntimeCommandKind.StartScan, out var thirdLease, out _);

        currentLease!.Dispose();
        thirdLease?.Dispose();
        Assert.False(thirdClaimAccepted, "Disposing a pre-Clear lease must not release the reclaimed AutoFocus operation.");
    }

    [Fact]
    public void RuntimeGate_DisposingTheSameLeaseTwice_DoesNotReleaseAnotherClaim()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.AutoFocus, out var firstLease, out _));

        firstLease!.Dispose();
        firstLease.Dispose();
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.StartScan, out var secondLease, out _));

        secondLease!.Dispose();
    }

    [Fact]
    public void RuntimeGate_ClaimLifecyclePublishesExactlyOneChangePerSnapshotMutation()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);
        var changes = 0;
        claims.Changed += (_, _) => changes++;

        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.SaveFilmProfileJson, out var exportLease, out _));
        Assert.Equal(1, changes);
        Assert.False(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.LoadFilmProfileJson, out _, out _));
        Assert.Equal(1, changes);

        exportLease!.Dispose();
        exportLease.Dispose();
        Assert.Equal(2, changes);

        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.AutoFocus, out _, out _));
        Assert.Equal(3, changes);
        claims.Clear();
        claims.Clear();
        Assert.Equal(4, changes);
    }

    [Fact]
    public void RuntimeGate_DisconnectClearPreservesUnrelatedProfileClaims()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.SaveFilmProfileJson, out var exportLease, out _));
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.AutoFocus, out var autoFocusLease, out _));
        var clearDeviceClaims = Assert.IsAssignableFrom<System.Reflection.MethodInfo>(
            typeof(ScanDebugRuntimeOperationClaims).GetMethod("ClearDeviceBoundClaims"));

        clearDeviceClaims.Invoke(claims, null);

        var afterDisconnect = claims.CreateSnapshot(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false));
        Assert.Contains(ScanDebugRuntimeOperation.ProfileExport, afterDisconnect.ActiveOperations);
        Assert.DoesNotContain(ScanDebugRuntimeOperation.AutoFocus, afterDisconnect.ActiveOperations);
        Assert.False(ScanDebugRuntimeOperationGate.Evaluate(afterDisconnect, ScanDebugRuntimeCommandKind.NewFilmProfile).CanExecute);
        exportLease!.Dispose();
        autoFocusLease!.Dispose();
    }

    [Fact]
    public void RuntimeGate_AuthoritativeWorkflowScanProjectsIntoDerivedOperationState()
    {
        var workflowOwner = new ScannerSessionOwner(
            "scan-workflow",
            ScannerSessionOwnerType.ScanWorkflow,
            ScannerSessionOperation.Scan,
            DateTimeOffset.UnixEpoch,
            "workflow-lease");
        var snapshot = new ScannerDeviceSessionSnapshot(
            ScannerSessionState.Running,
            "scanner",
            workflowOwner,
            null,
            ScannerReconnectPromptState.None,
            DateTimeOffset.UnixEpoch);
        var projectSessionOperation = Assert.IsAssignableFrom<System.Reflection.MethodInfo>(
            typeof(ScanDebugRuntimeOperationGate).GetMethod("ProjectSessionOperation"));

        var projected = Assert.IsType<ScanDebugRuntimeOperation>(projectSessionOperation.Invoke(null, [snapshot]));

        Assert.Equal(ScanDebugRuntimeOperation.Scan, projected);
        var gateSnapshot = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true, [projected]);
        Assert.False(ScanDebugRuntimeOperationGate.Evaluate(gateSnapshot, ScanDebugRuntimeCommandKind.NewFilmProfile).CanExecute);

        var sourcePath = Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        var source = File.ReadAllText(sourcePath);
        Assert.Contains("ScanDebugRuntimeOperationGate.ProjectSessionOperation(_sessionCoordinator.Snapshot)", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeGate_ConcurrentStaleLeaseDisposalsAfterClear_KeepReclaimedOperationOwned()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.AutoFocus, out var staleLease, out _));
        claims.Clear();
        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.AutoFocus, out var currentLease, out _));

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(staleLease!.Dispose)));
        var conflictingClaimAccepted = claims.TryClaim(connected, ScanDebugRuntimeCommandKind.StartScan, out var conflictingLease, out _);

        currentLease!.Dispose();
        conflictingLease?.Dispose();
        Assert.False(conflictingClaimAccepted);
    }

    [Fact]
    public void RuntimeGate_DisconnectedAutoFocusBoundary_RejectsWithoutClaimingOrMutation()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var probe = new MutationProbe();

        if (claims.TryClaim(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false), ScanDebugRuntimeCommandKind.AutoFocus, out var lease, out _))
        {
            using (lease)
                probe.WriteDevice();
        }

        Assert.Equal(0, probe.DeviceWrites);
        Assert.False(claims.CreateSnapshot(new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false)).HasActiveOperation);
    }

    [Fact]
    public void RuntimeGate_CommandBoundaries_ClaimBeforeSideEffects()
    {
        var sourcePath = Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        var source = File.ReadAllText(sourcePath);

        foreach (var declaration in new[]
        {
            "private async Task ExportDng(",
            "private async Task ConnectDevices(",
            "private async Task DisconnectDevices(",
            "private async Task ApplyDeviceClock(",
            "private async Task ApplyParameters(",
            "private async Task RefreshIllumination(",
            "private async Task ApplyIllumination(",
            "private async Task RefreshMotion(",
            "private async Task MoveMotor(",
            "private async Task StopMotor(",
            "private async Task ApplyMotorConfig(",
            "private async Task SetMotorEnabledCoreAsync(",
            "private async Task RunAutoCalibrationAsync(",
            "private async Task AutoFocus(",
            "public void BeginManualFocusHold(",
            "public void EndManualFocusHold(",
            "private async Task StartScan(",
            "private async Task StopScan(",
            "private async Task SaveColumnSampleAsBlackLevel(",
            "private async Task SaveColumnSampleAsWhiteLevel(",
            "private async Task SaveChannelProfile(",
            "private async Task ClearChannelProfile(",
            "private async Task SaveFilmProfileJson(",
            "private async Task LoadFilmProfileJson(",
            "private async Task ApplyStagedFilmProfileImport(",
            "private void DiscardStagedFilmProfileImport(",
            "private async Task NewFilmProfile(",
            "private void ValidateFilmProfile(",
            "private void ResetSelectedRoi(",
            "private void ApplySelectedRoiInputs(",
            "private void ResetAllRois(",
            "private async Task HandleWarmUpToggleChangedAsync("
        })
        {
            var declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.True(declarationIndex >= 0, $"Could not find {declaration}.");
            var bodyStart = source.IndexOf('{', declarationIndex);
            var body = source[bodyStart..FindMethodBodyEnd(source, bodyStart)];
            Assert.Contains("TryClaimRuntimeOperation", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RuntimeGate_DisconnectionHandler_ClearsClaimsAndRefreshesHardwareAvailability()
    {
        var sourcePath = Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", "ScanDebugViewModel.cs");
        var source = File.ReadAllText(sourcePath);
        var declaration = "private void OnSessionCoordinatorSnapshotChanged(";
        var declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);

        Assert.True(declarationIndex >= 0, "Could not find the session snapshot handler.");
        var handlerStart = source.IndexOf('{', declarationIndex);
        var handler = source[declarationIndex..FindMethodBodyEnd(source, handlerStart)];
        var projectionDeclaration = "private void ApplySessionCoordinatorSnapshot(";
        var projectionIndex = source.IndexOf(projectionDeclaration, StringComparison.Ordinal);
        Assert.True(projectionIndex >= 0, "Could not find the session snapshot projection.");
        var projectionStart = source.IndexOf('{', projectionIndex);
        var projection = source[projectionStart..FindMethodBodyEnd(source, projectionStart)];

        Assert.Contains("_dispatcher.TryEnqueue(() =>", handler, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(snapshot, _sessionCoordinator.Snapshot)", handler, StringComparison.Ordinal);
        Assert.Contains("ApplySessionCoordinatorSnapshot(snapshot);", handler, StringComparison.Ordinal);
        Assert.Contains("ClearRuntimeOperationClaims();", projection, StringComparison.Ordinal);
        Assert.Contains("IsConnected = false;", projection, StringComparison.Ordinal);
        Assert.Contains("NotifyRuntimeOperationAvailabilityChanged();", projection, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeGate_ConnectionSnapshotDrivesAvailabilityAndDirectAutoFocusBoundary()
    {
        var root = FindHostSoftwareRoot();
        var contract = File.ReadAllText(Path.Combine(root, "PRISM Utility.Core", "Contracts", "Services", "IScanDebugSessionCoordinator.cs"));
        var coordinator = File.ReadAllText(Path.Combine(root, "PRISM Utility.Core", "Services", "ScanDebugSessionCoordinator.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "PRISM Utility", "ViewModels", "ScanDebugViewModel.cs"));
        var autofocusIndex = viewModel.IndexOf("private async Task AutoFocus()", StringComparison.Ordinal);
        var autofocusStart = viewModel.IndexOf('{', autofocusIndex);
        var autofocus = viewModel[autofocusStart..FindMethodBodyEnd(viewModel, autofocusStart)];

        Assert.Contains("ScannerDeviceSessionSnapshot Snapshot { get; }", contract, StringComparison.Ordinal);
        Assert.Contains("=> _sessionManager.Snapshot;", coordinator, StringComparison.Ordinal);
        Assert.Contains("private bool IsDeviceConnected", viewModel, StringComparison.Ordinal);
        Assert.Contains("_sessionCoordinator.Snapshot.State is ScannerSessionState.Connected or ScannerSessionState.Running", viewModel, StringComparison.Ordinal);
        Assert.Contains("IsDeviceConnected,", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("IsConnected && !IsConnecting && _sessionCoordinator.HasConnectedSession", viewModel, StringComparison.Ordinal);
        Assert.Equal(1, viewModel.Split("_sessionCoordinator.SnapshotChanged += OnSessionCoordinatorSnapshotChanged", StringSplitOptions.None).Length - 1);
        Assert.True(autofocus.IndexOf("ScanFocusRoi.TryCreate", StringComparison.Ordinal) < autofocus.IndexOf("TryClaimRuntimeOperation", StringComparison.Ordinal));
        Assert.Contains("if (!IsDeviceConnected)", autofocus, StringComparison.Ordinal);

        var notificationIndex = viewModel.IndexOf("private void NotifyRuntimeOperationAvailabilityChanged()", StringComparison.Ordinal);
        var notificationStart = viewModel.IndexOf('{', notificationIndex);
        var notifications = viewModel[notificationStart..FindMethodBodyEnd(viewModel, notificationStart)];
        foreach (var command in new[]
        {
            "ApplyDeviceClockCommand", "ApplyParametersCommand", "RefreshIlluminationCommand", "ApplyIlluminationCommand", "RefreshMotionCommand",
            "EnableMotorCommand", "DisableMotorCommand", "MoveMotorCommand", "StopMotorCommand", "ApplyMotorConfigCommand",
            "AutoBlackAdjustCommand", "AutoWhiteAdjustCommand", "AutoCalibrateCommand", "AutoFocusCommand"
        })
            Assert.Contains($"{command}.NotifyCanExecuteChanged();", notifications, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeGate_ApplyDeviceClockClaimsDeviceGlobalButIsNotTimingHazardousRecovery()
    {
        var disconnected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false);
        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);
        var policy = ScanDebugRuntimeOperationGate.GetPolicy(ScanDebugRuntimeCommandKind.ApplyDeviceClock);

        Assert.True(policy.RequiresDeviceConnection);
        Assert.Equal(ScanDebugRuntimeOperation.DeviceGlobal, policy.ClaimedOperation);
        Assert.True(policy.ConflictingActiveOperations.SetEquals(RuntimeMutationConflicts.Append(ScanDebugRuntimeOperation.DeviceGlobal)));
        Assert.Equal(ScanDebugRuntimeOperationGateReason.DeviceDisconnected, ScanDebugRuntimeOperationGate.Evaluate(disconnected, ScanDebugRuntimeCommandKind.ApplyDeviceClock).ReasonCode);
        Assert.True(ScanDebugRuntimeOperationGate.Evaluate(connected, ScanDebugRuntimeCommandKind.ApplyDeviceClock).CanExecute);
        Assert.False(ScanDebugRuntimeOperationGate.IsStopOrCancellation(ScanDebugRuntimeCommandKind.ApplyDeviceClock));

        var viewModel = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", "ScanDebugViewModel.cs"));
        var hazardStart = viewModel.IndexOf("private static bool IsTimingHazardousDeviceCommand", StringComparison.Ordinal);
        Assert.True(hazardStart >= 0);
        var hazardEnd = viewModel.IndexOf(';', hazardStart);
        Assert.True(hazardEnd > hazardStart);
        var hazardBody = viewModel[hazardStart..(hazardEnd + 1)];
        Assert.Contains("DeviceGlobalWrite", hazardBody, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyDeviceClock", hazardBody, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeGate_AutoFocusUiProjectsTheAuthoritativeOfflineState()
    {
        var root = FindHostSoftwareRoot();
        var viewModel = File.ReadAllText(Path.Combine(root, "PRISM Utility", "ViewModels", "ScanDebugViewModel.cs"));
        var xaml = File.ReadAllText(Path.Combine(root, "PRISM Utility", "Views", "ScanDebugPage.xaml"));
        var page = File.ReadAllText(Path.Combine(root, "PRISM Utility", "Views", "ScanDebugPage.xaml.cs"));

        Assert.Contains("public bool CanRunAutoFocusAction", viewModel, StringComparison.Ordinal);
        Assert.Contains("=> CanRunAutoFocus();", viewModel, StringComparison.Ordinal);
        Assert.Contains("OnPropertyChanged(nameof(CanRunAutoFocusAction));", viewModel, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{x:Bind ViewModel.CanRunAutoFocusAction, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"AutoFocusButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{x:Bind ViewModel.AutoFocusCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("if (ViewModel.CanRunAutoFocusAction)", page, StringComparison.Ordinal);
        Assert.Contains("ViewModel.AutoFocusCommand.Execute(null);", page, StringComparison.Ordinal);
    }

    [Fact]
    public void OracleRed_DeviceGlobalWriteRequiresAnActiveConnection()
    {
        var result = ScanDebugRuntimeOperationGate.Evaluate(
            new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false),
            ScanDebugRuntimeCommandKind.DeviceGlobalWrite);

        Assert.False(result.CanExecute);
        Assert.Equal(ScanDebugRuntimeOperationGateReason.DeviceDisconnected, result.ReasonCode);
    }

    [Fact]
    public void OracleRed_ProfileExportClaimsAndSerializesAgainstProfileMutations()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true);

        Assert.True(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.SaveFilmProfileJson, out var exportLease, out _));
        try
        {
            Assert.NotNull(exportLease);
            Assert.False(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.LoadFilmProfileJson, out _, out _));
            Assert.False(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.ApplyStagedFilmProfileImport, out _, out _));
            Assert.False(claims.TryClaim(connected, ScanDebugRuntimeCommandKind.SaveChannelProfile, out _, out _));
        }
        finally
        {
            exportLease?.Dispose();
        }
    }

    [Fact]
    public async Task OracleRed_ConcurrentConnectionBoundariesAdmitExactlyOneClaim()
    {
        var claims = new ScanDebugRuntimeOperationClaims();
        var connected = new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: false);
        var start = new Barrier(2);
        var accepted = 0;
        var leases = new List<ScanDebugRuntimeOperationClaims.ScanDebugRuntimeOperationLease>();
        var leasesGate = new object();

        await Task.WhenAll(Enumerable.Range(0, 2).Select(taskIndex => Task.Run(() =>
        {
            start.SignalAndWait();
            if (!claims.TryClaim(connected, ScanDebugRuntimeCommandKind.ConnectDevices, out var lease, out _))
                return;

            if (lease is not null)
            {
                Interlocked.Increment(ref accepted);
                lock (leasesGate)
                    leases.Add(lease);
            }
        })));

        foreach (var lease in leases)
            lease.Dispose();

        Assert.Equal(1, accepted);
    }

    [Fact]
    public void OracleRed_DisconnectIsRejectedWhileScanIsRunning()
    {
        var result = ScanDebugRuntimeOperationGate.Evaluate(
            new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true, [ScanDebugRuntimeOperation.Scan]),
            ScanDebugRuntimeCommandKind.DisconnectDevices);

        Assert.False(result.CanExecute);
        Assert.Equal(ScanDebugRuntimeOperationGateReason.RuntimeOperationActive, result.ReasonCode);
    }

    [Fact]
    public void OracleRed_StartScanClaimsBeforeAnyPreparationOrInputNormalization()
    {
        var source = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", "ScanDebugViewModel.cs"));
        var declarationIndex = source.IndexOf("private async Task StartScan()", StringComparison.Ordinal);
        var bodyStart = source.IndexOf('{', declarationIndex);
        var body = source[bodyStart..FindMethodBodyEnd(source, bodyStart)];
        var claimIndex = body.IndexOf("TryClaimRuntimeOperation(ScanDebugRuntimeCommandKind.StartScan", StringComparison.Ordinal);

        Assert.True(claimIndex >= 0);
        foreach (var preparation in new[] { "TryParseRequestedRows", "EnsureDeviceSettingsInitializedAsync", "TryBuildDebugWorkflowRequest" })
        {
            var preparationIndex = body.IndexOf(preparation, StringComparison.Ordinal);
            Assert.True(preparationIndex < 0 || claimIndex < preparationIndex, $"StartScan must claim before {preparation}.");
        }
    }

    [Fact]
    public void OracleRed_DiscardStagedImportIsNotBlockedByAnUnrelatedScan()
    {
        var result = ScanDebugRuntimeOperationGate.Evaluate(
            new ScanDebugRuntimeOperationSnapshot(isDeviceConnected: true, [ScanDebugRuntimeOperation.Scan]),
            ScanDebugRuntimeCommandKind.DiscardStagedFilmProfileImport);

        Assert.True(result.CanExecute);
    }

    [Fact]
    public void OracleRed_DisconnectDoesNotProjectLocalDisconnectionAfterCoordinatorRejection()
    {
        var source = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", "ScanDebugViewModel.cs"));
        var declarationIndex = source.IndexOf("private async Task DisconnectDevices()", StringComparison.Ordinal);
        var bodyStart = source.IndexOf('{', declarationIndex);
        var body = source[bodyStart..FindMethodBodyEnd(source, bodyStart)];
        var resultIndex = body.IndexOf("var result = await _sessionCoordinator.DisconnectAsync", StringComparison.Ordinal);
        var rejectionIndex = body.IndexOf("if (!result.Success)", StringComparison.Ordinal);
        var projectionIndex = body.IndexOf("SwitchToDiscoverySession();", StringComparison.Ordinal);

        Assert.True(resultIndex >= 0);
        Assert.True(rejectionIndex > resultIndex);
        Assert.True(projectionIndex > rejectionIndex);
    }

    private static IReadOnlyDictionary<ScanDebugRuntimeCommandKind, ExpectedPolicy> ExpectedPolicies
        => ExpectedPoliciesSource.Value;

    private static readonly Lazy<IReadOnlyDictionary<ScanDebugRuntimeCommandKind, ExpectedPolicy>> ExpectedPoliciesSource = new(CreateExpectedPolicies);

    private static IReadOnlyDictionary<ScanDebugRuntimeCommandKind, ExpectedPolicy> CreateExpectedPolicies()
        => new Dictionary<ScanDebugRuntimeCommandKind, ExpectedPolicy>
        {
            [ScanDebugRuntimeCommandKind.ConnectDevices] = Expected(false, ScanDebugRuntimeOperation.Connection, ConnectionConflicts),
            [ScanDebugRuntimeCommandKind.DisconnectDevices] = Expected(true, ScanDebugRuntimeOperation.Connection, ConnectionConflicts),
            [ScanDebugRuntimeCommandKind.StartScan] = Expected(true, ScanDebugRuntimeOperation.Scan, ScanStartConflicts),
            [ScanDebugRuntimeCommandKind.StopScan] = Expected(false, ScanDebugRuntimeOperation.None),
            [ScanDebugRuntimeCommandKind.ExportDng] = Expected(false, ScanDebugRuntimeOperation.OutputCapture, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.ApplyDeviceClock] = Expected(true, ScanDebugRuntimeOperation.DeviceGlobal, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.ApplyParameters] = Expected(true, ScanDebugRuntimeOperation.ParameterApplication, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.RefreshIllumination] = Expected(true, ScanDebugRuntimeOperation.Illumination, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.ApplyIllumination] = Expected(true, ScanDebugRuntimeOperation.Illumination, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.RefreshMotion] = Expected(true, ScanDebugRuntimeOperation.Motor, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.EnableMotor] = Expected(true, ScanDebugRuntimeOperation.Motor, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.DisableMotor] = Expected(true, ScanDebugRuntimeOperation.Motor, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.MoveMotor] = Expected(true, ScanDebugRuntimeOperation.Motor, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.StopMotor] = Expected(true, ScanDebugRuntimeOperation.None),
            [ScanDebugRuntimeCommandKind.ApplyMotorConfig] = Expected(true, ScanDebugRuntimeOperation.Motor, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.AutoBlackAdjust] = Expected(true, ScanDebugRuntimeOperation.AutoCalibration, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.AutoWhiteAdjust] = Expected(true, ScanDebugRuntimeOperation.AutoCalibration, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.AutoCalibrate] = Expected(true, ScanDebugRuntimeOperation.AutoCalibration, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.RestoreCalibrationCandidate] = Expected(true, ScanDebugRuntimeOperation.ParameterApplication, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.AcceptCalibrationCandidate] = Expected(true, ScanDebugRuntimeOperation.ParameterApplication, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.AcceptAndSaveCalibrationCandidate] = Expected(true, ScanDebugRuntimeOperation.ParameterApplication, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.AutoFocus] = Expected(true, ScanDebugRuntimeOperation.AutoFocus, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.StartManualFocus] = Expected(true, ScanDebugRuntimeOperation.ManualFocus, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.StopManualFocus] = Expected(false, ScanDebugRuntimeOperation.None),
            [ScanDebugRuntimeCommandKind.StopAllFocus] = Expected(false, ScanDebugRuntimeOperation.None),
            [ScanDebugRuntimeCommandKind.StopAllMotors] = Expected(true, ScanDebugRuntimeOperation.None),
            [ScanDebugRuntimeCommandKind.UpdateFocusMapping] = Expected(false, ScanDebugRuntimeOperation.DeviceGlobal, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.SaveChannelProfile] = Expected(false, ScanDebugRuntimeOperation.CalibrationRepository, RepositoryMutationConflicts),
            [ScanDebugRuntimeCommandKind.ClearChannelProfile] = Expected(false, ScanDebugRuntimeOperation.CalibrationRepository, RepositoryMutationConflicts),
            [ScanDebugRuntimeCommandKind.SaveColumnSampleAsBlackLevel] = Expected(false, ScanDebugRuntimeOperation.CalibrationRepository, RepositoryMutationConflicts),
            [ScanDebugRuntimeCommandKind.SaveColumnSampleAsWhiteLevel] = Expected(false, ScanDebugRuntimeOperation.CalibrationRepository, RepositoryMutationConflicts),
            [ScanDebugRuntimeCommandKind.SaveFilmProfileJson] = Expected(false, ScanDebugRuntimeOperation.ProfileExport, ProfileExportConflicts),
            [ScanDebugRuntimeCommandKind.LoadFilmProfileJson] = Expected(false, ScanDebugRuntimeOperation.ProfileImport, ProfileImportConflicts),
            [ScanDebugRuntimeCommandKind.ApplyStagedFilmProfileImport] = Expected(false, ScanDebugRuntimeOperation.ProfileLifecycle, ProfileImportConflicts),
            [ScanDebugRuntimeCommandKind.DiscardStagedFilmProfileImport] = Expected(false, ScanDebugRuntimeOperation.ProfileLifecycle, ProfileOnlyConflicts),
            [ScanDebugRuntimeCommandKind.NewFilmProfile] = Expected(false, ScanDebugRuntimeOperation.ProfileLifecycle, ProfileImportConflicts),
            [ScanDebugRuntimeCommandKind.ApplyManualReferenceLevels] = Expected(false, ScanDebugRuntimeOperation.ProfileLifecycle, ManualReferenceLevelConflicts),
            [ScanDebugRuntimeCommandKind.ValidateFilmProfile] = Expected(false, ScanDebugRuntimeOperation.ProfileLifecycle, ProfileMutationConflicts),
            [ScanDebugRuntimeCommandKind.ResetSelectedRoi] = Expected(false, ScanDebugRuntimeOperation.ProfileLifecycle, ProfileMutationConflicts),
            [ScanDebugRuntimeCommandKind.ApplySelectedRoiInputs] = Expected(false, ScanDebugRuntimeOperation.ProfileLifecycle, ProfileMutationConflicts),
            [ScanDebugRuntimeCommandKind.ResetAllRois] = Expected(false, ScanDebugRuntimeOperation.ProfileLifecycle, ProfileMutationConflicts),
            [ScanDebugRuntimeCommandKind.DeviceGlobalWrite] = Expected(true, ScanDebugRuntimeOperation.DeviceGlobal, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.CalibrationLibraryReplace] = Expected(false, ScanDebugRuntimeOperation.CalibrationRepository, RepositoryMutationConflicts),
            [ScanDebugRuntimeCommandKind.RawHardwareCommand] = Expected(true, ScanDebugRuntimeOperation.RawHardware, RuntimeMutationConflicts),
            [ScanDebugRuntimeCommandKind.PreviewOnlyEdit] = Expected(false, ScanDebugRuntimeOperation.None)
        };

    private static readonly ScanDebugRuntimeOperation[] ConnectionConflicts =
    [
        ScanDebugRuntimeOperation.Scan,
        ScanDebugRuntimeOperation.OutputCapture,
        ScanDebugRuntimeOperation.ParameterApplication,
        ScanDebugRuntimeOperation.AutoCalibration,
        ScanDebugRuntimeOperation.AutoFocus,
        ScanDebugRuntimeOperation.ManualFocus,
        ScanDebugRuntimeOperation.Motor,
        ScanDebugRuntimeOperation.Illumination,
        ScanDebugRuntimeOperation.ProfileExport,
        ScanDebugRuntimeOperation.ProfileImport,
        ScanDebugRuntimeOperation.ProfileLifecycle,
        ScanDebugRuntimeOperation.CalibrationRepository,
        ScanDebugRuntimeOperation.DeviceGlobal,
        ScanDebugRuntimeOperation.RawHardware
    ];

    private static readonly ScanDebugRuntimeOperation[] ScanStartConflicts =
    [
        ScanDebugRuntimeOperation.Connection,
        ScanDebugRuntimeOperation.OutputCapture,
        ScanDebugRuntimeOperation.ParameterApplication,
        ScanDebugRuntimeOperation.AutoCalibration,
        ScanDebugRuntimeOperation.AutoFocus,
        ScanDebugRuntimeOperation.ManualFocus,
        ScanDebugRuntimeOperation.Motor,
        ScanDebugRuntimeOperation.Illumination,
        ScanDebugRuntimeOperation.ProfileImport,
        ScanDebugRuntimeOperation.ProfileLifecycle,
        ScanDebugRuntimeOperation.CalibrationRepository,
        ScanDebugRuntimeOperation.DeviceGlobal,
        ScanDebugRuntimeOperation.RawHardware
    ];

    private static readonly ScanDebugRuntimeOperation[] RuntimeMutationConflicts =
    [
        ScanDebugRuntimeOperation.Connection,
        ScanDebugRuntimeOperation.Scan,
        ScanDebugRuntimeOperation.OutputCapture,
        ScanDebugRuntimeOperation.ParameterApplication,
        ScanDebugRuntimeOperation.AutoCalibration,
        ScanDebugRuntimeOperation.AutoFocus,
        ScanDebugRuntimeOperation.ManualFocus,
        ScanDebugRuntimeOperation.Motor,
        ScanDebugRuntimeOperation.Illumination,
        ScanDebugRuntimeOperation.ProfileImport,
        ScanDebugRuntimeOperation.ProfileLifecycle,
        ScanDebugRuntimeOperation.CalibrationRepository,
        ScanDebugRuntimeOperation.DeviceGlobal,
        ScanDebugRuntimeOperation.RawHardware
    ];

    private static readonly ScanDebugRuntimeOperation[] ProfileExportConflicts =
    [
        ScanDebugRuntimeOperation.Connection,
        ScanDebugRuntimeOperation.ProfileImport,
        ScanDebugRuntimeOperation.ProfileLifecycle,
        ScanDebugRuntimeOperation.CalibrationRepository
    ];

    private static readonly ScanDebugRuntimeOperation[] RepositoryMutationConflicts =
    [
        ScanDebugRuntimeOperation.Connection,
        ScanDebugRuntimeOperation.Scan,
        ScanDebugRuntimeOperation.OutputCapture,
        ScanDebugRuntimeOperation.ParameterApplication,
        ScanDebugRuntimeOperation.AutoCalibration,
        ScanDebugRuntimeOperation.AutoFocus,
        ScanDebugRuntimeOperation.ManualFocus,
        ScanDebugRuntimeOperation.Motor,
        ScanDebugRuntimeOperation.Illumination,
        ScanDebugRuntimeOperation.ProfileExport,
        ScanDebugRuntimeOperation.ProfileImport,
        ScanDebugRuntimeOperation.ProfileLifecycle,
        ScanDebugRuntimeOperation.DeviceGlobal,
        ScanDebugRuntimeOperation.RawHardware
    ];

    private static readonly ScanDebugRuntimeOperation[] ProfileImportConflicts =
    [
        ScanDebugRuntimeOperation.Connection,
        ScanDebugRuntimeOperation.Scan,
        ScanDebugRuntimeOperation.OutputCapture,
        ScanDebugRuntimeOperation.ParameterApplication,
        ScanDebugRuntimeOperation.AutoCalibration,
        ScanDebugRuntimeOperation.AutoFocus,
        ScanDebugRuntimeOperation.ManualFocus,
        ScanDebugRuntimeOperation.Motor,
        ScanDebugRuntimeOperation.Illumination,
        ScanDebugRuntimeOperation.ProfileExport,
        ScanDebugRuntimeOperation.ProfileLifecycle,
        ScanDebugRuntimeOperation.CalibrationRepository,
        ScanDebugRuntimeOperation.DeviceGlobal,
        ScanDebugRuntimeOperation.RawHardware
    ];

    private static readonly ScanDebugRuntimeOperation[] ProfileMutationConflicts =
    [
        ScanDebugRuntimeOperation.Connection,
        ScanDebugRuntimeOperation.ProfileExport,
        ScanDebugRuntimeOperation.ProfileImport,
        ScanDebugRuntimeOperation.CalibrationRepository
    ];

    private static readonly ScanDebugRuntimeOperation[] ManualReferenceLevelConflicts =
    [
        ..ProfileImportConflicts,
        ScanDebugRuntimeOperation.ProfileImport
    ];

    private static readonly ScanDebugRuntimeOperation[] ProfileOnlyConflicts =
    [
        ScanDebugRuntimeOperation.ProfileExport,
        ScanDebugRuntimeOperation.ProfileImport
    ];

    private static ExpectedPolicy Expected(
        bool requiresDeviceConnection,
        ScanDebugRuntimeOperation claimedOperation,
        params ScanDebugRuntimeOperation[] conflicts)
    {
        var expectedConflicts = conflicts.ToHashSet();
        if (claimedOperation != ScanDebugRuntimeOperation.None)
            expectedConflicts.Add(claimedOperation);

        return new ExpectedPolicy(requiresDeviceConnection, claimedOperation, expectedConflicts);
    }

    private sealed record ExpectedPolicy(
        bool RequiresDeviceConnection,
        ScanDebugRuntimeOperation ClaimedOperation,
        IReadOnlySet<ScanDebugRuntimeOperation> ConflictingActiveOperations);

    private static int FindMethodBodyEnd(string source, int bodyStart)
    {
        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return index + 1;
        }

        throw new InvalidOperationException("Could not find the end of the method body.");
    }

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility.Core")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the Host Software source root.");
    }

    private sealed class MutationProbe
    {
        public int DraftMutations { get; private set; }
        public int RepositoryWrites { get; private set; }
        public int DeviceWrites { get; private set; }

        public void MutateDraft() => DraftMutations++;

        public void WriteRepository() => RepositoryWrites++;

        public void WriteDevice() => DeviceWrites++;
    }
}
