using System.Reflection;
using Microsoft.UI.Xaml.Controls;
using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Core.Models;
using PRISM_Utility.ViewModels;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed partial class ScanSavedProfileHandoffBehaviorTests
{
    [Fact]
    public async Task SessionRestore_WithoutLoadedBaselineCannotOverwriteCurrentInputs()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            vm.SelectedRows = "4096";
            Assert.False(vm.RestoreLoadedSettingsCommand.CanExecute(null));

            vm.RestoreLoadedSettingsCommand.Execute(null);

            Assert.Equal("4096", vm.SelectedRows);
            Assert.False(vm.IsConfigProfileLoaded);
            Assert.Equal(0, harness.Repository.ReplaceCount);
        }
        finally { await vm.CleanupAsync(); }
    }

    [Fact]
    public async Task SessionRestore_ReturnsAllPageInputsToLoadedValuesWithoutSavingTheProfile()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);
            var baseline = CaptureSessionInputs(vm);
            var saved = harness.Workspace.LastSavedDocument;
            var editor = harness.Workspace.Snapshot;
            var library = harness.Repository.Snapshot;
            var replaceCount = harness.Repository.ReplaceCount;
            var changes = new List<string?>();
            vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            Assert.False(vm.HasSessionChanges);

            vm.SelectedRows = "4096";
            vm.MotorDistancePerLineValue = "0.02";
            vm.SelectedStartingDirection = vm.SelectedStartingDirection == "Forward" ? "Reverse" : "Forward";
            vm.IsWarmUpEnabled = !vm.IsWarmUpEnabled;
            vm.IsAlternateMotorDirectionEnabled = !vm.IsAlternateMotorDirectionEnabled;
            vm.IsColorManagementEnabled = !vm.IsColorManagementEnabled;
            vm.RedWavelengthNm = "710";
            vm.GreenWavelengthNm = "540";
            vm.BlueWavelengthNm = "460";
            vm.OutputGamma = "2.4";
            vm.ManualWhitePointColorTemperatureK = "5500";
            vm.IsPreviewEnabled = !vm.IsPreviewEnabled;
            vm.SelectedPreviewMode = "Raw Channel 2";
            vm.SelectedAlignmentMode = vm.AlignmentModeOptions.First(mode => mode != vm.SelectedAlignmentMode);

            Assert.True(vm.HasSessionChanges);
            Assert.True(vm.RestoreLoadedSettingsCommand.CanExecute(null));
            Assert.Contains(nameof(vm.HasSessionChanges), changes);
            Assert.False(string.IsNullOrWhiteSpace(vm.SessionChangesText));

            vm.RestoreLoadedSettingsCommand.Execute(null);

            Assert.Equal(baseline, CaptureSessionInputs(vm));
            Assert.False(vm.HasSessionChanges);
            Assert.False(vm.RestoreLoadedSettingsCommand.CanExecute(null));
            Assert.Equal(Newtonsoft.Json.JsonConvert.SerializeObject(saved),
                Newtonsoft.Json.JsonConvert.SerializeObject(harness.Workspace.LastSavedDocument));
            Assert.Same(editor, harness.Workspace.Snapshot);
            Assert.Same(library, harness.Repository.Snapshot);
            Assert.Equal(replaceCount, harness.Repository.ReplaceCount);
            Assert.Equal(0, harness.Repository.SaveProfileCount);
            Assert.Equal(0, harness.Repository.ClearProfileCount);
        }
        finally { await vm.CleanupAsync(); }
    }

    [Theory]
    [InlineData(nameof(ScanViewModel.IsRunning))]
    [InlineData(nameof(ScanViewModel.IsOutputOperationRunning))]
    [InlineData(nameof(ScanViewModel.IsConnecting))]
    public async Task SessionRestore_BusyCommandCannotChangeParametersEvenWhenInvokedDirectly(string busyProperty)
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);
            var baselineRows = vm.SelectedRows;
            vm.SelectedRows = "4096";
            var enabledChanges = 0;
            vm.RestoreLoadedSettingsCommand.CanExecuteChanged += (_, _) => enabledChanges++;
            typeof(ScanViewModel).GetProperty(busyProperty)!.SetValue(vm, true);

            Assert.False(vm.RestoreLoadedSettingsCommand.CanExecute(null));
            vm.RestoreLoadedSettingsCommand.Execute(null);
            Assert.Equal("4096", vm.SelectedRows);

            typeof(ScanViewModel).GetProperty(busyProperty)!.SetValue(vm, false);
            Assert.True(vm.RestoreLoadedSettingsCommand.CanExecute(null));
            Assert.True(enabledChanges >= 2);
            vm.RestoreLoadedSettingsCommand.Execute(null);
            Assert.Equal(baselineRows, vm.SelectedRows);
        }
        finally { await vm.CleanupAsync(); }
    }

    [Fact]
    public async Task SessionRestore_UsesLoadedBaselineUntilANewerSavedConfigurationIsExplicitlyLoaded()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);
            var originalRows = vm.SelectedRows;
            harness.Workspace.MarkExported(harness.Document with
            {
                AcquisitionSettings = harness.Document.AcquisitionSettings! with { Rows = 1024 }
            });
            vm.SelectedRows = "4096";
            vm.Deactivate();
            vm.Activate();

            vm.RestoreLoadedSettingsCommand.Execute(null);

            Assert.Equal(originalRows, vm.SelectedRows);
            Assert.Equal(1024, harness.Workspace.LastSavedDocument!.AcquisitionSettings!.Rows);
            Assert.True(vm.ShowSavedFilmProfileAction);

            await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);
            Assert.False(vm.HasSessionChanges);
            vm.SelectedRows = "8192";
            vm.RestoreLoadedSettingsCommand.Execute(null);
            Assert.Equal("1024", vm.SelectedRows);
        }
        finally { await vm.CleanupAsync(); }
    }

    [Fact]
    public async Task SessionRestore_ManuallyReturningToBaselineClearsTheChangeIndicator()
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            await vm.UseSavedFilmProfileCommand.ExecuteAsync(null);
            var originalRows = vm.SelectedRows;
            vm.SelectedRows = "4096";
            Assert.True(vm.HasSessionChanges);

            vm.SelectedRows = originalRows;

            Assert.False(vm.HasSessionChanges);
            Assert.False(vm.RestoreLoadedSettingsCommand.CanExecute(null));
        }
        finally { await vm.CleanupAsync(); }
    }

    [Theory]
    [InlineData("Scan completed.", InfoBarSeverity.Success)]
    [InlineData("Scan canceled.", InfoBarSeverity.Warning)]
    [InlineData("Scanner stopped responding.", InfoBarSeverity.Error)]
    public async Task RuntimeFeedback_TerminalResultRemainsVisibleAfterRunEndsAndReadinessChanges(string message, InfoBarSeverity severity)
    {
        var harness = CreatePreparationHarness();
        var vm = harness.ViewModel;
        try
        {
            var changes = new List<string?>();
            vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            Assert.False(vm.HasRuntimeFeedback);
            vm.IsRunning = true;
            Assert.False(vm.IsPreparationVisible);
            InvokePresentation(vm, "RecordScanCompletion", message, severity);
            Assert.False(vm.HasRuntimeFeedback);

            vm.IsRunning = false;
            vm.StatusText = "A connection is required.";
            vm.SelectedRows = "unfinished rows";
            vm.Deactivate();
            vm.Activate();

            Assert.True(vm.HasRuntimeFeedback);
            Assert.True(vm.IsPreparationVisible);
            Assert.Equal(message, vm.RuntimeFeedbackText);
            Assert.Equal(severity, vm.RuntimeFeedbackSeverity);
            Assert.Contains(nameof(vm.HasRuntimeFeedback), changes);
            Assert.Contains(nameof(vm.RuntimeFeedbackText), changes);
            Assert.True(vm.IsOfflineGuidanceVisible);
            Assert.False(string.IsNullOrWhiteSpace(vm.OfflineGuidanceText));

            vm.CurrentPassText = "Pass 4/4 · Completed";
            vm.CurrentLedText = "White W";
            vm.CurrentDirectionText = "Reverse";
            InvokePresentation(vm, "ResetScanPresentationForNewRun");
            Assert.False(vm.HasRuntimeFeedback);
            Assert.NotEqual("Pass 4/4 · Completed", vm.CurrentPassText);
            Assert.False(string.IsNullOrWhiteSpace(vm.CurrentPassText));
            Assert.True(string.IsNullOrEmpty(vm.CurrentLedText));
            Assert.True(string.IsNullOrEmpty(vm.CurrentDirectionText));
        }
        finally { await vm.CleanupAsync(); }
    }

    private static void InvokePresentation(ScanViewModel vm, string name, params object[] arguments)
        => typeof(ScanViewModel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(vm, arguments);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OutputFeedback_SuccessSurvivesUnrelatedStatusChangesAndOpensOnlyOnExplicitCommand(bool isFile)
    {
        var opener = new RecordingOutputLocationService();
        var harness = CreateHarness(outputLocationService: opener);
        var vm = harness.ViewModel;
        try
        {
            const string path = @"C:\scan-output\completed";
            const string message = "Saved the scan output.";
            var changes = new List<string?>();
            vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            Assert.False(vm.HasOutputFeedback);
            Assert.False(vm.OpenLastOutputFolderCommand.CanExecute(null));
            vm.IsOutputOperationRunning = true;

            InvokePresentation(vm, "RecordOutputSuccess", path, isFile, message);
            vm.IsOutputOperationRunning = false;
            vm.StatusText = "Scanner disconnected.";
            vm.SelectedRows = "unfinished rows";
            vm.Deactivate();
            vm.Activate();

            Assert.True(vm.HasOutputFeedback);
            Assert.Equal(message, vm.OutputFeedbackText);
            Assert.Equal(InfoBarSeverity.Success, vm.OutputFeedbackSeverity);
            Assert.Equal(path, vm.LastOutputPath);
            Assert.Contains(nameof(vm.HasOutputFeedback), changes);
            Assert.Contains(nameof(vm.OutputFeedbackText), changes);
            Assert.True(vm.OpenLastOutputFolderCommand.CanExecute(null));
            Assert.Empty(opener.Calls);

            await vm.OpenLastOutputFolderCommand.ExecuteAsync(null);

            Assert.Equal((path, isFile), Assert.Single(opener.Calls));
            Assert.Equal(message, vm.OutputFeedbackText);
            InvokePresentation(vm, "ResetScanPresentationForNewRun");
            Assert.False(vm.HasOutputFeedback);
            Assert.True(string.IsNullOrEmpty(vm.LastOutputPath));
            Assert.False(vm.OpenLastOutputFolderCommand.CanExecute(null));
        }
        finally { await vm.CleanupAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputFeedback_FolderOpenFailurePreservesThePathAndSuccessfulRetryRestoresCompletion(bool throws)
    {
        var opener = new RecordingOutputLocationService { Result = false, Throw = throws };
        var harness = CreateHarness(outputLocationService: opener);
        var vm = harness.ViewModel;
        try
        {
            const string path = @"C:\scan-output\completed";
            InvokePresentation(vm, "RecordOutputSuccess", path, false, "DNG export completed.");

            await vm.OpenLastOutputFolderCommand.ExecuteAsync(null);

            Assert.True(vm.HasOutputFeedback);
            Assert.False(string.IsNullOrWhiteSpace(vm.OutputFeedbackText));
            Assert.NotEqual("DNG export completed.", vm.OutputFeedbackText);
            Assert.Equal(InfoBarSeverity.Warning, vm.OutputFeedbackSeverity);
            Assert.Equal(path, vm.LastOutputPath);
            Assert.Single(opener.Calls);

            opener.Throw = false;
            opener.Result = true;
            await vm.OpenLastOutputFolderCommand.ExecuteAsync(null);

            Assert.True(vm.HasOutputFeedback);
            Assert.Equal("DNG export completed.", vm.OutputFeedbackText);
            Assert.Equal(InfoBarSeverity.Success, vm.OutputFeedbackSeverity);
            Assert.Equal(path, vm.LastOutputPath);
            Assert.Equal(2, opener.Calls.Count);
        }
        finally { await vm.CleanupAsync(); }
    }

    private sealed class RecordingOutputLocationService : IScanOutputLocationService
    {
        public List<(string Path, bool IsFile)> Calls { get; } = [];
        public bool Result { get; set; } = true;
        public bool Throw { get; set; }
        public Task<bool>? PendingResult { get; init; }

        public Task<bool> OpenAsync(string path, bool isFile)
        {
            Calls.Add((path, isFile));
            return PendingResult ?? (Throw ? Task.FromException<bool>(new IOException("Folder unavailable.")) : Task.FromResult(Result));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputFeedback_LateFolderOpenCompletionCannotRestoreFeedbackFromThePreviousScan(bool result)
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var opener = new RecordingOutputLocationService { PendingResult = release.Task };
        var harness = CreateHarness(outputLocationService: opener);
        var vm = harness.ViewModel;
        Task? opening = null;
        try
        {
            InvokePresentation(vm, "RecordOutputSuccess", @"C:\scan-output\old", false, "Previous scan exported.");
            opening = vm.OpenLastOutputFolderCommand.ExecuteAsync(null);
            Assert.False(opening.IsCompleted);

            InvokePresentation(vm, "ResetScanPresentationForNewRun");
            release.SetResult(result);
            await opening;

            Assert.False(vm.HasOutputFeedback);
            Assert.True(string.IsNullOrEmpty(vm.LastOutputPath));
            Assert.False(vm.OpenLastOutputFolderCommand.CanExecute(null));
        }
        finally
        {
            release.TrySetResult(false);
            if (opening is not null) await opening;
            await vm.CleanupAsync();
        }
    }

    private static object CaptureSessionInputs(ScanViewModel vm) => new
    {
        vm.SelectedRows, vm.MotorDistancePerLineValue, vm.MotorDistancePerLineUnit, vm.MotorIntervalUs,
        vm.SelectedStartingDirection, vm.IsWarmUpEnabled, vm.IsAlternateMotorDirectionEnabled,
        vm.IsColorManagementEnabled, vm.RedWavelengthNm, vm.GreenWavelengthNm, vm.BlueWavelengthNm,
        vm.OutputGamma, vm.SelectedTargetWhitePointMode, vm.ManualWhitePointColorTemperatureK,
        vm.IsPreviewEnabled, vm.SelectedPreviewMode, vm.SelectedAlignmentMode, vm.SelectedDngExportMode,
        vm.IsChannel1Reversed, vm.IsChannel2Reversed, vm.IsChannel3Reversed, vm.IsChannel4Reversed
    };
}
