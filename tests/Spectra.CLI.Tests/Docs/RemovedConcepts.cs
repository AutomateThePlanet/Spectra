using System.Text.RegularExpressions;

namespace Spectra.CLI.Tests.Docs;

[Flags]
internal enum ConceptScope
{
    Docs = 1,
    SourceStrings = 2,
    BundledContent = 4,
    All = Docs | SourceStrings | BundledContent,
}

/// <summary>A command, option, key or name that no longer exists (research R1).</summary>
internal sealed record RemovedConcept(string Name, Regex Pattern, string RemovedIn, string Replacement, ConceptScope Scope, bool IsMcp = false);

/// <summary>
/// Spec 100 (research R1/R2): the one list of removed concepts every guard uses, and the allow rule for docs: a mention
/// is allowed in a historical file, in a paragraph that says the thing was removed (a removal marker), or — for MCP —
/// in a paragraph naming the separate BELLATRIX/Nova SUT driver or the Azure DevOps bug-log MCP.
/// </summary>
internal static class RemovedConcepts
{
    private const RegexOptions Ci = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public static readonly IReadOnlyList<RemovedConcept> All =
    [
        new("spectra ai generate", new Regex(@"\bspectra ai generate(?![\w-])", Ci), "059", "the spectra-generate skill", ConceptScope.All),
        new("ai generate flags", new Regex(@"--(auto-complete|from-suggestions|analyze-only)\b", Ci), "059", "the spectra-generate skill", ConceptScope.All),
        new("spectra auth", new Regex(@"\bspectra auth\b", Ci), "069", "nothing to authenticate", ConceptScope.All),
        new("ai.providers", new Regex(@"\bai\.providers\b|""providers""\s*:", Ci), "069", "no provider config", ConceptScope.All),
        new("in-process critic keys", new Regex(@"\bai\.critic\.(provider|api_key_env|base_url|enabled|timeout_seconds|max_concurrent)\b|\bapi_key_env\b|\bbase_url\b", Ci), "069/098", "ai.critic.model", ConceptScope.All),
        new("--extract-criteria", new Regex(@"--extract-criteria\b", Ci), "069", "the spectra-criteria skill", ConceptScope.All),
        new("--skip-splitting", new Regex(@"--skip-splitting\b", Ci), "069", "import no longer splits", ConceptScope.All),
        new("SPECTRA's MCP server", new Regex(@"\bspectra-mcp\b|mcp__spectra__|\.vscode/mcp\.json|\bMCP server\b|\bvia MCP\b|\bMCP execution\b|\bMCP client\b|\bSpectra\.MCP\b(?!\.)", Ci), "070", "spectra run", ConceptScope.All, IsMcp: true),
        new("GitHub Copilot", new Regex(@"\bCopilot\b", Ci), "059/069/070", "Claude Code", ConceptScope.All),
        new("provider names", new Regex(@"\bgithub-models\b|\bazure-openai\b|\bazure-anthropic\b", Ci), "069", "no provider config", ConceptScope.All),
        new("ai.generation_batch_size", new Regex(@"\bgeneration_batch_size\b", Ci), "096", "generation.batch_size", ConceptScope.All),
        new("ai.defaultProvider", new Regex(@"\bdefaultProvider\b", Ci), "never a key", "source.local_dir / tests.dir", ConceptScope.All),
        new("ai.analysis.max_prompt_tokens", new Regex(@"\bai\.analysis\.max_prompt_tokens\b", Ci), "wrong path", "analysis.max_prompt_tokens", ConceptScope.All),
    ];

    /// <summary>Files (and folders, ending in <c>/</c>) that are history and may mention removed concepts (FR-003).</summary>
    public static readonly IReadOnlyList<string> HistoricalFiles =
    [
        "docs/claude-code-v2-migration.md",
        "docs/ai-models-cost-guide.md",
        "docs/migration-040.md",
        "docs/architecture/ARCHITECTURE-v2.md",
        "docs/specs/",
        "docs/investigation/",
        "docs/analysis/",
        "architecture-v5.md",
        "PROJECT-KNOWLEDGE.md",
        "CHANGELOG.md",
        "specs/",
    ];

    /// <summary>A paragraph containing one of these says the concept was removed (FR-004).</summary>
    public static readonly IReadOnlyList<string> RemovalMarkers =
    [
        "removed",
        "no longer",
        "there is no",
        "is gone",
        "are gone",
        "retired",
        "legacy",
        "pre-v2",
        "deprecated",
    ];

    /// <summary>MCPs that are not SPECTRA's own (FR-004).</summary>
    public static readonly IReadOnlyList<string> LegitimateMcps = ["BELLATRIX", "Nova", "Azure DevOps"];

    public static bool IsHistorical(string relative) =>
        HistoricalFiles.Any(h => h.EndsWith('/') ? relative.StartsWith(h, StringComparison.Ordinal) : relative == h);

    public static bool HasRemovalMarker(string paragraph) =>
        RemovalMarkers.Any(m => paragraph.Contains(m, StringComparison.OrdinalIgnoreCase));

    public static bool NamesLegitimateMcp(string paragraph) =>
        LegitimateMcps.Any(m => Regex.IsMatch(paragraph, $@"\b{Regex.Escape(m)}\b"));

    /// <summary>The removed concepts in <paramref name="paragraph"/> that the allow rule does not cover.</summary>
    public static IEnumerable<(RemovedConcept Concept, string Match)> Disallowed(string paragraph, bool fenced, ConceptScope scope)
    {
        foreach (var concept in All.Where(c => (c.Scope & scope) != 0))
        {
            var match = concept.Pattern.Match(paragraph);
            if (!match.Success)
            {
                continue;
            }

            // A fenced block is something to run or copy: a removal marker next to it does not cover it.
            if (!fenced && HasRemovalMarker(paragraph))
            {
                continue;
            }

            if (concept.IsMcp && NamesLegitimateMcp(paragraph))
            {
                continue;
            }

            yield return (concept, match.Value);
        }
    }
}
