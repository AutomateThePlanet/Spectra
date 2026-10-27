using Spectra.CLI.Verification;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;
using Spectra.Core.Parsing;

namespace Spectra.CLI.IO;

/// <summary>
/// Reads an existing test from disk, builds GroundingMetadata from a verdict JSON classification,
/// and rewrites the test file with the new grounding block via TestFileWriter.
/// Refuses hallucinated verdicts (exit 4 — those must go through record-drop + delete instead).
/// </summary>
public sealed class GroundingWriteBackService
{
    private readonly TestFileWriter _writer;

    public GroundingWriteBackService(TestFileWriter writer)
    {
        _writer = writer;
    }

    public enum WriteBackOutcome { Success, HallucinatedRefused, TestNotFound, VerdictFailure }

    public sealed record WriteBackResult
    {
        public WriteBackOutcome Outcome { get; init; }
        public string? ErrorMessage { get; init; }
        public GroundingMetadata? Grounding { get; init; }
        public bool IsSuccess => Outcome == WriteBackOutcome.Success;

        // --- Spec 103 FR-011: suppression must be observable, never silent ---

        /// <summary>True when this pass withheld an automatic re-raise because a newer review stood.</summary>
        public bool ReraiseSuppressed { get; init; }

        /// <summary>Timestamp of the review that caused the suppression, for reporting.</summary>
        public DateTimeOffset? SuppressedByReviewAt { get; init; }

        /// <summary>True when the test carried a review record that this pass carried forward.</summary>
        public bool ReviewRecordPreserved { get; init; }

        /// <summary>Spec 103 (M1): true when the staged <c>feedback --fix</c> review was written on this pass.</summary>
        public bool PendingReviewApplied { get; init; }
    }

    /// <summary>
    /// Classifies the verdict JSON, builds GroundingMetadata, and rewrites the test file. Spec 097 (FR-017): with
    /// <paramref name="dryRun"/> the same result is returned and nothing is written.
    /// </summary>
    /// <param name="flaggedForReview">
    /// Spec 103 FR-008: lets a caller request the awaiting-review marker explicitly, instead of it
    /// being derived mechanically from the verdict. This is what allows a human to flag a
    /// confidently-verified test. When null, the historical derivation applies
    /// (<c>partial &amp;&amp; !repaired</c>). A suppression decision can still override it to false.
    /// </param>
    /// <param name="pendingReview">
    /// Spec 103 (M1): the review staged by <c>review-flagged --action feedback --fix</c>. The repair's
    /// <c>ingest-update</c> drops the old critic block (Spec 090), and the review record with it, so it is applied
    /// here — only when the test has no grounding block — and the result says so.
    /// </param>
    public async Task<WriteBackResult> WriteAsync(
        string testFilePath,
        string verdictJson,
        int repairAttempts = 0,
        bool repaired = false,
        CancellationToken ct = default,
        bool dryRun = false,
        bool? flaggedForReview = null,
        PendingReview? pendingReview = null)
    {
        if (!File.Exists(testFilePath))
            return new WriteBackResult { Outcome = WriteBackOutcome.TestNotFound, ErrorMessage = $"Test file not found: {testFilePath}" };

        var classification = VerdictIngestor.Classify(verdictJson);
        if (!classification.IsSuccess)
            return new WriteBackResult
            {
                Outcome = WriteBackOutcome.VerdictFailure,
                ErrorMessage = string.Join("; ", classification.Errors)
            };

        var result = classification.Result!;

        if (result.Verdict == VerificationVerdict.Hallucinated)
            return new WriteBackResult
            {
                Outcome = WriteBackOutcome.HallucinatedRefused,
                ErrorMessage = "Hallucinated verdict cannot have a grounding block written. Use record-drop + delete instead."
            };

        var content = await File.ReadAllTextAsync(testFilePath, ct);
        var relativePath = Path.GetFileName(testFilePath);
        var parsed = new TestCaseParser().Parse(content, relativePath);
        if (!parsed.IsSuccess || parsed.Value is null)
            return new WriteBackResult { Outcome = WriteBackOutcome.TestNotFound, ErrorMessage = $"Could not parse test file: {testFilePath}" };

        var original = parsed.Value;
        var generator = !string.IsNullOrWhiteSpace(original.Grounding?.Generator)
            ? original.Grounding.Generator
            : "claude-code-session";

        var nonGrounded = result.Findings
            .Where(f => f.Status != FindingStatus.Grounded)
            .Select(f => new CondensedFinding
            {
                Element = f.Element,
                Reason = f.Reason ?? f.Claim
            })
            .ToList();

        // ── Spec 103 FR-010: the suppression decision ─────────────────────────────────────────
        // The baseline is the verification timestamp ALREADY ON THE TEST — the value parsed from
        // disk, NOT the one this pass is about to write. Compared against the new timestamp,
        // ReviewedAt could never be greater, suppression would never fire, and the feature would
        // silently behave as if suppression did not exist at all. This is the single most
        // load-bearing line in the feature.
        var priorVerifiedAt = original.Grounding?.VerifiedAt;
        var priorFlagged = original.Grounding?.FlaggedForReview ?? false;
        var reviewedBy = original.Grounding?.ReviewedBy;
        var reviewedAt = original.Grounding?.ReviewedAt;
        var note = original.Grounding?.Note;
        var pendingApplied = original.Grounding is null && pendingReview is not null;
        if (pendingApplied)
        {
            reviewedBy = pendingReview!.ReviewedBy;
            reviewedAt = pendingReview.ReviewedAt;
            note = pendingReview.Note;
        }

        var newVerdictIsPartial = result.Verdict == VerificationVerdict.Partial;

        // The pre-write flag distinguishes a human *clearance* (suppressible) from a human *raise*
        // (never suppressible). `feedback` writes a fresh ReviewedAt and leaves VerifiedAt alone, so
        // without this clause the timestamp comparison would suppress the reviewer's own raise.
        var suppress = reviewedBy is not null
            && !priorFlagged
            && reviewedAt.HasValue
            && priorVerifiedAt.HasValue
            && reviewedAt.Value > priorVerifiedAt.Value   // equality re-raises, deliberately
            && newVerdictIsPartial;

        // ── Spec 103: a human's RAISE survives an automated pass ──────────────────────────────
        // The mirror image of suppression, and the same principle: a human decision more recent than
        // the verification stands. Re-deriving the flag purely from the new verdict silently discards
        // a flag a reviewer set through `feedback` — an FR-008 raise on a `grounded` test derives to
        // false and vanishes on the next `--re-verify` pass, which supplies no explicit flag value.
        //
        // Scoped to an UNCHANGED verdict: a pass that merely agrees with the critic's prior verdict
        // has no new information and no business overruling a reviewer. When the verdict actually
        // changes — a repair upgrading partial to grounded — re-derivation is legitimate, otherwise
        // every repaired test would stay flagged forever.
        var verdictUnchanged = original.Grounding is not null
            && original.Grounding.Verdict == result.Verdict;
        var preserveExistingRaise = priorFlagged && verdictUnchanged;

        // An explicit value from the caller is a deliberate decision and always wins.
        var callerRequestedFlag = flaggedForReview
            ?? (preserveExistingRaise || (newVerdictIsPartial && !repaired));

        var flagged = suppress ? false : callerRequestedFlag;

        // The marker means *currently* suppressed: set on suppression, cleared on every re-raise and
        // whenever there is no partial concern left to withhold. Never carried forward.
        var suppressedAtMarker = suppress ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;

        var grounding = new GroundingMetadata
        {
            Verdict = result.Verdict,
            Score = result.Score,
            Generator = generator,
            Critic = result.CriticModel,
            VerifiedAt = DateTimeOffset.UtcNow,
            UnverifiedClaims = result.Findings
                .Where(f => f.Status != FindingStatus.Grounded)
                .Select(f => $"{f.Element}: {f.Reason ?? f.Claim}")
                .ToList(),
            FlaggedForReview = flagged,
            RepairAttempts = repairAttempts,
            Repaired = repaired,
            CondensedFindings = nonGrounded,
            // Spec 103 FR-003/FR-004: the review record and provenance are carried forward unchanged
            // at the single point where critic-derived metadata is constructed, so no caller can lose
            // them by omission.
            Source = original.Grounding?.Source,
            CreatedBy = original.Grounding?.CreatedBy,
            Note = note,
            ReviewedBy = reviewedBy,
            ReviewedAt = reviewedAt,
            ReraiseSuppressedAt = suppressedAtMarker
        };

        // `with` keeps every other field (and the source layout), so only the grounding block changes.
        var updated = original with { Grounding = grounding };
        if (!dryRun)
        {
            await _writer.WriteAsync(testFilePath, updated, ct);
        }

        return new WriteBackResult
        {
            Outcome = WriteBackOutcome.Success,
            Grounding = grounding,
            ReraiseSuppressed = suppress,
            SuppressedByReviewAt = suppress ? reviewedAt : null,
            ReviewRecordPreserved = reviewedBy is not null && !pendingApplied,
            PendingReviewApplied = pendingApplied
        };
    }
}
