using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: add_test_note
/// Adds a note without changing status.
/// </summary>
public sealed class AddTestNoteTool : IMcpTool
{
    private readonly ExecutionEngine _engine;

    public string Description => "Adds a note to a test without changing its status";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            test_handle = new { type = "string", description = "Test handle to add note to" },
            note = new { type = "string", description = "Note text" }
        },
        required = new[] { "test_handle", "note" }
    };

    public AddTestNoteTool(ExecutionEngine engine)
    {
        _engine = engine;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<AddTestNoteRequest>(parameters);
        if (request is null || string.IsNullOrEmpty(request.TestHandle))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "test_handle is required"));
        }

        if (string.IsNullOrEmpty(request.Note))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "note is required"));
        }

        var result = await _engine.GetTestResultAsync(request.TestHandle);
        if (result is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_HANDLE",
                $"Test handle '{request.TestHandle}' not found"));
        }

        var run = await _engine.GetRunAsync(result.RunId);
        if (run is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                "Run not found"));
        }

        await _engine.AddNoteAsync(request.TestHandle, request.Note);

        // Get updated result to count notes
        var updated = await _engine.GetTestResultAsync(request.TestHandle);
        var noteCount = updated?.Notes?.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length ?? 1;

        var queue = await _engine.GetQueueAsync(result.RunId);
        var progress = queue?.GetProgress() ?? "?/?";

        var data = new
        {
            test_id = result.TestId,
            note_added = true,
            total_notes = noteCount
        };

        var nextAction = result.Status == TestStatus.InProgress ? "advance_test_case" : "get_test_case_details";

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(
            data,
            run.Status,
            progress,
            nextAction));
    }
}

internal sealed class AddTestNoteRequest
{
    [JsonPropertyName("test_handle")]
    public string? TestHandle { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}
