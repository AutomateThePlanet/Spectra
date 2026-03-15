namespace Spectra.Core.Models.Profile;

/// <summary>
/// A non-critical validation warning.
/// </summary>
public sealed class ValidationWarning
{
    /// <summary>
    /// Gets or sets the warning code (e.g., "UNKNOWN_OPTION").
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable warning description.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the field path if applicable.
    /// </summary>
    public string? Field { get; init; }

    /// <summary>
    /// Gets or sets the default value being used.
    /// </summary>
    public string? DefaultUsed { get; init; }

    /// <summary>
    /// Creates a new validation warning.
    /// </summary>
    public static ValidationWarning Create(string code, string message, string? field = null, string? defaultUsed = null) => new()
    {
        Code = code,
        Message = message,
        Field = field,
        DefaultUsed = defaultUsed
    };
}
