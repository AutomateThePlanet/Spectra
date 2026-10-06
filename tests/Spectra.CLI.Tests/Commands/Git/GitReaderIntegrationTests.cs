using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Git;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 086 T1, reader level (FR-001/FR-002, analysis I1): <see cref="GitReader"/> and <see cref="OwnerResolver"/>
/// against a real repository with a submodule and a non-ASCII path. The CLI-level part is GitCliReadOnlyTests.
/// </summary>
public sealed class GitReaderIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"spectra-086-reader-{Guid.NewGuid():N}");

    public void Dispose() => GitFixture.DeleteTree(_root);

    [GitFact]
    public async Task Reads_OnlyAllowedSubcommands_NoLocks_NonAsciiRoundTrips()
    {
        var child = Path.Combine(_root, "child");
        GitFixture.Init(child);
        File.WriteAllText(Path.Combine(child, "guide.md"), "# Guide\n\n## Setup\n\nInstall it.\n");
        GitFixture.Commit(child, "child");

        var repo = Path.Combine(_root, "repo");
        GitFixture.Init(repo);
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        File.WriteAllText(Path.Combine(repo, "docs", "über.md"), "# Über\n\n## Quotas\n\nEach user gets 10GB of storage.\n");
        GitFixture.Commit(repo, "one");
        GitFixture.Move(repo, "docs/über.md", "docs/neu/über.md");
        GitFixture.Commit(repo, "two");
        GitFixture.AddSubmodule(repo, child, "docs/sub");

        var runner = new RecordingGitRunner();
        var owners = new OwnerResolver(new GitReader(runner));

        var (main, mainReason) = await owners.OwnerOfAsync(Path.Combine(repo, "docs", "über.md"), CancellationToken.None);
        Assert.Null(mainReason);
        Assert.False(main!.IsSubmodule);
        Assert.Equal(Path.GetFullPath(repo), main.Root, ignoreCase: !OperatingSystem.IsLinux());

        var (sub, _) = await owners.OwnerOfAsync(Path.Combine(repo, "docs", "sub", "guide.md"), CancellationToken.None);
        Assert.True(sub!.IsSubmodule);
        Assert.Equal("guide.md", OwnerResolver.RelativePath(sub, Path.Combine(repo, "docs", "sub", "guide.md")));

        var (history, _) = await owners.RenameHistoryAsync(main, 90, CancellationToken.None);
        var (target, reason) = history!.ForwardCandidate("docs/über.md", p => File.Exists(Path.Combine(repo, p)));
        Assert.Null(reason);
        Assert.Equal("docs/neu/über.md", target);

        Assert.NotEmpty(runner.Calls);
        foreach (var call in runner.Calls)
        {
            Assert.Contains(call.Subcommand, GitReader.AllowedSubcommands);
            Assert.Equal("0", call.Env["GIT_OPTIONAL_LOCKS"]);
            Assert.Equal(["-C", call.Dir, "-c", "core.quotepath=false"], call.Args.Take(4));
        }

        Assert.Contains(runner.Calls, c => c.Subcommand == "log" && c.Args.Contains("-z"));
        Assert.Empty(GitFixture.IndexLocks(repo));
    }
}
