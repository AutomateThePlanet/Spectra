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

    public SkillsManifestStore(string? basePath = null)
    {
        var root = basePath ?? Directory.GetCurrentDirectory();
        _manifestPath = Path.Combine(root, ".spectra", "skills-manifest.json");
    }

    public async Task<SkillsManifest> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_manifestPath))
            return new SkillsManifest();

        try
        {
            var json = await File.ReadAllTextAsync(_manifestPath, ct);
            return JsonSerializer.Deserialize<SkillsManifest>(json, Options) ?? new SkillsManifest();
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
