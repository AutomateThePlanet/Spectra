namespace Spectra.CLI.Verification;

/// <summary>
/// Outcome of <see cref="CriticPromptCompiler.Compile"/>. Either a success carrying the compiled
/// critic verification prompt, or a refuse-to-emit failure naming the missing required input
/// (Spec 055 FR-002). The "missing input" case is an expected outcome — modeled as a value, not an
/// exception, so a skill/CLI can act on the specific input. Mirrors
/// <see cref="Spectra.CLI.Generation.PromptCompileResult"/> and
/// <see cref="Spectra.CLI.Extraction.ExtractionPromptCompileResult"/>.
/// </summary>
public sealed record CriticPromptCompileResult
{
    /// <summary>True when <see cref="Prompt"/> is populated.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>The compiled critic prompt (<c>{system}\n\n---\n\n{user}</c>). Non-null only on success.</summary>
    public string? Prompt { get; private init; }

    /// <summary>
    /// Machine-readable name of the required input that was missing (e.g. "test_artifact").
    /// Non-null only on failure.
    /// </summary>
    public string? MissingInput { get; private init; }

    /// <summary>Human-readable explanation of the refusal. Non-null only on failure.</summary>
    public string? Message { get; private init; }

    private CriticPromptCompileResult() { }

    /// <summary>Creates a successful result carrying the compiled prompt.</summary>
    public static CriticPromptCompileResult Success(string prompt) => new()
    {
        IsSuccess = true,
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt))
    };

    /// <summary>
    /// Creates a refuse-to-emit result. No prompt is produced; the caller is told exactly which
    /// input failed.
    /// </summary>
    public static CriticPromptCompileResult MissingRequired(string input, string message) => new()
    {
        IsSuccess = false,
        MissingInput = input,
        Message = message
    };
}
