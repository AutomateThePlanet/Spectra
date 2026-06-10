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
    /// The deprecated configuration keys retired by Spec 058 (critic-provider retirement). These are
    /// no longer modeled; a config that still carries them is accepted, but the keys are inert and
    /// surfaced through a non-blocking notice (never a silent drop). Note: <c>ai.providers</c> is NOT
    /// listed — it still feeds the in-process generator (its removal is Spec 059).
    /// </summary>
    private static readonly (string Dotted, string[] Path)[] DeprecatedKeyPaths =
    [
        ("ai.fallback_strategy", ["ai", "fallback_strategy"]),
        ("ai.critic.provider", ["ai", "critic", "provider"]),
        ("ai.critic.api_key_env", ["ai", "critic", "api_key_env"]),
        ("ai.critic.base_url", ["ai", "critic", "base_url"]),
    ];

    /// <summary>
    /// Inspects raw config JSON for the deprecated keys retired in Spec 058 and returns the dotted
    /// names of those present, in declaration order. Pure — does not mutate the file or affect
    /// validation. Callers surface the result as a non-blocking, key-naming notice (FR-006).
    /// Malformed JSON yields an empty list (the loader reports the syntax error instead).
    /// </summary>
    public static IReadOnlyList<string> DetectDeprecatedKeys(string json)
    {
        var present = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            foreach (var (dotted, path) in DeprecatedKeyPaths)
            {
                var node = doc.RootElement;
                var found = true;
                foreach (var segment in path)
                {
                    if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(segment, out node))
                    {
                        found = false;
                        break;
                    }
                }
                if (found) present.Add(dotted);
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — Load() reports the syntax error; nothing to detect here.
        }
        return present;
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

        // Validate AI configuration. Spec 069: ai.providers / ai.critic were removed — there is no
        // in-process model — so the only requirement is that an `ai` section exists.
        if (config.Ai is null)
        {
            errors.Add(new ParseError("MISSING_AI", "Configuration must have an 'ai' section", filePath));
        }

        return errors;
    }
}
