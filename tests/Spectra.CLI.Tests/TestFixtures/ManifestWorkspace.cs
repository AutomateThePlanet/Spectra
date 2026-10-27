using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 096: helpers for a workspace made by the real <c>spectra init</c> with skills, and for editing its
/// <c>.spectra/skills-manifest.json</c> the way older SPECTRA versions wrote it.
/// </summary>
public static class ManifestWorkspace
{
    public const string TemplateFile = ".spectra/prompts/test-generation.md";
    public const string ProfileFile = "profiles/_default.yaml";
    public const string CustomizationFile = "CUSTOMIZATION.md";
    public const string SkillFile = ".claude/skills/spectra-generate/SKILL.md";

    /// <summary>The real <c>spectra init</c> (embedded template) with skills and agents.</summary>
    public static Task InitWithSkillsAsync(this BaselineTestWorkspace ws) =>
        TemplateWorkspaces.InitAsync(ws, skipSkills: false); // Spec 099 F7: a copy of one real init per test process

    public static string ManifestPath(this BaselineTestWorkspace ws) => ws.PathOf(".spectra/skills-manifest.json");

    public static JsonObject Manifest(this BaselineTestWorkspace ws) =>
        JsonNode.Parse(File.ReadAllText(ws.ManifestPath()))!.AsObject();

    public static JsonObject ManifestFiles(this BaselineTestWorkspace ws) => ws.Manifest()["files"]!.AsObject();

    public static void SaveManifest(this BaselineTestWorkspace ws, JsonObject manifest) =>
        File.WriteAllText(ws.ManifestPath(), manifest.ToJsonString());

    /// <summary>Rewrites the manifest's file keys with <paramref name="rekey"/> (null = remove), keeping the hashes.</summary>
    public static void RekeyManifest(this BaselineTestWorkspace ws, Func<string, string?> rekey)
    {
        var manifest = ws.Manifest();
        var files = manifest["files"]!.AsObject();
        var entries = files.Select(f => (f.Key, Hash: f.Value!.GetValue<string>())).ToList();
        var rebuilt = new JsonObject();
        foreach (var (key, hash) in entries)
        {
            var newKey = rekey(key);
            if (newKey is not null)
            {
                rebuilt[newKey] = hash;
            }
        }

        manifest["files"] = rebuilt;
        ws.SaveManifest(manifest);
    }

    /// <summary>The repo-relative, '/' form of any key the manifest may hold (absolute or relative).</summary>
    public static string Relative(this BaselineTestWorkspace ws, string key) =>
        (Path.IsPathRooted(key) ? Path.GetRelativePath(ws.Root, key) : key).Replace('\\', '/');

    public static void Append(this BaselineTestWorkspace ws, string relative, string text) =>
        File.AppendAllText(ws.PathOf(relative), text);

    public static string Hash(string content) => Spectra.CLI.Infrastructure.FileHasher.ComputeHash(content);
}
