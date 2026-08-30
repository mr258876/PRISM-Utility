using System.Reflection;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class Scan007UnitContractsTests
{
    [Fact]
    public void Scan007_ProtocolModels_DoNotExposeFalseMicrosecondAliases()
    {
        Assert.Null(typeof(ScanWorkflowRequest).GetProperty("MotorIntervalUs"));
        Assert.Null(typeof(ScanWorkflowResult).GetProperty("MotorIntervalUs"));
        Assert.Null(typeof(ScanFilmAcquisitionSettings).GetProperty("MotorIntervalUs"));
        Assert.Null(typeof(ScanMotorState).GetProperty("IntervalUs"));
        Assert.Null(typeof(ScanAutofocusRequest).GetProperty("MotorIntervalUs"));
        Assert.Null(typeof(ScanDebugConstants).GetField("MotionMinIntervalUs"));
        Assert.Null(typeof(ScanDebugConstants).GetField("MotionDefaultIntervalUs"));
    }

    [Fact]
    public void Scan007_UiMicroseconds_ConvertExactlyToAndFromNanoseconds()
    {
        var conversionType = GetConversionType();

        Assert.True(InvokeTryParse(conversionType, "1", out var intervalNs));
        Assert.Equal(1_000u, intervalNs);
        Assert.True(InvokeTryFormat(conversionType, intervalNs, out var display));
        Assert.Equal("1", display);
        Assert.Equal(1u, InvokeMinimumWholeMicroseconds(conversionType, ScanDebugConstants.MotionMinIntervalNs));
    }

    [Fact]
    public void Scan007_UiMicroseconds_RejectMalformedOverflowAndNonIntegralValues()
    {
        var conversionType = GetConversionType();

        Assert.False(InvokeTryParse(conversionType, "1.5", out _));
        Assert.False(InvokeTryParse(conversionType, "+1", out _));
        Assert.False(InvokeTryParse(conversionType, "not-a-number", out _));
        Assert.False(InvokeTryParse(conversionType, uint.MaxValue.ToString(), out _));
        Assert.False(InvokeTryFormat(conversionType, 999, out _));
    }

    [Fact]
    public void Scan007_NanosecondManualFocusPaths_DoNotUseMicrosecondAliases()
    {
        var scanDebugViewModel = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");

        Assert.DoesNotContain("uint intervalUs", scanDebugViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("uint IntervalUs", scanDebugViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("motorIntervalUs", scanDebugViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan007_ViewModels_UseTheCheckedMicrosecondConversionBoundary()
    {
        var scanViewModel = ReadAppSource("ViewModels", "ScanViewModel.cs");
        var scanDebugViewModel = ReadAppSource("ViewModels", "ScanDebugViewModel.cs");

        Assert.Contains("ScanMotorIntervalText.TryParseMicroseconds", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("ScanMotorIntervalText.TryFormatMicroseconds", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("ScanMotorIntervalText.TryParseMicroseconds", scanDebugViewModel, StringComparison.Ordinal);
        Assert.Contains("ScanMotorIntervalText.TryFormatMicroseconds", scanDebugViewModel, StringComparison.Ordinal);
        Assert.Contains("MotorIntervalUs = FormatMotorIntervalInput(normalized.MotorIntervalNs);", scanViewModel, StringComparison.Ordinal);
        Assert.Contains("MotorIntervalUs = FormatMotorIntervalInput(normalized.MotorIntervalNs);", scanDebugViewModel, StringComparison.Ordinal);
        Assert.Contains("ClearMotorIntervalInput(selectedMotorId);", scanDebugViewModel, StringComparison.Ordinal);
    }

    private static Type GetConversionType()
    {
        var conversionType = typeof(ScanTimingMath).Assembly.GetType("PRISM_Utility.Core.Helpers.ScanMotorIntervalText");
        Assert.NotNull(conversionType);
        return conversionType!;
    }

    private static bool InvokeTryParse(Type conversionType, string text, out uint intervalNs)
    {
        var method = conversionType.GetMethod("TryParseMicroseconds", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);

        object?[] arguments = [text, 0u];
        var converted = Assert.IsType<bool>(method!.Invoke(null, arguments));
        intervalNs = Assert.IsType<uint>(arguments[1]);
        return converted;
    }

    private static bool InvokeTryFormat(Type conversionType, uint intervalNs, out string display)
    {
        var method = conversionType.GetMethod("TryFormatMicroseconds", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);

        object?[] arguments = [intervalNs, string.Empty];
        var formatted = Assert.IsType<bool>(method!.Invoke(null, arguments));
        display = Assert.IsType<string>(arguments[1]);
        return formatted;
    }

    private static uint InvokeMinimumWholeMicroseconds(Type conversionType, uint intervalNs)
    {
        var method = conversionType.GetMethod("MinimumWholeMicroseconds", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<uint>(method!.Invoke(null, [intervalNs]));
    }

    private static string ReadAppSource(params string[] parts)
        => File.ReadAllText(Path.Combine([FindHostSoftwareRoot(), "PRISM Utility", .. parts]));

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
