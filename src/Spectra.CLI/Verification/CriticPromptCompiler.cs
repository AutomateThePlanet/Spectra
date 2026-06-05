using Spectra.CLI.Agent.Critic;
using Spectra.CLI.Prompts;
using Spectra.Core.Models;

namespace Spectra.CLI.Verification;

/// <summary>
/// Deterministic, model-free critic-prompt compiler (Spec 055). Wraps the reused-verbatim
/// <see cref="CriticPromptBuilder"/> behind a validated refuse-to-emit boundary, mirroring
/// <see cref="Spectra.CLI.Generation.PromptCompiler"/> (053) and
/// <see cref="Spectra.CLI.Extraction.ExtractionPromptCompiler"/> (054).
///
/// Two entry points:
/// <list type="bullet">
/// <item><see cref="Assemble"/> — the lenient assembly. Delegates to <see cref="CriticPromptBuilder"/>
/// (system + user prompt, joined <c>{system}\n\n---\n\n{user}</c> exactly as the runtime does) so
/// there is a single source of critic-prompt truth.</item>
/// <item><see cref="Compile"/> — the validated entry (FR-002). Refuses to emit when the required
/// test artifact is missing and names the failing input.</item>
/// </list>
///
/// Determinism (FR-002): the builder emits no timestamps, GUIDs, or unordered enumeration — the
/// document order is the input order — so identical inputs produce byte-identical output. The
/// compiler never opens a model/provider session; the subagent skill performs the model turn.
///
/// Isolation (FR-002): the emitted prompt contains only the test artifact + the selected source
/// documents — never the generator's prompt, reasoning, tool calls, or token usage.
/// </summary>
public static class CriticPromptCompiler
{
    /// <summary>
    /// Validated compilation. Returns <see cref="CriticPromptCompileResult.MissingRequired"/> when
    /// the test artifact is absent (null) or carries no id/title — the thing the critic must verify.
    /// An empty <paramref name="documents"/> set is NOT a refusal (the builder emits a "no relevant
    /// documentation provided" notice). Otherwise assembles the critic prompt and returns success.
    /// </summary>
    public static CriticPromptCompileResult Compile(
        TestCase? test,
        IReadOnlyList<SourceDocument>? documents = null,
        PromptTemplateLoader? templateLoader = null)
    {
        if (test is null || string.IsNullOrWhiteSpace(test.Id) || string.IsNullOrWhiteSpace(test.Title))
        {
            return CriticPromptCompileResult.MissingRequired(
                "test_artifact",
                "A test artifact with an id and title is required to compile a critic prompt.");
        }

        var prompt = Assemble(test, documents ?? [], templateLoader);
        return CriticPromptCompileResult.Success(prompt);
    }

    /// <summary>
    /// Lenient, deterministic critic-prompt assembly — delegates to the reused-verbatim
    /// <see cref="CriticPromptBuilder"/>. Prefer <see cref="Compile"/> for the validated path.
    /// </summary>
    public static string Assemble(
        TestCase test,
        IReadOnlyList<SourceDocument> documents,
        PromptTemplateLoader? templateLoader = null)
    {
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(documents);

        var builder = new CriticPromptBuilder();
        builder.SetTemplateLoader(templateLoader);

        var system = builder.BuildSystemPrompt();
        var user = builder.BuildUserPrompt(test, documents);
        return $"{system}\n\n---\n\n{user}";
    }
}
