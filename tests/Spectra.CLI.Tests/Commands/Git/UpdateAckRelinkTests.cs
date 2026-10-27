using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 086 FR-014 (analysis C5, U1, C6): <c>update-ack</c> refuses a test with a RELINK ref — baselined or not, with
/// or without an entry — on both the <c>--ids</c> and the <c>--group</c> path, with a message naming
/// <c>relink-sources</c>, and never creates an entry for it.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class UpdateAckRelinkTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public UpdateAckRelinkTests()
    {
        if (GitAvailability.IsAvailable) _ws.InitGit();
        _ws.WriteDoc("docs/a.md", StorageDoc());
        _ws.WriteDoc("docs/b.md", Doc("B", ("Limits", "Ten files.")));
    }

    public void Dispose() => _ws.Dispose();

    private async Task<(int Exit, string Error)> AckAsync(params string[] selector)
    {
        var (exit, output, error) = await _ws.RunAsync(["ai", "update-ack", "--suite", "storage", .. selector, "--output-format", "json"]);
        Assert.Empty(output);
        return (exit, error);
    }

    private static void AssertRelinkRefusal(int exit, string error)
    {
        Assert.Equal(4, exit);
        var failure = BaselineTestWorkspace.Json(error);
        Assert.Equal("REF_REMOVED", failure.GetProperty("error_code").GetString());
        Assert.Contains("spectra ai relink-sources --suite storage", failure.GetProperty("errors")[0].GetString());
    }

    private void RenameA()
    {
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Commit("move");
    }

    [GitFact]
    public async Task Ids_BaselinedRefRenamed_IsRefused()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.Commit("one");
        RenameA();
        var before = _ws.ReadBaseline("storage");

        var (exit, error) = await AckAsync("--ids", "TC-101");

        AssertRelinkRefusal(exit, error);
        Assert.Contains("docs/a.md#Quotas → docs/guides/a.md#Quotas", error);
        Assert.Equal(before, _ws.ReadBaseline("storage"));
    }

    [GitFact]
    public async Task Ids_TestWithNoEntry_RenamedBeforeAnyInit_IsRefused_NoEntryCreated()
    {
        _ws.Commit("one");
        RenameA();
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);

        var (exit, error) = await AckAsync("--ids", "TC-101");

        AssertRelinkRefusal(exit, error);
        Assert.Null(_ws.ReadBaseline("storage"));
    }

    [GitFact]
    public async Task Ids_NeverBaselinedMissingRefWithACandidate_IsRefused_EntryUnchanged()
    {
        _ws.Commit("one");
        RenameA();
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas", "docs/b.md#Limits"]);
        await _ws.InitBaselineAsync(); // the entry has docs/b.md only
        var before = _ws.ReadBaseline("storage");

        var (exit, error) = await AckAsync("--ids", "TC-101");

        AssertRelinkRefusal(exit, error);
        Assert.Equal(before, _ws.ReadBaseline("storage"));
    }

    [GitFact]
    public async Task Group_RelinkGroup_IsRefused()
    {
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.Commit("one");
        RenameA();
        var triage = await _ws.TriageAsync();
        var group = Group(triage, "TC-101");
        Assert.Equal("RELINK", group.GetProperty("bucket").GetString());

        var (exit, error) = await AckAsync("--group", group.GetProperty("id").GetString()!);

        AssertRelinkRefusal(exit, error);
    }

    [GitFact]
    public async Task Group_RelinkGroupWithANoEntryTest_IsRefused_NotStale()
    {
        _ws.Commit("one");
        RenameA();
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        var triage = await _ws.TriageAsync();
        var group = Group(triage, "TC-101");

        var (exit, error) = await AckAsync("--group", group.GetProperty("id").GetString()!);

        AssertRelinkRefusal(exit, error);
        Assert.DoesNotContain("TRIAGE_STALE", error);
        Assert.Null(_ws.ReadBaseline("storage"));
    }
}
