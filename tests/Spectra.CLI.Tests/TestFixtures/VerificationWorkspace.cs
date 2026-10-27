using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Spectra.CLI.Commands.UpdateSkills;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectre.Console;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 097 (R8): helpers for the verification-hardening rows over a <see cref="BaselineTestWorkspace"/> — two suites
/// with verdict files and a stray verdict, explicit file times, the critic agent as 807047c installed it, and a captured
/// <c>update-skills</c> report. Uses only APIs that exist on <c>807047c</c>, so the proof rows compile against it.
/// </summary>
public static class VerificationWorkspace
{
    public const string Quotas = "docs/storage.md#Quotas";
    public const string StorageDoc = "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space.\n";
    public const string CriticAgent = ".claude/agents/spectra-critic.agent.md";
    public const string PinnedModel = "claude-sonnet-4-6";

    /// <summary>
    /// Suites <c>a</c> (TC-101…TC-104) and <c>b</c> (TC-201, TC-202) with indexes; grounded verdicts for TC-101 and
    /// TC-201, and a stray verdict of TC-999 (in no index). The workspace must already be initialised.
    /// </summary>
    public static void TwoSuites(this BaselineTestWorkspace ws)
    {
        ws.WriteDoc("docs/storage.md", StorageDoc);
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103", "TC-104" })
        {
            ws.WriteTest("a", id, [Quotas]);
        }

        foreach (var id in new[] { "TC-201", "TC-202" })
        {
            ws.WriteTest("b", id, [Quotas]);
        }

        ws.WriteVerdict("TC-101", "grounded");
        ws.WriteVerdict("TC-201", "grounded");
        ws.WriteVerdict("TC-999", "partial");
    }

    public static void SetFileTime(string path, DateTime utc) => File.SetLastWriteTimeUtc(path, utc);

    public static DateTime FileTime(string path) => File.GetLastWriteTimeUtc(path);

    /// <summary>Rewrites the record's <c>compiled_at</c> (the file's write time is set again afterwards by the caller).</summary>
    public static void SetCompiledAt(this BaselineTestWorkspace ws, string id, DateTime utc)
    {
        var path = ws.VerdictRecordPath(id);
        var record = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        record["compiled_at"] = new DateTimeOffset(utc, TimeSpan.Zero).ToString("o");
        File.WriteAllText(path, record.ToJsonString());
    }

    /// <summary>Edits a test by hand (its content fingerprint changes).</summary>
    public static void EditByHand(this BaselineTestWorkspace ws, string suite, string id) =>
        File.WriteAllText(ws.TestPath(suite, id), File.ReadAllText(ws.TestPath(suite, id)).Replace("It works", "It still works"));

    /// <summary>The installed critic agent's <c>model:</c> value.</summary>
    public static string? CriticModel(this BaselineTestWorkspace ws)
    {
        var path = ws.PathOf(CriticAgent);
        if (!File.Exists(path))
        {
            return null;
        }

        var match = Regex.Match(File.ReadAllText(path), @"^model:[ \t]*(?<m>\S+)[ \t]*\r?$", RegexOptions.Multiline);
        return match.Success ? match.Groups["m"].Value : null;
    }

    /// <summary>
    /// The critic agent as 807047c installed it: <c>model: claude-sonnet-4-6</c>, with the manifest recording that
    /// content (an unedited copy).
    /// </summary>
    public static void PinnedCriticAgent(this BaselineTestWorkspace ws)
    {
        var path = ws.PathOf(CriticAgent);
        var text = Regex.Replace(File.ReadAllText(path), @"^model:[^\r\n]*", $"model: {PinnedModel}", RegexOptions.Multiline);
        File.WriteAllText(path, text);
        var manifest = ws.Manifest();
        var files = manifest["files"]!.AsObject();
        foreach (var key in files.Select(f => f.Key).Where(k => ws.Relative(k) == CriticAgent).ToList())
        {
            files.Remove(key);
        }

        files[CriticAgent] = ManifestWorkspace.Hash(text);
        ws.SaveManifest(manifest);
    }

    /// <summary>Replaces <c>spectra.config.json</c> after applying <paramref name="change"/> to its JSON.</summary>
    public static void EditConfig(this BaselineTestWorkspace ws, Action<JsonObject> change)
    {
        var path = ws.PathOf("spectra.config.json");
        var config = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        change(config);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void SetCriticModel(this BaselineTestWorkspace ws, JsonNode? model) =>
        ws.EditConfig(c =>
        {
            var ai = c["ai"] as JsonObject ?? new JsonObject();
            c["ai"] = ai;
            var critic = ai["critic"] as JsonObject ?? new JsonObject();
            ai["critic"] = critic;
            critic["model"] = model;
        });

    /// <summary>Runs <c>update-skills</c> through the handler with a captured console; returns the exit and the report.</summary>
    public static async Task<(int Exit, string Report)> UpdateSkillsReportAsync(this BaselineTestWorkspace ws)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });
        console.Profile.Width = 1000;
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(ws.Root);
            var exit = await new UpdateSkillsHandler(VerbosityLevel.Normal, OutputFormat.Human, console).ExecuteAsync();
            return (exit, writer.ToString().Replace("\r\n", "\n"));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    /// <summary>SHA-256 of every file under the workspace (relative path → hash), excluding run-status files.</summary>
    public static Dictionary<string, string> Tree(this BaselineTestWorkspace ws) =>
        Directory.EnumerateFiles(ws.Root, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith(".spectra-result", StringComparison.Ordinal)
                        && !Path.GetFileName(f).StartsWith(".spectra-progress", StringComparison.Ordinal))
            .ToDictionary(f => Path.GetRelativePath(ws.Root, f).Replace('\\', '/'),
                f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));

    /// <summary>The JSON with <c>dry_run</c> and <c>timestamp</c> removed at every level (for real-vs-dry comparisons).</summary>
    public static string Comparable(string json)
    {
        var node = JsonNode.Parse(json);
        Strip(node);
        return node!.ToJsonString();

        static void Strip(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o:
                    o.Remove("dry_run");
                    o.Remove("timestamp");
                    foreach (var child in o.Select(p => p.Value).ToList())
                    {
                        Strip(child);
                    }

                    break;
                case JsonArray a:
                    foreach (var child in a)
                    {
                        Strip(child);
                    }

                    break;
            }
        }
    }

    /// <summary>A copy of the whole workspace (for running the same command twice on identical state).</summary>
    public static BaselineTestWorkspace CopyWorkspace(this BaselineTestWorkspace ws)
    {
        var copy = new BaselineTestWorkspace(writeConfig: false);
        foreach (var file in Directory.EnumerateFiles(ws.Root, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(copy.Root, Path.GetRelativePath(ws.Root, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(file));
        }

        return copy;
    }
}
