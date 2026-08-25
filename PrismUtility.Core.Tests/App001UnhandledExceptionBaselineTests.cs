using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "App001")]
public sealed class App001UnhandledExceptionBaselineTests
{
    [Fact]
    public void App001Baseline_GlobalHandlerIsRegisteredAndLeavesHandledAtWinUiDefault()
    {
        var source = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "App.xaml.cs"));
        var handler = ExtractMethod(source, "private void App_UnhandledException");

        Assert.Contains("UnhandledException += App_UnhandledException;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("e.Handled", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("Handled =", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void App001Handler_ReportsThroughCoreSeamWithoutRecoveringTheException()
    {
        var source = File.ReadAllText(Path.Combine(FindHostSoftwareRoot(), "PRISM Utility", "App.xaml.cs"));
        var handler = ExtractMethod(source, "private void App_UnhandledException");

        Assert.Contains("UnhandledExceptionReporter.Report(", handler, StringComparison.Ordinal);
        Assert.Contains("e.Exception", handler, StringComparison.Ordinal);
        Assert.Contains("GetService<IDebugOutputMirrorService>().Mirror", handler, StringComparison.Ordinal);
        Assert.Contains("Debug.WriteLine", handler, StringComparison.Ordinal);
        Assert.Contains("Trace.WriteLine", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("e.Handled", handler, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Could not find {signature} in App.xaml.cs.");

        var bodyStart = source.IndexOf('{', signatureIndex);
        Assert.True(bodyStart >= 0, $"Could not find the body for {signature} in App.xaml.cs.");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[bodyStart..(index + 1)];
        }

        throw new InvalidOperationException($"Could not find the end of {signature} in App.xaml.cs.");
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
