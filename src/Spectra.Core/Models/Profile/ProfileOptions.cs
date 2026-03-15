namespace Spectra.Core.Models.Profile;

/// <summary>
/// Container for all profile configuration options.
/// </summary>
public sealed class ProfileOptions
{
    /// <summary>
    /// Gets or sets the level of detail for test steps.
    /// </summary>
    public DetailLevel DetailLevel { get; init; } = DetailLevel.Detailed;

    /// <summary>
    /// Gets or sets the minimum negative test cases per feature.
    /// </summary>
    public int MinNegativeScenarios { get; init; } = 2;

    /// <summary>
    /// Gets or sets the default priority for generated tests.
    /// </summary>
    public Priority DefaultPriority { get; init; } = Priority.Medium;

    /// <summary>
    /// Gets or sets the formatting preferences.
    /// </summary>
    public FormattingOptions Formatting { get; init; } = new();

    /// <summary>
    /// Gets or sets the domain-specific settings.
    /// </summary>
    public DomainOptions Domain { get; init; } = new();

    /// <summary>
    /// Gets or sets categories of tests NOT to generate.
    /// </summary>
    public IReadOnlyList<string> Exclusions { get; init; } = [];
}
