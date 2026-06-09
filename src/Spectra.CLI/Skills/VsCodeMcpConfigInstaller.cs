using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spectra.CLI.Skills;

/// <summary>
/// Raised when an existing <c>.vscode/mcp.json</c> cannot be parsed. The shared-namespace init
/// contract (Spec 068, FR-015) requires a fail-loud outcome here — never overwrite a config the
/// tool could not read — so the file is left untouched and the caller surfaces an actionable error.
/// </summary>
public sealed class InvalidMcpConfigException : Exception
{
    public InvalidMcpConfigException(string message) : base(message) { }
    public InvalidMcpConfigException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Spec 068 (FR-013/FR-014/FR-018) — registers SPECTRA's MCP server in <c>.vscode/mcp.json</c> by
/// <b>merge-by-key</b> on <c>servers.spectra</c>, mirroring <see cref="ClaudeSettingsInstaller"/>.
/// Every foreign <c>servers.*</c> key and every top-level key (e.g. <c>inputs</c>) is preserved; the
/// file is never replaced wholesale and never skipped-if-exists (skip-if-exists is silent loss when
/// another tool wrote the file first). The merge is pure and idempotent by value; the writer no-ops
/// (and therefore preserves user comments) when the entry is already current.
/// </summary>
public static class VsCodeMcpConfigInstaller
{
    /// <summary>The MCP server key SPECTRA owns. Only this key is added/updated; foreign keys are preserved.</summary>
    public const string ServerKey = "spectra";

    // .vscode/mcp.json is JSONC in practice (VS Code allows comments + trailing commas). Parse
    // tolerantly; only genuinely malformed JSON fails loud (FR-015).
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>SPECTRA's MCP server definition: <c>{ "command": "spectra-mcp", "args": ["."] }</c>.</summary>
    private static JsonObject DesiredServer() => new()
    {
        ["command"] = "spectra-mcp",
        ["args"] = new JsonArray("."),
    };

    /// <summary>
    /// Returns the JSON that <c>.vscode/mcp.json</c> should contain so <c>servers.spectra</c> equals
    /// the desired value, merging into <paramref name="existingJson"/> (or creating a fresh document
    /// when null/blank). Foreign server keys and top-level keys are preserved. Throws
    /// <see cref="InvalidMcpConfigException"/> when an existing document cannot be parsed.
    /// </summary>
    public static string EnsureSpectraServer(string? existingJson)
    {
        JsonObject root;
        if (string.IsNullOrWhiteSpace(existingJson))
        {
            root = new JsonObject();
        }
        else
        {
            JsonNode? parsed;
            try
            {
                parsed = JsonNode.Parse(existingJson, documentOptions: ParseOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidMcpConfigException(
                    $"Could not parse the existing MCP config as JSON: {ex.Message}", ex);
            }

            root = parsed as JsonObject
                ?? throw new InvalidMcpConfigException("Expected a JSON object at the root of the MCP config.");
        }

        if (root["servers"] is not JsonObject servers)
        {
            servers = new JsonObject();
            root["servers"] = servers;
        }

        // Add/update only SPECTRA's own key; every other server entry is left untouched.
        servers[ServerKey] = DesiredServer();
        return root.ToJsonString(WriteOptions);
    }

    /// <summary>
    /// Idempotently ensures <c>&lt;workingDirectory&gt;/.vscode/mcp.json</c> registers the <c>spectra</c>
    /// server by merge-by-key, preserving all foreign entries. Returns the file path. When the file
    /// already contains the current <c>spectra</c> entry the file is left byte-unchanged (preserving any
    /// user comments). Propagates <see cref="InvalidMcpConfigException"/> without modifying the file.
    /// </summary>
    public static async Task<string> EnsureInstalledAsync(string workingDirectory, CancellationToken ct = default)
    {
        var vsCodeDir = Path.Combine(workingDirectory, ".vscode");
        Directory.CreateDirectory(vsCodeDir);
        var path = Path.Combine(vsCodeDir, "mcp.json");

        if (File.Exists(path))
        {
            var existing = await File.ReadAllTextAsync(path, ct);
            if (IsSpectraServerCurrent(existing))
            {
                return path; // already conformant — no rewrite, preserve formatting/comments
            }

            var merged = EnsureSpectraServer(existing); // may throw before any write (fail-loud)
            await File.WriteAllTextAsync(path, merged, ct);
            return path;
        }

        await File.WriteAllTextAsync(path, EnsureSpectraServer(null), ct);
        return path;
    }

    /// <summary>
    /// True when <paramref name="existingJson"/> already declares <c>servers.spectra</c> with the
    /// desired command/args. Tolerant of comments; returns false (not throws) on any parse/shape issue
    /// so the caller proceeds to the fail-loud merge path.
    /// </summary>
    private static bool IsSpectraServerCurrent(string existingJson)
    {
        try
        {
            if (JsonNode.Parse(existingJson, documentOptions: ParseOptions) is not JsonObject root)
                return false;
            if (root["servers"] is not JsonObject servers)
                return false;
            if (servers[ServerKey] is not JsonObject spectra)
                return false;
            if (spectra["command"]?.GetValue<string>() != "spectra-mcp")
                return false;
            if (spectra["args"] is not JsonArray args)
                return false;
            return args.Count == 1 && args[0]?.GetValue<string>() == ".";
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false; // GetValue type mismatch on a malformed entry
        }
    }
}
