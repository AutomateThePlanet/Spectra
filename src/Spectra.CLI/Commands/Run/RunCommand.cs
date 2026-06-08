using System.CommandLine;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Run;

/// <summary>
/// Spec 065: <c>spectra run …</c> — the first-class CLI execution surface. Thin adapters over the
/// shared <see cref="Spectra.MCP.Execution.ExecutionEngine"/> (in <c>Spectra.Execution</c>),
/// mirroring the MCP execution tools one-to-one over the same <c>.execution/spectra.db</c>.
/// </summary>
public sealed class RunCommand : Command
{
    public RunCommand() : base("run", "Drive deterministic test execution from the CLI (no MCP server required)")
    {
        AddCommand(BuildStart());
        AddCommand(BuildStatus());
        AddCommand(BuildShow());
        AddCommand(BuildAdvance());
        AddCommand(BuildSkip());
        AddCommand(BuildNote());
        AddCommand(BuildRetest());
        AddCommand(BuildBulkRecord());
        AddCommand(BuildFinalize());
        AddCommand(BuildSimpleRunId("pause", "Pause an active run"));
        AddCommand(BuildSimpleRunId("resume", "Resume a paused run"));
        AddCommand(BuildSimpleRunId("cancel", "Cancel a run"));
        AddCommand(BuildCancelAll());
        AddCommand(BuildListSuites());
        AddCommand(BuildListActive());
        AddCommand(BuildHistory());
        AddCommand(BuildSummary());
        AddCommand(BuildSelections());
        AddCommand(BuildScreenshot());
        AddCommand(BuildScreenshotClipboard());
    }

    private static RunHandler Handler(System.CommandLine.Invocation.InvocationContext ctx) => new(
        ctx.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption),
        ctx.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption));

    private static Command BuildStart()
    {
        var cmd = new Command("start", "Start a new execution run for a suite, test IDs, or saved selection");
        var suite = new Argument<string?>("suite", () => null, "Suite name");
        var priorities = new Option<string[]>("--priorities", "Filter by priority (high/medium/low)") { AllowMultipleArgumentsPerToken = true };
        var tags = new Option<string[]>("--tags", "Filter by tag") { AllowMultipleArgumentsPerToken = true };
        var components = new Option<string[]>("--components", "Filter by component") { AllowMultipleArgumentsPerToken = true };
        var testIds = new Option<string[]>("--test-ids", "Run specific test IDs") { AllowMultipleArgumentsPerToken = true };
        var selection = new Option<string?>("--selection", "Run a saved selection by name");
        var environment = new Option<string?>("--environment", "Target environment label");
        cmd.AddArgument(suite);
        cmd.AddOption(priorities); cmd.AddOption(tags); cmd.AddOption(components);
        cmd.AddOption(testIds); cmd.AddOption(selection); cmd.AddOption(environment);
        cmd.SetHandler(async ctx =>
        {
            ctx.ExitCode = await Handler(ctx).StartAsync(
                ctx.ParseResult.GetValueForArgument(suite),
                ctx.ParseResult.GetValueForOption(priorities),
                ctx.ParseResult.GetValueForOption(tags),
                ctx.ParseResult.GetValueForOption(components),
                ctx.ParseResult.GetValueForOption(testIds),
                ctx.ParseResult.GetValueForOption(selection),
                ctx.ParseResult.GetValueForOption(environment),
                ctx.GetCancellationToken());
        });
        return cmd;
    }

    private static Command BuildStatus()
    {
        var cmd = new Command("status", "Show run status and the next actionable test");
        var runId = new Argument<string?>("run-id", () => null, "Run id (defaults to the active run)");
        cmd.AddArgument(runId);
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).StatusAsync(ctx.ParseResult.GetValueForArgument(runId), ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildShow()
    {
        var cmd = new Command("show", "Show full details of the current/next test (or a specific one)");
        var runId = new Argument<string?>("run-id", () => null, "Run id (defaults to the active run)");
        var testId = new Option<string?>("--test-id", "Show a specific test by id");
        var handle = new Option<string?>("--handle", "Show a specific test by handle");
        cmd.AddArgument(runId); cmd.AddOption(testId); cmd.AddOption(handle);
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).ShowAsync(
            ctx.ParseResult.GetValueForArgument(runId),
            ctx.ParseResult.GetValueForOption(testId),
            ctx.ParseResult.GetValueForOption(handle),
            ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildAdvance()
    {
        var cmd = new Command("advance", "Record a verdict for a test and get the next one");
        var handle = new Argument<string?>("handle", () => null, "Test handle (defaults to the in-progress/next test)");
        var status = new Option<string?>("--status", "Verdict: pass | fail | blocked | skip") { IsRequired = false };
        var notes = new Option<string?>("--notes", "Observations (required for fail/blocked/skip)");
        cmd.AddArgument(handle); cmd.AddOption(status); cmd.AddOption(notes);
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).AdvanceAsync(
            ctx.ParseResult.GetValueForArgument(handle),
            ctx.ParseResult.GetValueForOption(status),
            ctx.ParseResult.GetValueForOption(notes),
            ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildSkip()
    {
        var cmd = new Command("skip", "Skip the current test with a reason");
        var handle = new Argument<string?>("handle", () => null, "Test handle (defaults to the in-progress/next test)");
        var reason = new Option<string?>("--reason", "Reason for skipping (required)");
        var blocked = new Option<bool>("--blocked", "Mark as BLOCKED instead of SKIPPED");
        cmd.AddArgument(handle); cmd.AddOption(reason); cmd.AddOption(blocked);
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).SkipAsync(
            ctx.ParseResult.GetValueForArgument(handle),
            ctx.ParseResult.GetValueForOption(reason),
            ctx.ParseResult.GetValueForOption(blocked),
            ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildNote()
    {
        var cmd = new Command("note", "Append a note to a test without changing its status");
        var handle = new Argument<string?>("handle", () => null, "Test handle (defaults to the in-progress test)");
        var note = new Option<string?>("--note", "Note text (required)");
        cmd.AddArgument(handle); cmd.AddOption(note);
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).NoteAsync(
            ctx.ParseResult.GetValueForArgument(handle),
            ctx.ParseResult.GetValueForOption(note),
            ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildRetest()
    {
        var cmd = new Command("retest", "Requeue a test for another attempt");
        var runId = new Argument<string?>("run-id", () => null, "Run id (defaults to the active run)");
        var testId = new Option<string?>("--test-id", "Test id to requeue (required)");
        cmd.AddArgument(runId); cmd.AddOption(testId);
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).RetestAsync(
            ctx.ParseResult.GetValueForArgument(runId),
            ctx.ParseResult.GetValueForOption(testId),
            ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildBulkRecord()
    {
        var cmd = new Command("bulk-record", "Record the same verdict for many tests");
        var runId = new Argument<string?>("run-id", () => null, "Run id (defaults to the active run)");
        var status = new Option<string?>("--status", "Verdict: pass | fail | blocked | skip");
        var remaining = new Option<bool>("--remaining", "Apply to all pending/in-progress tests");
        var testIds = new Option<string[]>("--test-ids", "Apply to specific test IDs") { AllowMultipleArgumentsPerToken = true };
        var reason = new Option<string?>("--reason", "Reason/notes");
        cmd.AddArgument(runId); cmd.AddOption(status); cmd.AddOption(remaining); cmd.AddOption(testIds); cmd.AddOption(reason);
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).BulkRecordAsync(
            ctx.ParseResult.GetValueForOption(status),
            ctx.ParseResult.GetValueForOption(remaining),
            ctx.ParseResult.GetValueForOption(testIds),
            ctx.ParseResult.GetValueForOption(reason),
            ctx.ParseResult.GetValueForArgument(runId),
            ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildFinalize()
    {
        var cmd = new Command("finalize", "Complete a run and generate reports");
        var runId = new Argument<string?>("run-id", () => null, "Run id (defaults to the active run)");
        var force = new Option<bool>("--force", "Finalize even if tests are still pending");
        cmd.AddArgument(runId); cmd.AddOption(force);
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).FinalizeAsync(
            ctx.ParseResult.GetValueForArgument(runId),
            ctx.ParseResult.GetValueForOption(force),
            ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildSimpleRunId(string name, string description)
    {
        var cmd = new Command(name, description);
        var runId = new Argument<string?>("run-id", () => null, "Run id (defaults to the active run)");
        cmd.AddArgument(runId);
        cmd.SetHandler(async ctx =>
        {
            var h = Handler(ctx);
            var id = ctx.ParseResult.GetValueForArgument(runId);
            ctx.ExitCode = name switch
            {
                "pause" => await h.PauseAsync(id, ctx.GetCancellationToken()),
                "resume" => await h.ResumeAsync(id, ctx.GetCancellationToken()),
                _ => await h.CancelAsync(id, ctx.GetCancellationToken()),
            };
        });
        return cmd;
    }

    private static Command BuildCancelAll()
    {
        var cmd = new Command("cancel-all", "Cancel every active run");
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).CancelAllAsync(ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildListSuites()
    {
        var cmd = new Command("list-suites", "List runnable suites");
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).ListSuitesAsync(ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildListActive()
    {
        var cmd = new Command("list-active", "List active runs");
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).ListActiveAsync(ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildHistory()
    {
        var cmd = new Command("history", "Show recent run history");
        var suite = new Option<string?>("--suite", "Filter by suite");
        cmd.AddOption(suite);
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).HistoryAsync(ctx.ParseResult.GetValueForOption(suite), ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildSummary()
    {
        var cmd = new Command("summary", "Show a status-count summary for a run");
        var runId = new Argument<string?>("run-id", () => null, "Run id (defaults to the active run)");
        cmd.AddArgument(runId);
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).SummaryAsync(ctx.ParseResult.GetValueForArgument(runId), ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildSelections()
    {
        var cmd = new Command("selections", "List saved selections");
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).SelectionsAsync(ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildScreenshot()
    {
        var cmd = new Command("screenshot", "Attach a screenshot file to the current/last test");
        var handle = new Argument<string?>("handle", () => null, "Test handle (defaults to the in-progress/last test)");
        var file = new Option<string?>("--file", "Path to the image file (required)");
        var caption = new Option<string?>("--caption", "Optional caption");
        cmd.AddArgument(handle); cmd.AddOption(file); cmd.AddOption(caption);
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).ScreenshotAsync(
            ctx.ParseResult.GetValueForArgument(handle),
            ctx.ParseResult.GetValueForOption(file),
            ctx.ParseResult.GetValueForOption(caption),
            ctx.GetCancellationToken()));
        return cmd;
    }

    private static Command BuildScreenshotClipboard()
    {
        var cmd = new Command("screenshot-clipboard", "Capture the clipboard image and attach it to the current/last test");
        var handle = new Argument<string?>("handle", () => null, "Test handle (defaults to the in-progress/last test)");
        var caption = new Option<string?>("--caption", "Optional caption");
        cmd.AddArgument(handle); cmd.AddOption(caption);
        cmd.SetHandler(async ctx => ctx.ExitCode = await Handler(ctx).ScreenshotClipboardAsync(
            ctx.ParseResult.GetValueForArgument(handle),
            ctx.ParseResult.GetValueForOption(caption),
            ctx.GetCancellationToken()));
        return cmd;
    }
}
