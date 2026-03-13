using Spectre.Console;
using Spectra.Core.Models;

namespace Spectra.CLI.Review;

/// <summary>
/// Presents review summary and options using Spectre.Console.
/// </summary>
public sealed class ReviewPresenter
{
    private readonly IAnsiConsole _console;

    public ReviewPresenter(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }

    /// <summary>
    /// Shows the review summary panel.
    /// </summary>
    public void ShowSummary(QueueSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold blue]Test Generation Summary[/]")
            .AddColumn("Category")
            .AddColumn(new TableColumn("Count").RightAligned());

        table.AddRow("[green]Valid[/]", summary.ValidCount.ToString());
        table.AddRow("[yellow]Duplicates[/]", summary.DuplicateCount.ToString());
        table.AddRow("[red]Invalid[/]", summary.InvalidCount.ToString());
        table.AddEmptyRow();
        table.AddRow("[bold]Total[/]", $"[bold]{summary.TotalCount}[/]");

        _console.Write(table);
        _console.WriteLine();
    }

    /// <summary>
    /// Shows test details in a panel.
    /// </summary>
    public void ShowTestDetails(PendingTest pendingTest)
    {
        ArgumentNullException.ThrowIfNull(pendingTest);

        var test = pendingTest.Test;
        var status = GetStatusMarkup(pendingTest.Status);

        var panel = new Panel(new Rows(
            new Markup($"[bold]ID:[/] {test.Id}"),
            new Markup($"[bold]Status:[/] {status}"),
            new Markup($"[bold]Priority:[/] {test.Priority}"),
            new Markup($"[bold]Tags:[/] {string.Join(", ", test.Tags)}"),
            new Rule().RuleStyle("dim"),
            new Markup($"[bold]Steps:[/]"),
            new Markup(FormatSteps(test.Steps)),
            new Rule().RuleStyle("dim"),
            new Markup($"[bold]Expected Result:[/]"),
            new Markup(Markup.Escape(test.ExpectedResult))
        ))
        {
            Header = new PanelHeader($"[bold]{Markup.Escape(test.Title)}[/]"),
            Border = BoxBorder.Rounded
        };

        _console.Write(panel);

        if (pendingTest.Status == PendingTestStatus.Duplicate && pendingTest.DuplicateOf is not null)
        {
            _console.MarkupLine($"[yellow]⚠ Similar to: {Markup.Escape(pendingTest.DuplicateOf)} ({pendingTest.DuplicateSimilarity:P0})[/]");
        }

        if (pendingTest.ValidationErrors?.Count > 0)
        {
            foreach (var error in pendingTest.ValidationErrors)
            {
                _console.MarkupLine($"[red]✗ {Markup.Escape(error.Message)}[/]");
            }
        }

        _console.WriteLine();
    }

    /// <summary>
    /// Prompts for review action.
    /// </summary>
    public ReviewAction PromptAction(PendingTest pendingTest)
    {
        var choices = new List<string>
        {
            "Accept",
            "Reject",
            "Edit",
            "Skip",
            "Accept all valid",
            "Reject all duplicates",
            "Abort"
        };

        var choice = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
                .AddChoices(choices));

        return choice switch
        {
            "Accept" => ReviewAction.Accept,
            "Reject" => ReviewAction.Reject,
            "Edit" => ReviewAction.Edit,
            "Skip" => ReviewAction.Skip,
            "Accept all valid" => ReviewAction.AcceptAllValid,
            "Reject all duplicates" => ReviewAction.RejectAllDuplicates,
            "Abort" => ReviewAction.Abort,
            _ => ReviewAction.Skip
        };
    }

    /// <summary>
    /// Shows completion message.
    /// </summary>
    public void ShowCompletion(int accepted, int rejected, int skipped)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold green]Review Complete[/]")
            .AddColumn("Result")
            .AddColumn(new TableColumn("Count").RightAligned());

        table.AddRow("[green]Accepted[/]", accepted.ToString());
        table.AddRow("[red]Rejected[/]", rejected.ToString());
        table.AddRow("[dim]Skipped[/]", skipped.ToString());

        _console.Write(table);
    }

    /// <summary>
    /// Shows a progress bar while writing tests.
    /// </summary>
    public async Task ShowProgressAsync(
        string description,
        int total,
        Func<Action<int>, Task> action)
    {
        await _console.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask(description, maxValue: total);

                await action(increment =>
                {
                    task.Increment(increment);
                });
            });
    }

    /// <summary>
    /// Shows a status message with spinner.
    /// </summary>
    public async Task ShowStatusAsync(string message, Func<Task> action)
    {
        await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync(message, async _ => await action());
    }

    /// <summary>
    /// Shows an error message.
    /// </summary>
    public void ShowError(string message)
    {
        _console.MarkupLine($"[red]✗ Error:[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Shows a success message.
    /// </summary>
    public void ShowSuccess(string message)
    {
        _console.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Shows a warning message.
    /// </summary>
    public void ShowWarning(string message)
    {
        _console.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Prompts for confirmation.
    /// </summary>
    public bool Confirm(string message)
    {
        return _console.Confirm(message);
    }

    private static string GetStatusMarkup(PendingTestStatus status)
    {
        return status switch
        {
            PendingTestStatus.Valid => "[green]Valid[/]",
            PendingTestStatus.Duplicate => "[yellow]Duplicate[/]",
            PendingTestStatus.Invalid => "[red]Invalid[/]",
            _ => "[dim]Unknown[/]"
        };
    }

    private static string FormatSteps(IReadOnlyList<string> steps)
    {
        if (steps.Count == 0)
        {
            return "[dim]No steps[/]";
        }

        return string.Join("\n", steps.Select((s, i) => $"  {i + 1}. {Markup.Escape(s)}"));
    }
}

/// <summary>
/// Actions available during review.
/// </summary>
public enum ReviewAction
{
    Accept,
    Reject,
    Edit,
    Skip,
    AcceptAllValid,
    RejectAllDuplicates,
    Abort
}
