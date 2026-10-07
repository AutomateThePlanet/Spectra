using System.Text;
using Spectra.Core.Git;

namespace Spectra.Core.Tests.Git;

/// <summary>Spec 087 F1 (FR-001): reading a repository's submodules (gitlinks, mode 160000) from <c>ls-files -s -z</c>.</summary>
public class GitlinkParseTests
{
    private static byte[] LsFiles(params (string Mode, int Stage, string Path)[] entries) =>
        Encoding.UTF8.GetBytes(string.Concat(entries.Select(e => $"{e.Mode} {new string('a', 40)} {e.Stage}\t{e.Path}\0")));

    [Fact]
    public void OnlyGitlinks_AreCollected()
    {
        var gitlinks = OwnerResolver.ParseGitlinks(LsFiles(
            ("100644", 0, ".gitmodules"), ("160000", 0, "docs/sub"), ("100755", 0, "run.sh"),
            ("120000", 0, "link"), ("160000", 0, "docs/über/inner")), StringComparer.Ordinal);

        Assert.Equal(new[] { "docs/sub", "docs/über/inner" }, gitlinks.OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void AnyStage_IsRead() =>
        Assert.Contains("docs/sub", OwnerResolver.ParseGitlinks(LsFiles(("160000", 2, "docs/sub")), StringComparer.Ordinal));

    [Fact]
    public void EmptyOutput_IsNoGitlinks() => Assert.Empty(OwnerResolver.ParseGitlinks([], StringComparer.Ordinal));

    [Fact]
    public void TheComparer_IsHonoured()
    {
        var bytes = LsFiles(("160000", 0, "docs/sub"));

        Assert.Contains("Docs/Sub", OwnerResolver.ParseGitlinks(bytes, StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain("Docs/Sub", OwnerResolver.ParseGitlinks(bytes, StringComparer.Ordinal));
    }
}
