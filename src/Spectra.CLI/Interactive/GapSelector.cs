using Spectre.Console;
using Spectra.Core.Models;
using Spectra.CLI.Output;

namespace Spectra.CLI.Interactive;

/// <summary>
/// Interactive gap selection for follow-up generation.
/// </summary>
public sealed class GapSelector
{
    private readonly IAnsiConsole _console;

    public GapSelector(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }

    /// <summary>
    /// Prompts user to generate more tests for remaining gaps.
    /// </summary>
    public GapSelectionResult PromptForMore(IReadOnlyList<CoverageGap> remainingGaps)
    {
        if (remainingGaps.Count == 0)
        {
            return new GapSelectionResult(GapAction.Done, []);
        }

        _console.MarkupLine($"{OutputSymbols.PromptMarkup} Generate tests for uncovered areas?");

        var choices = new[]
        {
            "Yes, generate all",
            "Let me pick",
            "No, I'm done"
        };

        var selected = _console.Prompt(
            new SelectionPrompt<string>()
                .AddChoices(choices)
                .HighlightStyle(Style.Parse("cyan")));

        _console.MarkupLine("└");

        return selected switch
        {
            "Yes, generate all" => new GapSelectionResult(GapAction.GenerateAll, remainingGaps.ToList()),
            "Let me pick" => PromptForSelection(remainingGaps),
            _ => new GapSelectionResult(GapAction.Done, [])
        };
    }

    private GapSelectionResult PromptForSelection(IReadOnlyList<CoverageGap> gaps)
    {
        var gapDescriptions = gaps
            .Select(g => g.Suggestion ?? Path.GetFileNameWithoutExtension(g.DocumentPath))
            .ToList();

        var selected = _console.Prompt(
            new MultiSelectionPrompt<string>()
                .Title($"{OutputSymbols.PromptMarkup} Select gaps to cover:")
                .AddChoices(gapDescriptions)
                .HighlightStyle(Style.Parse("cyan")));

        _console.MarkupLine("└");

        if (selected.Count == 0)
        {
            return new GapSelectionResult(GapAction.Done, []);
        }

        var selectedGaps = gaps
            .Where(g => selected.Contains(g.Suggestion ?? Path.GetFileNameWithoutExtension(g.DocumentPath)))
            .ToList();

        return new GapSelectionResult(GapAction.GenerateSelected, selectedGaps);
    }
}

/// <summary>
/// Action to take on gaps.
/// </summary>
public enum GapAction
{
    /// <summary>
    /// Generate tests for all remaining gaps.
    /// </summary>
    GenerateAll,

    /// <summary>
    /// Generate tests for selected gaps only.
    /// </summary>
    GenerateSelected,

    /// <summary>
    /// Done generating.
    /// </summary>
    Done
}

/// <summary>
/// Result of gap selection.
/// </summary>
public record GapSelectionResult(GapAction Action, List<CoverageGap> SelectedGaps);
