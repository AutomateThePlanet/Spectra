using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>Spec 080 A5: the message every write command gives when there is no config.</summary>
    public const string MissingConfigMessage = "No spectra.config.json found. Run 'spectra init' first.";

    /// <summary>Spec 080 A5: prefix of the one message every command gives for a malformed or invalid config.</summary>
    public const string InvalidConfigPrefix = "spectra.config.json is invalid: ";

    /// <summary>
    /// Spec 080 A5 / 081 C2 entry point for read-only commands. A missing config gives
    /// <see cref="SpectraConfig.Default"/>. A missing section the command <paramref name="reads"/> gives that
    /// section's defaults plus a warning; sections it doesn't read are never required. A malformed or invalid config
    /// is an error, never a fallback to defaults.
    /// </summary>
    public static Task<ConfigLoadResult> LoadForReadAsync(
        string repoRoot, ConfigSections reads = ConfigSections.None, CancellationToken ct = default) =>
        LoadEntryAsync(repoRoot, reads, forWrite: false, ct);

    /// <summary>
    /// Spec 080 A5 / 081 C2 entry point for commands that write. A missing config is an error
    /// (<see cref="MissingConfigMessage"/>), and so is a missing section the command <paramref name="reads"/>
    /// (<c>missing '&lt;section&gt;' section</c>) or a malformed or invalid config. Sections it doesn't read are never
    /// required.
    /// </summary>
    public static Task<ConfigLoadResult> LoadForWriteAsync(
        string repoRoot, ConfigSections reads = ConfigSections.None, CancellationToken ct = default) =>
        LoadEntryAsync(repoRoot, reads, forWrite: true, ct);

    /// <summary>Top-level sections the typed model requires, with the flag a command uses to declare it reads them.</summary>
    private static readonly (string Name, ConfigSections Flag)[] RequiredSections =
    [
        ("source", ConfigSections.Source),
        ("tests", ConfigSections.Tests),
        ("ai", ConfigSections.None) // no in-scope command reads `ai`; it is never required
    ];

    private static async Task<ConfigLoadResult> LoadEntryAsync(
        string repoRoot, ConfigSections reads, bool forWrite, CancellationToken ct)
    {
        var path = Path.Combine(repoRoot, ConfigFileName);
        if (!File.Exists(path))
        {
            return forWrite
                ? new ConfigLoadResult(null, MissingConfigMessage)
                : new ConfigLoadResult(SpectraConfig.Default, null);
        }

        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ConfigLoadResult(null, InvalidConfigPrefix + $"Error reading configuration file: {ex.Message}");
        }

        // Spec 081 C2: find which required sections are absent (keys match case-insensitively, as the deserializer
        // does) and fill them with an empty object, so their built-in defaults apply. The typed model is untouched.
        JsonObject root;
        try
        {
            var node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            if (node is not JsonObject obj)
            {
                return new ConfigLoadResult(null, InvalidConfigPrefix + "the configuration must be a JSON object");
            }
            root = obj;
        }
        catch (JsonException ex)
        {
            return new ConfigLoadResult(null, InvalidConfigPrefix + $"Invalid JSON syntax: {ex.Message}");
        }

        var missing = new List<(string Name, ConfigSections Flag)>();
        foreach (var section in RequiredSections)
        {
            if (!root.Any(kv => string.Equals(kv.Key, section.Name, StringComparison.OrdinalIgnoreCase)))
            {
                root[section.Name] = new JsonObject();
                missing.Add(section);
            }
        }

        var result = new ConfigLoader().Load(root.ToJsonString(), path);
        if (!result.IsSuccess)
        {
            return new ConfigLoadResult(null, InvalidConfigPrefix + result.Errors[0].Message);
        }

        var needed = missing.Where(m => m.Flag != ConfigSections.None && reads.HasFlag(m.Flag)).ToList();
        if (needed.Count > 0 && forWrite)
        {
            return new ConfigLoadResult(null, InvalidConfigPrefix + $"missing '{needed[0].Name}' section");
        }

        return new ConfigLoadResult(result.Value, null)
        {
            Warnings = needed
                .Select(m => $"spectra.config.json has no '{m.Name}' section; using the built-in defaults.")
                .ToList()
        };
    }

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

/// <summary>
/// Spec 080 A5: outcome of <see cref="ConfigLoader.LoadForReadAsync"/> / <see cref="ConfigLoader.LoadForWriteAsync"/>.
/// Exactly one of <see cref="Config"/> and <see cref="Error"/> is set; a non-null error means exit 1.
/// </summary>
public sealed record ConfigLoadResult(SpectraConfig? Config, string? Error)
{
    public bool IsSuccess => Error is null;

    /// <summary>Spec 081 C2: read commands only — sections the command reads that were missing (defaults used).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Spec 081 C2: the top-level config sections a command reads. A missing section a command reads gives defaults plus a
/// warning (read commands) or an error (write commands); sections it doesn't read are never required. <c>coverage</c>
/// is always optional and <c>ai</c> is read by no in-scope command, so neither has a flag.
/// </summary>
[Flags]
public enum ConfigSections
{
    None = 0,
    Source = 1,
    Tests = 2
}
