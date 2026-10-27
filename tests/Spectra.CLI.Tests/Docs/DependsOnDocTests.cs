using System.Text.RegularExpressions;

namespace Spectra.CLI.Tests.Docs;

/// <summary>
/// Spec 100 F3 (FR-009): <c>depends_on</c> is documented as it is — one test id. A list form does not parse
/// (<c>TestCase.DependsOn</c> is a string; Spec 090 decision), so no doc may type it as a list or show a list as valid.
/// </summary>
public sealed class DependsOnDocTests
{
    [Fact]
    public void TestFormat_DocumentsASingleId()
    {
        var row = RepoFiles.Lines(RepoFiles.Read("docs/test-format.md"))
            .Single(l => l.StartsWith("| `depends_on` |", StringComparison.Ordinal));
        Assert.Contains("| string |", row, StringComparison.Ordinal);
        Assert.DoesNotContain("string[]", row, StringComparison.Ordinal);
        Assert.Contains("not supported", row, StringComparison.Ordinal);
        Assert.Contains("spectra validate", row, StringComparison.Ordinal);
        Assert.Contains("spectra delete", row, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("docs/test-format.md")]
    [InlineData("docs/cli-reference.md")]
    [InlineData("docs/usage.md")]
    public void Docs_ShowNoListAsValid(string relative)
    {
        var lines = RepoFiles.Lines(RepoFiles.Read(relative));
        for (var i = 0; i < lines.Count; i++)
        {
            Assert.False(Regex.IsMatch(lines[i], @"^\s*depends_on:\s*\["), $"{relative}:{i + 1} shows a flow-list depends_on");
            var blockList = Regex.IsMatch(lines[i], @"^\s*depends_on:\s*$") && i + 1 < lines.Count &&
                            Regex.IsMatch(lines[i + 1], @"^\s*-\s");
            Assert.False(blockList, $"{relative}:{i + 1} shows a block-list depends_on");
        }
    }
}
