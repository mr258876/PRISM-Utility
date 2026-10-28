using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Models;

public sealed record ScanDngOutputPart(
    string FileSuffix,
    string ModelSuffix,
    DngPixelLayout PixelLayout,
    IReadOnlyList<int> PassIndexes);

public static class ScanDngOutputPlan
{
    public static IReadOnlyList<ScanDngOutputPart> Build(ScanChannelAssignment assignment, int passCount, ScanDngExportMode mode)
        => Build(assignment, Enumerable.Range(0, passCount).ToArray(), mode);

    public static IReadOnlyList<ScanDngOutputPart> Build(ScanChannelAssignment assignment, IReadOnlyList<int> capturedSlots, ScanDngExportMode mode)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(capturedSlots);
        var roles = assignment.Roles;
        if (capturedSlots.Count == 0 || capturedSlots.Distinct().Count() != capturedSlots.Count
            || capturedSlots.Any(slot => slot < 0 || slot >= roles.Count || slot > 4))
            throw new InvalidOperationException("Each DNG capture must identify a distinct configured channel slot.");
        if (capturedSlots.Contains(4) && !string.Equals(roles[4], "White", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The additional DNG capture must be the raw white W channel.");

        var outputs = new List<ScanDngOutputPart>();
        var captures = capturedSlots.Select((slot, index) => (Slot: slot, Index: index, Role: roles[slot])).ToArray();
        switch (mode)
        {
            case ScanDngExportMode.LinearRaw4:
                if (Enumerable.Range(0, 4).All(capturedSlots.Contains))
                {
                    var primaryIndexes = Enumerable.Range(0, 4).Select(slot => captures.Single(capture => capture.Slot == slot).Index).ToArray();
                    outputs.Add(new("linearraw4", "LinearRaw4", DngPixelLayout.LinearRawMultiChannel, Array.AsReadOnly(primaryIndexes)));
                }
                else
                {
                    foreach (var capture in captures.Where(capture => capture.Slot < 4))
                        outputs.Add(new($"raw_ch{capture.Slot + 1}", capture.Role, DngPixelLayout.MonochromeRaw, Array.AsReadOnly(new[] { capture.Index })));
                }
                var white = captures.SingleOrDefault(capture => capture.Slot == 4);
                if (capturedSlots.Contains(4))
                    outputs.Add(new("white_w", "White W", DngPixelLayout.MonochromeRaw, Array.AsReadOnly(new[] { white.Index })));
                break;
            case ScanDngExportMode.LinearRgbIrw:
                var capturedRoles = captures.Select(capture => capture.Role).ToArray();
                var rgb = new[] { FindRole(capturedRoles, "Red"), FindRole(capturedRoles, "Green"), FindRole(capturedRoles, "Blue") };
                var auxiliary = captures
                    .Where(capture => string.Equals(capture.Role, "IR", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(capture.Role, "White", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (auxiliary.Length == 0 || auxiliary.Length + 3 != captures.Length
                    || auxiliary.Select(capture => capture.Role).Distinct(StringComparer.OrdinalIgnoreCase).Count() != auxiliary.Length)
                    throw new InvalidOperationException("RGB DNG export requires RGB captures and distinct auxiliary IR or White captures.");
                outputs.Add(new("rgb", "RGB", DngPixelLayout.LinearRgb, Array.AsReadOnly(rgb)));
                foreach (var auxiliaryCapture in auxiliary)
                    outputs.Add(new(auxiliaryCapture.Slot == 4 ? "white_w" : "irw", auxiliaryCapture.Role,
                        DngPixelLayout.MonochromeRaw, Array.AsReadOnly(new[] { auxiliaryCapture.Index })));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported DNG export mode.");
        }

        return outputs.AsReadOnly();
    }

    private static int FindRole(IReadOnlyList<string> roles, string role)
    {
        var matches = Enumerable.Range(0, roles.Count).Where(index => string.Equals(roles[index], role, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException($"RGB DNG export requires exactly one '{role}' primary channel.");
        return matches[0];
    }
}
