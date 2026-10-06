using Spectra.CLI.Baseline;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Git;
using static Spectra.CLI.Tests.TestFixtures.GitWorkspace;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 086 T1, CLI level (FR-001/FR-002, SC-006, analysis I1/C4): every command that reads git — triage, ack, relink
/// (dry run and real), history bootstrap and topology — only ever runs the allowed subcommands with optional locks
/// off, leaves no <c>index.lock</c> under any <c>.git</c> (including the submodule's), and changes nothing git tracks
/// except SPECTRA's own files.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class GitCliReadOnlyTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();
    private readonly string _child;

    public GitCliReadOnlyTests()
    {
        _child = _ws.Root + "-child";
    }

    public void Dispose()
    {
        GitAccess.RunnerOverride = null;
        _ws.Dispose();
        GitFixture.DeleteTree(_child);
    }

    [GitFact]
    public async Task EveryGitReadingCommand_IsReadOnly()
    {
        _ws.InitGit();
        GitFixture.Init(_child);
        File.WriteAllText(Path.Combine(_child, "guide.md"), Doc("Guide", ("Setup", "Install it.")));
        GitFixture.Commit(_child, "child");
        _ws.WriteDoc("docs/a.md", StorageDoc());
        GitFixture.AddSubmodule(_ws.Root, _child, "docs/sub");
        _ws.WriteTest("storage", "TC-101", ["docs/a.md#Quotas", "docs/sub/guide.md#Setup"]);
        await _ws.InitBaselineAsync();
        _ws.Commit("one", new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        _ws.Move("docs/a.md", "docs/guides/a.md");
        _ws.Commit("move", new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero));
        _ws.WriteTest("storage", "TC-102", ["docs/guides/a.md#Sharing"]); // no entry: the bootstrap has work to do
        var trackedBefore = GitFixture.Git(_ws.Root, "ls-files", "-s");

        var runner = new RecordingGitRunner();
        GitAccess.RunnerOverride = runner;
        string[][] commands =
        [
            ["ai", "update-triage", "--suite", "storage"],
            ["ai", "update-ack", "--suite", "storage", "--ids", "TC-101"], // refused (RELINK), but reads git
            ["ai", "relink-sources", "--suite", "storage", "--dry-run"],
            ["ai", "relink-sources", "--suite", "storage"],
            ["ai", "source-baseline", "--init", "--suite", "storage", "--from-date", "2026-03-15"],
            ["docs", "topology"]
        ];
        foreach (var command in commands)
        {
            var before = runner.Calls.Count;
            var (exit, _, error) = await _ws.RunAsync([.. command, "--output-format", "json"]);
            Assert.True(exit is 0 or 4, $"{string.Join(' ', command)} exited {exit}: {error}");
            Assert.True(runner.Calls.Count > before, $"{string.Join(' ', command)} read no git");
            Assert.Empty(GitFixture.IndexLocks(_ws.Root));
        }

        foreach (var call in runner.Calls)
        {
            Assert.Contains(call.Subcommand, GitReader.AllowedSubcommands);
            Assert.Equal("0", call.Env["GIT_OPTIONAL_LOCKS"]);
            Assert.Equal("0", call.Env["GIT_TERMINAL_PROMPT"]);
        }

        // The index git tracks is untouched, and the only working-tree changes are SPECTRA's own files.
        GitAccess.RunnerOverride = null;
        Assert.Equal(trackedBefore, GitFixture.Git(_ws.Root, "ls-files", "-s"));
        var changed = GitFixture.Git(_ws.Root, "status", "--porcelain", "--untracked-files=all")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l[3..].Trim('"')).ToList();
        Assert.NotEmpty(changed);
        Assert.All(changed, path => Assert.True(
            path.StartsWith("test-cases/", StringComparison.Ordinal) || path.StartsWith(".spectra/", StringComparison.Ordinal),
            $"unexpected change: {path}"));
        Assert.DoesNotContain(changed, p => p.Contains(".git/", StringComparison.Ordinal));
    }
}
