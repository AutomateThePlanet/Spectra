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
}
