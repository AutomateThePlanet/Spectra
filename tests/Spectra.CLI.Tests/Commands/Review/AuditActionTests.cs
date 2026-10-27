using System.Text.Json;
using Spectra.CLI.Skills;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Commands.Review;

/// <summary>
/// Spec 098 F2 (FR-006–FR-008, SC-002): the audit's action for a test with a fresh verdict and no grounding block comes
/// from the verdict — grounded → <c>ground</c> (only the block is missing), partial → <c>repair</c>, hallucinated →
/// <c>review</c> (the user decides). On 986da01 all three were <c>repair</c>, so the generate resume path sent a
/// confirmed test and a kept hallucinated test through the repairer.
/// <para>Suite <c>a</c> after an interrupted run: TC-101 grounded, TC-102 partial, TC-103 hallucinated (no blocks, no
/// records); TC-104 grounded with its block written.</para>
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class AuditActionTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public AuditActionTests()
    {
        _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();
        _ws.WriteDoc("docs/storage.md", VerificationWorkspace.StorageDoc);
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103", "TC-104" })
        {
            _ws.WriteTest("a", id, [VerificationWorkspace.Quotas]);
        }

        _ws.WriteVerdict("TC-101", "grounded");
        _ws.WriteVerdict("TC-102", "partial", 0.7);
        _ws.WriteVerdict("TC-103", "hallucinated", 0.2);
        _ws.WriteVerdict("TC-104", "grounded");
        _ws.WriteCriticGroundingAsync("a", "TC-104").GetAwaiter().GetResult();
    }

    public void Dispose() => _ws.Dispose();

    private async Task<JsonElement> AuditAsync(params string[] extra)
    {
        var (exit, output, error) = await _ws.RunAsync(["ai", "audit-grounding", "--suite", "a", .. extra, "--output-format", "json"]);
        Assert.True(exit == 0, error);
        return VerdictWorkspace.Json(output);
    }

    private static string Action(JsonElement result, string id) =>
        result.GetProperty("tests").EnumerateArray().Single(t => t.GetProperty("id").GetString() == id)
            .GetProperty("action_needed").GetString()!;

    private static List<string> Ids(JsonElement result) =>
        result.GetProperty("tests").EnumerateArray().Select(t => t.GetProperty("id").GetString()!).OrderBy(i => i, StringComparer.Ordinal).ToList();

    [Fact]
    public async Task AnInterruptedRun_GivesGroundRepairAndReview()
    {
        var result = await AuditAsync();

        Assert.Equal("ground", Action(result, "TC-101"));
        Assert.Equal("repair", Action(result, "TC-102"));
        Assert.Equal("review", Action(result, "TC-103"));
        Assert.Equal("none", Action(result, "TC-104"));
    }

    [Fact]
    public async Task TheSummary_CountsEachAction()
    {
        var summary = (await AuditAsync()).GetProperty("summary");

        Assert.Equal(1, summary.GetProperty("pending_ground").GetInt32());
        Assert.Equal(1, summary.GetProperty("partial_pending_repair").GetInt32());
        Assert.Equal(1, summary.GetProperty("pending_review").GetInt32());
    }

    [Fact]
    public async Task ActionGround_ListsOnlyGroundEntries()
    {
        Assert.Equal(["TC-101"], Ids(await AuditAsync("--action", "ground")));
        Assert.Equal(["TC-102"], Ids(await AuditAsync("--action", "repair")));
        Assert.Equal(["TC-103"], Ids(await AuditAsync("--action", "review")));
    }

    [Fact]
    public async Task AFlaggedBlock_StaysReview_AndAStaleVerdict_StaysCritic()
    {
        await _ws.WriteCriticGroundingAsync("a", "TC-102", VerificationVerdict.Partial, flagged: true);
        var (code, _, err) = await _ws.CompileCriticAsync("a", "TC-101");
        Assert.True(code == 0, err);
        _ws.EditByHand("a", "TC-101");

        var result = await AuditAsync();

        Assert.Equal("review", Action(result, "TC-102"));
        Assert.Equal("critic", Action(result, "TC-101"));
    }

    [Fact]
    public async Task AnUnknownAction_NamesGround()
    {
        var (exit, _, error) = await _ws.RunAsync("ai", "audit-grounding", "--suite", "a", "--action", "bogus");

        Assert.Equal(1, exit);
        Assert.Contains("critic, ground, repair, review, none", error);
    }

    [Fact]
    public void TheResumePath_GroundsRepairsAndReportsByAction()
    {
        // Spec 101 F2: the resume procedure lives in the generate skill's reference.md.
        var skill = SkillReferenceContent.Generate;

        Assert.Contains("audit-grounding --suite {suite} --action ground,repair,review --output-format json", skill);
        Assert.Contains("`action_needed: ground`", skill);
        Assert.Contains("ingest-grounding --suite {suite} --all --ids {ground ids}", skill);
        Assert.Contains("`action_needed: review`", skill);
        Assert.Contains("never send them to the repairer", skill);
    }
}
