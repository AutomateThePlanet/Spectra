using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Git;
using Spectra.MCP.Identity;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 087 T3 (F3, FR-009–FR-011): variables inherited from a git hook or a shell never redirect SPECTRA's git calls.
/// These tests set <c>GIT_*</c> variables in the test process, so they run alone (<see cref="GitEnvironmentCollection"/>)
/// and set them only around the SPECTRA call.
/// </summary>
[Collection(GitEnvironmentCollection.Name)]
public sealed class GitEnvironmentIsolationTests : IDisposable
{
    private static readonly Dictionary<string, string?> Redirecting = new()
    {
        ["GIT_DIR"] = "/nowhere/.git",
        ["GIT_WORK_TREE"] = "/nowhere",
        ["GIT_INDEX_FILE"] = "/nowhere/.git/index",
        ["GIT_OBJECT_DIRECTORY"] = "/nowhere/objects",
        ["GIT_ALTERNATE_OBJECT_DIRECTORIES"] = "/nowhere/alt",
        ["GIT_COMMON_DIR"] = "/nowhere/common",
        ["GIT_NAMESPACE"] = "ns",
        ["GIT_CEILING_DIRECTORIES"] = "/",
        ["GIT_DISCOVERY_ACROSS_FILESYSTEM"] = "1",
        ["GIT_CONFIG"] = "/nowhere/config",
        ["GIT_CONFIG_PARAMETERS"] = "'user.name'='Injected'",
        ["GIT_CONFIG_COUNT"] = "1",
        ["GIT_CONFIG_KEY_0"] = "user.name",
        ["GIT_CONFIG_VALUE_0"] = "Injected"
    };

    private readonly BaselineTestWorkspace _ws = new();
    private readonly string _child;

    public GitEnvironmentIsolationTests()
    {
        _child = _ws.Root + "-child";
    }

    public void Dispose()
    {
        new UserIdentityResolver().ClearCache();
        _ws.Dispose();
        GitFixture.DeleteTree(_child);
    }

    [GitFact]
    public void StartInfo_HasNoRedirectingVariable_AndKeepsTheSettings()
    {
        System.Diagnostics.ProcessStartInfo? runner, identity;
        using (new ScopedEnvironment(Redirecting))
        {
            runner = new ProcessGitRunner().CreateStartInfo(_ws.Root, ["-C", _ws.Root, "rev-parse"], GitReader.Environment);
            identity = UserIdentityResolver.CreateGitStartInfo();
        }

        foreach (var info in new[] { runner!, identity! })
        {
            foreach (var name in Redirecting.Keys)
            {
                Assert.False(info.Environment.ContainsKey(name), $"{name} reached {info.FileName}");
            }
        }

        Assert.Equal("0", runner!.Environment["GIT_OPTIONAL_LOCKS"]);
        Assert.Equal("0", runner.Environment["GIT_TERMINAL_PROMPT"]);
        Assert.True(runner.Environment.ContainsKey("PATH") || runner.Environment.ContainsKey("Path"), "the rest is inherited");
    }

    [GitFact]
    public async Task APreCommitHookEnvironment_StillFindsTheSubmodulesRename()
    {
        _ws.InitGit();
        GitFixture.Init(_child);
        File.WriteAllText(Path.Combine(_child, "guide.md"), StorageDoc());
        GitFixture.Commit(_child, "child");
        GitFixture.AddSubmodule(_ws.Root, _child, "docs/sub");
        _ws.WriteTest("storage", "TC-101", ["docs/sub/guide.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.Commit("baseline");
        var sub = _ws.PathOf("docs/sub");
        GitFixture.Move(sub, "guide.md", "guides/storage.md");
        GitFixture.Commit(sub, "rename in the submodule");

        // What git sets for a hook of the superproject.
        var gitDir = _ws.PathOf(".git");
        using (new ScopedEnvironment(new Dictionary<string, string?> { ["GIT_DIR"] = gitDir, ["GIT_INDEX_FILE"] = Path.Combine(gitDir, "index") }))
        {
            var triage = await _ws.TriageAsync();

            var group = Group(triage, "TC-101");
            Assert.Equal("RELINK", group.GetProperty("bucket").GetString());
            Assert.Equal(["renamed to docs/sub/guides/storage.md"], Strings(group.GetProperty("why")));
        }
    }

    [GitTheory]
    [InlineData("parameters")]
    [InlineData("count")]
    [InlineData("file")]
    public void InjectedConfig_NeverBecomesTheExecutionIdentity(string route)
    {
        // Each route on its own makes `git config user.name` answer "Injected" (probed on git 2.53).
        var file = Path.Combine(_ws.Root, "injected.cfg");
        File.WriteAllText(file, "[user]\n\tname = Injected\n");
        var variables = route switch
        {
            "parameters" => new Dictionary<string, string?> { ["GIT_CONFIG_PARAMETERS"] = "'user.name'='Injected'" },
            "count" => new Dictionary<string, string?> { ["GIT_CONFIG_COUNT"] = "1", ["GIT_CONFIG_KEY_0"] = "user.name", ["GIT_CONFIG_VALUE_0"] = "Injected" },
            _ => new Dictionary<string, string?> { ["GIT_CONFIG"] = file }
        };
        var resolver = new UserIdentityResolver();
        resolver.ClearCache();
        string identity;
        using (new ScopedEnvironment(variables))
        {
            identity = resolver.GetCurrentUser();
        }

        resolver.ClearCache();
        Assert.NotEqual("Injected", identity);
    }
}
