using Spectre.Console;
using Spectra.Core.Models;
using Spectra.CLI.Output;

namespace Spectra.CLI.Coverage;

/// <summary>
/// Displays coverage gaps with symbols and formatting.
/// </summary>
public sealed class GapPresenter
{
    private readonly IAnsiConsole _console;

    public GapPresenter(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }

    /// <summary>
    /// Displays coverage gaps remaining after generation.
    /// </summary>
    public void ShowRemainingGaps(IReadOnlyList<CoverageGap> gaps)
    {
        if (gaps.Count == 0)
        {
            _console.MarkupLine($"{OutputSymbols.InfoMarkup} All identified gaps are now covered.");
            return;
        }

        _console.WriteLine();
        _console.MarkupLine($"{OutputSymbols.InfoMarkup} Gaps still uncovered:");

        foreach (var gap in gaps.Take(10))
        {
            var severityColor = GetSeverityColor(gap.Severity);
            var bullet = $"[{severityColor}]•[/]";
            var description = gap.Suggestion ?? gap.Reason;

            _console.MarkupLine($"  {bullet} {Markup.Escape(description)}");
        }

        if (gaps.Count > 10)
        {
            _console.MarkupLine($"  [dim]... and {gaps.Count - 10} more[/]");
        }
    }

    /// <summary>
    /// Displays uncovered areas before generation.
    /// </summary>
    public void ShowUncoveredAreas(IReadOnlyList<CoverageGap> gaps)
    {
        if (gaps.Count == 0)
        {
            _console.MarkupLine($"{OutputSymbols.InfoMarkup} No uncovered areas found.");
            return;
        }

        _console.MarkupLine($"{OutputSymbols.InfoMarkup} Uncovered areas:");

        foreach (var gap in gaps.Take(10))
        {
            var description = GetGapDescription(gap);
            _console.MarkupLine($"  [dim]•[/] {Markup.Escape(description)}");
        }

        if (gaps.Count > 10)
        {
            _console.MarkupLine($"  [dim]... and {gaps.Count - 10} more[/]");
        }
    }

    /// <summary>
    /// Shows a "no gaps" message when all areas are covered.
    /// </summary>
    public void ShowAllCovered(string? focusArea = null)
    {
        if (string.IsNullOrEmpty(focusArea))
        {
            _console.MarkupLine($"{OutputSymbols.SuccessMarkup} No gaps identified. All documentation areas are covered.");
        }
        else
        {
            _console.MarkupLine($"{OutputSymbols.SuccessMarkup} No gaps identified for \"{Markup.Escape(focusArea)}\".");
            _console.MarkupLine($"  Consider exploring other areas or running with full coverage.");
        }
    }

    /// <summary>
    /// Shows summary of gap analysis.
    /// </summary>
    public void ShowGapSummary(int totalDocs, int coveredDocs, int gapCount)
    {
        var percentage = totalDocs > 0 ? (coveredDocs * 100 / totalDocs) : 0;

        _console.MarkupLine($"{OutputSymbols.InfoMarkup} Documentation coverage: {percentage}% ({coveredDocs}/{totalDocs} documents)");

        if (gapCount > 0)
        {
            _console.MarkupLine($"  [yellow]{gapCount} areas need test coverage[/]");
        }
    }

    private static string GetSeverityColor(GapSeverity severity)
    {
        return severity switch
        {
            GapSeverity.Critical => "red bold",
            GapSeverity.High => "red",
            GapSeverity.Medium => "yellow",
            GapSeverity.Low => "dim",
            _ => "dim"
        };
    }

    private static string GetGapDescription(CoverageGap gap)
    {
        if (!string.IsNullOrEmpty(gap.Section))
        {
            return $"{Path.GetFileName(gap.DocumentPath)}: {gap.Section}";
        }

        return Path.GetFileNameWithoutExtension(gap.DocumentPath);
    }
}
