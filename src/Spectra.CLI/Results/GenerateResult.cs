using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class GenerateResult : CommandResult
{
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("analysis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GenerateAnalysis? Analysis { get; init; }

    [JsonPropertyName("generation")]
    public required GenerateGeneration Generation { get; init; }

    [JsonPropertyName("suggestions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<GenerateSuggestion>? Suggestions { get; init; }

    [JsonPropertyName("files_created")]
    public required IReadOnlyList<string> FilesCreated { get; init; }
}

public sealed class GenerateAnalysis
{
    [JsonPropertyName("total_behaviors")]
    public int TotalBehaviors { get; init; }

    [JsonPropertyName("already_covered")]
    public int AlreadyCovered { get; init; }

    [JsonPropertyName("recommended")]
    public int Recommended { get; init; }

    [JsonPropertyName("breakdown")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? Breakdown { get; init; }
}

public sealed class GenerateGeneration
{
    [JsonPropertyName("tests_generated")]
    public int TestsGenerated { get; init; }

    [JsonPropertyName("tests_written")]
    public int TestsWritten { get; init; }

    [JsonPropertyName("tests_rejected_by_critic")]
    public int TestsRejectedByCritic { get; init; }

    [JsonPropertyName("grounding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GroundingCounts? Grounding { get; init; }
}

public sealed class GroundingCounts
{
    [JsonPropertyName("grounded")]
    public int Grounded { get; init; }

    [JsonPropertyName("partial")]
    public int Partial { get; init; }

    [JsonPropertyName("hallucinated")]
    public int Hallucinated { get; init; }
}

public sealed class GenerateSuggestion
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("category")]
    public required string Category { get; init; }
}
