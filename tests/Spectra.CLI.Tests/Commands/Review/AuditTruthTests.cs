using System.Text.Json;
using Spectra.CLI.Skills;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Review;

/// <summary>
/// Spec 097 F2 (FR-004–FR-009a): <c>audit-grounding --suite S</c> lists only S's indexed tests, flags a verdict the
/// ingest would refuse as stale (<c>stale: true</c>, <c>action_needed: critic</c>, <c>summary.stale</c>), and filters
/// by <c>--action</c> / <c>--ids</c> while the summary keeps describing the whole suite. On 807047c every verdict file
/// was listed (other suites, strays), staleness was not checked, and there were no filters.
/// <para>Suite <c>a</c>: TC-101 grounded verdict, no record; TC-102 partial, edited after its record (stale content);
/// TC-103 partial, verdict older than its record (stale time); TC-104 partial, fresh. Suite <c>b</c>: TC-201 grounded.
/// Stray: TC-999.</para>
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class AuditTruthTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public AuditTruthTests()
    {
        _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();
        _ws.TwoSuites();
        foreach (var id in new[] { "TC-102", "TC-103", "TC-104" })
        {
            var (code, _, err) = _ws.CompileCriticAsync("a", id).GetAwaiter().GetResult();
            Assert.True(code == 0, err);
            _ws.WriteVerdict(id, "partial");
        }

        _ws.EditByHand("a", "TC-102");
        VerificationWorkspace.SetFileTime(_ws.VerdictPath("TC-103"),
            VerificationWorkspace.FileTime(_ws.VerdictRecordPath("TC-103")).AddHours(-1));
    }

    public void Dispose() => _ws.Dispose();

    private async Task<(JsonElement Result, string Err)> AuditAsync(params string[] extra)
    {
        var (exit, output, error) = await _ws.RunAsync(["ai", "audit-grounding", "--suite", "a", .. extra, "--output-format", "json"]);
        Assert.True(exit == 0, error);
        return (VerdictWorkspace.Json(output), error);
    }

    private static List<string> Ids(JsonElement result) =>
        result.GetProperty("tests").EnumerateArray().Select(t => t.GetProperty("id").GetString()!).OrderBy(i => i, StringComparer.Ordinal).ToList();

    private static JsonElement Entry(JsonElement result, string id) =>
        result.GetProperty("tests").EnumerateArray().Single(t => t.GetProperty("id").GetString() == id);

    [Fact]
    public async Task OnlyTheSuitesIndexedTests_AreListed()
    {
        var (result, _) = await AuditAsync();

        Assert.Equal(["TC-101", "TC-102", "TC-103", "TC-104"], Ids(result));
    }

    [Fact]
    public async Task AStaleVerdict_IsFlagged_AndNeedsTheCritic()
    {
        var (result, _) = await AuditAsync();

        foreach (var id in new[] { "TC-102", "TC-103" })
        {
            var entry = Entry(result, id);
            Assert.True(entry.GetProperty("stale").GetBoolean(), id);
            Assert.Equal("critic", entry.GetProperty("action_needed").GetString());
        }

        Assert.False(Entry(result, "TC-104").TryGetProperty("stale", out _), "a fresh verdict has no stale field");
        Assert.False(Entry(result, "TC-101").TryGetProperty("stale", out _), "a verdict without a record has no stale field");
        Assert.Equal(2, result.GetProperty("summary").GetProperty("stale").GetInt32());
    }

    [Fact]
    public async Task ActionRepair_ListsOnlyRepairEntries_AndKeepsTheWholeSuiteSummary()
    {
        var (all, _) = await AuditAsync();
        var (repair, _) = await AuditAsync("--action", "repair");

        // Spec 098 F2 (deliberate change): TC-101's grounded verdict without a block is `ground`, not `repair`.
        Assert.Equal(["TC-104"], Ids(repair));
        Assert.Equal(all.GetProperty("summary").GetRawText(), repair.GetProperty("summary").GetRawText());
    }

    [Fact]
    public async Task ActionValues_CanBeCommaSeparatedOrRepeated()
    {
        var (comma, _) = await AuditAsync("--action", "critic,review");
        var (repeated, _) = await AuditAsync("--action", "critic", "--action", "REVIEW");

        Assert.Equal(["TC-102", "TC-103"], Ids(comma));
        Assert.Equal(Ids(comma), Ids(repeated));
    }

    [Fact]
    public async Task AnUnknownAction_ExitsOne_NamingTheAllowedValues()
    {
        var (exit, _, error) = await _ws.RunAsync("ai", "audit-grounding", "--suite", "a", "--action", "bogus", "--output-format", "json");

        Assert.Equal(1, exit);
        Assert.Contains("critic, ground, repair, review, none", error); // Spec 098 F2: `ground` added
    }

    [Fact]
    public async Task Ids_FilterTheList_AndReportUnknownOnes()
    {
        var (result, error) = await AuditAsync("--ids", "TC-101,TC-999");

        Assert.Equal(["TC-101"], Ids(result));
        Assert.Equal(["TC-999"], result.GetProperty("not_found").EnumerateArray().Select(e => e.GetString()!));
        Assert.Contains("TC-999: not found in suite 'a'", error); // FR-009a: stderr in JSON mode
    }

    [Fact]
    public async Task EmptyIds_AreTheSameAsNoIds()
    {
        var (all, _) = await AuditAsync();
        var (empty, _) = await AuditAsync("--ids", " , ");

        Assert.Equal(Ids(all), Ids(empty));
    }

    [Fact]
    public async Task TheHumanTable_ShowsStaleEntries()
    {
        var (exit, output, error) = await _ws.RunAsync("ai", "audit-grounding", "--suite", "a");

        Assert.True(exit == 0, error);
        Assert.Contains("critic (stale)", output);
        Assert.Contains("2 stale", output);
    }

    [Fact]
    public void TheGenerateSkillsResumePath_AsksOnlyForWhatItActsOn() =>
        // Spec 098 F2 (deliberate change): the resume path also grounds and reports (was: `--action repair` only).
        // Spec 101 F2: the resume procedure lives in the generate skill's reference.md.
        Assert.Contains("audit-grounding --suite {suite} --action ground,repair,review --output-format json", SkillReferenceContent.Generate);
}
