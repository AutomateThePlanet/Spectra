using Spectra.Core.Git;

namespace Spectra.Core.Tests.Git;

/// <summary>
/// Spec 087 T2 (F2, FR-006/FR-007): the git executable comes only from absolute PATH entries — never from the current
/// folder, an empty, <c>.</c> or relative entry — and is <c>git.exe</c> on Windows.
/// </summary>
public class GitExecutableTests
{
    private static readonly string Root = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "spectra-087-fake-path");
    private static readonly string Workspace = Path.Combine(Root, "workspace");
    private static readonly string First = Path.Combine(Root, "first");
    private static readonly string Second = Path.Combine(Root, "second");

    private static string Exe(string dir, bool windows = true) => Path.Combine(dir, windows ? "git.exe" : "git");

    private static string? Resolve(IEnumerable<string?> entries, IEnumerable<string> existing, bool windows = true) =>
        GitExecutable.Resolve(entries, Workspace, windows, new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase).Contains);

    [Fact]
    public void AGitInTheCurrentFolder_IsNeverChosen()
    {
        // Relative and "." entries point at the current folder; an absolute entry naming it is skipped too.
        Assert.Equal(Exe(Second), Resolve([".", "", Workspace, Second], [Exe(Workspace), Exe(Second)]));
        Assert.Null(Resolve([".", Workspace], [Exe(Workspace)]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("bin")]
    [InlineData("tools/git")]
    [InlineData("..")]
    public void EmptyDotAndRelativeEntries_AreSkipped(string entry) =>
        Assert.Equal(Exe(First), Resolve([entry, null, First], [Exe(First), Path.GetFullPath(Path.Combine(Workspace, entry, "git.exe"))]));

    [Fact]
    public void ADriveRelativeEntry_IsSkipped_OnWindows()
    {
        if (!OperatingSystem.IsWindows()) return; // "C:tools" is only drive-relative on Windows
        Assert.Equal(Exe(First), Resolve(["C:tools", "\\tools", First], [Exe(First), "C:tools\\git.exe", "\\tools\\git.exe"]));
    }

    [Fact]
    public void AQuotedEntry_IsAccepted() =>
        Assert.Equal(Exe(First), Resolve(["\"" + First + "\""], [Exe(First)]));

    [Fact]
    public void TheFirstAbsoluteMatch_Wins() =>
        Assert.Equal(Exe(First), Resolve([First, Second], [Exe(First), Exe(Second)]));

    [Fact]
    public void Windows_LooksForGitExeOnly()
    {
        Assert.Null(Resolve([First], [Path.Combine(First, "git.cmd"), Path.Combine(First, "git.bat"), Path.Combine(First, "git")]));
        Assert.Equal(Exe(Second), Resolve([First, Second], [Path.Combine(First, "git.cmd"), Exe(Second)]));
    }

    [Fact]
    public void ElsewhereGit_IsLookedUpWithoutAnExtension()
    {
        Assert.Equal(Exe(First, windows: false), Resolve([First], [Exe(First, windows: false)], windows: false));
        Assert.Null(Resolve([First], [Exe(First)], windows: false));
    }

    [Fact]
    public void NoMatch_IsNull() => Assert.Null(Resolve([First, Second], []));

    [Fact]
    public void TheCachedPath_IsFullyQualified_WhenGitIsInstalled()
    {
        // On a machine with git on PATH (CI and dev); a machine without it gets null, which is "no git".
        if (GitExecutable.Path is { } path)
        {
            Assert.True(Path.IsPathFullyQualified(path), path);
            Assert.True(File.Exists(path), path);
        }
    }
}
