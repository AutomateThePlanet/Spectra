namespace Spectra.CLI.Infrastructure;

/// <summary>
/// Controls how command results are rendered to stdout.
/// </summary>
public enum OutputFormat
{
    /// <summary>
    /// Human-readable Spectre.Console output with colors, spinners, tables.
    /// </summary>
    Human,

    /// <summary>
    /// Structured JSON on stdout, no ANSI codes, no spinners, no progress.
    /// </summary>
    Json
}
