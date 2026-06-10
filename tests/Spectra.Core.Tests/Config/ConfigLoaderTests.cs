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
                "dir": "test-cases/",
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
        Assert.Equal("test-cases/", result.Value.Tests.Dir);
        Assert.Equal("TC", result.Value.Tests.IdPrefix);
        // Spec 069: ai.providers was removed from the config model — the legacy "providers" key in
        // this JSON now deserializes as an ignored unmapped member. (Retired assertions:
        // Assert.Single(Ai.Providers) / Assert.Equal("copilot", Ai.Providers[0].Name).)
    }

    // Spec 069: Load_WithMultipleProviders removed — ai.providers is no longer modeled, so there is
    // nothing to parse or assert.

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
              "tests": { "dir": "test-cases/" },
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
    public void Load_WithNoProviders_NowSucceeds()
    {
        // Spec 069: the MISSING_PROVIDERS rule was removed — SPECTRA runs no in-process model, so a
        // config with no providers is valid. (Retired assertion: IsFailure with code MISSING_PROVIDERS.)
        const string json = """
            {
              "source": { "local_dir": "docs/" },
              "tests": { "dir": "test-cases/" },
              "ai": {}
            }
            """;

        var result = _loader.Load(json);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Load_WithSuiteOverrides_ParsesCorrectly()
    {
        // Arrange
        const string json = """
            {
              "source": { "local_dir": "docs/" },
              "tests": { "dir": "test-cases/" },
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
        Assert.Equal("test-cases/", result.Value.Tests.Dir);
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
              "tests": { "dir": "test-cases/" },
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
