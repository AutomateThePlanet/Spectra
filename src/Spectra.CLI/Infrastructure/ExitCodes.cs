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
    /// Operation was cancelled by user (SIGINT).
    /// </summary>
    public const int Cancelled = 130;
}
