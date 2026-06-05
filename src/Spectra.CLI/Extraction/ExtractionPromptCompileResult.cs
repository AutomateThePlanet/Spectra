namespace Spectra.CLI.Extraction;

/// <summary>
/// Outcome of <see cref="ExtractionPromptCompiler.Compile"/>. Either a success carrying the
/// compiled criteria-extraction prompt, or a refuse-to-emit failure naming the missing required
/// input (Spec 054 FR-002). The "missing input" case is an expected outcome — modeled as a value,
/// not an exception, so a skill/CLI can act on the specific input. Mirrors
/// <see cref="Spectra.CLI.Generation.PromptCompileResult"/>.
/// </summary>
public sealed record ExtractionPromptCompileResult
{
    /// <summary>True when <see cref="Prompt"/> is populated.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>The compiled extraction prompt. Non-null only on success.</summary>
    public string? Prompt { get; private init; }

    /// <summary>
    /// Machine-readable name of the required input that was missing
    /// (e.g. "document_path", "document_content"). Non-null only on failure.
    /// </summary>
    public string? MissingInput { get; private init; }

    /// <summary>Human-readable explanation of the refusal. Non-null only on failure.</summary>
    public string? Message { get; private init; }

    private ExtractionPromptCompileResult() { }

    /// <summary>Creates a successful result carrying the compiled prompt.</summary>
    public static ExtractionPromptCompileResult Success(string prompt) => new()
    {
        IsSuccess = true,
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt))
    };

    /// <summary>
    /// Creates a refuse-to-emit result. No prompt is produced; the caller is told exactly which
    /// input failed.
    /// </summary>
    public static ExtractionPromptCompileResult MissingRequired(string input, string message) => new()
    {
        IsSuccess = false,
        MissingInput = input,
        Message = message
    };
}
