using System.Text;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Generation;

/// <summary>
/// Spec 059: deterministic, model-free loader for a suite's acceptance-criteria grounding
/// context. Relocated verbatim out of the (now-removed) in-process <c>GenerateHandler</c> so the
/// compile seam — the only surviving generation path — retains the Spec 048 criteria resolution.
/// Matches criteria by component / source-doc / file name and exposes the suite-match count
/// separately so the upstream "no criteria matched" note fires on a true no-match (not on the
/// last-resort "use all criteria" fallback).
/// </summary>
public static class CriteriaContextLoader
{
    /// <summary>
    /// Spec 048: result of loading acceptance-criteria context for a suite. <see cref="Context"/>
    /// is the formatted grounding string; <see cref="SuiteMatchedCount"/> is exposed separately so
    /// the no-match note fires on a true "nothing matched this suite" condition.
    /// </summary>
    public sealed record CriteriaContextResult(
        string? Context,
        int SuiteMatchedCount,
        int TotalCriteriaCount);

    /// <summary>
    /// Loads acceptance criteria relevant to the target suite from .criteria.yaml files.
    /// Matches by document name and by component field. Returns a record carrying the formatted
    /// context AND the suite-match count.
    /// </summary>
    public static async Task<CriteriaContextResult> LoadCriteriaContextAsync(
        string basePath,
        string suiteName,
        SpectraConfig config,
        CancellationToken ct)
    {
        var criteriaDir = Path.Combine(basePath, config.Coverage?.CriteriaDir ?? "docs/criteria");
        if (!Directory.Exists(criteriaDir))
            return new CriteriaContextResult(null, 0, 0);

        var reader = new CriteriaFileReader();
        var allCriteria = new List<AcceptanceCriterion>();

        // Load criteria from all .criteria.yaml files in the criteria directory
        var criteriaFiles = Directory.GetFiles(criteriaDir, "*.criteria.yaml", SearchOption.AllDirectories);
        foreach (var file in criteriaFiles)
        {
            var criteria = await reader.ReadAsync(file, ct);
            allCriteria.AddRange(criteria);
        }

        if (allCriteria.Count == 0)
            return new CriteriaContextResult(null, 0, 0);

        // Filter: criteria matching suite name (exact or partial match on component, source doc, or file name)
        var relevant = allCriteria.Where(c =>
            // Exact match by component
            (c.Component != null && c.Component.Equals(suiteName, StringComparison.OrdinalIgnoreCase)) ||
            // Component contains suite name (e.g., suite "reporting" matches component "reporting-analytics")
            (c.Component != null && c.Component.Contains(suiteName, StringComparison.OrdinalIgnoreCase)) ||
            // Suite contains component (e.g., suite "reporting-analytics" matches component "reporting")
            (c.Component != null && suiteName.Contains(c.Component, StringComparison.OrdinalIgnoreCase)) ||
            // Source doc file name contains suite name
            (c.SourceDoc != null && Path.GetFileNameWithoutExtension(c.SourceDoc)
                .Contains(suiteName, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        // Spec 048: capture the suite-match count BEFORE the last-resort fallback.
        var suiteMatchedCount = relevant.Count;

        // Also include criteria from files whose name starts with the suite name
        if (relevant.Count == 0)
        {
            var matchingFiles = criteriaFiles.Where(f =>
                Path.GetFileNameWithoutExtension(f).Replace(".criteria", "")
                    .Contains(suiteName, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchingFiles.Count > 0)
            {
                // Reload only from matching files
                relevant = new List<AcceptanceCriterion>();
                foreach (var file in matchingFiles)
                {
                    var fileCriteria = await reader.ReadAsync(file, ct);
                    relevant.AddRange(fileCriteria);
                }
                suiteMatchedCount = relevant.Count;
            }
        }

        // Last resort: use all criteria (better than none, but may be noisy).
        // Spec 048: this path does NOT contribute to suiteMatchedCount.
        if (relevant.Count == 0)
            relevant = allCriteria;

        // Format as context string
        var sb = new StringBuilder();
        foreach (var criterion in relevant)
        {
            sb.Append($"- **{criterion.Id}**");
            if (!string.IsNullOrEmpty(criterion.Rfc2119))
                sb.Append($" [{criterion.Rfc2119}]");
            sb.AppendLine($" {criterion.Text}");
            if (!string.IsNullOrEmpty(criterion.Component))
                sb.AppendLine($"  Component: {criterion.Component}");
            if (!string.IsNullOrEmpty(criterion.TechniqueHint))
                sb.AppendLine($"  Technique hint: {criterion.TechniqueHint}");
        }

        var context = sb.Length > 0 ? sb.ToString() : null;
        return new CriteriaContextResult(context, suiteMatchedCount, allCriteria.Count);
    }
}
