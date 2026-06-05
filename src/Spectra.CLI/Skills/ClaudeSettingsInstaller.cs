using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spectra.CLI.Skills;

/// <summary>
/// Spec 057 — ensures the client-side Claude Code MCP allowlist is present in
/// <c>.claude/settings.json</c> so the execution loop's <c>mcp__spectra__*</c> tool calls run without
/// a per-call permission prompt. Pure and idempotent: it merges into <c>permissions.allow</c>,
/// preserving any existing entries, and never duplicates the wildcard. This is a client-side setting
/// only — the MCP server enforces nothing. It is distinct from the <c>Bash(spectra-mcp:*)</c> entry
/// (a bash command name) that may live in <c>.claude/settings.local.json</c>.
/// </summary>
public static class ClaudeSettingsInstaller
{
    /// <summary>The MCP tool-namespace wildcard covering all Spectra MCP tools (<c>mcp__spectra__&lt;name&gt;</c>).</summary>
    public const string McpAllowEntry = "mcp__spectra__*";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Returns settings JSON whose <c>permissions.allow</c> array contains <see cref="McpAllowEntry"/>,
    /// merging into <paramref name="existingJson"/> (or creating a fresh document when null/blank).
    /// Existing entries are preserved; the entry is added at most once.
    /// </summary>
    public static string EnsureMcpAllow(string? existingJson)
    {
        JsonObject root;
        if (string.IsNullOrWhiteSpace(existingJson))
        {
            root = new JsonObject();
        }
        else
        {
            root = JsonNode.Parse(existingJson) as JsonObject ?? new JsonObject();
        }

        if (root["permissions"] is not JsonObject permissions)
        {
            permissions = new JsonObject();
            root["permissions"] = permissions;
        }

        if (permissions["allow"] is not JsonArray allow)
        {
            allow = new JsonArray();
            permissions["allow"] = allow;
        }

        var present = allow.Any(n => n is JsonValue v
            && v.TryGetValue<string>(out var s)
            && s == McpAllowEntry);

        if (!present)
            allow.Add(McpAllowEntry);

        return root.ToJsonString(WriteOptions);
    }

    /// <summary>
    /// Idempotently writes <c>.claude/settings.json</c> under <paramref name="workingDirectory"/> so it
    /// allows <see cref="McpAllowEntry"/>, preserving any existing settings. Returns the file path.
    /// </summary>
    public static async Task<string> EnsureInstalledAsync(string workingDirectory, CancellationToken ct = default)
    {
        var claudeDir = Path.Combine(workingDirectory, ".claude");
        Directory.CreateDirectory(claudeDir);
        var settingsPath = Path.Combine(claudeDir, "settings.json");

        var existing = File.Exists(settingsPath)
            ? await File.ReadAllTextAsync(settingsPath, ct)
            : null;

        var merged = EnsureMcpAllow(existing);
        await File.WriteAllTextAsync(settingsPath, merged, ct);
        return settingsPath;
    }
}
