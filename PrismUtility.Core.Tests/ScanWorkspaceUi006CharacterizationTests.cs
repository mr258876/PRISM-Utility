using System.Text.RegularExpressions;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "UI006")]
[Trait("Category", "ScanWorkspace")]
[Trait("Category", "Lifecycle")]
public sealed class ScanWorkspaceUi006CharacterizationTests
{
    [Fact]
    public void Ui006_SourceOnly_ScanPageBindingAndCommandSurfaceStaysEnumerated()
    {
        var xaml = ReadHostSource("PRISM Utility", "Views", "ScanPage.xaml");
        var viewModel = ReadHostSource("PRISM Utility", "ViewModels", "ScanViewModel.cs");
        var shellViewModel = ReadHostSource("PRISM Utility", "ViewModels", "ShellViewModel.cs");

        Assert.Contains("NavigationCacheMode=\"Enabled\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.StartScanCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.StopScanCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.LoadConfigProfileCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.ExportDngChannelsCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.SaveRgbImageCommand}\"", xaml, StringComparison.Ordinal);

        foreach (var binding in new[]
        {
            "ViewModel.HeaderScanStatusText",
            "ViewModel.TopRiskBannerText",
            "ViewModel.StartValidationPromptText",
            "ViewModel.DeviceStatusSummaryText",
            "ViewModel.ConfigurationStatusSummaryText",
            "ViewModel.SelectedConfigProfileName",
            "ViewModel.LoadedScanRecipeSummaryText",
            "ViewModel.IsColorManagementEnabled",
            "ViewModel.RedWavelengthNm",
            "ViewModel.GreenWavelengthNm",
            "ViewModel.BlueWavelengthNm",
            "ViewModel.OutputGamma",
            "ViewModel.SelectedTargetWhitePointMode",
            "ViewModel.ManualWhitePointColorTemperatureK",
            "ViewModel.ExecutionConfigSummaryText",
            "ViewModel.RowOptions",
            "ViewModel.SelectedRows",
            "ViewModel.MotorOptions",
            "ViewModel.SelectedScanMotor",
            "ViewModel.MotorDistancePerLineValue",
            "ViewModel.MotorDistancePerLineUnitOptions",
            "ViewModel.IsWarmUpEnabled",
            "ViewModel.IsAlternateMotorDirectionEnabled",
            "ViewModel.DirectionOptions",
            "ViewModel.SelectedStartingDirection",
            "ViewModel.ComputedMotorSummaryText",
            "ViewModel.PreviewImage",
            "ViewModel.PreviewModeOptions",
            "ViewModel.SelectedPreviewMode",
            "ViewModel.AlignmentModeOptions",
            "ViewModel.SelectedAlignmentMode",
            "ViewModel.DngExportModeOptions",
            "ViewModel.SelectedDngExportMode",
            "ViewModel.OutputCardBlockerText"
        })
        {
            Assert.Contains(binding, xaml, StringComparison.Ordinal);
        }

        foreach (var command in new[]
        {
            "private async Task ConnectDevices()",
            "private async Task DisconnectDevices()",
            "private async Task StartScan()",
            "private async Task StopScan()",
            "private async Task SaveRgbImage(CancellationToken cancellationToken)",
            "private async Task ExportDngChannels(CancellationToken cancellationToken)",
            "private async Task LoadConfigProfile()"
        })
        {
            Assert.Contains(command, viewModel, StringComparison.Ordinal);
        }

        Assert.Contains("ScanViewModel viewModel => viewModel.ConnectDevicesCommand", shellViewModel, StringComparison.Ordinal);
        Assert.Contains("ScanViewModel viewModel => viewModel.DisconnectDevicesCommand", shellViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui006_SourceOnly_ScanExecutionAndPreviewOwnershipRemainInScanViewModelAndServices()
    {
        var scanPage = ReadHostSource("PRISM Utility", "Views", "ScanPage.xaml.cs");
        var viewModel = ReadHostSource("PRISM Utility", "ViewModels", "ScanViewModel.cs");
        var imageService = ReadHostSource("PRISM Utility", "Services", "ScanChannelImageService.cs");
        var previewPresenter = ReadHostSource("PRISM Utility", "Services", "ScanPreviewPresenter.cs");
        var startScan = ExtractMemberBodyAtDeclaration(viewModel, "private async Task StartScan()");
        var stopScan = ExtractMemberBodyAtDeclaration(viewModel, "private async Task StopScan()");
        var updatePreview = ExtractMemberBodyAtDeclaration(viewModel, "private void UpdatePreviewState()");

        Assert.DoesNotContain("_workflow", scanPage, StringComparison.Ordinal);
        Assert.DoesNotContain("_scanSessionCoordinator", scanPage, StringComparison.Ordinal);
        Assert.DoesNotContain("_channelImages", scanPage, StringComparison.Ordinal);
        Assert.Contains("_scanSessionCoordinator.RunConnectedSessionStateAsync", startScan, StringComparison.Ordinal);
        Assert.Contains("_workflow.ExecuteAsync", startScan, StringComparison.Ordinal);
        Assert.Contains("_debugOutputMirror.Mirror(\"Scan.Diagnostic\", diagnostic)", startScan, StringComparison.Ordinal);
        Assert.Contains("_scanSessionCoordinator.StopAsync(CancellationToken.None)", stopScan, StringComparison.Ordinal);

        Assert.Contains("_channelImages.TryBuildRgbComposite", updatePreview, StringComparison.Ordinal);
        Assert.Contains("_channelImages.TryBuildRawPreview", updatePreview, StringComparison.Ordinal);
        Assert.Contains("private readonly IScanChannelImageService _channelImages;", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("IScanPreviewPresenter previewPresenter", viewModel, StringComparison.Ordinal);
        Assert.Contains("private readonly IScanPreviewPresenter _previewPresenter;", imageService, StringComparison.Ordinal);
        Assert.Contains("_previewPresenter.TryRender", imageService, StringComparison.Ordinal);
        Assert.Contains("public sealed class ScanPreviewPresenter : IScanPreviewPresenter", previewPresenter, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui006_SourceOnly_SettingsProjectionsAndCachedStateRemainViewModelResponsibilities()
    {
        var viewModel = ReadHostSource("PRISM Utility", "ViewModels", "ScanViewModel.cs");
        var constructor = ExtractMemberBodyAtDeclaration(viewModel, "public ScanViewModel(");
        var loadConfig = ExtractMemberBodyAtDeclaration(viewModel, "private async Task LoadConfigProfile()");
        var applyRecipe = ExtractMemberBodyAtDeclaration(viewModel, "private void ApplyScanRecipeSettings(ScanFilmScanRecipeSettings? settings)");
        var colorChanged = ExtractMemberBodyAtDeclaration(viewModel, "private void OnColorManagementChanged(Func<ScanColorManagementOptions, ScanColorManagementOptions> mutate)");
        var saveColor = ExtractMemberBodyAtDeclaration(viewModel, "private async Task SaveColorManagementSettingsAsync(Func<ScanColorManagementOptions, ScanColorManagementOptions> mutate)");
        var disconnect = ExtractMemberBodyAtDeclaration(viewModel, "private async Task DisconnectDevices()");

        Assert.Contains("private ScanWorkflowResult? _lastResult;", viewModel, StringComparison.Ordinal);
        Assert.Contains("private ScanParameterSnapshot? _loadedSnapshot;", viewModel, StringComparison.Ordinal);
        Assert.Contains("private ScanFilmAcquisitionSettings? _selectedConfigAcquisitionSettings;", viewModel, StringComparison.Ordinal);
        Assert.Contains("SelectedRows = RowOptions[1];", constructor, StringComparison.Ordinal);
        Assert.Contains("IsPreviewEnabled = true;", constructor, StringComparison.Ordinal);
        Assert.Contains("SelectedDngExportMode = DngExportModeOptions[0];", constructor, StringComparison.Ordinal);
        Assert.Contains("SelectedAlignmentMode = AlignmentModeOptions[0];", constructor, StringComparison.Ordinal);
        Assert.Contains("UpdatePreviewState();", constructor, StringComparison.Ordinal);
        Assert.Contains("UpdateReadinessSummaries();", constructor, StringComparison.Ordinal);

        Assert.Contains("_filmProfileFiles.ImportAsync(CancellationToken.None)", loadConfig, StringComparison.Ordinal);
        Assert.Contains("_calibrationProfiles.ReplaceAsync", loadConfig, StringComparison.Ordinal);
        Assert.Contains("_selectedConfigAcquisitionSettings = profile.AcquisitionSettings?.Normalize();", loadConfig, StringComparison.Ordinal);
        Assert.Contains("ApplyAcquisitionSettingsToInputs(_selectedConfigAcquisitionSettings);", loadConfig, StringComparison.Ordinal);
        Assert.Contains("ApplyScanRecipeSettings(profile.ScanRecipeSettings);", loadConfig, StringComparison.Ordinal);
        Assert.Contains("RefreshLoadedScanRecipeSummary();", loadConfig, StringComparison.Ordinal);
        Assert.Contains("UpdateExecutionConfigSummary();", loadConfig, StringComparison.Ordinal);
        Assert.Contains("UpdateReadinessSummaries();", loadConfig, StringComparison.Ordinal);

        Assert.Contains("IsColorManagementEnabled = colorManagement.IsEnabled;", applyRecipe, StringComparison.Ordinal);
        Assert.Contains("SelectedAlignmentMode = alignmentMode;", applyRecipe, StringComparison.Ordinal);
        Assert.Contains("SelectedDngExportMode = dngExportMode;", applyRecipe, StringComparison.Ordinal);
        Assert.Contains("UpdatePreviewState();", colorChanged, StringComparison.Ordinal);
        Assert.Contains("SaveColorManagementSettingsAsync(mutate);", colorChanged, StringComparison.Ordinal);
        Assert.Contains("_colorManagementSettings.SetSettingsAsync", saveColor, StringComparison.Ordinal);

        Assert.Contains("_loadedSnapshot = null;", disconnect, StringComparison.Ordinal);
        Assert.Contains("_lastResult = null;", disconnect, StringComparison.Ordinal);
        Assert.Contains("PreviewImage = null;", disconnect, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui006_SourceOnly_CachedLeaveReturnLifecycleIsCharacterizedAsBlockedNotFixed()
    {
        var scanPage = ReadHostSource("PRISM Utility", "Views", "ScanPage.xaml.cs");
        var xaml = ReadHostSource("PRISM Utility", "Views", "ScanPage.xaml");
        var viewModel = ReadHostSource("PRISM Utility", "ViewModels", "ScanViewModel.cs");
        var app = ReadHostSource("PRISM Utility", "App.xaml.cs");
        var constructor = ExtractMemberBodyAtDeclaration(scanPage, "public ScanPage()");
        var loaded = ExtractMemberBodyAtDeclaration(scanPage, "private void OnLoaded(object sender, RoutedEventArgs e)");
        var unloaded = ExtractMemberBodyAtDeclaration(scanPage, "private void OnUnloaded(object sender, RoutedEventArgs e)");
        var activate = ExtractMemberBodyAtDeclaration(viewModel, "public void Activate()");
        var deactivate = ExtractMemberBodyAtDeclaration(viewModel, "public void Deactivate()");
        var cleanup = ExtractMemberBodyAtDeclaration(viewModel, "public Task CleanupAsync()");

        Assert.Contains("NavigationCacheMode=\"Enabled\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Loaded += OnLoaded;", constructor, StringComparison.Ordinal);
        Assert.Contains("Unloaded += OnUnloaded;", constructor, StringComparison.Ordinal);
        Assert.Contains("ViewModel.PropertyChanged += OnViewModelPropertyChanged;", loaded, StringComparison.Ordinal);
        Assert.Contains("ViewModel.Activate();", loaded, StringComparison.Ordinal);
        Assert.Contains("ViewModel.PropertyChanged -= OnViewModelPropertyChanged;", unloaded, StringComparison.Ordinal);
        Assert.Contains("ViewModel.Deactivate();", unloaded, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(loaded, "PropertyChanged \\+= OnViewModelPropertyChanged", RegexOptions.CultureInvariant));
        Assert.Single(Regex.Matches(unloaded, "PropertyChanged -= OnViewModelPropertyChanged", RegexOptions.CultureInvariant));
        Assert.DoesNotContain("_isPropertyChangedSubscribed", scanPage, StringComparison.Ordinal);

        Assert.Contains("if (!_areSessionEventsSubscribed)", activate, StringComparison.Ordinal);
        Assert.Contains("_sessionManager.TargetsChanged += OnSessionTargetsChanged;", activate, StringComparison.Ordinal);
        Assert.Contains("_sessionManager.SnapshotChanged += OnSessionSnapshotChanged;", activate, StringComparison.Ordinal);
        Assert.Contains("ApplyManagerSnapshot(_sessionManager.Snapshot);", activate, StringComparison.Ordinal);
        Assert.Contains("RefreshTargets();", activate, StringComparison.Ordinal);
        Assert.Contains("_sessionManager.TargetsChanged -= OnSessionTargetsChanged;", deactivate, StringComparison.Ordinal);
        Assert.Contains("_sessionManager.SnapshotChanged -= OnSessionSnapshotChanged;", deactivate, StringComparison.Ordinal);

        Assert.DoesNotContain("CleanupAsync", app, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel.CleanupAsync()", scanPage, StringComparison.Ordinal);
        Assert.DoesNotContain("_lastResult = null;", unloaded, StringComparison.Ordinal);
        Assert.DoesNotContain("_loadedSnapshot = null;", unloaded, StringComparison.Ordinal);
        Assert.Contains("_uiLifetimeCts.Cancel();", cleanup, StringComparison.Ordinal);
        Assert.Contains("PreviewImage = null;", cleanup, StringComparison.Ordinal);
    }

    [Fact]
    public void Ui006_SourceOnly_NoDuplicateScanWorkspaceOrExtractionAbstractionExistsWhileVm001Blocks()
    {
        var productionSources = EnumerateProductionSources().ToArray();
        var app = ReadHostSource("PRISM Utility", "App.xaml.cs");
        var issueRegistry = ReadHostSource("docs", "architecture", "issues-and-remediation.md");
        var priorities = ReadHostSource("docs", "architecture", "design-correction-priorities.md");

        Assert.Contains("VM-001 extraction hard gate 当前为 `EXTRACTION_BLOCKED`", issueRegistry, StringComparison.Ordinal);
        Assert.Contains("不得做 adapter/extraction", issueRegistry, StringComparison.Ordinal);
        Assert.Contains("VM-001 extraction hard gate 为 `EXTRACTION_BLOCKED`", priorities, StringComparison.Ordinal);

        foreach (var forbidden in new[]
        {
            "IScanPageWorkspace",
            "ScanPageWorkspace",
            "IScanWorkflowWorkspace",
            "ScanWorkflowWorkspace",
            "IScanRuntimeWorkspace",
            "ScanRuntimeWorkspace",
            "IScanExecutionWorkspace",
            "ScanExecutionWorkspace",
            "IScanPagePresenter",
            "ScanPagePresenter",
            "IScanWorkflowPresenter",
            "ScanWorkflowPresenter"
        })
        {
            Assert.DoesNotContain(productionSources, source => source.Content.Contains(forbidden, StringComparison.Ordinal));
        }

        Assert.Contains("services.AddTransient<ScanViewModel>()", app, StringComparison.Ordinal);
        Assert.DoesNotContain("IScanPageWorkspace", app, StringComparison.Ordinal);
        Assert.DoesNotContain("IScanWorkflowWorkspace", app, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<IScanPreviewPresenter, ScanPreviewPresenter>();", app, StringComparison.Ordinal);
    }

    private static IEnumerable<(string Path, string Content)> EnumerateProductionSources()
    {
        foreach (var root in new[] { "PRISM Utility", "PRISM Utility.Core" })
        {
            var fullRoot = Path.Combine(FindHostSoftwareRoot(), root);
            foreach (var file in Directory.EnumerateFiles(fullRoot, "*.cs", SearchOption.AllDirectories))
                yield return (file, File.ReadAllText(file));
        }
    }

    private static string ReadHostSource(params string[] path)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), Path.Combine(path)));

    private static string ExtractMemberBodyAtDeclaration(string source, string declaration)
    {
        var declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
        Assert.True(declarationIndex >= 0, $"Could not find declaration: {declaration}");

        var bodyStart = source.IndexOf('{', declarationIndex);
        Assert.True(bodyStart >= 0, $"Could not find body for declaration: {declaration}");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[bodyStart..(index + 1)];
        }

        throw new InvalidOperationException($"Could not extract body for declaration: {declaration}");
    }

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate Host Software source root.");
    }
}
