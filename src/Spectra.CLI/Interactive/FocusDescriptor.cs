using Spectre.Console;
using Spectra.CLI.Output;

namespace Spectra.CLI.Interactive;

/// <summary>
/// Interactive focus description input using Spectre.Console.
/// </summary>
public sealed class FocusDescriptor
{
    private readonly IAnsiConsole _console;

    public FocusDescriptor(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }

    /// <summary>
    /// Prompts user to describe their focus area.
    /// </summary>
    public string? GetFocus()
    {
        _console.MarkupLine($"{OutputSymbols.PromptMarkup} Describe what you need:");

        var focus = _console.Prompt(
            new TextPrompt<string>("│  > ")
                .AllowEmpty()
                .PromptStyle(Style.Parse("cyan")));

        _console.MarkupLine("└");

        return string.IsNullOrWhiteSpace(focus) ? null : focus.Trim();
    }

    /// <summary>
    /// Prompts for specific area description.
    /// </summary>
    public string? GetSpecificArea()
    {
        _console.MarkupLine($"{OutputSymbols.PromptMarkup} Describe the specific area to cover:");

        var area = _console.Prompt(
            new TextPrompt<string>("│  > ")
                .AllowEmpty()
                .PromptStyle(Style.Parse("cyan")));

        _console.MarkupLine("└");

        return string.IsNullOrWhiteSpace(area) ? null : area.Trim();
    }
}
