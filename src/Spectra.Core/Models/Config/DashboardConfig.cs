using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for dashboard generation.
/// </summary>
public sealed class DashboardConfig
{
    /// <summary>
    /// Output directory for the generated dashboard.
    /// Default: "./site"
    /// </summary>
    [JsonPropertyName("output_dir")]
    public string OutputDir { get; init; } = "./site";

    /// <summary>
    /// Dashboard page title.
    /// Default: repository name
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Path to custom dashboard template directory.
    /// </summary>
    [JsonPropertyName("template_dir")]
    public string? TemplateDir { get; init; }

    /// <summary>
    /// Include coverage visualization in dashboard.
    /// Default: true
    /// </summary>
    [JsonPropertyName("include_coverage")]
    public bool IncludeCoverage { get; init; } = true;

    /// <summary>
    /// Include run history in dashboard.
    /// Default: true
    /// </summary>
    [JsonPropertyName("include_runs")]
    public bool IncludeRuns { get; init; } = true;

    /// <summary>
    /// Maximum number of trend data points to include.
    /// Default: 30
    /// </summary>
    [JsonPropertyName("max_trend_points")]
    public int MaxTrendPoints { get; init; } = 30;

    /// <summary>
    /// Cloudflare Pages project name for dashboard deployment.
    /// Default: "spectra-dashboard"
    /// </summary>
    [JsonPropertyName("cloudflare_project_name")]
    public string CloudflareProjectName { get; init; } = "spectra-dashboard";
}
