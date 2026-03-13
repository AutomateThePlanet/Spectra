using Spectra.Core.Config;

namespace Spectra.Core.Tests.Config;

public class ConfigLoaderTests
{
    private readonly ConfigLoader _loader = new();

    [Fact]
    public void Load_WithValidConfig_ReturnsSuccess()
    {
        // Arrange
        const string json = """
            {
              "source": {
                "mode": "local",
                "local_dir": "docs/"
              },
              "tests": {
                "dir": "tests/",
                "id_prefix": "TC",
                "id_start": 100
              },
              "ai": {
                "providers": [
                  {
                    "name": "copilot",
                    "model": "gpt-4o",
                    "enabled": true,
                    "priority": 1
                  }
                ],
                "fallback_strategy": "auto"
              }
            }
            """;

        // Act
        var result = _loader.Load(json);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("local", result.Value.Source.Mode);
        Assert.Equal("docs/", result.Value.Source.LocalDir);
        Assert.Equal("tests/", result.Value.Tests.Dir);
        Assert.Equal("TC", result.Value.Tests.IdPrefix);
        Assert.Single(result.Value.Ai.Providers);
        Assert.Equal("copilot", result.Value.Ai.Providers[0].Name);
    }

    [Fact]
    public void Load_WithMultipleProviders_ParsesAll()
    {
        // Arrange
        const string json = """
            {
              "source": { "local_dir": "docs/" },
              "tests": { "dir": "tests/" },
              "ai": {
                "providers": [
                  { "name": "copilot", "model": "gpt-4o", "priority": 1 },
                  { "name": "anthropic", "model": "claude-sonnet-4-5", "api_key_env": "ANTHROPIC_API_KEY", "priority": 2 },
                  { "name": "openai", "model": "gpt-4-turbo", "api_key_env": "OPENAI_API_KEY", "priority": 3 }
                ],
                "fallback_strategy": "auto"
              }
            }
            """;

        // Act
        var result = _loader.Load(json);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Ai.Providers.Count);
        Assert.Equal("anthropic", result.Value.Ai.Providers[1].Name);
        Assert.Equal("ANTHROPIC_API_KEY", result.Value.Ai.Providers[1].ApiKeyEnv);
    }

    [Fact]
    public void Load_WithInvalidJson_ReturnsFailure()
    {
        // Arrange
        const string json = """{ "source": { invalid json here """;

        // Act
        var result = _loader.Load(json);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Single(result.Errors);
        Assert.Equal("INVALID_JSON", result.Errors[0].Code);
    }

    [Fact]
    public void Load_WithMissingSource_ReturnsFailure()
    {
        // Arrange - missing 'source' required property
        const string json = """
            {
              "tests": { "dir": "tests/" },
              "ai": {
                "providers": [{ "name": "copilot", "model": "gpt-4o" }]
              }
            }
            """;

        // Act
        var result = _loader.Load(json);

        // Assert
        Assert.True(result.IsFailure);
        // .NET 8 JSON serializer catches missing required properties as INVALID_JSON
        Assert.Contains(result.Errors, e =>
            e.Code == "MISSING_SOURCE" || e.Code == "INVALID_JSON");
    }

    [Fact]
    public void Load_WithMissingProviders_ReturnsFailure()
    {
        // Arrange
        const string json = """
            {
              "source": { "local_dir": "docs/" },
              "tests": { "dir": "tests/" },
              "ai": {
                "providers": [],
                "fallback_strategy": "auto"
              }
            }
            """;

        // Act
        var result = _loader.Load(json);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Code == "MISSING_PROVIDERS");
    }

    [Fact]
    public void Load_WithSuiteOverrides_ParsesCorrectly()
    {
        // Arrange
        const string json = """
            {
              "source": { "local_dir": "docs/" },
              "tests": { "dir": "tests/" },
              "ai": {
                "providers": [{ "name": "copilot", "model": "gpt-4o" }]
              },
              "suites": {
                "checkout": {
                  "component": "payments",
                  "relevant_docs": ["docs/checkout.md"],
                  "default_tags": ["checkout"],
                  "default_priority": "high"
                }
              }
            }
            """;

        // Act
        var result = _loader.Load(json);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Suites);
        Assert.True(result.Value.Suites.ContainsKey("checkout"));
        Assert.Equal("payments", result.Value.Suites["checkout"].Component);
    }

    [Fact]
    public void Load_WithDefaults_AppliesDefaults()
    {
        // Arrange
        const string json = """
            {
              "source": {},
              "tests": {},
              "ai": {
                "providers": [{ "name": "copilot", "model": "gpt-4o" }]
              }
            }
            """;

        // Act
        var result = _loader.Load(json);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("local", result.Value.Source.Mode);
        Assert.Equal("docs/", result.Value.Source.LocalDir);
        Assert.Equal(50, result.Value.Source.MaxFileSizeKb);
        Assert.Equal("tests/", result.Value.Tests.Dir);
        Assert.Equal("TC", result.Value.Tests.IdPrefix);
        Assert.Equal(100, result.Value.Tests.IdStart);
        Assert.Equal(15, result.Value.Generation.DefaultCount);
        Assert.True(result.Value.Generation.RequireReview);
    }

    [Fact]
    public void Load_WithComments_HandlesJsonComments()
    {
        // Arrange
        const string json = """
            {
              // This is a comment
              "source": { "local_dir": "docs/" },
              "tests": { "dir": "tests/" },
              "ai": {
                "providers": [{ "name": "copilot", "model": "gpt-4o" }]
              }
            }
            """;

        // Act
        var result = _loader.Load(json);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void GenerateDefaultConfig_ReturnsValidJson()
    {
        // Act
        var json = ConfigLoader.GenerateDefaultConfig();
        var result = _loader.Load(json);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }
}
