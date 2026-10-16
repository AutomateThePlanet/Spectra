using System.Text;
using Spectra.CLI.Profile;
using Spectra.CLI.Prompts;

namespace Spectra.CLI.Generation;

/// <summary>
/// Deterministic, model-free prompt compiler (Spec 053). Relocated out of
/// <c>CopilotGenerationAgent.BuildFullPrompt</c> so the grounded-prompt assembly is a
/// standalone artifact, decoupled from the Copilot SDK by location (investigation 03 F-2).
///
/// Two entry points:
/// <list type="bullet">
/// <item><see cref="Assemble"/> — the relocated, <b>lenient</b> assembly. Same behavior and
/// signature contract as the old <c>BuildFullPrompt</c>: a null/empty criteria context simply
/// omits the MANDATORY block. <c>CopilotGenerationAgent.BuildFullPrompt</c> delegates here so
/// there is a single source of prompt truth.</item>
/// <item><see cref="Compile"/> — the <b>validated</b> entry (FR-004). Refuses to emit when a
/// required input is missing and names the failing input. This is what the
/// <c>spectra ai compile-prompt</c> command calls.</item>
/// </list>
///
/// Determinism (FR-003): the emitted text contains no timestamps, GUIDs, or unordered
/// enumeration — identical inputs produce byte-identical output.
/// </summary>
public static class PromptCompiler
{
    /// <summary>
    /// Validated compilation. Returns <see cref="PromptCompileResult.MissingRequired"/> when a
    /// required input (user prompt, positive count, criteria context) is absent or whitespace —
    /// never emitting a degraded prompt with a missing section (FR-004). Otherwise assembles the
    /// grounded prompt and returns success.
    /// </summary>
    public static PromptCompileResult Compile(
        string? userPrompt,
        int requestedCount,
        string? criteriaContext,
        PromptTemplateLoader? templateLoader = null,
        string? profileFormat = null)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return PromptCompileResult.MissingRequired(
                "user_prompt",
                "A user prompt (focus / behaviors) is required to compile a generation prompt.");
        }

        if (requestedCount <= 0)
        {
            return PromptCompileResult.MissingRequired(
                "count",
                $"Requested test count must be greater than zero (got {requestedCount}).");
        }

        if (string.IsNullOrWhiteSpace(criteriaContext))
        {
            return PromptCompileResult.MissingRequired(
                "criteria_context",
                "No acceptance-criteria context was resolved for this suite. Refusing to emit a "
                + "prompt without grounding criteria. Run 'spectra ai analyze --extract-criteria' "
                + "(or 'spectra docs index') first.");
        }

        var prompt = Assemble(
            userPrompt,
            requestedCount,
            criteriaContext,
            templateLoader,
            profileFormat);

        return PromptCompileResult.Success(prompt);
    }

    /// <summary>
    /// Lenient, deterministic prompt assembly — the relocated body of the former
    /// <c>BuildFullPrompt</c>. Tolerates a null/empty <paramref name="criteriaContext"/> by
    /// omitting the MANDATORY block (preserves the from-description flow's existing behavior).
    /// Prefer <see cref="Compile"/> for the validated path.
    /// </summary>
    public static string Assemble(
        string userPrompt,
        int requestedCount,
        string? criteriaContext = null,
        PromptTemplateLoader? templateLoader = null,
        string? profileFormat = null)
    {
        // The JSON output schema sent to the AI. Prefer the caller-supplied profileFormat
        // (resolved from profiles/_default.yaml on disk or the embedded default by
        // ProfileFormatLoader). When null (legacy callers and unit tests), fall back to the
        // embedded default to keep behavior identical.
        var jsonExample = profileFormat ?? ProfileFormatLoader.LoadFormat(Directory.GetCurrentDirectory());

        if (templateLoader is not null)
        {
            var template = templateLoader.LoadTemplate("test-generation");
            var values = new Dictionary<string, string>
            {
                // Spec 099 F5: the in-process Testimize dataset is gone (its only producer was the Copilot-era
                // generator); the placeholders stay empty, so {{#if testimize_dataset}} collapses as before.
                ["testimize_dataset"] = "",
                ["testimize_strategy_name"] = "",
                ["behaviors"] = userPrompt,
                ["suite_name"] = "",
                ["existing_tests"] = "",
                ["acceptance_criteria"] = criteriaContext ?? "",
                ["profile_format"] = jsonExample,
                ["count"] = requestedCount.ToString(),
                ["focus_areas"] = "",
                // Spec 094: count mode — the model reads the docs itself; no inlined sections.
                ["read_docs"] = "yes",
                ["source_sections"] = ""
            };

            return PromptTemplateLoader.Resolve(template, values);
        }

        return $"""
            You are a test case generation expert that creates DOCUMENT-GROUNDED test cases.

            ## CRITICAL RULES

            1. NEVER generate generic test patterns. Every test MUST trace to specific documentation.
            2. Every step and expected result MUST come from the documentation — never from memory or assumptions.

            ## DOCUMENTATION

            Read the relevant documentation with your file tools before writing any test, and ground every step and expected
            result in what it says. Test IDs are assigned when the tests are ingested — do not invent them.

            ## OUTPUT FORMAT

            Your FINAL message must contain ONLY a JSON array of test cases.
            Do NOT include any explanatory text before or after the JSON. Output ONLY the JSON array.

            The JSON array must follow this exact schema:

            ```json
            {jsonExample}
            ```

            ---

            ## YOUR TASK

            Generate {requestedCount} new manual test cases based on this request:

            {userPrompt}

            {(string.IsNullOrEmpty(criteriaContext) ? "" : $"\n## ACCEPTANCE CRITERIA — MANDATORY\n\nYou MUST map each test case to matching acceptance criteria below. Every test MUST have at least one criterion ID in its \"criteria\" array. If a test doesn't match any criterion, use the closest related one.\n\n{criteriaContext}\n")}
            IMPORTANT:
            1. Only generate tests that are grounded in the documentation
            2. Your FINAL response must be ONLY the JSON array — no other text
            3. MANDATORY: For each test, populate the "criteria" array with IDs of acceptance criteria it verifies (e.g. ["AC-REPORTING-001", "AC-REPORTING-003"]). Never leave criteria empty when acceptance criteria are provided above.
            """;
    }
}
