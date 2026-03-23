using YamlDotNet.Serialization;

namespace Spectra.Core.Models.Grounding;

/// <summary>
/// DTO for grounding metadata YAML deserialization.
/// Use GroundingMetadata for business logic.
/// </summary>
public sealed class GroundingFrontmatter
{
    [YamlMember(Alias = "verdict")]
    public string? Verdict { get; set; }

    [YamlMember(Alias = "score")]
    public double Score { get; set; }

    [YamlMember(Alias = "generator")]
    public string? Generator { get; set; }

    [YamlMember(Alias = "critic")]
    public string? Critic { get; set; }

    [YamlMember(Alias = "verified_at")]
    public string? VerifiedAt { get; set; }

    [YamlMember(Alias = "unverified_claims")]
    public List<string> UnverifiedClaims { get; set; } = [];

    [YamlMember(Alias = "source")]
    public string? Source { get; set; }

    [YamlMember(Alias = "created_by")]
    public string? CreatedBy { get; set; }

    [YamlMember(Alias = "note")]
    public string? Note { get; set; }

    /// <summary>
    /// Converts the frontmatter DTO to a GroundingMetadata record.
    /// Returns null if required fields are missing.
    /// </summary>
    public GroundingMetadata? ToMetadata()
    {
        if (string.IsNullOrWhiteSpace(Verdict))
        {
            return null;
        }

        if (!TryParseVerdict(Verdict, out var verdict))
        {
            return null;
        }

        // Manual verdict uses placeholder values — no critic verification was performed
        if (verdict == VerificationVerdict.Manual)
        {
            return new GroundingMetadata
            {
                Verdict = VerificationVerdict.Manual,
                Score = 1.0,
                Generator = "user",
                Critic = "none",
                VerifiedAt = DateTimeOffset.UtcNow,
                UnverifiedClaims = []
            };
        }

        if (string.IsNullOrWhiteSpace(Generator) ||
            string.IsNullOrWhiteSpace(Critic))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(VerifiedAt, out var verifiedAt))
        {
            verifiedAt = DateTimeOffset.MinValue;
        }

        return new GroundingMetadata
        {
            Verdict = verdict,
            Score = Math.Clamp(Score, 0.0, 1.0),
            Generator = Generator,
            Critic = Critic,
            VerifiedAt = verifiedAt,
            UnverifiedClaims = UnverifiedClaims
        };
    }

    private static bool TryParseVerdict(string value, out VerificationVerdict verdict)
    {
        verdict = VerificationVerdict.Partial;

        return value.ToLowerInvariant() switch
        {
            "grounded" => SetAndReturn(VerificationVerdict.Grounded, ref verdict),
            "partial" => SetAndReturn(VerificationVerdict.Partial, ref verdict),
            "hallucinated" => SetAndReturn(VerificationVerdict.Hallucinated, ref verdict),
            "manual" => SetAndReturn(VerificationVerdict.Manual, ref verdict),
            _ => false
        };

        static bool SetAndReturn(VerificationVerdict v, ref VerificationVerdict target)
        {
            target = v;
            return true;
        }
    }
}
