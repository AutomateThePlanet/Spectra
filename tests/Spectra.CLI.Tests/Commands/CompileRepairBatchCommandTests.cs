using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.Commands.Generate;
using Spectra.CLI.Options;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 072 FR1 — compile-repair-batch command: deterministic batch repair manifest.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class CompileRepairBatchCommandTests : IDisposable
{
    private readonly string _dir;

    public CompileRepairBatchCommandTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-repair-batch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private void WriteTestMd(string suite, string id, bool withGrounding = false)
    {
        var dir = Path.Combine(_dir, "test-cases", suite);
        Directory.CreateDirectory(dir);
        var grounding = withGrounding
            ? "grounding:\n  verdict: grounded\n  score: 0.95\n  generator: claude-code-session\n  critic: claude-sonnet-4-6\n  verified_at: 2026-06-19T10:00:00Z\n  flagged_for_review: false\n"
            : "";
        File.WriteAllText(Path.Combine(dir, $"{id}.md"), $"""
            ---
            id: {id}
            priority: medium
            criteria: []
            source_refs: []
            {grounding}---

            # Test {id}

            ## Steps

            1. Open the app
            2. Navigate to the page

            ## Expected Result

            Page loads correctly
            """);
        File.WriteAllText(Path.Combine(dir, "_index.json"),
            $$"""{"suite":"{{suite}}","generated_at":"2026-06-19T00:00:00Z","tests":[{"id":"{{id}}","title":"Test {{id}}","priority":"medium","file":"{{suite}}/{{id}}.md"}]}""");
    }

    private void AppendTestToIndex(string suite, string id)
    {
        // Overwrite index with multiple entries
        var dir = Path.Combine(_dir, "test-cases", suite);
        var existingIds = Directory.GetFiles(dir, "TC-*.md")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToList();
        var entries = string.Join(",", existingIds.Select(tid =>
            $$$"""{"id":"{{{tid}}}","title":"Test {{{tid}}}","priority":"medium","file":"{{{suite}}}/{{{tid}}}.md"}"""));
        File.WriteAllText(Path.Combine(dir, "_index.json"),
            $$"""{"suite":"{{suite}}","generated_at":"2026-06-19T00:00:00Z","tests":[{{entries}}]}""");
    }

    private void WriteVerdictFile(string id, string verdict, string findings = "")
    {
        var verdictDir = Path.Combine(_dir, ".spectra", "verdicts");
        Directory.CreateDirectory(verdictDir);
        var findingsJson = string.IsNullOrEmpty(findings)
            ? """[{"element":"step_1","claim":"Open the app","status":"unverified","reason":"No doc ref"}]"""
            : findings;
        File.WriteAllText(Path.Combine(verdictDir, $"critic-verdict-{id}.json"),
            $$"""{"verdict":"{{verdict}}","score":0.65,"summary":"findings","findings":{{findingsJson}}}""");
    }

    private static async Task<int> RunAsync(params string[] args)
    {
        var root = new RootCommand();
        GlobalOptions.AddTo(root);
        root.AddCommand(AiCommand.Create());
        return await root.InvokeAsync(args);
    }

    [Fact]
    public async Task NoVerdictDir_EmitsEmptyArray()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-400");
            var exit = await RunAsync("ai", "compile-repair-batch", "--suite", "smoke");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            Assert.Equal("[]", json);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task PartialWithoutGrounding_EmittedInManifest()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-401");
            WriteVerdictFile("TC-401", "partial");

            var exit = await RunAsync("ai", "compile-repair-batch", "--suite", "smoke");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var entries = JsonSerializer.Deserialize<JsonElement[]>(json);
            Assert.NotNull(entries);
            Assert.Single(entries);
            Assert.Equal("TC-401", entries![0].GetProperty("id").GetString());
            Assert.Contains("SPECTRA Test Repair", entries[0].GetProperty("prompt").GetString());
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task AlreadyGrounded_SkippedInManifest()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-402", withGrounding: true);
            WriteVerdictFile("TC-402", "partial");

            var exit = await RunAsync("ai", "compile-repair-batch", "--suite", "smoke");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var entries = JsonSerializer.Deserialize<JsonElement[]>(json);
            Assert.NotNull(entries);
            Assert.Empty(entries!); // Already grounded — resume checkpoint skips it
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task GroundedVerdict_NotIncludedInManifest()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-403");
            // grounded verdict — should not appear in repair manifest
            WriteVerdictFile("TC-403", "grounded",
                """[{"element":"step_1","claim":"Open the app","status":"grounded","reason":""}]""");

            var exit = await RunAsync("ai", "compile-repair-batch", "--suite", "smoke");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var entries = JsonSerializer.Deserialize<JsonElement[]>(json);
            Assert.Empty(entries!);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task ManifestEntry_HasRequiredFields()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-404");
            WriteVerdictFile("TC-404", "partial");

            var exit = await RunAsync("ai", "compile-repair-batch", "--suite", "smoke");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var entries = JsonSerializer.Deserialize<JsonElement[]>(json);
            Assert.NotNull(entries);
            var entry = Assert.Single(entries!);

            Assert.True(entry.TryGetProperty("id", out _), "id field required");
            Assert.True(entry.TryGetProperty("prompt", out _), "prompt field required");
            Assert.True(entry.TryGetProperty("source_refs", out _), "source_refs field required");
            Assert.True(entry.TryGetProperty("file", out _), "file field required");
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task FileField_IsRelativePath()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("smoke", "TC-405");
            WriteVerdictFile("TC-405", "partial");

            var exit = await RunAsync("ai", "compile-repair-batch", "--suite", "smoke");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var entries = JsonSerializer.Deserialize<JsonElement[]>(json);
            var entry = Assert.Single(entries!);
            var file = entry.GetProperty("file").GetString();
            Assert.NotNull(file);
            Assert.False(Path.IsPathRooted(file), $"file should be relative: {file}");
            Assert.EndsWith(".md", file);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task MixedSuite_OnlyUngroundedPartialsInManifest()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            // grounded (skip: not partial)
            WriteTestMd("smoke", "TC-410");
            AppendTestToIndex("smoke", "TC-410");
            WriteVerdictFile("TC-410", "grounded",
                """[{"element":"step_1","claim":"Open the app","status":"grounded","reason":""}]""");
            // partial, no grounding written (include)
            WriteTestMd("smoke", "TC-411");
            AppendTestToIndex("smoke", "TC-411");
            WriteVerdictFile("TC-411", "partial");
            // partial, grounding already written (skip: resume checkpoint)
            WriteTestMd("smoke", "TC-412", withGrounding: true);
            AppendTestToIndex("smoke", "TC-412");
            WriteVerdictFile("TC-412", "partial");

            var exit = await RunAsync("ai", "compile-repair-batch", "--suite", "smoke");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var entries = JsonSerializer.Deserialize<JsonElement[]>(json);
            Assert.NotNull(entries);
            Assert.Single(entries!);
            Assert.Equal("TC-411", entries![0].GetProperty("id").GetString());
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task MissingSuite_ReturnsError()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var exit = await RunAsync("ai", "compile-repair-batch", "--suite", "nonexistent-suite");
            Assert.NotEqual(0, exit);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }
}
