using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spectra.CLI.Skills;

/// <summary>
/// Emits or merges .claude/settings.json with spectra CLI + .spectra/ scratch-dir permissions.
/// Idempotent: merge-by-key, never duplicates entries, never clobbers unrelated settings.
/// NO MCP allowlist — CLI-only permissions only (Spec 070 GUARD: mcp__spectra__* is gone).
/// </summary>
public static class ClaudeSettingsInstaller
{
    // "Bash(spectra *)" is the broad form matching all `spectra <subcommand> …` invocations,
    // including the `cd "…" && spectra …` compound form Claude Code issues.
    // Write + Edit cover .spectra/ scratch writes (progress.json, analysis.json, critic-verdict.json, etc.).
    private static readonly string[] PermissionsToAdd =
    [
        "Bash(spectra *)",
        "Write(.spectra/**)",
        "Edit(.spectra/**)",
    ];

    public static async Task EnsureInstalledAsync(string projectRoot, CancellationToken ct = default)
    {
        var settingsPath = Path.Combine(projectRoot, ".claude", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);

        JsonObject root;
        if (File.Exists(settingsPath))
        {
            var json = await File.ReadAllTextAsync(settingsPath, ct);
            try { root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject(); }
            catch (JsonException) { root = new JsonObject(); }
        }
        else
        {
            root = new JsonObject();
        }

        // Ensure permissions.allow array exists, preserving other keys.
        if (root["permissions"] is not JsonObject perms)
        {
            perms = new JsonObject();
            root["permissions"] = perms;
        }

        if (perms["allow"] is not JsonArray allow)
        {
            allow = new JsonArray();
            perms["allow"] = allow;
        }

        // Merge-by-value: add only entries not already present.
        var existing = allow
            .Select(n => n?.GetValue<string>())
            .Where(s => s is not null)
            .ToHashSet(StringComparer.Ordinal)!;

        foreach (var entry in PermissionsToAdd)
        {
            if (!existing.Contains(entry))
                allow.Add(entry);
        }

        await File.WriteAllTextAsync(
            settingsPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            ct);
    }
}
