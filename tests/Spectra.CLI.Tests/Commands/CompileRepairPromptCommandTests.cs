using System.CommandLine;
using Spectra.CLI.Commands.Ai;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 071 FR4 — exit-code contract for spectra ai compile-repair-prompt.
/// Plain text to stdout; refuses non-partial verdicts (exit 4); partial emits prompt (exit 0).
/// </summary>
[Collection("WorkingDirectory")]
public sealed class CompileRepairPromptCommandTests : IDisposable
{
    private readonly string _dir;

    public CompileRepairPromptCommandTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-repair-cmd-{Guid.NewGuid():N}");
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

    private void WriteTestMd(string suite, string id)
    {
        var dir = Path.Combine(_dir, "test-cases", suite);
        Directory.CreateDirectory(dir);
        // No source_refs — command will still emit prompt, just with no docs section
        File.WriteAllText(Path.Combine(dir, $"{id}.md"), $"""
            ---
            id: {id}
            priority: medium
            criteria: []
            ---

            # Verify {id} file size conversion

            ## Steps

            1. Navigate to file settings
            2. Upload a 1024-byte file
            3. Note the displayed size

            ## Expected Result

            Size shows as 1.0 KB
            """);
        // The command resolves the test file path via _index.json
        File.WriteAllText(Path.Combine(dir, "_index.json"),
            $$"""{"suite":"{{suite}}","generated_at":"2026-06-19T00:00:00Z","tests":[{"id":"{{id}}","title":"Verify {{id}} file size conversion","priority":"medium","file":"{{id}}.md"}]}""");
    }

    private string WriteVerdictJson(string id, string verdict)
    {
        var dir = Path.Combine(_dir, ".spectra", "verdicts");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"critic-verdict-{id}.json");
        var json = verdict switch
        {
            "grounded" => """{"verdict":"grounded","score":0.95,"critic_model":"claude-sonnet-4-6","findings":[{"element":"Step 1","claim":"navigate","status":"grounded","evidence":"doc says so"}]}""",
            "partial" => """{"verdict":"partial","score":0.72,"critic_model":"claude-sonnet-4-6","findings":[{"element":"Expected Result","claim":"1.0 KB","status":"unverified","reason":"conversion factor not found in docs"}]}""",
            "hallucinated" => """{"verdict":"hallucinated","score":0.30,"critic_model":"claude-sonnet-4-6","findings":[{"element":"Step 2","claim":"1 KB = 1000 bytes","status":"hallucinated","reason":"contradicts docs"}]}""",
            _ => throw new ArgumentException(verdict)
        };
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public async Task CompileRepairPrompt_PartialVerdict_Exits0()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-113");
            WriteVerdictJson("TC-113", "partial");
            var exit = await RunAsync("ai", "compile-repair-prompt",
                "--suite", "reporting", "--test", "TC-113");
            Assert.Equal(0, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task CompileRepairPrompt_GroundedVerdict_Exits4()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-113");
            WriteVerdictJson("TC-113", "grounded");
            var exit = await RunAsync("ai", "compile-repair-prompt",
                "--suite", "reporting", "--test", "TC-113");
            Assert.Equal(4, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task CompileRepairPrompt_HallucinatedVerdict_Exits4()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-138");
            WriteVerdictJson("TC-138", "hallucinated");
            var exit = await RunAsync("ai", "compile-repair-prompt",
                "--suite", "reporting", "--test", "TC-138");
            Assert.Equal(4, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task CompileRepairPrompt_MissingVerdictFile_Exits5()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-113");
            // No verdict file
            var exit = await RunAsync("ai", "compile-repair-prompt",
                "--suite", "reporting", "--test", "TC-113");
            Assert.Equal(5, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task CompileRepairPrompt_InvalidVerdictJson_Exits6()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-113");
            var verdictDir = Path.Combine(_dir, ".spectra", "verdicts");
            Directory.CreateDirectory(verdictDir);
            File.WriteAllText(Path.Combine(verdictDir, "critic-verdict-TC-113.json"), "not valid json");
            var exit = await RunAsync("ai", "compile-repair-prompt",
                "--suite", "reporting", "--test", "TC-113");
            Assert.Equal(6, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task CompileRepairPrompt_FromOverride_UsesSpecifiedFile()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-113");
            var customPath = Path.Combine(_dir, "custom-verdict.json");
            File.WriteAllText(customPath, """{"verdict":"partial","score":0.6,"critic_model":"claude-sonnet-4-6","findings":[{"element":"Step 2","claim":"upload action","status":"unverified","reason":"not in docs"}]}""");
            var exit = await RunAsync("ai", "compile-repair-prompt",
                "--suite", "reporting", "--test", "TC-113",
                "--from", customPath);
            Assert.Equal(0, exit);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }
}
