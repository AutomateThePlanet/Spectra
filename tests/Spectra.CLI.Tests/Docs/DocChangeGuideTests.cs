using System.Text.RegularExpressions;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Docs;

/// <summary>
/// Spec 100 F2 (FR-005–FR-008, SC-002): docs/usage.md's "When documentation changes" walks the whole doc-change flow
/// in order, with one worked example whose commands parse, real skill and agent names, the refresh line, a
/// committed-vs-scratch table that matches what SPECTRA gitignores, and links from README.md and docs/index.md.
/// </summary>
public sealed class DocChangeGuideTests
{
    private const string Heading = "## When documentation changes";

    private static readonly string[] Steps =
    [
        "Docs changed", "Criteria", "Triage", "Relink", "REMOVED review", "Group edits", "Unchanged and pending",
        "Re-verify", "Acknowledge", "Committed vs scratch",
    ];

    /// <summary>The scratch files a user meets in the update flow (all gitignored by SPECTRA).</summary>
    private static readonly string[] UpdateFlowScratch =
    [
        ".spectra/update-triage.json", ".spectra/updates/", ".spectra/repairs/", ".spectra/verdicts/",
        ".spectra/generation-plan/", ".spectra/baseline.lock", ".spectra/criteria.lock", ".spectra/dropped-tests.json",
    ];

    private static string Section()
    {
        var section = RepoFiles.Section(RepoFiles.Read("docs/usage.md"), Heading);
        Assert.NotNull(section);
        return section!;
    }

    [Fact]
    public void Guide_HasTheTenStepsInOrder()
    {
        var items = RepoFiles.Lines(Section())
            .Select(l => Regex.Match(l, @"^\d+\. \*\*(.+?)\*\*"))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .ToList();
        Assert.Equal(Steps, items);
    }

    [Fact]
    public void Guide_CommandLinesExist()
    {
        var section = Section();
        var lines = RepoFiles.FencedLines(section).Concat(RepoFiles.InlineCode(section))
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("spectra ", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(lines);
        var problems = lines.Select(CommandLineCheck.Structural).OfType<string>().ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void WorkedExample_CommandsParse()
    {
        var example = RepoFiles.Section(Section(), "### Worked example");
        Assert.NotNull(example);
        var lines = RepoFiles.FencedLines(example!).Select(l => l.Trim())
            .Where(l => l.StartsWith("spectra ", StringComparison.Ordinal)).ToList();
        Assert.True(lines.Count >= 8, $"the worked example shows {lines.Count} commands");
        var problems = lines.Select(CommandLineCheck.Parse).OfType<string>().ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Guide_NamesOnlyBundledSkillsAndAgents()
    {
        var known = SkillContent.All.Keys
            .Concat(AgentContent.All.Keys.Select(k => k.Replace(".agent.md", "", StringComparison.Ordinal)))
            .ToHashSet(StringComparer.Ordinal);
        // The .gitignore marker `# spectra-offered:` is not a skill (the colon excludes it).
        var section = Section();
        var names = Regex.Matches(section, @"(?<![\w./-])spectra-[a-z-]+[a-z](?![\w:-])").Select(m => m.Value).Distinct().ToList();
        Assert.Contains("spectra-update", names);
        Assert.Contains("spectra-criteria", names);
        var unknown = names.Where(n => !known.Contains(n)).ToList();
        Assert.True(unknown.Count == 0, "not a bundled skill or agent: " + string.Join(", ", unknown));
    }

    [Fact]
    public void Guide_SaysHowToRefreshEditedFiles()
    {
        var section = Section();
        Assert.Contains("`spectra update-skills`", section, StringComparison.Ordinal);
        Assert.Contains("USAGE.md", section, StringComparison.Ordinal);
        Assert.Contains("CUSTOMIZATION.md", section, StringComparison.Ordinal);
        Assert.Contains("spectra prompts reset", section, StringComparison.Ordinal);
    }

    [Fact]
    public void CommittedVsScratchTable_MatchesTheGitignoreList()
    {
        var rows = RepoFiles.Lines(Section())
            .Select(l => l.Trim())
            .Where(l => l.StartsWith('|') && !l.StartsWith("|--", StringComparison.Ordinal) && !l.StartsWith("| Path", StringComparison.Ordinal))
            .Select(l => l.Split('|', StringSplitOptions.TrimEntries))
            .Where(c => c.Length >= 3)
            .ToList();
        Assert.NotEmpty(rows);

        var scratch = rows.Where(r => r[2].StartsWith("no", StringComparison.Ordinal))
            .SelectMany(r => Regex.Matches(r[1], "`([^`]+)`").Select(m => m.Groups[1].Value))
            .ToList();
        var notIgnored = scratch.Where(p => !SpectraGitIgnore.Patterns.Contains(p)).ToList();
        Assert.True(notIgnored.Count == 0, "scratch rows SPECTRA does not gitignore: " + string.Join(", ", notIgnored));

        var missing = UpdateFlowScratch.Where(p => !scratch.Contains(p)).ToList();
        Assert.True(missing.Count == 0, "scratch files missing from the table: " + string.Join(", ", missing));
        Assert.All(UpdateFlowScratch, p => Assert.Contains(p, SpectraGitIgnore.Patterns));

        var committed = rows.Where(r => r[2].StartsWith("yes", StringComparison.Ordinal))
            .SelectMany(r => Regex.Matches(r[1], "`([^`]+)`").Select(m => m.Groups[1].Value))
            .ToList();
        Assert.True(committed.Intersect(SpectraGitIgnore.Patterns).Count() == 0, "a committed row is gitignored");
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("docs/index.md")]
    public void Guide_IsLinked(string relative) =>
        Assert.Contains("usage.md#when-documentation-changes", RepoFiles.Read(relative), StringComparison.Ordinal);
}
