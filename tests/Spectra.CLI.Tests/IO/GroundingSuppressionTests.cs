using Spectra.CLI.IO;
using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.IO;

/// <summary>
/// Spec 078 (User Story 2): the review record survives automated re-verification, and a newer
/// review suppresses an automatic re-raise. Implements the invariant list in
/// contracts/ingest-grounding.md.
/// </summary>
public class GroundingSuppressionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly GroundingWriteBackService _service = new(new TestFileWriter());

    public GroundingSuppressionTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private const string PartialVerdict = """
        {"verdict":"partial","score":0.70,"critic_model":"claude-opus-5",
         "findings":[{"element":"Step 3","claim":"refund interval","status":"unverified",
                      "reason":"interval absent from documentation"}]}
        """;

    private const string GroundedVerdict = """
        {"verdict":"grounded","score":0.95,"critic_model":"claude-opus-5",
         "findings":[{"element":"Step 1","claim":"navigates","status":"grounded","evidence":"docs say so"}]}
        """;

    /// <summary>Writes a test whose grounding block is under our control, including the review record.</summary>
    private string WriteTest(
        string id,
        string verifiedAt,
        bool flagged = false,
        string? reviewedBy = null,
        string? reviewedAt = null,
        string? suppressedAt = null,
        string? note = null)
    {
        var file = Path.Combine(_root, id + ".md");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("id: " + id);
        sb.AppendLine("priority: medium");
        sb.AppendLine("criteria: []");
        sb.AppendLine("grounding:");
        sb.AppendLine("  verdict: partial");
        sb.AppendLine("  score: 0.72");
        sb.AppendLine("  generator: claude-code-session");
        sb.AppendLine("  critic: claude-opus-5");
        sb.AppendLine("  verified_at: " + verifiedAt);
        sb.AppendLine("  unverified_claims:");
        sb.AppendLine("    - \"Step 3: absent\"");
        if (flagged) sb.AppendLine("  flagged_for_review: true");
        if (note is not null) sb.AppendLine("  note: \"" + note + "\"");
        if (reviewedBy is not null) sb.AppendLine("  reviewed_by: \"" + reviewedBy + "\"");
        if (reviewedAt is not null) sb.AppendLine("  reviewed_at: " + reviewedAt);
        if (suppressedAt is not null) sb.AppendLine("  reraise_suppressed_at: " + suppressedAt);
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("# Test " + id);
        sb.AppendLine();
        sb.AppendLine("## Steps");
        sb.AppendLine();
        sb.AppendLine("1. Step one");
        sb.AppendLine();
        sb.AppendLine("## Expected Result");
        sb.AppendLine();
        sb.AppendLine("It works");

        File.WriteAllText(file, sb.ToString());
        return file;
    }

    private static TestCase Parse(string file)
    {
        var parsed = new TestCaseParser().Parse(File.ReadAllText(file), Path.GetFileName(file));
        Assert.True(parsed.IsSuccess, "test file failed to parse");
        return parsed.Value!;
    }

    // ---------- Invariant 1: THE BASELINE (the regression guard for the whole feature) ----------

    /// <summary>
    /// The comparison must use the verification timestamp ALREADY ON THE TEST, not the one this pass
    /// is about to write. Against the new value a review could never be newer, suppression would
    /// never fire, and the feature would silently behave as if it did not exist.
    /// </summary>
    [Fact]
    public async Task Suppression_ComparesAgainstPreWriteVerifiedAt_NotTheValueBeingWritten()
    {
        // review (2026-08-28) is newer than the recorded verification (2026-08-20)
        var file = WriteTest("TC-400", "2026-08-20T09:14:00Z",
            reviewedBy: "Oleg Kuzmanov", reviewedAt: "2026-08-28T10:15:00Z");

        var result = await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.ReraiseSuppressed,
            "suppression did not fire — the comparison is almost certainly using the newly-written verified_at");

        var g = Parse(file).Grounding!;
        Assert.False(g.FlaggedForReview);
        Assert.NotNull(g.ReraiseSuppressedAt);
        // The pass still advanced verified_at past the review — proving the baseline was the old value.
        Assert.True(g.VerifiedAt > g.ReviewedAt!.Value);
    }

    // ---------- Invariant 2: equality re-raises ----------

    [Fact]
    public async Task Suppression_EqualTimestamps_ReRaisesRatherThanSuppresses()
    {
        var stamp = "2026-08-28T10:15:00Z";
        var file = WriteTest("TC-401", stamp, reviewedBy: "Oleg Kuzmanov", reviewedAt: stamp);

        var result = await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None);

        Assert.False(result.ReraiseSuppressed);
        var g = Parse(file).Grounding!;
        Assert.True(g.FlaggedForReview);
        Assert.Null(g.ReraiseSuppressedAt);
    }

    // ---------- Invariant 7: no re-suppression without a new review ----------

    [Fact]
    public async Task Suppression_SecondPassWithUnchangedReview_ReRaisesAndClearsMarker()
    {
        var file = WriteTest("TC-402", "2026-08-20T09:14:00Z",
            reviewedBy: "Oleg Kuzmanov", reviewedAt: "2026-08-28T10:15:00Z");

        // Pass 1 suppresses and advances verified_at to now.
        var first = await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None);
        Assert.True(first.ReraiseSuppressed);

        // Pass 2 with reviewed_at unchanged: verification is now newer, so it must re-raise.
        var second = await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None);

        Assert.False(second.ReraiseSuppressed);
        var g = Parse(file).Grounding!;
        Assert.True(g.FlaggedForReview);
        Assert.Null(g.ReraiseSuppressedAt);
        // The review record itself survives both passes.
        Assert.Equal("Oleg Kuzmanov", g.ReviewedBy);
    }

    // ---------- Invariant 8: re-suppression is valid after a NEW review ----------

    [Fact]
    public async Task Suppression_AfterAFreshReview_SuppressesAgain_FromClearedState()
    {
        var file = WriteTest("TC-403", "2026-08-20T09:14:00Z",
            reviewedBy: "Oleg Kuzmanov", reviewedAt: "2026-08-28T10:15:00Z");

        await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None); // suppress #1
        var afterFirst = Parse(file).Grounding!;

        // Simulate a fresh human clearance: reviewed_at moves past the verified_at just written.
        // The from-state is `cleared` (unflagged, marker null) — there is deliberately no direct
        // suppressed -> suppressed transition to assert, because it is unreachable by construction.
        var refreshed = WriteTest("TC-403", afterFirst.VerifiedAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            reviewedBy: "Oleg Kuzmanov",
            reviewedAt: afterFirst.VerifiedAt.AddMinutes(5).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"));

        var second = await _service.WriteAsync(refreshed, PartialVerdict, ct: CancellationToken.None);

        Assert.True(second.ReraiseSuppressed);
        var g = Parse(refreshed).Grounding!;
        Assert.False(g.FlaggedForReview);
        Assert.NotNull(g.ReraiseSuppressedAt);
    }

    // ---------- Invariant 9: a human's raise is never suppressed ----------

    /// <summary>
    /// `feedback` writes a fresh reviewed_at and leaves verified_at alone, so the timestamp
    /// comparison alone would suppress the reviewer's own raise. The pre-write flag clause prevents
    /// it. This is that clause's regression guard.
    /// </summary>
    [Fact]
    public async Task Suppression_DoesNotClearAFlagAHumanRaised()
    {
        var file = WriteTest("TC-404", "2026-08-20T09:14:00Z",
            flagged: true,
            reviewedBy: "Oleg Kuzmanov", reviewedAt: "2026-08-28T10:15:00Z");

        var result = await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None);

        Assert.False(result.ReraiseSuppressed);
        var g = Parse(file).Grounding!;
        Assert.True(g.FlaggedForReview);
        Assert.Null(g.ReraiseSuppressedAt);
    }

    // ---------- Invariants 6, 11, 12: marker lifecycle ----------

    [Fact]
    public async Task Marker_ClearedWhenNextVerdictIsGrounded()
    {
        var file = WriteTest("TC-405", "2026-08-20T09:14:00Z",
            reviewedBy: "Oleg Kuzmanov", reviewedAt: "2026-08-28T10:15:00Z",
            suppressedAt: "2026-08-29T08:00:00Z");

        var result = await _service.WriteAsync(file, GroundedVerdict, ct: CancellationToken.None);

        Assert.False(result.ReraiseSuppressed);
        var g = Parse(file).Grounding!;
        Assert.Null(g.ReraiseSuppressedAt);
        Assert.False(g.FlaggedForReview);
    }

    [Fact]
    public async Task Marker_NeverCoexistsWithFlaggedForReview()
    {
        foreach (var (flagged, reviewed) in new[] { (true, true), (false, true), (true, false), (false, false) })
        {
            var file = WriteTest("TC-406", "2026-08-20T09:14:00Z",
                flagged: flagged,
                reviewedBy: reviewed ? "Oleg Kuzmanov" : null,
                reviewedAt: reviewed ? "2026-08-28T10:15:00Z" : null);

            await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None);

            var g = Parse(file).Grounding!;
            Assert.False(g.ReraiseSuppressedAt is not null && g.FlaggedForReview,
                $"mutual exclusion violated for flagged={flagged}, reviewed={reviewed}");
        }
    }

    [Fact]
    public async Task Marker_NeverCarriesATimestampFromAnEarlierPass()
    {
        var stale = "2026-01-01T00:00:00Z";
        var file = WriteTest("TC-407", "2026-08-20T09:14:00Z",
            reviewedBy: "Oleg Kuzmanov", reviewedAt: "2026-08-28T10:15:00Z",
            suppressedAt: stale);

        await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None);

        var g = Parse(file).Grounding!;
        Assert.NotNull(g.ReraiseSuppressedAt);
        Assert.NotEqual(DateTimeOffset.Parse(stale), g.ReraiseSuppressedAt!.Value);
    }

    // ---------- Invariants 4, 5: the review record survives re-verification (AC-2, SC-002) ----------

    [Fact]
    public async Task ReviewRecord_SurvivesAWriteBackWhileCriticFieldsUpdate()
    {
        var file = WriteTest("TC-408", "2026-08-20T09:14:00Z",
            reviewedBy: "Oleg Kuzmanov", reviewedAt: "2026-08-28T10:15:00Z",
            note: "Confirmed against the support runbook.");

        var before = Parse(file).Grounding!;
        await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None);
        var after = Parse(file).Grounding!;

        // Preserved
        Assert.Equal(before.ReviewedBy, after.ReviewedBy);
        Assert.Equal(before.ReviewedAt, after.ReviewedAt);
        Assert.Equal(before.Note, after.Note);
        // Legitimately updated
        Assert.NotEqual(before.VerifiedAt, after.VerifiedAt);
        Assert.Equal(0.70, after.Score);
    }

    [Fact]
    public async Task ReviewRecord_ByteIdenticalAcrossThreeConsecutivePasses()
    {
        var file = WriteTest("TC-409", "2026-08-20T09:14:00Z",
            reviewedBy: "Oleg Kuzmanov", reviewedAt: "2026-08-28T10:15:00Z",
            note: "Checked the runbook.");

        var baseline = Parse(file).Grounding!;

        for (var i = 0; i < 3; i++)
        {
            await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None);
            var g = Parse(file).Grounding!;
            Assert.Equal(baseline.ReviewedBy, g.ReviewedBy);
            Assert.Equal(baseline.ReviewedAt, g.ReviewedAt);
            Assert.Equal(baseline.Note, g.Note);
        }
    }

    [Fact]
    public async Task ProvenanceFields_ArePreservedAcrossWriteBack()
    {
        var file = Path.Combine(_root, "TC-410.md");
        File.WriteAllText(file, """
            ---
            id: TC-410
            priority: medium
            criteria: []
            grounding:
              verdict: partial
              score: 0.72
              generator: claude-code-session
              critic: claude-opus-5
              verified_at: 2026-08-20T09:14:00Z
              unverified_claims:
                - "Step 3: absent"
              source: "user-described"
              created_by: "Oleg Kuzmanov"
            ---

            # Test TC-410

            ## Steps

            1. Step one

            ## Expected Result

            It works
            """);

        await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None);

        var g = Parse(file).Grounding!;
        Assert.Equal("user-described", g.Source);
        Assert.Equal("Oleg Kuzmanov", g.CreatedBy);
    }

    // ---------- Invariant 14: a test with no review record behaves exactly as before ----------

    [Fact]
    public async Task NoReviewRecord_NormalMarkingAndNoNewFieldsEmitted()
    {
        var file = WriteTest("TC-411", "2026-08-20T09:14:00Z");

        var result = await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None);

        Assert.False(result.ReraiseSuppressed);
        Assert.False(result.ReviewRecordPreserved);

        var content = await File.ReadAllTextAsync(file);
        Assert.DoesNotContain("reviewed_by:", content);
        Assert.DoesNotContain("reraise_suppressed_at:", content);
        Assert.Contains("flagged_for_review: true", content);
    }

    // ---------- FR-008: the caller-supplied flag path ----------

    [Fact]
    public async Task CallerSuppliedFlag_CanMarkAConfidentlyVerifiedTest()
    {
        var file = WriteTest("TC-412", "2026-08-20T09:14:00Z");

        var result = await _service.WriteAsync(file, GroundedVerdict, ct: CancellationToken.None,
            flaggedForReview: true);

        Assert.True(result.IsSuccess);
        var g = Parse(file).Grounding!;
        Assert.Equal(Core.Models.Grounding.VerificationVerdict.Grounded, g.Verdict);
        Assert.True(g.FlaggedForReview);
    }

    [Fact]
    public async Task CallerSuppliedFlag_WhenOmitted_KeepsHistoricalDerivation()
    {
        var partial = WriteTest("TC-413", "2026-08-20T09:14:00Z");
        var grounded = WriteTest("TC-414", "2026-08-20T09:14:00Z");

        await _service.WriteAsync(partial, PartialVerdict, ct: CancellationToken.None);
        await _service.WriteAsync(grounded, GroundedVerdict, ct: CancellationToken.None);

        Assert.True(Parse(partial).Grounding!.FlaggedForReview);
        Assert.False(Parse(grounded).Grounding!.FlaggedForReview);
    }
}
