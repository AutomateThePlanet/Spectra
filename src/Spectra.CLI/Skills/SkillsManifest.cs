using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spectra.CLI.Skills;

/// <summary>
/// Tracks installed SKILL file hashes for update detection.
/// </summary>
public sealed class SkillsManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("files")]
    public Dictionary<string, string> Files { get; set; } = new();

    /// <summary>
    /// Spec 090 (FR-D1): the gitignore patterns offered to this workspace (appended, already present, or negated by the
    /// user). Null in a workspace from before 090.
    /// </summary>
    [JsonPropertyName("gitignore_offered")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? GitIgnoreOffered { get; set; }

    /// <summary>
    /// Spec 096 (analyze H1): every hash recorded for a key — several when an older manifest held the same file under two
    /// key forms (init's relative key and update-skills' absolute key). Each was written by SPECTRA, so a file whose hash
    /// equals any of them is unedited.
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, List<string>> Candidates { get; } = new(StringComparer.Ordinal);

    /// <summary>Spec 096 (analyze M1): foreign keys that matched no install path and were dropped on load.</summary>
    [JsonIgnore]
    public int DroppedForeignKeys { get; set; }

    /// <summary>Whether <paramref name="key"/> (normalised) has a record.</summary>
    public bool IsRecorded(string key) => Candidates.ContainsKey(key) || Files.ContainsKey(key);

    /// <summary>Whether <paramref name="hash"/> is one of the hashes recorded for <paramref name="key"/>.</summary>
    public bool MatchesRecord(string key, string hash) =>
        Candidates.TryGetValue(key, out var hashes)
            ? hashes.Contains(hash, StringComparer.OrdinalIgnoreCase)
            : Files.TryGetValue(key, out var single) && string.Equals(single, hash, StringComparison.OrdinalIgnoreCase);

    /// <summary>Records <paramref name="hash"/> as the one entry for <paramref name="key"/> (normalised).</summary>
    public void Record(string key, string hash)
    {
        Files[key] = hash;
        Candidates[key] = [hash];
    }

    /// <summary>Spec 096 (R1): rewrites the keys to the one normalised form, keeping duplicate keys' hashes as candidates.</summary>
    internal void Normalize(string root)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        Candidates.Clear();
        DroppedForeignKeys = 0;
        foreach (var (key, hash) in Files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var normalized = ManifestKey.Normalize(root, key);
            if (normalized is null)
            {
                DroppedForeignKeys++;
                continue;
            }

            if (!Candidates.TryGetValue(normalized, out var hashes))
            {
                Candidates[normalized] = hashes = [];
                files[normalized] = hash;
            }

            if (!hashes.Contains(hash, StringComparer.OrdinalIgnoreCase))
            {
                hashes.Add(hash);
            }
        }

        Files = files;
    }
}

/// <summary>
/// Reads and writes the skills manifest file.
/// </summary>
public sealed class SkillsManifestStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private readonly string _manifestPath;
    private readonly string _root;

    public SkillsManifestStore(string? basePath = null)
    {
        _root = basePath ?? Directory.GetCurrentDirectory();
        _manifestPath = Path.Combine(_root, ".spectra", "skills-manifest.json");
    }

    public async Task<SkillsManifest> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_manifestPath))
            return new SkillsManifest();

        try
        {
            var json = await File.ReadAllTextAsync(_manifestPath, ct);
            var manifest = JsonSerializer.Deserialize<SkillsManifest>(json, Options) ?? new SkillsManifest();
            manifest.Normalize(_root); // Spec 096: one key form; duplicates kept as candidates; moved keys re-rooted
            return manifest;
        }
        catch (JsonException)
        {
            return new SkillsManifest();
        }
    }

    public async Task SaveAsync(SkillsManifest manifest, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_manifestPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(manifest, Options);
        await File.WriteAllTextAsync(_manifestPath, json, ct);
    }
}
