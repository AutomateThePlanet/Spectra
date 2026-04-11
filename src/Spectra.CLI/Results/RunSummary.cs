using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

/// <summary>
/// Run-context block shown at the top of the Run Summary panel and
/// serialized as <c>run_summary</c> in --output-format json and
/// .spectra-result.json (Spec 040).
///
/// Generate-specific and update-specific fields are all nullable so the
/// same DTO serves both commands; only the populated fields are written
/// (via <see cref="JsonIgnoreCondition.WhenWritingNull"/>).
/// </summary>
public sealed class RunSummary
{
    // Generate-specific
    [JsonPropertyName("documents_processed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DocumentsProcessed { get; init; }

    [JsonPropertyName("behaviors_identified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BehaviorsIdentified { get; init; }

    [JsonPropertyName("tests_generated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TestsGenerated { get; init; }

    [JsonPropertyName("verdicts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RunSummaryVerdicts? Verdicts { get; init; }

    [JsonPropertyName("batch_size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BatchSize { get; init; }

    [JsonPropertyName("batches")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Batches { get; init; }

    // Update-specific
    [JsonPropertyName("tests_scanned")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TestsScanned { get; init; }

    [JsonPropertyName("tests_updated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TestsUpdated { get; init; }

    [JsonPropertyName("tests_unchanged")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TestsUnchanged { get; init; }

    [JsonPropertyName("classifications")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? Classifications { get; init; }

    [JsonPropertyName("chunks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Chunks { get; init; }

    // Shared
    [JsonPropertyName("duration_seconds")]
    public double DurationSeconds { get; init; }
}

public sealed class RunSummaryVerdicts
{
    [JsonPropertyName("grounded")]
    public int Grounded { get; init; }

    [JsonPropertyName("partial")]
    public int Partial { get; init; }

    [JsonPropertyName("hallucinated")]
    public int Hallucinated { get; init; }
}
