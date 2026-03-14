using Spectra.Core.Models;
using Spectra.Core.Validation;

namespace Spectra.CLI.Agent.Tools;

/// <summary>
/// Tool that checks for duplicate tests in batch.
/// </summary>
public sealed class CheckDuplicatesBatchTool
{
    private readonly DuplicateDetector _detector;

    public CheckDuplicatesBatchTool(DuplicateDetector detector)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
    }

    /// <summary>
    /// Tool name for AI function calling.
    /// </summary>
    public static string Name => "check_duplicates_batch";

    /// <summary>
    /// Tool description for AI function calling.
    /// </summary>
    public static string Description =>
        "Checks a batch of proposed tests for duplicates against existing tests. " +
        "Returns similarity scores and flags potential duplicates.";

    /// <summary>
    /// Executes the tool and returns duplicate check results.
    /// </summary>
    public CheckDuplicatesBatchResult Execute(
        IReadOnlyList<TestCase> proposedTests,
        IReadOnlyList<TestCase> existingTests)
    {
        ArgumentNullException.ThrowIfNull(proposedTests);
        ArgumentNullException.ThrowIfNull(existingTests);

        try
        {
            var results = new List<DuplicateCheckItem>();

            foreach (var proposed in proposedTests)
            {
                var duplicates = _detector.FindDuplicates(proposed, existingTests);

                results.Add(new DuplicateCheckItem
                {
                    TestId = proposed.Id,
                    TestTitle = proposed.Title,
                    IsDuplicate = duplicates.Count > 0,
                    PotentialDuplicates = duplicates.Select(d => new DuplicateMatch
                    {
                        MatchedTestId = d.MatchedTestId,
                        MatchedTestTitle = d.MatchedTestTitle,
                        Similarity = d.Similarity
                    }).ToList()
                });
            }

            return new CheckDuplicatesBatchResult
            {
                Success = true,
                Results = results,
                TotalChecked = proposedTests.Count,
                DuplicatesFound = results.Count(r => r.IsDuplicate)
            };
        }
        catch (Exception ex)
        {
            return new CheckDuplicatesBatchResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Result of the check_duplicates_batch tool.
/// </summary>
public sealed record CheckDuplicatesBatchResult
{
    public required bool Success { get; init; }
    public IReadOnlyList<DuplicateCheckItem>? Results { get; init; }
    public int TotalChecked { get; init; }
    public int DuplicatesFound { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Duplicate check result for a single test.
/// </summary>
public sealed record DuplicateCheckItem
{
    public required string TestId { get; init; }
    public required string TestTitle { get; init; }
    public required bool IsDuplicate { get; init; }
    public required IReadOnlyList<DuplicateMatch> PotentialDuplicates { get; init; }
}

/// <summary>
/// A potential duplicate match.
/// </summary>
public sealed record DuplicateMatch
{
    public required string MatchedTestId { get; init; }
    public required string MatchedTestTitle { get; init; }
    public required double Similarity { get; init; }
}
