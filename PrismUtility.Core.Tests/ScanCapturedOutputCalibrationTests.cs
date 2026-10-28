using System.Reflection;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanSavedProfileHandoffBehaviorTests
{
    [Theory]
    [InlineData(WhiteLightSource.DedicatedLed)]
    [InlineData(WhiteLightSource.RgbComposite)]
    public async Task ScanOutput_CapturedReferencesSurviveRepositoryChangesAndAnotherLoadedConfiguration(WhiteLightSource source)
    {
        var harness = await CreateWhiteScanHarnessAsync(source, whiteOnly: source == WhiteLightSource.DedicatedLed);
        var vm = harness.ViewModel;
        vm.IsPreviewEnabled = false;
        if (source == WhiteLightSource.DedicatedLed)
            await harness.DeviceSettings.SetSettingsAsync(harness.DeviceSettings.Settings with
            {
                Channel1Role = "White", Channel2Role = "Green", Channel3Role = "Red", Channel4Role = "Blue"
            });

        var original = vm.LoadedFilmProfileEditRequest!.Profile;
        var template = original.ChannelProfiles["White"];
        var captured = template with { BlackLevel = 111, WhiteLevel = 3100 };
        var replacement = template with
        {
            Parameters = template.Parameters with { ExposureTicks = (ushort)(template.Parameters.ExposureTicks + 1) },
            BlackLevel = 777, WhiteLevel = 3900
        };
        var white = original.AcquisitionSettings!.WhiteChannelSettings!
            .WithActiveProfile(captured)
            .WithSourceSettings(source == WhiteLightSource.DedicatedLed ? WhiteLightSource.RgbComposite : WhiteLightSource.DedicatedLed,
                new(replacement));
        original = original with
        {
            ChannelProfiles = original.ChannelProfiles.ToDictionary(pair => pair.Key, _ => captured),
            AcquisitionSettings = original.AcquisitionSettings with { WhiteChannelSettings = white }
        };
        harness.Workspace.MarkExported(original);
        await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);

        var session = DispatchProxy.Create<IScanSessionService, CapturedOutputSession>();
        var coordinator = DispatchProxy.Create<IScanWorkflowSessionCoordinator, CapturedOutputCoordinator>();
        var coordinatorProxy = (CapturedOutputCoordinator)(object)coordinator;
        coordinatorProxy.Session = session;
        // Validation yields after freezing the request. A repository change during that wait
        // must not replace the references used by the already prepared capture.
        coordinatorProxy.BeforeValidationReturns = () => harness.Repository.ReplaceAsync(
            new(original.ChannelProfiles.ToDictionary(pair => pair.Key, _ => replacement), "White", white), CancellationToken.None);
        var workflow = DispatchProxy.Create<IScanWorkflowService, CapturedOutputWorkflow>();
        var workflowProxy = (CapturedOutputWorkflow)(object)workflow;
        var images = DispatchProxy.Create<IScanChannelImageService, CapturedOutputImages>();
        var imageProxy = (CapturedOutputImages)(object)images;
        SetScanPrivateField(vm, "_scanSessionCoordinator", coordinator);
        SetScanPrivateField(vm, "_workflow", workflow);
        SetScanPrivateField(vm, "_channelImages", images);
        vm.IsDevicesPresent = true;
        vm.IsConnected = true;

        await vm.StartScanCommand.ExecuteAsync(null);

        Assert.True(vm.IsOutputAvailable, vm.StatusText);
        Assert.NotNull(workflowProxy.Request);
        var whiteIndex = Array.IndexOf(workflowProxy.Request.PassChannelRoles, "White");
        Assert.Equal(captured.Parameters, workflowProxy.Request.PassParameterProfiles[whiteIndex]);
        Assert.Equal(source, workflowProxy.Request.AcquisitionSettings!.WhiteChannelSettings!.Source);

        harness.Workspace.MarkExported(original with
        {
            ProfileName = "Another loaded configuration",
            ChannelProfiles = original.ChannelProfiles.ToDictionary(pair => pair.Key, _ => replacement),
            AcquisitionSettings = original.AcquisitionSettings with
            {
                WhiteChannelSettings = white with
                {
                    Source = source == WhiteLightSource.DedicatedLed ? WhiteLightSource.RgbComposite : WhiteLightSource.DedicatedLed
                }
            }
        });
        await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);
        Assert.Equal(replacement, harness.Repository.Snapshot.Profiles["White"]);

        // Invoke the post-picker export path; the recorder does not need a native folder.
        await (Task)typeof(ScanViewModel).GetMethod("ExportCapturedDngChannelsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(vm, [null, CancellationToken.None])!;

        var exported = Assert.IsAssignableFrom<IReadOnlyDictionary<string, ScanChannelCalibrationProfile>>(imageProxy.ExportedProfiles);
        Assert.Equal(captured, exported["White"]);
        Assert.Equal(source, imageProxy.ExportedResult!.Passes.Single(pass => pass.Provenance!.ChannelRole == "White").Provenance!.WhiteLightSource);
        Assert.All(exported.Values, profile => Assert.Equal(captured, profile));
        if (source == WhiteLightSource.RgbComposite)
        {
            vm.SelectedPreviewMode = "RGB Composite";
            vm.IsPreviewEnabled = true;
            Assert.Same(exported, imageProxy.PreviewProfiles);
        }
        await vm.CleanupAsync();
    }

    [Fact]
    public async Task ScanOutput_MissingActiveWhiteProfileDoesNotBorrowOtherSourceReferences()
    {
        var harness = await CreateWhiteScanHarnessAsync(WhiteLightSource.RgbComposite, whiteOnly: true);
        var acquisition = harness.ViewModel.LoadedFilmProfileEditRequest!.Profile.AcquisitionSettings!;
        SetScanPrivateField(harness.ViewModel, "_selectedConfigAcquisitionSettings", acquisition with
        {
            WhiteChannelSettings = acquisition.WhiteChannelSettings! with { RgbComposite = new() }
        });
        object?[] args = [null, string.Empty];
        var built = (bool)typeof(ScanViewModel).GetMethod("TryBuildWorkflowRequest", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(harness.ViewModel, args)!;

        Assert.False(built);
        Assert.True(harness.ViewModel.NeedsConfigProfile);
        await harness.ViewModel.CleanupAsync();
    }

    public class CapturedOutputSession : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method!.Name == "get_SingleTransferMaxRows" ? 8192
                : throw new InvalidOperationException($"Unexpected session operation: {method.Name}");
    }

    public class CapturedOutputCoordinator : DispatchProxy
    {
        public IScanSessionService Session { get; set; } = null!;
        public Func<Task>? BeforeValidationReturns { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "UseConnectedSessionAsync")
                return ValidateAsync((Func<IScanSessionService, CancellationToken, Task<int>>)args![0]!, (CancellationToken)args[1]!);
            if (method.Name == "RunConnectedSessionStateAsync")
                return ((Func<IScanSessionService, CancellationToken, Task<ScanWorkflowResult>>)args![1]!)(Session, (CancellationToken)args[2]!);
            throw new InvalidOperationException($"Unexpected coordinator operation: {method.Name}");
        }
        private async Task<int> ValidateAsync(Func<IScanSessionService, CancellationToken, Task<int>> action, CancellationToken token)
        {
            if (BeforeValidationReturns is not null)
                await BeforeValidationReturns();
            return await action(Session, token);
        }
    }

    public class CapturedOutputWorkflow : DispatchProxy
    {
        public ScanWorkflowRequest? Request { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Request = (ScanWorkflowRequest)args![1]!;
            var passes = Request.PassChannelRoles.Select((role, slot) => (role, slot))
                .Where(item => item.role != "Unused").Select((item, index) => new ScanPassCapture(index + 1, (byte)item.slot, true, 1, 0, [])
                {
                    Provenance = new(Guid.NewGuid(), item.role, null, null, Request.PassParameterProfiles[item.slot],
                        1, 1, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1)
                    {
                        WhiteLightSource = item.role == "White" ? Request.AcquisitionSettings!.WhiteChannelSettings!.Source : null
                    }
                }).ToArray();
            return Task.FromResult(new ScanWorkflowResult(1, passes, 0, 100_000, Request.ExposureTicks, Request.SysClockKhz));
        }
    }

    public class CapturedOutputImages : DispatchProxy
    {
        public IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? ExportedProfiles { get; private set; }
        public IReadOnlyDictionary<string, ScanChannelCalibrationProfile>? PreviewProfiles { get; private set; }
        public ScanWorkflowResult? ExportedResult { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "ExportDngChannelsAsync")
            {
                ExportedResult = (ScanWorkflowResult)args![1]!;
                ExportedProfiles = (IReadOnlyDictionary<string, ScanChannelCalibrationProfile>?)args[5];
                return Task.FromResult(new ScanDngExportResult(ScanChannelAlignmentStatus.Disabled, null, null));
            }
            if (method.Name == "TryBuildRgbComposite")
            {
                PreviewProfiles = (IReadOnlyDictionary<string, ScanChannelCalibrationProfile>?)args![7];
                args[5] = null;
                args[6] = string.Empty;
                return false;
            }
            throw new InvalidOperationException($"Unexpected image operation: {method.Name}");
        }
    }
}
