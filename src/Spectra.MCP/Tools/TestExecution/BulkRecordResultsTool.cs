using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: bulk_record_results
/// Records results for multiple tests in a single call.
/// Supports: skip all remaining, pass all remaining, fail all remaining, or specific test IDs.
/// </summary>
public sealed class BulkRecordResultsTool : IMcpTool
{
    private readonly ExecutionEngine _engine;

    public string Description => "Records results for multiple tests at once. Use 'remaining: true' to process all pending tests, or specify test_ids array.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            status = new
            {
                type = "string",
                @enum = new[] { "PASSED", "FAILED", "SKIPPED", "BLOCKED" },
                description = "Status to apply to all tests"
            },
            remaining = new
            {
                type = "boolean",
                description = "If true, applies to all remaining pending/in-progress tests",
                @default = false
            },
            test_ids = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Specific test IDs to process (ignored if remaining=true)"
            },
            reason = new
            {
                type = "string",
                description = "Reason/notes (REQUIRED for FAILED, SKIPPED, BLOCKED)"
            }
        },
        required = new[] { "status" }
    };

    public BulkRecordResultsTool(ExecutionEngine engine)
    {
        _engine = engine;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<BulkRecordRequest>(parameters);
        if (request is null || string.IsNullOrEmpty(request.Status))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "status is required"));
        }

        if (!Enum.TryParse<TestStatus>(request.Status, true, out var status))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_STATUS",
                "Status must be PASSED, FAILED, SKIPPED, or BLOCKED"));
        }

        // Reason is required for non-passing statuses
        if (status != TestStatus.Passed && string.IsNullOrWhiteSpace(request.Reason))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "REASON_REQUIRED",
                $"Reason is required for {status} status"));
        }

        // Must specify either remaining=true or test_ids
        if (!request.Remaining && (request.TestIds == null || request.TestIds.Count == 0))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "Either set remaining=true or provide test_ids array"));
        }

        // Get the active run
        var activeRun = await _engine.GetActiveRunAsync();
        if (activeRun is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "NO_ACTIVE_RUN",
                "No active execution run found"));
        }

        var runId = activeRun.RunId;
        var queue = await _engine.GetQueueAsync(runId);
        if (queue is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "QUEUE_NOT_FOUND",
                "Run queue not found"));
        }

        // Determine which tests to process
        var testsToProcess = new List<QueuedTest>();

        if (request.Remaining)
        {
            // Get all pending and in-progress tests
            testsToProcess = queue.Tests
                .Where(t => t.Status == TestStatus.Pending || t.Status == TestStatus.InProgress)
                .ToList();
        }
        else
        {
            // Get specific tests by ID
            foreach (var testId in request.TestIds!)
            {
                var test = queue.GetById(testId);
                if (test is not null && (test.Status == TestStatus.Pending || test.Status == TestStatus.InProgress))
                {
                    testsToProcess.Add(test);
                }
            }
        }

        if (testsToProcess.Count == 0)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "NO_TESTS_TO_PROCESS",
                "No pending or in-progress tests found to process"));
        }

        try
        {
            var results = await _engine.BulkRecordResultsAsync(
                runId,
                testsToProcess.Select(t => t.TestHandle),
                status,
                request.Reason);

            var progress = queue.GetProgress();
            var next = queue.GetNext();

            var data = new
            {
                processed_count = results.ProcessedCount,
                status = status.ToString().ToUpperInvariant(),
                processed_tests = results.ProcessedTests.Select(t => new
                {
                    test_id = t.TestId,
                    status = t.Status.ToString().ToUpperInvariant()
                }),
                blocked_tests = results.BlockedTests,
                next = next is not null ? new
                {
                    test_handle = next.TestHandle,
                    test_id = next.TestId,
                    title = next.Title
                } : null
            };

            var nextAction = next is not null ? "get_test_case_details" : "finalize_execution_run";

            return JsonSerializer.Serialize(McpToolResponse<object>.Success(
                data,
                activeRun.Status,
                progress,
                nextAction));
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "BULK_OPERATION_FAILED",
                ex.Message,
                activeRun.Status));
        }
    }
}

internal sealed class BulkRecordRequest
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("remaining")]
    public bool Remaining { get; set; }

    [JsonPropertyName("test_ids")]
    public List<string>? TestIds { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
