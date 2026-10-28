using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media.Imaging;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanSavedProfileHandoffBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScanPreview_SameModeNewRunClearsPreviousImageAndInterruptedRunCannotRestoreIt(bool canceled)
    {
        var harness = await CreateWhiteScanHarnessAsync(WhiteLightSource.RgbComposite, whiteOnly: true);
        var vm = harness.ViewModel;
        var session = DispatchProxy.Create<IScanSessionService, CapturedOutputSession>();
        var coordinator = DispatchProxy.Create<IScanWorkflowSessionCoordinator, CapturedOutputCoordinator>();
        ((CapturedOutputCoordinator)(object)coordinator).Session = session;
        var workflow = new PreviewLifecycleWorkflow(DispatchProxy.Create<IScanWorkflowService, CapturedOutputWorkflow>());
        var images = DispatchProxy.Create<IScanChannelImageService, PreviewLifecycleImages>();
        var imageService = (PreviewLifecycleImages)(object)images;
        SetScanPrivateField(vm, "_scanSessionCoordinator", coordinator);
        SetScanPrivateField(vm, "_workflow", workflow);
        SetScanPrivateField(vm, "_channelImages", images);
        vm.IsDevicesPresent = true;
        vm.IsConnected = true;
        vm.IsPreviewEnabled = true;
        Task? secondRun = null;

        try
        {
            var selectedMode = vm.SelectedPreviewMode;
            Assert.True(vm.StartScanCommand.CanExecute(null));
            await vm.StartScanCommand.ExecuteAsync(null);

            Assert.True(vm.IsOutputAvailable, vm.StatusText);
            Assert.Same(imageService.Image, vm.PreviewImage);
            Assert.False(vm.IsPreviewPlaceholderVisible);
            Assert.Equal(1, imageService.RenderCount);

            secondRun = vm.StartScanCommand.ExecuteAsync(null);
            await workflow.SecondRunEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(vm.IsRunning);
            Assert.Equal(selectedMode, vm.SelectedPreviewMode);
            Assert.Null(vm.PreviewImage);
            Assert.True(vm.IsPreviewPlaceholderVisible);
            Assert.Equal("Scanning. The preview will appear when the scan finishes.", vm.PreviewPlaceholderText);
            Assert.False(vm.IsOutputAvailable);

            workflow.SecondRunCompletion.SetException(canceled
                ? new OperationCanceledException("Canceled before another frame")
                : new InvalidOperationException("Failed before another frame"));
            await secondRun;

            Assert.False(vm.IsRunning);
            Assert.Null(vm.PreviewImage);
            Assert.True(vm.IsPreviewPlaceholderVisible);
            Assert.False(vm.IsOutputAvailable);
            Assert.DoesNotContain("Scanning.", vm.PreviewPlaceholderText, StringComparison.Ordinal);
            // Revisiting preview settings must not render the discarded result again.
            vm.IsPreviewEnabled = false;
            vm.IsPreviewEnabled = true;
            Assert.Null(vm.PreviewImage);
            Assert.True(vm.IsPreviewPlaceholderVisible);
            Assert.Equal(1, imageService.RenderCount);
        }
        finally
        {
            workflow.SecondRunCompletion.TrySetCanceled();
            if (secondRun is not null)
                await secondRun;
            await vm.CleanupAsync();
        }
    }

    private sealed class PreviewLifecycleWorkflow(IScanWorkflowService firstRun) : IScanWorkflowService
    {
        private int _runCount;
        public TaskCompletionSource SecondRunEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ScanWorkflowResult> SecondRunCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ScanWorkflowResult> ExecuteAsync(IScanSessionService session, ScanWorkflowRequest request,
            CancellationToken ct, Action<ScanWorkflowProgress>? onProgress = null, Action<string>? onStatus = null,
            Action<string>? onDiagnostic = null, Action<int, int>? onByteProgress = null,
            ScanWorkflowRowsAvailableHandler? onRowsAvailable = null)
        {
            if (++_runCount == 1)
                return firstRun.ExecuteAsync(session, request, ct);
            SecondRunEntered.SetResult();
            return SecondRunCompletion.Task;
        }
    }

    public class PreviewLifecycleImages : DispatchProxy
    {
        // Only reference identity is needed; no pixel or other native WinUI API is used.
        public WriteableBitmap Image { get; } = (WriteableBitmap)RuntimeHelpers.GetUninitializedObject(typeof(WriteableBitmap));
        public int RenderCount { get; private set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != "TryBuildRawPreview")
                throw new InvalidOperationException($"Unexpected image operation: {method.Name}");
            RenderCount++;
            args![5] = Image;
            args[6] = string.Empty;
            return true;
        }
    }
}
