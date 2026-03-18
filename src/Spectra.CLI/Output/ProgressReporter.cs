using Spectre.Console;

namespace Spectra.CLI.Output;

/// <summary>
/// Reports progress using Spectre.Console spinners and status.
/// </summary>
public sealed class ProgressReporter
{
    private readonly IAnsiConsole _console;

    public ProgressReporter(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }

    /// <summary>
    /// Shows a status message with spinner while executing an action.
    /// </summary>
    public async Task<T> StatusAsync<T>(string message, Func<Task<T>> action)
    {
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
    /// Writes a success message.
    /// </summary>
    public void Success(string message)
    {
        _console.MarkupLine($"{OutputSymbols.SuccessMarkup} {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes an error message.
    /// </summary>
    public void Error(string message)
    {
        _console.MarkupLine($"{OutputSymbols.ErrorMarkup} {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes a warning message.
    /// </summary>
    public void Warning(string message)
    {
        _console.MarkupLine($"{OutputSymbols.WarningMarkup} {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes an info message.
    /// </summary>
    public void Info(string message)
    {
        _console.MarkupLine($"{OutputSymbols.InfoMarkup} {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes a loading message (no spinner, just prefix).
    /// </summary>
    public void Loading(string message)
    {
        _console.MarkupLine($"[cyan]{OutputSymbols.Loading}[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes a blank line.
    /// </summary>
    public void BlankLine()
    {
        _console.WriteLine();
    }
}
