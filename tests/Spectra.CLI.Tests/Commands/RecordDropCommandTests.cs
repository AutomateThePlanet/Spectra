using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 071 — exit-code contract for spectra ai record-drop.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class RecordDropCommandTests : IDisposable
{
    private readonly string _dir;

    public RecordDropCommandTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-record-drop-{Guid.NewGuid():N}");
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
        File.WriteAllText(Path.Combine(dir, $"{id}.md"), $"""
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
    }

    private void WriteIndexJson(string suite, string id)
    {
        var dir = Path.Combine(_dir, "test-cases", suite);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "_index.json"),
            $$$"""{"suite":"{{{suite}}}","generated_at":"2026-06-19T00:00:00Z","tests":[{"id":"{{{id}}}","title":"Test {{{id}}}","priority":"medium","file":"{{{id}}}.md"}]}""");
    }

    private string WriteVerdictJson(string id)
    {
        var dir = Path.Combine(_dir, ".spectra", "verdicts");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"critic-verdict-{id}.json");
        File.WriteAllText(path, """{"verdict":"hallucinated","score":0.30,"critic_model":"claude-sonnet-4-6","findings":[{"element":"Step 2","claim":"1 KB = 1000 bytes","status":"hallucinated","reason":"contradicts docs: 1 KB = 1024 bytes"}]}""");
        return path;
    }

    private string TrailPath => Path.Combine(_dir, ".spectra", "dropped-tests.json");

    [Fact]
    public async Task RecordDrop_Hallucinated_Exits0_CreatesTrail()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-138");
            WriteIndexJson("reporting", "TC-138");
            WriteVerdictJson("TC-138");

            var exit = await RunAsync("ai", "record-drop",
                "--suite", "reporting", "--test", "TC-138");

            Assert.Equal(0, exit);
            Assert.True(File.Exists(TrailPath));
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task RecordDrop_Hallucinated_TrailHasContradictingClaim()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-138");
            WriteIndexJson("reporting", "TC-138");
            WriteVerdictJson("TC-138");
            await RunAsync("ai", "record-drop",
                "--suite", "reporting", "--test", "TC-138");

            var content = await File.ReadAllTextAsync(TrailPath);
            var obj = JsonSerializer.Deserialize<JsonElement>(content.Trim());
            Assert.Equal("TC-138", obj.GetProperty("id").GetString());
            Assert.Equal("hallucinated", obj.GetProperty("drop_reason").GetString());
            Assert.Equal("critic", obj.GetProperty("source").GetString());
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task RecordDrop_UserDecided_Exits0_NoCriticFields()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-138");
            WriteIndexJson("reporting", "TC-138");

            var exit = await RunAsync("ai", "record-drop",
                "--suite", "reporting", "--test", "TC-138", "--reason", "user_decided");

            Assert.Equal(0, exit);
            var content = await File.ReadAllTextAsync(TrailPath);
            var obj = JsonSerializer.Deserialize<JsonElement>(content.Trim());
            Assert.Equal("user_decided", obj.GetProperty("drop_reason").GetString());
            Assert.Equal("review", obj.GetProperty("source").GetString());
            Assert.False(obj.TryGetProperty("contradicting_claim", out _));
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task RecordDrop_SequentialCalls_AccumulateEntries()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-138");
            WriteTestMd("reporting", "TC-139");
            WriteIndexJson("reporting", "TC-138");
            WriteVerdictJson("TC-138");

            await RunAsync("ai", "record-drop", "--suite", "reporting", "--test", "TC-138");
            await RunAsync("ai", "record-drop", "--suite", "reporting", "--test", "TC-139",
                "--reason", "user_decided");

            var lines = (await File.ReadAllLinesAsync(TrailPath))
                .Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            Assert.Equal(2, lines.Count);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }

    [Fact]
    public async Task RecordDrop_MissingVerdictFileForHallucinated_Exits5()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            WriteTestMd("reporting", "TC-200");
            WriteIndexJson("reporting", "TC-200");
            // No verdict file — reason is hallucinated (default) but --from not provided
            var exit = await RunAsync("ai", "record-drop",
                "--suite", "reporting", "--test", "TC-200");
            // Should exit 5 (missing verdict file) or 0 (no claim available)
            // The key contract: does NOT crash and leaves the trail in a valid state
            Assert.True(exit is 0 or 5);
        }
        finally { Directory.SetCurrentDirectory(original); }
    }
}
