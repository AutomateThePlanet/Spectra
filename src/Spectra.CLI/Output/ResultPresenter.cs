using Spectre.Console;
using Spectra.Core.Models;

namespace Spectra.CLI.Output;

/// <summary>
/// Presents results in table format using Spectre.Console.
/// </summary>
public sealed class ResultPresenter
{
    private readonly IAnsiConsole _console;

    public ResultPresenter(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }

    /// <summary>
    /// Displays generated tests in a table.
    /// </summary>
    public void ShowGeneratedTests(IReadOnlyList<TestCase> tests)
    {
        if (tests.Count == 0)
        {
            _console.MarkupLine("[dim]No tests generated.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("ID").Centered())
            .AddColumn("Title")
            .AddColumn(new TableColumn("Priority").Centered())
            .AddColumn("Tags");

        foreach (var test in tests)
        {
            table.AddRow(
                $"[cyan]{Markup.Escape(test.Id)}[/]",
                TruncateTitle(test.Title, 50),
                GetPriorityMarkup(test.Priority),
                FormatTags(test.Tags));
        }

        _console.Write(table);
    }

    /// <summary>
    /// Displays existing tests matching a focus area.
    /// </summary>
    public void ShowExistingTests(IReadOnlyList<TestCase> tests, string? focusArea = null)
    {
        if (tests.Count == 0)
        {
            _console.MarkupLine("[dim]No existing tests found.[/]");
            return;
        }

        var title = focusArea is not null
            ? $"Existing {focusArea} tests"
            : "Existing tests";

        _console.MarkupLine($"{OutputSymbols.InfoMarkup} {title}: {tests.Count}");

        var table = new Table()
            .Border(TableBorder.Simple)
            .HideHeaders()
            .AddColumn("ID")
            .AddColumn("Title");

        foreach (var test in tests.Take(10))
        {
            table.AddRow(
                $"[dim]{Markup.Escape(test.Id)}[/]",
                $"[dim]{TruncateTitle(test.Title, 60)}[/]");
        }

        _console.Write(table);

        if (tests.Count > 10)
        {
            _console.MarkupLine($"[dim]  ... and {tests.Count - 10} more[/]");
        }
    }

    /// <summary>
    /// Displays suite summaries for selection.
    /// </summary>
    public void ShowSuites(IReadOnlyList<SuiteSummary> suites)
    {
        if (suites.Count == 0)
        {
            _console.MarkupLine("[dim]No suites found.[/]");
            return;
        }

        foreach (var suite in suites)
        {
            var lastUpdated = suite.LastUpdated.HasValue
                ? $", last updated {FormatTimeAgo(suite.LastUpdated.Value)}"
                : "";

            _console.MarkupLine($"  [cyan]{Markup.Escape(suite.Name)}[/] ({suite.TestCount} tests{lastUpdated})");
        }
    }

    /// <summary>
    /// Displays update results summary.
    /// </summary>
    public void ShowUpdateSummary(UpdateResult result)
    {
        _console.WriteLine();
        _console.MarkupLine("Results:");
        _console.MarkupLine($"  {OutputSymbols.SuccessMarkup} {result.UpToDate} up to date");

        if (result.Updated > 0)
        {
            _console.MarkupLine($"  {OutputSymbols.WarningMarkup} {result.Updated} outdated — updated");
        }

        if (result.Orphaned > 0)
        {
            _console.MarkupLine($"  {OutputSymbols.ErrorMarkup} {result.Orphaned} orphaned — marked with WARNING header");
        }

        if (result.Redundant > 0)
        {
            _console.MarkupLine($"  [dim]{OutputSymbols.Link}[/] {result.Redundant} redundant — flagged in index");
        }
    }

    /// <summary>
    /// Displays completion message with file paths.
    /// </summary>
    public void ShowCompletion(string suitePath, int testCount)
    {
        _console.MarkupLine($"{OutputSymbols.SuccessMarkup} Written to [cyan]{Markup.Escape(suitePath)}[/]");
        _console.MarkupLine($"{OutputSymbols.SuccessMarkup} Index updated");
    }

    private static string TruncateTitle(string title, int maxLength)
    {
        if (title.Length <= maxLength)
        {
            return Markup.Escape(title);
        }

        return Markup.Escape(title[..(maxLength - 3)]) + "...";
    }

    private static string GetPriorityMarkup(Priority priority)
    {
        return priority switch
        {
            Priority.High => "[red]high[/]",
            Priority.Medium => "[yellow]medium[/]",
            Priority.Low => "[green]low[/]",
            _ => "[dim]unknown[/]"
        };
    }

    private static string FormatTags(IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            return "[dim]-[/]";
        }

        return string.Join(", ", tags.Select(t => Markup.Escape(t)));
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
