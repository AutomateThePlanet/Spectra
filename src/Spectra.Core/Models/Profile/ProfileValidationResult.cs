namespace Spectra.Core.Models.Profile;

/// <summary>
/// Result of validating a profile file.
/// </summary>
public sealed class ProfileValidationResult
{
    /// <summary>
    /// Gets whether the profile passed validation.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets or sets the critical errors that block usage.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];

    /// <summary>
    /// Gets or sets the non-critical issues.
    /// </summary>
    public IReadOnlyList<ValidationWarning> Warnings { get; init; } = [];

    /// <summary>
    /// Gets or sets the parsed profile if valid.
    /// </summary>
    public GenerationProfile? Profile { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ProfileValidationResult Success(GenerationProfile profile, IReadOnlyList<ValidationWarning>? warnings = null) => new()
    {
        Profile = profile,
        Errors = [],
        Warnings = warnings ?? []
    };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ProfileValidationResult Failure(IReadOnlyList<ValidationError> errors, IReadOnlyList<ValidationWarning>? warnings = null) => new()
    {
        Profile = null,
        Errors = errors,
        Warnings = warnings ?? []
    };

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static ProfileValidationResult Failure(ValidationError error) => Failure([error]);
}
