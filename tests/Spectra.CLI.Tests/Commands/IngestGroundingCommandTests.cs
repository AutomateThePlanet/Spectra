using System.CommandLine;
using Spectra.CLI.Commands.Ai;

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
}
