using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Criteria;

/// <summary>
/// Spec 080 B1–B3: the one provenance rule for criteria files shared by several documents (a pre-079 base-name
/// collision). Only the criteria whose <c>source_doc</c> is a document belong to it; compile, ingest, apply-move and
/// retire all use this rule, so none offers, copies, relabels or removes another document's criteria.
/// </summary>
public static class CriteriaProvenance
{
    /// <summary>True when another index source points at the same criteria file as <paramref name="source"/>.</summary>
    public static bool IsShared(CriteriaIndex index, CriteriaSource source)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(source);

        var file = CriteriaPaths.NormalizeLexical(source.File);
        return file.Length > 0 && index.Sources.Any(s => !ReferenceEquals(s, source)
            && string.Equals(CriteriaPaths.NormalizeLexical(s.File), file, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The criteria written by <paramref name="doc"/>: those whose <c>source_doc</c> is that document. Spec 081 C1: both
    /// sides are normalised repo-aware (<see cref="CriteriaPaths.NormalizeSourceDoc"/>), so a <c>source_doc</c> stored
    /// absolute inside the repository, or with <c>\</c> / <c>./</c>, still matches. A criterion whose
    /// <c>source_doc</c> is empty or lies outside the repository belongs to no document.
    /// </summary>
    public static IReadOnlyList<AcceptanceCriterion> OwnedBy(
        IEnumerable<AcceptanceCriterion> criteria, string doc, string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var normalizedDoc = Normalize(doc, repoRoot);
        if (normalizedDoc is null)
        {
            return [];
        }

        return criteria
            .Where(c => string.Equals(Normalize(c.SourceDoc, repoRoot), normalizedDoc, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Spec 081 C1: true when two document paths name the same repository document after repo-aware normalisation
    /// (the rule <see cref="OwnedBy"/> uses). A path that is empty or outside the repository matches nothing.
    /// </summary>
    public static bool SameDocument(string? a, string? b, string repoRoot)
    {
        var left = Normalize(a, repoRoot);
        return left is not null && string.Equals(left, Normalize(b, repoRoot), StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? path, string repoRoot) =>
        string.IsNullOrWhiteSpace(path) ? null : CriteriaPaths.NormalizeSourceDoc(path, repoRoot).Path;
}
