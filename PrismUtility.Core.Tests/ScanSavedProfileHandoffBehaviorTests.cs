using System.Reflection;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Services;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
[Trait("Category", "Lifecycle")]
public sealed partial class ScanSavedProfileHandoffBehaviorTests
{
    public ScanSavedProfileHandoffBehaviorTests()
        => AppContext.SetSwitch("PRISM.Utility.UseResourceFallbacks", true);

    [Fact]
    public async Task SavedConfiguration_LoadsAcquisitionAndExplicitLinePitchWithoutStartingHardware()
    {
        var harness = CreateHarness();

        await harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);

        var acquisition = harness.Document.AcquisitionSettings!;
        Assert.True(harness.ViewModel.IsConfigProfileLoaded);
        Assert.Equal(harness.Document.ProfileName, harness.ViewModel.SelectedConfigProfileName);
        Assert.Equal(acquisition.Rows.ToString(), harness.ViewModel.SelectedRows);
        var transportMotor = harness.DeviceSettings.Settings.Normalize().DefaultScanMotorId!.Value;
        Assert.Equal($"Motor{transportMotor + 1}", harness.ViewModel.SelectedScanMotor);
        Assert.Equal("Reverse", harness.ViewModel.SelectedStartingDirection);
        Assert.True(harness.ViewModel.IsWarmUpEnabled);
        Assert.False(harness.ViewModel.IsAlternateMotorDirectionEnabled);
        Assert.True(ScanMotorDistanceText.TryParseMillimeters(
            harness.ViewModel.MotorDistancePerLineValue, harness.ViewModel.MotorDistancePerLineUnit,
            harness.DeviceSettings.Settings.GetMotorSettings(transportMotor), out var distanceMillimeters));
        Assert.Equal(acquisition.TargetLinePitchMicrometers!.Value / 1_000.0, distanceMillimeters, 9);
        Assert.False(harness.ViewModel.StartScanCommand.CanExecute(null));
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task LeavingBeforeRepositoryReplacementPreventsHiddenConfigurationChanges()
    {
        var harness = CreateHarness();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.ColorSettings.NextInitialization = release.Task;
        var load = harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
        Assert.False(harness.ViewModel.IsSetupEditingEnabled);

        harness.ViewModel.Deactivate();
        var statusAfterLeaving = harness.ViewModel.StatusText;
        release.SetResult();
        await load;

        Assert.False(harness.ViewModel.IsConfigProfileLoaded);
        Assert.Equal(0, harness.Repository.ReplaceCount);
        Assert.Equal(statusAfterLeaving, harness.ViewModel.StatusText);
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task ReturningAndLoadingNewSaveCannotBeOverwrittenByPreviousActivation()
    {
        var harness = CreateHarness();
        var releaseOld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.ColorSettings.NextInitialization = releaseOld.Task;
        var oldLoad = harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
        harness.ViewModel.Deactivate();

        harness.Workspace.MarkExported(harness.Document with
        {
            ProfileName = "New saved configuration",
            AcquisitionSettings = harness.Document.AcquisitionSettings! with { Rows = 1_024 }
        });
        harness.ViewModel.Activate();
        await harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
        var currentStatus = harness.ViewModel.StatusText;
        releaseOld.SetResult();
        await oldLoad;

        Assert.Equal("New saved configuration", harness.ViewModel.SelectedConfigProfileName);
        Assert.Equal("1024", harness.ViewModel.SelectedRows);
        Assert.Equal(1, harness.Repository.ReplaceCount);
        Assert.Equal(currentStatus, harness.ViewModel.StatusText);
        Assert.True(harness.ViewModel.IsSetupEditingEnabled);
        await harness.ViewModel.CleanupAsync();
    }

    [Fact]
    public async Task StorageAlreadyInProgressMayFinishButCannotProjectAfterLeaving()
    {
        var harness = CreateHarness(holdRepositoryWrite: true);
        var load = harness.ViewModel.UseSavedFilmProfileCommand.ExecuteAsync(null);
        await harness.RepositoryGate!.ReplaceEntered.Task;

        harness.ViewModel.Deactivate();
        var statusAfterLeaving = harness.ViewModel.StatusText;
        harness.RepositoryGate.ReleaseReplace.SetResult();
        await load;

        Assert.Equal(1, harness.Repository.ReplaceCount);
        Assert.False(harness.ViewModel.IsConfigProfileLoaded);
        Assert.Equal(statusAfterLeaving, harness.ViewModel.StatusText);
        await harness.ViewModel.CleanupAsync();
    }

    private static Harness CreateHarness(bool holdRepositoryWrite = false, IScanOutputLocationService? outputLocationService = null)
    {
        var document = new ScanFilmProfileDocumentService().Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "FilmProfile", "full-v6.json"))).Document!;
        var repository = new RecordingCalibrationProfileRepository();
        var workspace = new ScanFilmProfileWorkspace(repository, new ScanFilmProfileDocumentService());
        workspace.MarkExported(document);
        var manager = DispatchProxy.Create<IScannerDeviceSessionManager, OfflineSessionManager>();
        var usb = new UsbUsageCoordinator();
        var settings = new ScanDeviceSettingsService(new Set003RecordingLocalSettings());
        var color = new DeferredColorSettings();
        HeldRepository? gate = holdRepositoryWrite ? new HeldRepository(repository) : null;
        var viewModel = new ScanViewModel(manager, new ScanWorkflowSessionCoordinator(usb, manager),
            null!, new RecordingTransferSettingsService(), null!, new RecordingDebugOutputMirrorService(), null!,
            settings, color, gate ?? (IScanCalibrationProfileRepository)repository, null!, workspace, usb,
            new RecordingUiDispatcher(autoDrain: true), outputLocationService);
        return new(viewModel, repository, workspace, document, settings, color, gate);
    }

    private sealed record Harness(ScanViewModel ViewModel, RecordingCalibrationProfileRepository Repository,
        ScanFilmProfileWorkspace Workspace, ScanFilmParameterProfileSet Document,
        ScanDeviceSettingsService DeviceSettings, DeferredColorSettings ColorSettings, HeldRepository? RepositoryGate);

    // Configuration loading must never attempt a hardware operation in these tests.
    public class OfflineSessionManager : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod!.Name switch
            {
                "get_Snapshot" => ScannerDeviceSessionSnapshot.Disconnected(DateTimeOffset.UnixEpoch),
                "get_Targets" => new ScanTargetState(false, null, null),
                "TryGetConnectedSession" => null,
                "add_SnapshotChanged" or "remove_SnapshotChanged" or "add_TargetsChanged" or "remove_TargetsChanged" => null,
                _ => throw new InvalidOperationException($"Unexpected hardware operation: {targetMethod.Name}")
            };
    }

    private sealed class DeferredColorSettings : IScanColorManagementSettingsService
    {
        public Task? NextInitialization { get; set; }
        public ScanColorManagementOptions DefaultSettings => ScanColorManagementOptions.CreateDefault();
        public ScanColorManagementOptions Settings { get; private set; } = ScanColorManagementOptions.CreateDefault();

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var pending = NextInitialization;
            NextInitialization = null;
            return pending ?? Task.CompletedTask;
        }

        public Task SetSettingsAsync(ScanColorManagementOptions settings, CancellationToken cancellationToken = default)
        {
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class HeldRepository(RecordingCalibrationProfileRepository inner) : IScanCalibrationProfileRepository
    {
        public TaskCompletionSource ReplaceEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseReplace { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ScanCalibrationProfileRepositorySnapshot Snapshot => inner.Snapshot;
        public Task InitializeAsync(CancellationToken ct) => inner.InitializeAsync(ct);
        public Task<ScanCalibrationProfileRepositorySnapshot> ReadAsync(CancellationToken ct) => inner.ReadAsync(ct);
        public bool TryGetProfile(string role, out ScanChannelCalibrationProfile profile) => inner.TryGetProfile(role, out profile!);
        public Task SaveProfileAsync(string role, ScanChannelCalibrationProfile profile, CancellationToken ct) => inner.SaveProfileAsync(role, profile, ct);
        public Task<bool> ClearProfileAsync(string role, CancellationToken ct) => inner.ClearProfileAsync(role, ct);
        public Task<string?> GetSelectedChannelAsync(CancellationToken ct) => inner.GetSelectedChannelAsync(ct);
        public Task SetSelectedChannelAsync(string? role, CancellationToken ct) => inner.SetSelectedChannelAsync(role, ct);

        public async Task ReplaceAsync(ScanCalibrationProfileRepositorySnapshot snapshot, CancellationToken ct)
        {
            ReplaceEntered.SetResult();
            await ReleaseReplace.Task;
            await inner.ReplaceAsync(snapshot, CancellationToken.None);
        }
    }
}
