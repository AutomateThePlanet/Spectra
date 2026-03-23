using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Root configuration loaded from spectra.config.json.
/// </summary>
public sealed class SpectraConfig
{
    [JsonPropertyName("source")]
    public required SourceConfig Source { get; init; }

    [JsonPropertyName("tests")]
    public required TestsConfig Tests { get; init; }

    [JsonPropertyName("ai")]
    public required AiConfig Ai { get; init; }

    [JsonPropertyName("generation")]
    public GenerationConfig Generation { get; init; } = new();

    [JsonPropertyName("update")]
    public UpdateConfig Update { get; init; } = new();

    [JsonPropertyName("suites")]
    public IReadOnlyDictionary<string, SuiteConfig> Suites { get; init; } =
        new Dictionary<string, SuiteConfig>();

    [JsonPropertyName("git")]
    public GitConfig Git { get; init; } = new();

    [JsonPropertyName("validation")]
    public ValidationConfig Validation { get; init; } = new();

    [JsonPropertyName("dashboard")]
    public DashboardConfig Dashboard { get; init; } = new();

    [JsonPropertyName("coverage")]
    public CoverageConfig Coverage { get; init; } = new();

    [JsonPropertyName("execution")]
    public ExecutionConfig Execution { get; init; } = new();

    [JsonPropertyName("bug_tracking")]
    public BugTrackingConfig BugTracking { get; init; } = new();

    [JsonPropertyName("profile")]
    public ProfileConfig Profile { get; init; } = new();

    [JsonPropertyName("selections")]
    public IReadOnlyDictionary<string, SavedSelectionConfig> Selections { get; init; } =
        new Dictionary<string, SavedSelectionConfig>();

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static SpectraConfig Default => new()
    {
        Source = new SourceConfig(),
        Tests = new TestsConfig(),
        Ai = new AiConfig
        {
            Providers =
            [
                new ProviderConfig
                {
                    Name = "copilot",
                    Model = "gpt-4o",
                    Enabled = true,
                    Priority = 1
                }
            ]
        },
        Selections = new Dictionary<string, SavedSelectionConfig>
        {
            ["smoke"] = new SavedSelectionConfig
            {
                Description = "Quick smoke test — high priority tests only",
                Priorities = ["high"]
            }
        }
    };
}
