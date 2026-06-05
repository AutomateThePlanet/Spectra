using System.Text;
using Spectra.CLI.Profile;
using Spectra.CLI.Prompts;
using Spectra.Core.Models.Testimize;

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
        string? profileFormat = null,
        TestimizeDataset? testimizeData = null)
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
            profileFormat,
            testimizeData);

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
        string? profileFormat = null,
        TestimizeDataset? testimizeData = null)
    {
        // The JSON output schema sent to the AI. Prefer the caller-supplied profileFormat
        // (resolved from profiles/_default.yaml on disk or the embedded default by
        // ProfileFormatLoader). When null (legacy callers and unit tests), fall back to the
        // embedded default to keep behavior identical.
        var jsonExample = profileFormat ?? ProfileFormatLoader.LoadFormat(Directory.GetCurrentDirectory());

        // Render the optional Testimize dataset as a YAML-ish block only when present and
        // non-empty. When null or empty, both placeholders are empty strings and the
        // {{#if testimize_dataset}} block in test-generation.md collapses to nothing.
        var testimizeBlock = FormatTestimizeDataset(testimizeData);
        var testimizeStrategyName = testimizeData?.Strategy ?? "";

        if (templateLoader is not null)
        {
            var template = templateLoader.LoadTemplate("test-generation");
            var values = new Dictionary<string, string>
            {
                ["testimize_dataset"] = testimizeBlock,
                ["testimize_strategy_name"] = testimizeStrategyName,
                ["behaviors"] = userPrompt,
                ["suite_name"] = "",
                ["existing_tests"] = "",
                ["acceptance_criteria"] = criteriaContext ?? "",
                ["profile_format"] = jsonExample,
                ["count"] = requestedCount.ToString(),
                ["focus_areas"] = ""
            };

            return PromptTemplateLoader.Resolve(template, values);
        }

        return $"""
            You are a test case generation expert that creates DOCUMENT-GROUNDED test cases.

            ## CRITICAL RULES

            1. NEVER generate generic test patterns. Every test MUST trace to specific documentation.
            2. Use the available tools to read documentation ON-DEMAND instead of relying on memory.
            3. ALWAYS check for duplicates before generating tests.

            ## WORKFLOW

            Follow this exact workflow for test generation:

            1. **LIST DOCUMENTS**: First, call ListDocumentationFiles to see what documentation is available.
            2. **READ TEST INDEX**: Call ReadTestIndex to see existing tests and avoid duplicates.
            3. **READ RELEVANT DOCS**: Based on the user's request, call ReadDocument for specific files.
            4. **CHECK DUPLICATES**: Before finalizing, call CheckDuplicates with your proposed titles.
            5. **GET TEST IDs**: Call GetNextTestIds to allocate unique IDs for new tests.
            6. **GENERATE TESTS**: Return tests as a JSON array.

            ## OUTPUT FORMAT

            After using tools to gather information, your FINAL message must contain ONLY a JSON array of test cases.
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
            1. Use the tools to read documentation and check for duplicates first
            2. Only generate tests that are grounded in the documentation
            3. Ensure unique test IDs using GetNextTestIds
            4. Your FINAL response must be ONLY the JSON array — no other text
            5. MANDATORY: For each test, populate the "criteria" array with IDs of acceptance criteria it verifies (e.g. ["AC-REPORTING-001", "AC-REPORTING-003"]). Never leave criteria empty when acceptance criteria are provided above.
            """;
    }

    /// <summary>
    /// Renders a <see cref="TestimizeDataset"/> as a compact YAML-ish block for embedding in
    /// the test-generation prompt. Returns an empty string when the dataset is null or contains
    /// no rows, which causes the <c>{{#if testimize_dataset}}</c> block in
    /// <c>test-generation.md</c> to collapse to nothing.
    /// </summary>
    public static string FormatTestimizeDataset(TestimizeDataset? dataset)
    {
        if (dataset is null || dataset.TestCases.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("```yaml");
        sb.AppendLine($"strategy: {dataset.Strategy}");
        sb.AppendLine($"field_count: {dataset.FieldCount}");
        sb.AppendLine($"test_data_sets: {dataset.TestCases.Count}");
        sb.AppendLine("fields:");
        foreach (var f in dataset.Fields)
        {
            sb.AppendLine($"  - name: {f.Name}");
            sb.AppendLine($"    type: {f.Type}");
            if (f.Required) sb.AppendLine("    required: true");
            if (f.Min is not null) sb.AppendLine($"    min: {f.Min}");
            if (f.Max is not null) sb.AppendLine($"    max: {f.Max}");
            if (f.MinLength is not null) sb.AppendLine($"    min_length: {f.MinLength}");
            if (f.MaxLength is not null) sb.AppendLine($"    max_length: {f.MaxLength}");
            if (!string.IsNullOrWhiteSpace(f.MinDate)) sb.AppendLine($"    min_date: {f.MinDate}");
            if (!string.IsNullOrWhiteSpace(f.MaxDate)) sb.AppendLine($"    max_date: {f.MaxDate}");
            if (f.AllowedValues is { Count: > 0 })
                sb.AppendLine($"    allowed_values: [{string.Join(", ", f.AllowedValues.Select(v => $"\"{v}\""))}]");
            if (!string.IsNullOrWhiteSpace(f.ExpectedInvalidMessage))
                sb.AppendLine($"    expected_invalid_message: \"{EscapeYaml(f.ExpectedInvalidMessage)}\"");
        }
        sb.AppendLine("test_cases:");
        for (var i = 0; i < dataset.TestCases.Count; i++)
        {
            var row = dataset.TestCases[i];
            sb.AppendLine($"  - id: {i + 1}");
            if (row.Score > 0) sb.AppendLine($"    score: {row.Score:F2}");
            sb.AppendLine("    values:");
            foreach (var cell in row.Values)
            {
                sb.AppendLine($"      - field: {cell.FieldName}");
                sb.AppendLine($"        value: {FormatYamlValue(cell.Value)}");
                sb.AppendLine($"        category: {cell.Category}");
                if (!string.IsNullOrWhiteSpace(cell.ExpectedInvalidMessage))
                    sb.AppendLine($"        expected_invalid_message: \"{EscapeYaml(cell.ExpectedInvalidMessage)}\"");
            }
        }
        sb.AppendLine("```");
        return sb.ToString();
    }

    private static string FormatYamlValue(object? value)
    {
        if (value is null) return "null";
        if (value is string s) return $"\"{EscapeYaml(s)}\"";
        if (value is bool b) return b ? "true" : "false";
        if (value is DateTime dt) return $"\"{dt:yyyy-MM-dd}\"";
        if (value is System.Collections.IEnumerable en && value is not string)
        {
            var items = new List<string>();
            foreach (var item in en) items.Add(FormatYamlValue(item));
            return $"[{string.Join(", ", items)}]";
        }
        return value.ToString() ?? "null";
    }

    private static string EscapeYaml(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
