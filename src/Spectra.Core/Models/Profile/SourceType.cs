namespace Spectra.Core.Models.Profile;

/// <summary>
/// Identifies the type of profile source.
/// </summary>
public enum SourceType
{
    /// <summary>
    /// spectra.profile.md at repo root.
    /// </summary>
    Repository,

    /// <summary>
    /// _profile.md in suite folder.
    /// </summary>
    Suite,

    /// <summary>
    /// Built-in defaults (no file).
    /// </summary>
    Default
}
