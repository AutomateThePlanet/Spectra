using System.Text;
using Spectra.Core.Git;

namespace Spectra.Core.Tests.Git;

/// <summary>Spec 086 T1 (FR-001–FR-003, research R1): the reader's allow-list, arguments, environment and reasons.</summary>
public class GitReaderTests
{
    private sealed class FakeRunner(GitProcessResult result) : IGitProcessRunner
    {
        public List<(string Dir, IReadOnlyList<string> Args, IReadOnlyDictionary<string, string> Env)> Calls { get; } = [];

        public Task<GitProcessResult> RunAsync(
            string workingDir, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env, byte[]? stdin, CancellationToken ct)
        {
            Calls.Add((workingDir, args.ToList(), env));
            return Task.FromResult(result);
        }
    }

    private static GitProcessResult Ok(string stdout = "") => new(0, Encoding.UTF8.GetBytes(stdout), "", false);

    [Theory]
    [InlineData("rev-parse")]
    [InlineData("log")]
    [InlineData("show")]
    [InlineData("ls-files")]
    [InlineData("cat-file")]
    public async Task AllowedSubcommand_RunsWithDirQuotepathAndEnvironment(string subcommand)
    {
        var runner = new FakeRunner(Ok("x"));
        var result = await new GitReader(runner).RunAsync("/repo", subcommand, "--a", "b");

        Assert.True(result.Ok);
        var call = Assert.Single(runner.Calls);
        Assert.Equal(["-C", "/repo", "-c", "core.quotepath=false", subcommand, "--a", "b"], call.Args);
        Assert.Equal("0", call.Env["GIT_OPTIONAL_LOCKS"]);
        Assert.Equal("0", call.Env["GIT_TERMINAL_PROMPT"]);
    }

    [Fact]
    public void TheAllowedSubcommands_AreExactlyTheFiveReadOnlyOnes() =>
        // Spec 087 T6 (FR-019): the review fixes add no git subcommand (submodules are read with ls-files).
        Assert.Equal(new[] { "cat-file", "log", "ls-files", "rev-parse", "show" }, GitReader.AllowedSubcommands.OrderBy(s => s, StringComparer.Ordinal));

    [Theory]
    [InlineData("status")]
    [InlineData("add")]
    [InlineData("fetch")]
    [InlineData("checkout")]
    [InlineData("rev-list")]
    [InlineData("config")]
    public async Task OtherSubcommand_ThrowsBeforeAnyProcessStarts(string subcommand)
    {
        var runner = new FakeRunner(Ok());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new GitReader(runner).RunAsync("/repo", subcommand));
        Assert.Empty(runner.Calls);
    }

    [Theory]
    [InlineData("fatal: not a git repository (or any of the parent directories): .git", "not in a git repository")]
    [InlineData("fatal: detected dubious ownership in repository at 'C:/x'\nTo add an exception …", "repository not readable (dubious ownership)")]
    [InlineData("fatal: bad revision 'nosuch'\nmore", "git failed: fatal: bad revision 'nosuch'")]
    [InlineData("", "git failed: exit code 128")]
    public async Task Failure_MapsToTheUnavailableReason(string stderr, string reason)
    {
        var result = await new GitReader(new FakeRunner(new GitProcessResult(128, [], stderr, false))).RunAsync("/r", "log");

        Assert.False(result.Ok);
        Assert.Equal(reason, result.Unavailable);
    }

    [Fact]
    public async Task NotInstalled_IsNoGit()
    {
        var result = await new GitReader(new FakeRunner(new GitProcessResult(-1, [], "", true))).RunAsync("/r", "log");

        Assert.False(result.Ok);
        Assert.Equal("no git", result.Unavailable);
    }

    [Fact]
    public async Task Stdout_IsDecodedAsUtf8()
    {
        var result = await new GitReader(new FakeRunner(Ok("docs/über.md"))).RunAsync("/r", "ls-files");

        Assert.Equal("docs/über.md", result.Text);
    }
}
