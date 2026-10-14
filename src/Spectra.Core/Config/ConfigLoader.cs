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

    /// <summary>Spec 082 A4: no <c>spectra.config.json</c> where one is required.</summary>
    public const string ErrorConfigMissing = "CONFIG_MISSING";

    /// <summary>Spec 082 A4: not valid JSON, not an object, unreadable, or fails validation.</summary>
    public const string ErrorConfigInvalid = "CONFIG_INVALID";

    private static ConfigLoadResult Fail(string errorCode, string message) =>
        new(null, message) { ErrorCode = errorCode };

    // Spec 083 — the one config rule (replaces 081 C2 and 082 A1/A3). No section is ever required.
    //   no file                              read: SpectraConfig.Default     write: CONFIG_MISSING
    //   not JSON / not an object / unreadable                   CONFIG_INVALID (both)
    //   top-level keys equal ignoring case                      CONFIG_INVALID naming the key (both)
    //   `source` / `tests` missing or null, and read            defaults + one warning naming the section
    //   `coverage` (or any unread section) missing or null      defaults, silently
    //   Spec 084: `update` missing or null, or its keys null       defaults, silently; out-of-range → CONFIG_INVALID
    // Any future config rule MUST be checked against the embedded init template (Templates/spectra.config.json, what
    // `spectra init` writes) and a pre-existing workspace — the InitTemplateWorkspaceTests guard. 080–082 checked rules
    // against SpectraConfig.Default and fixtures only, and 082 broke every init-created workspace.

    /// <summary>
    /// Entry point for read-only commands. A missing config gives <see cref="SpectraConfig.Default"/>. A missing or
    /// <c>null</c> section the command <paramref name="reads"/> gives that section's defaults plus a warning; no section
    /// is ever required. A config that can't be read or parsed is an error, never a fallback to defaults.
    /// </summary>
    public static Task<ConfigLoadResult> LoadForReadAsync(
        string repoRoot, ConfigSections reads = ConfigSections.None, CancellationToken ct = default) =>
        LoadEntryAsync(repoRoot, reads, forWrite: false, ct);

    /// <summary>
    /// Entry point for commands that write. A missing config is an error (<see cref="ErrorConfigMissing"/>) — the
    /// protection against writing into the wrong folder. Otherwise the same rule as reads: no section is ever
    /// required; a missing or <c>null</c> section the command <paramref name="reads"/> gives defaults plus a warning.
    /// </summary>
    public static Task<ConfigLoadResult> LoadForWriteAsync(
        string repoRoot, ConfigSections reads = ConfigSections.None, CancellationToken ct = default) =>
        LoadEntryAsync(repoRoot, reads, forWrite: true, ct);

    /// <summary>
    /// Top-level sections the typed model needs present (a missing or <c>null</c> one is filled with an empty object so
    /// its built-in defaults apply), with the flag a command uses to declare it reads them. Only declared reads warn:
    /// <c>coverage</c> and <c>ai</c> have no flag, so they are always filled silently.
    /// </summary>
    private static readonly (string Name, ConfigSections Flag)[] CheckedSections =
    [
        ("source", ConfigSections.Source),
        ("tests", ConfigSections.Tests),
        ("coverage", ConfigSections.None),
        ("ai", ConfigSections.None),
        // Spec 084: `"update": null` would otherwise leave SpectraConfig.Update null.
        ("update", ConfigSections.None),
        // Spec 094: likewise `"generation": null` (generation.batch_size).
        ("generation", ConfigSections.None)
    ];

    private static async Task<ConfigLoadResult> LoadEntryAsync(
        string repoRoot, ConfigSections reads, bool forWrite, CancellationToken ct)
    {
        var path = Path.Combine(repoRoot, ConfigFileName);
        if (!File.Exists(path))
        {
            return forWrite
                ? Fail(ErrorConfigMissing, MissingConfigMessage)
                : new ConfigLoadResult(SpectraConfig.Default, null);
        }

        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail(ErrorConfigInvalid, InvalidConfigPrefix + $"Error reading configuration file: {ex.Message}");
        }

        // Find which checked sections are absent or null (keys match case-insensitively, as the deserializer does) and
        // fill them with an empty object, so their built-in defaults apply. The typed model is untouched.
        JsonObject root;
        List<string> keys;
        try
        {
            var node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            if (node is not JsonObject obj)
            {
                return Fail(ErrorConfigInvalid, InvalidConfigPrefix + "the configuration must be a JSON object");
            }
            root = obj;
            // Materialise the keys here: an exact duplicate key throws ArgumentException when the object is built.
            keys = root.Select(kv => kv.Key).ToList();
        }
        catch (JsonException ex)
        {
            return Fail(ErrorConfigInvalid, InvalidConfigPrefix + $"Invalid JSON syntax: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return Fail(ErrorConfigInvalid, InvalidConfigPrefix + $"duplicate top-level key: {ex.Message}");
        }

        // Spec 083 F3: top-level keys that differ only in case are ambiguous — the deserializer would silently keep the
        // last one (possibly null) — so the config is invalid, naming every spelling.
        if (keys.GroupBy(k => k, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } duplicate)
        {
            return Fail(ErrorConfigInvalid, InvalidConfigPrefix
                + $"duplicate top-level key {string.Join(" / ", duplicate.Select(k => $"'{k}'"))} (keys may not differ only in case)");
        }

        // A checked section that is absent or `null` is filled with an empty object (keys are unique ignoring case).
        var missing = new List<(string Name, ConfigSections Flag)>();
        foreach (var section in CheckedSections)
        {
            var key = keys.FirstOrDefault(k => string.Equals(k, section.Name, StringComparison.OrdinalIgnoreCase));
            if (key is not null && root[key] is not null)
            {
                continue;
            }

            if (key is not null)
            {
                root.Remove(key);
            }
            root[section.Name] = new JsonObject();
            missing.Add(section);
        }

        var result = new ConfigLoader().Load(root.ToJsonString(), path);
        if (!result.IsSuccess)
        {
            return Fail(ErrorConfigInvalid, InvalidConfigPrefix + result.Errors[0].Message);
        }

        // A missing section the command reads gives defaults plus a warning; any other is filled silently.
        return new ConfigLoadResult(result.Value, null)
        {
            Warnings = missing
                .Where(m => m.Flag != ConfigSections.None && reads.HasFlag(m.Flag))
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
        // Spec 098 (F1): the other keys of the removed in-process critic.
        ("ai.critic.enabled", ["ai", "critic", "enabled"]),
        ("ai.critic.timeout_seconds", ["ai", "critic", "timeout_seconds"]),
        ("ai.critic.max_concurrent", ["ai", "critic", "max_concurrent"]),
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

        // Spec 084: the optional update-triage keys (a missing or null key means its default).
        if (config.Update is { GroupSize: < 1 })
        {
            errors.Add(new ParseError("INVALID_UPDATE_GROUP_SIZE",
                "update.group_size must be an integer ≥ 1", filePath));
        }

        // Spec 098 (F1): ai.critic is never an error — a legacy block of the in-process critic or an invalid model is
        // ignored with a warning where the model is used (update-skills, validate); see CriticConfig.Choose.

        // Spec 094: the optional generation batch size (a missing or null key means 8).
        if (config.Generation is { BatchSize: < 1 or > 50 })
        {
            errors.Add(new ParseError("INVALID_GENERATION_BATCH_SIZE",
                "generation.batch_size must be an integer from 1 to 50", filePath));
        }

        // Spec 098 (F9, FR-019): the optional coverage threshold (a missing or null key means 0.6).
        if (config.Generation is { CoverageThreshold: < 0 or > 1 })
        {
            errors.Add(new ParseError("INVALID_COVERAGE_THRESHOLD",
                "generation.coverage_threshold must be a number from 0 to 1", filePath));
        }

        if (config.Update is { LightThresholdWords: < 0 })
        {
            errors.Add(new ParseError("INVALID_UPDATE_LIGHT_THRESHOLD",
                "update.light_threshold_words must be an integer ≥ 0", filePath));
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

    /// <summary>Spec 081 C2: sections the command reads that were missing (defaults used).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Spec 082 A4: <see cref="ConfigLoader.ErrorConfigMissing"/> or <see cref="ConfigLoader.ErrorConfigInvalid"/> when
    /// <see cref="Error"/> is set; otherwise null.
    /// </summary>
    public string? ErrorCode { get; init; }
}

/// <summary>
/// The top-level config sections a command declares it reads. A missing or <c>null</c> declared section gives its
/// defaults plus a warning; no section is ever required (Spec 083). <c>coverage</c> and <c>ai</c> have no flag: they are
/// always filled silently.
/// </summary>
[Flags]
public enum ConfigSections
{
    None = 0,
    Source = 1,
    Tests = 2
}
