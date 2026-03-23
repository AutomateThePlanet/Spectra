using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Models;
using Spectre.Console;

namespace Spectra.CLI.Output;

/// <summary>
/// Displays behavior analysis results using Spectre.Console.
/// </summary>
public static class AnalysisPresenter
{
    private static readonly Dictionary<BehaviorCategory, string> CategoryLabels = new()
    {
        [BehaviorCategory.HappyPath] = "happy path flows",
        [BehaviorCategory.Negative] = "negative / error scenarios",
        [BehaviorCategory.EdgeCase] = "edge cases / boundary conditions",
        [BehaviorCategory.Security] = "security / permission checks",
        [BehaviorCategory.Performance] = "performance / load scenarios"
    };

    /// <summary>
    /// Displays the categorized behavior breakdown from analysis.
    /// </summary>
    public static void DisplayBreakdown(BehaviorAnalysisResult result)
    {
        if (Console.IsOutputRedirected)
            return;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"  Found [bold]{result.DocumentsAnalyzed}[/] documents ({result.TotalWords:N0} words total)");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"  Identified [bold]{result.TotalBehaviors}[/] testable behaviors:");

        foreach (var (category, count) in result.Breakdown.OrderByDescending(kvp => kvp.Value))
        {
            if (count <= 0) continue;
            var label = CategoryLabels.GetValueOrDefault(category, category.ToString());
            AnsiConsole.MarkupLine($"    [grey]•[/] {count} {label}");
        }

        if (result.AlreadyCovered > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(
                $"  [grey]{result.AlreadyCovered} already covered by existing tests.[/]");
            AnsiConsole.MarkupLine(
                $"  Recommended: [bold]{result.RecommendedCount}[/] new test cases.");
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays a gap notification after partial generation.
    /// Shows remaining uncovered behaviors by category.
    /// </summary>
    public static void DisplayGapNotification(
        BehaviorAnalysisResult analysis,
        int generatedCount,
        string suiteName,
        IReadOnlyList<BehaviorCategory>? generatedCategories = null)
    {
        var remaining = analysis.TotalBehaviors - analysis.AlreadyCovered - generatedCount;
        if (remaining <= 0)
            return;

        if (Console.IsOutputRedirected)
            return;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [cyan]{OutputSymbols.Info}[/] {remaining} testable behaviors not yet covered:");

        var remainingByCategory = analysis.GetRemainingByCategory(generatedCategories);
        foreach (var (category, count) in remainingByCategory.OrderByDescending(kvp => kvp.Value))
        {
            if (count <= 0) continue;
            var label = CategoryLabels.GetValueOrDefault(category, category.ToString());
            AnsiConsole.MarkupLine($"    [grey]•[/] {count} {label}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"  [grey]Next steps:[/]");
        AnsiConsole.MarkupLine(
            $"  [grey]  spectra ai generate --suite {Markup.Escape(suiteName)}    # Generate remaining tests[/]");
    }

    /// <summary>
    /// Displays a message when all behaviors are already covered.
    /// </summary>
    public static void DisplayAllCovered(int totalBehaviors)
    {
        if (Console.IsOutputRedirected)
            return;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"  {OutputSymbols.SuccessMarkup} All {totalBehaviors} identified behaviors are already covered by existing tests.");
        AnsiConsole.MarkupLine(
            $"  [grey]Consider running: spectra ai update    # Update existing tests[/]");
        AnsiConsole.WriteLine();
    }
}
