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
    /// Test is kept on disk with a partial grounding block; the critic's condensed findings
    /// are embedded in the frontmatter. A bounded repair attempt is made automatically.
    /// </summary>
    Partial,

    /// <summary>
    /// Test contains invented behaviors that contradict the source documentation.
    /// The contradicting claim is recorded in the drop trail (.spectra/dropped-tests.json),
    /// then the test is deleted via the three-phase clean delete (index, depends_on, file).
    /// </summary>
    Hallucinated,

    /// <summary>
    /// Test was created from a user-described behavior, not from documentation.
    /// Skips critic verification entirely. Written to disk with manual grounding metadata.
    /// </summary>
    Manual
}
