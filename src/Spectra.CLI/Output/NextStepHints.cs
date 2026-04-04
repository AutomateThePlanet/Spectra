using Spectra.CLI.Infrastructure;
using Spectre.Console;

namespace Spectra.CLI.Output;

/// <summary>
/// Context for selecting appropriate next-step hints.
/// </summary>
public sealed record HintContext
{
    /// <summary>Whether --auto-link was used in the current command.</summary>
    public bool HasAutoLink { get; init; }

    /// <summary>Whether coverage gaps were detected.</summary>
    public bool HasGaps { get; init; }

    /// <summary>Number of test suites in the repository.</summary>
    public int SuiteCount { get; init; }

    /// <summary>Number of validation errors found.</summary>
    public int ErrorCount { get; init; }

    /// <summary>Dashboard output path (for "open in browser" hint).</summary>
    public string? OutputPath { get; init; }

    /// <summary>Suite that was just generated/updated.</summary>
    public string? SuiteName { get; init; }
}

/// <summary>
/// Displays context-aware next-step suggestions after command completion.
/// </summary>
public static class NextStepHints
{
    /// <summary>
    /// Prints next-step hints if conditions allow (normal+ verbosity, interactive terminal).
    /// </summary>
    public static void Print(string commandName, bool success, VerbosityLevel verbosity, HintContext? context = null, OutputFormat outputFormat = OutputFormat.Human)
    {
        if (outputFormat == OutputFormat.Json)
            return;

        if (verbosity < VerbosityLevel.Normal)
            return;

        if (Console.IsOutputRedirected)
            return;

        var hints = GetHints(commandName, success, context ?? new HintContext());
        if (hints.Count == 0)
            return;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [grey]Next steps:[/]");
        foreach (var hint in hints)
        {
            AnsiConsole.MarkupLine($"  [grey]  {Markup.Escape(hint)}[/]");
        }
    }

    public static List<string> GetHints(string commandName, bool success, HintContext context)
    {
        return commandName.ToLowerInvariant() switch
        {
            "init" => GetInitHints(),
            "generate" => GetGenerateHints(success, context),
            "analyze" => GetAnalyzeHints(success, context),
            "dashboard" => GetDashboardHints(success, context),
            "validate" => GetValidateHints(success, context),
            "docs-index" => GetDocsIndexHints(),
            "index" => GetIndexHints(),
            _ => []
        };
    }

    private static List<string> GetInitHints() =>
    [
        "spectra ai generate           # Generate your first test suite",
        "spectra init-profile           # Configure generation preferences",
        "",
        "ℹ  Optional: Create a Copilot Space with your product documentation",
        "   for inline help during test execution.",
        "   See docs/copilot-spaces-setup.md"
    ];

    private static List<string> GetGenerateHints(bool success, HintContext context)
    {
        if (!success) return [];

        var hints = new List<string>
        {
            "spectra ai analyze --coverage  # Check coverage gaps"
        };

        if (context.SuiteName is not null)
        {
            hints.Add("spectra ai generate            # Interactive mode (pick another suite)");
        }

        return hints;
    }

    private static List<string> GetAnalyzeHints(bool success, HintContext context)
    {
        if (!success) return [];

        var hints = new List<string>();

        if (!context.HasAutoLink)
        {
            hints.Add("spectra ai analyze --coverage --auto-link  # Link automation code to tests");
        }

        hints.Add("spectra dashboard --output ./site           # Generate visual dashboard");

        if (context.HasGaps)
        {
            hints.Add("spectra ai generate                        # Fill coverage gaps");
        }

        return hints;
    }

    private static List<string> GetDashboardHints(bool success, HintContext context)
    {
        if (!success) return [];

        var path = context.OutputPath ?? "./site";
        return
        [
            $"Open {path}/index.html in your browser",
            "See docs/deployment/cloudflare-pages-setup.md for hosting"
        ];
    }

    private static List<string> GetValidateHints(bool success, HintContext context)
    {
        if (!success || context.ErrorCount > 0)
        {
            return
            [
                "Fix the errors above, then run:",
                "spectra validate               # Re-validate after fixes"
            ];
        }

        return
        [
            "spectra ai generate            # Generate more tests",
            "spectra index                  # Rebuild indexes if needed"
        ];
    }

    private static List<string> GetDocsIndexHints() =>
    [
        "spectra ai generate            # Generate tests from indexed docs"
    ];

    private static List<string> GetIndexHints() =>
    [
        "spectra validate               # Validate test files",
        "spectra ai generate            # Generate more tests"
    ];
}
