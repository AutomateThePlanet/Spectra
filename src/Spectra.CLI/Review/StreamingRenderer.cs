using Spectre.Console;

namespace Spectra.CLI.Review;

/// <summary>
/// Renders streaming AI output to the console.
/// </summary>
public sealed class StreamingRenderer : IDisposable
{
    private readonly IAnsiConsole _console;
    private readonly bool _useLiveDisplay;
    private readonly List<string> _lines;
    private bool _disposed;

    public StreamingRenderer(IAnsiConsole? console = null, bool useLiveDisplay = true)
    {
        _console = console ?? AnsiConsole.Console;
        _useLiveDisplay = useLiveDisplay;
        _lines = [];
    }

    /// <summary>
    /// Starts the streaming display.
    /// </summary>
    public async Task StartAsync(
        string title,
        Func<IStreamingOutput, CancellationToken, Task> action,
        CancellationToken ct = default)
    {
        if (_useLiveDisplay)
        {
            await _console.Live(CreatePanel(title, ""))
                .StartAsync(async ctx =>
                {
                    var output = new LiveStreamingOutput(ctx, title, _lines);
                    await action(output, ct);
                });
        }
        else
        {
            _console.MarkupLine($"[bold blue]{Markup.Escape(title)}[/]");
            var output = new SimpleStreamingOutput(_console);
            await action(output, ct);
        }
    }

    /// <summary>
    /// Displays streaming content with a spinner.
    /// </summary>
    public async Task<T> WithSpinnerAsync<T>(
        string message,
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct = default)
    {
        T result = default!;

        await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync(message, async _ =>
            {
                result = await action(ct);
            });

        return result;
    }

    /// <summary>
    /// Writes a token to the stream.
    /// </summary>
    public void WriteToken(string token)
    {
        _console.Write(token);
    }

    /// <summary>
    /// Writes a line to the stream.
    /// </summary>
    public void WriteLine(string line = "")
    {
        _console.WriteLine(line);
    }

    /// <summary>
    /// Writes a progress update.
    /// </summary>
    public void WriteProgress(int current, int total, string message)
    {
        _console.MarkupLine($"[dim][{current}/{total}][/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes an info message.
    /// </summary>
    public void WriteInfo(string message)
    {
        _console.MarkupLine($"[blue]ℹ[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes a success message.
    /// </summary>
    public void WriteSuccess(string message)
    {
        _console.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Writes an error message.
    /// </summary>
    public void WriteError(string message)
    {
        _console.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }

    private static Panel CreatePanel(string title, string content)
    {
        return new Panel(new Markup(Markup.Escape(content)))
        {
            Header = new PanelHeader($"[bold blue]{Markup.Escape(title)}[/]"),
            Border = BoxBorder.Rounded,
            Expand = true
        };
    }

    private sealed class LiveStreamingOutput : IStreamingOutput
    {
        private readonly LiveDisplayContext _ctx;
        private readonly string _title;
        private readonly List<string> _lines;
        private string _currentLine = "";

        public LiveStreamingOutput(LiveDisplayContext ctx, string title, List<string> lines)
        {
            _ctx = ctx;
            _title = title;
            _lines = lines;
        }

        public void WriteToken(string token)
        {
            if (token.Contains('\n'))
            {
                var parts = token.Split('\n');
                for (var i = 0; i < parts.Length; i++)
                {
                    _currentLine += parts[i];
                    if (i < parts.Length - 1)
                    {
                        _lines.Add(_currentLine);
                        _currentLine = "";
                    }
                }
            }
            else
            {
                _currentLine += token;
            }

            UpdateDisplay();
        }

        public void WriteLine(string line = "")
        {
            _lines.Add(_currentLine + line);
            _currentLine = "";
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            var allContent = string.Join("\n", _lines);
            if (!string.IsNullOrEmpty(_currentLine))
            {
                allContent += (allContent.Length > 0 ? "\n" : "") + _currentLine;
            }

            // Limit displayed lines
            var displayLines = allContent.Split('\n').TakeLast(20);
            var displayContent = string.Join("\n", displayLines);

            _ctx.UpdateTarget(CreatePanel(_title, displayContent));
        }
    }

    private sealed class SimpleStreamingOutput : IStreamingOutput
    {
        private readonly IAnsiConsole _console;

        public SimpleStreamingOutput(IAnsiConsole console)
        {
            _console = console;
        }

        public void WriteToken(string token)
        {
            _console.Write(token);
        }

        public void WriteLine(string line = "")
        {
            _console.WriteLine(line);
        }
    }
}

/// <summary>
/// Interface for streaming output.
/// </summary>
public interface IStreamingOutput
{
    void WriteToken(string token);
    void WriteLine(string line = "");
}
