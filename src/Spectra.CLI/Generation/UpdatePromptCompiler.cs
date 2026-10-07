using System.Text.Json;
using Spectra.CLI.Profile;
using Spectra.CLI.Prompts;
using Spectra.Core.Models;

namespace Spectra.CLI.Generation;

/// <summary>Spec 088: a compiled update prompt, or the refusal; <see cref="Warning"/> for an outdated custom template.</summary>
public sealed record UpdatePromptResult(string? Prompt, string? Warning, string? RefusalCode, string? Message)
{
    public bool IsSuccess => Prompt is not null;
}

/// <summary>
/// Spec 063: deterministic, model-free compiler for the inverted <b>update</b> seam. Mirrors
/// <see cref="PromptCompiler"/> but assembles an <i>edit</i> prompt for ONE OUTDATED test rather
/// than a generation prompt: the existing test (serialized) + the changed source/criteria +
/// explicit "edit, don't regenerate; preserve id/structure/manual fields" directives, via the
/// <c>test-update</c> template.
///
/// Determinism: the emitted text contains no timestamps, GUIDs, or unordered enumeration —
/// identical inputs produce byte-identical output.
/// </summary>
public static class UpdatePromptCompiler
{
    public const string ErrorTemplateOutdated = "TEMPLATE_OUTDATED";

    /// <summary>The hint for a custom template written before Spec 088.</summary>
    public const string ResetHint =
        "The template .spectra/prompts/test-update.md has no {{tests}} / {{source_diff}} placeholders. "
        + "Run 'spectra update-skills' (it refreshes a copy you never edited) or, for a customised copy, "
        + "'spectra prompts reset test-update' (it replaces your changes).";

    /// <summary>Spec 088 (FR-012): a user-customized template without both new placeholders.</summary>
    public static bool IsOutdated(PromptTemplate template) =>
        template.IsUserCustomized
        && (!template.Body.Contains("{{tests}}", StringComparison.Ordinal)
            || !template.Body.Contains("{{source_diff}}", StringComparison.Ordinal));

    /// <summary>
    /// Spec 088 (FR-008–FR-012): fills the <c>test-update</c> template from an <see cref="UpdateContext"/> — the
    /// sections, the linked criteria and the tests. An outdated custom template still works for one test, through its
    /// legacy placeholders (<c>test_case</c>, <c>current_source</c> = the rendered sections, <c>acceptance_criteria</c>),
    /// with a warning; for a group it is refused (<see cref="ErrorTemplateOutdated"/>).
    /// </summary>
    public static UpdatePromptResult CompileContext(UpdateContext context, PromptTemplate template, string profileFormat, bool group)
    {
        var sourceDiff = UpdatePromptRenderer.SourceDiff(context).TrimEnd('\n');
        var criteria = UpdatePromptRenderer.Criteria(context).TrimEnd('\n');
        if (IsOutdated(template))
        {
            if (group || context.Tests.Count != 1)
            {
                return new UpdatePromptResult(null, null, ErrorTemplateOutdated, $"{ResetHint} A group prompt needs them.");
            }

            var legacy = new Dictionary<string, string>
            {
                ["test_case"] = SerializeTest(context.Tests[0].Test),
                ["current_source"] = sourceDiff,
                ["acceptance_criteria"] = criteria,
                ["profile_format"] = profileFormat
            };
            return new UpdatePromptResult(PromptTemplateLoader.Resolve(template, legacy), ResetHint, null, null);
        }

        var values = new Dictionary<string, string>
        {
            ["source_diff"] = sourceDiff,
            ["acceptance_criteria"] = criteria,
            ["tests"] = UpdatePromptRenderer.Tests(context).TrimEnd('\n'),
            ["profile_format"] = profileFormat,
            ["test_case"] = "",
            ["current_source"] = ""
        };
        return new UpdatePromptResult(PromptTemplateLoader.Resolve(template, values), null, null, null);
    }

    /// <summary>
    /// Validated compilation. Returns <see cref="PromptCompileResult.MissingRequired"/> when a
    /// required input is missing: the original test, or any changed source/criteria context to
    /// reconcile against (refusing to emit an ungrounded edit prompt). Otherwise assembles the
    /// edit prompt and returns success.
    /// </summary>
    public static PromptCompileResult Compile(
        TestCase? originalTest,
        string? sourceContext,
        string? criteriaContext,
        PromptTemplateLoader? templateLoader = null,
        string? profileFormat = null)
    {
        if (originalTest is null)
        {
            return PromptCompileResult.MissingRequired(
                "original_test",
                "An existing test is required to compile an update prompt.");
        }

        if (string.IsNullOrWhiteSpace(sourceContext) && string.IsNullOrWhiteSpace(criteriaContext))
        {
            return PromptCompileResult.MissingRequired(
                "changed_context",
                "No changed source or acceptance-criteria context was resolved for this suite. "
                + "Refusing to emit an update prompt without anything to reconcile the test against. "
                + "Run 'spectra docs index' / 'spectra ai analyze --extract-criteria' first.");
        }

        var prompt = Assemble(originalTest, sourceContext, criteriaContext, templateLoader, profileFormat);
        return PromptCompileResult.Success(prompt);
    }

    /// <summary>
    /// Lenient, deterministic edit-prompt assembly. Serializes <paramref name="originalTest"/> to
    /// the output JSON schema (so the model edits that exact shape) and injects the changed
    /// source/criteria. Prefer <see cref="Compile"/> for the validated path.
    /// </summary>
    public static string Assemble(
        TestCase originalTest,
        string? sourceContext = null,
        string? criteriaContext = null,
        PromptTemplateLoader? templateLoader = null,
        string? profileFormat = null)
    {
        ArgumentNullException.ThrowIfNull(originalTest);

        var jsonExample = profileFormat ?? ProfileFormatLoader.LoadFormat(Directory.GetCurrentDirectory());
        var serializedTest = SerializeTest(originalTest);

        // Spec 088: the built-in template no longer has {{test_case}}; this legacy single-test path then uses the
        // inline layout below.
        var loaded = templateLoader?.LoadTemplate("test-update");
        if (loaded is not null && loaded.Body.Contains("{{test_case}}", StringComparison.Ordinal))
        {
            var template = loaded;
            var values = new Dictionary<string, string>
            {
                ["test_case"] = serializedTest,
                ["current_source"] = sourceContext ?? "",
                ["acceptance_criteria"] = criteriaContext ?? "",
                ["profile_format"] = jsonExample
            };
            return PromptTemplateLoader.Resolve(template, values);
        }

        // Fallback inline template (templateLoader null — legacy callers / unit tests).
        var sourceBlock = string.IsNullOrWhiteSpace(sourceContext)
            ? ""
            : $"\n### CURRENT SOURCE (reconcile the test against this)\n{sourceContext}\n";
        var criteriaBlock = string.IsNullOrWhiteSpace(criteriaContext)
            ? ""
            : $"\n### ACCEPTANCE CRITERIA (map the edited test to these)\n{criteriaContext}\n";

        return $$"""
            You are a test maintenance engineer. Edit the affected parts of this OUTDATED test in
            place to match the current documentation. EDIT — do not regenerate. Keep the original
            id. Keep priority, component, and tags unchanged. Touch only what the change requires.

            ### EXISTING TEST (edit this)
            ```json
            {{serializedTest}}
            ```
            {{sourceBlock}}{{criteriaBlock}}
            ## OUTPUT FORMAT

            Your FINAL message must contain ONLY a JSON array with the SINGLE edited test case,
            keeping the original id, following this schema:

            ```json
            {{jsonExample}}
            ```
            """;
    }

    /// <summary>
    /// Serializes a test to the JSON object shape the generation parse pipeline understands
    /// (snake_case keys), so the model can edit it and return the same shape. Deterministic.
    /// </summary>
    public static string SerializeTest(TestCase test)
    {
        ArgumentNullException.ThrowIfNull(test);

        var obj = new Dictionary<string, object?>
        {
            ["id"] = test.Id,
            ["title"] = test.Title,
            ["priority"] = test.Priority.ToString().ToLowerInvariant(),
            ["tags"] = test.Tags,
            ["component"] = test.Component,
            ["preconditions"] = test.Preconditions,
            ["steps"] = test.Steps,
            ["expected_result"] = test.ExpectedResult,
            ["test_data"] = test.TestData,
            ["source_refs"] = test.SourceRefs,
            ["scenario_from_doc"] = test.ScenarioFromDoc,
            ["criteria"] = test.Criteria
        };

        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        });
    }
}
