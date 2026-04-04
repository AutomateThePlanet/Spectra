using Spectre.Console;

namespace Spectra.CLI.Interactive;

/// <summary>
/// Prompts the user for a behavior description and optional context.
/// </summary>
public sealed class UserDescriptionPrompt
{
    /// <summary>
    /// Prompts for a behavior description.
    /// </summary>
    public string GetDescription()
    {
        AnsiConsole.MarkupLine("  [bold]Describe the behavior you want to test:[/]");
        var description = AnsiConsole.Prompt(
            new TextPrompt<string>("  > ")
                .PromptStyle(new Style(foreground: Color.Cyan)));
        return description;
    }

    /// <summary>
    /// Prompts for optional additional context.
    /// </summary>
    public string? GetContext()
    {
        AnsiConsole.MarkupLine("  [bold]Any additional context?[/] [dim](Enter to skip)[/]");
        var context = AnsiConsole.Prompt(
            new TextPrompt<string>("  > ")
                .PromptStyle(new Style(foreground: Color.Cyan))
                .AllowEmpty());
        return string.IsNullOrWhiteSpace(context) ? null : context;
    }
}
