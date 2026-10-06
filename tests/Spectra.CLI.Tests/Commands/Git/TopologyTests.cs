using System.Text.Json;
using System.Text.Json.Nodes;
using Spectra.CLI.Baseline;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Git;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 086 T2 (FR-003, FR-004, FR-019): <c>spectra docs topology</c> — the owner of each referenced doc (the repo, a
/// submodule, a nested submodule), and the referenced docs no readable repository owns, with the reason.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class TopologyTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();
    private readonly List<string> _dirs = [];

    public TopologyTests()
    {
        if (GitAvailability.IsAvailable) _ws.InitGit();
    }

    public void Dispose()
    {
        GitAccess.RunnerOverride = null;
        _ws.Dispose();
        foreach (var dir in _dirs) GitFixture.DeleteTree(dir);
    }

    private string Repo(string name, string file)
    {
        var dir = _ws.Root + "-" + name;
        _dirs.Add(dir);
        GitFixture.Init(dir);
        File.WriteAllText(Path.Combine(dir, file), Doc(name, ("Setup", "Install it.")));
        return dir;
    }

    /// <summary>docs/a.md in the repo, docs/sub (submodule) with guide.md, docs/sub/inner (nested) with deep.md.</summary>
    private void Layout()
    {
        var inner = Repo("inner", "deep.md");
        GitFixture.Commit(inner, "inner");
        var sub = Repo("sub", "guide.md");
        GitFixture.Git(sub, "-c", "protocol.file.allow=always", "submodule", "add", "-q", inner, "inner");
        GitFixture.Commit(sub, "sub");
        _ws.WriteDoc("docs/a.md", StorageDoc());
        GitFixture.Git(_ws.Root, "-c", "protocol.file.allow=always", "submodule", "add", "-q", sub, "docs/sub");
        GitFixture.Git(_ws.Root, "-c", "protocol.file.allow=always", "submodule", "update", "--init", "--recursive", "-q");
        _ws.Commit("layout");
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas", "docs/sub/guide.md#Setup"]);
        _ws.WriteTest("storage", "TC-102", ["docs/sub/inner/deep.md#Setup", "docs/a.md#Sharing"]);
    }

    private static JsonElement Owner(JsonElement result, string root) =>
        Assert.Single(result.GetProperty("owners").EnumerateArray(), o => o.GetProperty("root").GetString() == root);

    [GitFact]
    public async Task Owners_RepoSubmoduleAndNestedSubmodule_AndADocOutsideGit()
    {
        Layout();
        var outside = _ws.Root + "-outside";
        _dirs.Add(outside);
        Directory.CreateDirectory(Path.Combine(outside, "docs"));
        File.WriteAllText(Path.Combine(outside, "docs", "x.md"), Doc("X", ("Intro", "Outside.")));
        var config = JsonNode.Parse(File.ReadAllText(_ws.PathOf("spectra.config.json")))!;
        config["source"]!["local_dir"] = Path.Combine(outside, "docs").Replace('\\', '/');
        File.WriteAllText(_ws.PathOf("spectra.config.json"), config.ToJsonString());
        _ws.WriteTest("storage", "TC-103", ["x.md#Intro"]);

        var result = await _ws.RunJsonAsync("docs", "topology");

        var main = Owner(result, ".");
        Assert.False(main.GetProperty("is_submodule").GetBoolean());
        Assert.False(main.GetProperty("shallow").GetBoolean());
        Assert.Equal(1, main.GetProperty("referenced_docs").GetInt32());
        Assert.Equal(GitFixture.Git(_ws.Root, "rev-parse", "HEAD").Trim(), main.GetProperty("head").GetString());
        Assert.True(Owner(result, "docs/sub").GetProperty("is_submodule").GetBoolean());
        Assert.Equal(1, Owner(result, "docs/sub").GetProperty("referenced_docs").GetInt32());
        Assert.True(Owner(result, "docs/sub/inner").GetProperty("is_submodule").GetBoolean());

        var unowned = Assert.Single(result.GetProperty("unowned").EnumerateArray());
        Assert.EndsWith("/docs/x.md", unowned.GetProperty("doc").GetString());
        Assert.Equal("not in a git repository", unowned.GetProperty("reason").GetString());
    }

    [GitFact]
    public async Task DubiousOwnership_IsUnownedWithTheReason()
    {
        Layout();
        GitAccess.RunnerOverride = new FakeGitRunner((dir, args) =>
            args.Contains("rev-parse") && dir.Replace('\\', '/').EndsWith("docs/sub", StringComparison.OrdinalIgnoreCase)
                ? new GitProcessResult(128, [], string.Format(FakeGitRunner.DubiousOwnershipStderr, dir), false)
                : null);

        var result = await _ws.RunJsonAsync("docs", "topology");

        var unowned = Assert.Single(result.GetProperty("unowned").EnumerateArray());
        Assert.Equal("docs/sub/guide.md", unowned.GetProperty("doc").GetString());
        Assert.Equal("repository not readable (dubious ownership)", unowned.GetProperty("reason").GetString());
        Owner(result, ".");
        Owner(result, "docs/sub/inner");
    }

    [GitFact]
    public async Task HumanTable_IsOnStderrInJsonMode()
    {
        Layout();

        var (exit, output, error) = await _ws.RunAsync("docs", "topology", "--output-format", "json");

        Assert.Equal(0, exit);
        Assert.Equal("docs-topology", BaselineTestWorkspace.Json(output).GetProperty("command").GetString());
        Assert.Contains("Docs topology — 3 referenced doc(s), 3 repository(ies)", error);
    }
}
