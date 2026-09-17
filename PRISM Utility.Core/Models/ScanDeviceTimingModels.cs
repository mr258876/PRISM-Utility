using System;
using System.Collections.Generic;
using System.Linq;

namespace PRISM_Utility.Core.Models;

public enum ScanDeviceClockStateKind
{
    Unknown,
    DeviceKnown,
    Edited,
    ReadRequired
}

public sealed record ScanDeviceTimingState
{
    private static readonly StringComparer RoleComparer = StringComparer.OrdinalIgnoreCase;

    public static ScanDeviceTimingState Unknown { get; } = new(ScanDeviceClockStateKind.Unknown);

    public ScanDeviceTimingState()
        : this(ScanDeviceClockStateKind.Unknown)
    {
    }

    public ScanDeviceTimingState(ScanDeviceClockStateKind stateKind, IReadOnlyList<string>? revalidationRequiredChannelRoles = null)
    {
        StateKind = stateKind;
        RevalidationRequiredChannelRoles = NormalizeRoles(revalidationRequiredChannelRoles);
    }

    public ScanDeviceClockStateKind StateKind { get; init; }

    public IReadOnlyList<string> RevalidationRequiredChannelRoles { get; init; }

    public bool RequiresDeviceRead => StateKind is ScanDeviceClockStateKind.ReadRequired;

    public bool RequiresDeviceReadForChannel(string channelRole)
        => RequiresDeviceRead && IsRevalidationRequired(channelRole);

    public bool IsRevalidationRequired(string channelRole)
        => !string.IsNullOrWhiteSpace(channelRole)
            && RevalidationRequiredChannelRoles.Any(role => RoleComparer.Equals(role, channelRole));

    public ScanDeviceTimingState MarkDeviceKnown()
        => new(ScanDeviceClockStateKind.DeviceKnown, RevalidationRequiredChannelRoles);

    public ScanDeviceTimingState MarkEdited(IReadOnlyList<string>? revalidationRequiredChannelRoles = null)
        => new(ScanDeviceClockStateKind.Edited, revalidationRequiredChannelRoles);

    public ScanDeviceTimingState MarkReadRequired(IReadOnlyList<string>? revalidationRequiredChannelRoles = null)
        => new(ScanDeviceClockStateKind.ReadRequired, revalidationRequiredChannelRoles);

    public ScanDeviceTimingState WithRevalidationRequiredChannelRoles(IReadOnlyList<string>? revalidationRequiredChannelRoles)
        => new(StateKind, revalidationRequiredChannelRoles);

    private static IReadOnlyList<string> NormalizeRoles(IReadOnlyList<string>? roles)
        => roles is null || roles.Count == 0
            ? Array.Empty<string>()
            : roles.Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(RoleComparer)
                .ToArray();
}
