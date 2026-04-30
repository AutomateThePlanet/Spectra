using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

/// <summary>
/// Base result for all CLI command JSON output.
/// </summary>
/// <remarks>
/// Status values are documented strings (no code-level enum). Common values:
/// <list type="bullet">
///   <item><c>completed</c> — operation finished successfully.</item>
///   <item><c>failed</c> — operation hit a fatal error.</item>
///   <item><c>cancelled</c> — operation halted by user request (Spec 040). Partial results may be present.</item>
///   <item><c>no_active_run</c> — <c>spectra cancel</c> invoked when nothing was running (Spec 040). Not an error.</item>
/// </list>
/// Per-command intermediate values (e.g., <c>analyzing</c>, <c>verifying</c>, <c>analyzed</c>) are also valid.
/// </remarks>
public class CommandResult
{
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = DateTimeOffset.UtcNow.ToString("o");

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }
}

/// <summary>
/// Error result for failed commands or missing arguments.
/// </summary>
public sealed class ErrorResult : CommandResult
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    [JsonPropertyName("missing_arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? MissingArguments { get; init; }
}
