using Spectra.Core.Models;
using Spectra.Core.Update;

namespace Spectra.CLI.Agent.Tools;

/// <summary>
/// Tool that proposes updates for tests based on documentation changes.
/// </summary>
public sealed class BatchProposeUpdatesTool
{
    private readonly TestClassifier _classifier;

    public BatchProposeUpdatesTool(TestClassifier? classifier = null)
    {
        _classifier = classifier ?? new TestClassifier();
    }

    /// <summary>
    /// Tool name for AI function calling.
    /// </summary>
    public static string Name => "batch_propose_updates";

    /// <summary>
    /// Tool description for AI function calling.
    /// </summary>
    public static string Description =>
        "Analyzes tests against source documentation and proposes updates. " +
        "Returns classification (up-to-date, outdated, orphaned, redundant) and " +
        "proposed changes for each test.";

    /// <summary>
    /// Executes the tool and returns update proposals.
    /// </summary>
    public BatchProposeUpdatesResult Execute(
        IReadOnlyList<TestCase> tests,
        IReadOnlyDictionary<string, string> sourceContents)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(sourceContents);

        try
        {
            var classifications = _classifier.ClassifyBatch(tests, sourceContents);
            var proposals = new List<UpdateProposalItem>();

            foreach (var result in classifications)
            {
                proposals.Add(new UpdateProposalItem
                {
                    TestId = result.Test.Id,
                    TestTitle = result.Test.Title,
                    Classification = result.Classification.ToString(),
                    Confidence = result.Confidence,
                    Reason = result.Reason,
                    RelatedTestId = result.RelatedTestId,
                    ShouldUpdate = result.Classification == UpdateClassification.Outdated,
                    ShouldDelete = result.Classification == UpdateClassification.Orphaned ||
                                   result.Classification == UpdateClassification.Redundant,
                    NoActionNeeded = result.Classification == UpdateClassification.UpToDate
                });
            }

            var summary = new UpdateSummary
            {
                TotalTests = tests.Count,
                UpToDateCount = proposals.Count(p => p.Classification == "UpToDate"),
                OutdatedCount = proposals.Count(p => p.Classification == "Outdated"),
                OrphanedCount = proposals.Count(p => p.Classification == "Orphaned"),
                RedundantCount = proposals.Count(p => p.Classification == "Redundant")
            };

            return new BatchProposeUpdatesResult
            {
                Success = true,
                Proposals = proposals,
                Summary = summary
            };
        }
        catch (Exception ex)
        {
            return new BatchProposeUpdatesResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Result of the batch_propose_updates tool.
/// </summary>
public sealed record BatchProposeUpdatesResult
{
    public required bool Success { get; init; }
    public IReadOnlyList<UpdateProposalItem>? Proposals { get; init; }
    public UpdateSummary? Summary { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Update proposal for a single test.
/// </summary>
public sealed record UpdateProposalItem
{
    public required string TestId { get; init; }
    public required string TestTitle { get; init; }
    public required string Classification { get; init; }
    public required double Confidence { get; init; }
    public required string Reason { get; init; }
    public string? RelatedTestId { get; init; }
    public required bool ShouldUpdate { get; init; }
    public required bool ShouldDelete { get; init; }
    public required bool NoActionNeeded { get; init; }
}

/// <summary>
/// Summary of update proposals.
/// </summary>
public sealed record UpdateSummary
{
    public required int TotalTests { get; init; }
    public required int UpToDateCount { get; init; }
    public required int OutdatedCount { get; init; }
    public required int OrphanedCount { get; init; }
    public required int RedundantCount { get; init; }
}
