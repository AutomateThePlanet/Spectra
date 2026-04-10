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

    [JsonPropertyName("duplicate_warnings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DuplicateWarning>? DuplicateWarnings { get; init; }

    [JsonPropertyName("verification")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<VerifiedTest>? Verification { get; init; }

    [JsonPropertyName("rejected_tests")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RejectedTest>? RejectedTests { get; init; }

    [JsonPropertyName("session")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SessionCounts? Session { get; init; }
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

    /// <summary>
    /// Counts of identified behaviors grouped by ISTQB test design technique.
    /// Always serialized (as <c>{}</c> when empty) so SKILL/CI consumers see a
    /// stable contract. Keys are short codes: BVA, EP, DT, ST, EG, UC.
    /// </summary>
    [JsonPropertyName("technique_breakdown")]
    public Dictionary<string, int> TechniqueBreakdown { get; init; } = new();
}

public sealed class GenerateGeneration
{
    [JsonPropertyName("tests_requested")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TestsRequested { get; init; }

    [JsonPropertyName("tests_generated")]
    public int TestsGenerated { get; init; }

    [JsonPropertyName("tests_written")]
    public int TestsWritten { get; init; }

    [JsonPropertyName("tests_rejected_by_critic")]
    public int TestsRejectedByCritic { get; init; }

    [JsonPropertyName("batches_completed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int BatchesCompleted { get; init; }

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

public sealed class DuplicateWarning
{
    [JsonPropertyName("new_test")]
    public required string NewTest { get; init; }

    [JsonPropertyName("similar_to")]
    public required string SimilarTo { get; init; }

    [JsonPropertyName("similarity")]
    public double Similarity { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }
}

public sealed class VerifiedTest
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("verdict")]
    public required string Verdict { get; init; }

    [JsonPropertyName("score")]
    public double Score { get; init; }

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
}

public sealed class RejectedTest
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("verdict")]
    public required string Verdict { get; init; }

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
}

public sealed class SessionCounts
{
    [JsonPropertyName("from_docs")]
    public int FromDocs { get; init; }

    [JsonPropertyName("from_suggestions")]
    public int FromSuggestions { get; init; }

    [JsonPropertyName("from_description")]
    public int FromDescription { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }
}
