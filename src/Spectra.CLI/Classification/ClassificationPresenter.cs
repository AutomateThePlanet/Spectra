using Spectre.Console;
using Spectra.Core.Models;
using Spectra.Core.Update;
using Spectra.CLI.Output;

namespace Spectra.CLI.Classification;

/// <summary>
/// Presents test classification results in a formatted console output.
/// </summary>
public sealed class ClassificationPresenter
{
    private readonly IAnsiConsole _console;

    public ClassificationPresenter(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }

    /// <summary>
    /// Shows a summary of classification results.
    /// </summary>
    public void ShowSummary(IReadOnlyList<ClassificationResult> results)
    {
        var upToDate = results.Count(r => r.Classification == UpdateClassification.UpToDate);
        var outdated = results.Count(r => r.Classification == UpdateClassification.Outdated);
        var orphaned = results.Count(r => r.Classification == UpdateClassification.Orphaned);
        var redundant = results.Count(r => r.Classification == UpdateClassification.Redundant);

        _console.MarkupLine($"{OutputSymbols.InfoMarkup} Classification Results:");
        _console.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Status")
            .AddColumn(new TableColumn("Count").RightAligned());

        table.AddRow($"[green]{OutputSymbols.Success}[/] Up to date", $"[green]{upToDate}[/]");
        table.AddRow($"[yellow]{OutputSymbols.Warning}[/] Outdated", $"[yellow]{outdated}[/]");
        table.AddRow($"[red]{OutputSymbols.Error}[/] Orphaned", $"[red]{orphaned}[/]");
        table.AddRow($"[blue]≈[/] Redundant", $"[blue]{redundant}[/]");

        _console.Write(table);
        _console.WriteLine();
    }

    /// <summary>
    /// Shows details for outdated tests.
    /// </summary>
    public void ShowOutdated(IReadOnlyList<ClassificationResult> results)
    {
        var outdated = results.Where(r => r.Classification == UpdateClassification.Outdated).ToList();

        if (outdated.Count == 0)
        {
            return;
        }

        _console.MarkupLine($"{OutputSymbols.WarningMarkup} Outdated tests:");
        _console.WriteLine();

        foreach (var result in outdated)
        {
            _console.MarkupLine($"  [yellow]{result.Test.Id}[/]: {Markup.Escape(result.Test.Title)}");
            _console.MarkupLine($"    [dim]{Markup.Escape(result.Reason)}[/]");
            _console.MarkupLine($"    Confidence: {result.Confidence:P0}");
        }

        _console.WriteLine();
    }

    /// <summary>
    /// Shows details for orphaned tests.
    /// </summary>
    public void ShowOrphaned(IReadOnlyList<ClassificationResult> results)
    {
        var orphaned = results.Where(r => r.Classification == UpdateClassification.Orphaned).ToList();

        if (orphaned.Count == 0)
        {
            return;
        }

        _console.MarkupLine($"{OutputSymbols.ErrorMarkup} Orphaned tests (source docs removed or renamed):");
        _console.WriteLine();

        foreach (var result in orphaned)
        {
            _console.MarkupLine($"  [red]{result.Test.Id}[/]: {Markup.Escape(result.Test.Title)}");
            _console.MarkupLine($"    [dim]{Markup.Escape(result.Reason)}[/]");

            if (result.Test.SourceRefs.Count > 0)
            {
                _console.MarkupLine($"    Missing refs: [dim]{string.Join(", ", result.Test.SourceRefs)}[/]");
            }
        }

        _console.WriteLine();
        _console.MarkupLine("[dim]Tip: Use 'git diff' to see recent doc changes, or delete orphaned tests[/]");
        _console.WriteLine();
    }

    /// <summary>
    /// Shows details for redundant tests.
    /// </summary>
    public void ShowRedundant(IReadOnlyList<ClassificationResult> results)
    {
        var redundant = results.Where(r => r.Classification == UpdateClassification.Redundant).ToList();

        if (redundant.Count == 0)
        {
            return;
        }

        _console.MarkupLine($"[blue]≈[/] Redundant tests (duplicates coverage):");
        _console.WriteLine();

        foreach (var result in redundant)
        {
            _console.MarkupLine($"  [blue]{result.Test.Id}[/]: {Markup.Escape(result.Test.Title)}");
            if (!string.IsNullOrEmpty(result.RelatedTestId))
            {
                _console.MarkupLine($"    Duplicates: [cyan]{result.RelatedTestId}[/]");
            }
            _console.MarkupLine($"    Similarity: {result.Confidence:P0}");
        }

        _console.WriteLine();
    }

    /// <summary>
    /// Shows update completion message.
    /// </summary>
    public void ShowUpdateComplete(int updated, int marked, int deleted)
    {
        _console.WriteLine();
        _console.MarkupLine($"{OutputSymbols.SuccessMarkup} Update complete:");
        _console.MarkupLine($"  [green]{updated}[/] tests updated");

        if (marked > 0)
        {
            _console.MarkupLine($"  [yellow]{marked}[/] tests marked as orphaned");
        }

        if (deleted > 0)
        {
            _console.MarkupLine($"  [red]{deleted}[/] tests deleted");
        }
    }

    /// <summary>
    /// Shows a single classification result.
    /// </summary>
    public void ShowResult(ClassificationResult result)
    {
        var statusColor = result.Classification switch
        {
            UpdateClassification.UpToDate => "green",
            UpdateClassification.Outdated => "yellow",
            UpdateClassification.Orphaned => "red",
            UpdateClassification.Redundant => "blue",
            _ => "white"
        };

        var symbol = result.Classification switch
        {
            UpdateClassification.UpToDate => OutputSymbols.Success,
            UpdateClassification.Outdated => OutputSymbols.Warning,
            UpdateClassification.Orphaned => OutputSymbols.Error,
            UpdateClassification.Redundant => "≈",
            _ => "?"
        };

        _console.MarkupLine($"[{statusColor}]{symbol}[/] [{statusColor}]{result.Test.Id}[/]: {Markup.Escape(result.Test.Title)}");
        _console.MarkupLine($"  [dim]{Markup.Escape(result.Reason)}[/]");
    }
}
