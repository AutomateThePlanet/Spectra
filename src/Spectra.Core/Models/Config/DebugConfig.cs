using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Debug logging configuration (Spec 040). When <see cref="Enabled"/> is
/// <c>false</c> (the default), <c>.spectra-debug.log</c> is not written.
/// <c>--verbosity diagnostic</c> force-enables debug logging for a single run
/// regardless of this setting.
/// </summary>
public sealed class DebugConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = false;

    [JsonPropertyName("log_file")]
    public string LogFile { get; init; } = ".spectra-debug.log";

    /// <summary>
    /// How the debug log file is opened at the start of each run (Spec 040
    /// follow-up). Accepted values (case-insensitive):
    /// <list type="bullet">
    ///   <item><c>"append"</c> (default): keep existing content and write a
    ///     separator + header line before the new run. Good for post-hoc
    ///     comparison across multiple runs.</item>
    ///   <item><c>"overwrite"</c>: truncate the file at run start and write
    ///     just the header. Keeps the file focused on the latest run only.</item>
    /// </list>
    /// Any other value falls back to <c>"append"</c>.
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "append";
}
