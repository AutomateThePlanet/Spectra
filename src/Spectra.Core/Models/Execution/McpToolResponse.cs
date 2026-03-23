using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Standard wrapper for all MCP tool responses.
/// Provides self-contained context per Constitution Principle III.
/// </summary>
/// <typeparam name="T">Type of the data payload.</typeparam>
public sealed record McpToolResponse<T>
{
    /// <summary>Tool-specific result data.</summary>
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    /// <summary>Current run state (when run exists).</summary>
    [JsonPropertyName("run_status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RunStatus? RunStatus { get; init; }

    /// <summary>Progress in "completed/total" format.</summary>
    [JsonPropertyName("progress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Progress { get; init; }

    /// <summary>Suggested next tool call.</summary>
    [JsonPropertyName("next_expected_action")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextExpectedAction { get; init; }

    /// <summary>Human-readable instruction for the AI model on what to do next. Survives context compaction.</summary>
    [JsonPropertyName("instruction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Instruction { get; init; }

    /// <summary>Error details if failed.</summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorInfo? Error { get; init; }

    /// <summary>Current run status for error responses.</summary>
    [JsonPropertyName("current_run_status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RunStatus? CurrentRunStatus { get; init; }

    /// <summary>
    /// Creates a successful response with data.
    /// </summary>
    public static McpToolResponse<T> Success(
        T data,
        RunStatus? runStatus = null,
        string? progress = null,
        string? nextExpectedAction = null,
        string? instruction = null)
    {
        return new McpToolResponse<T>
        {
            Data = data,
            RunStatus = runStatus,
            Progress = progress,
            NextExpectedAction = nextExpectedAction,
            Instruction = instruction
        };
    }

    /// <summary>
    /// Creates an error response.
    /// </summary>
    public static McpToolResponse<T> Failure(
        string code,
        string message,
        RunStatus? currentRunStatus = null,
        string? nextExpectedAction = null)
    {
        return new McpToolResponse<T>
        {
            Error = new ErrorInfo { Code = code, Message = message },
            CurrentRunStatus = currentRunStatus,
            NextExpectedAction = nextExpectedAction
        };
    }
}
