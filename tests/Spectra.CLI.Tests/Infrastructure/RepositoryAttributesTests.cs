using System.Diagnostics;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Infrastructure;

/// <summary>
/// Spec 096 F7 (FR-011): shell scripts check out with LF line endings whatever <c>core.autocrlf</c> says, so the spec
/// acceptance scripts run in bash. On 23aa08c the repository had no <c>.gitattributes</c> and they checked out with CRLF.
/// </summary>
public sealed class RepositoryAttributesTests
{
    private const string Script = "specs/094-generation-batch-prompts/acceptance/setup-workspace.sh";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("not inside the repository");
    }

    [GitFact]
    public void ShellScripts_AreLfOnCheckout()
    {
        var root = RepoRoot();
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false, WorkingDirectory = root };
        foreach (var arg in new[] { "check-attr", "eol", "--", Script })
        {
            start.ArgumentList.Add(arg);
        }

        using var git = Process.Start(start)!;
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();

        Assert.Equal($"{Script}: eol: lf", output.Trim());
        Assert.DoesNotContain((byte)'\r', File.ReadAllBytes(Path.Combine(root, Script)));
    }
}
