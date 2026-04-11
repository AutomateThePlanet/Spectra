using System.Text;

namespace Spectra.CLI.Infrastructure;

/// <summary>
/// Spec 043: dedicated error log. Captures full exception context (type,
/// message, response body, retry-after, stack trace) for every failed AI
/// call across analyze / generate / critic / update / criteria phases.
///
/// Companion to <see cref="DebugLogger"/>. Where the debug log is high
/// volume (one line per call), the error log is low volume (zero lines on
/// a healthy run). On a clean run the file is not created at all.
///
/// Best-effort: file write failures emit a single stderr warning and
/// flip <see cref="Enabled"/> to false for the remainder of the run.
/// Thread-safe via a single lock — multiple parallel critic tasks may
/// call <see cref="Write"/> concurrently without corrupting the file.
/// </summary>
public static class ErrorLogger
{
    private static readonly object _gate = new();
    private static bool _truncated;
    private static bool _warned;

    /// <summary>
    /// Master switch. Defaults to <c>false</c>. Wired by the CLI host from
    /// <c>config.Debug.Enabled</c> at startup (matching <see cref="DebugLogger.Enabled"/>).
    /// </summary>
    public static bool Enabled { get; set; }

    /// <summary>
    /// Path to the error log file. Defaults to <c>.spectra-errors.log</c>
    /// in the current working directory. Set from
    /// <c>config.Debug.ErrorLogFile</c> at startup.
    /// </summary>
    public static string LogFile { get; set; } = ".spectra-errors.log";

    /// <summary>
    /// File-open mode at run start: <c>"append"</c> (default) or
    /// <c>"overwrite"</c>. Mirrors <see cref="DebugLogger.Mode"/>.
    /// </summary>
    public static string Mode { get; set; } = "append";

    /// <summary>
    /// Reset the per-run state. Called from the handler at run start
    /// alongside <see cref="DebugLogger.BeginRun"/>. Does not create or
    /// touch the file — that happens lazily on the first <see cref="Write"/>.
    /// </summary>
    public static void BeginRun()
    {
        lock (_gate)
        {
            _truncated = false;
            _warned = false;
        }
    }

    /// <summary>
    /// Append a structured error entry. No-op when <see cref="Enabled"/>
    /// is false. Never throws — file failures emit a single stderr
    /// warning and disable further writes for the run.
    /// </summary>
    /// <param name="phase">Phase tag (e.g., "critic", "generate", "analyze").</param>
    /// <param name="context">Free-form context (e.g., "test_id=TC-150", "batch=3").</param>
    /// <param name="ex">The exception to log.</param>
    /// <param name="responseBody">Optional HTTP response body. Truncated to 500 chars.</param>
    /// <param name="retryAfter">Optional <c>Retry-After</c> header value.</param>
    public static void Write(
        string phase,
        string context,
        Exception ex,
        string? responseBody = null,
        string? retryAfter = null)
    {
        if (!Enabled) return;

        var entry = FormatEntry(phase, context, ex, responseBody, retryAfter);

        lock (_gate)
        {
            if (!Enabled) return;
            try
            {
                var path = ResolvePath();
                var isOverwrite = string.Equals(Mode, "overwrite", StringComparison.OrdinalIgnoreCase);

                if (isOverwrite && !_truncated)
                {
                    File.WriteAllText(path, entry);
                    _truncated = true;
                }
                else
                {
                    File.AppendAllText(path, entry);
                }
            }
            catch (Exception ioEx)
            {
                if (!_warned)
                {
                    _warned = true;
                    try
                    {
                        Console.Error.WriteLine(
                            $"warning: error log write failed ({ioEx.GetType().Name}: {ioEx.Message}). " +
                            $"Further error logging disabled for this run.");
                    }
                    catch { /* stderr unavailable — give up silently */ }
                    Enabled = false;
                }
            }
        }
    }

    private static string FormatEntry(
        string phase,
        string context,
        Exception ex,
        string? responseBody,
        string? retryAfter)
    {
        var sb = new StringBuilder();
        var ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        sb.Append(ts).Append(" [").Append(phase).Append("] ERROR");
        if (!string.IsNullOrEmpty(context))
        {
            sb.Append(' ').Append(context);
        }
        sb.AppendLine();
        sb.Append("  Type: ").AppendLine(ex.GetType().FullName ?? ex.GetType().Name);
        sb.Append("  Message: ").AppendLine(ex.Message);

        if (!string.IsNullOrEmpty(responseBody))
        {
            var truncated = responseBody.Length > 500
                ? responseBody.Substring(0, 500) + "...[truncated]"
                : responseBody;
            // Collapse newlines so the response stays on one line for easy grep.
            truncated = truncated.Replace("\r", "").Replace("\n", "\\n");
            sb.Append("  Response: ").AppendLine(truncated);
        }

        if (!string.IsNullOrEmpty(retryAfter))
        {
            sb.Append("  Retry-After: ").AppendLine(retryAfter);
        }

        var stack = ex.StackTrace;
        if (!string.IsNullOrEmpty(stack))
        {
            sb.Append("  Stack: ").AppendLine(stack.TrimStart());
        }

        sb.AppendLine();
        return sb.ToString();
    }

    private static string ResolvePath() =>
        Path.IsPathRooted(LogFile)
            ? LogFile
            : Path.Combine(Directory.GetCurrentDirectory(), LogFile);

    /// <summary>
    /// Classifies an exception as a rate-limit / 429 failure. Used by the
    /// critic loop to bump <see cref="Spectra.CLI.Services.RunErrorTracker.RateLimits"/>
    /// in addition to the generic <c>Errors</c> counter.
    /// </summary>
    public static bool IsRateLimit(Exception ex)
    {
        if (ex is HttpRequestException httpEx && httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            return true;

        var msg = ex.Message;
        if (string.IsNullOrEmpty(msg)) return false;
        return msg.Contains("429", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("too many requests", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("rate_limit_exceeded", StringComparison.OrdinalIgnoreCase);
    }
}
