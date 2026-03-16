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

    [JsonPropertyName("profile")]
    public ProfileConfig Profile { get; init; } = new();

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
        }
    };
}
