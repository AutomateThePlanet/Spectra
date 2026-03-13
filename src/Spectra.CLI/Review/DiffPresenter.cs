using Spectre.Console;
using Spectra.Core.Models;

namespace Spectra.CLI.Review;

/// <summary>
/// Presents test changes in diff format using Spectre.Console.
/// </summary>
public sealed class DiffPresenter
{
    private readonly IAnsiConsole _console;

    public DiffPresenter(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }

    /// <summary>
    /// Shows a diff between original and proposed test.
    /// </summary>
    public void ShowDiff(TestCase original, TestCase proposed)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(proposed);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold]Changes to {Markup.Escape(original.Id)}[/]")
            .AddColumn("[red]Original[/]")
            .AddColumn("[green]Proposed[/]");

        // Title
        if (original.Title != proposed.Title)
        {
            table.AddRow(
                $"[dim]Title:[/] [red]{Markup.Escape(original.Title)}[/]",
                $"[dim]Title:[/] [green]{Markup.Escape(proposed.Title)}[/]");
        }

        // Priority
        if (original.Priority != proposed.Priority)
        {
            table.AddRow(
                $"[dim]Priority:[/] [red]{original.Priority}[/]",
                $"[dim]Priority:[/] [green]{proposed.Priority}[/]");
        }

        // Steps
        var stepsChanged = !original.Steps.SequenceEqual(proposed.Steps);
        if (stepsChanged)
        {
            table.AddRow(
                FormatSteps(original.Steps, "red"),
                FormatSteps(proposed.Steps, "green"));
        }

        // Expected Result
        if (original.ExpectedResult != proposed.ExpectedResult)
        {
            table.AddRow(
                $"[dim]Expected:[/]\n[red]{Markup.Escape(original.ExpectedResult)}[/]",
                $"[dim]Expected:[/]\n[green]{Markup.Escape(proposed.ExpectedResult)}[/]");
        }

        // Tags
        var tagsChanged = !original.Tags.OrderBy(t => t).SequenceEqual(proposed.Tags.OrderBy(t => t));
        if (tagsChanged)
        {
            table.AddRow(
                $"[dim]Tags:[/] [red]{string.Join(", ", original.Tags)}[/]",
                $"[dim]Tags:[/] [green]{string.Join(", ", proposed.Tags)}[/]");
        }

        _console.Write(table);
        _console.WriteLine();
    }

    /// <summary>
    /// Shows an update proposal summary.
    /// </summary>
    public void ShowProposal(UpdateProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var classification = proposal.Classification;
        var color = GetClassificationColor(classification);

        var panel = new Panel(new Rows(
            new Markup($"[bold]Test:[/] {Markup.Escape(proposal.OriginalTest.Id)} - {Markup.Escape(proposal.OriginalTest.Title)}"),
            new Markup($"[bold]Status:[/] [{color}]{classification}[/]"),
            new Markup($"[bold]Confidence:[/] {proposal.Confidence:P0}"),
            new Markup($"[bold]Reason:[/] {Markup.Escape(proposal.Reason)}")
        ))
        {
            Header = new PanelHeader($"[{color}]Update Proposal[/]"),
            Border = BoxBorder.Rounded
        };

        _console.Write(panel);

        if (proposal.ProposedTest is not null)
        {
            ShowDiff(proposal.OriginalTest, proposal.ProposedTest);
        }

        if (proposal.Changes.Count > 0)
        {
            ShowChanges(proposal.Changes);
        }
    }

    /// <summary>
    /// Shows a list of proposed changes.
    /// </summary>
    public void ShowChanges(IReadOnlyList<ProposedChange> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Field")
            .AddColumn("Change")
            .AddColumn("Details");

        foreach (var change in changes)
        {
            var changeType = change.Type switch
            {
                ChangeType.Modified => "[yellow]Modified[/]",
                ChangeType.Added => "[green]Added[/]",
                ChangeType.Removed => "[red]Removed[/]",
                ChangeType.ItemAdded => "[green]+Item[/]",
                ChangeType.ItemRemoved => "[red]-Item[/]",
                ChangeType.Reordered => "[blue]Reordered[/]",
                _ => "[dim]Unknown[/]"
            };

            var details = change.Type switch
            {
                ChangeType.Modified => $"[red]{Markup.Escape(change.OldValue ?? "")}[/] → [green]{Markup.Escape(change.NewValue ?? "")}[/]",
                ChangeType.Added => $"[green]{Markup.Escape(change.NewValue ?? "")}[/]",
                ChangeType.Removed => $"[red]{Markup.Escape(change.OldValue ?? "")}[/]",
                _ => change.Reason ?? ""
            };

            table.AddRow(Markup.Escape(change.Field), changeType, details);
        }

        _console.Write(table);
        _console.WriteLine();
    }

    /// <summary>
    /// Shows a summary of all proposals.
    /// </summary>
    public void ShowSummary(IReadOnlyList<UpdateProposal> proposals)
    {
        var upToDate = proposals.Count(p => p.Classification == UpdateClassification.UpToDate);
        var outdated = proposals.Count(p => p.Classification == UpdateClassification.Outdated);
        var orphaned = proposals.Count(p => p.Classification == UpdateClassification.Orphaned);
        var redundant = proposals.Count(p => p.Classification == UpdateClassification.Redundant);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold blue]Update Analysis Summary[/]")
            .AddColumn("Status")
            .AddColumn(new TableColumn("Count").RightAligned());

        table.AddRow("[green]Up to Date[/]", upToDate.ToString());
        table.AddRow("[yellow]Outdated[/]", outdated.ToString());
        table.AddRow("[red]Orphaned[/]", orphaned.ToString());
        table.AddRow("[dim]Redundant[/]", redundant.ToString());
        table.AddEmptyRow();
        table.AddRow("[bold]Total[/]", $"[bold]{proposals.Count}[/]");

        _console.Write(table);
        _console.WriteLine();
    }

    private static string FormatSteps(IReadOnlyList<string> steps, string color)
    {
        if (steps.Count == 0)
        {
            return $"[dim]No steps[/]";
        }

        var formatted = steps.Select((s, i) => $"  {i + 1}. [{color}]{Markup.Escape(s)}[/]");
        return $"[dim]Steps:[/]\n{string.Join("\n", formatted)}";
    }

    private static string GetClassificationColor(UpdateClassification classification)
    {
        return classification switch
        {
            UpdateClassification.UpToDate => "green",
            UpdateClassification.Outdated => "yellow",
            UpdateClassification.Orphaned => "red",
            UpdateClassification.Redundant => "dim",
            _ => "white"
        };
    }
}
