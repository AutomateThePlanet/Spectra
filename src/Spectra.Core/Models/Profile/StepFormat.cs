namespace Spectra.Core.Models.Profile;

/// <summary>
/// Format for test steps.
/// </summary>
public enum StepFormat
{
    /// <summary>
    /// Bullet point format (- Step 1\n- Step 2).
    /// </summary>
    Bullets,

    /// <summary>
    /// Numbered format (1. Step 1\n2. Step 2).
    /// </summary>
    Numbered,

    /// <summary>
    /// Prose format.
    /// </summary>
    Paragraphs
}
