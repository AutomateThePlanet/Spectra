namespace Spectra.Core.Models.Profile;

/// <summary>
/// A critical validation error.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// Gets or sets the error code (e.g., "INVALID_YAML").
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable error description.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the line number if applicable.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Gets or sets the field path if applicable.
    /// </summary>
    public string? Field { get; init; }

    /// <summary>
    /// Creates a new validation error.
    /// </summary>
    public static ValidationError Create(string code, string message, int? line = null, string? field = null) => new()
    {
        Code = code,
        Message = message,
        Line = line,
        Field = field
    };
}
