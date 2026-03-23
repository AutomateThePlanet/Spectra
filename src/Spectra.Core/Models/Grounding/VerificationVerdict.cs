namespace Spectra.Core.Models.Grounding;

/// <summary>
/// Verdict assigned by the critic model after verification.
/// </summary>
public enum VerificationVerdict
{
    /// <summary>
    /// All claims in the test are traceable to documentation.
    /// Test is written to disk as-is with grounding metadata.
    /// </summary>
    Grounded,

    /// <summary>
    /// Some claims are verified but others are unverified assumptions.
    /// Test is written with warning marker and unverified_claims list.
    /// </summary>
    Partial,

    /// <summary>
    /// Test contains invented behaviors or undocumented claims.
    /// Test is rejected and NOT written to disk.
    /// </summary>
    Hallucinated,

    /// <summary>
    /// Test was created from a user-described behavior, not from documentation.
    /// Skips critic verification entirely. Written to disk with manual grounding metadata.
    /// </summary>
    Manual
}
