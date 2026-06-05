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

    /// <summary>
    /// Non-blocking notices (Spec 048 pattern). Spec 058: names any retired provider-config keys
    /// still present in <c>spectra.config.json</c> so they are surfaced rather than silently ignored.
    /// </summary>
    [JsonPropertyName("notes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Notes { get; init; }
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
