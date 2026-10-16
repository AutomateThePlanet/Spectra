using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Agent;

/// <summary>
/// Loads the acceptance criteria related to a suite for the legacy <c>ai update</c> flow. Spec 099 F5: the
/// generation-prompt members of this class (the in-process generator's, retired in Spec 059) were deleted.
/// </summary>
public static class GroundedPromptBuilder
{
    /// <summary>
    /// Loads acceptance criteria related to a specific suite by matching component name or source document.
    /// </summary>
    public static async Task<IReadOnlyList<AcceptanceCriterion>> LoadRelatedCriteriaAsync(
        string criteriaDir,
        string criteriaFile,
        string suiteName,
        CancellationToken ct)
    {
        var indexReader = new CriteriaIndexReader();
        var index = await indexReader.ReadAsync(criteriaFile, ct);

        if (index.Sources.Count == 0)
            return [];

        var fileReader = new CriteriaFileReader();
        var allCriteria = new List<AcceptanceCriterion>();

        foreach (var source in index.Sources)
        {
            var filePath = Path.IsPathRooted(source.File)
                ? source.File
                : Path.Combine(criteriaDir, source.File);

            var criteria = await fileReader.ReadAsync(filePath, ct);
            allCriteria.AddRange(criteria);
        }

        // Filter criteria where Component matches suiteName or SourceDoc contains the suite name
        var suiteNameLower = suiteName.ToLowerInvariant();
        var related = allCriteria
            .Where(c =>
                (!string.IsNullOrEmpty(c.Component) &&
                 c.Component.Equals(suiteName, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(c.SourceDoc) &&
                 c.SourceDoc.Contains(suiteName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return related;
    }
}
