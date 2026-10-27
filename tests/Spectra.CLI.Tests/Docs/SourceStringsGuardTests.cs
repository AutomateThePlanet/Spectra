namespace Spectra.CLI.Tests.Docs;

/// <summary>
/// Spec 100 F4 (FR-011/FR-012, SC-005): no user-visible text the CLI prints, writes or installs names a removed
/// command, option, key or document — help and messages in <c>src/**/*.cs</c> (comments excluded), the bundled skills,
/// agents, prompt templates and config template, and both copies of the dashboard script.
/// </summary>
/// <remarks>The allow-list is explicit: code that must name a retired key to detect it, an internal id, and negative
/// statements that tell an agent a command no longer exists.</remarks>
public sealed class SourceStringsGuardTests
{
    private static readonly (string File, string Text)[] Allowed =
    [
        // Detecting or still reading a legacy config needs the retired key names (Specs 069/096/098).
        ("src/Spectra.Core/Config/ConfigLoader.cs", "ai.critic."),
        ("src/Spectra.Core/Models/Config/CriticConfig.cs", "\"api_key_env\", \"base_url\""),
        ("src/Spectra.Core/Models/Config/AiConfig.cs", "[JsonPropertyName(\"generation_batch_size\")]"),
        // Dead code with no production caller (only tests build a TokenUsageReport); listed in spec 100's Notes.
        ("src/Spectra.CLI/Services/CostEstimator.cs", "\"github-models\""),
        ("src/Spectra.CLI/Services/CostEstimator.cs", "\"Included in Copilot plan (rate limits apply)\""),
        // An internal progress-phase id, never shown as a command.
        ("src/Spectra.CLI/Progress/ProgressPageWriter.cs", "\"extract-criteria\" =>"),
        // Negative statements to the agent: "there is no such command".
        ("src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md", "There is **no** `spectra ai generate` command anymore."),
        ("src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md", "No `spectra ai generate`"),
        ("src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md", "There is **no** `spectra ai analyze --extract-criteria` model call anymore."),
        ("src/Spectra.CLI/Skills/Content/Skills/spectra-execute.md", "no MCP server"),
        ("src/Spectra.CLI/Skills/Content/Skills/spectra-execute.md", "No MCP server required."),
        ("src/Spectra.CLI/Skills/Content/Skills/spectra-dashboard.md", "**NEVER use MCP tools for dashboard generation"),
        // Spec 104: GitHub Copilot as an install target of `spectra init --agent` (not the removed SDK integration).
        ("src/Spectra.CLI/Install/AgentTargets.cs", "public override string Name => \"copilot\";"),
        ("src/Spectra.CLI/Install/AgentTargets.cs", "public override string DisplayName => \"GitHub Copilot\";"),
        ("src/Spectra.CLI/Install/AgentTargets.cs", "\".github/copilot-instructions.md\""),
    ];

    private static IEnumerable<string> Files()
    {
        var files = new List<string>();
        foreach (var project in new[] { "src/Spectra.CLI", "src/Spectra.Core", "src/Spectra.Execution" })
        {
            files.AddRange(Directory.EnumerateFiles(RepoFiles.PathOf(project), "*.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(RepoFiles.Root, f).Replace(Path.DirectorySeparatorChar, '/'))
                .Where(f => !f.Contains("/bin/", StringComparison.Ordinal) && !f.Contains("/obj/", StringComparison.Ordinal)));
        }

        files.AddRange(RepoFiles.MarkdownUnder("src/Spectra.CLI/Skills/Content"));
        files.AddRange(RepoFiles.MarkdownUnder("src/Spectra.CLI/Prompts/Content"));
        files.Add("src/Spectra.CLI/Templates/spectra.config.json");
        files.Add("src/Spectra.CLI/Dashboard/Templates/scripts/app.js");
        files.Add("dashboard-site/scripts/app.js");
        return files.OrderBy(f => f, StringComparer.Ordinal);
    }

    [Fact]
    public void UserVisibleText_NamesNoRemovedConcept()
    {
        var problems = Files().SelectMany(Problems).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<string> Problems(string relative)
    {
        var isCode = relative.EndsWith(".cs", StringComparison.Ordinal);
        var scope = isCode ? ConceptScope.SourceStrings : ConceptScope.BundledContent;
        var problems = new List<string>();
        var lines = RepoFiles.Lines(RepoFiles.Read(relative));
        for (var i = 0; i < lines.Count; i++)
        {
            var line = isCode ? WithoutComment(lines[i]) : lines[i];
            if (line.Length == 0 || Allowed.Any(a => a.File == relative && line.Contains(a.Text, StringComparison.Ordinal)))
            {
                continue;
            }

            foreach (var concept in RemovedConcepts.All.Where(c => (c.Scope & scope) != 0))
            {
                var match = concept.Pattern.Match(line);
                if (match.Success && !(concept.IsMcp && RemovedConcepts.NamesLegitimateMcp(line)))
                {
                    problems.Add($"{relative}:{i + 1}: '{match.Value}' ({concept.Name}, removed in {concept.RemovedIn}; use {concept.Replacement})");
                }
            }
        }

        return problems;
    }

    /// <summary>The line without a <c>//</c> comment (whole-line or trailing, outside a string literal).</summary>
    private static string WithoutComment(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal) ||
            trimmed.StartsWith("/*", StringComparison.Ordinal))
        {
            return "";
        }

        var inString = false;
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inString = !inString;
            }
            else if (!inString && line[i] == '/' && line[i + 1] == '/' && (i == 0 || line[i - 1] != ':'))
            {
                return line[..i];
            }
        }

        return line;
    }
}
