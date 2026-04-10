using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Infrastructure;
using Spectre.Console;

namespace Spectra.CLI.Output;

/// <summary>
/// Displays behavior analysis results using Spectre.Console.
/// </summary>
public static class AnalysisPresenter
{
    private static readonly Dictionary<string, string> CategoryLabels = new()
    {
        ["happy_path"] = "happy path flows",
        ["negative"] = "negative / error scenarios",
        ["edge_case"] = "edge cases / boundary conditions",
        ["boundary"] = "boundary conditions",
        ["error_handling"] = "error handling",
        ["security"] = "security / permission checks",
        ["performance"] = "performance / load scenarios",
        ["uncategorized"] = "uncategorized"
    };

    private static readonly Dictionary<string, string> TechniqueLabels = new()
    {
        ["BVA"] = "Boundary Value Analysis",
        ["EP"] = "Equivalence Partitioning",
        ["DT"] = "Decision Table",
        ["ST"] = "State Transition",
        ["EG"] = "Error Guessing",
        ["UC"] = "Use Case"
    };

    /// <summary>Fixed display order for technique breakdown rendering.</summary>
    private static readonly string[] TechniqueDisplayOrder = ["BVA", "EP", "DT", "ST", "EG", "UC"];

    private static string FormatCategoryLabel(string id) =>
        CategoryLabels.TryGetValue(id, out var label)
            ? label
            : id.Replace('_', ' ').Replace('-', ' ');

    private static string FormatTechniqueLabel(string code) =>
        TechniqueLabels.TryGetValue(code, out var label) ? label : code;

    /// <summary>
    /// Displays the categorized behavior breakdown from analysis.
    /// </summary>
    public static void DisplayBreakdown(BehaviorAnalysisResult result, OutputFormat outputFormat = OutputFormat.Human)
    {
        if (outputFormat == OutputFormat.Json) return;
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
            var label = FormatCategoryLabel(category);
            AnsiConsole.MarkupLine($"    [grey]•[/] {count} {label}");
        }

        if (result.TechniqueBreakdown.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [bold]Technique Breakdown[/]");
            // Render known techniques in fixed order, then any unknown ones alphabetically.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in TechniqueDisplayOrder)
            {
                if (result.TechniqueBreakdown.TryGetValue(code, out var count) && count > 0)
                {
                    AnsiConsole.MarkupLine($"    [grey]•[/] {count} {FormatTechniqueLabel(code)}");
                    seen.Add(code);
                }
            }
            foreach (var (code, count) in result.TechniqueBreakdown
                         .Where(kvp => !seen.Contains(kvp.Key) && kvp.Value > 0)
                         .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"    [grey]•[/] {count} {FormatTechniqueLabel(code)}");
            }
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
        IReadOnlyList<string>? generatedCategories = null,
        OutputFormat outputFormat = OutputFormat.Human)
    {
        if (outputFormat == OutputFormat.Json) return;

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
            var label = FormatCategoryLabel(category);
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
    public static void DisplayAllCovered(int totalBehaviors, OutputFormat outputFormat = OutputFormat.Human)
    {
        if (outputFormat == OutputFormat.Json) return;
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
