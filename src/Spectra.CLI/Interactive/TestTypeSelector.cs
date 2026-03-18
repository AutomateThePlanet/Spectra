using Spectre.Console;
using Spectra.Core.Models;
using Spectra.CLI.Output;

namespace Spectra.CLI.Interactive;

/// <summary>
/// Interactive test type selection using Spectre.Console.
/// </summary>
public sealed class TestTypeSelector
{
    private readonly IAnsiConsole _console;

    public TestTypeSelector(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }

    /// <summary>
    /// Prompts user to select the type of tests to generate.
    /// </summary>
    public TestTypeSelection Select(string suiteName)
    {
        _console.MarkupLine($"{OutputSymbols.PromptMarkup} [cyan]{Markup.Escape(suiteName)}[/] selected. What kind of tests?");

        var choices = new Dictionary<string, TestTypeSelection>
        {
            ["Full coverage (happy path + negative + boundary)"] = TestTypeSelection.FullCoverage,
            ["Negative / error scenarios only"] = TestTypeSelection.NegativeOnly,
            ["Specific area — let me describe"] = TestTypeSelection.SpecificArea,
            ["Free description"] = TestTypeSelection.FreeDescription
        };

        var selected = _console.Prompt(
            new SelectionPrompt<string>()
                .AddChoices(choices.Keys)
                .HighlightStyle(Style.Parse("cyan")));

        _console.MarkupLine("└");

        return choices[selected];
    }

    /// <summary>
    /// Gets a focus description based on the test type.
    /// </summary>
    public string? GetFocusDescription(TestTypeSelection type)
    {
        return type switch
        {
            TestTypeSelection.FullCoverage => null,
            TestTypeSelection.NegativeOnly => "negative scenarios, error handling, edge cases",
            TestTypeSelection.SpecificArea => null, // Will be provided by FocusDescriptor
            TestTypeSelection.FreeDescription => null, // Will be provided by FocusDescriptor
            _ => null
        };
    }
}
