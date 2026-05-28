using PRISM_Utility.Core.Helpers;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class InvariantNumericTextTests
{
    [Fact]
    public void TryParseDouble_UsesInvariantDecimalSeparator()
    {
        var ok = InvariantNumericText.TryParseDouble("1.25", out var value);

        Assert.True(ok);
        Assert.Equal(1.25, value, 6);
    }

    [Fact]
    public void FormatCompactDouble_UsesCompactInvariantFormat()
        => Assert.Equal("1.235", InvariantNumericText.FormatCompactDouble(1.23456));
}
