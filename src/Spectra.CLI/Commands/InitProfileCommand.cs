using System.CommandLine;
using System.CommandLine.Invocation;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Profile;
using Spectra.Core.Models.Profile;
using Spectra.Core.Profile;

namespace Spectra.CLI.Commands;

/// <summary>
/// Command for initializing a test generation profile.
/// </summary>
public sealed class InitProfileCommand : Command
{
    public InitProfileCommand() : base("init-profile", "Create or update a test generation profile")
    {
        AddOption(new Option<bool>(
            ["--non-interactive", "-n"],
            "Run without interactive prompts, using defaults or provided options"));

        AddOption(new Option<string?>(
            ["--detail-level", "-d"],
            "Detail level: high_level, detailed, or very_detailed"));

        AddOption(new Option<int?>(
            ["--min-negative"],
            "Minimum negative test scenarios per feature"));

        AddOption(new Option<string?>(
            ["--priority", "-p"],
            "Default priority: high, medium, or low"));

        AddOption(new Option<string?>(
            ["--step-format"],
            "Step format: numbered, bullets, or paragraphs"));

        AddOption(new Option<bool?>(
            ["--action-verbs"],
            "Use action verbs in steps"));

        AddOption(new Option<bool?>(
            ["--screenshots"],
            "Include screenshot suggestions"));

        AddOption(new Option<string[]?>(
            ["--domains"],
            "Domains: payments, authentication, pii_gdpr, healthcare, financial, accessibility"));

        AddOption(new Option<string?>(
            ["--pii-sensitivity"],
            "PII sensitivity: none, standard, or strict"));

        AddOption(new Option<string[]?>(
            ["--exclusions"],
            "Exclusion categories"));

        AddOption(new Option<string?>(
            ["--description"],
            "Profile description"));

        AddOption(new Option<bool>(
            ["--force", "-f"],
            "Overwrite existing profile without confirmation"));

        AddOption(new Option<string?>(
            ["--suite", "-s"],
            "Create a suite-level profile in the specified suite directory"));

        AddOption(new Option<bool>(
            ["--edit", "-e"],
            "Edit an existing profile (only update specified options)"));

        this.SetHandler(ExecuteAsync);
    }

    private async Task ExecuteAsync(InvocationContext context)
    {
        var ct = context.GetCancellationToken();

        var nonInteractive = context.ParseResult.GetValueForOption<bool>(Options.Get<bool>("--non-interactive"));
        var force = context.ParseResult.GetValueForOption<bool>(Options.Get<bool>("--force"));
        var suiteDir = context.ParseResult.GetValueForOption<string?>(Options.Get<string?>("--suite"));
        var edit = context.ParseResult.GetValueForOption<bool>(Options.Get<bool>("--edit"));

        try
        {
            var basePath = Directory.GetCurrentDirectory();
            var profilePath = GetProfilePath(basePath, suiteDir);

            // Check for existing profile
            var existingProfile = await LoadExistingProfileAsync(profilePath, ct);

            if (existingProfile is not null && !force && !edit)
            {
                if (nonInteractive)
                {
                    Console.Error.WriteLine($"Profile already exists at {profilePath}. Use --force to overwrite or --edit to modify.");
                    context.ExitCode = ExitCodes.Error;
                    return;
                }

                Console.WriteLine($"A profile already exists at {profilePath}.");
                Console.Write("Overwrite? [y/N]: ");
                var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (response is not "y" and not "yes")
                {
                    Console.WriteLine("Cancelled.");
                    context.ExitCode = ExitCodes.Cancelled;
                    return;
                }
            }

            GenerationProfile profile;

            if (nonInteractive)
            {
                // Build profile from command-line options
                var options = BuildOptionsFromContext(context, existingProfile);
                var questionnaire = new ProfileQuestionnaire(interactive: false);
                profile = questionnaire.CreateFromOptions(options);
            }
            else
            {
                // Run interactive questionnaire
                var questionnaire = new ProfileQuestionnaire();
                profile = await questionnaire.RunAsync(ct);
            }

            // If editing, merge with existing
            if (edit && existingProfile is not null)
            {
                profile = MergeProfiles(existingProfile, profile, context);
            }

            // Validate
            var validator = new ProfileValidator();
            var validation = validator.Validate(profile);

            if (!validation.IsValid)
            {
                Console.Error.WriteLine("Profile validation failed:");
                foreach (var error in validation.Errors)
                {
                    Console.Error.WriteLine($"  - [{error.Code}] {error.Message}");
                }
                context.ExitCode = ExitCodes.Error;
                return;
            }

            if (validation.Warnings.Count > 0)
            {
                Console.WriteLine("Warnings:");
                foreach (var warning in validation.Warnings)
                {
                    Console.WriteLine($"  - [{warning.Code}] {warning.Message}");
                }
            }

            // Write profile
            var writer = new ProfileWriter();
            var directory = Path.GetDirectoryName(profilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await writer.WriteAsync(profilePath, profile, ct: ct);

            Console.WriteLine();
            Console.WriteLine($"Profile created: {profilePath}");

            // Show summary
            var renderer = new ProfileRenderer();
            var effective = EffectiveProfile.FromProfile(profile,
                suiteDir is not null
                    ? ProfileSource.Suite(profilePath)
                    : ProfileSource.Repository(profilePath));
            Console.WriteLine();
            Console.WriteLine(renderer.FormatSummary(effective));

            context.ExitCode = ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nOperation cancelled.");
            context.ExitCode = ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            context.ExitCode = ExitCodes.Error;
        }
    }

    private static string GetProfilePath(string basePath, string? suiteDir)
    {
        if (suiteDir is not null)
        {
            var suitePath = Path.IsPathRooted(suiteDir)
                ? suiteDir
                : Path.Combine(basePath, suiteDir);
            return Path.Combine(suitePath, ProfileDefaults.SuiteProfileFileName);
        }

        return Path.Combine(basePath, ProfileDefaults.RepositoryProfileFileName);
    }

    private static async Task<GenerationProfile?> LoadExistingProfileAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(path, ct);
            var parser = new ProfileParser();
            var result = parser.Parse(content);
            return result.IsSuccess ? result.Profile : null;
        }
        catch
        {
            return null;
        }
    }

    private static ProfileCreationOptions BuildOptionsFromContext(InvocationContext context, GenerationProfile? existing)
    {
        var detailLevelStr = context.ParseResult.GetValueForOption<string?>(Options.Get<string?>("--detail-level"));
        var minNegative = context.ParseResult.GetValueForOption<int?>(Options.Get<int?>("--min-negative"));
        var priorityStr = context.ParseResult.GetValueForOption<string?>(Options.Get<string?>("--priority"));
        var stepFormatStr = context.ParseResult.GetValueForOption<string?>(Options.Get<string?>("--step-format"));
        var actionVerbs = context.ParseResult.GetValueForOption<bool?>(Options.Get<bool?>("--action-verbs"));
        var screenshots = context.ParseResult.GetValueForOption<bool?>(Options.Get<bool?>("--screenshots"));
        var domainsStr = context.ParseResult.GetValueForOption<string[]?>(Options.Get<string[]?>("--domains"));
        var piiSensitivityStr = context.ParseResult.GetValueForOption<string?>(Options.Get<string?>("--pii-sensitivity"));
        var exclusionsStr = context.ParseResult.GetValueForOption<string[]?>(Options.Get<string[]?>("--exclusions"));
        var description = context.ParseResult.GetValueForOption<string?>(Options.Get<string?>("--description"));

        return new ProfileCreationOptions
        {
            Description = description ?? existing?.Description,
            DetailLevel = ParseDetailLevel(detailLevelStr) ?? existing?.Options.DetailLevel,
            MinNegativeScenarios = minNegative ?? existing?.Options.MinNegativeScenarios,
            DefaultPriority = ParsePriority(priorityStr) ?? existing?.Options.DefaultPriority,
            StepFormat = ParseStepFormat(stepFormatStr) ?? existing?.Options.Formatting.StepFormat,
            UseActionVerbs = actionVerbs ?? existing?.Options.Formatting.UseActionVerbs,
            IncludeScreenshots = screenshots ?? existing?.Options.Formatting.IncludeScreenshots,
            Domains = ParseDomains(domainsStr) ?? existing?.Options.Domain.Domains,
            PiiSensitivity = ParsePiiSensitivity(piiSensitivityStr) ?? existing?.Options.Domain.PiiSensitivity,
            Exclusions = exclusionsStr?.ToList() ?? existing?.Options.Exclusions as IReadOnlyList<string>
        };
    }

    private static GenerationProfile MergeProfiles(GenerationProfile existing, GenerationProfile updates, InvocationContext context)
    {
        // Only apply updates for options that were explicitly specified
        return new GenerationProfile
        {
            ProfileVersion = existing.ProfileVersion,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
            Description = updates.Description ?? existing.Description,
            Options = new ProfileOptions
            {
                DetailLevel = WasSpecified(context, "--detail-level") ? updates.Options.DetailLevel : existing.Options.DetailLevel,
                MinNegativeScenarios = WasSpecified(context, "--min-negative") ? updates.Options.MinNegativeScenarios : existing.Options.MinNegativeScenarios,
                DefaultPriority = WasSpecified(context, "--priority") ? updates.Options.DefaultPriority : existing.Options.DefaultPriority,
                Formatting = new FormattingOptions
                {
                    StepFormat = WasSpecified(context, "--step-format") ? updates.Options.Formatting.StepFormat : existing.Options.Formatting.StepFormat,
                    UseActionVerbs = WasSpecified(context, "--action-verbs") ? updates.Options.Formatting.UseActionVerbs : existing.Options.Formatting.UseActionVerbs,
                    IncludeScreenshots = WasSpecified(context, "--screenshots") ? updates.Options.Formatting.IncludeScreenshots : existing.Options.Formatting.IncludeScreenshots,
                    MaxStepsPerTest = existing.Options.Formatting.MaxStepsPerTest
                },
                Domain = new DomainOptions
                {
                    Domains = WasSpecified(context, "--domains") ? updates.Options.Domain.Domains : existing.Options.Domain.Domains,
                    PiiSensitivity = WasSpecified(context, "--pii-sensitivity") ? updates.Options.Domain.PiiSensitivity : existing.Options.Domain.PiiSensitivity,
                    IncludeComplianceNotes = existing.Options.Domain.IncludeComplianceNotes
                },
                Exclusions = WasSpecified(context, "--exclusions") ? updates.Options.Exclusions : existing.Options.Exclusions
            }
        };
    }

    private static bool WasSpecified(InvocationContext context, string optionName)
    {
        return context.ParseResult.Tokens.Any(t => t.Value == optionName);
    }

    private static DetailLevel? ParseDetailLevel(string? value) => value?.ToLowerInvariant() switch
    {
        "high_level" or "highlevel" => DetailLevel.HighLevel,
        "detailed" => DetailLevel.Detailed,
        "very_detailed" or "verydetailed" => DetailLevel.VeryDetailed,
        _ => null
    };

    private static Priority? ParsePriority(string? value) => value?.ToLowerInvariant() switch
    {
        "high" => Priority.High,
        "medium" => Priority.Medium,
        "low" => Priority.Low,
        _ => null
    };

    private static StepFormat? ParseStepFormat(string? value) => value?.ToLowerInvariant() switch
    {
        "numbered" => StepFormat.Numbered,
        "bullets" => StepFormat.Bullets,
        "paragraphs" => StepFormat.Paragraphs,
        _ => null
    };

    private static PiiSensitivity? ParsePiiSensitivity(string? value) => value?.ToLowerInvariant() switch
    {
        "none" => PiiSensitivity.None,
        "standard" => PiiSensitivity.Standard,
        "strict" => PiiSensitivity.Strict,
        _ => null
    };

    private static IReadOnlyList<DomainType>? ParseDomains(string[]? values)
    {
        if (values is null || values.Length == 0) return null;

        var result = new List<DomainType>();
        foreach (var value in values)
        {
            var domain = value.ToLowerInvariant() switch
            {
                "payments" => (DomainType?)DomainType.Payments,
                "authentication" => DomainType.Authentication,
                "pii_gdpr" or "piigdpr" => DomainType.PiiGdpr,
                "healthcare" => DomainType.Healthcare,
                "financial" => DomainType.Financial,
                "accessibility" => DomainType.Accessibility,
                _ => null
            };
            if (domain.HasValue)
            {
                result.Add(domain.Value);
            }
        }
        return result.Count > 0 ? result : null;
    }

    // Helper class for option retrieval
    private static class Options
    {
        private static readonly Dictionary<string, Option> _options = new();

        static Options()
        {
            Register<bool>("--non-interactive");
            Register<string?>("--detail-level");
            Register<int?>("--min-negative");
            Register<string?>("--priority");
            Register<string?>("--step-format");
            Register<bool?>("--action-verbs");
            Register<bool?>("--screenshots");
            Register<string[]?>("--domains");
            Register<string?>("--pii-sensitivity");
            Register<string[]?>("--exclusions");
            Register<string?>("--description");
            Register<bool>("--force");
            Register<string?>("--suite");
            Register<bool>("--edit");
        }

        private static void Register<T>(string name)
        {
            _options[name] = new Option<T>(name);
        }

        public static Option<T> Get<T>(string name)
        {
            return (Option<T>)_options[name];
        }
    }
}
