using System.Globalization;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Results;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Spectra.CLI.Output;

/// <summary>
/// Renders the Spec 040 Run Summary panel after a generate or update run.
/// Shows a run-context block (documents, behaviors, tests, duration) plus
/// a token-usage table grouped by (phase, model, provider), a TOTAL row,
/// and an estimated-cost line.
/// </summary>
public static class RunSummaryPresenter
{
    public static void Render(
        RunSummary? runSummary,
        TokenUsageReport? tokenUsage,
        VerbosityLevel verbosity,
        IAnsiConsole? console = null,
        Spectra.CLI.Services.RunErrorTracker? errorTracker = null,
        int? criticConcurrency = null)
    {
        if (verbosity == VerbosityLevel.Quiet) return;
        if (runSummary is null && tokenUsage is null && errorTracker is null) return;

        var target = console ?? AnsiConsole.Console;

        var body = new Rows(BuildChildren(runSummary, tokenUsage, errorTracker, criticConcurrency));
        var panel = new Panel(body)
            .Header("[bold]Run Summary[/]")
            .Border(BoxBorder.Rounded)
            .Expand();

        target.WriteLine();
        target.Write(panel);
        target.WriteLine();
    }

    private static IEnumerable<IRenderable> BuildChildren(
        RunSummary? runSummary,
        TokenUsageReport? tokenUsage,
        Spectra.CLI.Services.RunErrorTracker? errorTracker = null,
        int? criticConcurrency = null)
    {
        if (runSummary is not null)
        {
            yield return BuildContextGrid(runSummary, errorTracker, criticConcurrency);
        }
        else if (errorTracker is not null || criticConcurrency.HasValue)
        {
            yield return BuildErrorOnlyGrid(errorTracker, criticConcurrency);
        }

        if (tokenUsage is not null && tokenUsage.Total.Calls > 0)
        {
            yield return new Markup(" ");
            yield return new Markup("[bold]Token Usage[/]");
            yield return BuildTokenTable(tokenUsage);
            yield return new Markup($"[grey]Estimated cost:[/] {Markup.Escape(tokenUsage.CostDisplay)}");
        }
    }

    private static Grid BuildContextGrid(
        RunSummary r,
        Spectra.CLI.Services.RunErrorTracker? errorTracker = null,
        int? criticConcurrency = null)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(4))
            .AddColumn();

        if (r.DocumentsProcessed.HasValue)
            grid.AddRow("[grey]Documents processed[/]", r.DocumentsProcessed.Value.ToString(CultureInfo.InvariantCulture));

        if (r.BehaviorsIdentified.HasValue)
            grid.AddRow("[grey]Behaviors identified[/]", r.BehaviorsIdentified.Value.ToString(CultureInfo.InvariantCulture));

        if (r.TestsGenerated.HasValue)
        {
            var v = r.Verdicts;
            var verdictText = v is null
                ? ""
                : $"  [grey]({v.Grounded} grounded, {v.Partial} partial, {v.Hallucinated} rejected)[/]";
            grid.AddRow("[grey]Tests generated[/]", $"{r.TestsGenerated.Value}{verdictText}");
        }

        if (r.BatchSize.HasValue)
        {
            var batches = r.Batches.HasValue ? $"  [grey]({r.Batches.Value} batch{(r.Batches.Value == 1 ? "" : "es")})[/]" : "";
            grid.AddRow("[grey]Batch size[/]", $"{r.BatchSize.Value}{batches}");
        }

        if (r.TestsScanned.HasValue)
            grid.AddRow("[grey]Tests scanned[/]", r.TestsScanned.Value.ToString(CultureInfo.InvariantCulture));

        if (r.TestsUpdated.HasValue)
        {
            var breakdown = r.Classifications is null || r.Classifications.Count == 0
                ? ""
                : $"  [grey]({string.Join(", ", r.Classifications.Where(kv => kv.Value > 0).Select(kv => $"{kv.Value} {kv.Key}"))})[/]";
            grid.AddRow("[grey]Tests updated[/]", $"{r.TestsUpdated.Value}{breakdown}");
        }

        if (r.TestsUnchanged.HasValue)
            grid.AddRow("[grey]Tests unchanged[/]", r.TestsUnchanged.Value.ToString(CultureInfo.InvariantCulture));

        if (r.Chunks.HasValue)
            grid.AddRow("[grey]Chunks[/]", r.Chunks.Value.ToString(CultureInfo.InvariantCulture));

        grid.AddRow("[grey]Duration[/]", FormatDuration(r.DurationSeconds));

        // Spec 043: critic concurrency + per-run error counters.
        AppendErrorRows(grid, errorTracker, criticConcurrency);

        return grid;
    }

    private static Grid BuildErrorOnlyGrid(
        Spectra.CLI.Services.RunErrorTracker? errorTracker,
        int? criticConcurrency)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(4))
            .AddColumn();
        AppendErrorRows(grid, errorTracker, criticConcurrency);
        return grid;
    }

    private static void AppendErrorRows(
        Grid grid,
        Spectra.CLI.Services.RunErrorTracker? errorTracker,
        int? criticConcurrency)
    {
        if (criticConcurrency.HasValue)
        {
            grid.AddRow("[grey]Critic concurrency[/]",
                criticConcurrency.Value.ToString(CultureInfo.InvariantCulture));
        }
        if (errorTracker is not null)
        {
            grid.AddRow("[grey]Errors[/]", errorTracker.Errors.ToString(CultureInfo.InvariantCulture));
            var rl = errorTracker.RateLimits;
            var rlText = rl > 0
                ? $"{rl}  [yellow](consider reducing ai.critic.max_concurrent)[/]"
                : rl.ToString(CultureInfo.InvariantCulture);
            grid.AddRow("[grey]Rate limits[/]", rlText);
        }
    }

    private static Table BuildTokenTable(TokenUsageReport report)
    {
        var table = new Table()
            .Border(TableBorder.MinimalHeavyHead)
            .AddColumn("Phase")
            .AddColumn("Model")
            .AddColumn(new TableColumn("Calls").RightAligned())
            .AddColumn(new TableColumn("Tokens In").RightAligned())
            .AddColumn(new TableColumn("Tokens Out").RightAligned())
            .AddColumn(new TableColumn("Total").RightAligned())
            .AddColumn(new TableColumn("Time").RightAligned());

        foreach (var row in report.Phases)
        {
            table.AddRow(
                Markup.Escape(Capitalize(row.Phase)),
                Markup.Escape(string.IsNullOrEmpty(row.Model) ? "-" : row.Model),
                row.Calls.ToString("N0", CultureInfo.InvariantCulture),
                FormatTokens(row.TokensIn),
                FormatTokens(row.TokensOut),
                FormatTokens(row.TotalTokens),
                FormatDuration(row.ElapsedSeconds));
        }

        var total = report.Total;
        table.AddRow(
            "[bold]TOTAL[/]",
            "",
            $"[bold]{total.Calls:N0}[/]",
            $"[bold]{FormatTokens(total.TokensIn)}[/]",
            $"[bold]{FormatTokens(total.TokensOut)}[/]",
            $"[bold]{FormatTokens(total.TotalTokens)}[/]",
            $"[bold]{FormatDuration(total.ElapsedSeconds)}[/]");

        return table;
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string FormatTokens(int n) =>
        n == 0 ? "-" : n.ToString("N0", CultureInfo.InvariantCulture);

    private static string FormatDuration(double seconds)
    {
        if (seconds <= 0) return "-";
        if (seconds < 60) return $"{seconds:F1}s";

        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h{ts.Minutes:D2}m{ts.Seconds:D2}s";
        return $"{ts.Minutes}m{ts.Seconds:D2}s";
    }
}
