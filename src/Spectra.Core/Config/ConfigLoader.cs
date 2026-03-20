using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.Core.Config;

/// <summary>
/// Loads and validates spectra.config.json configuration files.
/// </summary>
public sealed class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public const string ConfigFileName = "spectra.config.json";

    /// <summary>
    /// Loads configuration from a JSON string.
    /// </summary>
    /// <param name="json">The JSON content</param>
    /// <param name="filePath">Optional file path for error messages</param>
    /// <returns>ParseResult containing the configuration or errors</returns>
    public ParseResult<SpectraConfig> Load(string json, string? filePath = null)
    {
        try
        {
            var config = JsonSerializer.Deserialize<SpectraConfig>(json, JsonOptions);

            if (config is null)
            {
                return ParseResult<SpectraConfig>.Failure(new ParseError(
                    "DESERIALIZATION_FAILED",
                    "Failed to deserialize configuration",
                    filePath));
            }

            // Validate required fields
            var errors = Validate(config, filePath);
            if (errors.Count > 0)
            {
                return ParseResult<SpectraConfig>.Failure(errors);
            }

            return ParseResult<SpectraConfig>.Success(config);
        }
        catch (JsonException ex)
        {
            return ParseResult<SpectraConfig>.Failure(new ParseError(
                "INVALID_JSON",
                $"Invalid JSON syntax: {ex.Message}",
                filePath,
                ex.LineNumber.HasValue ? (int)ex.LineNumber.Value + 1 : null,
                ex.BytePositionInLine.HasValue ? (int)ex.BytePositionInLine.Value + 1 : null));
        }
        catch (Exception ex)
        {
            return ParseResult<SpectraConfig>.Failure(new ParseError(
                "LOAD_ERROR",
                $"Unexpected error loading configuration: {ex.Message}",
                filePath));
        }
    }

    /// <summary>
    /// Loads configuration from a file.
    /// </summary>
    public async Task<ParseResult<SpectraConfig>> LoadFromFileAsync(string path, CancellationToken ct = default)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return Load(json, path);
        }
        catch (FileNotFoundException)
        {
            return ParseResult<SpectraConfig>.Failure(new ParseError(
                "FILE_NOT_FOUND",
                $"Configuration file not found: {path}",
                path));
        }
        catch (Exception ex)
        {
            return ParseResult<SpectraConfig>.Failure(new ParseError(
                "READ_ERROR",
                $"Error reading configuration file: {ex.Message}",
                path));
        }
    }

    /// <summary>
    /// Finds the configuration file in the current directory or parent directories.
    /// </summary>
    public static string? FindConfigFile(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            var configPath = Path.Combine(directory.FullName, ConfigFileName);
            if (File.Exists(configPath))
            {
                return configPath;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Generates a default configuration JSON string.
    /// </summary>
    public static string GenerateDefaultConfig()
    {
        return JsonSerializer.Serialize(SpectraConfig.Default, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    /// <summary>
    /// Generates a configuration JSON string with custom provider and model.
    /// </summary>
    public static string GenerateConfig(string providerName, string model, string? apiKeyEnv = null, string? baseUrl = null)
    {
        var config = new SpectraConfig
        {
            Source = new SourceConfig(),
            Tests = new TestsConfig(),
            Ai = new AiConfig
            {
                Providers =
                [
                    new ProviderConfig
                    {
                        Name = providerName,
                        Model = model,
                        ApiKeyEnv = apiKeyEnv,
                        BaseUrl = baseUrl,
                        Enabled = true,
                        Priority = 1
                    }
                ]
            }
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    private static IReadOnlyList<ParseError> Validate(SpectraConfig config, string? filePath)
    {
        var errors = new List<ParseError>();

        // Validate source configuration
        if (config.Source is null)
        {
            errors.Add(new ParseError("MISSING_SOURCE", "Configuration must have a 'source' section", filePath));
        }

        // Validate tests configuration
        if (config.Tests is null)
        {
            errors.Add(new ParseError("MISSING_TESTS", "Configuration must have a 'tests' section", filePath));
        }

        // Validate AI configuration
        if (config.Ai is null)
        {
            errors.Add(new ParseError("MISSING_AI", "Configuration must have an 'ai' section", filePath));
        }
        else if (config.Ai.Providers is null || config.Ai.Providers.Count == 0)
        {
            errors.Add(new ParseError("MISSING_PROVIDERS", "AI configuration must have at least one provider", filePath));
        }
        else
        {
            // Validate providers
            foreach (var provider in config.Ai.Providers)
            {
                if (string.IsNullOrWhiteSpace(provider.Name))
                {
                    errors.Add(new ParseError("INVALID_PROVIDER", "Provider must have a name", filePath));
                }
                if (string.IsNullOrWhiteSpace(provider.Model))
                {
                    errors.Add(new ParseError("INVALID_PROVIDER", $"Provider '{provider.Name}' must have a model", filePath));
                }
            }
        }

        return errors;
    }
}
