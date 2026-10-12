using Spectra.CLI.Baseline;
using Spectra.Core.Baseline;

namespace Spectra.CLI.Generation.Plan;

/// <summary>
/// Spec 094: where a behaviour's source (or a plan's section ref) points now. Resolved: the canonical
/// <see cref="Ref"/>, its document, the heading <see cref="Position"/> (−1 = the whole document) and the section's
/// fingerprint. Unresolved: the <see cref="Reason"/> (<c>empty ref</c>, <c>not found</c>, <c>outside allowed roots</c>,
/// <c>unreadable</c>, <c>anchor not found</c>).
/// </summary>
public sealed record SectionResolution(
    string? Ref, string? DocPath, string? FullPath, int Position, string? Fingerprint, string? Reason, bool Ambiguous, string AnchorKey)
{
    public bool Resolved => Reason is null;
}

/// <summary>
/// Spec 094 (FR-002, FR-010; R1, R5): resolves a ref with the baseline's rules — <see cref="BaselineContext"/> (084
/// resolver and section extraction, the 085 heading matcher) — and gives it its canonical form
/// (<see cref="SectionRef.Canonical"/>). The analysis ingest and <c>compile-prompt --batch</c> both use it, so the plan's
/// fingerprints and the staleness check always agree.
/// </summary>
public static class PlanSectionResolver
{
    public static async Task<SectionResolution> ResolveAsync(BaselineContext context, string? source, CancellationToken ct)
    {
        var trimmed = source?.Trim() ?? string.Empty;
        var anchor = SectionExtractor.AnchorOf(trimmed);
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return Unresolved("empty ref");
        }

        var section = await context.GetSectionAsync(trimmed, ct);
        if (section.Current.Missing || section.RelativePath is null || section.FullPath is null)
        {
            return Unresolved(section.Current.MissingReason ?? BaselineContext.NotFound);
        }

        if (string.IsNullOrEmpty(anchor))
        {
            return new SectionResolution(section.RelativePath, section.RelativePath, section.FullPath, -1,
                section.Current.Fingerprint, null, false, string.Empty);
        }

        var headings = await context.GetHeadingsAsync(section.FullPath, ct);
        var match = SectionExtractor.Match(headings, anchor);
        if (match is null)
        {
            return Unresolved("anchor not found");
        }

        var canonical = SectionRef.Canonical(section.RelativePath, headings, match.Index) ?? $"{section.RelativePath}#{anchor}";
        return new SectionResolution(canonical, section.RelativePath, section.FullPath, match.Index,
            section.Current.Fingerprint, null, section.Ambiguous, AnchorKey.FromAnchor(anchor));

        SectionResolution Unresolved(string reason) => new(null, null, null, 0, null, reason, false, string.Empty);
    }
}
