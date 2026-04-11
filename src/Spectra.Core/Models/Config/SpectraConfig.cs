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

    [JsonPropertyName("analysis")]
    public AnalysisConfig Analysis { get; init; } = new();

    [JsonPropertyName("selections")]
    public IReadOnlyDictionary<string, SavedSelectionConfig> Selections { get; init; } =
        new Dictionary<string, SavedSelectionConfig>();

    [JsonPropertyName("testimize")]
    public TestimizeConfig Testimize { get; init; } = new();

    [JsonPropertyName("debug")]
    public DebugConfig Debug { get; init; } = new();

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static SpectraConfig Default => new()
    {
        Source = new SourceConfig(),
        Tests = new TestsConfig(),
        Ai = new AiConfig
        {
            // Spec 041: default to gpt-4.1 generator + gpt-5-mini critic
            // (both 0× on any paid Copilot plan, cross-architecture critic).
            Providers =
            [
                new ProviderConfig
                {
                    Name = "github-models",
                    Model = "gpt-4.1",
                    Enabled = true,
                    Priority = 1
                }
            ],
            Critic = new CriticConfig
            {
                Enabled = true,
                Provider = "github-models",
                Model = "gpt-5-mini"
            }
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
