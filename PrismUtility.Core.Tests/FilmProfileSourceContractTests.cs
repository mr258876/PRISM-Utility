using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "FilmProfile")]
public sealed class FilmProfileSourceContractTests
{
    [Fact]
    public void ShellNavigation_HasExistingMainAndScanDebugItems()
    {
        var shell = ReadAppSource("Views", "ShellPage.xaml");

        Assert.Contains("x:Uid=\"Shell_Main\" helpers:NavigationHelper.NavigateTo=\"Main\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"Shell_ScanDebug\" helpers:NavigationHelper.NavigateTo=\"ScanDebug\"", shell, StringComparison.Ordinal);
        Assert.Contains("<NavigationView.MenuItems>", shell, StringComparison.Ordinal);
        Assert.Contains("Header=\"{x:Bind ViewModel.HeaderText, Mode=OneWay}\"", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugHardwareControls_ArePresent()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");

        var requiredControls = new[]
        {
            "ScanDebug_StartButton",
            "ScanDebug_StopButton",
            "ScanDebug_PreviewToggleSwitch",
            "ScanDebug_SaveChannelButton",
            "ScanDebug_ClearChannelButton",
            "ScanDebug_ApplyParamsButton",
            "ScanDebug_AutoBlackButton",
            "ScanDebug_AutoWhiteButton",
            "ScanDebug_AutoCalibrateButton",
            "ScanDebug_RunAutofocusButton",
            "ScanDebug_ApplyIlluminationButton",
            "ScanDebug_Motor1MoveButton",
            "ScanDebug_Motor1StopButton",
            "ScanDebug_Motor2MoveButton",
            "ScanDebug_Motor2StopButton",
            "ScanDebug_Motor3MoveButton",
            "ScanDebug_Motor3StopButton",
            "ScanDebug_ExportDngButton",
            "PreviewScrollViewer",
            "PreviewCanvasControl"
        };

        foreach (var control in requiredControls)
            Assert.Contains(control, xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugPage_UsesWorkspaceSummaryAndDirectJsonActions()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");

        Assert.Contains("ScanDebug_FilmProfileWorkspaceSummaryTitle", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.CurrentProfileNameText, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{x:Bind ViewModel.FilmProfileValidationSummary, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_SaveJsonButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.SaveFilmProfileJsonCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"ScanDebug_LoadJsonButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{x:Bind ViewModel.LoadFilmProfileJsonCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugViewModel_JsonCommandsUseWorkspaceAndFileCoordinator()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");

        Assert.Contains("private async Task SaveFilmProfileJson()", source, StringComparison.Ordinal);
        Assert.Contains("private async Task LoadFilmProfileJson()", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.BuildExportDocument", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.MarkExported", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.StageImport", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileWorkspace.ApplyStagedImportAsync", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileFiles.ExportAsync", source, StringComparison.Ordinal);
        Assert.Contains("_filmProfileFiles.ImportAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugViewModel_InvalidJsonStatusesSeparateSummaryStates()
    {
        var source = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");

        Assert.Contains("ScanDebug_Runtime_StatusFilmProfileInvalid\".GetLocalizedOrFallback", source, StringComparison.Ordinal);
        Assert.Contains("ScanDebug_Runtime_StatusFilmProfileInvalidWithSummary\".GetLocalizedFormatOrFallback", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanDebugChannelParameterInputs_UpdateBeforeCalibrationActions()
    {
        var xaml = ReadAppSource("Views", "ScanDebugPage.xaml");

        var requiredBindings = new[]
        {
            "Text=\"{x:Bind ViewModel.SysClockKhz, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.ExposureTicks, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.Adc1Offset, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.Adc1Gain, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.Adc2Offset, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            "Text=\"{x:Bind ViewModel.Adc2Gain, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\""
        };

        foreach (var binding in requiredBindings)
            Assert.Contains(binding, xaml, StringComparison.Ordinal);
    }

    private static string ReadAppSource(params string[] relativePath)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", Path.Combine(relativePath)));

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
