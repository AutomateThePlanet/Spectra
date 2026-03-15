namespace Spectra.Core.Models.Profile;

/// <summary>
/// The resolved profile after applying inheritance rules.
/// </summary>
public sealed class EffectiveProfile
{
    /// <summary>
    /// Gets or sets where this profile came from.
    /// </summary>
    public ProfileSource Source { get; init; } = ProfileSource.Default();

    /// <summary>
    /// Gets or sets the resolved profile options.
    /// </summary>
    public GenerationProfile Profile { get; init; } = new();

    /// <summary>
    /// Gets or sets the order of profiles applied.
    /// </summary>
    public IReadOnlyList<ProfileSource> InheritanceChain { get; init; } = [];

    /// <summary>
    /// Creates an effective profile from defaults.
    /// </summary>
    public static EffectiveProfile FromDefaults() => new()
    {
        Source = ProfileSource.Default(),
        Profile = new GenerationProfile(),
        InheritanceChain = [ProfileSource.Default()]
    };

    /// <summary>
    /// Creates an effective profile from a loaded profile.
    /// </summary>
    public static EffectiveProfile FromProfile(GenerationProfile profile, ProfileSource source)
    {
        return new EffectiveProfile
        {
            Source = source,
            Profile = profile,
            InheritanceChain = [source]
        };
    }

    /// <summary>
    /// Creates an effective profile from a merged suite and repository profile.
    /// </summary>
    public static EffectiveProfile FromMerge(
        GenerationProfile merged,
        ProfileSource suiteSource,
        ProfileSource repoSource)
    {
        return new EffectiveProfile
        {
            Source = suiteSource,
            Profile = merged,
            InheritanceChain = [suiteSource, repoSource]
        };
    }
}
