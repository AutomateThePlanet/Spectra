using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.Core.Coverage;

/// <summary>
/// Analyzes acceptance criteria coverage: which criteria have linked tests.
/// Reads from the criteria master index and per-document criteria files.
/// Falls back to legacy single-file parsing when no index exists.
/// </summary>
public sealed class AcceptanceCriteriaCoverageAnalyzer
{
    private readonly CriteriaIndexReader _indexReader = new();
    private readonly CriteriaFileReader _fileReader = new();
    private readonly AcceptanceCriteriaParser _legacyParser = new();

    /// <summary>
    /// Analyzes acceptance criteria coverage from the criteria directory (index-based).
    /// </summary>
    public async Task<AcceptanceCriteriaCoverage> AnalyzeFromDirectoryAsync(
        string criteriaDir,
        string criteriaIndexPath,
        IReadOnlyList<TestCase> allTests,
        CancellationToken ct = default)
    {
        var index = await _indexReader.ReadAsync(criteriaIndexPath, ct);

        if (index.Sources.Count == 0)
        {
            // No index — return empty coverage
            return BuildCoverage([], allTests);
        }

        // Read all criteria from per-document files
        var allCriteria = new List<AcceptanceCriterion>();

        foreach (var source in index.Sources)
        {
            var filePath = Path.Combine(criteriaDir, source.File);
            var criteria = await _fileReader.ReadAsync(filePath, ct);
            allCriteria.AddRange(criteria);
        }

        return BuildCoverage(allCriteria, allTests);
    }

    /// <summary>
    /// Analyzes acceptance criteria coverage from a single criteria file (legacy).
    /// </summary>
    public async Task<AcceptanceCriteriaCoverage> AnalyzeAsync(
        string criteriaFilePath,
        IReadOnlyList<TestCase> allTests,
        CancellationToken ct = default)
    {
        // Try index-based approach first: check if criteria dir and index exist
        var criteriaDir = Path.GetDirectoryName(criteriaFilePath);
        if (criteriaDir is not null)
        {
            var indexPath = Path.Combine(criteriaDir, "_criteria_index.yaml");
            if (File.Exists(indexPath))
            {
                return await AnalyzeFromDirectoryAsync(criteriaDir, indexPath, allTests, ct);
            }
        }

        // Fall back to legacy single-file parsing
        var definitions = await _legacyParser.ParseAsync(criteriaFilePath, ct);
        return BuildCoverage(definitions, allTests);
    }

    private static AcceptanceCriteriaCoverage BuildCoverage(
        IReadOnlyList<AcceptanceCriterion> definitions,
        IReadOnlyList<TestCase> allTests)
    {
        var hasDefinitions = definitions.Count > 0;

        // Build map: criterion ID -> list of test IDs that reference it
        var criterionToTests = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var test in allTests)
        {
            // Check both legacy Requirements and new Criteria fields
            var linkedIds = test.Requirements
                .Concat(test.Criteria);

            foreach (var criterionId in linkedIds)
            {
                if (!criterionToTests.TryGetValue(criterionId, out var testIds))
                {
                    testIds = [];
                    criterionToTests[criterionId] = testIds;
                }
                testIds.Add(test.Id);
            }
        }

        var details = new List<CriteriaCoverageDetail>();

        if (hasDefinitions)
        {
            // Cross-reference definitions with test references
            foreach (var criterion in definitions)
            {
                var testIds = criterionToTests.GetValueOrDefault(criterion.Id, []);
                details.Add(new CriteriaCoverageDetail
                {
                    Id = criterion.Id,
                    Text = criterion.Text,
                    Tests = testIds,
                    Covered = testIds.Count > 0
                });
            }
        }
        else
        {
            // No criteria definitions — report criteria discovered from tests as a flat list
            foreach (var (criterionId, testIds) in criterionToTests.OrderBy(kv => kv.Key))
            {
                details.Add(new CriteriaCoverageDetail
                {
                    Id = criterionId,
                    Text = null,
                    Tests = testIds,
                    Covered = true
                });
            }
        }

        var total = details.Count;
        var covered = details.Count(d => d.Covered);
        var percentage = total > 0
            ? Math.Round((covered * 100m) / total, 2)
            : 0m;

        // Build source breakdown grouped by source_type
        Dictionary<string, SourceCoverageStats>? sourceBreakdown = null;

        if (hasDefinitions)
        {
            var bySourceType = definitions
                .GroupBy(c => c.SourceType ?? "document")
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var sourceIds = g.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var sourceDetails = details.Where(d => sourceIds.Contains(d.Id)).ToList();
                        var sourceTotal = sourceDetails.Count;
                        var sourceCovered = sourceDetails.Count(d => d.Covered);
                        return new SourceCoverageStats
                        {
                            SourceType = g.Key,
                            Total = sourceTotal,
                            Covered = sourceCovered,
                            Percentage = sourceTotal > 0
                                ? Math.Round((sourceCovered * 100m) / sourceTotal, 2)
                                : 0m
                        };
                    });

            if (bySourceType.Count > 0)
                sourceBreakdown = bySourceType;
        }

        return new AcceptanceCriteriaCoverage
        {
            TotalCriteria = total,
            CoveredCriteria = covered,
            Percentage = percentage,
            HasCriteriaFile = hasDefinitions,
            Details = details,
            SourceBreakdown = sourceBreakdown
        };
    }
}
