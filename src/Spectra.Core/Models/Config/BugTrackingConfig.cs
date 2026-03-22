using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for bug tracking integration during test execution.
/// </summary>
public sealed class BugTrackingConfig
{
    /// <summary>
    /// Bug tracker provider: "auto", "azure-devops", "jira", "github", "local".
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = "auto";

    /// <summary>
    /// Path to bug report template (relative to repo root). Null to disable template.
    /// </summary>
    [JsonPropertyName("template")]
    public string? Template { get; init; } = "templates/bug-report.md";

    /// <summary>
    /// Default severity when not derived from test priority: "critical", "major", "medium", "minor".
    /// </summary>
    [JsonPropertyName("default_severity")]
    public string DefaultSeverity { get; init; } = "medium";

    /// <summary>
    /// Automatically include execution screenshots in bug reports.
    /// </summary>
    [JsonPropertyName("auto_attach_screenshots")]
    public bool AutoAttachScreenshots { get; init; } = true;

    /// <summary>
    /// Offer bug logging on every test failure. False = only when user asks.
    /// </summary>
    [JsonPropertyName("auto_prompt_on_failure")]
    public bool AutoPromptOnFailure { get; init; } = true;
}
