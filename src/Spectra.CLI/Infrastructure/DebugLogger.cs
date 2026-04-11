using System.Reflection;

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
    /// Controls how the log file is opened at the start of a run (Spec 040
    /// follow-up). Accepted values (case-insensitive):
    /// <list type="bullet">
    ///   <item><c>"append"</c> (default): write a separator + header line
    ///     before the new run; existing content is preserved.</item>
    ///   <item><c>"overwrite"</c>: truncate the file and write just the
    ///     header. Only the latest run is kept.</item>
    /// </list>
    /// Set by the CLI host from <c>config.Debug.Mode</c> at startup.
    /// </summary>
    public static string Mode { get; set; } = "append";

    /// <summary>
    /// Called once by the handler at the start of a <c>generate</c> /
    /// <c>update</c> run, after <see cref="Enabled"/>, <see cref="LogFile"/>,
    /// and <see cref="Mode"/> have been wired from config. Writes a header
    /// block identifying the SPECTRA version, command line, and UTC
    /// timestamp. In <c>append</c> mode (default), prepends a horizontal
    /// separator so each run is visually distinct. In <c>overwrite</c>
    /// mode, truncates the file first.
    ///
    /// No-op when <see cref="Enabled"/> is <c>false</c>. Never throws.
    /// </summary>
    public static void BeginRun()
    {
        if (!Enabled) return;
        try
        {
            var path = ResolvePath();
            var isAppend = !string.Equals(Mode, "overwrite", StringComparison.OrdinalIgnoreCase);
            var fileExists = File.Exists(path);

            var header = BuildHeader();
            var block = (isAppend && fileExists)
                // Existing file + append: prepend a separator so the new run
                // is visually distinct from whatever preceded it.
                ? Environment.NewLine + new string('─', 62) + Environment.NewLine + header + Environment.NewLine
                // Fresh file OR overwrite mode: just the header (no leading
                // separator — nothing to separate from).
                : new string('─', 62) + Environment.NewLine + header + Environment.NewLine;

            if (isAppend)
            {
                File.AppendAllText(path, block);
            }
            else
            {
                File.WriteAllText(path, block);
            }
        }
        catch
        {
            // Diagnostics must never block the calling code path.
        }
    }

    private static string BuildHeader()
    {
        var version = ResolveVersion();
        var commandLine = ResolveCommandLine();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        return $"=== SPECTRA v{version} | {commandLine} | {timestamp} ===";
    }

    private static string ResolveVersion()
    {
        try
        {
            var asm = typeof(DebugLogger).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(info))
            {
                // dotnet pack -p:Version=1.45.2 produces "1.45.2+<commithash>".
                // Strip the hash for readability in the header.
                var plus = info.IndexOf('+');
                return plus >= 0 ? info[..plus] : info;
            }
            return asm.GetName().Version?.ToString() ?? "?";
        }
        catch
        {
            return "?";
        }
    }

    private static string ResolveCommandLine()
    {
        try
        {
            // args[0] is the dotnet tool shim path — ugly and irrelevant.
            // Reconstruct the user-visible form: "spectra <args[1..]>".
            var args = Environment.GetCommandLineArgs();
            if (args.Length <= 1) return "spectra";
            return "spectra " + string.Join(' ', args.Skip(1));
        }
        catch
        {
            return "spectra";
        }
    }

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
    ///
    /// Spec 040 follow-up: when <paramref name="estimated"/> is true, both
    /// token fields gain a <c>~</c> prefix (<c>~tokens_in=N ~tokens_out=N</c>)
    /// to signal that the counts came from the <c>text.Length / 4</c>
    /// fallback in <see cref="Spectra.CLI.Services.TokenEstimator"/> rather
    /// than provider-reported <c>AssistantUsageEvent</c> data.
    /// </summary>
    public static void AppendAi(
        string component,
        string message,
        string? model,
        string? provider,
        int? tokensIn,
        int? tokensOut,
        bool estimated = false)
    {
        if (!Enabled) return;

        var modelText = string.IsNullOrEmpty(model) ? "?" : model;
        var providerText = string.IsNullOrEmpty(provider) ? "?" : provider;
        var tokensInText = tokensIn.HasValue ? tokensIn.Value.ToString() : "?";
        var tokensOutText = tokensOut.HasValue ? tokensOut.Value.ToString() : "?";

        var tokenPrefix = estimated ? "~" : "";

        var suffixed =
            $"{message} model={modelText} provider={providerText} "
            + $"{tokenPrefix}tokens_in={tokensInText} {tokenPrefix}tokens_out={tokensOutText}";
        WriteLine(component, suffixed);
    }

    private static void WriteLine(string component, string message)
    {
        try
        {
            var path = ResolvePath();
            var line = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ} [{component}] {message}{Environment.NewLine}";
            File.AppendAllText(path, line);
        }
        catch
        {
            // Diagnostics must never block the calling code path.
        }
    }

    private static string ResolvePath() =>
        Path.IsPathRooted(LogFile)
            ? LogFile
            : Path.Combine(Directory.GetCurrentDirectory(), LogFile);
}
