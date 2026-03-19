namespace Spectra.Core.Models.Grounding;

/// <summary>
/// Status of an individual finding from the critic.
/// </summary>
public enum FindingStatus
{
    /// <summary>
    /// Claim can be traced to documentation.
    /// </summary>
    Grounded,

    /// <summary>
    /// Claim cannot be verified but isn't clearly wrong.
    /// </summary>
    Unverified,

    /// <summary>
    /// Claim contradicts or invents beyond documentation.
    /// </summary>
    Hallucinated
}
