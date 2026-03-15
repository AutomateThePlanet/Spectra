using Spectra.Core.Models.Profile;

namespace Spectra.Core.Profile;

/// <summary>
/// Loads profile files from the file system with inheritance support.
/// </summary>
public sealed class ProfileLoader
{
    private readonly ProfileParser _parser = new();
    private readonly ProfileValidator _validator = new();

    /// <summary>
    /// Loads the effective profile for a given suite path.
    /// </summary>
    public async Task<EffectiveProfile> LoadAsync(string basePath, string? suitePath = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(basePath);

        // Try to load suite profile if suite path is provided
        GenerationProfile? suiteProfile = null;
        ProfileSource? suiteSource = null;

        if (!string.IsNullOrEmpty(suitePath))
        {
            var suiteProfilePath = Path.Combine(suitePath, ProfileDefaults.SuiteProfileFileName);
            var loadResult = await TryLoadProfileAsync(suiteProfilePath, ct);
            if (loadResult.HasValue)
            {
                suiteProfile = loadResult.Value.Profile;
                suiteSource = ProfileSource.Suite(suiteProfilePath);
            }
        }

        // Try to load repository profile
        var repoProfilePath = Path.Combine(basePath, ProfileDefaults.RepositoryProfileFileName);
        var repoResult = await TryLoadProfileAsync(repoProfilePath, ct);
        GenerationProfile? repoProfile = repoResult?.Profile;
        ProfileSource? repoSource = repoResult.HasValue
            ? ProfileSource.Repository(repoProfilePath)
            : null;

        // Determine effective profile
        if (suiteProfile is not null && repoProfile is not null)
        {
            // Merge suite over repo
            var merged = MergeProfiles(repoProfile, suiteProfile);
            return EffectiveProfile.FromMerge(merged, suiteSource!, repoSource!);
        }

        if (suiteProfile is not null)
        {
            return EffectiveProfile.FromProfile(suiteProfile, suiteSource!);
        }

        if (repoProfile is not null)
        {
            return EffectiveProfile.FromProfile(repoProfile, repoSource!);
        }

        // No profile found, return defaults
        return EffectiveProfile.FromDefaults();
    }

    /// <summary>
    /// Loads only the repository profile.
    /// </summary>
    public async Task<ProfileLoadResult> LoadRepositoryProfileAsync(string basePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(basePath);

        var profilePath = Path.Combine(basePath, ProfileDefaults.RepositoryProfileFileName);
        return await LoadFromPathAsync(profilePath, ct);
    }

    /// <summary>
    /// Loads a profile from a specific path.
    /// </summary>
    public async Task<ProfileLoadResult> LoadFromPathAsync(string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return ProfileLoadResult.NotFound(path);
        }

        try
        {
            var content = await File.ReadAllTextAsync(path, ct);
            var parseResult = _parser.Parse(content);

            if (!parseResult.IsSuccess)
            {
                return ProfileLoadResult.Invalid(path, [parseResult.Error!]);
            }

            var validationResult = _validator.Validate(parseResult.Profile!);

            if (!validationResult.IsValid)
            {
                return ProfileLoadResult.Invalid(path, validationResult.Errors, validationResult.Warnings);
            }

            return ProfileLoadResult.Success(parseResult.Profile!, path, validationResult.Warnings);
        }
        catch (Exception ex)
        {
            return ProfileLoadResult.Invalid(path, [ValidationError.Create("READ_ERROR", ex.Message)]);
        }
    }

    /// <summary>
    /// Checks if a repository profile exists.
    /// </summary>
    public bool RepositoryProfileExists(string basePath)
    {
        var profilePath = Path.Combine(basePath, ProfileDefaults.RepositoryProfileFileName);
        return File.Exists(profilePath);
    }

    /// <summary>
    /// Checks if a suite profile exists.
    /// </summary>
    public bool SuiteProfileExists(string suitePath)
    {
        var profilePath = Path.Combine(suitePath, ProfileDefaults.SuiteProfileFileName);
        return File.Exists(profilePath);
    }

    private async Task<(GenerationProfile Profile, string Path)?> TryLoadProfileAsync(string path, CancellationToken ct)
    {
        var result = await LoadFromPathAsync(path, ct);
        return result.IsSuccess ? (result.Profile!, path) : null;
    }

    private static GenerationProfile MergeProfiles(GenerationProfile baseProfile, GenerationProfile overrideProfile)
    {
        // Suite profile options override repo profile options at the category level
        return new GenerationProfile
        {
            ProfileVersion = Math.Max(baseProfile.ProfileVersion, overrideProfile.ProfileVersion),
            CreatedAt = baseProfile.CreatedAt,
            UpdatedAt = overrideProfile.UpdatedAt,
            Description = overrideProfile.Description ?? baseProfile.Description,
            Options = MergeOptions(baseProfile.Options, overrideProfile.Options)
        };
    }

    private static ProfileOptions MergeOptions(ProfileOptions baseOptions, ProfileOptions overrideOptions)
    {
        // Each category is replaced entirely if specified in override
        return new ProfileOptions
        {
            DetailLevel = overrideOptions.DetailLevel != ProfileDefaults.DetailLevel
                ? overrideOptions.DetailLevel
                : baseOptions.DetailLevel,
            MinNegativeScenarios = overrideOptions.MinNegativeScenarios != ProfileDefaults.MinNegativeScenarios
                ? overrideOptions.MinNegativeScenarios
                : baseOptions.MinNegativeScenarios,
            DefaultPriority = overrideOptions.DefaultPriority != ProfileDefaults.DefaultPriority
                ? overrideOptions.DefaultPriority
                : baseOptions.DefaultPriority,
            Formatting = HasFormattingChanges(overrideOptions.Formatting)
                ? overrideOptions.Formatting
                : baseOptions.Formatting,
            Domain = HasDomainChanges(overrideOptions.Domain)
                ? overrideOptions.Domain
                : baseOptions.Domain,
            Exclusions = overrideOptions.Exclusions.Count > 0
                ? overrideOptions.Exclusions
                : baseOptions.Exclusions
        };
    }

    private static bool HasFormattingChanges(FormattingOptions formatting)
    {
        var defaults = ProfileDefaults.CreateDefaultFormatting();
        return formatting.StepFormat != defaults.StepFormat ||
               formatting.UseActionVerbs != defaults.UseActionVerbs ||
               formatting.IncludeScreenshots != defaults.IncludeScreenshots ||
               formatting.MaxStepsPerTest != defaults.MaxStepsPerTest;
    }

    private static bool HasDomainChanges(DomainOptions domain)
    {
        var defaults = ProfileDefaults.CreateDefaultDomain();
        return domain.Domains.Count > 0 ||
               domain.PiiSensitivity != defaults.PiiSensitivity ||
               domain.IncludeComplianceNotes != defaults.IncludeComplianceNotes;
    }
}

/// <summary>
/// Result of loading a profile.
/// </summary>
public sealed class ProfileLoadResult
{
    public bool IsSuccess { get; private init; }
    public bool WasNotFound { get; private init; }
    public GenerationProfile? Profile { get; private init; }
    public string Path { get; private init; } = string.Empty;
    public IReadOnlyList<ValidationError> Errors { get; private init; } = [];
    public IReadOnlyList<ValidationWarning> Warnings { get; private init; } = [];

    public static ProfileLoadResult Success(GenerationProfile profile, string path, IReadOnlyList<ValidationWarning>? warnings = null) => new()
    {
        IsSuccess = true,
        Profile = profile,
        Path = path,
        Warnings = warnings ?? []
    };

    public static ProfileLoadResult NotFound(string path) => new()
    {
        IsSuccess = false,
        WasNotFound = true,
        Path = path
    };

    public static ProfileLoadResult Invalid(string path, IReadOnlyList<ValidationError> errors, IReadOnlyList<ValidationWarning>? warnings = null) => new()
    {
        IsSuccess = false,
        Path = path,
        Errors = errors,
        Warnings = warnings ?? []
    };
}
