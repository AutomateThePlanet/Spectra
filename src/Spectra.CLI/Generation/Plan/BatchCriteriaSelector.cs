using Spectra.CLI.Criteria;
using Spectra.Core.Baseline;
using Spectra.Core.Criteria;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Generation.Plan;

/// <summary>Spec 094: the criteria a batch prompt carries, and what the selection had to warn about.</summary>
public sealed record BatchCriteria(IReadOnlyList<AcceptanceCriterion> Criteria, IReadOnlyList<string> Warnings);

/// <summary>A section the batch inlines: its document, the heading indexes it covers (all of them for a whole document).</summary>
public sealed record BatchSection(string Ref, string DocPath, bool WholeDocument, IReadOnlyCollection<int> CoveredHeadings);

/// <summary>
/// Spec 094 (FR-009; R6, decision Q1 = C): the criteria of a batch's sections. A criterion belongs to a batch document
/// when its <c>source_doc</c> is that document; it is selected when its <c>source_section</c> names a heading one of the
/// batch's refs covers, or — with no <c>source_section</c>, or one the document doesn't have (warned) — when the batch
/// has the whole document. When nothing is selected, all criteria of the batch's documents are used, with a warning;
/// none at all means the batch has no criteria (the caller refuses). The first definition of an id wins; sorted by id.
/// </summary>
public static class BatchCriteriaSelector
{
    public static async Task<BatchCriteria> SelectAsync(
        string repoRoot,
        SpectraConfig config,
        IReadOnlyList<BatchSection> sections,
        Func<string, CancellationToken, Task<IReadOnlyList<MarkdownHeading>>> headingsOf,
        CancellationToken ct)
    {
        var criteriaDir = Path.GetFullPath(Path.Combine(repoRoot,
            string.IsNullOrWhiteSpace(config.Coverage?.CriteriaDir) ? "docs/criteria" : config.Coverage!.CriteriaDir));
        var files = await CriteriaStore.ScanFilesAsync(criteriaDir, ct);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documents = sections.Select(s => s.DocPath).Distinct(StringComparer.Ordinal).ToList();
        var ofDocuments = new List<AcceptanceCriterion>();
        var selected = new List<AcceptanceCriterion>();
        var warnings = new List<string>();

        foreach (var criterion in files.SelectMany(f => f.Criteria))
        {
            if (string.IsNullOrWhiteSpace(criterion.Id) || !seen.Add(criterion.Id.Trim()))
            {
                continue;
            }

            var document = documents.FirstOrDefault(d => CriteriaProvenance.SameDocument(criterion.SourceDoc, d, repoRoot));
            if (document is null)
            {
                continue;
            }

            ofDocuments.Add(criterion);
            var ofDocument = sections.Where(s => s.DocPath == document).ToList();
            var whole = ofDocument.Any(s => s.WholeDocument);
            if (string.IsNullOrWhiteSpace(criterion.SourceSection))
            {
                if (whole)
                {
                    selected.Add(criterion);
                }

                continue;
            }

            var headings = await headingsOf(document, ct);
            var match = SectionExtractor.Match(headings, criterion.SourceSection);
            if (match is null)
            {
                warnings.Add($"criterion {criterion.Id}: section '{criterion.SourceSection}' not found in {document}; treated as document-level");
                if (whole)
                {
                    selected.Add(criterion);
                }

                continue;
            }

            if (whole || ofDocument.Any(s => s.CoveredHeadings.Contains(match.Index)))
            {
                selected.Add(criterion);
            }
        }

        if (selected.Count == 0 && ofDocuments.Count > 0)
        {
            warnings.Add($"no criteria for the batch's sections; using all criteria of {string.Join(", ", documents)}");
            selected = ofDocuments;
        }

        return new BatchCriteria(selected.OrderBy(c => c.Id.Trim(), StringComparer.Ordinal).ToList(), warnings);
    }
}
