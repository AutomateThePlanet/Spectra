namespace Spectra.Core.Models.Profile;

/// <summary>
/// Identifies the origin of a profile.
/// </summary>
public sealed class ProfileSource
{
    /// <summary>
    /// Gets or sets the source type (repository, suite, or default).
    /// </summary>
    public SourceType Type { get; init; }

    /// <summary>
    /// Gets or sets the file path to the profile.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the file exists.
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    /// Creates a default profile source.
    /// </summary>
    public static ProfileSource Default() => new()
    {
        Type = SourceType.Default,
        Path = string.Empty,
        Exists = false
    };

    /// <summary>
    /// Creates a repository profile source.
    /// </summary>
    public static ProfileSource Repository(string path, bool exists = true) => new()
    {
        Type = SourceType.Repository,
        Path = path,
        Exists = exists
    };

    /// <summary>
    /// Creates a suite profile source.
    /// </summary>
    public static ProfileSource Suite(string path, bool exists = true) => new()
    {
        Type = SourceType.Suite,
        Path = path,
        Exists = exists
    };
}
