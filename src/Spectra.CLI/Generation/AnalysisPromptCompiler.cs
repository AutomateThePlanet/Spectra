using System.Text;
using Spectra.CLI.Prompts;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Generation;

/// <summary>
/// Spec 059: deterministic, model-free compiler for the behavior-analysis prompt. The
/// prompt-building half of <c>BehaviorAnalyzer</c> (<c>BuildAnalysisPrompt</c> + <c>FormatDocuments</c>)
/// is relocated (copied) here so the analyze-first step lives on the CLI seam: identical inputs
/// → identical prompt, zero token spend. Refuses to emit (missing "documents") when no documents
/// are supplied.
/// </summary>
public static class AnalysisPromptCompiler
{
    /// <summary>
    /// Compiles the behavior-analysis prompt from the resolved documents and optional grounding.
    /// </summary>
    public static AnalysisPromptCompileResult Compile(
        IReadOnlyList<SourceDocument> documents,
        string? focusArea,
        SpectraConfig? config,
        PromptTemplateLoader? templateLoader,
        string? coverageContext)
    {
        if (documents is null || documents.Count == 0)
            return AnalysisPromptCompileResult.MissingRequired(
                "documents", "No source documents resolved for analysis.");

        var prompt = BuildAnalysisPrompt(documents, focusArea, config, templateLoader, coverageContext);
        return AnalysisPromptCompileResult.Success(prompt);
    }

    // ---- Relocated (copied) from BehaviorAnalyzer; deterministic, model-free ----

    internal static string BuildAnalysisPrompt(
        IReadOnlyList<SourceDocument> documents,
        string? focusArea,
        SpectraConfig? config = null,
        PromptTemplateLoader? templateLoader = null,
        string? coverageContext = null)
    {
        // Try template-driven prompt
        if (templateLoader is not null)
        {
            var template = templateLoader.LoadTemplate("behavior-analysis");
            var categories = PromptTemplateLoader.GetCategories(config);

            var docText = FormatDocuments(documents);
            var values = new Dictionary<string, string>
            {
                // Spec 038: gates the {{#if testimize_enabled}} block in
                // behavior-analysis.md. Empty string is falsy.
                ["testimize_enabled"] = (config?.Testimize.Enabled ?? false) ? "true" : "",
                ["document_text"] = docText,
                ["document_title"] = string.Join(", ", documents.Select(d => d.Title ?? d.Path)),
                ["suite_name"] = "",
                ["existing_tests"] = "",
                ["focus_areas"] = focusArea ?? "",
                ["acceptance_criteria"] = "",
                // Spec 044: coverage-aware analysis context
                ["coverage_context"] = coverageContext ?? ""
            };

            var listValues = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["categories"] = PromptTemplateLoader.FormatCategoriesForTemplate(categories)
            };

            return PromptTemplateLoader.Resolve(template, values, listValues);
        }

        // Legacy fallback (used when no PromptTemplateLoader is wired up).
        // Mirrors the ISTQB-enhanced template in condensed form.
        var sb = new StringBuilder();
        sb.AppendLine("""
            Analyze the following documentation and identify all distinct testable behaviors.

            Apply these test design techniques systematically:
            - Equivalence Partitioning (EP): for every input, identify valid and invalid classes
            - Boundary Value Analysis (BVA): for every range, test at min, max, min-1, max+1
            - Decision Table (DT): for business rules with multiple conditions
            - State Transition (ST): for workflows, test valid and invalid transitions
            - Error Guessing (EG): common defects (null, overflow, special chars, divide-by-zero)
            - Use Case (UC): main success + alternate + exception paths

            Do NOT generate more than 40% of behaviors in any single category.

            Categorize each behavior into one of these categories:
            - happy_path: Normal successful user flows
            - negative: Error handling, invalid inputs, failure scenarios
            - edge_case: Boundary conditions, unusual combinations, limits
            - boundary: Values at exact limits (min, max, min-1, max+1)
            - error_handling: System error responses, recovery, logging
            - security: Permission checks, access control, authentication

            For each behavior, provide:
            - category: one of the categories above
            - title: short description (max 80 chars)
            - source: which document it comes from
            - technique: EP, BVA, DT, ST, EG, or UC

            Return ONLY a JSON object in this exact format (no other text):

            {"behaviors": [{"category": "boundary", "title": "...", "source": "...", "technique": "BVA"}]}

            Count only DISTINCT testable behaviors — do not duplicate similar scenarios.
            """);

        if (!string.IsNullOrWhiteSpace(focusArea))
        {
            sb.AppendLine($"\nFocus area: {focusArea}");
        }

        sb.AppendLine("\nDocumentation:");
        sb.Append(FormatDocuments(documents));

        return sb.ToString();
    }

    private static string FormatDocuments(IReadOnlyList<SourceDocument> documents)
    {
        var sb = new StringBuilder();
        foreach (var doc in documents)
        {
            sb.AppendLine($"\n### {doc.Title} ({doc.Path})");
            if (doc.Sections.Count > 0)
            {
                sb.AppendLine($"Sections: {string.Join(", ", doc.Sections)}");
            }

            var preview = doc.Content.Length > 2000
                ? doc.Content[..2000] + "..."
                : doc.Content;
            sb.AppendLine(preview);
        }
        return sb.ToString();
    }
}
