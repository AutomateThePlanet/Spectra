using Spectra.CLI.Agent.Analysis;
using Spectra.Core.Models;
using Spectre.Console;

namespace Spectra.CLI.Interactive;

/// <summary>
/// User's selection from the interactive count menu.
/// </summary>
public sealed record CountSelection
{
    /// <summary>Number of tests to generate.</summary>
    public required int Count { get; init; }

    /// <summary>If user selected specific categories (null = all).</summary>
    public IReadOnlyList<BehaviorCategory>? SelectedCategories { get; init; }

    /// <summary>If user chose "describe what I want".</summary>
    public string? FreeTextDescription { get; init; }
}

/// <summary>
/// Interactive menu for selecting how many tests to generate based on behavior analysis.
/// </summary>
public sealed class CountSelector
{
    private static readonly Dictionary<BehaviorCategory, string> CategoryLabels = new()
    {
        [BehaviorCategory.HappyPath] = "happy paths",
        [BehaviorCategory.Negative] = "negative scenarios",
        [BehaviorCategory.EdgeCase] = "edge cases",
        [BehaviorCategory.Security] = "security checks",
        [BehaviorCategory.Performance] = "performance tests"
    };

    /// <summary>
    /// Presents a selection menu based on the analysis result and returns the user's choice.
    /// </summary>
    public CountSelection Select(BehaviorAnalysisResult analysis)
    {
        var options = BuildOptions(analysis);

        AnsiConsole.MarkupLine($"  ◆ How many test cases to generate?");
        var prompt = new SelectionPrompt<string>()
            .HighlightStyle(Style.Parse("cyan"))
            .AddChoices(options.Select(o => o.Label));

        var selected = AnsiConsole.Prompt(prompt);
        var option = options.First(o => o.Label == selected);

        if (option.IsCustomNumber)
        {
            var countPrompt = new TextPrompt<int>("  │  Enter number of tests:")
                .PromptStyle(new Style(foreground: Color.Cyan))
                .Validate(n => n > 0 && n <= analysis.TotalBehaviors
                    ? Spectre.Console.ValidationResult.Success()
                    : Spectre.Console.ValidationResult.Error($"Enter a number between 1 and {analysis.TotalBehaviors}"));
            var customCount = AnsiConsole.Prompt(countPrompt);
            return new CountSelection { Count = customCount };
        }

        if (option.IsFreeText)
        {
            var textPrompt = new TextPrompt<string>("  │  Describe what you want:")
                .PromptStyle(new Style(foreground: Color.Cyan));
            var description = AnsiConsole.Prompt(textPrompt);
            return new CountSelection
            {
                Count = analysis.RecommendedCount,
                FreeTextDescription = description
            };
        }

        return new CountSelection
        {
            Count = option.Count,
            SelectedCategories = option.Categories
        };
    }

    internal static List<MenuOption> BuildOptions(BehaviorAnalysisResult analysis)
    {
        var options = new List<MenuOption>();
        var effectiveCount = analysis.RecommendedCount > 0
            ? analysis.RecommendedCount
            : analysis.TotalBehaviors;

        // Option 1: All behaviors
        options.Add(new MenuOption
        {
            Label = $"All {effectiveCount} — full coverage of identified behaviors",
            Count = effectiveCount,
            Categories = null
        });

        // Build cumulative category options from the breakdown, ordered by count descending
        var orderedCategories = analysis.Breakdown
            .Where(kvp => kvp.Value > 0)
            .OrderByDescending(kvp => kvp.Value)
            .ToList();

        if (orderedCategories.Count > 1)
        {
            // Single largest category
            var first = orderedCategories[0];
            var firstLabel = CategoryLabels.GetValueOrDefault(first.Key, first.Key.ToString());
            options.Add(new MenuOption
            {
                Label = $"{first.Value} — {firstLabel} only",
                Count = first.Value,
                Categories = [first.Key]
            });

            // Cumulative: first + second (if there's a third category)
            if (orderedCategories.Count > 2)
            {
                var second = orderedCategories[1];
                var secondLabel = CategoryLabels.GetValueOrDefault(second.Key, second.Key.ToString());
                var cumulativeCount = first.Value + second.Value;
                options.Add(new MenuOption
                {
                    Label = $"{cumulativeCount} — {firstLabel} + {secondLabel}",
                    Count = cumulativeCount,
                    Categories = [first.Key, second.Key]
                });
            }
        }

        // Custom number
        options.Add(new MenuOption
        {
            Label = "Custom number",
            Count = 0,
            IsCustomNumber = true
        });

        // Free text
        options.Add(new MenuOption
        {
            Label = "Let me describe what I want",
            Count = 0,
            IsFreeText = true
        });

        return options;
    }

    internal sealed record MenuOption
    {
        public required string Label { get; init; }
        public required int Count { get; init; }
        public IReadOnlyList<BehaviorCategory>? Categories { get; init; }
        public bool IsCustomNumber { get; init; }
        public bool IsFreeText { get; init; }
    }
}
