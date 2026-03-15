namespace Spectra.Core.Models.Profile;

/// <summary>
/// Default values for profile options.
/// </summary>
public static class ProfileDefaults
{
    /// <summary>
    /// Default detail level.
    /// </summary>
    public const DetailLevel DetailLevel = Profile.DetailLevel.Detailed;

    /// <summary>
    /// Default minimum negative scenarios.
    /// </summary>
    public const int MinNegativeScenarios = 2;

    /// <summary>
    /// Default priority.
    /// </summary>
    public const Priority DefaultPriority = Profile.Priority.Medium;

    /// <summary>
    /// Default step format.
    /// </summary>
    public const StepFormat StepFormat = Profile.StepFormat.Numbered;

    /// <summary>
    /// Default use action verbs setting.
    /// </summary>
    public const bool UseActionVerbs = true;

    /// <summary>
    /// Default include screenshots setting.
    /// </summary>
    public const bool IncludeScreenshots = false;

    /// <summary>
    /// Default PII sensitivity.
    /// </summary>
    public const PiiSensitivity PiiSensitivity = Profile.PiiSensitivity.None;

    /// <summary>
    /// Default include compliance notes setting.
    /// </summary>
    public const bool IncludeComplianceNotes = false;

    /// <summary>
    /// Current profile version.
    /// </summary>
    public const int ProfileVersion = 1;

    /// <summary>
    /// Repository profile file name.
    /// </summary>
    public const string RepositoryProfileFileName = "spectra.profile.md";

    /// <summary>
    /// Suite profile file name.
    /// </summary>
    public const string SuiteProfileFileName = "_profile.md";

    /// <summary>
    /// Maximum allowed exclusions.
    /// </summary>
    public const int MaxExclusions = 20;

    /// <summary>
    /// Maximum allowed steps per test.
    /// </summary>
    public const int MaxStepsLimit = 50;

    /// <summary>
    /// Maximum allowed negative scenarios.
    /// </summary>
    public const int MaxNegativeScenarios = 20;

    /// <summary>
    /// Creates a default GenerationProfile.
    /// </summary>
    public static GenerationProfile CreateDefaultProfile() => new()
    {
        ProfileVersion = ProfileVersion,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Description = null,
        Options = CreateDefaultOptions()
    };

    /// <summary>
    /// Creates default ProfileOptions.
    /// </summary>
    public static ProfileOptions CreateDefaultOptions() => new()
    {
        DetailLevel = DetailLevel,
        MinNegativeScenarios = MinNegativeScenarios,
        DefaultPriority = DefaultPriority,
        Formatting = CreateDefaultFormatting(),
        Domain = CreateDefaultDomain(),
        Exclusions = []
    };

    /// <summary>
    /// Creates default FormattingOptions.
    /// </summary>
    public static FormattingOptions CreateDefaultFormatting() => new()
    {
        StepFormat = StepFormat,
        UseActionVerbs = UseActionVerbs,
        IncludeScreenshots = IncludeScreenshots,
        MaxStepsPerTest = null
    };

    /// <summary>
    /// Creates default DomainOptions.
    /// </summary>
    public static DomainOptions CreateDefaultDomain() => new()
    {
        Domains = [],
        PiiSensitivity = PiiSensitivity,
        IncludeComplianceNotes = IncludeComplianceNotes
    };

    /// <summary>
    /// Valid exclusion categories.
    /// </summary>
    public static readonly IReadOnlyList<string> ValidExclusions =
    [
        "performance",
        "load_testing",
        "security",
        "accessibility",
        "mobile_specific",
        "api_only",
        "ui_only",
        "edge_cases",
        "negative",
        "localization"
    ];
}
