using Spectra.CLI.Infrastructure;
using Spectre.Console;
using Spectra.CLI.Output;

namespace Spectra.CLI.Session;

/// <summary>
/// Displays a session summary at exit.
/// </summary>
public static class SessionSummary
{
    public static void Display(
        GenerationSessionState session,
        OutputFormat outputFormat = OutputFormat.Human)
    {
        if (outputFormat == OutputFormat.Json)
            return;

        var fromDocs = session.Generated.Count;
        var fromSuggestions = session.Suggestions.Count(s => s.Status == SuggestionStatus.Generated);
        var fromDescription = session.UserDescribed.Count;
        var total = fromDocs + fromSuggestions + fromDescription;

        if (total == 0)
            return;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Session summary:[/]");

        if (fromDocs > 0)
            AnsiConsole.MarkupLine($"  {OutputSymbols.SuccessMarkup} {fromDocs} tests generated from documentation");

        if (fromSuggestions > 0)
            AnsiConsole.MarkupLine($"  {OutputSymbols.SuccessMarkup} {fromSuggestions} tests from suggestions");

        if (fromDescription > 0)
            AnsiConsole.MarkupLine($"  {OutputSymbols.SuccessMarkup} {fromDescription} tests from your descriptions");

        AnsiConsole.MarkupLine($"  [bold]{total} total new tests[/] in {session.Suite} suite");
    }
}
