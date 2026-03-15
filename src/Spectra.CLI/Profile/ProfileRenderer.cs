using System.Text;
using Spectra.Core.Models.Profile;

namespace Spectra.CLI.Profile;

/// <summary>
/// Renders profile information for console display.
/// </summary>
public sealed class ProfileRenderer
{
    /// <summary>
    /// Formats a profile for display.
    /// </summary>
    public string Format(EffectiveProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("=== SPECTRA Test Generation Profile ===");
        sb.AppendLine();

        // Source information
        sb.AppendLine($"Source: {FormatSourceType(profile.Source.Type)}");
        if (profile.Source.Exists && !string.IsNullOrEmpty(profile.Source.Path))
        {
            sb.AppendLine($"Path: {profile.Source.Path}");
        }
        sb.AppendLine();

        // Profile details
        var p = profile.Profile;
        if (!string.IsNullOrEmpty(p.Description))
        {
            sb.AppendLine($"Description: {p.Description}");
            sb.AppendLine();
        }

        // Options
        sb.AppendLine("--- Options ---");
        sb.AppendLine();

        sb.AppendLine($"Detail Level: {FormatDetailLevel(p.Options.DetailLevel)}");
        sb.AppendLine($"Min Negative Scenarios: {p.Options.MinNegativeScenarios}");
        sb.AppendLine($"Default Priority: {FormatPriority(p.Options.DefaultPriority)}");
        sb.AppendLine();

        // Formatting
        sb.AppendLine("Formatting:");
        sb.AppendLine($"  Step Format: {FormatStepFormat(p.Options.Formatting.StepFormat)}");
        sb.AppendLine($"  Use Action Verbs: {(p.Options.Formatting.UseActionVerbs ? "Yes" : "No")}");
        sb.AppendLine($"  Include Screenshots: {(p.Options.Formatting.IncludeScreenshots ? "Yes" : "No")}");
        if (p.Options.Formatting.MaxStepsPerTest.HasValue)
        {
            sb.AppendLine($"  Max Steps Per Test: {p.Options.Formatting.MaxStepsPerTest}");
        }
        sb.AppendLine();

        // Domain
        if (p.Options.Domain.Domains.Count > 0 ||
            p.Options.Domain.PiiSensitivity != PiiSensitivity.None ||
            p.Options.Domain.IncludeComplianceNotes)
        {
            sb.AppendLine("Domain:");
            if (p.Options.Domain.Domains.Count > 0)
            {
                sb.AppendLine($"  Domains: {string.Join(", ", p.Options.Domain.Domains.Select(FormatDomainType))}");
            }
            sb.AppendLine($"  PII Sensitivity: {FormatPiiSensitivity(p.Options.Domain.PiiSensitivity)}");
            sb.AppendLine($"  Include Compliance Notes: {(p.Options.Domain.IncludeComplianceNotes ? "Yes" : "No")}");
            sb.AppendLine();
        }

        // Exclusions
        if (p.Options.Exclusions.Count > 0)
        {
            sb.AppendLine($"Exclusions: {string.Join(", ", p.Options.Exclusions)}");
            sb.AppendLine();
        }

        // Inheritance chain
        if (profile.InheritanceChain.Count > 1)
        {
            sb.AppendLine("--- Inheritance ---");
            foreach (var source in profile.InheritanceChain)
            {
                var marker = source == profile.Source ? " (active)" : "";
                sb.AppendLine($"  {FormatSourceType(source.Type)}: {source.Path}{marker}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a brief summary of the profile.
    /// </summary>
    public string FormatSummary(EffectiveProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var p = profile.Profile;
        return $"Profile: {FormatSourceType(profile.Source.Type)} | " +
               $"Detail: {FormatDetailLevel(p.Options.DetailLevel)} | " +
               $"Priority: {FormatPriority(p.Options.DefaultPriority)} | " +
               $"Format: {FormatStepFormat(p.Options.Formatting.StepFormat)}";
    }

    /// <summary>
    /// Formats the "no profile" message.
    /// </summary>
    public string FormatNoProfile()
    {
        return """
            === SPECTRA Test Generation Profile ===

            No profile configured.

            Run 'spectra init-profile' to create a profile with your team's preferences.
            Without a profile, test generation will use default settings.
            """;
    }

    private static string FormatSourceType(SourceType type) => type switch
    {
        SourceType.Repository => "Repository",
        SourceType.Suite => "Suite Override",
        SourceType.Default => "Default (no file)",
        _ => type.ToString()
    };

    private static string FormatDetailLevel(DetailLevel level) => level switch
    {
        DetailLevel.HighLevel => "High-level",
        DetailLevel.Detailed => "Detailed",
        DetailLevel.VeryDetailed => "Very Detailed",
        _ => level.ToString()
    };

    private static string FormatPriority(Priority priority) => priority switch
    {
        Priority.High => "High (P1)",
        Priority.Medium => "Medium (P2)",
        Priority.Low => "Low (P3)",
        _ => priority.ToString()
    };

    private static string FormatStepFormat(StepFormat format) => format switch
    {
        StepFormat.Bullets => "Bullets",
        StepFormat.Numbered => "Numbered",
        StepFormat.Paragraphs => "Paragraphs",
        _ => format.ToString()
    };

    private static string FormatPiiSensitivity(PiiSensitivity sensitivity) => sensitivity switch
    {
        PiiSensitivity.None => "None",
        PiiSensitivity.Standard => "Standard",
        PiiSensitivity.Strict => "Strict",
        _ => sensitivity.ToString()
    };

    private static string FormatDomainType(DomainType domain) => domain switch
    {
        DomainType.Payments => "Payments",
        DomainType.Authentication => "Authentication",
        DomainType.PiiGdpr => "PII/GDPR",
        DomainType.Healthcare => "Healthcare",
        DomainType.Financial => "Financial",
        DomainType.Accessibility => "Accessibility",
        _ => domain.ToString()
    };
}
