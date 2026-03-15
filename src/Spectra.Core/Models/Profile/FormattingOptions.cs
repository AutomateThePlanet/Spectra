namespace Spectra.Core.Models.Profile;

/// <summary>
/// Preferences for how test content is formatted.
/// </summary>
public sealed class FormattingOptions
{
    /// <summary>
    /// Gets or sets the step format (bullets, numbered, paragraphs).
    /// </summary>
    public StepFormat StepFormat { get; init; } = StepFormat.Numbered;

    /// <summary>
    /// Gets or sets whether to start steps with action verbs.
    /// </summary>
    public bool UseActionVerbs { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to suggest screenshot capture points.
    /// </summary>
    public bool IncludeScreenshots { get; init; }

    /// <summary>
    /// Gets or sets the maximum steps per test case (null = unlimited).
    /// </summary>
    public int? MaxStepsPerTest { get; init; }
}
