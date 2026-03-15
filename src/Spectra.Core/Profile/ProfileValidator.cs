using Spectra.Core.Models.Profile;

namespace Spectra.Core.Profile;

/// <summary>
/// Validates profile content against schema and value constraints.
/// </summary>
public sealed class ProfileValidator
{
    /// <summary>
    /// Validates a parsed profile.
    /// </summary>
    public ProfileValidationResult Validate(GenerationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Validate profile version
        if (profile.ProfileVersion < 1)
        {
            errors.Add(ValidationError.Create(
                "INVALID_VERSION",
                "profile_version must be >= 1",
                field: "profile_version"));
        }

        // Validate options
        ValidateOptions(profile.Options, errors, warnings);

        return errors.Count > 0
            ? ProfileValidationResult.Failure(errors, warnings)
            : ProfileValidationResult.Success(profile, warnings);
    }

    /// <summary>
    /// Validates profile content string (parses and validates).
    /// </summary>
    public ProfileValidationResult ValidateContent(string content)
    {
        var parser = new ProfileParser();
        var parseResult = parser.Parse(content);

        if (!parseResult.IsSuccess)
        {
            return ProfileValidationResult.Failure(parseResult.Error!);
        }

        return Validate(parseResult.Profile!);
    }

    private void ValidateOptions(ProfileOptions options, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        // Validate min_negative_scenarios
        if (options.MinNegativeScenarios < 0)
        {
            errors.Add(ValidationError.Create(
                "INVALID_MIN_NEGATIVE",
                "min_negative_scenarios must be >= 0",
                field: "options.min_negative_scenarios"));
        }
        else if (options.MinNegativeScenarios > ProfileDefaults.MaxNegativeScenarios)
        {
            errors.Add(ValidationError.Create(
                "INVALID_MIN_NEGATIVE",
                $"min_negative_scenarios must be <= {ProfileDefaults.MaxNegativeScenarios}",
                field: "options.min_negative_scenarios"));
        }

        // Validate formatting
        ValidateFormatting(options.Formatting, errors, warnings);

        // Validate exclusions
        ValidateExclusions(options.Exclusions, errors, warnings);
    }

    private void ValidateFormatting(FormattingOptions formatting, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        // Validate max_steps_per_test
        if (formatting.MaxStepsPerTest.HasValue)
        {
            if (formatting.MaxStepsPerTest.Value < 1)
            {
                errors.Add(ValidationError.Create(
                    "INVALID_MAX_STEPS",
                    "max_steps_per_test must be >= 1",
                    field: "options.formatting.max_steps_per_test"));
            }
            else if (formatting.MaxStepsPerTest.Value > ProfileDefaults.MaxStepsLimit)
            {
                errors.Add(ValidationError.Create(
                    "INVALID_MAX_STEPS",
                    $"max_steps_per_test must be <= {ProfileDefaults.MaxStepsLimit}",
                    field: "options.formatting.max_steps_per_test"));
            }
        }
    }

    private void ValidateExclusions(IReadOnlyList<string> exclusions, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (exclusions.Count > ProfileDefaults.MaxExclusions)
        {
            errors.Add(ValidationError.Create(
                "TOO_MANY_EXCLUSIONS",
                $"exclusions array exceeds maximum of {ProfileDefaults.MaxExclusions} items",
                field: "options.exclusions"));
        }

        foreach (var exclusion in exclusions)
        {
            if (!ProfileDefaults.ValidExclusions.Contains(exclusion.ToLowerInvariant()))
            {
                warnings.Add(ValidationWarning.Create(
                    "UNKNOWN_EXCLUSION",
                    $"Unknown exclusion category: {exclusion}",
                    field: "options.exclusions",
                    defaultUsed: "ignored"));
            }
        }
    }
}
