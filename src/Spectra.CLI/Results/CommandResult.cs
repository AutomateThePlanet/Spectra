using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

/// <summary>
/// Base result for all CLI command JSON output.
/// </summary>
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
