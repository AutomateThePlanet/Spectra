using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;
using Spectra.CLI;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 084: a temp workspace for the source-baseline commands. <see cref="RunAsync"/> runs the real root command
/// (every command the CLI has) from the workspace root, so tests using it must be in the
/// <see cref="CriteriaCommandsCollection"/> (current directory and console are global).
/// </summary>
public sealed class BaselineTestWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), $"spectra-084-baseline-{Guid.NewGuid():N}");

    public BaselineTestWorkspace(bool writeConfig = true)
    {
        Directory.CreateDirectory(PathOf("docs/criteria"));
        Directory.CreateDirectory(PathOf("test-cases"));
        Directory.CreateDirectory(PathOf(".spectra"));
        if (writeConfig)
        {
            File.WriteAllText(PathOf("spectra.config.json"), JsonSerializer.Serialize(SpectraConfig.Default));
        }
    }

    // Spec 086: a workspace may hold a git repository, whose object files are read-only on Windows.
    public void Dispose() => GitFixture.DeleteTree(Root);

    public string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public string WriteDoc(string relative, string text)
    {
        var path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text.Replace("\r\n", "\n"));
        return path;
    }

    public string WriteCriteriaFile(string relativeToCriteriaDir, string yaml)
    {
        var path = PathOf("docs/criteria/" + relativeToCriteriaDir);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, yaml.Replace("\r\n", "\n"));
        return path;
    }

    /// <summary>A criteria file with one criterion per (id, text) pair.</summary>
    public void WriteCriteria(string file, params (string Id, string Text)[] criteria) =>
        WriteCriteriaFile(file, "criteria:\n" + string.Concat(criteria.Select(c =>
            $"  - id: {c.Id}\n    text: \"{c.Text}\"\n    rfc2119: MUST\n    source_type: document\n")));

    /// <summary>Test markdown with <c>source_refs</c> and <c>criteria</c> frontmatter.</summary>
    public static string TestMarkdown(string id, string[] refs, string[]? criteria = null, string title = "A test") =>
        "---\n" +
        $"id: {id}\n" +
        "priority: high\n" +
        (refs.Length == 0 ? "" : "source_refs:\n" + string.Concat(refs.Select(r => $"  - \"{r}\"\n"))) +
        (criteria is { Length: > 0 } ? "criteria:\n" + string.Concat(criteria.Select(c => $"  - {c}\n")) : "") +
        "---\n\n" +
        $"# {title} {id}\n\n" +
        "## Steps\n\n1. Do the thing\n\n" +
        "## Expected Result\n\nIt works\n";

    /// <summary>Writes a test file and regenerates the suite's <c>_index.json</c>.</summary>
    public string WriteTest(string suite, string id, string[] refs, string[]? criteria = null, string title = "A test")
    {
        var path = PathOf($"test-cases/{suite}/{id}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, TestMarkdown(id, refs, criteria, title));
        RebuildIndex(suite);
        return path;
    }

    public void RebuildIndex(string suite)
    {
        var dir = PathOf($"test-cases/{suite}");
        var parser = new TestCaseParser();
        var tests = Directory.GetFiles(dir, "*.md")
            .Where(f => !Path.GetFileName(f).StartsWith('_'))
            .Select(f => parser.Parse(File.ReadAllText(f), Path.GetFileName(f)))
            .Where(r => r.IsSuccess)
            .Select(r => r.Value!)
            .ToList();
        new IndexWriter().WriteAsync(Path.Combine(dir, "_index.json"), new IndexGenerator().Generate(suite, tests)).GetAwaiter().GetResult();
    }

    public string BaselinePath(string suite) => PathOf($"test-cases/{suite}/_source-baseline.json");

    public string? ReadBaseline(string suite) => File.Exists(BaselinePath(suite)) ? File.ReadAllText(BaselinePath(suite)) : null;

    public JsonElement BaselineJson(string suite) => Json(ReadBaseline(suite) ?? throw new InvalidOperationException("no baseline"));

    public string[] SnapshotFiles() =>
        Directory.Exists(PathOf(".spectra/source-snapshots"))
            ? Directory.GetFiles(PathOf(".spectra/source-snapshots")).Select(f => Path.GetFileName(f)).OrderBy(n => n, StringComparer.Ordinal).ToArray()
            : [];

    /// <summary>SHA-256 of every file under the root, including <c>.spectra/</c>.</summary>
    public IReadOnlyDictionary<string, string> Snapshot(params string[] except) =>
        Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
            .Where(r => !except.Contains(r))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToDictionary(r => r, r => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(PathOf(r)))));

    /// <summary>Hashes of the test files only.</summary>
    public IReadOnlyDictionary<string, string> TestFiles() =>
        Snapshot().Where(kv => kv.Key.StartsWith("test-cases/", StringComparison.Ordinal) && kv.Key.EndsWith(".md", StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>Writes a generated-tests payload for <c>ai ingest-tests</c> and returns its path.</summary>
    public string GeneratedPayload(params (string Id, string[] Refs, string[] Criteria)[] tests)
    {
        var path = PathOf($".spectra/gen-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(tests.Select(t => new
        {
            id = t.Id,
            title = $"Test {t.Id}",
            priority = "high",
            steps = new[] { "Do the thing" },
            expected_result = "It works",
            source_refs = t.Refs,
            criteria = t.Criteria
        })));
        return path;
    }

    /// <summary>Runs <c>spectra</c> from the workspace root, capturing stdout and stderr separately.</summary>
    public async Task<(int Exit, string Out, string Err)> RunAsync(params string[] args)
    {
        var originalDir = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(Root);
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exit = await Program.CreateRootCommand().InvokeAsync(args);
            return (exit, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    /// <summary>Runs a command in JSON mode, asserts it exited 0, and parses its stdout.</summary>
    public async Task<JsonElement> RunJsonAsync(params string[] args)
    {
        var (exit, output, error) = await RunAsync([.. args, "--output-format", "json"]);
        if (exit != 0)
        {
            throw new Xunit.Sdk.XunitException($"'{string.Join(' ', args)}' exited {exit}\nstdout: {output}\nstderr: {error}");
        }

        return Json(output);
    }

    public Task<JsonElement> TriageAsync(string suite = "storage", params string[] extra) =>
        RunJsonAsync(["ai", "update-triage", "--suite", suite, .. extra]);

    public Task<JsonElement> InitBaselineAsync(string suite = "storage", params string[] extra) =>
        RunJsonAsync(["ai", "source-baseline", "--init", "--suite", suite, .. extra]);

    public static JsonElement Json(string text)
    {
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    /// <summary>The first group whose tests include <paramref name="id"/>, or default.</summary>
    public static JsonElement GroupOf(JsonElement triage, string id) =>
        triage.GetProperty("groups").EnumerateArray()
            .FirstOrDefault(g => g.GetProperty("tests").EnumerateArray().Any(t => t.GetString() == id));

    /// <summary>A suite's count for a bucket (absent from the compact output = 0).</summary>
    public static int Count(JsonElement triage, string suite, string bucket) =>
        triage.GetProperty("suites").EnumerateArray().Single(s => s.GetProperty("suite").GetString() == suite)
            .GetProperty("counts").TryGetProperty(bucket, out var n) ? n.GetInt32() : 0;
}
