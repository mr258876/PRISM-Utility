using PRISM_Utility.Core.Services;
using Xunit;

namespace PrismUtility.Core.Tests;

[Trait("Category", "App001")]
public sealed class UnhandledExceptionReporterTests
{
    [Fact]
    public void Report_MirrorsNestedExceptionWithoutFallback()
    {
        var mirrored = new List<(string Source, string Message)>();
        var debugFallback = new List<string>();
        var traceFallback = new List<string>();
        var exception = new Exception(message: null, innerException: new IOException("nested file scheduling failure"));

        var reportFailure = InvokeReport(
            exception,
            (source, message) => mirrored.Add((source, message)),
            debugFallback.Add,
            traceFallback.Add);

        Assert.Null(reportFailure);
        var entry = Assert.Single(mirrored);
        Assert.Equal("WinUI.UnhandledException", entry.Source);
        Assert.Contains("nested file scheduling failure", entry.Message, StringComparison.Ordinal);
        Assert.Empty(debugFallback);
        Assert.Empty(traceFallback);
    }

    [Fact]
    public void Report_WhenMirrorThrowsForFileSchedulingFailure_AttemptsBothFallbacksAndDoesNotThrow()
    {
        var debugFallback = new List<string>();
        var traceFallback = new List<string>();
        var exception = new InvalidOperationException("top-level failure", new IOException("file scheduling failure"));

        var reportFailure = InvokeReport(
            exception,
            (_, _) => throw new IOException("mirror file scheduling failure"),
            debugFallback.Add,
            traceFallback.Add);

        Assert.Null(reportFailure);
        Assert.Contains("file scheduling failure", Assert.Single(debugFallback), StringComparison.Ordinal);
        Assert.Contains("file scheduling failure", Assert.Single(traceFallback), StringComparison.Ordinal);
    }

    [Fact]
    public void Report_WhenMirrorAndDebugFallbackThrow_StillAttemptsTraceAndDoesNotThrow()
    {
        var traceFallback = new List<string>();
        var exception = new InvalidOperationException("log subscriber failure");

        var reportFailure = InvokeReport(
            exception,
            (_, _) => throw new InvalidOperationException("mirror subscriber failure"),
            _ => throw new InvalidOperationException("debug listener failure"),
            traceFallback.Add);

        Assert.Null(reportFailure);
        Assert.Contains("log subscriber failure", Assert.Single(traceFallback), StringComparison.Ordinal);
    }

    private static Exception? InvokeReport(Exception exception, Action<string, string> mirror, Action<string> debugFallback, Action<string> traceFallback)
        => Record.Exception(() => UnhandledExceptionReporter.Report(exception, mirror, debugFallback, traceFallback));
}
