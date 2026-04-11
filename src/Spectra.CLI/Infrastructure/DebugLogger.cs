namespace Spectra.CLI.Infrastructure;

/// <summary>
/// Append-only diagnostic log for AI calls (analyze / generate / critic /
/// testimize). Writes to <c>.spectra-debug.log</c> in the current working
/// directory. Best-effort — never throws, never blocks.
///
/// Configured once at the start of a run from the loaded SpectraConfig:
/// <c>DebugLogger.Enabled = config.Ai.DebugLogEnabled</c>. When false, all
/// <see cref="Append(string, string)"/> calls are no-ops.
///
/// v1.43.0: extracted from inline helpers in BehaviorAnalyzer / GenerationAgent
/// so CopilotCritic can also write per-test verification lines without each
/// component needing its own copy of the same try/catch.
/// </summary>
public static class DebugLogger
{
    /// <summary>
    /// Master switch. Default is true; set to false from
    /// <c>ai.debug_log_enabled</c> to silence all callers.
    /// </summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    /// Append a one-line timestamped diagnostic. <paramref name="component"/>
    /// is rendered between square brackets (e.g. <c>[generate]</c>,
    /// <c>[analyze] </c>, <c>[critic]  </c>). Caller chooses padding.
    /// </summary>
    public static void Append(string component, string message)
    {
        if (!Enabled) return;
        try
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), ".spectra-debug.log");
            var line = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ} [{component}] {message}{Environment.NewLine}";
            File.AppendAllText(path, line);
        }
        catch
        {
            // Diagnostics must never block the calling code path.
        }
    }
}
