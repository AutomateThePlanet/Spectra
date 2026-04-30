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
    /// Spec 040 lifecycle: requested test or suite not found
    /// (e.g., <c>spectra delete TC-999</c>, <c>spectra suite rename missing other</c>).
    /// Shares the numeric value with <see cref="PreFlightBudget"/> but is
    /// command-namespaced — the result artifact's <c>error</c> field
    /// disambiguates (<c>TEST_NOT_FOUND</c>, <c>SUITE_NOT_FOUND</c>).
    /// </summary>
    public const int NotFound = 4;

    /// <summary>
    /// Spec 040 lifecycle: deletion blocked by automation links and no
    /// <c>--force</c> override given. Result artifact lists the stranded files.
    /// </summary>
    public const int AutomationLinked = 5;

    /// <summary>
    /// Spec 040 lifecycle: suite rename target already exists.
    /// </summary>
    public const int SuiteAlreadyExists = 6;

    /// <summary>
    /// Spec 040 lifecycle: suite name violates naming rules
    /// (must match <c>^[a-z0-9][a-z0-9_-]*$</c>).
    /// </summary>
    public const int InvalidSuiteName = 7;

    /// <summary>
    /// Spec 040 lifecycle: suite-delete blocked by external <c>depends_on</c>
    /// references from other suites and no <c>--force</c> override given.
    /// </summary>
    public const int ExternalDependencies = 8;

    /// <summary>
    /// Spec 040 lifecycle: <c>spectra doctor ids</c> reported duplicates and
    /// caller passed <c>--no-interaction</c> without <c>--fix</c>. CI-friendly:
    /// pipelines can fail noisily on regressions.
    /// </summary>
    public const int DuplicatesFound = 9;

    /// <summary>
    /// Operation was cancelled by user (SIGINT).
    /// </summary>
    public const int Cancelled = 130;
}
