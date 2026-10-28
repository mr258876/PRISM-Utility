using System.Reflection;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanDebugCalibrationStatusTests
{
    [Fact]
    public async Task WhiteCapture_CompositeUsesOwnParametersAndRestoresPreviousDeviceState()
    {
        var previous = WhiteTestProfile(99, 10, 2000).Parameters;
        var parameters = new StatusParameters(loadedSnapshot: previous);
        var illumination = new WhiteCaptureIllumination();
        var draft = WhiteTestDraft(includeComposite: true).SwitchWhiteLightSource(WhiteLightSource.RgbComposite);
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, draft, attachRuntimeBindings: true,
            parameters: parameters, illumination: illumination);
        var beforeCount = parameters.AppliedSnapshots.Count;

        var result = await InvokeWhiteCaptureAsync(harness.ViewModel, new StatusSession(true), () =>
        {
            Assert.Equal((ushort)60, parameters.AppliedSnapshots.Last().ExposureTicks);
            Assert.Equal((byte)0b1101, illumination.AppliedStates.Last().SteadyMask);
            return Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(new ushort[] { 60, 99 }, parameters.AppliedSnapshots.Skip(beforeCount).Select(snapshot => snapshot.ExposureTicks));
        Assert.Equal(previous, parameters.AppliedSnapshots.Last());
        Assert.Equal(illumination.Original, Assert.Single(illumination.RestoredStates));
        Assert.Equal(1, illumination.TurnOffCount);
    }

    [Fact]
    public async Task WhiteCapture_MissingCompositeProfileNeverUsesDedicatedParameters()
    {
        var parameters = new StatusParameters();
        var illumination = new WhiteCaptureIllumination();
        var draft = WhiteTestDraft(includeComposite: false).SwitchWhiteLightSource(WhiteLightSource.RgbComposite);
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, draft, attachRuntimeBindings: true,
            parameters: parameters, illumination: illumination);
        var beforeApply = parameters.ChannelApplyCount;
        var beforeLoad = parameters.LoadCount;
        var operationCalled = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeWhiteCaptureAsync(harness.ViewModel, new StatusSession(true), () =>
        {
            operationCalled = true;
            return Task.FromResult(42);
        }));

        Assert.False(operationCalled);
        Assert.Equal(beforeApply, parameters.ChannelApplyCount);
        Assert.Equal(beforeLoad, parameters.LoadCount);
        Assert.Empty(illumination.AppliedStates);
        Assert.Equal(0, illumination.ReadCount);
    }

    [Fact]
    public async Task WhiteCapture_FailedCaptureStillRestoresParametersAndLighting()
    {
        var previous = WhiteTestProfile(99, 10, 2000).Parameters;
        var parameters = new StatusParameters(loadedSnapshot: previous);
        var illumination = new WhiteCaptureIllumination();
        var draft = WhiteTestDraft(includeComposite: true).SwitchWhiteLightSource(WhiteLightSource.RgbComposite);
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, draft, attachRuntimeBindings: true,
            parameters: parameters, illumination: illumination);

        await Assert.ThrowsAsync<IOException>(() => InvokeWhiteCaptureAsync(harness.ViewModel, new StatusSession(true),
            () => Task.FromException<int>(new IOException("capture failed"))));

        Assert.Equal(previous, parameters.AppliedSnapshots.Last());
        Assert.Equal(illumination.Original, Assert.Single(illumination.RestoredStates));
    }

    [Fact]
    public async Task WhiteCapture_AutoCalibrationStartsFromSelectedSourceAndRestoresDeviceParameters()
    {
        var previous = WhiteTestProfile(99, 10, 2000).Parameters;
        var parameters = new StatusParameters(loadedSnapshot: previous);
        var illumination = new WhiteCaptureIllumination();
        var calibration = new StatusAutoCalibration(autoBlackResult: WhiteTestProfile(70, 200, 4000).Parameters);
        var draft = WhiteTestDraft(includeComposite: true).SwitchWhiteLightSource(WhiteLightSource.RgbComposite);
        var session = new StatusSession(true);
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, draft, attachRuntimeBindings: true, connected: true,
            parameters: parameters, illumination: illumination, autoCalibration: calibration,
            scanSession: session, sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true));
        await harness.SelectChannelAsync("White");

        await harness.ViewModel.AutoBlackAdjustCommand.ExecuteAsync(null);
        await harness.FlushAsync();

        Assert.Equal((ushort)60, calibration.LastAutoBlackInput!.ExposureTicks);
        Assert.NotNull(harness.ViewModel.PendingCalibrationResult);
        Assert.Equal((ushort)60, harness.ViewModel.PendingCalibrationResult.OriginalSnapshot.ExposureTicks);
        Assert.Equal(previous, parameters.AppliedSnapshots.Last());
        Assert.Equal(illumination.Original, illumination.RestoredStates.Last());
    }

    [Fact]
    public async Task WhiteCapture_ExportUsesCapturedSourceAndExposureAfterSelectingAnotherSource()
    {
        var draft = WhiteTestDraft(includeComposite: true);
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, draft, attachRuntimeBindings: true);
        await ((IScanCalibrationProfileRepository)harness.Repository).SetWhiteChannelSettingsAsync(draft.WhiteChannelSettings, CancellationToken.None);
        var capturedParameters = WhiteTestProfile(61, 200, 4000).Parameters;
        var capture = new ScanPassCapture(1, 4, true, 1, 0, [1])
        {
            Provenance = new ScanPassCaptureProvenance(Guid.NewGuid(), "White", null, null, capturedParameters,
                1, 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1) { WhiteLightSource = WhiteLightSource.RgbComposite }
        };
        var result = new ScanWorkflowResult(1, [capture], 0, 0, 999, ScanDebugConstants.MinSysClockKhz);
        var assignment = (ScanChannelAssignment)typeof(ScanDebugViewModel)
            .GetMethod("BuildCapturedWorkflowChannelAssignment", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(harness.ViewModel, [result])!;

        var exported = (IReadOnlyDictionary<string, ScanChannelCalibrationProfile>)typeof(ScanDebugViewModel)
            .GetMethod("BuildCapturedDngProfiles", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(harness.ViewModel, [result, assignment])!;

        Assert.Equal(WhiteLightSource.DedicatedLed, harness.ViewModel.SelectedWhiteLightSource);
        Assert.Equal("White", assignment.Roles[4]);
        Assert.Equal("Unused", assignment.Roles[0]);
        Assert.Equal((ushort)200, exported["White"].BlackLevel);
        Assert.Equal((ushort)4000, exported["White"].WhiteLevel);
        Assert.Equal(capturedParameters, exported["White"].Parameters);
    }

    [Fact]
    public async Task WhiteCapture_SingleCaptureExportKeepsSourceParametersAndReferencesAfterSourceSwitch()
    {
        var draft = WhiteTestDraft(includeComposite: true).SwitchWhiteLightSource(WhiteLightSource.RgbComposite);
        var session = new StatusSession(isConnected: true,
            startScan: (rows, _, _) => Task.FromResult(new ScanStartResult(true, "sampled", new byte[rows * ScanDebugConstants.BytesPerLine])));
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, draft, attachRuntimeBindings: true, connected: true,
            scanSession: session, sessionCoordinator: new StatusSessionCoordinator(session, useConnectedSession: true),
            illumination: new WhiteCaptureIllumination());
        await harness.SelectChannelAsync("White");
        foreach (var channel in harness.ViewModel.AcquisitionChannels)
            channel.IsSelected = string.Equals(channel.Role, "White", StringComparison.OrdinalIgnoreCase);

        await InvokeRunSingleScanAsync(harness.ViewModel, 1, CancellationToken.None);
        await harness.FlushAsync();
        await harness.ViewModel.ChangeWhiteLightSourceAsync(WhiteLightSource.DedicatedLed);
        await harness.FlushAsync();

        Assert.Equal(WhiteLightSource.DedicatedLed, harness.ViewModel.SelectedWhiteLightSource);
        Assert.Equal("30", harness.ViewModel.ExposureTicks);
        var evidence = typeof(ScanDebugViewModel).GetField("_displayedCaptureEvidence", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(harness.ViewModel);
        Assert.NotNull(evidence);
        Assert.Equal(WhiteLightSource.RgbComposite,
            Assert.IsType<WhiteLightSource>(evidence.GetType().GetProperty("WhiteLightSource")!.GetValue(evidence)));
        var exported = ((string Role, ushort ExposureTicks, uint SysClockKhz, ScanChannelCalibrationProfile? Profile))typeof(ScanDebugViewModel)
            .GetMethod("GetMonochromeDngExportMetadata", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(harness.ViewModel, null)!;
        Assert.Equal("White", exported.Role);
        Assert.Equal((ushort)60, exported.ExposureTicks);
        Assert.Equal(ScanDebugConstants.MinSysClockKhz, exported.SysClockKhz);
        Assert.Equal((ushort)200, exported.Profile!.BlackLevel);
        Assert.Equal((ushort)4000, exported.Profile.WhiteLevel);

        await harness.SelectChannelAsync("Red");
        foreach (var channel in harness.ViewModel.AcquisitionChannels)
            channel.IsSelected = string.Equals(channel.Role, "Red", StringComparison.OrdinalIgnoreCase);
        await InvokeRunSingleScanAsync(harness.ViewModel, 1, CancellationToken.None);
        await harness.FlushAsync();
        var nextEvidence = typeof(ScanDebugViewModel).GetField("_displayedCaptureEvidence", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(harness.ViewModel)!;
        Assert.Null(nextEvidence.GetType().GetProperty("WhiteLightSource")!.GetValue(nextEvidence));
        Assert.Null(nextEvidence.GetType().GetProperty("SubmittedWhiteProfile")!.GetValue(nextEvidence));
        var nextExport = ((string Role, ushort ExposureTicks, uint SysClockKhz, ScanChannelCalibrationProfile? Profile))typeof(ScanDebugViewModel)
            .GetMethod("GetMonochromeDngExportMetadata", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(harness.ViewModel, null)!;
        Assert.Equal("Red", nextExport.Role);
        Assert.NotEqual(exported.Profile, nextExport.Profile);
    }

    [Fact]
    public async Task WhiteCapture_WorkflowExportKeepsReferencesAfterRepositoryReplacement()
    {
        var draft = WhiteTestDraft(includeComposite: true).SwitchWhiteLightSource(WhiteLightSource.RgbComposite);
        var capturedParameters = WhiteTestProfile(61, 200, 4000).Parameters;
        var capture = new ScanPassCapture(1, 1, true, 1, 0, [1])
        {
            Provenance = new ScanPassCaptureProvenance(Guid.NewGuid(), "White", null, null, capturedParameters,
                1, 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1) { WhiteLightSource = WhiteLightSource.RgbComposite }
        };
        var result = new ScanWorkflowResult(1, [capture], 0, 0, 999, ScanDebugConstants.MinSysClockKhz);
        var workflow = new StatusWorkflow((_, _, _) => Task.FromResult(result));
        var harness = await StatusHarness.CreateAsync(EmptyProfiles, draft, attachRuntimeBindings: true, connected: true, workflow: workflow);
        var repository = (IScanCalibrationProfileRepository)harness.Repository;
        await repository.SetWhiteChannelSettingsAsync(draft.WhiteChannelSettings, CancellationToken.None);
        var request = new ScanWorkflowRequest(1, false, [1, 2, 3, 4], ["Unused", "White", "Unused", "Unused"],
            [capturedParameters, capturedParameters, capturedParameters, capturedParameters], 0, 0, true, false,
            capturedParameters.ExposureTicks, capturedParameters.SysClockKhz, draft.AcquisitionSettings);

        await InvokeRunWorkflowScanAsync(harness.ViewModel, request);
        await harness.FlushAsync();
        await repository.SetWhiteChannelSettingsAsync(draft.WhiteChannelSettings.WithActiveProfile(WhiteTestProfile(90, 700, 8000)), CancellationToken.None);
        var assignment = (ScanChannelAssignment)typeof(ScanDebugViewModel)
            .GetMethod("BuildCapturedWorkflowChannelAssignment", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(harness.ViewModel, [result])!;
        var exported = (IReadOnlyDictionary<string, ScanChannelCalibrationProfile>)typeof(ScanDebugViewModel)
            .GetMethod("BuildCapturedDngProfiles", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(harness.ViewModel, [result, assignment])!;

        Assert.Equal((ushort)700, repository.Snapshot.WhiteChannelSettings!.RgbComposite!.Profile!.BlackLevel);
        Assert.Equal((ushort)200, exported["White"].BlackLevel);
        Assert.Equal((ushort)4000, exported["White"].WhiteLevel);
        Assert.Equal(capturedParameters, exported["White"].Parameters);
    }

    private static Task<int> InvokeWhiteCaptureAsync(ScanDebugViewModel viewModel, IScanSessionService session, Func<Task<int>> action)
        => (Task<int>)typeof(ScanDebugViewModel).GetMethod("RunWithWhiteCaptureSourceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(int)).Invoke(viewModel, [session, "White", CancellationToken.None, action])!;

    private sealed class WhiteCaptureIllumination : IScanIlluminationService
    {
        public ScanIlluminationState Original { get; } = new(1, 2, 3, 4, 2, 0, 0, 2, 2, 2, 2);
        public List<ScanIlluminationState> AppliedStates { get; } = [];
        public List<ScanIlluminationState> RestoredStates { get; } = [];
        public int ReadCount { get; private set; }
        public int TurnOffCount { get; private set; }

        public Task<ScanIlluminationState> GetStateAsync(IScanSessionService session, CancellationToken ct)
        {
            ReadCount++;
            return Task.FromResult(Original);
        }

        public Task ApplyStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
        {
            AppliedStates.Add(state);
            return Task.CompletedTask;
        }

        public Task ApplyStateWithSafeTransitionAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
            => ApplyStateAsync(session, state, ct);

        public Task ApplySingleChannelAsync(IScanSessionService session, ScanFilmAcquisitionSettings settings, byte ledIndex, CancellationToken ct)
            => throw new NotSupportedException();

        public Task TurnOffAsync(IScanSessionService session, CancellationToken ct)
        {
            TurnOffCount++;
            return Task.CompletedTask;
        }

        public Task RestoreStateAsync(IScanSessionService session, ScanIlluminationState state, CancellationToken ct)
        {
            RestoredStates.Add(state);
            return Task.CompletedTask;
        }
    }
}
