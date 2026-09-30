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
    /// The criteria written by <paramref name="doc"/> (a normalised repo-relative path): those whose
    /// <c>source_doc</c> is that document, compared lexically and case-insensitively. A criterion without a
    /// <c>source_doc</c> belongs to no document.
    /// </summary>
    public static IReadOnlyList<AcceptanceCriterion> OwnedBy(IEnumerable<AcceptanceCriterion> criteria, string doc)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var normalizedDoc = CriteriaPaths.NormalizeLexical(doc);
        return criteria
            .Where(c => !string.IsNullOrWhiteSpace(c.SourceDoc)
                && string.Equals(CriteriaPaths.NormalizeLexical(c.SourceDoc), normalizedDoc, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
