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

    // --- Spec 078 FR-011/FR-018: the review record and suppression state ---

    [JsonPropertyName("reviewed_by")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReviewedBy { get; init; }

    [JsonPropertyName("reviewed_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReviewedAt { get; init; }

    [JsonPropertyName("note")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; init; }

    /// <summary>
    /// Non-null means a re-raise is being withheld <b>right now</b>. Mutually exclusive with
    /// <see cref="FlaggedForReview"/>; the two together indicate a hand-edited or corrupt file.
    /// </summary>
    [JsonPropertyName("reraise_suppressed_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReraiseSuppressedAt { get; init; }

    /// <summary>
    /// Set when the entry carries the impossible flagged-plus-marker combination. Additive — it never
    /// changes <see cref="ActionNeeded"/>, because a flagged test needs a human regardless.
    /// </summary>
    [JsonPropertyName("inconsistent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Inconsistent { get; init; }

    /// <summary>
    /// True when verification has overtaken the sign-off and nothing is being withheld — so the next
    /// partial verdict will re-raise. Disjoint from <see cref="ReraiseSuppressedAt"/> by construction.
    /// </summary>
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

    // --- Spec 078: the three additive counters. The existing four keep their meanings. ---

    /// <summary>Tests a human cleared — <c>action_needed: "reviewed"</c> rather than <c>"none"</c>.</summary>
    [JsonPropertyName("reviewed")]
    public int Reviewed { get; init; }

    /// <summary>
    /// Concerns being withheld right now. <c>0</c> on a suite with known review records is the
    /// signature of the FR-010 baseline bug.
    /// </summary>
    [JsonPropertyName("reraises_suppressed")]
    public int ReraisesSuppressed { get; init; }

    /// <summary>
    /// Sign-offs verification has overtaken with nothing withheld. Disjoint from
    /// <see cref="ReraisesSuppressed"/> — the null-marker clause is what stops double-counting.
    /// </summary>
    [JsonPropertyName("reviews_stale")]
    public int ReviewsStale { get; init; }
}
