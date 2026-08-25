namespace PRISM_Utility.Core.Models;

public enum ScanChannelAlignmentStatus
{
    Aligned,
    Disabled,
    Fallback,
    Failed
}

public enum ScanChannelAlignmentDiagnosticKind
{
    EmptyInput,
    InvalidRoi,
    MissingUniqueGreen,
    NonConverged,
    OpenCvFailure,
    AlignmentUnavailable,
    UnsupportedMode
}

public sealed record ScanChannelAlignmentDiagnostic(
    ScanChannelAlignmentDiagnosticKind Kind,
    int? ChannelIndex,
    string? ChannelRole,
    string Message,
    int? NativeCode = null,
    string? NativeFunction = null,
    string? NativeFile = null,
    int? NativeLine = null);

public sealed record ScanChannelAlignmentChannelOutcome(
    int ChannelIndex,
    string ChannelRole,
    ScanChannelAlignmentStatus Status,
    ScanChannelAlignmentDiagnostic? Diagnostic = null);

public sealed record ScanChannelAlignmentResult(
    ScanChannelAlignmentStatus Status,
    byte[][] AlignedPassBuffers,
    IReadOnlyList<ScanChannelAlignmentChannelOutcome> ChannelOutcomes,
    IReadOnlyList<ScanChannelAlignmentDiagnostic> Diagnostics)
{
    public bool HasUsableBuffers
        => Status != ScanChannelAlignmentStatus.Failed && AlignedPassBuffers.Length != 0;

    public string GetDiagnosticSummary()
        => string.Join(
            "; ",
            Diagnostics.Select(diagnostic =>
            {
                var channel = diagnostic.ChannelRole is null
                    ? "scan"
                    : $"channel {diagnostic.ChannelRole}";
                var native = string.Join(
                    ", ",
                    new[]
                    {
                        diagnostic.NativeCode is null ? null : $"code={diagnostic.NativeCode}",
                        diagnostic.NativeFunction is null ? null : $"func={diagnostic.NativeFunction}",
                        diagnostic.NativeFile is null ? null : $"file={diagnostic.NativeFile}",
                        diagnostic.NativeLine is null ? null : $"line={diagnostic.NativeLine}"
                    }.Where(value => value is not null));
                native = string.IsNullOrEmpty(native) ? string.Empty : $" native({native})";
                return $"{channel} {diagnostic.Kind}: {diagnostic.Message}{native}";
            }));
}

public sealed record ScanDngExportResult
{
    public ScanDngExportResult(
        ScanChannelAlignmentStatus alignmentStatus,
        IEnumerable<ScanChannelAlignmentDiagnostic>? diagnostics,
        IEnumerable<string?>? affectedChannelRoles)
    {
        AlignmentStatus = alignmentStatus;
        Diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<ScanChannelAlignmentDiagnostic>()).ToArray());
        AffectedChannelRoles = Array.AsReadOnly(SnapshotAffectedChannelRoles(affectedChannelRoles));
    }

    public ScanChannelAlignmentStatus AlignmentStatus { get; }

    public IReadOnlyList<ScanChannelAlignmentDiagnostic> Diagnostics { get; }

    public bool HasAlignmentWarning
        => AlignmentStatus is ScanChannelAlignmentStatus.Fallback or ScanChannelAlignmentStatus.Disabled
           || Diagnostics.Count != 0;

    public IReadOnlyList<string> AffectedChannelRoles { get; }

    public static ScanDngExportResult FromAlignmentResult(ScanChannelAlignmentResult alignmentResult)
    {
        var affectedChannelRoles = alignmentResult.Diagnostics
            .Select(diagnostic => diagnostic.ChannelRole)
            .Concat(alignmentResult.ChannelOutcomes
                .Where(outcome => outcome.Status is ScanChannelAlignmentStatus.Fallback or ScanChannelAlignmentStatus.Disabled)
                .Select(outcome => outcome.ChannelRole));

        return new ScanDngExportResult(alignmentResult.Status, alignmentResult.Diagnostics, affectedChannelRoles);
    }

    private static string[] SnapshotAffectedChannelRoles(IEnumerable<string?>? affectedChannelRoles)
    {
        if (affectedChannelRoles is null)
            return Array.Empty<string>();

        var roles = new List<string>();
        var seenRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in affectedChannelRoles)
        {
            var normalizedRole = role?.Trim();
            if (string.IsNullOrEmpty(normalizedRole) || !seenRoles.Add(normalizedRole))
                continue;

            roles.Add(normalizedRole);
        }

        return roles.ToArray();
    }
}
