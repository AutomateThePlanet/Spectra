using Spectra.Core.Models;
using Spectra.Core.Models.Execution;

namespace Spectra.MCP.Reports;

/// <summary>
/// Generates execution reports from run data.
/// </summary>
public sealed class ReportGenerator
{
    /// <summary>
    /// Generates an execution report from a completed run.
    /// </summary>
    public ExecutionReport Generate(
        Run run,
        IEnumerable<TestResult> results,
        IReadOnlyDictionary<string, string>? testTitles = null,
        IReadOnlyDictionary<string, TestCase>? testCases = null)
    {
        var resultsList = results.ToList();

        // Count all attempts in the summary (per T085: show all attempts)
        var statusCounts = new Dictionary<TestStatus, int>();
        foreach (var result in resultsList)
        {
            statusCounts.TryAdd(result.Status, 0);
            statusCounts[result.Status]++;
        }

        var summary = ReportSummary.FromCounts(statusCounts);

        // Include all attempts in the results, ordered by test ID then attempt
        var entries = resultsList
            .OrderBy(r => r.TestId)
            .ThenBy(r => r.Attempt)
            .Select(r =>
            {
                var tc = testCases?.GetValueOrDefault(r.TestId);
                return new TestResultEntry
                {
                    TestId = r.TestId,
                    Title = testTitles?.GetValueOrDefault(r.TestId) ?? r.TestId,
                    Status = r.Status,
                    Attempt = r.Attempt,
                    DurationMs = CalculateDurationMs(r.StartedAt, r.CompletedAt),
                    Notes = r.Notes,
                    BlockedBy = r.BlockedBy,
                    Preconditions = tc?.Preconditions,
                    Steps = tc?.Steps is { Count: > 0 } ? tc.Steps : null,
                    ExpectedResult = tc?.ExpectedResult,
                    TestData = tc?.TestData,
                    ScreenshotPaths = r.ScreenshotPaths
                };
            }).ToList();

        return new ExecutionReport
        {
            RunId = run.RunId,
            Suite = run.Suite,
            Environment = run.Environment,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt ?? DateTime.UtcNow,
            ExecutedBy = run.StartedBy,
            Status = run.Status,
            Summary = summary,
            Results = entries,
            Filters = run.Filters
        };
    }

    /// <summary>
    /// Generates a summary from status counts.
    /// </summary>
    public ReportSummary GenerateSummary(Dictionary<TestStatus, int> statusCounts)
    {
        return ReportSummary.FromCounts(statusCounts);
    }

    /// <summary>
    /// Calculates duration in milliseconds, normalizing timestamps to UTC.
    /// </summary>
    private static long? CalculateDurationMs(DateTime? startedAt, DateTime? completedAt)
    {
        if (!startedAt.HasValue || !completedAt.HasValue)
            return null;

        // Normalize both to UTC to avoid timezone issues
        var startUtc = startedAt.Value.ToUniversalTime();
        var endUtc = completedAt.Value.ToUniversalTime();

        var duration = endUtc - startUtc;

        // Guard against negative durations (clock skew, etc.)
        if (duration < TimeSpan.Zero)
            return 0;

        return (long)duration.TotalMilliseconds;
    }
}
