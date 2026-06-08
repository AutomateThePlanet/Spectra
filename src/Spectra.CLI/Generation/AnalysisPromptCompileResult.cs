namespace Spectra.CLI.Generation;

/// <summary>
/// Outcome of <see cref="AnalysisPromptCompiler.Compile"/> (Spec 059). Mirrors
/// <see cref="PromptCompileResult"/>: either a success carrying the compiled behavior-analysis
/// prompt, or a refuse-to-emit failure naming the missing required input. The "missing input"
/// case is an expected outcome — modeled as a value, not an exception.
/// </summary>
public sealed record AnalysisPromptCompileResult
{
    /// <summary>True when <see cref="Prompt"/> is populated.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>The compiled behavior-analysis prompt. Non-null only on success.</summary>
    public string? Prompt { get; private init; }

    /// <summary>
    /// Machine-readable name of the required input that was missing (e.g. "documents").
    /// Non-null only on failure.
    /// </summary>
    public string? MissingInput { get; private init; }

    /// <summary>Human-readable explanation of the refusal. Non-null only on failure.</summary>
    public string? Message { get; private init; }

    private AnalysisPromptCompileResult() { }

    /// <summary>Creates a successful result carrying the compiled prompt.</summary>
    public static AnalysisPromptCompileResult Success(string prompt) => new()
    {
        IsSuccess = true,
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt))
    };

    /// <summary>
    /// Creates a refuse-to-emit result. No prompt is produced; the caller is told exactly which
    /// input failed.
    /// </summary>
    public static AnalysisPromptCompileResult MissingRequired(string input, string message) => new()
    {
        IsSuccess = false,
        MissingInput = input,
        Message = message
    };
}
