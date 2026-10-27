using System.Text;
using Spectra.Core.Git;

namespace Spectra.Core.Tests.Git;

/// <summary>Spec 086 T3 (FR-005/FR-006, research R3): parsing the rename log and the candidates it gives. Pure; no git.</summary>
public class RenameHistoryTests
{
    // The probed git 2.53 format: \x1e<hash>\0<ct>\0\nR100\0old\0new\0…
    private static byte[] Log(params (string Hash, long Time, (int Score, string Old, string New)[] Renames)[] commits) =>
        Encoding.UTF8.GetBytes(string.Concat(commits.Select(c =>
            $"\x1e{c.Hash}\0{c.Time}\0\n" + string.Concat(c.Renames.Select(r => $"R{r.Score:000}\0{r.Old}\0{r.New}\0")))));

    private static RenameHistory History(bool shallow = false, params (string Old, string New)[] renamesNewestFirst) =>
        new(renamesNewestFirst.Select((r, i) => new GitRename($"c{i}", 100 - i, r.Old, r.New, 100)).ToList(), 90, shallow);

    [Fact]
    public void Parse_ReadsCommitsTimesScoresAndNonAsciiPaths()
    {
        var renames = RenameHistory.Parse(Log(
            ("aaaa", 1791283849, [(100, "docs/b.md", "docs/c.md"), (95, "docs/über.md", "docs/neu/über.md")]),
            ("bbbb", 1791283000, [(100, "x.md", "y.md")])));

        Assert.Equal(
            [
                new GitRename("aaaa", 1791283849, "docs/b.md", "docs/c.md", 100),
                new GitRename("aaaa", 1791283849, "docs/über.md", "docs/neu/über.md", 95),
                new GitRename("bbbb", 1791283000, "x.md", "y.md", 100)
            ],
            renames);
    }

    [Fact]
    public void Parse_WithoutTimes_ReadsRenames()
    {
        var renames = RenameHistory.Parse(Encoding.UTF8.GetBytes("\x1e" + "cccc\0\nR100\0a.md\0b.md\0"));

        Assert.Equal([new GitRename("cccc", 0, "a.md", "b.md", 100)], renames);
    }

    [Fact]
    public void Parse_EmptyOutput_IsNoRenames() => Assert.Empty(RenameHistory.Parse([]));

    public static IEnumerable<object?[]> Candidates() =>
    [
        // name, renames (newest first), existing files, path, expected target, expected reason
        ["plain", new[] { ("a.md", "b.md") }, new[] { "b.md" }, "a.md", "b.md", null],
        ["chain a→b→c", new[] { ("b.md", "c.md"), ("a.md", "b.md") }, new[] { "c.md" }, "a.md", "c.md", null],
        ["two places", new[] { ("a.md", "c.md"), ("a.md", "b.md") }, new[] { "b.md", "c.md" }, "a.md", null, "ambiguous rename"],
        // Analysis A1: a → b, then a re-created and a → c — two distinct targets anywhere in the history.
        ["re-created then renamed again", new[] { ("a.md", "c.md"), ("a.md", "b.md") }, new[] { "c.md" }, "a.md", null, "ambiguous rename"],
        ["chain ends missing", new[] { ("a.md", "b.md") }, Array.Empty<string>(), "a.md", null, "rename chain ends at a missing file"],
        ["no entry", new[] { ("x.md", "y.md") }, new[] { "y.md" }, "a.md", null, "no rename found at 90% similarity"],
        ["cycle", new[] { ("b.md", "a.md"), ("a.md", "b.md") }, Array.Empty<string>(), "a.md", null, "rename chain ends at a missing file"],
        ["ambiguous mid-chain", new[] { ("b.md", "d.md"), ("b.md", "c.md"), ("a.md", "b.md") }, new[] { "c.md", "d.md" }, "a.md", null, "ambiguous rename"]
    ];

    [Theory]
    [MemberData(nameof(Candidates))]
    public void ForwardCandidate(string name, (string, string)[] renames, string[] existing, string path, string? target, string? reason)
    {
        _ = name;
        var history = History(false, renames);

        var (actualTarget, actualReason) = history.ForwardCandidate(path, existing.Contains);

        Assert.Equal(target, actualTarget);
        Assert.Equal(reason, actualReason);
    }

    [Fact]
    public void ForwardCandidate_Shallow_SaysSo()
    {
        var (target, reason) = History(true).ForwardCandidate("a.md", _ => true);

        Assert.Null(target);
        Assert.Equal("no rename found at 90% similarity (shallow history)", reason);
    }

    [Fact]
    public void PathAt_UndoesRenamesNewestToOldest()
    {
        // a → b (older), then b → c (newer): the file now at c was a before both.
        var since = new[] { new GitRename("2", 2, "b.md", "c.md", 100), new GitRename("1", 1, "a.md", "b.md", 100) };

        Assert.Equal(("a.md", (string?)null), RenameHistory.PathAt(since, "c.md"));
        Assert.Equal(("b.md", (string?)null), RenameHistory.PathAt(since.Take(1), "c.md"));
        Assert.Equal(("z.md", (string?)null), RenameHistory.PathAt(since, "z.md"));
    }
}
