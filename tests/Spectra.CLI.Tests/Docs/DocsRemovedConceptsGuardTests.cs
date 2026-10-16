using System.Text.RegularExpressions;

namespace Spectra.CLI.Tests.Docs;

/// <summary>
/// Spec 100 F1 (FR-002/FR-004/FR-004a, SC-001/SC-002): user-facing docs never present a removed command or key as
/// something to run or set, the README quick start runs, and cli-reference.md documents every command of the CLI.
/// </summary>
public sealed class DocsRemovedConceptsGuardTests
{
    private static readonly string[] RootDocs = ["README.md", "AGENTS.md", "CONTRIBUTING.md", "CLAUDE.md"];

    public static IEnumerable<object[]> UserFacingDocs() =>
        RootDocs.Concat(RepoFiles.MarkdownUnder("docs"))
            .Where(f => !RemovedConcepts.IsHistorical(f))
            .Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(UserFacingDocs))]
    public void UserFacingDoc_NeverPresentsARemovedConcept(string relative)
    {
        var problems = new List<string>();
        foreach (var (line, text, fenced) in RepoFiles.Paragraphs(RepoFiles.Read(relative)))
        {
            foreach (var (concept, match) in RemovedConcepts.Disallowed(text, fenced, ConceptScope.Docs))
            {
                problems.Add($"{relative}:{line}: '{match}' ({concept.Name}, removed in {concept.RemovedIn}; use {concept.Replacement})");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void HistoricalFiles_AreMarked()
    {
        foreach (var file in new[] { "PROJECT-KNOWLEDGE.md", "architecture-v5.md" })
        {
            var head = string.Join('\n', RepoFiles.Lines(RepoFiles.Read(file)).Take(6));
            Assert.Contains("Historical document", head, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ReadmeQuickStart_CommandsExist()
    {
        var section = RepoFiles.Section(RepoFiles.Read("README.md"), "## Quick Start");
        Assert.NotNull(section);
        var lines = RepoFiles.FencedLines(section!).Select(l => l.Trim()).Where(l => l.StartsWith("spectra ", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(lines);
        var problems = lines.Select(CommandLineCheck.Structural).OfType<string>().ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>FR-004a: a command counts as documented when its full path (<c>spectra a b</c>, or <c>a b</c> for a
    /// subcommand) appears in a heading or a code span/block of cli-reference.md — not as a bare word.</summary>
    [Fact]
    public void CliReference_CoversEveryCommand()
    {
        var text = RepoFiles.Read("docs/cli-reference.md");
        // `spectra run pause|resume|cancel` documents each alternative.
        var documented = string.Join('\n',
            RepoFiles.Lines(text).Where(l => l.TrimStart().StartsWith('#'))
                .Concat(RepoFiles.FencedLines(text))
                .Concat(RepoFiles.InlineCode(text))
                .SelectMany(ExpandAlternatives));

        var missing = new List<string>();
        foreach (var path in CommandLineCheck.CommandPaths())
        {
            var forms = new List<string> { "spectra " + string.Join(' ', path) };
            if (path.Count >= 2)
            {
                forms.Add(string.Join(' ', path.TakeLast(2)));
            }

            var found = forms.Any(f => Regex.IsMatch(documented, $@"(?<![\w-]){Regex.Escape(f)}(?![\w-])"));
            if (!found)
            {
                missing.Add("spectra " + string.Join(' ', path));
            }
        }

        Assert.True(missing.Count == 0, "not documented in docs/cli-reference.md:\n" + string.Join("\n", missing));
    }

    private static IEnumerable<string> ExpandAlternatives(string line)
    {
        yield return line;
        foreach (Match m in Regex.Matches(line, @"(?<![\w-])[\w-]+(?:\|[\w-]+)+"))
        {
            foreach (var alternative in m.Value.Split('|'))
            {
                yield return line[..m.Index] + alternative + line[(m.Index + m.Length)..];
            }
        }
    }
}
