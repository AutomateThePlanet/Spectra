using Spectre.Console;
using Spectra.Core.Models;

namespace Spectra.CLI.Review;

/// <summary>
/// Handles the interactive update review flow.
/// </summary>
public sealed class UpdateReviewer
{
    private readonly IAnsiConsole _console;
    private readonly DiffPresenter _diffPresenter;

    public UpdateReviewer(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
        _diffPresenter = new DiffPresenter(_console);
    }

    /// <summary>
    /// Runs the interactive update review session.
    /// </summary>
    public UpdateReviewResult Review(IReadOnlyList<UpdateProposal> proposals)
    {
        ArgumentNullException.ThrowIfNull(proposals);

        if (proposals.Count == 0)
        {
            _console.MarkupLine("[yellow]No updates to review.[/]");
            return new UpdateReviewResult { Completed = true };
        }

        // Show summary
        _diffPresenter.ShowSummary(proposals);

        var toUpdate = new List<UpdateProposal>();
        var toDelete = new List<UpdateProposal>();
        var skipped = new List<UpdateProposal>();
        var aborted = false;

        // Filter proposals that need action
        var actionable = proposals
            .Where(p => p.Classification != UpdateClassification.UpToDate)
            .ToList();

        if (actionable.Count == 0)
        {
            _console.MarkupLine("[green]All tests are up to date![/]");
            return new UpdateReviewResult
            {
                Completed = true,
                UpToDateCount = proposals.Count
            };
        }

        _console.MarkupLine($"\n[bold]{actionable.Count}[/] tests need attention.\n");

        foreach (var proposal in actionable)
        {
            _diffPresenter.ShowProposal(proposal);

            var action = PromptAction(proposal);

            switch (action)
            {
                case UpdateAction.Apply:
                    if (proposal.ShouldDelete)
                    {
                        toDelete.Add(proposal);
                    }
                    else
                    {
                        toUpdate.Add(proposal);
                    }
                    break;

                case UpdateAction.Skip:
                    skipped.Add(proposal);
                    break;

                case UpdateAction.ApplyAll:
                    // Apply remaining
                    foreach (var remaining in actionable.Skip(actionable.IndexOf(proposal)))
                    {
                        if (remaining.ShouldDelete)
                        {
                            toDelete.Add(remaining);
                        }
                        else if (remaining.ShouldUpdate)
                        {
                            toUpdate.Add(remaining);
                        }
                    }
                    goto done;

                case UpdateAction.SkipAll:
                    skipped.AddRange(actionable.Skip(actionable.IndexOf(proposal)));
                    goto done;

                case UpdateAction.Abort:
                    if (_console.Confirm("Are you sure you want to abort?"))
                    {
                        aborted = true;
                        goto done;
                    }
                    break;
            }
        }

        done:

        // Show completion
        ShowCompletion(toUpdate.Count, toDelete.Count, skipped.Count);

        return new UpdateReviewResult
        {
            Completed = !aborted,
            Aborted = aborted,
            ToUpdate = toUpdate,
            ToDelete = toDelete,
            Skipped = skipped,
            UpToDateCount = proposals.Count(p => p.Classification == UpdateClassification.UpToDate)
        };
    }

    /// <summary>
    /// Auto-reviews all proposals without interaction.
    /// </summary>
    public UpdateReviewResult AutoReview(
        IReadOnlyList<UpdateProposal> proposals,
        bool applyOutdated = true,
        bool deleteOrphaned = false,
        bool deleteRedundant = false)
    {
        var toUpdate = new List<UpdateProposal>();
        var toDelete = new List<UpdateProposal>();
        var skipped = new List<UpdateProposal>();

        foreach (var proposal in proposals)
        {
            switch (proposal.Classification)
            {
                case UpdateClassification.Outdated when applyOutdated && proposal.ProposedTest is not null:
                    toUpdate.Add(proposal);
                    break;

                case UpdateClassification.Orphaned when deleteOrphaned:
                    toDelete.Add(proposal);
                    break;

                case UpdateClassification.Redundant when deleteRedundant:
                    toDelete.Add(proposal);
                    break;

                case UpdateClassification.UpToDate:
                    // No action needed
                    break;

                default:
                    skipped.Add(proposal);
                    break;
            }
        }

        return new UpdateReviewResult
        {
            Completed = true,
            ToUpdate = toUpdate,
            ToDelete = toDelete,
            Skipped = skipped,
            UpToDateCount = proposals.Count(p => p.Classification == UpdateClassification.UpToDate)
        };
    }

    private UpdateAction PromptAction(UpdateProposal proposal)
    {
        var choices = new List<string>();

        if (proposal.ShouldUpdate)
        {
            choices.Add("Apply update");
        }
        else if (proposal.ShouldDelete)
        {
            choices.Add("Delete test");
        }

        choices.AddRange([
            "Skip",
            "Apply all remaining",
            "Skip all remaining",
            "Abort"
        ]);

        var choice = _console.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
                .AddChoices(choices));

        return choice switch
        {
            "Apply update" => UpdateAction.Apply,
            "Delete test" => UpdateAction.Apply,
            "Skip" => UpdateAction.Skip,
            "Apply all remaining" => UpdateAction.ApplyAll,
            "Skip all remaining" => UpdateAction.SkipAll,
            "Abort" => UpdateAction.Abort,
            _ => UpdateAction.Skip
        };
    }

    private void ShowCompletion(int updated, int deleted, int skipped)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold green]Update Review Complete[/]")
            .AddColumn("Action")
            .AddColumn(new TableColumn("Count").RightAligned());

        table.AddRow("[green]To Update[/]", updated.ToString());
        table.AddRow("[red]To Delete[/]", deleted.ToString());
        table.AddRow("[dim]Skipped[/]", skipped.ToString());

        _console.Write(table);
    }
}

/// <summary>
/// Actions available during update review.
/// </summary>
public enum UpdateAction
{
    Apply,
    Skip,
    ApplyAll,
    SkipAll,
    Abort
}

/// <summary>
/// Result of an update review session.
/// </summary>
public sealed record UpdateReviewResult
{
    public required bool Completed { get; init; }
    public bool Aborted { get; init; }
    public IReadOnlyList<UpdateProposal> ToUpdate { get; init; } = [];
    public IReadOnlyList<UpdateProposal> ToDelete { get; init; } = [];
    public IReadOnlyList<UpdateProposal> Skipped { get; init; } = [];
    public int UpToDateCount { get; init; }
}
