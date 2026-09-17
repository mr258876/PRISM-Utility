using PRISM_Utility.Core.Models;
using PRISM_Utility.Core.Helpers;

namespace PRISM_Utility.Core.Services;

internal sealed record ScanWorkflowTransportPass(
    int PassIndex,
    string ChannelRole,
    ScanParameterSnapshot ParameterProfile,
    uint MotorSteps,
    uint MotorIntervalNanoseconds);

internal static class ScanWorkflowTransportPlan
{
    private const string UnusedChannelRole = "Unused";

    public static IReadOnlyList<ScanWorkflowTransportPass> Resolve(ScanWorkflowRequest request)
    {
        var activePassIndices = GetActivePassIndices(request.PassChannelRoles);
        var effectiveProfiles = request.PassParameterProfiles
            .Select(profile => profile with { SysClockKhz = request.SysClockKhz })
            .ToArray();
        if (!request.EnableMotorTransport)
        {
            return activePassIndices
                .Select(passIndex => new ScanWorkflowTransportPass(
                    passIndex,
                    request.PassChannelRoles[passIndex],
                    effectiveProfiles[passIndex],
                    0,
                    0))
                .ToArray();
        }

        var input = request.LinePitchInput ?? throw new ScanWorkflowTransportValidationException(
            ScanWorkflowTransportValidationFailure.MissingLinePitchInput,
            "Scan transport requires current line-pitch inputs.");
        if (request.SysClockKhz < ScanDebugConstants.MinSysClockKhz)
        {
            throw new ScanWorkflowTransportValidationException(
                ScanWorkflowTransportValidationFailure.InvalidLinePitchInput,
                "Scan transport requires a live global system clock.");
        }
        if (input.MotorIntervalOverrides is null || input.MotorIntervalOverrides.Length != request.PassChannelRoles.Length)
        {
            throw new ScanWorkflowTransportValidationException(
                ScanWorkflowTransportValidationFailure.InvalidLinePitchInput,
                "Line-pitch interval overrides must match the workflow pass count.");
        }

        var plan = ScanTimingMath.BuildLinePitchPlan(new ScanLinePitchPlanRequest(
            request.Rows,
            input.TargetLinePitchMicrometers,
            input.MotorMechanics,
            input.MinimumMotorIntervalNanoseconds,
            Enumerable.Range(0, request.PassChannelRoles.Length)
                .Select(passIndex =>
                {
                    var passProfile = effectiveProfiles[passIndex];
                    return new ScanLinePitchPassInput(
                        passIndex,
                        request.PassChannelRoles[passIndex],
                        !string.Equals(request.PassChannelRoles[passIndex], UnusedChannelRole, StringComparison.OrdinalIgnoreCase),
                        passProfile.ExposureTicks,
                        passProfile.SysClockKhz,
                        input.MotorIntervalOverrides[passIndex],
                        passProfile);
                })
                .ToArray()));
        if (!plan.CanScan)
        {
            throw new ScanWorkflowTransportValidationException(
                ScanWorkflowTransportValidationFailure.InvalidLinePitchPlan,
                "Scan transport is blocked by the line-pitch plan.",
                plan);
        }

        if (request.AlternateMotorDirection && !plan.CanUseAlternateDirection)
        {
            throw new ScanWorkflowTransportValidationException(
                ScanWorkflowTransportValidationFailure.AlternateDirectionNotAllowed,
                "Alternate motor direction is blocked by the line-pitch plan.",
                plan);
        }

        if (plan.Passes.Count != activePassIndices.Count)
        {
            throw new ScanWorkflowTransportValidationException(
                ScanWorkflowTransportValidationFailure.PlanPassMismatch,
                "Line-pitch plan passes do not match the active workflow passes.",
                plan);
        }

        var unresolvedPassIndices = new HashSet<int>(activePassIndices);
        var resolvedPasses = new List<ScanWorkflowTransportPass>(plan.Passes.Count);
        foreach (var passPlan in plan.Passes)
        {
            if (passPlan.PassIndex < 0
                || passPlan.PassIndex >= request.PassChannelRoles.Length
                || !unresolvedPassIndices.Remove(passPlan.PassIndex)
                || !string.Equals(request.PassChannelRoles[passPlan.PassIndex], passPlan.ChannelRole, StringComparison.OrdinalIgnoreCase)
                || passPlan.MotorStepsPerPass == 0
                || passPlan.SelectedMotorIntervalNanoseconds < ScanDebugConstants.MotionMinIntervalNs)
            {
                throw new ScanWorkflowTransportValidationException(
                    ScanWorkflowTransportValidationFailure.PlanPassMismatch,
                    "Line-pitch plan passes do not match the active workflow passes.",
                    plan);
            }

            resolvedPasses.Add(new ScanWorkflowTransportPass(
                passPlan.PassIndex,
                request.PassChannelRoles[passPlan.PassIndex],
                effectiveProfiles[passPlan.PassIndex],
                request.EnableMotorTransport ? passPlan.MotorStepsPerPass : 0,
                request.EnableMotorTransport ? passPlan.SelectedMotorIntervalNanoseconds : 0));
        }

        if (unresolvedPassIndices.Count != 0)
        {
            throw new ScanWorkflowTransportValidationException(
                ScanWorkflowTransportValidationFailure.PlanPassMismatch,
                "Line-pitch plan passes do not match the active workflow passes.",
                plan);
        }

        return resolvedPasses;
    }

    private static List<int> GetActivePassIndices(IReadOnlyList<string> passChannelRoles)
    {
        var activePassIndices = new List<int>(passChannelRoles.Count);
        for (var index = 0; index < passChannelRoles.Count; index++)
        {
            if (!string.Equals(passChannelRoles[index], UnusedChannelRole, StringComparison.OrdinalIgnoreCase))
                activePassIndices.Add(index);
        }

        return activePassIndices;
    }
}
