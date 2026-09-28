using Spectra.CLI.IO;
using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.IO;

/// <summary>
/// Spec 078: a flag a human RAISED must not be cleared as a side effect of an automated verification
/// pass that merely agrees with the critic's prior verdict. This is the same class of defect as the
/// suppression guard in <see cref="GroundingSuppressionTests"/> invariant 9 — an automated pass
/// discarding a human decision — reached through the flag-derivation route rather than the
/// suppression route, so it gets its own guard.
/// </summary>
public class HumanRaiseSurvivalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly GroundingWriteBackService _service = new(new TestFileWriter());

    public HumanRaiseSurvivalTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private const string GroundedVerdict = """
        {"verdict":"grounded","score":0.95,"critic_model":"claude-opus-5",
         "findings":[{"element":"Step 1","claim":"navigates","status":"grounded","evidence":"docs say so"}]}
        """;

    private const string PartialVerdict = """
        {"verdict":"partial","score":0.70,"critic_model":"claude-opus-5",
         "findings":[{"element":"Step 3","claim":"interval","status":"unverified","reason":"absent"}]}
        """;

    /// <summary>Writes a test flagged by a human via feedback: flagged, with a review newer than the verification.</summary>
    private string WriteHumanRaisedTest(string id, string verdict)
    {
        var file = Path.Combine(_root, id + ".md");
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
        sb.AppendLine("  flagged_for_review: true");
        sb.AppendLine("  note: \"Step 2 references a loyalty tier field that does not exist.\"");
        sb.AppendLine("  reviewed_by: \"Oleg Kuzmanov\"");
        sb.AppendLine("  reviewed_at: 2026-08-28T10:15:00Z");
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

    /// <summary>
    /// THE GUARD. A reviewer flags a confidently-verified test through <c>feedback</c> (FR-008). A
    /// later re-verification pass returns <c>grounded</c> again — nothing has changed, so the pass has
    /// no new information. The flag must survive. The batch <c>--re-verify</c> path supplies no
    /// explicit flag value, so this is exactly the shape that reaches production.
    /// </summary>
    [Fact]
    public async Task HumanRaisedFlagOnGroundedTest_SurvivesAReVerifyPassWithAnUnchangedVerdict()
    {
        var file = WriteHumanRaisedTest("TC-800", "grounded");

        // No flaggedForReview argument — this is what ingest-grounding --all --re-verify does.
        var result = await _service.WriteAsync(file, GroundedVerdict, ct: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var g = Parse(file).Grounding!;
        Assert.True(g.FlaggedForReview,
            "an automated pass agreeing with the prior verdict silently cleared a human-raised flag");
        Assert.Equal("Oleg Kuzmanov", g.ReviewedBy);
        Assert.Null(g.ReraiseSuppressedAt);
    }

    [Fact]
    public async Task HumanRaisedFlagOnGroundedTest_SurvivesRepeatedReVerifyPasses()
    {
        // The raise is a standing objection: it must not decay after one pass the way suppression does.
        var file = WriteHumanRaisedTest("TC-801", "grounded");

        for (var i = 1; i <= 3; i++)
        {
            await _service.WriteAsync(file, GroundedVerdict, ct: CancellationToken.None);
            Assert.True(Parse(file).Grounding!.FlaggedForReview, $"flag lost on pass {i}");
        }
    }

    [Fact]
    public async Task HumanRaisedFlagOnPartialTest_SurvivesAnUnchangedPartialVerdict()
    {
        var file = WriteHumanRaisedTest("TC-802", "partial");

        await _service.WriteAsync(file, PartialVerdict, ct: CancellationToken.None);

        var g = Parse(file).Grounding!;
        Assert.True(g.FlaggedForReview);
        Assert.Null(g.ReraiseSuppressedAt);
    }

    /// <summary>
    /// The counterpart: when the verdict actually CHANGES, re-derivation is legitimate. A repair that
    /// upgrades a partial test to grounded must still clear the flag, or the repair loop would leave
    /// every repaired test flagged forever.
    /// </summary>
    [Fact]
    public async Task AutoFlaggedPartialTest_UpgradedToGrounded_StillClearsTheFlag()
    {
        var file = Path.Combine(_root, "TC-803.md");
        File.WriteAllText(file, """
            ---
            id: TC-803
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
              flagged_for_review: true
            ---

            # Test TC-803

            ## Steps

            1. Step one

            ## Expected Result

            It works
            """);

        await _service.WriteAsync(file, GroundedVerdict, repairAttempts: 1, repaired: true,
            ct: CancellationToken.None);

        var g = Parse(file).Grounding!;
        Assert.False(g.FlaggedForReview, "a repaired-and-upgraded test must not stay flagged");
    }

    [Fact]
    public async Task ExplicitCallerOverride_StillWinsOverPreservation()
    {
        // An explicit false from a caller is a deliberate decision and must not be overridden by the
        // preserve-the-raise default.
        var file = WriteHumanRaisedTest("TC-804", "grounded");

        await _service.WriteAsync(file, GroundedVerdict, ct: CancellationToken.None,
            flaggedForReview: false);

        Assert.False(Parse(file).Grounding!.FlaggedForReview);
    }
}
