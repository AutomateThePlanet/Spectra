namespace Spectra.Core.Models.Grounding;

/// <summary>
/// Condensed critic finding embedded in test frontmatter — element + one-line reason.
/// Full findings (claim, evidence, status) are in the per-test verdict JSON file.
/// </summary>
public sealed record CondensedFinding
{
    /// <summary>
    /// The test element this finding applies to (e.g., "Step 3", "Expected Result").
    /// </summary>
    public required string Element { get; init; }

    /// <summary>
    /// One-line reason the claim could not be grounded.
    /// </summary>
    public required string Reason { get; init; }
}
