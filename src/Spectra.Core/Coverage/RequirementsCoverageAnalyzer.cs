using Spectra.Core.Models;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.Core.Coverage;

/// <summary>
/// Analyzes requirements coverage: which requirements have linked tests.
/// </summary>
public sealed class RequirementsCoverageAnalyzer
{
    private readonly RequirementsParser _parser = new();

    /// <summary>
    /// Analyzes requirements coverage from a requirements file and all parsed tests.
    /// </summary>
    public async Task<RequirementsCoverage> AnalyzeAsync(
        string requirementsFilePath,
        IReadOnlyList<TestCase> allTests,
        CancellationToken ct = default)
    {
        var definitions = await _parser.ParseAsync(requirementsFilePath, ct);
        var hasFile = definitions.Count > 0;

        // Build map: requirement ID → list of test IDs that reference it
        var reqToTests = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var test in allTests)
        {
            foreach (var reqId in test.Requirements)
            {
                if (!reqToTests.TryGetValue(reqId, out var testIds))
                {
                    testIds = [];
                    reqToTests[reqId] = testIds;
                }
                testIds.Add(test.Id);
            }
        }

        var details = new List<RequirementCoverageDetail>();

        if (hasFile)
        {
            // Cross-reference definitions with test references
            foreach (var req in definitions)
            {
                var testIds = reqToTests.GetValueOrDefault(req.Id, []);
                details.Add(new RequirementCoverageDetail
                {
                    Id = req.Id,
                    Title = req.Title,
                    Tests = testIds,
                    Covered = testIds.Count > 0
                });
            }
        }
        else
        {
            // No requirements file — report requirements discovered from tests as a flat list
            foreach (var (reqId, testIds) in reqToTests.OrderBy(kv => kv.Key))
            {
                details.Add(new RequirementCoverageDetail
                {
                    Id = reqId,
                    Title = null,
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

        return new RequirementsCoverage
        {
            TotalRequirements = total,
            CoveredRequirements = covered,
            Percentage = percentage,
            HasRequirementsFile = hasFile,
            Details = details
        };
    }
}
