using PRISM_Utility.Core.Helpers;
using Xunit;

namespace PrismUtility.Core.Tests;

public sealed class ScanChannelRoleHelperTests
{
    [Fact]
    public void CountActiveRoles_IgnoresUnusedCaseInsensitively()
    {
        var roles = new[] { "Red", "unused", "IR", "Unused" };

        Assert.Equal(2, ScanChannelRoleHelper.CountActiveRoles(roles));
    }

    [Fact]
    public void CountIrOrWhiteRoles_MatchesBothRolesCaseInsensitively()
    {
        var roles = new[] { "IR", "white", "Blue", "Unused" };

        Assert.Equal(2, ScanChannelRoleHelper.CountIrOrWhiteRoles(roles));
    }
}
