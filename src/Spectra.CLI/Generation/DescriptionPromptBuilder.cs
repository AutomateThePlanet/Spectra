namespace Spectra.CLI.Generation;

/// <summary>
/// Spec 059: deterministic, model-free user-prompt shaping for the from-description flow.
/// Relocated out of <c>UserDescribedGenerator.BuildPrompt</c> so the description-prompt text
/// survives the removal of the in-process generator (the agent-calling
/// <c>UserDescribedGenerator.GenerateAsync</c> is deleted with the provider chain). The
/// <c>spectra ai compile-prompt --from-description</c> path calls this to build the user prompt,
/// then hands it to <see cref="PromptCompiler.Assemble"/> for grounded assembly.
///
/// Criteria are NOT inlined here — they are forwarded to the prompt compiler, which emits the
/// MANDATORY "you MUST map" block (the same block the batch flow uses). See Spec 050.
/// </summary>
public static class DescriptionPromptBuilder
{
    /// <summary>
    /// Builds the user-level instruction that frames a tester's plain-language description as a
    /// single grounded test case. Deterministic: identical inputs produce identical output.
    /// </summary>
    public static string Build(
        string description,
        string? context,
        string suite,
        IReadOnlyCollection<string> existingIds,
        string? documentContext = null)
    {
        var idList = string.Join(", ", existingIds);
        var contextLine = string.IsNullOrEmpty(context) ? "" : $"\nAdditional context: {context}";

        var prompt = $"""
            Create a single manual test case for the '{suite}' feature based on this tester's description.

            **The user's description is the source of truth.** Use any reference material below ONLY to align terminology, navigation paths, and known acceptance criteria — never to override or contradict the description.

            Description: {description}{contextLine}

            Requirements:
            - Create a unique ID in format TC-XXX (do not duplicate: {idList})
            - Include clear steps and expected results
            - Set priority based on the described behavior's criticality
            - This is a user-described test — include relevant tags

            Provide: id, title, priority, steps, expected_result, tags, component (if inferable)
            """;

        if (!string.IsNullOrWhiteSpace(documentContext))
        {
            prompt += $"""


                ## Reference Documentation (for formatting context only)

                The following documentation describes the product area related to this test.
                Use it to align your test steps with actual product behavior, terminology,
                and navigation paths. Do NOT verify the user's description against these docs —
                the user's description is the source of truth.

                {documentContext}
                """;
        }

        return prompt;
    }
}
