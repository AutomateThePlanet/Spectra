using Spectra.CLI.Infrastructure;
using Spectre.Console;

namespace Spectra.CLI.Output;

/// <summary>
/// Reports progress using Spectre.Console spinners and status.
/// </summary>
public sealed class ProgressReporter
{
    private readonly IAnsiConsole _console;
    private readonly OutputFormat _outputFormat;
    private readonly VerbosityLevel _verbosity;

    /// <summary>
    /// Spec 041: set while a <see cref="ProgressTwoTaskAsync"/> block is
    /// active. Inner <see cref="StatusAsync(string, Func{Task})"/> calls
    /// short-circuit when this is true so they don't conflict with the
    /// outer Spectre live region.
    /// </summary>
    private bool _progressActive;

    public ProgressReporter(
        IAnsiConsole? console = null,
        OutputFormat outputFormat = OutputFormat.Human,
        VerbosityLevel verbosity = VerbosityLevel.Normal)
    {
        _console = console ?? AnsiConsole.Console;
        _outputFormat = outputFormat;
        _verbosity = verbosity;
    }

    /// <summary>
    /// Spec 041: returns true when progress bars / spinners should NOT be
    /// rendered. Suppression rules:
    ///   - JSON output mode (would corrupt structured stdout)
    ///   - Quiet verbosity
    ///   - Non-interactive console (redirected stdout, CI logs)
    /// </summary>
    public bool ShouldSuppressProgress()
    {
        if (_outputFormat == OutputFormat.Json) return true;
        if (_verbosity == VerbosityLevel.Quiet) return true;
        if (!_console.Profile.Capabilities.Interactive) return true;
        return false;
    }

    /// <summary>
    /// Spec 041: true when verbosity is Minimal — progress bars still render
    /// but per-test detail strings (test ID, verdict) should be omitted.
    /// </summary>
    public bool IsMinimalVerbosity => _verbosity == VerbosityLevel.Minimal;

    /// <summary>
    /// Shows a status message with spinner while executing an action.
    /// </summary>
    public async Task<T> StatusAsync<T>(string message, Func<Task<T>> action)
    {
        if (ShouldSuppressProgress() || _progressActive)
            return await action();

        T result = default!;

        await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync($"{OutputSymbols.Loading} {message}", async _ =>
            {
                result = await action();
            });

        return result;
    }

    /// <summary>
    /// Shows a status message with spinner while executing an action.
    /// </summary>
    public async Task StatusAsync(string message, Func<Task> action)
    {
        if (ShouldSuppressProgress() || _progressActive)
        {
            await action();
            return;
        }

        await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync($"{OutputSymbols.Loading} {message}", async _ =>
            {
                await action();
            });
    }

    /// <summary>
    /// Shows a progress bar while executing an action.
    /// </summary>
    public async Task ProgressAsync(
        string description,
        int total,
        Func<Action<int>, Task> action)
    {
        if (ShouldSuppressProgress())
        {
            await action(_ => { });
            return;
        }

        await _console.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask(description, maxValue: total);

                await action(increment =>
                {
                    task.Increment(increment);
                });
            });
    }

    /// <summary>
    /// Spec 041: shows two sequential progress bars (generation + verification)
    /// inside a single Spectre.Console live region. The action receives two
    /// task handles; the second is null when <paramref name="includeVerifyTask"/>
    /// is false (e.g. <c>--skip-critic</c>). When suppressed, the action is
    /// invoked with no-op handles so the calling handler runs unchanged.
    /// </summary>
    public async Task ProgressTwoTaskAsync(
        string genDescription,
        string verifyDescription,
        int total,
        bool includeVerifyTask,
        Func<IProgressTaskHandle, IProgressTaskHandle?, Task> action)
    {
        if (ShouldSuppressProgress())
        {
            var noopGen = new NoopTaskHandle();
            IProgressTaskHandle? noopVerify = includeVerifyTask ? new NoopTaskHandle() : null;
            await action(noopGen, noopVerify);
            return;
        }

        _progressActive = true;
        try
        {
            await _console.Progress()
                .AutoClear(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var genTask = ctx.AddTask(genDescription, maxValue: total);
                    IProgressTaskHandle genHandle = new SpectreTaskHandle(genTask);

                    IProgressTaskHandle? verifyHandle = null;
                    if (includeVerifyTask)
                    {
                        var verifyTask = ctx.AddTask(verifyDescription, maxValue: total, autoStart: false);
                        verifyHandle = new SpectreTaskHandle(verifyTask);
                    }

                    await action(genHandle, verifyHandle);
                });
        }
        finally
        {
            _progressActive = false;
        }
    }

    /// <summary>
    /// Writes a success message.
    /// </summary>
    public void Success(string message)
    {
        if (_outputFormat == OutputFormat.Json) return;
        _console.MarkupLine($"{OutputSymbols.SuccessMarkup} {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes an error message.
    /// </summary>
    public void Error(string message)
    {
        if (_outputFormat == OutputFormat.Json) return;
        _console.MarkupLine($"{OutputSymbols.ErrorMarkup} {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes a warning message.
    /// </summary>
    public void Warning(string message)
    {
        if (_outputFormat == OutputFormat.Json) return;
        _console.MarkupLine($"{OutputSymbols.WarningMarkup} {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes an info message.
    /// </summary>
    public void Info(string message)
    {
        if (_outputFormat == OutputFormat.Json) return;
        _console.MarkupLine($"{OutputSymbols.InfoMarkup} {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes a loading message (no spinner, just prefix).
    /// </summary>
    public void Loading(string message)
    {
        if (_outputFormat == OutputFormat.Json) return;
        _console.MarkupLine($"[cyan]{OutputSymbols.Loading}[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes a blank line.
    /// </summary>
    public void BlankLine()
    {
        if (_outputFormat == OutputFormat.Json) return;
        _console.WriteLine();
    }
}

/// <summary>
/// Spec 041: small abstraction over Spectre.Console's <c>ProgressTask</c> so
/// command handlers can advance progress bars without taking a direct
/// dependency on Spectre internals (which makes test substitution easier and
/// also lets the suppression path return cheap no-ops).
/// </summary>
public interface IProgressTaskHandle
{
    void Increment(double value);
    void SetDescription(string description);
    void StartTask();
}

internal sealed class SpectreTaskHandle : IProgressTaskHandle
{
    private readonly ProgressTask _task;

    public SpectreTaskHandle(ProgressTask task)
    {
        _task = task;
    }

    public void Increment(double value) => _task.Increment(value);

    public void SetDescription(string description) => _task.Description = description;

    public void StartTask() => _task.StartTask();
}

internal sealed class NoopTaskHandle : IProgressTaskHandle
{
    public void Increment(double value) { }
    public void SetDescription(string description) { }
    public void StartTask() { }
}
