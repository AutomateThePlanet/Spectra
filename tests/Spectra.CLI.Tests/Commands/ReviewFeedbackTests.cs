using Spectra.CLI.Commands.Review;
using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 103 (User Story 3): the <c>feedback</c> action. A reviewer can raise a concern on any
/// eligible test — including one the critic verified confidently — without triggering a rewrite.
/// </summary>
public class ReviewFeedbackTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _testsDir;
    private readonly ReviewFlaggedHandler _handler;

    public ReviewFeedbackTests()
    {
        _testsDir = Path.Combine(_root, "test-cases");
        Directory.CreateDirectory(_testsDir);
        _handler = new ReviewFlaggedHandler(_root, "test-cases");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private async Task<string> WriteTestAsync(
        string suite, string id, string verdict, bool flagged,
        string? reviewedBy = null, string? reviewedAt = null, string? suppressedAt = null)
    {
        var suiteDir = Path.Combine(_testsDir, suite);
        Directory.CreateDirectory(suiteDir);
        var file = Path.Combine(suiteDir, id + ".md");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("id: " + id);
        sb.AppendLine("priority: medium");
        sb.AppendLine("criteria: []");
        sb.AppendLine("grounding:");
        sb.AppendLine("  verdict: " + verdict);
        sb.AppendLine("  score: 0.95");
        sb.AppendLine("  generator: claude-code-session");
        sb.AppendLine("  critic: claude-opus-5");
        sb.AppendLine("  verified_at: 2026-08-20T09:14:00Z");
        if (verdict == "partial")
        {
            sb.AppendLine("  unverified_claims:");
            sb.AppendLine("    - \"Step 3: absent\"");
        }
        if (flagged) sb.AppendLine("  flagged_for_review: true");
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

        await File.WriteAllTextAsync(file, sb.ToString());
        return file;
    }

    private static TestCase Parse(string file)
    {
        var parsed = new TestCaseParser().Parse(File.ReadAllText(file), Path.GetFileName(file));
        Assert.True(parsed.IsSuccess, "test file failed to parse");
        return parsed.Value!;
    }

    // ---------- AC-4 / invariant 3: feedback flags a previously-unflagged grounded test ----------

    [Fact]
    public async Task Feedback_OnUnflaggedGroundedTest_FlagsItAndRecordsTheReview()
    {
        var file = await WriteTestAsync("suite-a", "TC-600", "grounded", flagged: false);
        var before = Parse(file).Grounding!;

        var result = await _handler.FeedbackAsync("suite-a", "TC-600",
            "Step 2 references a loyalty tier field that does not exist in the checkout UI.",
            "Oleg Kuzmanov", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var after = Parse(file).Grounding!;
        Assert.True(after.FlaggedForReview);
        Assert.Equal("Oleg Kuzmanov", after.ReviewedBy);
        Assert.NotNull(after.ReviewedAt);
        Assert.Contains("loyalty tier", after.Note);
        // The critic's own assessment is untouched.
        Assert.Equal(before.Verdict, after.Verdict);
        Assert.Equal(before.Score, after.Score);
    }

    [Fact]
    public async Task Feedback_OnAlreadyFlaggedTest_StaysFlaggedAndUpdatesTheNote()
    {
        var file = await WriteTestAsync("suite-a", "TC-601", "partial", flagged: true);

        var result = await _handler.FeedbackAsync("suite-a", "TC-601", "latest observation",
            "Oleg Kuzmanov", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var g = Parse(file).Grounding!;
        Assert.True(g.FlaggedForReview);
        Assert.Equal("latest observation", g.Note);
    }

    // ---------- FR-015: a note is required ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Feedback_WithoutNote_IsRefusedAndWritesNothing(string? note)
    {
        var file = await WriteTestAsync("suite-a", "TC-602", "grounded", flagged: false);
        var before = await File.ReadAllTextAsync(file);

        var result = await _handler.FeedbackAsync("suite-a", "TC-602", note, "R", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("NOTE_REQUIRED", result.ErrorCode);
        Assert.Equal(before, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Feedback_UnrepresentableNote_IsRefusedAndWritesNothing()
    {
        var file = await WriteTestAsync("suite-a", "TC-603", "grounded", flagged: false);
        var before = await File.ReadAllTextAsync(file);

        var result = await _handler.FeedbackAsync("suite-a", "TC-603", "bad\0note", "R", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("NOTE_UNREPRESENTABLE", result.ErrorCode);
        Assert.Equal(before, await File.ReadAllTextAsync(file));
    }

    // ---------- FR-007: eligibility still applies to feedback ----------

    [Theory]
    [InlineData("manual", "VERDICT_INELIGIBLE_MANUAL")]
    [InlineData("hallucinated", "VERDICT_INELIGIBLE_HALLUCINATED")]
    public async Task Feedback_OnIneligibleVerdict_IsRefusedWithASpecificCode(string verdict, string expectedCode)
    {
        var file = await WriteTestAsync("suite-a", "TC-604", verdict, flagged: false);
        var before = await File.ReadAllTextAsync(file);

        var result = await _handler.FeedbackAsync("suite-a", "TC-604", "a note", "R", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(before, await File.ReadAllTextAsync(file));
    }

    // ---------- Invariants 10, 11, 15: the marker lifecycle under feedback ----------

    [Fact]
    public async Task Feedback_OnSuppressedTest_FlagsItAndClearsTheMarker()
    {
        // Suppressed state: unflagged, review record present, marker live. Feedback is the ONLY
        // action a suppressed test is eligible for — the other three require a flagged test.
        var file = await WriteTestAsync("suite-a", "TC-605", "partial", flagged: false,
            reviewedBy: "Oleg Kuzmanov", reviewedAt: "2026-08-28T10:15:00Z",
            suppressedAt: "2026-08-31T08:00:00Z");

        Assert.NotNull(Parse(file).Grounding!.ReraiseSuppressedAt);

        var result = await _handler.FeedbackAsync("suite-a", "TC-605", "still wrong", "R", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var g = Parse(file).Grounding!;
        Assert.True(g.FlaggedForReview);
        Assert.Null(g.ReraiseSuppressedAt);
    }

    [Fact]
    public async Task MutualExclusion_HoldsAfterEveryAction()
    {
        var feedbackFile = await WriteTestAsync("suite-a", "TC-606", "grounded", flagged: false);
        var acceptFile = await WriteTestAsync("suite-a", "TC-607", "partial", flagged: true);

        await _handler.FeedbackAsync("suite-a", "TC-606", "note", "R", CancellationToken.None);
        await _handler.AcceptAsync("suite-a", "TC-607", "note", "R", CancellationToken.None);

        foreach (var file in new[] { feedbackFile, acceptFile })
        {
            var g = Parse(file).Grounding!;
            Assert.False(g.ReraiseSuppressedAt is not null && g.FlaggedForReview);
        }
    }

    // ---------- FR-017: selection scope differs per action ----------

    [Fact]
    public async Task FindFeedbackEligible_IncludesUnflaggedGroundedAndPartialTests()
    {
        await WriteTestAsync("suite-a", "TC-610", "grounded", flagged: false);
        await WriteTestAsync("suite-a", "TC-611", "partial", flagged: true);
        await WriteTestAsync("suite-a", "TC-612", "manual", flagged: false);
        await WriteTestAsync("suite-a", "TC-613", "hallucinated", flagged: false);

        var eligible = await _handler.FindFeedbackEligibleAsync("suite-a", CancellationToken.None);
        var ids = eligible.Select(e => e.Id).OrderBy(i => i).ToArray();

        Assert.Equal(["TC-610", "TC-611"], ids);
    }

    [Fact]
    public async Task FindFlagged_RemainsRestrictedToFlaggedTests()
    {
        await WriteTestAsync("suite-a", "TC-614", "grounded", flagged: false);
        await WriteTestAsync("suite-a", "TC-615", "partial", flagged: true);

        var flagged = await _handler.FindFlaggedAsync("suite-a", CancellationToken.None);

        Assert.Equal("TC-615", flagged.Single().Id);
    }

    // ---------- FR-011: the suppressed listing surface ----------

    [Fact]
    public async Task FindSuppressed_ReturnsOnlyCurrentlySuppressedTests()
    {
        await WriteTestAsync("suite-a", "TC-620", "partial", flagged: true);
        await WriteTestAsync("suite-a", "TC-621", "partial", flagged: false,
            reviewedBy: "Oleg Kuzmanov", reviewedAt: "2026-08-28T10:15:00Z",
            suppressedAt: "2026-08-31T08:00:00Z");
        await WriteTestAsync("suite-a", "TC-622", "grounded", flagged: false);

        var suppressed = await _handler.FindSuppressedAsync("suite-a", CancellationToken.None);

        Assert.Equal("TC-621", suppressed.Single().Id);
        Assert.NotNull(suppressed.Single().ReraiseSuppressedAt);
        Assert.NotNull(suppressed.Single().ReviewedBy);
    }

    [Fact]
    public async Task FlaggedAndSuppressedListings_AreDisjoint()
    {
        await WriteTestAsync("suite-a", "TC-630", "partial", flagged: true);
        await WriteTestAsync("suite-a", "TC-631", "partial", flagged: false,
            reviewedBy: "R", reviewedAt: "2026-08-28T10:15:00Z", suppressedAt: "2026-08-31T08:00:00Z");

        var flagged = await _handler.FindFlaggedAsync("suite-a", CancellationToken.None);
        var suppressed = await _handler.FindSuppressedAsync("suite-a", CancellationToken.None);

        Assert.Empty(flagged.Select(f => f.Id).Intersect(suppressed.Select(s => s.Id)));
        Assert.All(flagged, t => Assert.Null(t.ReraiseSuppressedAt));
        Assert.All(suppressed, t => Assert.NotNull(t.ReraiseSuppressedAt));
    }
}
