using System.Text.RegularExpressions;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Git;
using Spectra.MCP.Identity;

namespace Spectra.CLI.Tests.Commands.Git;

/// <summary>
/// Spec 087 T2 (F2, FR-008): every place SPECTRA starts git uses the resolved absolute path and passes arguments one by
/// one — never the bare name <c>git</c> (which Windows also looks up in the current folder) and never a command string.
/// </summary>
public sealed class GitStartSourceGuardTests
{
    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Spectra.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, relative));
    }

    [Theory]
    [InlineData("src/Spectra.Core/Git/GitProcessRunner.cs")]
    [InlineData("src/Spectra.Execution/Identity/UserIdentityResolver.cs")]
    public void GitIsNeverStartedByItsBareName(string file)
    {
        var source = RepoFile(file);

        Assert.DoesNotMatch(new Regex(@"ProcessStartInfo\(\s*""git"""), source);
        Assert.DoesNotMatch(new Regex(@"FileName\s*=\s*""git"""), source);
        Assert.DoesNotMatch(new Regex(@"\bArguments\s*="), source);
        Assert.Contains("GitExecutable", source);
    }

    [GitFact]
    public void TheRunner_StartsTheResolvedAbsolutePath_WithAnArgumentList()
    {
        var dir = Path.GetTempPath();

        var info = new ProcessGitRunner().CreateStartInfo(dir, ["-C", dir, "rev-parse"], GitReader.Environment);

        Assert.NotNull(info);
        Assert.Equal(GitExecutable.Path, info!.FileName);
        Assert.True(Path.IsPathFullyQualified(info.FileName));
        Assert.False(info.UseShellExecute);
        Assert.Equal(["-C", dir, "rev-parse"], info.ArgumentList);
        Assert.Equal(string.Empty, info.Arguments);
    }

    [GitFact]
    public void TheIdentityLookup_StartsTheResolvedAbsolutePath_WithAnArgumentList()
    {
        var info = UserIdentityResolver.CreateGitStartInfo();

        Assert.NotNull(info);
        Assert.Equal(GitExecutable.Path, info!.FileName);
        Assert.False(info.UseShellExecute);
        Assert.Equal(["config", "user.name"], info.ArgumentList);
        Assert.Equal(string.Empty, info.Arguments);
    }
}
