using Spectre.Console;
using Spectra.Core.Models;
using Spectra.CLI.Output;

namespace Spectra.CLI.Interactive;

/// <summary>
/// Interactive suite selection using Spectre.Console.
/// </summary>
public sealed class SuiteSelector
{
    private readonly IAnsiConsole _console;
    private const string CreateNewSuiteOption = "Create new suite";

    public SuiteSelector(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }

    /// <summary>
    /// Prompts user to select a suite for generation.
    /// </summary>
    public SuiteSelectionResult SelectForGeneration(IReadOnlyList<SuiteSummary> suites)
    {
        _console.MarkupLine("┌ SPECTRA Test Generation");
        _console.MarkupLine("│");

        var choices = suites
            .Select(s => $"{s.Name} ({s.TestCount} tests)")
            .Concat([CreateNewSuiteOption])
            .ToList();

        var selected = _console.Prompt(
            new SelectionPrompt<string>()
                .Title($"{OutputSymbols.PromptMarkup} Which suite?")
                .AddChoices(choices)
                .HighlightStyle(Style.Parse("cyan")));

        _console.MarkupLine("└");

        if (selected == CreateNewSuiteOption)
        {
            return new SuiteSelectionResult(null, IsCreateNew: true);
        }

        var suiteName = selected.Split(' ')[0];
        var suite = suites.FirstOrDefault(s => s.Name == suiteName);

        return new SuiteSelectionResult(suite, IsCreateNew: false);
    }

    /// <summary>
    /// Prompts user to select a suite for update.
    /// </summary>
    public SuiteSummary? SelectForUpdate(IReadOnlyList<SuiteSummary> suites)
    {
        _console.MarkupLine("┌ SPECTRA Test Maintenance");
        _console.MarkupLine("│");

        var choices = suites
            .Select(s =>
            {
                var lastUpdated = s.LastUpdated.HasValue
                    ? $", last updated {FormatTimeAgo(s.LastUpdated.Value)}"
                    : "";
                return $"{s.Name} ({s.TestCount} tests{lastUpdated})";
            })
            .Concat(["All suites"])
            .ToList();

        var selected = _console.Prompt(
            new SelectionPrompt<string>()
                .Title($"{OutputSymbols.PromptMarkup} Which suite to review?")
                .AddChoices(choices)
                .HighlightStyle(Style.Parse("cyan")));

        _console.MarkupLine("└");

        if (selected == "All suites")
        {
            return null; // Indicates all suites
        }

        var suiteName = selected.Split(' ')[0];
        return suites.FirstOrDefault(s => s.Name == suiteName);
    }

    private static string FormatTimeAgo(DateTimeOffset time)
    {
        var diff = DateTimeOffset.Now - time;

        if (diff.TotalDays >= 1)
        {
            var days = (int)diff.TotalDays;
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }

        if (diff.TotalHours >= 1)
        {
            var hours = (int)diff.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        var minutes = (int)diff.TotalMinutes;
        return minutes <= 1 ? "just now" : $"{minutes} minutes ago";
    }
}

/// <summary>
/// Result of suite selection.
/// </summary>
public record SuiteSelectionResult(SuiteSummary? Suite, bool IsCreateNew);
