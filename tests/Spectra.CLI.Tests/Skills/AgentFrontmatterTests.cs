using System.Text.RegularExpressions;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 101 F4 (FR-020–FR-023; US5; contract C6): the bundled agents carry only fields Claude Code reads for a subagent
/// (<c>name</c>, <c>description</c>, <c>tools</c>, <c>model</c>), and each description says which skill it is for and
/// not to delegate to it directly. On 6a4c41a all six carried the skill fields <c>disable-model-invocation</c> and
/// <c>context</c>, which Claude Code ignores on subagents, and said "never auto-invoked".
/// </summary>
public sealed class AgentFrontmatterTests
{
    private static readonly string[] Allowed = ["name", "description", "tools", "model"];

    public static TheoryData<string, string> Skills => new()
    {
        { "spectra-analyst.agent.md", "spectra-generate" },
        { "spectra-generator.agent.md", "spectra-generate" },
        { "spectra-repairer.agent.md", "spectra-generate and spectra-update" },
        { "spectra-updater.agent.md", "spectra-update" },
        { "spectra-extractor.agent.md", "spectra-criteria" },
        { "spectra-critic.agent.md", "spectra-generate, spectra-update and spectra-review-flagged" },
    };

    public static TheoryData<string, string> BundledAndInstalled()
    {
        var data = new TheoryData<string, string>();
        foreach (var (name, content) in AgentContent.All)
        {
            data.Add($"bundled {name}", content);
        }

        foreach (var (name, content) in AgentContent.Rendered("sonnet"))
        {
            data.Add($"installed {name}", content);
        }

        return data;
    }

    private static string Frontmatter(string content) =>
        Regex.Match(content.Replace("\r\n", "\n"), @"\A---\n(.*?)\n---\n", RegexOptions.Singleline).Groups[1].Value;

    private static string Description(string content) =>
        Frontmatter(content).Split('\n').Single(l => l.StartsWith("description:", StringComparison.Ordinal))["description:".Length..].Trim();

    [Theory]
    [MemberData(nameof(BundledAndInstalled))]
    public void TheAgent_CarriesOnlyFieldsClaudeCodeReads(string label, string content)
    {
        var keys = Frontmatter(content).Split('\n')
            .Where(l => l.Length > 0 && !char.IsWhiteSpace(l[0]) && l.Contains(':'))
            .Select(l => l[..l.IndexOf(':')].Trim())
            .ToList();

        Assert.NotEmpty(keys);
        Assert.All(keys, k => Assert.True(Allowed.Contains(k), $"{label}: field '{k}' is not one Claude Code reads on a subagent"));
    }

    [Theory]
    [MemberData(nameof(Skills))]
    public void TheDescription_SaysWhichSkillItIsFor_AndNotToDelegateDirectly(string file, string skills)
    {
        var description = Description(AgentContent.All[file]);

        Assert.StartsWith($"Only for use by the {skills} skill", description);
        Assert.Contains("Do not delegate to it directly", description);
    }

    [Fact]
    public void NoAgentOrSkill_ClaimsContextForkOrNeverAutoInvoked()
    {
        var texts = AgentContent.All.Select(a => (a.Key, a.Value))
            .Concat(SkillContent.All.Select(s => (s.Key, s.Value)));

        Assert.All(texts, t =>
        {
            Assert.False(t.Value.Contains("context: fork", StringComparison.Ordinal), $"{t.Key} says `context: fork`");
            Assert.False(t.Value.Contains("never auto-invoked", StringComparison.OrdinalIgnoreCase), $"{t.Key} says \"never auto-invoked\"");
        });
    }
}
