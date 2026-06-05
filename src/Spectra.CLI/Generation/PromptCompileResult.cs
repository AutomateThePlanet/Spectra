namespace Spectra.CLI.Generation;

/// <summary>
/// Outcome of <see cref="PromptCompiler.Compile"/>. Either a success carrying the
/// fully-grounded prompt, or a refuse-to-emit failure naming the missing required
/// input (Spec 053 FR-004). The "missing input" case is an expected outcome — it is
/// modeled as a value, not an exception, so a skill/CLI can act on the specific input.
/// </summary>
public sealed record PromptCompileResult
{
    /// <summary>True when <see cref="Prompt"/> is populated.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>The compiled, fully-grounded prompt. Non-null only on success.</summary>
    public string? Prompt { get; private init; }

    /// <summary>
    /// Machine-readable name of the required input that was missing
    /// (e.g. "criteria_context", "count", "user_prompt"). Non-null only on failure.
    /// </summary>
    public string? MissingInput { get; private init; }

    /// <summary>Human-readable explanation of the refusal. Non-null only on failure.</summary>
    public string? Message { get; private init; }

    private PromptCompileResult() { }

    /// <summary>Creates a successful result carrying the compiled prompt.</summary>
    public static PromptCompileResult Success(string prompt) => new()
    {
        IsSuccess = true,
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt))
    };

    /// <summary>
    /// Creates a refuse-to-emit result. No prompt is produced; the caller is told
    /// exactly which input failed.
    /// </summary>
    public static PromptCompileResult MissingRequired(string input, string message) => new()
    {
        IsSuccess = false,
        MissingInput = input,
        Message = message
    };
}
