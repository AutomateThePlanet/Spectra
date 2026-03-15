using System.Text;
using Spectra.Core.Models.Profile;

namespace Spectra.Core.Profile;

/// <summary>
/// Converts profile settings to AI prompt context.
/// </summary>
public sealed class ProfileContextBuilder
{
    /// <summary>
    /// Builds an AI context prompt section from an effective profile.
    /// </summary>
    public string Build(EffectiveProfile effectiveProfile)
    {
        ArgumentNullException.ThrowIfNull(effectiveProfile);

        var profile = effectiveProfile.Profile;
        var sb = new StringBuilder();

        sb.AppendLine("## Test Generation Profile");
        sb.AppendLine();
        sb.AppendLine("Generate tests following these preferences:");
        sb.AppendLine();

        // Detail level
        AppendDetailLevel(sb, profile.Options.DetailLevel);

        // Formatting
        AppendFormatting(sb, profile.Options.Formatting);

        // Domain considerations
        AppendDomain(sb, profile.Options.Domain);

        // Test case requirements
        AppendRequirements(sb, profile.Options);

        // Exclusions
        AppendExclusions(sb, profile.Options.Exclusions);

        return sb.ToString();
    }

    /// <summary>
    /// Builds a minimal context for profiles with mostly defaults.
    /// </summary>
    public string BuildMinimal(EffectiveProfile effectiveProfile)
    {
        ArgumentNullException.ThrowIfNull(effectiveProfile);

        if (effectiveProfile.Source.Type == SourceType.Default)
        {
            return string.Empty;
        }

        var profile = effectiveProfile.Profile;
        var sb = new StringBuilder();

        sb.AppendLine("## Profile");
        sb.AppendLine($"- Detail: {FormatDetailLevel(profile.Options.DetailLevel)}");
        sb.AppendLine($"- Priority: {FormatPriority(profile.Options.DefaultPriority)}");
        sb.AppendLine($"- Format: {FormatStepFormat(profile.Options.Formatting.StepFormat)}");

        if (profile.Options.Exclusions.Count > 0)
        {
            sb.AppendLine($"- Exclude: {string.Join(", ", profile.Options.Exclusions)}");
        }

        return sb.ToString();
    }

    private static void AppendDetailLevel(StringBuilder sb, DetailLevel level)
    {
        sb.AppendLine($"**Detail Level**: {FormatDetailLevel(level)}");

        switch (level)
        {
            case DetailLevel.HighLevel:
                sb.AppendLine("- Keep steps brief and high-level");
                sb.AppendLine("- Assume tester has system knowledge");
                sb.AppendLine("- Focus on key actions and outcomes");
                break;

            case DetailLevel.Detailed:
                sb.AppendLine("- Include comprehensive step-by-step instructions");
                sb.AppendLine("- Specify expected results for each action");
                sb.AppendLine("- Include relevant UI elements and data");
                break;

            case DetailLevel.VeryDetailed:
                sb.AppendLine("- Include granular step-by-step instructions");
                sb.AppendLine("- Assume no prior knowledge of the system");
                sb.AppendLine("- Specify exact UI elements, values, and expected results");
                sb.AppendLine("- Include navigation paths and preconditions");
                break;
        }

        sb.AppendLine();
    }

    private static void AppendFormatting(StringBuilder sb, FormattingOptions formatting)
    {
        sb.AppendLine("**Formatting**:");

        var formatDesc = formatting.StepFormat switch
        {
            StepFormat.Numbered => "numbered steps (1. 2. 3.)",
            StepFormat.Bullets => "bullet points (- - -)",
            StepFormat.Paragraphs => "prose paragraphs",
            _ => "numbered steps"
        };
        sb.AppendLine($"- Use {formatDesc}");

        if (formatting.UseActionVerbs)
        {
            sb.AppendLine("- Start each step with an action verb (Click, Enter, Verify, Navigate)");
        }

        if (formatting.IncludeScreenshots)
        {
            sb.AppendLine("- Include screenshot capture suggestions at key points");
        }

        if (formatting.MaxStepsPerTest.HasValue)
        {
            sb.AppendLine($"- Maximum {formatting.MaxStepsPerTest} steps per test case");
        }

        sb.AppendLine();
    }

    private static void AppendDomain(StringBuilder sb, DomainOptions domain)
    {
        if (domain.Domains.Count == 0 &&
            domain.PiiSensitivity == PiiSensitivity.None &&
            !domain.IncludeComplianceNotes)
        {
            return;
        }

        sb.AppendLine("**Domain Considerations**:");

        foreach (var domainType in domain.Domains)
        {
            var consideration = domainType switch
            {
                DomainType.Payments => "Payments: Include PCI DSS compliance checks, card validation, transaction security",
                DomainType.Authentication => "Authentication: Test MFA flows, session management, credential handling",
                DomainType.PiiGdpr => "PII/GDPR: Consider data protection, consent mechanisms, deletion rights",
                DomainType.Healthcare => "Healthcare: Include HIPAA compliance, PHI handling, audit requirements",
                DomainType.Financial => "Financial: Test audit trails, SOX compliance, regulatory reporting",
                DomainType.Accessibility => "Accessibility: Test WCAG compliance, screen reader compatibility, keyboard navigation",
                _ => null
            };

            if (consideration is not null)
            {
                sb.AppendLine($"- {consideration}");
            }
        }

        if (domain.PiiSensitivity != PiiSensitivity.None)
        {
            var sensitivityDesc = domain.PiiSensitivity switch
            {
                PiiSensitivity.Standard => "Apply standard PII protection awareness",
                PiiSensitivity.Strict => "Apply strict GDPR/privacy compliance focus",
                _ => null
            };

            if (sensitivityDesc is not null)
            {
                sb.AppendLine($"- {sensitivityDesc}");
            }
        }

        if (domain.IncludeComplianceNotes)
        {
            sb.AppendLine("- Add compliance reminders to relevant test steps");
        }

        sb.AppendLine();
    }

    private static void AppendRequirements(StringBuilder sb, ProfileOptions options)
    {
        sb.AppendLine("**Test Case Requirements**:");
        sb.AppendLine($"- Default priority: {FormatPriority(options.DefaultPriority)}");
        sb.AppendLine($"- Minimum {options.MinNegativeScenarios} negative scenarios per feature");
        sb.AppendLine();
    }

    private static void AppendExclusions(StringBuilder sb, IReadOnlyList<string> exclusions)
    {
        if (exclusions.Count == 0)
        {
            return;
        }

        sb.AppendLine("**Exclusions** (do NOT generate):");

        foreach (var exclusion in exclusions)
        {
            var description = exclusion switch
            {
                "performance" => "Performance tests",
                "load_testing" => "Load testing scenarios",
                "security" => "Security-focused tests",
                "accessibility" => "Accessibility tests",
                "mobile_specific" => "Mobile-specific tests",
                "api_only" => "API-only tests",
                "ui_only" => "UI-only tests",
                "edge_cases" => "Unusual edge case scenarios",
                "negative" => "Negative/error scenarios",
                "localization" => "Localization tests",
                _ => exclusion
            };

            sb.AppendLine($"- {description}");
        }

        sb.AppendLine();
    }

    private static string FormatDetailLevel(DetailLevel level) => level switch
    {
        DetailLevel.HighLevel => "High-Level",
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
}
