using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spectra.CLI.Install;

/// <summary>Spec 104 (R6): one file SPECTRA wrote for an agent — repo-relative path + SHA-256 of what it wrote.</summary>
public sealed class AgentManifestEntry
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;

    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
}

/// <summary>
/// Spec 104 (R6/R8): <c>.spectra/manifest.&lt;agent&gt;.json</c>, the record of every file <c>spectra init --agent</c> wrote for
/// one non-Claude agent. On a re-run a file whose content differs from its recorded hash is the user's edit and is skipped
/// unless <c>--regenerate</c>. A file several agents rely on (a shared skill directory, the shared fragment) is listed in
/// each of their manifests. Claude's install keeps its own <c>.spectra/skills-manifest.json</c>, which this never touches.
/// </summary>
public sealed class AgentManifest
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    [JsonPropertyName("version")] public string Version { get; set; } = "1.0";

    [JsonPropertyName("agent")] public string Agent { get; set; } = string.Empty;

    [JsonPropertyName("files")] public List<AgentManifestEntry> Files { get; set; } = [];

    public static string RelativePathFor(string agent) => $".spectra/manifest.{agent}.json";

    public static string PathFor(string root, string agent) => System.IO.Path.Combine(root, ".spectra", $"manifest.{agent}.json");

    public static AgentManifest? Load(string root, string agent)
    {
        var path = PathFor(root, agent);
        return File.Exists(path) ? JsonSerializer.Deserialize<AgentManifest>(File.ReadAllText(path)) : null;
    }

    public void Save(string root)
    {
        var path = PathFor(root, Agent);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options).Replace("\r\n", "\n") + "\n");
    }

    public string? HashFor(string relativePath) => Files.FirstOrDefault(f => f.Path == relativePath)?.Sha256;

    /// <summary>SHA-256 with CRLF normalised to LF, so a checkout that rewrites line endings is not mistaken for an edit.</summary>
    public static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.Replace("\r\n", "\n")))).ToLowerInvariant();

    public static string HashOfFile(string path) => Hash(File.ReadAllText(path));
}
