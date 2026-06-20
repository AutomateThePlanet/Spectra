using System.CommandLine;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.Options;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 071 — exit-code contract for spectra ai ingest-grounding.
/// No model; no network. Reads a verdict JSON file and writes grounding block to .md.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class IngestGroundingCommandTests : IDisposable
{
    private readonly string _dir;

    public IngestGroundingCommandTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-ingest-grounding-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private static async Task<int> RunAsync(params string[] args)
    {
        var root = new RootCommand();
        GlobalOptions.AddTo(root);
        root.AddCommand(AiCommand.Create());
        return await root.InvokeAsync(args);
    }

    private string WriteTestMd(string suite, string id)
    {
        var dir = Path.Combine(_dir, "test-cases", suite);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{id}.md");
        File.WriteAllText(path, $"""
            ---
            id: {id}
            priority: medium
            criteria: []
            ---

            # Test {id}

            ## Steps

            1. Step one

            ## Expected Result

            Expected result
            """);
        // Also write the index so the command can find the file by test ID
        File.WriteAllText(Path.Combine(dir, "_index.json"),
            $$"""{"suite":"{{suite}}","generated_at":"2026-06-19T00:00:00Z","tests":[{"id":"{{id}}","title":"Test {{id}}","priority":"medium","file":"{{id}}.md"}]}""");
        return path;
    }

    private string WriteVerdictJson(string id, string verdict)
    {
        var dir = Path.Combine(_dir, ".spectra", "verdicts");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"critic-verdict-{id}.json");
        var json = verdict switch
        {
            "grounded" => """{"verdict":"grounded","score":0.95,"critic_model":"claude-sonnet-4-6","findings":[{"element":"Step 1","claim":"step action","status":"grounded","evidence":"doc says so"}]}""",
            "partial" => """{"verdict":"partial","score":0.72,"critic_model":"claude-sonnet-4-6","findings":[{"element":"Step 1","claim":"step action","status":"grounded","evidence":"doc says so"},{"element":"Expected Result","claim":"exact value","status":"unverified","reason":"not in docs"}]}""",
            "hallucinated" => """{"verdict":"hallucinated","score":0.30,"critic_model":"claude-sonnet-4-6","findings":[{"element":"Step 2","claim":"1 KB = 1000 bytes","status":"hallucinated","reason":"contradicts docs"}]}""",
            _ => throw new ArgumentException($"Unknown verdict: {verdict}")
        };
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public async Task IngestGrounding_GroundedVerdict_Exits0()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-113");
            WriteVerdictJson("TC-113", "grounded");
            var exit = await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--test", "TC-113");
            Assert.Equal(0, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task IngestGrounding_GroundedVerdict_WritesGroundingBlock()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var testPath = WriteTestMd("reporting", "TC-113");
            WriteVerdictJson("TC-113", "grounded");
            await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--test", "TC-113");

            var content = await File.ReadAllTextAsync(testPath);
            Assert.Contains("verdict: grounded", content);
            Assert.Contains("score: 0.95", content);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task IngestGrounding_PartialVerdict_Exits0_WithFlaggedBlock()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var testPath = WriteTestMd("reporting", "TC-113");
            WriteVerdictJson("TC-113", "partial");
            var exit = await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--test", "TC-113");

            Assert.Equal(0, exit);
            var content = await File.ReadAllTextAsync(testPath);
            Assert.Contains("verdict: partial", content);
            Assert.Contains("flagged_for_review: true", content);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task IngestGrounding_HallucinatedVerdict_Exits4()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-138");
            WriteVerdictJson("TC-138", "hallucinated");
            var exit = await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--test", "TC-138");
            Assert.Equal(4, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task IngestGrounding_MissingVerdictFile_Exits5()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-200");
            // No verdict file written — default path does not exist
            var exit = await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--test", "TC-200");
            Assert.Equal(5, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task IngestGrounding_WithRepaired_WritesRepairedBlock()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var testPath = WriteTestMd("reporting", "TC-113");
            WriteVerdictJson("TC-113", "grounded");
            await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--test", "TC-113",
                "--repaired", "--repair-attempts", "1");

            var content = await File.ReadAllTextAsync(testPath);
            Assert.Contains("repaired: true", content);
            Assert.Contains("repair_attempts: 1", content);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task IngestGrounding_FromOverride_UsesSpecifiedFile()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var testPath = WriteTestMd("reporting", "TC-113");
            // Write verdict to a custom path
            var customPath = Path.Combine(_dir, "custom-verdict.json");
            File.WriteAllText(customPath, """{"verdict":"grounded","score":0.88,"critic_model":"claude-sonnet-4-6","findings":[]}""");

            var exit = await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--test", "TC-113",
                "--from", customPath);

            Assert.Equal(0, exit);
            var content = await File.ReadAllTextAsync(testPath);
            Assert.Contains("score: 0.88", content);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    // ─── Spec 073: --all batch mode tests ──────────────────────────────────────

    [Fact]
    public async Task AllMode_NoVerdictDir_WritesNothing_ExitsZero()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-500");
            // No verdict dir — batch should succeed with 0 written
            var exit = await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--all", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = System.Text.Json.JsonDocument.Parse(json).RootElement;
            Assert.Equal("batch", result.GetProperty("mode").GetString());
            Assert.Equal(0, result.GetProperty("written").GetInt32());
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task AllMode_GroundedVerdicts_WritesBatchBlocks()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var testPath = WriteTestMd("reporting", "TC-501");
            WriteVerdictJson("TC-501", "grounded");

            var exit = await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--all");
            Assert.Equal(0, exit);
            var content = await File.ReadAllTextAsync(testPath);
            Assert.Contains("verdict: grounded", content);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task AllMode_SkipsAlreadyWritten_Idempotent()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            // Write test with grounding block already present
            var dir = Path.Combine(_dir, "test-cases", "reporting");
            Directory.CreateDirectory(dir);
            var testPath = Path.Combine(dir, "TC-502.md");
            File.WriteAllText(testPath, """
                ---
                id: TC-502
                priority: medium
                criteria: []
                grounding:
                  verdict: grounded
                  score: 0.95
                  generator: claude-code-session
                  critic: claude-sonnet-4-6
                  verified_at: 2026-06-19T10:00:00Z
                  flagged_for_review: false
                ---

                # Test TC-502

                ## Steps

                1. Step one

                ## Expected Result

                Expected
                """);
            File.WriteAllText(Path.Combine(dir, "_index.json"),
                """{"suite":"reporting","generated_at":"2026-06-19T00:00:00Z","tests":[{"id":"TC-502","title":"Test TC-502","priority":"medium","file":"TC-502.md"}]}""");
            WriteVerdictJson("TC-502", "grounded");

            var exit = await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--all", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = System.Text.Json.JsonDocument.Parse(json).RootElement;
            Assert.Equal(0, result.GetProperty("written").GetInt32());
            Assert.Equal(1, result.GetProperty("skipped_already_written").GetInt32());
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task AllMode_SkipsPartialWithoutRepaired()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-503");
            WriteVerdictJson("TC-503", "partial");

            var exit = await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--all", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = System.Text.Json.JsonDocument.Parse(json).RootElement;
            Assert.Equal(0, result.GetProperty("written").GetInt32());
            Assert.Equal(1, result.GetProperty("skipped_partial_pre_repair").GetInt32());

            // Test .md must NOT have a grounding block yet (needs repair first)
            var dir = Path.Combine(_dir, "test-cases", "reporting");
            var content = await File.ReadAllTextAsync(Path.Combine(dir, "TC-503.md"));
            Assert.DoesNotContain("grounding:", content);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task AllMode_WritesPartialWithRepaired()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var testPath = WriteTestMd("reporting", "TC-504");
            WriteVerdictJson("TC-504", "partial");

            var exit = await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--all",
                "--repaired", "--repair-attempts", "1", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = System.Text.Json.JsonDocument.Parse(json).RootElement;
            Assert.Equal(1, result.GetProperty("written").GetInt32());
            Assert.Equal(0, result.GetProperty("skipped_partial_pre_repair").GetInt32());

            var content = await File.ReadAllTextAsync(testPath);
            Assert.Contains("verdict: partial", content);
            // With --repaired, FlaggedForReview is false (repair already attempted); repaired: true is set
            Assert.Contains("repaired: true", content);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task AllMode_MixedSuite_CorrectCounts()
    {
        var original = Directory.GetCurrentDirectory();
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            Directory.SetCurrentDirectory(_dir);
            // TC-510: grounded, no block yet → will be written
            WriteTestMd("reporting", "TC-510");
            WriteVerdictJson("TC-510", "grounded");

            // TC-511: partial, no block yet, no --repaired → will be skipped
            WriteTestMd("reporting", "TC-511");
            WriteVerdictJson("TC-511", "partial");

            // TC-512: grounded, already has block → will be skipped (idempotent)
            var dir = Path.Combine(_dir, "test-cases", "reporting");
            File.WriteAllText(Path.Combine(dir, "TC-512.md"), """
                ---
                id: TC-512
                priority: medium
                criteria: []
                grounding:
                  verdict: grounded
                  score: 0.95
                  generator: claude-code-session
                  critic: claude-sonnet-4-6
                  verified_at: 2026-06-19T10:00:00Z
                  flagged_for_review: false
                ---

                # Test TC-512

                ## Steps

                1. Step one

                ## Expected Result

                Expected
                """);
            WriteVerdictJson("TC-512", "grounded");

            // Consolidated index for all three
            File.WriteAllText(Path.Combine(dir, "_index.json"),
                """{"suite":"reporting","generated_at":"2026-06-19T00:00:00Z","tests":[{"id":"TC-510","title":"T","priority":"medium","file":"TC-510.md"},{"id":"TC-511","title":"T","priority":"medium","file":"TC-511.md"},{"id":"TC-512","title":"T","priority":"medium","file":"TC-512.md"}]}""");

            var exit = await RunAsync("ai", "ingest-grounding", "--suite", "reporting", "--all", "--output-format", "json");
            Assert.Equal(0, exit);
            var json = captured.ToString().Trim();
            var result = System.Text.Json.JsonDocument.Parse(json).RootElement;
            Assert.Equal(1, result.GetProperty("written").GetInt32());
            Assert.Equal(1, result.GetProperty("skipped_already_written").GetInt32());
            Assert.Equal(1, result.GetProperty("skipped_partial_pre_repair").GetInt32());
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task NeitherTestNorAll_ReturnsError()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-520");
            // Missing both --test and --all → should return error
            var exit = await RunAsync("ai", "ingest-grounding", "--suite", "reporting");
            Assert.NotEqual(0, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }
}
