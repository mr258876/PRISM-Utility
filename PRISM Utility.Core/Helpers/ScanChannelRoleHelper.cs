namespace PRISM_Utility.Core.Helpers;

public static class ScanChannelRoleHelper
{
    public const string UnusedRole = "Unused";

    public static bool IsActiveRole(string? role)
        => !string.Equals(role, UnusedRole, StringComparison.OrdinalIgnoreCase);

    public static int CountRole(IEnumerable<string> roles, string role)
        => roles.Count(candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));

    public static int CountActiveRoles(IEnumerable<string> roles)
        => roles.Count(IsActiveRole);

    public static int CountIrOrWhiteRoles(IEnumerable<string> roles)
        => roles.Count(role => string.Equals(role, "IR", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "White", StringComparison.OrdinalIgnoreCase));
}
