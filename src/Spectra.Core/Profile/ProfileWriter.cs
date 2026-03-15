using System.Text;
using Spectra.Core.Models.Profile;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.Core.Profile;

/// <summary>
/// Writes profile to Markdown file with YAML frontmatter.
/// </summary>
public sealed class ProfileWriter
{
    private readonly ISerializer _serializer;

    public ProfileWriter()
    {
        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
    }

    /// <summary>
    /// Formats a profile as Markdown with YAML frontmatter.
    /// </summary>
    public string Format(GenerationProfile profile, string? bodyContent = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var yaml = FormatYaml(profile);
        var sb = new StringBuilder();

        sb.AppendLine("---");
        sb.Append(yaml);
        sb.AppendLine("---");

        if (!string.IsNullOrWhiteSpace(bodyContent))
        {
            sb.AppendLine();
            sb.AppendLine(bodyContent);
        }
        else if (!string.IsNullOrWhiteSpace(profile.Description))
        {
            sb.AppendLine();
            sb.AppendLine($"# Test Generation Profile");
            sb.AppendLine();
            sb.AppendLine(profile.Description);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes a profile to a file.
    /// </summary>
    public async Task WriteAsync(string path, GenerationProfile profile, string? bodyContent = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(profile);

        var content = Format(profile, bodyContent);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, ct);
    }

    private string FormatYaml(GenerationProfile profile)
    {
        var yaml = new ProfileYaml
        {
            ProfileVersion = profile.ProfileVersion,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            Description = profile.Description,
            Options = MapOptions(profile.Options)
        };

        return _serializer.Serialize(yaml);
    }

    private static ProfileOptionsYaml MapOptions(ProfileOptions options)
    {
        return new ProfileOptionsYaml
        {
            DetailLevel = FormatDetailLevel(options.DetailLevel),
            MinNegativeScenarios = options.MinNegativeScenarios,
            DefaultPriority = FormatPriority(options.DefaultPriority),
            Formatting = MapFormatting(options.Formatting),
            Domain = MapDomain(options.Domain),
            Exclusions = options.Exclusions.Count > 0 ? options.Exclusions.ToList() : null
        };
    }

    private static FormattingOptionsYaml MapFormatting(FormattingOptions formatting)
    {
        return new FormattingOptionsYaml
        {
            StepFormat = FormatStepFormat(formatting.StepFormat),
            UseActionVerbs = formatting.UseActionVerbs,
            IncludeScreenshots = formatting.IncludeScreenshots,
            MaxStepsPerTest = formatting.MaxStepsPerTest
        };
    }

    private static DomainOptionsYaml? MapDomain(DomainOptions domain)
    {
        if (domain.Domains.Count == 0 &&
            domain.PiiSensitivity == PiiSensitivity.None &&
            !domain.IncludeComplianceNotes)
        {
            return null;
        }

        return new DomainOptionsYaml
        {
            Domains = domain.Domains.Count > 0
                ? domain.Domains.Select(FormatDomainType).ToList()
                : null,
            PiiSensitivity = FormatPiiSensitivity(domain.PiiSensitivity),
            IncludeComplianceNotes = domain.IncludeComplianceNotes ? true : null
        };
    }

    private static string FormatDetailLevel(DetailLevel level) => level switch
    {
        DetailLevel.HighLevel => "high_level",
        DetailLevel.Detailed => "detailed",
        DetailLevel.VeryDetailed => "very_detailed",
        _ => "detailed"
    };

    private static string FormatPriority(Priority priority) => priority switch
    {
        Priority.High => "high",
        Priority.Medium => "medium",
        Priority.Low => "low",
        _ => "medium"
    };

    private static string FormatStepFormat(StepFormat format) => format switch
    {
        StepFormat.Bullets => "bullets",
        StepFormat.Numbered => "numbered",
        StepFormat.Paragraphs => "paragraphs",
        _ => "numbered"
    };

    private static string FormatPiiSensitivity(PiiSensitivity sensitivity) => sensitivity switch
    {
        PiiSensitivity.None => "none",
        PiiSensitivity.Standard => "standard",
        PiiSensitivity.Strict => "strict",
        _ => "none"
    };

    private static string FormatDomainType(DomainType domain) => domain switch
    {
        DomainType.Payments => "payments",
        DomainType.Authentication => "authentication",
        DomainType.PiiGdpr => "pii_gdpr",
        DomainType.Healthcare => "healthcare",
        DomainType.Financial => "financial",
        DomainType.Accessibility => "accessibility",
        _ => domain.ToString().ToLowerInvariant()
    };

    // YAML serialization models
    private sealed class ProfileYaml
    {
        public int ProfileVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? Description { get; set; }
        public ProfileOptionsYaml? Options { get; set; }
    }

    private sealed class ProfileOptionsYaml
    {
        public string? DetailLevel { get; set; }
        public int MinNegativeScenarios { get; set; }
        public string? DefaultPriority { get; set; }
        public FormattingOptionsYaml? Formatting { get; set; }
        public DomainOptionsYaml? Domain { get; set; }
        public List<string>? Exclusions { get; set; }
    }

    private sealed class FormattingOptionsYaml
    {
        public string? StepFormat { get; set; }
        public bool UseActionVerbs { get; set; }
        public bool IncludeScreenshots { get; set; }
        public int? MaxStepsPerTest { get; set; }
    }

    private sealed class DomainOptionsYaml
    {
        public List<string>? Domains { get; set; }
        public string? PiiSensitivity { get; set; }
        public bool? IncludeComplianceNotes { get; set; }
    }
}
