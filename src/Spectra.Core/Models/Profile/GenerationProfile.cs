namespace Spectra.Core.Models.Profile;

/// <summary>
/// The root entity representing a complete test generation profile.
/// </summary>
public sealed class GenerationProfile
{
    /// <summary>
    /// Gets or sets the profile format version.
    /// </summary>
    public int ProfileVersion { get; init; } = 1;

    /// <summary>
    /// Gets or sets when the profile was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the profile was last modified.
    /// </summary>
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets a human-readable description of the profile's purpose.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets all configuration options.
    /// </summary>
    public ProfileOptions Options { get; init; } = new();
}
