using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class ValidateResult : CommandResult
{
    [JsonPropertyName("total_files")]
    public int TotalFiles { get; init; }

    [JsonPropertyName("valid")]
    public int Valid { get; init; }

    [JsonPropertyName("errors")]
    public required IReadOnlyList<ValidationError> Errors { get; init; }
}

public sealed class ValidationError
{
    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
