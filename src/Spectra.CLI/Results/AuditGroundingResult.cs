using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class AuditGroundingResult : CommandResult
{
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("tests")]
    public required IReadOnlyList<AuditGroundingEntry> Tests { get; init; }

    [JsonPropertyName("summary")]
    public required AuditGroundingSummary Summary { get; init; }
}

public sealed class AuditGroundingEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("verdict")]
    public required string Verdict { get; init; }

    [JsonPropertyName("score")]
    public required double Score { get; init; }

    [JsonPropertyName("grounding_written")]
    public required bool GroundingWritten { get; init; }

    [JsonPropertyName("flagged_for_review")]
    public required bool FlaggedForReview { get; init; }

    [JsonPropertyName("action_needed")]
    public required string ActionNeeded { get; init; }

    [JsonPropertyName("file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? File { get; init; }
}

public sealed class AuditGroundingSummary
{
    [JsonPropertyName("total")]
    public required int Total { get; init; }

    [JsonPropertyName("grounding_written")]
    public required int GroundingWritten { get; init; }

    [JsonPropertyName("partial_pending_repair")]
    public required int PartialPendingRepair { get; init; }

    [JsonPropertyName("flagged_for_review")]
    public required int FlaggedForReview { get; init; }
}
