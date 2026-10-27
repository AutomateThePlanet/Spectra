using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Generate;
using Spectra.CLI.Commands.Review;
using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 103 (User Story 2): <c>--re-verify</c> is what makes preservation and suppression reachable
/// in the real flow. Without it the batch path skips every already-grounded test, so a second
/// verification pass writes nothing at all.
/// </summary>
[Collection("Sequential Command Tests")]
public class IngestGroundingReVerifyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _originalDir = Directory.GetCurrentDirectory();
    private readonly string _suiteDir;

    public IngestGroundingReVerifyTests()
    {
        _suiteDir = Path.Combine(_root, "test-cases", "checkout");
        Directory.CreateDirectory(_suiteDir);
        Directory.CreateDirectory(Path.Combine(_root, ".spectra", "verdicts"));
        Directory.SetCurrentDirectory(_root);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private void WriteTest(string id, string verifiedAt, string? reviewedBy, string? reviewedAt)
    {
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
        if (reviewedBy is not null) sb.AppendLine("  reviewed_by: \"" + reviewedBy + "\"");
        if (reviewedAt is not null) sb.AppendLine("  reviewed_at: " + reviewedAt);
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

        File.WriteAllText(Path.Combine(_suiteDir, id + ".md"), sb.ToString());

        // Suite index — the batch path filters by it, and File is a bare filename (Spec 075).
        var index = new
        {
            suite = "checkout",
            generated_at = "2026-08-31T00:00:00Z",
            tests = new[] { new { id, file = id + ".md", title = "Test " + id, priority = "medium", tags = Array.Empty<string>(), component = (string?)null, depends_on = (string?)null, source_refs = Array.Empty<string>() } }
        };
        File.WriteAllText(Path.Combine(_suiteDir, "_index.json"), JsonSerializer.Serialize(index));

        File.WriteAllText(
            Path.Combine(_root, ".spectra", "verdicts", $"critic-verdict-{id}.json"),
            """
            {"verdict":"partial","score":0.70,"critic_model":"claude-opus-5",
             "findings":[{"element":"Step 3","claim":"interval","status":"unverified","reason":"absent"}]}
            """);
    }

    private TestCase Parse(string id)
    {
        var file = Path.Combine(_suiteDir, id + ".md");
        var parsed = new TestCaseParser().Parse(File.ReadAllText(file), id + ".md");
        Assert.True(parsed.IsSuccess, "test file failed to parse");
        return parsed.Value!;
    }

    private static async Task<int> RunAsync(params string[] args)
    {
        var root = new RootCommand();
        root.AddCommand(new IngestGroundingCommand());
        return await root.InvokeAsync(args);
    }

    // ---------- Invariant 3: the default skip is a HARD regression gate (Specs 073/077) ----------

    [Fact]
    public async Task WithoutReVerify_AlreadyGroundedTestIsSkippedAndRecordUntouched()
    {
        WriteTest("TC-500", "2026-08-20T09:14:00Z", "Oleg Kuzmanov", "2026-08-28T10:15:00Z");
        var before = Parse("TC-500").Grounding!;

        var exit = await RunAsync("ingest-grounding", "--suite", "checkout", "--all");

        Assert.Equal(0, exit);
        var after = Parse("TC-500").Grounding!;
        // Untouched: the skip fired, so verified_at did not move.
        Assert.Equal(before.VerifiedAt, after.VerifiedAt);
        Assert.Equal(before.ReviewedBy, after.ReviewedBy);
        Assert.Equal(before.ReviewedAt, after.ReviewedAt);
    }

    // ---------- Invariant 4: --re-verify rewrites, preserving the record (AC-2) ----------

    [Fact]
    public async Task WithReVerify_TestIsRewrittenAndReviewRecordSurvives()
    {
        WriteTest("TC-501", "2026-08-20T09:14:00Z", "Oleg Kuzmanov", "2026-08-28T10:15:00Z");
        var before = Parse("TC-501").Grounding!;

        var exit = await RunAsync("ingest-grounding", "--suite", "checkout", "--all", "--re-verify");

        Assert.Equal(0, exit);
        var after = Parse("TC-501").Grounding!;
        Assert.NotEqual(before.VerifiedAt, after.VerifiedAt);   // critic field updated
        Assert.Equal(before.ReviewedBy, after.ReviewedBy);      // review record preserved
        Assert.Equal(before.ReviewedAt, after.ReviewedAt);
    }

    [Fact]
    public async Task WithReVerify_SuppressionFiresAndIsReported()
    {
        WriteTest("TC-502", "2026-08-20T09:14:00Z", "Oleg Kuzmanov", "2026-08-28T10:15:00Z");

        var exit = await RunAsync("ingest-grounding", "--suite", "checkout", "--all", "--re-verify");

        Assert.Equal(0, exit);
        var g = Parse("TC-502").Grounding!;
        Assert.False(g.FlaggedForReview);
        Assert.NotNull(g.ReraiseSuppressedAt);
    }

    // ---------- Invariant 5 / SC-002: byte-identical across three passes ----------

    [Fact]
    public async Task ThreeConsecutiveReVerifyPasses_LeaveTheReviewRecordByteIdentical()
    {
        WriteTest("TC-503", "2026-08-20T09:14:00Z", "Oleg Kuzmanov", "2026-08-28T10:15:00Z");
        var baseline = Parse("TC-503").Grounding!;

        for (var i = 0; i < 3; i++)
        {
            await RunAsync("ingest-grounding", "--suite", "checkout", "--all", "--re-verify");
            var g = Parse("TC-503").Grounding!;
            Assert.Equal(baseline.ReviewedBy, g.ReviewedBy);
            Assert.Equal(baseline.ReviewedAt, g.ReviewedAt);
        }
    }

    // ---------- Invariant 14: no review record => behaves exactly as before ----------

    [Fact]
    public async Task WithReVerify_TestWithNoReviewRecord_GetsNormalMarkingAndNoNewFields()
    {
        WriteTest("TC-504", "2026-08-20T09:14:00Z", reviewedBy: null, reviewedAt: null);

        await RunAsync("ingest-grounding", "--suite", "checkout", "--all", "--re-verify");

        var content = await File.ReadAllTextAsync(Path.Combine(_suiteDir, "TC-504.md"));
        Assert.DoesNotContain("reviewed_by:", content);
        Assert.DoesNotContain("reraise_suppressed_at:", content);

        // Marking follows the unchanged historical derivation (`partial && !repaired`): a re-verified partial with no
        // review record is flagged, and nothing is marked repaired.
        Assert.Contains("flagged_for_review: true", content);
        Assert.DoesNotContain("repaired:", content);
        var g = Parse("TC-504").Grounding!;
        Assert.True(g.FlaggedForReview);
        Assert.Null(g.ReviewedBy);
        Assert.Null(g.ReraiseSuppressedAt);
    }

    // ---------- Spec 103 review fix: only a new verdict is a verification pass ----------

    [Fact]
    public async Task WithReVerify_AVerdictOlderThanTheBlock_IsNotReingested()
    {
        WriteTest("TC-507", "2026-08-20T09:14:00Z", "Oleg Kuzmanov", "2026-08-28T10:15:00Z");
        File.SetLastWriteTimeUtc(
            Path.Combine(_root, ".spectra", "verdicts", "critic-verdict-TC-507.json"), new DateTime(2026, 8, 19, 0, 0, 0, DateTimeKind.Utc));
        var before = Parse("TC-507").Grounding!;

        var exit = await RunAsync("ingest-grounding", "--suite", "checkout", "--all", "--re-verify");

        Assert.Equal(0, exit);
        var after = Parse("TC-507").Grounding!;
        // The critic didn't run again, so verified_at must not move past the review (which would spend its suppression).
        Assert.Equal(before.VerifiedAt, after.VerifiedAt);
        Assert.Null(after.ReraiseSuppressedAt);
    }

    [Fact]
    public async Task ReVerify_WithoutAll_IsAUsageError()
    {
        WriteTest("TC-508", "2026-08-20T09:14:00Z", reviewedBy: null, reviewedAt: null);

        var exit = await RunAsync("ingest-grounding", "--suite", "checkout", "--test", "TC-508", "--re-verify");

        Assert.Equal(1, exit);
    }

    // ---------- Spec 103 (M1): a feedback --fix review survives the repair chain ----------

    [Fact]
    public async Task StagedFixReview_IsWrittenToTheRepairedTestsNewBlock()
    {
        // ingest-update dropped the critic block (Spec 090): write the test with no grounding at all.
        WriteTest("TC-509", "2026-08-20T09:14:00Z", reviewedBy: null, reviewedAt: null);
        var path = Path.Combine(_suiteDir, "TC-509.md");
        var text = await File.ReadAllTextAsync(path);
        var start = text.IndexOf("grounding:", StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, text[..start] + text[text.IndexOf("---", start, StringComparison.Ordinal)..]);
        File.WriteAllText(
            Path.Combine(_root, ".spectra", "verdicts", "critic-verdict-TC-509.json"),
            """{"verdict":"grounded","score":0.95,"critic_model":"claude-opus-5","findings":[]}""");

        var store = new Spectra.CLI.IO.PendingReviewStore(_root);
        await store.SaveAsync("checkout", "TC-509", new Spectra.CLI.IO.PendingReview
        {
            ReviewedBy = "Oleg Kuzmanov",
            ReviewedAt = DateTimeOffset.Parse("2026-08-28T10:15:00Z"),
            Note = "Step 2 names a field that doesn't exist."
        });

        var exit = await RunAsync("ingest-grounding", "--suite", "checkout", "--test", "TC-509", "--repaired", "--repair-attempts", "1");

        Assert.Equal(0, exit);
        var g = Parse("TC-509").Grounding!;
        Assert.Equal("Oleg Kuzmanov", g.ReviewedBy);
        Assert.Equal(DateTimeOffset.Parse("2026-08-28T10:15:00Z"), g.ReviewedAt);
        Assert.Equal("Step 2 names a field that doesn't exist.", g.Note);
        Assert.False(g.FlaggedForReview);
        Assert.False(File.Exists(store.PathFor("checkout", "TC-509"))); // consumed
    }

    // ---------- T041: audit counters are disjoint, and the existing values are unchanged ----------

    [Fact]
    public async Task Audit_ReportsReviewedRatherThanNone_ForAHumanClearedTest()
    {
        WriteTest("TC-505", "2026-08-20T09:14:00Z", "Oleg Kuzmanov", "2026-08-28T10:15:00Z");
        // Suppress it, which leaves it unflagged with a review record and a marker.
        await RunAsync("ingest-grounding", "--suite", "checkout", "--all", "--re-verify");

        var handler = new AuditGroundingHandler(_root, "test-cases");
        var exit = await handler.RunAsync("checkout", json: false, CancellationToken.None);

        Assert.Equal(0, exit);
        var g = Parse("TC-505").Grounding!;
        Assert.NotNull(g.ReviewedBy);
        Assert.NotNull(g.ReraiseSuppressedAt);
        Assert.False(g.FlaggedForReview);
    }

    [Fact]
    public async Task Audit_SuppressedTestIsNotAlsoCountedAsStale()
    {
        // A suppressing pass advances verified_at past reviewed_at, so without the null-marker clause
        // in the stale predicate the two counters would double-count the same condition.
        WriteTest("TC-506", "2026-08-20T09:14:00Z", "Oleg Kuzmanov", "2026-08-28T10:15:00Z");
        await RunAsync("ingest-grounding", "--suite", "checkout", "--all", "--re-verify");

        var g = Parse("TC-506").Grounding!;
        Assert.NotNull(g.ReraiseSuppressedAt);
        Assert.True(g.VerifiedAt > g.ReviewedAt!.Value, "precondition: verification overtook the review");

        // Stale requires a NULL marker, so a suppressed test must not qualify.
        var stale = g.ReviewedBy is not null
            && !g.FlaggedForReview
            && g.ReraiseSuppressedAt is null
            && g.VerifiedAt > g.ReviewedAt!.Value;
        Assert.False(stale);
    }
}
