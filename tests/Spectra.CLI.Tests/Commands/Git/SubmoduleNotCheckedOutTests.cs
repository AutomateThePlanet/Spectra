using System.Text.Json;
using System.Text.Json.Nodes;
using Spectra.CLI.Tests.TestFixtures;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 087 T1 (F1, FR-001–FR-004a): in a clone made without its submodules, refs into a submodule are a setup
/// problem — REMOVED with "submodule not checked out", one unchunked group per submodule, never RELINK — and a note
/// and an <c>--init</c> warning count them even when nothing is baselined yet.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class SubmoduleNotCheckedOutTests : IDisposable
{
    private const string Reason = "submodule not checked out: docs/sub (run git submodule update --init)";
    private const string Note = "4 reference(s) from 3 test(s) point into submodule docs/sub, which isn't checked out (run git submodule update --init)";

    private readonly BaselineTestWorkspace _super = new();
    private readonly BaselineTestWorkspace _clone = new();
    private readonly List<string> _repos = [];

    public void Dispose()
    {
        _super.Dispose();
        _clone.Dispose();
        foreach (var repo in _repos) GitFixture.DeleteTree(repo);
    }

    private string Repo(string name, params (string File, string Title)[] docs)
    {
        var dir = _super.Root + "-" + name;
        _repos.Add(dir);
        GitFixture.Init(dir);
        foreach (var (file, title) in docs)
        {
            File.WriteAllText(Path.Combine(dir, file), Doc(title, ("Setup", $"Install {title}.")));
        }

        GitFixture.Commit(dir, name);
        return dir;
    }

    /// <summary>
    /// A superproject with <c>docs/sub</c> (guide.md, setup.md) and three tests — TC-101 [guide], TC-102 [setup],
    /// TC-103 [guide, setup] (4 references from 3 tests) — optionally baselined, committed, then cloned without submodules.
    /// </summary>
    private async Task ClonedAsync(bool baselined = true, Action? beforeCommit = null, Action<string>? childSetup = null)
    {
        if (baselined && beforeCommit is null && childSetup is null)
        {
            // Spec 099 F7: the default setup is built once per test process and its clone copied here (the tests that
            // use it only touch the clone; its origin and submodule URLs point at the template's repositories, which
            // live until the process exits).
            GitFixture.DeleteTree(_clone.Root);
            CopyTree(await DefaultClone.Value, _clone.Root);
            Assert.Empty(Directory.EnumerateFileSystemEntries(_clone.PathOf("docs/sub")));
            return;
        }

        await BuildAsync(baselined, beforeCommit, childSetup);
    }

    private static readonly Lazy<Task<string>> DefaultClone = new(async () =>
    {
        var template = new SubmoduleNotCheckedOutTests();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => template.Dispose();
        await template.BuildAsync(baselined: true, beforeCommit: null, childSetup: null);
        return template._clone.Root;
    });

    private static void CopyTree(string from, string to)
    {
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        }

        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)));
        }
    }

    private async Task BuildAsync(bool baselined, Action? beforeCommit, Action<string>? childSetup)
    {
        _super.InitGit();
        var child = Repo("child", ("guide.md", "Guide"), ("setup.md", "Setup"));
        childSetup?.Invoke(child);
        GitFixture.AddSubmodule(_super.Root, child, "docs/sub");
        _super.WriteTest("storage", "TC-101", ["docs/sub/guide.md#Setup"]);
        _super.WriteTest("storage", "TC-102", ["docs/sub/setup.md#Setup"]);
        _super.WriteTest("storage", "TC-103", ["docs/sub/guide.md#Setup", "docs/sub/setup.md#Setup"]);
        beforeCommit?.Invoke();
        if (baselined) await _super.InitBaselineAsync();
        _super.Commit("tests");
        _super.CloneInto(_clone);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_clone.PathOf("docs/sub")));
    }

    private static string[] Notes(JsonElement result) => Strings(result.GetProperty("notes"));

    private static JsonElement RefItems(JsonElement group) => group.GetProperty("refs");

    [GitFact]
    public async Task EveryRef_IsRemovedWithTheSubmoduleReason_InOneUnchunkedGroup_WithTheNote()
    {
        await ClonedAsync();
        var config = JsonNode.Parse(File.ReadAllText(_clone.PathOf("spectra.config.json")))!;
        config["update"] = new JsonObject { ["group_size"] = 2 };
        File.WriteAllText(_clone.PathOf("spectra.config.json"), config.ToJsonString());

        var triage = await _clone.TriageAsync();

        var removed = triage.GetProperty("removed").EnumerateArray().ToList();
        Assert.Equal(4, removed.Count);
        Assert.All(removed, r => Assert.Equal($"not found; {Reason}", r.GetProperty("reason").GetString()));
        var group = Assert.Single(triage.GetProperty("groups").EnumerateArray());
        Assert.Equal("REMOVED", group.GetProperty("bucket").GetString());
        Assert.Equal(["TC-101", "TC-102", "TC-103"], Strings(group.GetProperty("tests")));
        var item = Assert.Single(RefItems(group).EnumerateArray());
        Assert.Equal("docs/sub", item.GetProperty("ref").GetString());
        Assert.Equal("REMOVED", item.GetProperty("state").GetString());
        Assert.True(item.GetProperty("submodule").GetBoolean());
        Assert.Equal([$"not found; {Reason}"], Strings(group.GetProperty("why")));
        Assert.Equal(0, BaselineTestWorkspace.Count(triage, "storage", "relink"));
        Assert.Contains(Note, Notes(triage));
    }

    [GitFact]
    public async Task RelinkSources_ListsThemAsNotRelinked_WithTheBareReason()
    {
        await ClonedAsync();

        var relink = await _clone.RunJsonAsync("ai", "relink-sources", "--suite", "storage", "--dry-run");

        Assert.Empty(relink.GetProperty("relinked").EnumerateArray());
        var notRelinked = relink.GetProperty("not_relinked").EnumerateArray().ToList();
        Assert.Equal(4, notRelinked.Count);
        Assert.All(notRelinked, n => Assert.Equal(Reason, n.GetProperty("reason").GetString()));
    }

    [GitFact]
    public async Task UpdateAck_RefusesThem()
    {
        await ClonedAsync();

        var (exit, _, error) = await _clone.RunAsync("ai", "update-ack", "--suite", "storage", "--ids", "TC-101", "--output-format", "json");

        Assert.Equal(4, exit);
        Assert.Equal("REF_REMOVED", BaselineTestWorkspace.Json(error).GetProperty("error_code").GetString());
    }

    [GitFact]
    public async Task Bootstrap_LeavesThemUnbaselined_WithTheBareReason_AndWarns()
    {
        await ClonedAsync();
        _clone.WriteTest("storage", "TC-105", ["docs/sub/guide.md#Setup"]); // no entry

        var result = await _clone.InitBaselineAsync("storage", "--from-date", "2100-01-01");

        var unbaselined = result.GetProperty("suites")[0].GetProperty("unbaselined").EnumerateArray()
            .Single(u => u.GetProperty("test").GetString() == "TC-105");
        Assert.Equal(Reason, unbaselined.GetProperty("reason").GetString());
        Assert.Contains(Strings(result.GetProperty("warnings")), w => w.Contains("point into submodule docs/sub", StringComparison.Ordinal));
    }

    [GitFact]
    public async Task TheSubmoduleFolderDeletedEntirely_GivesTheSameReason()
    {
        await ClonedAsync();
        Directory.Delete(_clone.PathOf("docs/sub"));

        var triage = await _clone.TriageAsync();

        Assert.All(triage.GetProperty("removed").EnumerateArray(),
            r => Assert.Equal($"not found; {Reason}", r.GetProperty("reason").GetString()));
    }

    [GitFact]
    public async Task ATestWithRefsIntoTwoSubmodules_IsKeyedOnBoth()
    {
        await ClonedAsync(beforeCommit: () =>
        {
            GitFixture.AddSubmodule(_super.Root, Repo("other", ("notes.md", "Notes")), "docs/other");
            _super.WriteTest("storage", "TC-104", ["docs/sub/guide.md#Setup", "docs/other/notes.md#Setup"]);
        });

        var triage = await _clone.TriageAsync();

        var group = Group(triage, "TC-104");
        Assert.Equal(["TC-104"], Strings(group.GetProperty("tests")));
        Assert.Equal(["docs/other", "docs/sub"],
            RefItems(group).EnumerateArray().Select(i => i.GetProperty("ref").GetString()!).OrderBy(r => r, StringComparer.Ordinal));
        Assert.Contains("1 reference(s) from 1 test(s) point into submodule docs/other, which isn't checked out (run git submodule update --init)",
            Notes(triage));
    }

    [GitFact]
    public async Task ANestedSubmoduleNotCheckedOut_NamesTheNestedPath()
    {
        await ClonedAsync(
            childSetup: child =>
            {
                // The child repository owns its own submodule, so a clone can fetch every commit it records.
                GitFixture.AddSubmodule(child, Repo("inner", ("deep.md", "Deep")), "inner");
            },
            beforeCommit: () =>
            {
                // Check the nested submodule out in the superproject, so TC-106 is baselined there.
                GitFixture.Git(_super.PathOf("docs/sub"), "-c", "protocol.file.allow=always", "submodule", "update", "--init", "-q");
                _super.WriteTest("storage", "TC-106", ["docs/sub/inner/deep.md#Setup"]);
            });
        _clone.InitSubmodules(); // docs/sub checked out, docs/sub/inner not

        var triage = await _clone.TriageAsync();

        Assert.Equal("not found; submodule not checked out: docs/sub/inner (run git submodule update --init)",
            Removed(triage, "TC-106").GetProperty("reason").GetString());
        Assert.Equal(3, BaselineTestWorkspace.Count(triage, "storage", "unchanged")); // TC-101..103 resolve now
    }

    [GitFact]
    public async Task ACheckedOutSubmodule_KeepsTheNormalBehaviour()
    {
        await ClonedAsync();
        _clone.InitSubmodules();
        var sub = _clone.PathOf("docs/sub");
        GitFixture.Git(sub, "checkout", "-q", "-b", "work");
        GitFixture.Move(sub, "guide.md", "guides/guide.md");
        File.Delete(Path.Combine(sub, "setup.md"));
        GitFixture.Commit(sub, "rename guide, delete setup");

        var triage = await _clone.TriageAsync();

        Assert.Equal("RELINK", Group(triage, "TC-101").GetProperty("bucket").GetString());
        Assert.Equal("not found; no rename found at 90% similarity", Removed(triage, "TC-102").GetProperty("reason").GetString());
        Assert.DoesNotContain(Notes(triage), n => n.Contains("point into submodule", StringComparison.Ordinal));
    }

    [GitFact]
    public async Task AFreshCloneWithNoBaselines_GetsTheNoteAndTheInitWarning()
    {
        await ClonedAsync(baselined: false);

        var triage = await _clone.TriageAsync();
        Assert.Equal(3, BaselineTestWorkspace.Count(triage, "storage", "unbaselined"));
        Assert.Contains(Note, Notes(triage));

        var (exit, output, error) = await _clone.RunAsync("ai", "source-baseline", "--init", "--suite", "storage", "--output-format", "json");
        Assert.Equal(0, exit);
        Assert.Contains(Note, Strings(BaselineTestWorkspace.Json(output).GetProperty("warnings")));
        Assert.Contains($"Warning: {Note}", error);
        var tests = _clone.BaselineJson("storage").GetProperty("tests");
        Assert.All(tests.EnumerateObject(), t => Assert.Empty(t.Value.GetProperty("refs").EnumerateObject()));

        var fromDate = await _clone.InitBaselineAsync("storage", "--from-date", "2100-01-01", "--dry-run");
        Assert.Contains(Note, Strings(fromDate.GetProperty("warnings")));
    }
}
