using Xunit;
using static PrismUtility.Core.Tests.ScanDebugFilmProfileOrchestrationSourceTests;

namespace PrismUtility.Core.Tests;

[Trait("Category", "Workflow")]
public sealed class ScanWorkflowLinePitchCallerSourceTests
{
    [Theory]
    [InlineData("ScanViewModel.cs", "TryBuildWorkflowRequest")]
    [InlineData("ScanDebugViewModel.cs", "TryBuildDebugWorkflowRequest")]
    public void WorkflowRequestBuilder_BuildsRawAuthoritativeLinePitchInput_when_motor_transport_is_requested(
        string viewModelFileName,
        string methodName)
    {
        var requestBuilder = ExtractMethod(ReadViewModelSource(viewModelFileName), methodName);

        Assert.Contains("ScanTimingMath.BuildLinePitchPlan(", requestBuilder, StringComparison.Ordinal);
        Assert.Contains("new ScanLinePitchPlanRequest(", requestBuilder, StringComparison.Ordinal);
        Assert.Contains("new ScanLinePitchPassInput(", requestBuilder, StringComparison.Ordinal);
        Assert.Contains("ScanChannelRoleHelper.IsActiveRole", requestBuilder, StringComparison.Ordinal);
        Assert.Contains("!linePitchPlan.CanScan", requestBuilder, StringComparison.Ordinal);
        Assert.Contains("new ScanWorkflowLinePitchInput(", requestBuilder, StringComparison.Ordinal);
        Assert.Contains("LinePitchInput: linePitchInput", requestBuilder, StringComparison.Ordinal);
        Assert.Contains("ParameterProfile: passProfiles[passIndex]", requestBuilder, StringComparison.Ordinal);
        Assert.DoesNotContain("MotorIntervalOverrideNanoseconds:", requestBuilder, StringComparison.Ordinal);
    }

    [Fact]
    public void TransportResolver_DerivesCommandsFromRawWorkflowInputInsteadOfCallerPlan()
    {
        var resolver = ReadCoreSource("Services", "ScanWorkflowTransportPlan.cs");

        Assert.Contains("ScanTimingMath.BuildLinePitchPlan(", resolver, StringComparison.Ordinal);
        Assert.Contains("request.LinePitchInput", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("request.LinePitchPlan", resolver, StringComparison.Ordinal);
        Assert.DoesNotContain("request.LinePitchPlanInput", resolver, StringComparison.Ordinal);
    }

    private static string ReadViewModelSource(string viewModelFileName)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "ViewModels", viewModelFileName));

    private static string ReadCoreSource(string directory, string fileName)
        => File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility.Core", directory, fileName));

    private static string FindHostSoftwareRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PRISM Utility")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the Host Software source root.");
    }
}
