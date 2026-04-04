using Spectra.CLI.Infrastructure;
using Spectra.CLI.Session;
using Spectre.Console;

namespace Spectra.CLI.Output;

/// <summary>
/// Displays suggestions menu and handles user selection.
/// </summary>
public sealed class SuggestionPresenter
{
    private readonly OutputFormat _outputFormat;

    public SuggestionPresenter(OutputFormat outputFormat = OutputFormat.Human)
    {
        _outputFormat = outputFormat;
    }

    /// <summary>
    /// Displays suggestions and returns the user's choice.
    /// </summary>
    public SuggestionChoice ShowSuggestionsMenu(IReadOnlyList<SessionSuggestion> suggestions)
    {
        if (_outputFormat == OutputFormat.Json)
            return new SuggestionChoice { Action = SuggestionAction.Done };

        if (suggestions.Count == 0)
            return new SuggestionChoice { Action = SuggestionAction.Done };

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OutputSymbols.InfoMarkup} [cyan]Suggested additional test cases based on gap analysis:[/]");

        foreach (var s in suggestions.Where(s => s.Status == SuggestionStatus.Pending))
        {
            AnsiConsole.MarkupLine($"    [grey]{s.Index}.[/] {Markup.Escape(s.Title)} [dim]({s.Category})[/]");
        }

        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("  [bold]What would you like to do?[/]")
                .HighlightStyle(Style.Parse("cyan"))
                .AddChoices(
                    $"Generate all {suggestions.Count(s => s.Status == SuggestionStatus.Pending)} suggestions",
                    "Pick specific suggestions (by number)",
                    "Describe your own test case",
                    "Done — exit session"));

        if (choice.StartsWith("Generate all"))
            return new SuggestionChoice { Action = SuggestionAction.GenerateAll };

        if (choice.StartsWith("Pick specific"))
        {
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>("  Enter suggestion numbers (e.g., 1,3):")
                    .PromptStyle(new Style(foreground: Color.Cyan)));

            var indices = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var n) ? n : -1)
                .Where(n => n > 0)
                .ToList();

            return new SuggestionChoice { Action = SuggestionAction.GenerateSelected, SelectedIndices = indices };
        }

        if (choice.StartsWith("Describe"))
            return new SuggestionChoice { Action = SuggestionAction.DescribeOwn };

        return new SuggestionChoice { Action = SuggestionAction.Done };
    }
}

public enum SuggestionAction
{
    GenerateAll,
    GenerateSelected,
    DescribeOwn,
    Done
}

public sealed class SuggestionChoice
{
    public SuggestionAction Action { get; init; }
    public List<int>? SelectedIndices { get; init; }
}
