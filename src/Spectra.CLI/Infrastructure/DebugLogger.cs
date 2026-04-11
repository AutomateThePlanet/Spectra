namespace Spectra.CLI.Infrastructure;

/// <summary>
/// Append-only diagnostic log for AI calls (analyze / generate / critic /
/// update / criteria / testimize). Writes to the path configured by
/// <c>DebugConfig.LogFile</c> (default <c>.spectra-debug.log</c>) in the
/// current working directory. Best-effort — never throws, never blocks.
///
/// Spec 040: <see cref="Enabled"/> defaults to <c>false</c>. Callers (the
/// CLI host) set it once at startup from
/// <c>config.Debug.Enabled || verbosity == Diagnostic</c>. When false, all
/// append calls are no-ops and no file is created.
///
/// Two append variants:
/// - <see cref="Append"/>: plain non-AI lines (e.g. testimize lifecycle).
/// - <see cref="AppendAi"/>: AI call lines; automatically appends
///   <c>model=… provider=… tokens_in=… tokens_out=…</c> suffix per the
///   Spec 040 contract (<c>?</c> placeholders when usage is unknown).
/// </summary>
public static class DebugLogger
{
    /// <summary>
    /// Master switch. Spec 040: default is <c>false</c>. Wired from
    /// <c>config.Debug.Enabled</c> with a <c>--verbosity diagnostic</c>
    /// override at CLI startup.
    /// </summary>
    public static bool Enabled { get; set; } = false;

    /// <summary>
    /// Log file path. Defaults to <c>.spectra-debug.log</c> in the current
    /// working directory. Set from <c>config.Debug.LogFile</c> at startup.
    /// </summary>
    public static string LogFile { get; set; } = ".spectra-debug.log";

    /// <summary>
    /// Append a one-line timestamped diagnostic for a non-AI event.
    /// </summary>
    public static void Append(string component, string message)
    {
        if (!Enabled) return;
        WriteLine(component, message);
    }

    /// <summary>
    /// Append a one-line timestamped diagnostic for an AI call. The line
    /// is suffixed with <c>model=… provider=… tokens_in=… tokens_out=…</c>.
    /// Null token values render as literal <c>?</c>.
    /// </summary>
    public static void AppendAi(
        string component,
        string message,
        string? model,
        string? provider,
        int? tokensIn,
        int? tokensOut)
    {
        if (!Enabled) return;

        var modelText = string.IsNullOrEmpty(model) ? "?" : model;
        var providerText = string.IsNullOrEmpty(provider) ? "?" : provider;
        var tokensInText = tokensIn.HasValue ? tokensIn.Value.ToString() : "?";
        var tokensOutText = tokensOut.HasValue ? tokensOut.Value.ToString() : "?";

        var suffixed =
            $"{message} model={modelText} provider={providerText} tokens_in={tokensInText} tokens_out={tokensOutText}";
        WriteLine(component, suffixed);
    }

    private static void WriteLine(string component, string message)
    {
        try
        {
            var path = Path.IsPathRooted(LogFile)
                ? LogFile
                : Path.Combine(Directory.GetCurrentDirectory(), LogFile);
            var line = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ} [{component}] {message}{Environment.NewLine}";
            File.AppendAllText(path, line);
        }
        catch
        {
            // Diagnostics must never block the calling code path.
        }
    }
}
