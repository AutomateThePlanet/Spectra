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

    /// <summary>Spec 082 A4: a section the command requires is missing or <c>null</c>.</summary>
    public const string ErrorConfigSectionMissing = "CONFIG_SECTION_MISSING";

    private static ConfigLoadResult Fail(string errorCode, string message) =>
        new(null, message) { ErrorCode = errorCode };

    /// <summary>
    /// Spec 080 A5 / 081 C2 entry point for read-only commands. A missing config gives
    /// <see cref="SpectraConfig.Default"/>. A missing section the command <paramref name="reads"/> gives that
    /// section's defaults plus a warning; sections it doesn't read are never required. A malformed or invalid config
    /// is an error, never a fallback to defaults.
    /// </summary>
    public static Task<ConfigLoadResult> LoadForReadAsync(
        string repoRoot, ConfigSections reads = ConfigSections.None, CancellationToken ct = default) =>
        LoadEntryAsync(repoRoot, ConfigSections.None, reads, forWrite: false, ct);

    /// <summary>
    /// Spec 080 A5 / 081 C2 / 082 A1 entry point for commands that write. A missing config is an error
    /// (<see cref="MissingConfigMessage"/>), and so is a missing section the command <paramref name="requires"/>
    /// (<c>missing '&lt;section&gt;' section</c> — for the criteria writes that is <c>coverage</c>, where they write)
    /// or a malformed or invalid config. A missing section it only <paramref name="reads"/> gives defaults plus a
    /// warning; sections it doesn't declare are never required.
    /// </summary>
    public static Task<ConfigLoadResult> LoadForWriteAsync(
        string repoRoot, ConfigSections requires = ConfigSections.None, ConfigSections reads = ConfigSections.None,
        CancellationToken ct = default) =>
        LoadEntryAsync(repoRoot, requires, reads, forWrite: true, ct);

    /// <summary>
    /// Top-level sections the loader checks, with the flag a command uses to declare it reads (or requires) them. A
    /// missing one is filled with an empty object so its built-in defaults apply.
    /// </summary>
    private static readonly (string Name, ConfigSections Flag)[] CheckedSections =
    [
        ("source", ConfigSections.Source),
        ("tests", ConfigSections.Tests),
        ("coverage", ConfigSections.Coverage),
        ("ai", ConfigSections.None) // no in-scope command reads `ai`; it is never required
    ];

    private static async Task<ConfigLoadResult> LoadEntryAsync(
        string repoRoot, ConfigSections requires, ConfigSections reads, bool forWrite, CancellationToken ct)
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
                return Fail(ErrorConfigInvalid, InvalidConfigPrefix + "the configuration must be a JSON object");
            }
            root = obj;
        }
        catch (JsonException ex)
        {
            return Fail(ErrorConfigInvalid, InvalidConfigPrefix + $"Invalid JSON syntax: {ex.Message}");
        }

        // Spec 082 A3: a section written as `null` is a missing section. Every key matching it case-insensitively is
        // considered; a present, non-null one wins, otherwise the null ones are dropped and an empty object added.
        var missing = new List<(string Name, ConfigSections Flag)>();
        foreach (var section in CheckedSections)
        {
            var matches = root
                .Where(kv => string.Equals(kv.Key, section.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Any(kv => kv.Value is not null))
            {
                continue;
            }

            foreach (var match in matches)
            {
                root.Remove(match.Key);
            }
            root[section.Name] = new JsonObject();
            missing.Add(section);
        }

        var result = new ConfigLoader().Load(root.ToJsonString(), path);
        if (!result.IsSuccess)
        {
            return Fail(ErrorConfigInvalid, InvalidConfigPrefix + result.Errors[0].Message);
        }

        // Spec 082 A1: a missing section the command requires refuses a write; one it only reads gives defaults plus
        // a warning; one it doesn't declare is filled silently.
        var declared = missing.Where(m => m.Flag != ConfigSections.None).ToList();
        if (forWrite && declared.FirstOrDefault(m => requires.HasFlag(m.Flag)) is { Name: not null } required)
        {
            return Fail(ErrorConfigSectionMissing, InvalidConfigPrefix + $"missing '{required.Name}' section");
        }

        return new ConfigLoadResult(result.Value, null)
        {
            Warnings = declared
                .Where(m => reads.HasFlag(m.Flag) && !requires.HasFlag(m.Flag))
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

    /// <summary>Spec 081 C2: sections the command reads that were missing (defaults used).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Spec 082 A4: <see cref="ConfigLoader.ErrorConfigMissing"/>, <see cref="ConfigLoader.ErrorConfigInvalid"/> or
    /// <see cref="ConfigLoader.ErrorConfigSectionMissing"/> when <see cref="Error"/> is set; otherwise null.
    /// </summary>
    public string? ErrorCode { get; init; }
}

/// <summary>
/// Spec 081 C2 / 082 A1: the top-level config sections a command declares. A missing section a command requires
/// refuses a write; one it only reads gives defaults plus a warning; undeclared sections are never required. <c>ai</c>
/// is read by no in-scope command, so it has no flag.
/// </summary>
[Flags]
public enum ConfigSections
{
    None = 0,
    Source = 1,
    Tests = 2,

    /// <summary>Spec 082 A1: where criteria are written (<c>criteria_dir</c>, <c>criteria_file</c>).</summary>
    Coverage = 4
}
