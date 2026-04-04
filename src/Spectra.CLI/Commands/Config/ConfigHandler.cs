using System.Text.Json;
using System.Text.Json.Nodes;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Config;

/// <summary>
/// Handles the config command execution.
/// </summary>
public sealed class ConfigHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;

    public ConfigHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
    }

    /// <summary>
    /// Executes the config command.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string? key,
        string? value,
        bool showAll,
        CancellationToken ct = default)
    {
        try
        {
            var basePath = Directory.GetCurrentDirectory();
            var configPath = Path.Combine(basePath, "spectra.config.json");

            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
                return ExitCodes.Error;
            }

            var configJson = await File.ReadAllTextAsync(configPath, ct);
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

            // If value is provided, this is a set operation
            if (key is not null && value is not null)
            {
                return await SetConfigValueAsync(configPath, configJson, key, value, jsonOptions, ct);
            }

            // If key is provided without value, show that key
            if (key is not null)
            {
                return ShowConfigValue(configJson, key, jsonOptions);
            }

            // Show all config
            return ShowAllConfig(configJson, showAll, jsonOptions);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nOperation cancelled.");
            return ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return ExitCodes.Error;
        }
    }

    private async Task<int> SetConfigValueAsync(
        string configPath,
        string configJson,
        string key,
        string value,
        JsonSerializerOptions options,
        CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            var root = doc.RootElement;

            // Parse the JSON into a mutable dictionary
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(configJson, options)
                ?? new Dictionary<string, object>();

            // Support nested keys like "source.dir"
            var keys = key.Split('.');
            SetNestedValue(dict, keys, value);

            var newJson = JsonSerializer.Serialize(dict, options);
            await File.WriteAllTextAsync(configPath, newJson, ct);

            Console.WriteLine($"Set {key} = {value}");
            return ExitCodes.Success;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Invalid JSON: {ex.Message}");
            return ExitCodes.Error;
        }
    }

    private static void SetNestedValue(Dictionary<string, object> dict, string[] keys, string value)
    {
        if (keys.Length == 1)
        {
            dict[keys[0]] = value;
            return;
        }

        var current = dict;
        for (var i = 0; i < keys.Length - 1; i++)
        {
            if (!current.ContainsKey(keys[i]) || current[keys[i]] is not Dictionary<string, object>)
            {
                current[keys[i]] = new Dictionary<string, object>();
            }
            current = (Dictionary<string, object>)current[keys[i]];
        }
        current[keys[^1]] = value;
    }

    private int ShowConfigValue(string configJson, string key, JsonSerializerOptions options)
    {
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            var element = doc.RootElement;

            // Navigate nested keys
            var keys = key.Split('.');
            foreach (var k in keys)
            {
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(k, out var prop))
                {
                    Console.Error.WriteLine($"Key not found: {key}");
                    return ExitCodes.Error;
                }
                element = prop;
            }

            var value = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => element.GetRawText()
            };

            Console.WriteLine($"{key} = {value}");
            return ExitCodes.Success;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error parsing config: {ex.Message}");
            return ExitCodes.Error;
        }
    }

    private int ShowAllConfig(string configJson, bool showRaw, JsonSerializerOptions options)
    {
        if (showRaw)
        {
            Console.WriteLine(configJson);
            return ExitCodes.Success;
        }

        try
        {
            var config = JsonSerializer.Deserialize<SpectraConfig>(configJson, options);
            if (config is null)
            {
                Console.Error.WriteLine("Invalid config file.");
                return ExitCodes.Error;
            }

            Console.WriteLine("SPECTRA Configuration");
            Console.WriteLine(new string('-', 40));

            Console.WriteLine();
            Console.WriteLine("Source:");
            Console.WriteLine($"  Mode:      {config.Source?.Mode ?? "local"}");
            Console.WriteLine($"  Directory: {config.Source?.LocalDir ?? "docs/"}");
            Console.WriteLine($"  Patterns:  {string.Join(", ", config.Source?.IncludePatterns ?? ["**/*.md"])}");

            Console.WriteLine();
            Console.WriteLine("Tests:");
            Console.WriteLine($"  Directory: {config.Tests?.Dir ?? "tests/"}");
            Console.WriteLine($"  ID Prefix: {config.Tests?.IdPrefix ?? "TC"}");
            Console.WriteLine($"  ID Start:  {config.Tests?.IdStart ?? 100}");

            Console.WriteLine();
            Console.WriteLine("AI:");
            if (config.Ai?.Providers is { Count: > 0 })
            {
                Console.WriteLine("  Providers:");
                foreach (var provider in config.Ai.Providers)
                {
                    Console.WriteLine($"    - {provider.Name}: {provider.Model ?? "(default model)"}");
                }
                Console.WriteLine($"  Fallback: {config.Ai.FallbackStrategy}");
            }
            else
            {
                Console.WriteLine("  Providers: (not configured)");
            }

            Console.WriteLine();
            Console.WriteLine("Generation:");
            Console.WriteLine($"  Default count:   {config.Generation?.DefaultCount ?? 15}");
            Console.WriteLine($"  Require review:  {config.Generation?.RequireReview ?? true}");
            Console.WriteLine($"  Dup threshold:   {config.Generation?.DuplicateThreshold ?? 0.6}");

            Console.WriteLine();
            Console.WriteLine("Validation:");
            Console.WriteLine($"  Required fields: {string.Join(", ", config.Validation?.RequiredFields ?? ["id", "priority"])}");
            Console.WriteLine($"  ID pattern:      {config.Validation?.IdPattern ?? @"^TC-\d{3,}$"}");
            Console.WriteLine($"  Max steps:       {config.Validation?.MaxSteps ?? 20}");

            return ExitCodes.Success;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error parsing config: {ex.Message}");
            return ExitCodes.Error;
        }
    }

    /// <summary>
    /// Adds an automation directory to coverage.automation_dirs.
    /// </summary>
    public async Task<int> AddAutomationDirAsync(string path, CancellationToken ct = default)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "spectra.config.json");
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
            return ExitCodes.Error;
        }

        var json = await File.ReadAllTextAsync(configPath, ct);
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true });
        if (root is null)
        {
            Console.Error.WriteLine("Invalid spectra.config.json");
            return ExitCodes.Error;
        }

        // Ensure coverage.automation_dirs exists
        root["coverage"] ??= new JsonObject();
        var coverage = root["coverage"]!.AsObject();
        coverage["automation_dirs"] ??= new JsonArray("tests", "test", "spec", "specs", "e2e");
        var dirs = coverage["automation_dirs"]!.AsArray();

        // Check for duplicate
        var existing = dirs.Select(d => d?.GetValue<string>()).Where(d => d is not null).ToList();
        if (existing.Contains(path))
        {
            Console.WriteLine($"'{path}' is already configured as an automation directory");
            return ExitCodes.Success;
        }

        dirs.Add(path);

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(configPath, root.ToJsonString(options), ct);

        Console.WriteLine($"Added '{path}' to automation directories");
        return ExitCodes.Success;
    }

    /// <summary>
    /// Removes an automation directory from coverage.automation_dirs.
    /// </summary>
    public async Task<int> RemoveAutomationDirAsync(string path, CancellationToken ct = default)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "spectra.config.json");
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
            return ExitCodes.Error;
        }

        var json = await File.ReadAllTextAsync(configPath, ct);
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true });
        if (root is null)
        {
            Console.Error.WriteLine("Invalid spectra.config.json");
            return ExitCodes.Error;
        }

        var dirs = root["coverage"]?["automation_dirs"]?.AsArray();
        if (dirs is null)
        {
            Console.WriteLine($"Warning: '{path}' was not found in automation directories");
            return ExitCodes.Success;
        }

        var index = -1;
        for (var i = 0; i < dirs.Count; i++)
        {
            if (dirs[i]?.GetValue<string>() == path)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            Console.WriteLine($"Warning: '{path}' was not found in automation directories");
            return ExitCodes.Success;
        }

        dirs.RemoveAt(index);

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(configPath, root.ToJsonString(options), ct);

        Console.WriteLine($"Removed '{path}' from automation directories");
        return ExitCodes.Success;
    }

    /// <summary>
    /// Lists all configured automation directories with existence status.
    /// </summary>
    public async Task<int> ListAutomationDirsAsync(CancellationToken ct = default)
    {
        var basePath = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(basePath, "spectra.config.json");
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
            return ExitCodes.Error;
        }

        var json = await File.ReadAllTextAsync(configPath, ct);
        var config = JsonSerializer.Deserialize<SpectraConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var dirs = config?.Coverage.AutomationDirs ?? [];

        if (dirs.Count == 0)
        {
            Console.WriteLine("No automation directories configured.");
            return ExitCodes.Success;
        }

        Console.WriteLine("Automation directories:");
        foreach (var dir in dirs)
        {
            var fullPath = Path.IsPathRooted(dir)
                ? dir
                : Path.GetFullPath(Path.Combine(basePath, dir));
            var exists = Directory.Exists(fullPath);
            var status = exists ? "[exists] " : "[missing]";
            Console.WriteLine($"  {status} {dir}");
        }

        return ExitCodes.Success;
    }
}
