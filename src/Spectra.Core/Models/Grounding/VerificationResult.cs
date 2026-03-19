namespace Spectra.Core.Models.Grounding;

/// <summary>
/// Complete verification result for a test case.
/// </summary>
public sealed class VerificationResult
{
    /// <summary>
    /// Overall verdict for the test.
    /// </summary>
    public required VerificationVerdict Verdict { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0).
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Individual findings for each claim in the test.
    /// </summary>
    public required IReadOnlyList<CriticFinding> Findings { get; init; }

    /// <summary>
    /// Model that performed the verification.
    /// </summary>
    public required string CriticModel { get; init; }

    /// <summary>
    /// Time taken for verification.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Any errors encountered during verification.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Whether verification completed successfully.
    /// </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>
    /// Convert to grounding metadata for frontmatter.
    /// </summary>
    public GroundingMetadata ToMetadata(string generatorModel) => new()
    {
        Verdict = Verdict,
        Score = Score,
        Generator = generatorModel,
        Critic = CriticModel,
        VerifiedAt = DateTimeOffset.UtcNow,
        UnverifiedClaims = Findings
            .Where(f => f.Status != FindingStatus.Grounded)
            .Select(f => $"{f.Element}: {f.Reason ?? f.Claim}")
            .ToList()
    };

    /// <summary>
    /// Creates a result for when verification could not be performed.
    /// </summary>
    public static VerificationResult Unverified(string criticModel, string reason) => new()
    {
        Verdict = VerificationVerdict.Partial,
        Score = 0.0,
        Findings = [],
        CriticModel = criticModel,
        Errors = [reason]
    };
}
