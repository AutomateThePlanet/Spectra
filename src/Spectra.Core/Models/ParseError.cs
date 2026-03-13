namespace Spectra.Core.Models;

/// <summary>
/// Represents an error that occurred during parsing.
/// </summary>
/// <param name="Code">Error code (e.g., "INVALID_YAML", "MISSING_FIELD")</param>
/// <param name="Message">Human-readable error message</param>
/// <param name="FilePath">Path to the file that caused the error</param>
/// <param name="Line">Line number where the error occurred (1-based)</param>
/// <param name="Column">Column number where the error occurred (1-based)</param>
public sealed record ParseError(
    string Code,
    string Message,
    string? FilePath = null,
    int? Line = null,
    int? Column = null);
