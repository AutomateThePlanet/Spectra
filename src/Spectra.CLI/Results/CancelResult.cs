using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class CancelResult : CommandResult
{
    [JsonPropertyName("target_pid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TargetPid { get; init; }

    [JsonPropertyName("target_command")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetCommand { get; init; }

    [JsonPropertyName("shutdown_path")]
    public required string ShutdownPath { get; init; }

    [JsonPropertyName("elapsed_seconds")]
    public double ElapsedSeconds { get; init; }

    [JsonPropertyName("force")]
    public bool Force { get; init; }
}
