namespace Spectra.CLI.Infrastructure;

/// <summary>
/// Standard exit codes for the CLI.
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// Operation completed successfully.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// General error occurred.
    /// </summary>
    public const int Error = 1;

    /// <summary>
    /// Validation error occurred.
    /// </summary>
    public const int ValidationError = 2;

    /// <summary>
    /// Required arguments missing in non-interactive mode.
    /// </summary>
    public const int MissingArguments = 3;

    /// <summary>
    /// Pre-flight token budget exceeded (Spec 040). The analyzer prompt would
    /// have overflowed the configured <c>ai.analysis.max_prompt_tokens</c>
    /// limit. The error message names the candidate suites and suggests
    /// narrowing with <c>--suite</c>.
    /// </summary>
    public const int PreFlightBudget = 4;

    /// <summary>
    /// Operation was cancelled by user (SIGINT).
    /// </summary>
    public const int Cancelled = 130;
}
