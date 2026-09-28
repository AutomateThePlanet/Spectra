using Spectra.CLI.Commands.Review;
using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 078 (User Story 1): the human review record is written, attributable, and does not disturb
/// the critic's own assessment. Also covers the eligibility gate (FR-007) and identity resolution
/// (FR-020/FR-021).
/// </summary>
public class ReviewRecordHandlerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _testsDir;
    private readonly ReviewFlaggedHandler _handler;

    public ReviewRecordHandlerTests()
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
        sb.AppendLine("  score: 0.72");
        sb.AppendLine("  generator: claude-code-session");
        sb.AppendLine("  critic: claude-opus-5");
        sb.AppendLine("  verified_at: 2026-06-19T10:00:00Z");
        if (verdict == "partial")
        {
            sb.AppendLine("  unverified_claims:");
            sb.AppendLine("    - \"Step 2: factor not in docs\"");
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

    // ---------- T019: accept records the review, critic fields untouched ----------

    [Fact]
    public async Task AcceptWithNote_RecordsReviewerTimestampAndNote()
    {
        var file = await WriteTestAsync("suite-a", "TC-300", "partial", flagged: true);

        var result = await _handler.AcceptAsync("suite-a", "TC-300",
            note: "Confirmed against the support runbook.", reviewer: "Oleg Kuzmanov",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var g = Parse(file).Grounding!;
        Assert.Equal("Oleg Kuzmanov", g.ReviewedBy);
        Assert.NotNull(g.ReviewedAt);
        Assert.Equal("Confirmed against the support runbook.", g.Note);
        Assert.False(g.FlaggedForReview);
    }

    [Fact]
    public async Task BareAccept_StillRecordsReviewerAndTimestamp()
    {
        var file = await WriteTestAsync("suite-a", "TC-301", "partial", flagged: true);

        var result = await _handler.AcceptAsync("suite-a", "TC-301", note: null,
            reviewer: "Oleg Kuzmanov", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var g = Parse(file).Grounding!;
        Assert.Equal("Oleg Kuzmanov", g.ReviewedBy);
        Assert.NotNull(g.ReviewedAt);
        Assert.False(g.FlaggedForReview);
    }

    [Fact]
    public async Task Accept_LeavesVerdictScoreAndVerifiedAtUnchanged()
    {
        var file = await WriteTestAsync("suite-a", "TC-302", "partial", flagged: true);
        var before = Parse(file).Grounding!;

        await _handler.AcceptAsync("suite-a", "TC-302", "ok", "R", CancellationToken.None);

        var after = Parse(file).Grounding!;
        Assert.Equal(before.Verdict, after.Verdict);
        Assert.Equal(before.Score, after.Score);
        Assert.Equal(before.VerifiedAt, after.VerifiedAt);
    }

    [Fact]
    public async Task Accept_ResultEchoesVerdictAndScoreForCallerAssertion()
    {
        await WriteTestAsync("suite-a", "TC-309", "partial", flagged: true);

        var result = await _handler.AcceptAsync("suite-a", "TC-309", null, "R", CancellationToken.None);

        Assert.Equal("partial", result.Verdict);
        Assert.Equal(0.72, result.Score);
    }

    // ---------- T020: eligibility refusals, distinguishable, write nothing ----------

    [Fact]
    public async Task Accept_ManualVerdict_RefusedWithSpecificCode()
    {
        var file = await WriteTestAsync("suite-a", "TC-303", "manual", flagged: false);
        var before = await File.ReadAllTextAsync(file);

        var result = await _handler.AcceptAsync("suite-a", "TC-303", null, "R", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VERDICT_INELIGIBLE_MANUAL", result.ErrorCode);
        Assert.Equal(before, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Accept_HallucinatedVerdict_RefusedWithDistinctCode()
    {
        var file = await WriteTestAsync("suite-a", "TC-304", "hallucinated", flagged: false);
        var before = await File.ReadAllTextAsync(file);

        var result = await _handler.AcceptAsync("suite-a", "TC-304", null, "R", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VERDICT_INELIGIBLE_HALLUCINATED", result.ErrorCode);
        Assert.Equal(before, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Accept_UnflaggedTest_RefusedAsNotFlagged()
    {
        var file = await WriteTestAsync("suite-a", "TC-305", "grounded", flagged: false);
        var before = await File.ReadAllTextAsync(file);

        var result = await _handler.AcceptAsync("suite-a", "TC-305", null, "R", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("NOT_FLAGGED", result.ErrorCode);
        Assert.Equal(before, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Accept_MissingTest_RefusedAsTestNotFound()
    {
        await WriteTestAsync("suite-a", "TC-306", "partial", flagged: true);

        var result = await _handler.AcceptAsync("suite-a", "TC-999", null, "R", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TEST_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Accept_MissingSuite_RefusedAsSuiteNotFound()
    {
        var result = await _handler.AcceptAsync("no-such-suite", "TC-100", null, "R", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("SUITE_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Accept_UnrepresentableNote_RefusedAndWritesNothing()
    {
        var file = await WriteTestAsync("suite-a", "TC-307", "partial", flagged: true);
        var before = await File.ReadAllTextAsync(file);

        var result = await _handler.AcceptAsync("suite-a", "TC-307", "bad\0note", "R", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("NOTE_UNREPRESENTABLE", result.ErrorCode);
        Assert.Equal(before, await File.ReadAllTextAsync(file));
    }

    // ---------- T021: identity resolution always succeeds, never prompts ----------

    [Fact]
    public void ResolveReviewer_ExplicitValue_WinsAndIsTrimmed()
    {
        Assert.Equal("Oleg Kuzmanov", ReviewFlaggedHandler.ResolveReviewer("Oleg Kuzmanov"));
        Assert.Equal("Oleg Kuzmanov", ReviewFlaggedHandler.ResolveReviewer("  Oleg Kuzmanov  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveReviewer_WithoutExplicitValue_AlwaysResolvesNonBlank(string? explicitValue)
    {
        // Spec 078 Decision 6: resolution cannot fail — git user.name, else the OS user name.
        // There is deliberately no prompt and no failure path to test.
        Assert.False(string.IsNullOrWhiteSpace(ReviewFlaggedHandler.ResolveReviewer(explicitValue)));
    }

    [Fact]
    public async Task EveryCompletedAccept_WritesNonBlankReviewer()
    {
        var file = await WriteTestAsync("suite-a", "TC-308", "partial", flagged: true);

        var result = await _handler.AcceptAsync("suite-a", "TC-308", null, reviewer: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.ReviewedBy));
        Assert.False(string.IsNullOrWhiteSpace(Parse(file).Grounding!.ReviewedBy));
    }

    // ---------- T022: interactive wrapper and action path cannot drift ----------

    [Fact]
    public async Task InteractiveWrapperAndActionPath_ProduceSameOnDiskResult()
    {
        var fileA = await WriteTestAsync("suite-a", "TC-310", "partial", flagged: true);
        var fileB = await WriteTestAsync("suite-b", "TC-311", "partial", flagged: true);

        await _handler.AcceptAsync("suite-a", "TC-310", note: null, reviewer: "R", CancellationToken.None);

        var flagged = await _handler.FindFlaggedAsync("suite-b", CancellationToken.None);
        Assert.True(await _handler.AcceptAsync(flagged[0], CancellationToken.None));

        var a = Parse(fileA).Grounding!;
        var b = Parse(fileB).Grounding!;
        Assert.False(a.FlaggedForReview);
        Assert.False(b.FlaggedForReview);
        Assert.False(string.IsNullOrWhiteSpace(a.ReviewedBy));
        Assert.False(string.IsNullOrWhiteSpace(b.ReviewedBy));
        Assert.Equal(a.Verdict, b.Verdict);
    }

    // ---------- T023: the listing surfaces the review record ----------

    [Fact]
    public async Task FindFlagged_DistinguishesCommentedFromUntouched()
    {
        await WriteTestAsync("suite-a", "TC-320", "partial", flagged: true);
        await WriteTestAsync("suite-a", "TC-321", "partial", flagged: true,
            reviewedBy: "Oleg Kuzmanov", reviewedAt: "2026-08-28T11:02:00Z");

        var flagged = await _handler.FindFlaggedAsync("suite-a", CancellationToken.None);

        var untouched = flagged.Single(t => t.Id == "TC-320");
        var commented = flagged.Single(t => t.Id == "TC-321");

        Assert.Null(untouched.ReviewedBy);
        Assert.Null(untouched.ReviewedAt);
        Assert.Equal("Oleg Kuzmanov", commented.ReviewedBy);
        Assert.NotNull(commented.ReviewedAt);
    }

    [Fact]
    public async Task FindFlagged_ReportsVerdictPerTest()
    {
        await WriteTestAsync("suite-a", "TC-322", "grounded", flagged: true);

        var flagged = await _handler.FindFlaggedAsync("suite-a", CancellationToken.None);

        Assert.Equal("grounded", flagged.Single().Verdict);
    }

    [Fact]
    public async Task FindFlagged_MarkerAlwaysNullInFlaggedList()
    {
        // A flagged test withholds nothing, so the marker is never live there.
        await WriteTestAsync("suite-a", "TC-323", "partial", flagged: true,
            reviewedBy: "Oleg Kuzmanov", reviewedAt: "2026-08-28T11:02:00Z");

        var flagged = await _handler.FindFlaggedAsync("suite-a", CancellationToken.None);

        Assert.Null(flagged.Single().ReraiseSuppressedAt);
    }
}
