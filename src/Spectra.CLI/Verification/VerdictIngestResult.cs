using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Verification;

/// <summary>
/// Typed outcome of ingesting a critic response at the fail-loud boundary (Spec 055).
/// </summary>
public enum VerdictIngestOutcome
{
    /// <summary>A well-formed critic response was classified into a <see cref="VerificationResult"/>.</summary>
    Verdict,

    /// <summary>The response was empty/whitespace — damage, fail loud (never a verdict).</summary>
    EmptyResponse,

    /// <summary>
    /// Missing/unparseable <c>verdict</c> or <c>score</c>, or non-JSON — damage, fail loud.
    /// Replaces the old silent <c>Partial</c>/<c>0.5</c> default (FR-006).
    /// </summary>
    ParseFailure
}

/// <summary>
/// Result of <see cref="VerdictIngestor.Classify"/> (Spec 055 FR-006/FR-007). The verdict stays
/// advisory-gating (only <see cref="VerificationVerdict.Hallucinated"/> drops), while damage
/// (empty / missing-field / unparseable) is surfaced as a typed failure with a specific error —
/// never coerced into a soft pass.
///
/// Distinct from a critic <i>call</i> failure: an exception/timeout is the runtime's
/// Unverified-style result on the retained in-process path and is never routed through
/// <see cref="VerdictIngestor.Classify"/>, so failure and parse-miss are never conflated (FR-007).
///
/// Reuses the verbatim <see cref="VerificationResult"/>/<see cref="VerificationVerdict"/> from
/// <c>Spectra.Core</c>.
/// </summary>
public sealed record VerdictIngestResult
{
    /// <summary>The typed classification.</summary>
    public VerdictIngestOutcome Outcome { get; private init; }

    /// <summary>True only when a well-formed verdict was classified.</summary>
    public bool IsSuccess => Outcome == VerdictIngestOutcome.Verdict;

    /// <summary>The parsed verdict/score/findings on <see cref="VerdictIngestOutcome.Verdict"/>; null otherwise.</summary>
    public VerificationResult? Result { get; private init; }

    /// <summary>
    /// Gate decision: a test drops iff the critic verdict is
    /// <see cref="VerificationVerdict.Hallucinated"/>. False for every non-verdict outcome —
    /// damage and failure never silently drop a test (FR-005).
    /// </summary>
    public bool Drops => Result?.Verdict == VerificationVerdict.Hallucinated;

    /// <summary>Specific error(s) on a damage outcome; empty on success.</summary>
    public IReadOnlyList<string> Errors { get; private init; } = [];

    private VerdictIngestResult() { }

    /// <summary>Creates a success carrying the classified verdict.</summary>
    public static VerdictIngestResult FromVerdict(VerificationResult result) => new()
    {
        Outcome = VerdictIngestOutcome.Verdict,
        Result = result ?? throw new ArgumentNullException(nameof(result))
    };

    /// <summary>Creates a fail-loud damage result with specific error(s). Never <see cref="VerdictIngestOutcome.Verdict"/>.</summary>
    public static VerdictIngestResult Failure(VerdictIngestOutcome outcome, params string[] errors)
    {
        if (outcome == VerdictIngestOutcome.Verdict)
            throw new ArgumentException("Failure outcome cannot be Verdict.", nameof(outcome));
        return new() { Outcome = outcome, Errors = errors ?? [] };
    }
}
