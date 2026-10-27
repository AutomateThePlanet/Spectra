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

    /// <summary>Spec 097 (FR-006): <c>--ids</c> not in the suite's index; only when <c>--ids</c> was given.</summary>
    [JsonPropertyName("not_found")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? NotFound { get; init; }
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

    /// <summary>
    /// Spec 097 (FR-007): true when the verdict fails the 090/091 freshness check (the ingest would refuse it as
    /// <c>VERDICT_STALE</c>); then <see cref="ActionNeeded"/> is <c>critic</c>. Omitted otherwise.
    /// </summary>
    [JsonPropertyName("stale")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Stale { get; init; }

    // --- Spec 103 (FR-011/FR-018): the review record and suppression state. Additive: action_needed is unchanged. ---

    [JsonPropertyName("reviewed_by")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReviewedBy { get; init; }

    [JsonPropertyName("reviewed_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReviewedAt { get; init; }

    /// <summary>The reviewer's note; only with a review record.</summary>
    [JsonPropertyName("note")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; init; }

    /// <summary>Non-null means a re-raise is being withheld right now (never together with a flag).</summary>
    [JsonPropertyName("reraise_suppressed_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReraiseSuppressedAt { get; init; }

    /// <summary>Set for the impossible flagged-plus-marker combination (a hand-edited file).</summary>
    [JsonPropertyName("inconsistent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Inconsistent { get; init; }

    /// <summary>True when verification is newer than the sign-off and nothing is withheld: the next partial re-raises.</summary>
    [JsonPropertyName("review_stale")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ReviewStale { get; init; }
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

    /// <summary>Spec 090 (FR-A7): tests with neither a verdict file nor a grounding block.</summary>
    [JsonPropertyName("unverified")]
    public int Unverified { get; init; }

    /// <summary>Spec 098 (FR-007): tests with a fresh grounded verdict whose block isn't written yet (<c>ground</c>).</summary>
    [JsonPropertyName("pending_ground")]
    public int PendingGround { get; init; }

    /// <summary>Spec 098 (FR-007): tests with a fresh hallucinated verdict and no block — waiting for the user (<c>review</c>).</summary>
    [JsonPropertyName("pending_review")]
    public int PendingReview { get; init; }

    /// <summary>Spec 097 (FR-007): entries whose verdict is stale.</summary>
    [JsonPropertyName("stale")]
    public int Stale { get; init; }

    /// <summary>Spec 103: tests a human cleared (a review record, not flagged). The three review counts are omitted at 0.</summary>
    [JsonPropertyName("reviewed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Reviewed { get; init; }

    /// <summary>Spec 103 (FR-011): concerns being withheld right now.</summary>
    [JsonPropertyName("reraises_suppressed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ReraisesSuppressed { get; init; }

    /// <summary>Spec 103: sign-offs verification has overtaken with nothing withheld (disjoint from the above).</summary>
    [JsonPropertyName("reviews_stale")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ReviewsStale { get; init; }
}
