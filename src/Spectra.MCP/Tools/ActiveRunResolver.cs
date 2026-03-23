using System.Text.Json;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools;

/// <summary>
/// Shared helper for auto-resolving run_id and test_handle when omitted by callers.
/// Enables weaker models (GPT-4.1, GPT-4o) that call tools with empty {} input.
/// </summary>
public static class ActiveRunResolver
{
    /// <summary>
    /// Resolves a run_id. If provided, returns it directly. If omitted, auto-detects
    /// from active runs: returns the single active run's ID, or an error JSON string
    /// if 0 or 2+ active runs exist.
    /// </summary>
    /// <returns>(resolvedRunId, errorJson) — exactly one is non-null.</returns>
    public static async Task<(string? RunId, string? ErrorJson)> ResolveRunIdAsync(
        string? runId,
        RunRepository runRepo)
    {
        if (!string.IsNullOrEmpty(runId))
            return (runId, null);

        var activeRuns = await runRepo.GetActiveRunsAsync();

        if (activeRuns.Count == 0)
        {
            var error = JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "NO_ACTIVE_RUNS",
                "No active runs found. Use start_execution_run to begin a new execution.",
                nextExpectedAction: "start_execution_run"));
            return (null, error);
        }

        if (activeRuns.Count == 1)
            return (activeRuns[0].RunId, null);

        // Multiple active runs — list them for the caller
        var listing = string.Join("\n", activeRuns.Select(r =>
            $"- {r.RunId} | suite: {r.Suite} | status: {r.Status} | started: {r.StartedAt:O}"));

        var multiError = JsonSerializer.Serialize(McpToolResponse<object>.Failure(
            "MULTIPLE_ACTIVE_RUNS",
            $"Multiple active runs found. Please specify run_id:\n{listing}",
            nextExpectedAction: "list_active_runs"));
        return (null, multiError);
    }

    /// <summary>
    /// Resolves a test_handle within a run. If provided, returns it directly. If omitted,
    /// auto-detects from in-progress tests in the given run.
    /// </summary>
    /// <returns>(resolvedTestHandle, errorJson) — exactly one is non-null.</returns>
    public static async Task<(string? TestHandle, string? ErrorJson)> ResolveTestHandleAsync(
        string? testHandle,
        string runId,
        ResultRepository resultRepo)
    {
        if (!string.IsNullOrEmpty(testHandle))
            return (testHandle, null);

        var inProgress = await resultRepo.GetInProgressTestsAsync(runId);

        if (inProgress.Count == 0)
        {
            var error = JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "NO_TEST_IN_PROGRESS",
                "No test currently in progress. Use get_execution_status to see the next test.",
                nextExpectedAction: "get_execution_status"));
            return (null, error);
        }

        if (inProgress.Count == 1)
            return (inProgress[0].TestHandle, null);

        // Multiple in-progress tests — list them
        var listing = string.Join("\n", inProgress.Select(t =>
            $"- {t.TestHandle} | {t.TestId}"));

        var multiError = JsonSerializer.Serialize(McpToolResponse<object>.Failure(
            "MULTIPLE_TESTS_IN_PROGRESS",
            $"Multiple tests in progress. Please specify test_handle:\n{listing}"));
        return (null, multiError);
    }
}
