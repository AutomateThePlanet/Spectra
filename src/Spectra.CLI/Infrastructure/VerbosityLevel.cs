namespace Spectra.CLI.Infrastructure;

/// <summary>
/// Output verbosity levels for CLI commands.
/// </summary>
public enum VerbosityLevel
{
    /// <summary>
    /// No output except errors.
    /// </summary>
    Quiet,

    /// <summary>
    /// Minimal output (errors and warnings).
    /// </summary>
    Minimal,

    /// <summary>
    /// Normal output (default).
    /// </summary>
    Normal,

    /// <summary>
    /// Detailed output for troubleshooting.
    /// </summary>
    Detailed,

    /// <summary>
    /// Full diagnostic output.
    /// </summary>
    Diagnostic
}
