using System.Text.Json.Serialization;

namespace Spectra.CLI.Progress;

/// <summary>
/// Spec 041: in-flight progress snapshot attached to GenerateResult / UpdateResult
/// while a long-running command is executing. Cleared by ProgressManager on
/// Complete() / Fail() so the final result file shows runSummary instead.
/// </summary>
public sealed class ProgressSnapshot
{
    [JsonPropertyName("phase")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProgressPhase Phase { get; set; }

    [JsonPropertyName("testsTarget")]
    public int TestsTarget { get; set; }

    [JsonPropertyName("testsGenerated")]
    public int TestsGenerated { get; set; }

    [JsonPropertyName("testsVerified")]
    public int TestsVerified { get; set; }

    [JsonPropertyName("currentBatch")]
    public int CurrentBatch { get; set; }

    [JsonPropertyName("totalBatches")]
    public int TotalBatches { get; set; }

    [JsonPropertyName("lastTestId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastTestId { get; set; }

    [JsonPropertyName("lastVerdict")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastVerdict { get; set; }
}

public enum ProgressPhase
{
    Generating,
    Verifying,
    Updating
}
