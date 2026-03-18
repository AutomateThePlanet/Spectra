namespace Spectra.Core.Models;

/// <summary>
/// Represents an error found during validation.
/// </summary>
/// <param name="Code">Error code (e.g., "MISSING_PRIORITY", "INVALID_ID")</param>
/// <param name="Message">Human-readable error message</param>
/// <param name="FilePath">Path to the file that caused the error</param>
/// <param name="TestId">Test ID if applicable</param>
/// <param name="LineNumber">Line number where error occurred (if determinable)</param>
/// <param name="FieldName">Field name that caused the error</param>
public sealed record ValidationError(
    string Code,
    string Message,
    string FilePath,
    string? TestId = null,
    int? LineNumber = null,
    string? FieldName = null);

/// <summary>
/// Represents a warning found during validation.
/// </summary>
/// <param name="Code">Warning code (e.g., "MISSING_TAGS", "LONG_TITLE")</param>
/// <param name="Message">Human-readable warning message</param>
/// <param name="FilePath">Path to the file that caused the warning</param>
/// <param name="TestId">Test ID if applicable</param>
public sealed record ValidationWarning(
    string Code,
    string Message,
    string FilePath,
    string? TestId = null);

/// <summary>
/// Result of validating test cases.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// Returns true if validation passed (no errors).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Total number of files validated.
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Number of files that passed validation.
    /// </summary>
    public int ValidFiles { get; init; }

    /// <summary>
    /// Validation errors that prevent the test from being used.
    /// </summary>
    public required IReadOnlyList<ValidationError> Errors { get; init; }

    /// <summary>
    /// Validation warnings that don't prevent the test from being used.
    /// </summary>
    public required IReadOnlyList<ValidationWarning> Warnings { get; init; }

    /// <summary>
    /// Creates an empty (valid) result.
    /// </summary>
    public static ValidationResult Valid() => new()
    {
        Errors = [],
        Warnings = []
    };

    /// <summary>
    /// Creates a result with errors.
    /// </summary>
    public static ValidationResult WithErrors(IReadOnlyList<ValidationError> errors) => new()
    {
        Errors = errors,
        Warnings = []
    };

    /// <summary>
    /// Creates a result with a single error.
    /// </summary>
    public static ValidationResult WithError(ValidationError error) => new()
    {
        Errors = [error],
        Warnings = []
    };

    /// <summary>
    /// Combines multiple validation results.
    /// </summary>
    public static ValidationResult Combine(IEnumerable<ValidationResult> results)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        foreach (var result in results)
        {
            errors.AddRange(result.Errors);
            warnings.AddRange(result.Warnings);
        }

        return new ValidationResult
        {
            Errors = errors,
            Warnings = warnings
        };
    }
}
