using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.CLI.Prompts;
using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Extraction;

/// <summary>
/// Deterministic, model-free compiler for the acceptance-criteria extraction prompt (Spec 054).
/// Relocated out of <c>CriteriaExtractor.BuildExtractionPrompt</c> so the extraction-prompt
/// assembly is a standalone artifact, decoupled from the Copilot SDK by location — the same
/// "decouple by location" move Spec 053 applied to generation
/// (<see cref="Spectra.CLI.Generation.PromptCompiler"/>).
///
/// Two entry points mirror <c>PromptCompiler</c>:
/// <list type="bullet">
/// <item><see cref="Assemble"/> — the relocated, <b>lenient</b> assembly. Same behavior and
/// signature contract as the old <c>BuildExtractionPrompt</c>.
/// <c>CriteriaExtractor.BuildExtractionPrompt</c> delegates here so there is a single source of
/// extraction-prompt truth.</item>
/// <item><see cref="Compile"/> — the <b>validated</b> entry (FR-002). Refuses to emit when a
/// required input (document path or content) is missing, and names the failing input.</item>
/// </list>
///
/// Determinism (FR-002): the emitted text contains no timestamps, GUIDs, or unordered
/// enumeration — identical inputs produce byte-identical output.
///
/// Note: an empty/whitespace <c>documentContent</c> is <b>not</b> a refusal here — it is the
/// FR-003 empty-source short-circuit (<c>Extracted, []</c> with no model turn) and is handled by
/// the caller/skill <i>before</i> compilation. <see cref="Compile"/> still rejects null/whitespace
/// content as a missing required input so a caller that reaches compilation with no content is told
/// so explicitly.
/// </summary>
public static class ExtractionPromptCompiler
{
    /// <summary>
    /// Validated compilation. Returns <see cref="ExtractionPromptCompileResult.MissingRequired"/>
    /// when <paramref name="documentPath"/> or <paramref name="documentContent"/> is absent or
    /// whitespace — never emitting a degraded prompt (FR-002). Otherwise assembles the extraction
    /// prompt and returns success.
    /// </summary>
    public static ExtractionPromptCompileResult Compile(
        string? documentPath,
        string? documentContent,
        string? component = null,
        PromptTemplateLoader? templateLoader = null,
        IReadOnlyList<AcceptanceCriterion>? existingCriteria = null)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return ExtractionPromptCompileResult.MissingRequired(
                "document_path",
                "A source document path is required to compile an extraction prompt.");
        }

        if (string.IsNullOrWhiteSpace(documentContent))
        {
            return ExtractionPromptCompileResult.MissingRequired(
                "document_content",
                "The document has no content to extract from. An empty/whitespace source is an "
                + "'Extracted, []' short-circuit handled before compilation — refusing to emit a "
                + "prompt with no document body.");
        }

        var prompt = Assemble(documentPath, documentContent, component, templateLoader, existingCriteria);
        return ExtractionPromptCompileResult.Success(prompt);
    }

    /// <summary>
    /// Lenient, deterministic prompt assembly — the relocated body of the former
    /// <c>CriteriaExtractor.BuildExtractionPrompt</c>. Prefer <see cref="Compile"/> for the
    /// validated path.
    /// </summary>
    public static string Assemble(
        string docPath,
        string content,
        string? component = null,
        PromptTemplateLoader? templateLoader = null,
        IReadOnlyList<AcceptanceCriterion>? existingCriteria = null)
    {
        var existing = RenderExistingCriteria(existingCriteria);

        if (templateLoader is not null)
        {
            var template = templateLoader.LoadTemplate("criteria-extraction");
            var values = new Dictionary<string, string>
            {
                ["document_text"] = content,
                ["document_title"] = docPath,
                ["existing_criteria"] = existing,
                ["component"] = component ?? ""
            };
            return PromptTemplateLoader.Resolve(template, values);
        }

        var componentHint = component is not null
            ? $"\nThe document belongs to the \"{component}\" component."
            : "";
        var existingHint = existing.Length > 0
            ? "\nThis document already has acceptance criteria. For every criterion you extract that expresses the SAME "
              + "requirement as one of these (even if you word it differently), return that criterion's \"id\" unchanged. "
              + "Omit \"id\" for new requirements. Never invent an id, and never use the same id twice.\n\nExisting criteria:\n"
              + existing
            : "";

        return $$"""
            You are a requirements analyst. Extract all testable acceptance criteria from this single document.

            For each criterion:
            - text: Rewrite using RFC 2119 language (MUST, SHOULD, MAY). Preserve original meaning.
            - rfc2119: The primary RFC 2119 keyword used (MUST, MUST NOT, SHALL, SHALL NOT, SHOULD, SHOULD NOT, MAY, REQUIRED, RECOMMENDED, OPTIONAL)
            - source_section: The heading/section where this criterion was found
            - priority: "high" for MUST/SHALL/REQUIRED, "medium" for SHOULD/RECOMMENDED, "low" for MAY/OPTIONAL
            - tags: 1-3 relevant categorization tags
            - technique_hint (optional): "BVA" if the criterion mentions a numeric range, "DT" if multiple conditions with outcomes, "ST" if a workflow/state change, "EP" if valid/invalid input categories. Omit if no technique clearly applies.
            {{componentHint}}{{existingHint}}

            Respond ONLY with a JSON array. No markdown, no explanation, no code fences. Example:
            [
              {"text": "System MUST validate IBAN format before payment", "rfc2119": "MUST", "source_section": "Payment Validation", "priority": "high", "tags": ["payment", "validation"], "technique_hint": "EP"},
              {"text": "System SHOULD display inline error within 500ms", "rfc2119": "SHOULD", "source_section": "UX Requirements", "priority": "medium", "tags": ["ux", "performance"]}
            ]

            Document path: {{docPath}}

            Document content:
            {{content}}
            """;
    }

    private static readonly JsonSerializerOptions ExistingCriteriaJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Spec 079 FR-001/FR-003: the doc's existing criteria as a compact JSON array (id, text, source_section), in
    /// file order. Deterministic: identical criteria give identical bytes. Empty when there are none.
    /// </summary>
    public static string RenderExistingCriteria(IReadOnlyList<AcceptanceCriterion>? existing)
    {
        if (existing is null || existing.Count == 0)
        {
            return string.Empty;
        }

        var items = existing
            .Where(c => !string.IsNullOrWhiteSpace(c.Id))
            .Select(c => new ExistingCriterionView(c.Id, c.Text, string.IsNullOrWhiteSpace(c.SourceSection) ? null : c.SourceSection))
            .ToList();
        return items.Count == 0 ? string.Empty : JsonSerializer.Serialize(items, ExistingCriteriaJson);
    }

    private sealed record ExistingCriterionView(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("source_section")] string? SourceSection);
}
