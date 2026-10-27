using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spectra.CLI.Install;

/// <summary>
/// Spec 104: one entry SPECTRA adds to an agent's documented project-level command allowlist so <c>spectra</c> commands
/// run without an approval prompt (the counterpart of Claude's <c>Bash(spectra *)</c>). The file is the user's: the entry
/// is merged by key, nothing else is changed, an existing value for the key is never overwritten, and a file that is not
/// plain JSON (e.g. has comments) is left alone with a reason.
/// </summary>
public sealed class AllowlistTarget
{
    private readonly string[] _jsonPath;
    private readonly string _entry;
    private readonly string? _value;

    private AllowlistTarget(string path, string[] jsonPath, string entry, string? value)
    {
        Path = path;
        _jsonPath = jsonPath;
        _entry = entry;
        _value = value;
    }

    /// <summary>Repo-relative path of the settings file.</summary>
    public string Path { get; }

    /// <summary>One line for reports, e.g. <c>.cursor/cli.json permissions.allow += "Shell(spectra)"</c>.</summary>
    public string Description => _value is null
        ? $"{Path} {string.Join('.', _jsonPath)} += \"{_entry}\""
        : $"{Path} {string.Join('.', _jsonPath)}[\"{_entry}\"] = \"{_value}\"";

    /// <summary>An entry appended to a string array at <paramref name="jsonPath"/> (Cursor, Devin).</summary>
    public static AllowlistTarget StringArray(string path, string[] jsonPath, string entry) => new(path, jsonPath, entry, null);

    /// <summary>A pattern key set in an object at <paramref name="jsonPath"/> (OpenCode's <c>permission.bash</c>).</summary>
    public static AllowlistTarget PatternMap(string path, string[] jsonPath, string pattern, string value) => new(path, jsonPath, pattern, value);

    public AllowlistOutcome Apply(string root, bool dryRun)
    {
        var full = System.IO.Path.Combine(root, Path.Replace('/', System.IO.Path.DirectorySeparatorChar));
        JsonObject document;
        var original = File.Exists(full) ? File.ReadAllText(full) : null;
        if (string.IsNullOrWhiteSpace(original))
        {
            document = new JsonObject();
        }
        else
        {
            try
            {
                document = JsonNode.Parse(original) as JsonObject
                    ?? throw new JsonException("the top level is not an object");
            }
            catch (JsonException ex)
            {
                return AllowlistOutcome.Skip($"{Path} is not plain JSON ({ex.Message.Split('.')[0]}); add {Entry()} by hand");
            }
        }

        var parent = document;
        for (var i = 0; i < _jsonPath.Length; i++)
        {
            var key = _jsonPath[i];
            var last = i == _jsonPath.Length - 1;
            var existing = parent[key];
            if (existing is null)
            {
                JsonNode created = last && _value is null ? new JsonArray() : new JsonObject();
                parent[key] = created;
                existing = created;
            }

            if (last)
            {
                return _value is null ? AddToArray(full, original, document, existing, dryRun) : SetInMap(full, original, document, existing, dryRun);
            }

            if (existing is not JsonObject next)
            {
                return AllowlistOutcome.Skip($"{Path}: '{key}' is not an object; add {Entry()} by hand");
            }

            parent = next;
        }

        return AllowlistOutcome.Skip("empty JSON path");
    }

    private AllowlistOutcome AddToArray(string full, string? original, JsonObject document, JsonNode node, bool dryRun)
    {
        if (node is not JsonArray array)
        {
            return AllowlistOutcome.Skip($"{Path}: '{_jsonPath[^1]}' is not a list; add {Entry()} by hand");
        }

        if (array.Any(item => item is JsonValue v && v.TryGetValue<string>(out var s) && s == _entry))
        {
            return AllowlistOutcome.Present;
        }

        array.Add(_entry);
        return Save(full, original, document, dryRun);
    }

    private AllowlistOutcome SetInMap(string full, string? original, JsonObject document, JsonNode node, bool dryRun)
    {
        if (node is not JsonObject map)
        {
            return AllowlistOutcome.Skip($"{Path}: '{_jsonPath[^1]}' is not an object; add {Entry()} by hand");
        }

        if (map.ContainsKey(_entry))
        {
            return AllowlistOutcome.Present; // the user's value for this pattern wins, whatever it is
        }

        map[_entry] = _value;
        return Save(full, original, document, dryRun);
    }

    private static AllowlistOutcome Save(string full, string? original, JsonObject document, bool dryRun)
    {
        if (!dryRun)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            var newline = original is not null && original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var json = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n").Replace("\n", newline);
            File.WriteAllText(full, json + newline);
        }

        return AllowlistOutcome.Added;
    }

    private string Entry() => _value is null ? $"\"{_entry}\"" : $"\"{_entry}\": \"{_value}\"";
}

/// <summary>Spec 104: what happened to one allowlist entry.</summary>
public sealed record AllowlistOutcome(string Status, string? Reason)
{
    public static AllowlistOutcome Added { get; } = new("added", null);

    public static AllowlistOutcome Present { get; } = new("present", null);

    public static AllowlistOutcome Skip(string reason) => new("skipped", reason);
}
