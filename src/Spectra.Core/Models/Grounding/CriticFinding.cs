namespace Spectra.Core.Models.Grounding;

/// <summary>
/// Assessment of a single claim within a test case.
/// </summary>
public sealed record CriticFinding
{
    /// <summary>
    /// What part of the test this finding applies to.
    /// Examples: "Step 3", "Expected Result", "Precondition"
    /// </summary>
    public required string Element { get; init; }

    /// <summary>
    /// The specific claim being checked.
    /// </summary>
    public required string Claim { get; init; }

    /// <summary>
    /// Status of this claim: grounded, unverified, or hallucinated.
    /// </summary>
    public required FindingStatus Status { get; init; }

    /// <summary>
    /// Quote or reference from documentation supporting the claim.
    /// Null if unverified or hallucinated.
    /// </summary>
    public string? Evidence { get; init; }

    /// <summary>
    /// Reason why the claim is unverified or hallucinated.
    /// Null if grounded.
    /// </summary>
    public string? Reason { get; init; }
}
