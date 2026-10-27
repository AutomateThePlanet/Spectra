using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 086 T3 (FR-004–FR-007, FR-012/FR-013): what triage reports for a ref whose doc file is missing, from a real
/// repository history — RELINK with the target, or REMOVED with <c>not found; &lt;reason&gt;</c>.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class RenameTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public RenameTests()
    {
        if (GitAvailability.IsAvailable) _ws.InitGit();
    }

    public void Dispose() => _ws.Dispose();

    /// <summary>One test (TC-101) referencing <paramref name="sourceRef"/>, baselined and committed.</summary>
    private async Task BaselinedAsync(string sourceRef = "docs/a.md#Quotas")
    {
        _ws.WriteTest("storage", "TC-101", [sourceRef]);
        await _ws.InitBaselineAsync();
        _ws.Commit("baseline");
    }

    private void Doc(string relative, string quota = "10GB") => _ws.WriteDoc(relative, StorageDoc(quota));

    private static void AssertRelink(JsonElement triage, string target)
    {
        var group = Group(triage, "TC-101");
        Assert.Equal("RELINK", group.GetProperty("bucket").GetString());
        Assert.Equal([$"renamed to {target}"], Strings(group.GetProperty("why")));
        Assert.Equal(target, group.GetProperty("refs")[0].GetProperty("target").GetString());
        Assert.Empty(triage.GetProperty("removed").EnumerateArray());
    }

    private static void AssertRemoved(JsonElement triage, string reason)
    {
        Assert.Equal("REMOVED", Group(triage, "TC-101").GetProperty("bucket").GetString());
        Assert.Equal(reason, Removed(triage, "TC-101").GetProperty("reason").GetString());
    }

    [GitFact]
    public async Task PlainRename_IsRelink()
    {
        Doc("docs/a.md");
        _ws.Commit("one");
        await BaselinedAsync();
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Commit("move");

        AssertRelink(await _ws.TriageAsync(), "docs/guides/a.md");
    }

    [GitFact]
    public async Task EditAboveTheThreshold_IsRelink()
    {
        Doc("docs/a.md");
        _ws.Commit("one");
        await BaselinedAsync();
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Edit("docs/guides/a.md", "10GB", "50GB");
        _ws.Commit("move and edit");

        AssertRelink(await _ws.TriageAsync(), "docs/guides/a.md");
    }

    [GitFact]
    public async Task EditBelowTheThreshold_IsRemovedAt90_ButRelinkedAt50()
    {
        // git scores this rename 54%: below 90, above 50.
        _ws.WriteDoc("docs/a.md", "# Storage\n\n## Quotas\n\nEach user gets 10GB of storage space.\nUsers must log in first.\n"
            + "Quotas reset monthly.\nAdmins can raise a quota.\n");
        _ws.Commit("one");
        await BaselinedAsync();
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.WriteDoc("docs/guides/a.md", "# Storage\n\n## Quotas\n\nEach member receives 50GB of space.\nUsers must log in first.\n"
            + "Quotas reset every week.\nAdmins can raise a quota.\n");
        _ws.Commit("move and rewrite");

        AssertRemoved(await _ws.TriageAsync(), "not found; no rename found at 90% similarity");

        var relink = await _ws.RunJsonAsync("ai", "relink-sources", "--suite", "storage", "--min-similarity", "50", "--dry-run");
        Assert.Equal("docs/guides/a.md#Quotas", RelinkedTo(relink, "TC-101"));
    }

    [GitFact]
    public async Task PlainChain_IsRelinkedToTheEnd()
    {
        Doc("docs/a.md");
        _ws.Commit("one");
        await BaselinedAsync();
        _ws.Move("docs/a.md", "docs/b.md");
        _ws.Commit("a→b");
        _ws.Move("docs/b.md", "docs/c.md");
        _ws.Commit("b→c");

        AssertRelink(await _ws.TriageAsync(), "docs/c.md");
        var relink = await _ws.RunJsonAsync("ai", "relink-sources", "--suite", "storage", "--dry-run");
        Assert.Equal("docs/c.md#Quotas", RelinkedTo(relink, "TC-101"));
    }

    [GitFact]
    public async Task RenamedToTwoPlaces_IsAmbiguous()
    {
        Doc("docs/a.md");
        _ws.Commit("one");
        await BaselinedAsync();
        GitFixture.Git(_ws.Root, "checkout", "-q", "-b", "other");
        _ws.Move("docs/a.md", "docs/b.md");
        _ws.Commit("a→b on other");
        GitFixture.Git(_ws.Root, "checkout", "-q", "main");
        _ws.Move("docs/a.md", "docs/c.md");
        _ws.Commit("a→c on main");
        GitFixture.Git(_ws.Root, "merge", "-q", "-s", "ours", "-m", "merge other", "other");

        AssertRemoved(await _ws.TriageAsync(), "not found; ambiguous rename");
        var relink = await _ws.RunJsonAsync("ai", "relink-sources", "--suite", "storage", "--dry-run");
        Assert.Null(RelinkedTo(relink, "TC-101"));
    }

    [GitFact]
    public async Task RecreatedThenRenamedAgain_IsAmbiguous_AndNotRelinked()
    {
        // Analysis A1: a → b, then a re-created and a → c. Two distinct targets anywhere in the history.
        Doc("docs/a.md");
        _ws.Commit("one");
        await BaselinedAsync();
        _ws.Move("docs/a.md", "docs/b.md");
        _ws.Commit("a→b");
        Doc("docs/a.md", "20GB");
        _ws.Commit("a re-created");
        _ws.Move("docs/a.md", "docs/c.md");
        _ws.Commit("a→c");

        AssertRemoved(await _ws.TriageAsync(), "not found; ambiguous rename");
        var relink = await _ws.RunJsonAsync("ai", "relink-sources", "--suite", "storage");
        Assert.Null(RelinkedTo(relink, "TC-101"));
        Assert.Contains("docs/a.md#Quotas", File.ReadAllText(_ws.PathOf("test-cases/storage/TC-101.md")));
    }

    [GitFact]
    public async Task RenamedIntoAFileLaterDeleted_ChainEndsMissing()
    {
        Doc("docs/a.md");
        _ws.Commit("one");
        await BaselinedAsync();
        _ws.Move("docs/a.md", "docs/b.md");
        _ws.Commit("a→b");
        File.Delete(_ws.PathOf("docs/b.md"));
        _ws.Commit("delete b");

        AssertRemoved(await _ws.TriageAsync(), "not found; rename chain ends at a missing file");
    }

    [GitFact]
    public async Task FolderGone_OwnerFromTheNearestExistingParent()
    {
        Doc("docs/old/a.md");
        _ws.Commit("one");
        await BaselinedAsync("docs/old/a.md#Quotas");
        _ws.Move("docs/old/a.md", "docs/new/a.md");
        _ws.Commit("move folder");
        Assert.False(Directory.Exists(_ws.PathOf("docs/old")));

        AssertRelink(await _ws.TriageAsync(), "docs/new/a.md");
    }

    [GitFact]
    public async Task RenameInsideASubmodule_IsRelinkWithTheParentRelativeTarget()
    {
        var child = _ws.Root + "-child";
        try
        {
            GitFixture.Init(child);
            File.WriteAllText(Path.Combine(child, "old.md"), StorageDoc());
            GitFixture.Commit(child, "child");
            GitFixture.AddSubmodule(_ws.Root, child, "docs/sub");

            await BaselinedAsync("docs/sub/old.md#Quotas");
            var sub = _ws.PathOf("docs/sub");
            GitFixture.Move(sub, "old.md", "guides/new.md");
            GitFixture.Commit(sub, "move in submodule");

            AssertRelink(await _ws.TriageAsync(), "docs/sub/guides/new.md");
        }
        finally
        {
            GitFixture.DeleteTree(child);
        }
    }

    [GitFact]
    public async Task BareLegacyRef_TargetStillUnderTheSourceDir_StaysSourceDirRelative()
    {
        Doc("docs/a.md");
        _ws.Commit("one");
        await BaselinedAsync("a.md#Quotas");
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Commit("move");

        AssertRelink(await _ws.TriageAsync(), "docs/guides/a.md");
        var relink = await _ws.RunJsonAsync("ai", "relink-sources", "--suite", "storage", "--dry-run");
        Assert.Equal("guides/a.md#Quotas", RelinkedTo(relink, "TC-101"));
    }

    [GitFact]
    public async Task BareLegacyRef_TargetMovedOutOfTheSourceDir_BecomesRepoRootRelative()
    {
        Doc("docs/a.md");
        _ws.Commit("one");
        await BaselinedAsync("a.md#Quotas");
        _ws.Move("docs/a.md", "manual/a.md");
        _ws.Commit("move out of docs");

        var relink = await _ws.RunJsonAsync("ai", "relink-sources", "--suite", "storage", "--dry-run");
        Assert.Equal("manual/a.md#Quotas", RelinkedTo(relink, "TC-101"));
    }

    [GitFact]
    public async Task TargetOutsideTheAllowedRoots_IsRemoved()
    {
        // The source dir is a sibling repository; the doc moves out of its docs/ folder, outside both roots.
        var ext = _ws.Root + "-ext";
        try
        {
            GitFixture.Init(ext);
            Directory.CreateDirectory(Path.Combine(ext, "docs"));
            File.WriteAllText(Path.Combine(ext, "docs", "a.md"), StorageDoc());
            GitFixture.Commit(ext, "ext");
            var config = BaselineTestWorkspace.Json(File.ReadAllText(_ws.PathOf("spectra.config.json")));
            var node = System.Text.Json.Nodes.JsonNode.Parse(config.GetRawText())!;
            node["source"]!["local_dir"] = $"../{Path.GetFileName(ext)}/docs";
            File.WriteAllText(_ws.PathOf("spectra.config.json"), node.ToJsonString());
            _ws.Commit("config");

            await BaselinedAsync("a.md#Quotas");
            GitFixture.Move(ext, "docs/a.md", "other/a.md");
            GitFixture.Commit(ext, "move out");

            AssertRemoved(await _ws.TriageAsync(), "not found; renamed outside the allowed roots");
        }
        finally
        {
            GitFixture.DeleteTree(ext);
        }
    }

    // ---- analysis H1: the rename happened before any baseline ----

    [GitFact]
    public async Task RenameBeforeInit_InATestWithAnEntry_IsRelink_NotUnbaselined()
    {
        Doc("docs/a.md");
        _ws.WriteDoc("docs/b.md", "# B\n\n## Limits\n\nTen files.\n");
        _ws.Commit("one");
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Commit("move before any baseline");
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas", "docs/b.md#Limits"]);
        await _ws.InitBaselineAsync(); // records docs/b.md only: docs/a.md is missing

        var triage = await _ws.TriageAsync();

        AssertRelink(triage, "docs/guides/a.md");
        Assert.Equal(0, BaselineTestWorkspace.Count(triage, "storage", "partial"));
        Assert.Empty(Strings(_ws.DetailOf("TC-101").GetProperty("unbaselined_refs")));
    }

    [GitFact]
    public async Task RenameBeforeInit_InATestWithNoEntry_IsListedOnceUnderRelink()
    {
        Doc("docs/a.md");
        _ws.Commit("one");
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Commit("move before any baseline");
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas"]);
        _ws.WriteTest("storage", "TC-102", ["docs/guides/a.md#Quotas"]); // no entry, nothing to relink

        var triage = await _ws.TriageAsync();

        AssertRelink(triage, "docs/guides/a.md");
        Assert.Equal(1, BaselineTestWorkspace.Count(triage, "storage", "relink"));
        Assert.Equal(1, BaselineTestWorkspace.Count(triage, "storage", "unbaselined"));
        var unbaselined = Assert.Single(triage.GetProperty("unbaselined").EnumerateArray());
        Assert.Equal(1, unbaselined.GetProperty("full").GetInt32());

        var detail = _ws.DetailOf("TC-101");
        Assert.True(detail.GetProperty("unbaselined").GetBoolean());
        Assert.Equal("RELINK", detail.GetProperty("bucket").GetString());
        Assert.True(detail.GetProperty("current").GetProperty("refs").TryGetProperty("docs/a.md#Quotas", out _));
        var full = BaselineTestWorkspace.Json(File.ReadAllText(_ws.PathOf(".spectra/update-triage.json")))
            .GetProperty("unbaselined").GetProperty("storage").GetProperty("full");
        Assert.Equal(["TC-102"], Strings(full));
    }
}
