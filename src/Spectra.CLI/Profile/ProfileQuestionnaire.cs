using Spectra.Core.Models.Profile;

namespace Spectra.CLI.Profile;

/// <summary>
/// Interactive questionnaire for creating a test generation profile.
/// </summary>
public sealed class ProfileQuestionnaire
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly bool _interactive;

    /// <summary>
    /// Creates a questionnaire with the specified I/O streams.
    /// </summary>
    public ProfileQuestionnaire(TextReader? input = null, TextWriter? output = null, bool interactive = true)
    {
        _input = input ?? Console.In;
        _output = output ?? Console.Out;
        _interactive = interactive;
    }

    /// <summary>
    /// Gets the list of all questions.
    /// </summary>
    public static IReadOnlyList<Question> Questions =>
    [
        Question.SingleChoice(
            "detail_level",
            "What level of detail should test steps have?",
            ["High-level (brief, assumes knowledge)", "Detailed (comprehensive)", "Very detailed (granular, no assumptions)"],
            "Detailed",
            "High-level is good for experienced testers; Very detailed is good for junior testers."),

        Question.Number(
            "min_negative_scenarios",
            "Minimum number of negative test cases per feature?",
            2,
            "Negative tests cover error conditions and edge cases."),

        Question.SingleChoice(
            "default_priority",
            "What should be the default priority for generated tests?",
            ["High (P1)", "Medium (P2)", "Low (P3)"],
            "Medium (P2)"),

        Question.SingleChoice(
            "step_format",
            "How should test steps be formatted?",
            ["Numbered (1. 2. 3.)", "Bullets (- - -)", "Paragraphs"],
            "Numbered"),

        Question.YesNo(
            "use_action_verbs",
            "Should steps start with action verbs (Click, Enter, Verify)?",
            true),

        Question.YesNo(
            "include_screenshots",
            "Should test steps include screenshot suggestions?",
            false),

        Question.MultiChoice(
            "domains",
            "Which specialized domains apply to your project?",
            ["Payments (PCI)", "Authentication (MFA)", "PII/GDPR", "Healthcare (HIPAA)", "Financial", "Accessibility (WCAG)", "None"],
            "None",
            "Select domains that require special test considerations."),

        Question.SingleChoice(
            "pii_sensitivity",
            "What level of PII/GDPR consideration is needed?",
            ["None", "Standard", "Strict"],
            "None"),

        Question.MultiChoice(
            "exclusions",
            "Which test categories should NOT be generated?",
            ["Performance", "Load testing", "Security", "Accessibility", "Mobile-specific", "API-only", "UI-only", "Edge cases", "Negative", "Localization", "None"],
            "None",
            "Select categories to exclude from test generation."),

        Question.FreeText(
            "description",
            "Brief description of this profile (optional):",
            null,
            "This will be included in the profile file header.")
    ];

    /// <summary>
    /// Runs the interactive questionnaire.
    /// </summary>
    public async Task<GenerationProfile> RunAsync(CancellationToken ct = default)
    {
        var state = new QuestionnaireState(Questions.Count);

        await _output.WriteLineAsync("\n=== SPECTRA Test Generation Profile ===\n");
        await _output.WriteLineAsync("Answer the following questions to customize how tests are generated.");
        await _output.WriteLineAsync("Press Enter to accept defaults shown in [brackets].\n");

        foreach (var question in Questions)
        {
            ct.ThrowIfCancellationRequested();

            await AskQuestionAsync(question, state);
        }

        return BuildProfile(state);
    }

    /// <summary>
    /// Creates a profile from non-interactive options.
    /// </summary>
    public GenerationProfile CreateFromOptions(ProfileCreationOptions options)
    {
        return new GenerationProfile
        {
            ProfileVersion = ProfileDefaults.ProfileVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Description = options.Description,
            Options = new ProfileOptions
            {
                DetailLevel = options.DetailLevel ?? ProfileDefaults.DetailLevel,
                MinNegativeScenarios = options.MinNegativeScenarios ?? ProfileDefaults.MinNegativeScenarios,
                DefaultPriority = options.DefaultPriority ?? ProfileDefaults.DefaultPriority,
                Formatting = new FormattingOptions
                {
                    StepFormat = options.StepFormat ?? ProfileDefaults.StepFormat,
                    UseActionVerbs = options.UseActionVerbs ?? ProfileDefaults.UseActionVerbs,
                    IncludeScreenshots = options.IncludeScreenshots ?? ProfileDefaults.IncludeScreenshots,
                    MaxStepsPerTest = options.MaxStepsPerTest
                },
                Domain = new DomainOptions
                {
                    Domains = options.Domains ?? [],
                    PiiSensitivity = options.PiiSensitivity ?? ProfileDefaults.PiiSensitivity,
                    IncludeComplianceNotes = options.IncludeComplianceNotes ?? ProfileDefaults.IncludeComplianceNotes
                },
                Exclusions = options.Exclusions ?? []
            }
        };
    }

    private async Task AskQuestionAsync(Question question, QuestionnaireState state)
    {
        await _output.WriteLineAsync($"[{state.CurrentStep + 1}/{state.TotalSteps}] {question.Text}");

        if (!string.IsNullOrEmpty(question.HelpText))
        {
            await _output.WriteLineAsync($"    ({question.HelpText})");
        }

        object answer = question.Type switch
        {
            QuestionType.SingleChoice => await AskSingleChoiceAsync(question),
            QuestionType.MultiChoice => await AskMultiChoiceAsync(question),
            QuestionType.Number => await AskNumberAsync(question),
            QuestionType.YesNo => await AskYesNoAsync(question),
            QuestionType.Text => await AskTextAsync(question),
            _ => question.DefaultValue ?? string.Empty
        };

        state.RecordAnswer(question.Key, answer);
        await _output.WriteLineAsync();
    }

    private async Task<string> AskSingleChoiceAsync(Question question)
    {
        if (question.Options is null) return question.DefaultValue?.ToString() ?? string.Empty;

        for (var i = 0; i < question.Options.Count; i++)
        {
            var isDefault = question.Options[i] == question.DefaultValue?.ToString();
            var marker = isDefault ? " [default]" : "";
            await _output.WriteLineAsync($"    {i + 1}. {question.Options[i]}{marker}");
        }

        await _output.WriteAsync("    Enter choice (1-" + question.Options.Count + "): ");

        if (!_interactive)
        {
            await _output.WriteLineAsync();
            return question.DefaultValue?.ToString() ?? question.Options[0];
        }

        var input = await _input.ReadLineAsync() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return question.DefaultValue?.ToString() ?? question.Options[0];
        }

        if (int.TryParse(input.Trim(), out var choice) && choice >= 1 && choice <= question.Options.Count)
        {
            return question.Options[choice - 1];
        }

        return question.DefaultValue?.ToString() ?? question.Options[0];
    }

    private async Task<IReadOnlyList<string>> AskMultiChoiceAsync(Question question)
    {
        if (question.Options is null) return [];

        for (var i = 0; i < question.Options.Count; i++)
        {
            await _output.WriteLineAsync($"    {i + 1}. {question.Options[i]}");
        }

        await _output.WriteAsync("    Enter choices (comma-separated, e.g., 1,3,5): ");

        if (!_interactive)
        {
            await _output.WriteLineAsync();
            return [];
        }

        var input = await _input.ReadLineAsync() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var selections = new List<string>();
        foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part.Trim(), out var choice) && choice >= 1 && choice <= question.Options.Count)
            {
                var selected = question.Options[choice - 1];
                if (selected != "None" && !selections.Contains(selected))
                {
                    selections.Add(selected);
                }
            }
        }

        return selections;
    }

    private async Task<int> AskNumberAsync(Question question)
    {
        var defaultValue = question.DefaultValue is int d ? d : 0;
        await _output.WriteAsync($"    Enter number [{defaultValue}]: ");

        if (!_interactive)
        {
            await _output.WriteLineAsync();
            return defaultValue;
        }

        var input = await _input.ReadLineAsync() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        return int.TryParse(input.Trim(), out var value) ? value : defaultValue;
    }

    private async Task<bool> AskYesNoAsync(Question question)
    {
        var defaultValue = question.DefaultValue is bool b && b;
        var defaultText = defaultValue ? "Y/n" : "y/N";
        await _output.WriteAsync($"    [{defaultText}]: ");

        if (!_interactive)
        {
            await _output.WriteLineAsync();
            return defaultValue;
        }

        var input = await _input.ReadLineAsync() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        return input.Trim().ToLowerInvariant() is "y" or "yes";
    }

    private async Task<string> AskTextAsync(Question question)
    {
        var defaultValue = question.DefaultValue?.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(defaultValue))
        {
            await _output.WriteAsync($"    [{defaultValue}]: ");
        }
        else
        {
            await _output.WriteAsync("    : ");
        }

        if (!_interactive)
        {
            await _output.WriteLineAsync();
            return defaultValue;
        }

        var input = await _input.ReadLineAsync() ?? string.Empty;
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
    }

    private GenerationProfile BuildProfile(QuestionnaireState state)
    {
        return new GenerationProfile
        {
            ProfileVersion = ProfileDefaults.ProfileVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Description = state.GetAnswer<string>("description", null!),
            Options = new ProfileOptions
            {
                DetailLevel = ParseDetailLevel(state.GetAnswer<string>("detail_level", "Detailed")),
                MinNegativeScenarios = state.GetAnswer("min_negative_scenarios", 2),
                DefaultPriority = ParsePriority(state.GetAnswer<string>("default_priority", "Medium (P2)")),
                Formatting = new FormattingOptions
                {
                    StepFormat = ParseStepFormat(state.GetAnswer<string>("step_format", "Numbered")),
                    UseActionVerbs = state.GetAnswer("use_action_verbs", true),
                    IncludeScreenshots = state.GetAnswer("include_screenshots", false)
                },
                Domain = new DomainOptions
                {
                    Domains = ParseDomains(state.GetAnswer<IReadOnlyList<string>>("domains", [])),
                    PiiSensitivity = ParsePiiSensitivity(state.GetAnswer<string>("pii_sensitivity", "None")),
                    IncludeComplianceNotes = false
                },
                Exclusions = ParseExclusions(state.GetAnswer<IReadOnlyList<string>>("exclusions", []))
            }
        };
    }

    private static DetailLevel ParseDetailLevel(string value) => value switch
    {
        var v when v.Contains("High-level", StringComparison.OrdinalIgnoreCase) => DetailLevel.HighLevel,
        var v when v.Contains("Very detailed", StringComparison.OrdinalIgnoreCase) => DetailLevel.VeryDetailed,
        _ => DetailLevel.Detailed
    };

    private static Priority ParsePriority(string value) => value switch
    {
        var v when v.Contains("High", StringComparison.OrdinalIgnoreCase) => Priority.High,
        var v when v.Contains("Low", StringComparison.OrdinalIgnoreCase) => Priority.Low,
        _ => Priority.Medium
    };

    private static StepFormat ParseStepFormat(string value) => value switch
    {
        var v when v.Contains("Bullets", StringComparison.OrdinalIgnoreCase) => StepFormat.Bullets,
        var v when v.Contains("Paragraphs", StringComparison.OrdinalIgnoreCase) => StepFormat.Paragraphs,
        _ => StepFormat.Numbered
    };

    private static PiiSensitivity ParsePiiSensitivity(string value) => value switch
    {
        var v when v.Contains("Standard", StringComparison.OrdinalIgnoreCase) => PiiSensitivity.Standard,
        var v when v.Contains("Strict", StringComparison.OrdinalIgnoreCase) => PiiSensitivity.Strict,
        _ => PiiSensitivity.None
    };

    private static IReadOnlyList<DomainType> ParseDomains(IReadOnlyList<string> values)
    {
        var result = new List<DomainType>();
        foreach (var value in values)
        {
            var domain = value.ToLowerInvariant() switch
            {
                var v when v.Contains("payment") => (DomainType?)DomainType.Payments,
                var v when v.Contains("auth") => DomainType.Authentication,
                var v when v.Contains("pii") || v.Contains("gdpr") => DomainType.PiiGdpr,
                var v when v.Contains("health") || v.Contains("hipaa") => DomainType.Healthcare,
                var v when v.Contains("financial") => DomainType.Financial,
                var v when v.Contains("accessibility") || v.Contains("wcag") => DomainType.Accessibility,
                _ => null
            };
            if (domain.HasValue)
            {
                result.Add(domain.Value);
            }
        }
        return result;
    }

    private static IReadOnlyList<string> ParseExclusions(IReadOnlyList<string> values)
    {
        var result = new List<string>();
        foreach (var value in values)
        {
            var exclusion = value.ToLowerInvariant() switch
            {
                var v when v.Contains("performance") => "performance",
                var v when v.Contains("load") => "load_testing",
                var v when v.Contains("security") => "security",
                var v when v.Contains("accessibility") => "accessibility",
                var v when v.Contains("mobile") => "mobile_specific",
                var v when v.Contains("api") => "api_only",
                var v when v.Contains("ui") => "ui_only",
                var v when v.Contains("edge") => "edge_cases",
                var v when v.Contains("negative") => "negative",
                var v when v.Contains("local") => "localization",
                _ => null
            };
            if (exclusion is not null && !result.Contains(exclusion))
            {
                result.Add(exclusion);
            }
        }
        return result;
    }
}

/// <summary>
/// Options for non-interactive profile creation.
/// </summary>
public sealed class ProfileCreationOptions
{
    public string? Description { get; init; }
    public DetailLevel? DetailLevel { get; init; }
    public int? MinNegativeScenarios { get; init; }
    public Priority? DefaultPriority { get; init; }
    public StepFormat? StepFormat { get; init; }
    public bool? UseActionVerbs { get; init; }
    public bool? IncludeScreenshots { get; init; }
    public int? MaxStepsPerTest { get; init; }
    public IReadOnlyList<DomainType>? Domains { get; init; }
    public PiiSensitivity? PiiSensitivity { get; init; }
    public bool? IncludeComplianceNotes { get; init; }
    public IReadOnlyList<string>? Exclusions { get; init; }
}
