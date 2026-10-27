using System.Text.Json.Serialization;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Criteria;

/// <summary>A criterion ID that appears more than once in the workspace, with the files it appears in.</summary>
public sealed record DuplicateCriterionId(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("files")] IReadOnlyList<string> Files);

/// <summary>
/// Finds criterion IDs defined more than once across (or within) criteria files (Spec 079 FR-010). Reports only;
/// nothing is ever renumbered.
/// </summary>
public static class CriteriaDuplicateScanner
{
    public static IReadOnlyList<DuplicateCriterionId> Scan(
        IEnumerable<(string File, IReadOnlyList<AcceptanceCriterion> Criteria)> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        return files
            .SelectMany(f => f.Criteria
                .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                .Select(c => (Id: c.Id.Trim(), f.File)))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateCriterionId(
                g.Key,
                g.Select(x => x.File).Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal).ToList()))
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToList();
    }
}
