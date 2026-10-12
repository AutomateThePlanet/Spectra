using System.Text;
using Spectra.CLI.Prompts;
using Spectra.Core.Models;

namespace Spectra.CLI.Generation.Plan;

/// <summary>
/// Spec 094 (FR-009, FR-013; R7, R8): renders the generation prompt for one batch through the <c>test-generation</c>
/// template — the batch's behaviours into <c>behaviors</c>, its sections (each fenced, under <c>### &lt;ref&gt;</c>) into
/// <c>source_sections</c>, <c>read_docs</c> empty, the selected criteria, the count and the profile format. Pure: the same
/// inputs give the same bytes.
/// </summary>
public static class BatchPromptCompiler
{
    /// <summary>
    /// Spec 094 (R7, FR-010a): a template loaded from the project (<c>.spectra/prompts/test-generation.md</c>) without the
    /// batch placeholder predates batch prompts.
    /// </summary>
    public static bool PredatesBatch(PromptTemplate template) =>
        template.IsUserCustomized && !template.Body.Contains("{{source_sections}}", StringComparison.Ordinal);

    /// <summary>One line per behaviour: <c>- B3 [boundary · BVA] title (ref)</c>.</summary>
    public static string Behaviors(GenerationPlan plan, PlannedBatch batch, string? focus)
    {
        var lines = batch.BehaviorIds.Select(id =>
        {
            var b = plan.Behavior(id);
            var tags = string.Join(" · ", new[] { b.Category, b.Technique }.Where(t => !string.IsNullOrWhiteSpace(t)));
            return $"- {b.Id} [{tags}] {b.Title} ({b.Ref})";
        });
        var text = string.Join("\n", lines);
        return string.IsNullOrWhiteSpace(focus) ? text : $"{text}\n\nFocus: {focus.Trim()}";
    }

    /// <summary>Each section under <c>### &lt;ref&gt;</c> in a <c>~~~~markdown</c> fence, in order, LF only.</summary>
    public static string Sections(IReadOnlyList<(string Ref, string Raw)> sections)
    {
        var sb = new StringBuilder();
        foreach (var (sectionRef, raw) in sections)
        {
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            UpdatePromptRenderer.AppendFenced(sb, $"### {sectionRef}", "markdown", raw);
        }

        return sb.ToString().TrimEnd('\n');
    }

    public static string Compile(
        PromptTemplate template,
        GenerationPlan plan,
        PlannedBatch batch,
        IReadOnlyList<(string Ref, string Raw)> sections,
        string criteriaContext,
        int count,
        string? focus,
        string profileFormat)
    {
        var values = new Dictionary<string, string>
        {
            ["testimize_dataset"] = "",
            ["testimize_strategy_name"] = "",
            ["behaviors"] = Behaviors(plan, batch, focus),
            ["suite_name"] = "",
            ["existing_tests"] = "",
            ["acceptance_criteria"] = criteriaContext,
            ["profile_format"] = profileFormat,
            ["count"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["focus_areas"] = focus ?? "",
            ["source_sections"] = Sections(sections),
            ["read_docs"] = ""
        };

        return PromptTemplateLoader.Resolve(template, values);
    }
}
