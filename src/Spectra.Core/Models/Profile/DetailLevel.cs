namespace Spectra.Core.Models.Profile;

/// <summary>
/// Level of detail for generated test steps.
/// </summary>
public enum DetailLevel
{
    /// <summary>
    /// Brief steps, assumes tester knowledge.
    /// </summary>
    HighLevel,

    /// <summary>
    /// Comprehensive steps with expected results.
    /// </summary>
    Detailed,

    /// <summary>
    /// Granular steps, no assumptions.
    /// </summary>
    VeryDetailed
}
