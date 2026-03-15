using System.Text.RegularExpressions;
using Spectra.Core.Models.Profile;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.Core.Profile;

/// <summary>
/// Parses profile files from YAML frontmatter in Markdown.
/// </summary>
public sealed partial class ProfileParser
{
    private static readonly Regex FrontmatterRegex = FrontmatterPattern();

    private readonly IDeserializer _deserializer;

    public ProfileParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Parses a profile from Markdown content.
    /// </summary>
    public ProfileParseResult Parse(string content)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(content);

        var match = FrontmatterRegex.Match(content);
        if (!match.Success)
        {
            return ProfileParseResult.Failure(
                ValidationError.Create("MISSING_FRONTMATTER", "Profile must contain YAML frontmatter"));
        }

        var yaml = match.Groups["yaml"].Value;
        var body = match.Groups["body"].Value.Trim();

        try
        {
            var raw = _deserializer.Deserialize<ProfileYaml>(yaml);
            if (raw is null)
            {
                return ProfileParseResult.Failure(
                    ValidationError.Create("EMPTY_YAML", "YAML frontmatter is empty"));
            }

            var profile = MapToProfile(raw, body);
            return ProfileParseResult.Success(profile);
        }
        catch (YamlException ex)
        {
            return ProfileParseResult.Failure(
                ValidationError.Create("INVALID_YAML", ex.Message, (int)ex.Start.Line));
        }
    }

    /// <summary>
    /// Extracts just the body content (after frontmatter) from Markdown.
    /// </summary>
    public string? ExtractBody(string content)
    {
        var match = FrontmatterRegex.Match(content);
        return match.Success ? match.Groups["body"].Value.Trim() : null;
    }

    private static GenerationProfile MapToProfile(ProfileYaml raw, string body)
    {
        var description = raw.Description ?? (string.IsNullOrWhiteSpace(body) ? null : ExtractFirstParagraph(body));

        return new GenerationProfile
        {
            ProfileVersion = raw.ProfileVersion ?? ProfileDefaults.ProfileVersion,
            CreatedAt = raw.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = raw.UpdatedAt ?? DateTime.UtcNow,
            Description = description,
            Options = MapOptions(raw.Options)
        };
    }

    private static ProfileOptions MapOptions(ProfileOptionsYaml? raw)
    {
        if (raw is null) return ProfileDefaults.CreateDefaultOptions();

        return new ProfileOptions
        {
            DetailLevel = ParseDetailLevel(raw.DetailLevel),
            MinNegativeScenarios = raw.MinNegativeScenarios ?? ProfileDefaults.MinNegativeScenarios,
            DefaultPriority = ParsePriority(raw.DefaultPriority),
            Formatting = MapFormatting(raw.Formatting),
            Domain = MapDomain(raw.Domain),
            Exclusions = raw.Exclusions ?? []
        };
    }

    private static FormattingOptions MapFormatting(FormattingOptionsYaml? raw)
    {
        if (raw is null) return ProfileDefaults.CreateDefaultFormatting();

        return new FormattingOptions
        {
            StepFormat = ParseStepFormat(raw.StepFormat),
            UseActionVerbs = raw.UseActionVerbs ?? ProfileDefaults.UseActionVerbs,
            IncludeScreenshots = raw.IncludeScreenshots ?? ProfileDefaults.IncludeScreenshots,
            MaxStepsPerTest = raw.MaxStepsPerTest
        };
    }

    private static DomainOptions MapDomain(DomainOptionsYaml? raw)
    {
        if (raw is null) return ProfileDefaults.CreateDefaultDomain();

        return new DomainOptions
        {
            Domains = ParseDomainTypes(raw.Domains),
            PiiSensitivity = ParsePiiSensitivity(raw.PiiSensitivity),
            IncludeComplianceNotes = raw.IncludeComplianceNotes ?? ProfileDefaults.IncludeComplianceNotes
        };
    }

    private static DetailLevel ParseDetailLevel(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "high_level" => DetailLevel.HighLevel,
            "highlevel" => DetailLevel.HighLevel,
            "detailed" => DetailLevel.Detailed,
            "very_detailed" => DetailLevel.VeryDetailed,
            "verydetailed" => DetailLevel.VeryDetailed,
            _ => ProfileDefaults.DetailLevel
        };
    }

    private static Priority ParsePriority(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "high" => Priority.High,
            "medium" => Priority.Medium,
            "low" => Priority.Low,
            _ => ProfileDefaults.DefaultPriority
        };
    }

    private static StepFormat ParseStepFormat(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "bullets" => StepFormat.Bullets,
            "numbered" => StepFormat.Numbered,
            "paragraphs" => StepFormat.Paragraphs,
            _ => ProfileDefaults.StepFormat
        };
    }

    private static PiiSensitivity ParsePiiSensitivity(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "none" => PiiSensitivity.None,
            "standard" => PiiSensitivity.Standard,
            "strict" => PiiSensitivity.Strict,
            _ => ProfileDefaults.PiiSensitivity
        };
    }

    private static IReadOnlyList<DomainType> ParseDomainTypes(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0) return [];

        var result = new List<DomainType>();
        foreach (var value in values)
        {
            var parsed = value.ToLowerInvariant() switch
            {
                "payments" => (DomainType?)DomainType.Payments,
                "authentication" => DomainType.Authentication,
                "pii_gdpr" => DomainType.PiiGdpr,
                "piigdpr" => DomainType.PiiGdpr,
                "healthcare" => DomainType.Healthcare,
                "financial" => DomainType.Financial,
                "accessibility" => DomainType.Accessibility,
                _ => null
            };
            if (parsed.HasValue)
            {
                result.Add(parsed.Value);
            }
        }
        return result;
    }

    private static string? ExtractFirstParagraph(string body)
    {
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith('#'))
            {
                return trimmed;
            }
        }
        return null;
    }

    [GeneratedRegex(@"^---\s*\n(?<yaml>.*?)\n---\s*\n?(?<body>.*)?$", RegexOptions.Singleline)]
    private static partial Regex FrontmatterPattern();

    // YAML deserialization models
    private sealed class ProfileYaml
    {
        public int? ProfileVersion { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Description { get; set; }
        public ProfileOptionsYaml? Options { get; set; }
    }

    private sealed class ProfileOptionsYaml
    {
        public string? DetailLevel { get; set; }
        public int? MinNegativeScenarios { get; set; }
        public string? DefaultPriority { get; set; }
        public FormattingOptionsYaml? Formatting { get; set; }
        public DomainOptionsYaml? Domain { get; set; }
        public List<string>? Exclusions { get; set; }
    }

    private sealed class FormattingOptionsYaml
    {
        public string? StepFormat { get; set; }
        public bool? UseActionVerbs { get; set; }
        public bool? IncludeScreenshots { get; set; }
        public int? MaxStepsPerTest { get; set; }
    }

    private sealed class DomainOptionsYaml
    {
        public List<string>? Domains { get; set; }
        public string? PiiSensitivity { get; set; }
        public bool? IncludeComplianceNotes { get; set; }
    }
}

/// <summary>
/// Result of parsing a profile.
/// </summary>
public sealed class ProfileParseResult
{
    public bool IsSuccess => Error is null;
    public GenerationProfile? Profile { get; private init; }
    public ValidationError? Error { get; private init; }

    public static ProfileParseResult Success(GenerationProfile profile) => new() { Profile = profile };
    public static ProfileParseResult Failure(ValidationError error) => new() { Error = error };
}
