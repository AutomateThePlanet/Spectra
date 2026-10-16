using System.Text.RegularExpressions;

namespace Spectra.CLI.Tests.Docs;

/// <summary>
/// Spec 100 F5 (FR-014–FR-017, SC-002/SC-003): CLAUDE.md is current agent guidance — at most 150 lines, every command in
/// its Commands block exists in the CLI's command tree, every folder in its Project Structure exists, and Recent Changes
/// is a table (the spec-kit agent-context script keeps only the first two <c>- </c> lines of that section, so a list
/// would be truncated). AGENTS.md links only to files that exist and names no removed project.
/// </summary>
public sealed class ClaudeMdGuardTests
{
    private static readonly string[] RequiredHeadings =
    [
        "## Tech Stack", "## Project Structure", "## Commands", "## Code Style", "## Execution", "## Active Technologies",
        "## Recent Changes",
    ];

    private static string ClaudeMd() => RepoFiles.Read("CLAUDE.md");

    [Fact]
    public void ClaudeMd_IsAtMost150Lines()
    {
        var lines = RepoFiles.Lines(ClaudeMd().TrimEnd('\r', '\n'));
        Assert.True(lines.Count <= 150, $"CLAUDE.md has {lines.Count} lines (at most 150)");
    }

    [Fact]
    public void ClaudeMd_HasTheRequiredHeadings()
    {
        var headings = RepoFiles.Lines(ClaudeMd()).Select(l => l.TrimEnd()).ToHashSet(StringComparer.Ordinal);
        Assert.All(RequiredHeadings, h => Assert.Contains(h, headings));
    }

    [Fact]
    public void ClaudeMd_CommandsExist()
    {
        var section = RepoFiles.Section(ClaudeMd(), "## Commands");
        Assert.NotNull(section);

        // `spectra a | spectra b` on one line documents two commands.
        var commands = RepoFiles.FencedLines(section!)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("spectra ", StringComparison.Ordinal))
            .SelectMany(l => Regex.Split(l, @"\s\|\s(?=spectra )"))
            .ToList();
        Assert.True(commands.Count >= 30, $"only {commands.Count} commands listed");
        var problems = commands.Select(CommandLineCheck.Structural).OfType<string>().ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void ClaudeMd_ProjectStructureFoldersExist()
    {
        var section = RepoFiles.Section(ClaudeMd(), "## Project Structure");
        Assert.NotNull(section);

        var parents = new List<(int Indent, string Path)>();
        var missing = new List<string>();
        var folders = 0;
        foreach (var raw in RepoFiles.FencedLines(section!))
        {
            var line = raw.Split('#')[0].TrimEnd();
            if (line.Trim().Length == 0)
            {
                continue;
            }

            var indent = line.Length - line.TrimStart().Length;
            parents.RemoveAll(p => p.Indent >= indent);
            var parent = parents.Count == 0 ? "" : parents[^1].Path;

            string? last = null;
            foreach (Match m in Regex.Matches(line, @"(?<=^|\s)([\w.-]+)/(?=\s|$)"))
            {
                var path = parent.Length == 0 ? m.Groups[1].Value : $"{parent}/{m.Groups[1].Value}";
                folders++;
                if (!Directory.Exists(RepoFiles.PathOf(path)))
                {
                    missing.Add(path);
                }

                last = path;
            }

            if (last is not null)
            {
                parents.Add((indent, last));
            }
        }

        Assert.True(folders >= 40, $"only {folders} folders listed");
        Assert.True(missing.Count == 0, "folders that do not exist: " + string.Join(", ", missing));
    }

    [Fact]
    public void ClaudeMd_RecentChangesIsATable()
    {
        var section = RepoFiles.Section(ClaudeMd(), "## Recent Changes");
        Assert.NotNull(section);
        var lines = RepoFiles.Lines(section!);
        Assert.DoesNotContain(lines, l => l.StartsWith("- ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("| 100 ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("| 078 ", StringComparison.Ordinal));
    }

    [Fact]
    public void AgentsMd_LinksExistAndNamesNoRemovedProject()
    {
        var text = RepoFiles.Read("AGENTS.md");
        var links = Regex.Matches(text, @"\]\(([^)#]+)\)").Select(m => m.Groups[1].Value)
            .Where(l => !l.StartsWith("http", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(links);
        var broken = links.Where(l => !RepoFiles.Exists(l.TrimEnd('/'))).ToList();
        Assert.True(broken.Count == 0, "AGENTS.md links to missing files: " + string.Join(", ", broken));
        Assert.DoesNotContain("Spectra.MCP", text, StringComparison.Ordinal);
        Assert.Contains("CLAUDE.md", text, StringComparison.Ordinal);
    }
}
