using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.Options;
using Spectra.Core.Index;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 077 — regression guard for FR1: <c>ingest-verdict --suite --all</c> batch mode.
/// Verifies that all verdict files for a suite are classified in a single call, the summary
/// counts match, suite filtering works, and per-test <c>--from</c> mode remains backward-compatible.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class IngestVerdictBatchTests : IDisposable
{
    private readonly string _dir;
    private readonly string _verdictDir;
    private const string Suite = "unit-converter";
    private const string OtherSuite = "checkout";

    public IngestVerdictBatchTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-verdict-batch-{Guid.NewGuid():N}");
        _verdictDir = Path.Combine(_dir, ".spectra", "verdicts");
        Directory.CreateDirectory(_verdictDir);
        Directory.CreateDirectory(Path.Combine(_dir, "test-cases", Suite));
        Directory.CreateDirectory(Path.Combine(_dir, "test-cases", OtherSuite));

        var config = new
        {
            source = new { local_dir = "docs/", include_patterns = new[] { "**/*.md" } },
            tests = new { dir = "test-cases/" },
            ai = new { providers = new[] { new { name = "test", model = "test-model", enabled = true, priority = 1 } } }
        };
        File.WriteAllText(Path.Combine(_dir, "spectra.config.json"), JsonSerializer.Serialize(config));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private async Task WriteIndexAsync(string suite, params string[] ids)
    {
        var suitePath = Path.Combine(_dir, "test-cases", suite);
        var index = new MetadataIndex
        {
            Suite = suite,
            GeneratedAt = DateTime.UtcNow,
            Tests = ids.Select(id => new TestIndexEntry { Id = id, File = $"{id}.md", Title = id, Priority = "high" }).ToList()
        };
        await new IndexWriter().WriteAsync(IndexWriter.GetIndexPath(suitePath), index);
    }

    private static void WriteVerdictFile(string verdictDir, string id, string verdict = "grounded", double score = 0.9)
    {
        var json = JsonSerializer.Serialize(new { verdict, score, findings = Array.Empty<object>() });
        File.WriteAllText(Path.Combine(verdictDir, $"critic-verdict-{id}.json"), json);
    }

    private async Task<(int Exit, string Out, string Err)> RunBatchAsync(string suite)
    {
        var original = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var outW = new StringWriter();
        var errW = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            Console.SetOut(outW);
            Console.SetError(errW);
            var root = new RootCommand();
            GlobalOptions.AddTo(root);
            root.AddCommand(AiCommand.Create());
            var exit = await root.InvokeAsync(
                new[] { "ai", "ingest-verdict", "--suite", suite, "--all", "--output-format", "json" });
            return (exit, outW.ToString(), errW.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Directory.SetCurrentDirectory(original);
        }
    }

    private async Task<(int Exit, string Out, string Err)> RunPerTestAsync(string from)
    {
        var original = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var outW = new StringWriter();
        var errW = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            Console.SetOut(outW);
            Console.SetError(errW);
            var root = new RootCommand();
            GlobalOptions.AddTo(root);
            root.AddCommand(AiCommand.Create());
            var exit = await root.InvokeAsync(
                new[] { "ai", "ingest-verdict", "--from", from, "--output-format", "json" });
            return (exit, outW.ToString(), errW.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public async Task BatchIngest_AllSuiteVerdicts_CountsMatch()
    {
        // Setup: 3 grounded + 1 partial verdict for suite, plus 1 verdict for another suite (filtered out)
        await WriteIndexAsync(Suite, "TC-100", "TC-101", "TC-102", "TC-103");
        await WriteIndexAsync(OtherSuite, "TC-200");
        WriteVerdictFile(_verdictDir, "TC-100", "grounded", 0.95);
        WriteVerdictFile(_verdictDir, "TC-101", "grounded", 0.90);
        WriteVerdictFile(_verdictDir, "TC-102", "grounded", 0.88);
        WriteVerdictFile(_verdictDir, "TC-103", "partial", 0.65);
        WriteVerdictFile(_verdictDir, "TC-200", "grounded", 0.90); // other suite — filtered out

        var (exit, out_, _) = await RunBatchAsync(Suite);

        Assert.Equal(0, exit);
        var result = JsonSerializer.Deserialize<JsonElement>(out_.Trim());
        Assert.Equal(3, result.GetProperty("grounded").GetInt32());
        Assert.Equal(1, result.GetProperty("partial").GetInt32());
        Assert.Equal(0, result.GetProperty("hallucinated").GetInt32());
        Assert.Equal(0, result.GetProperty("errors").GetInt32());
    }

    [Fact]
    public async Task BatchIngest_OtherSuiteVerdicts_AreSkipped()
    {
        // Only TC-200 belongs to OtherSuite; TC-100 is in Suite but we're querying OtherSuite
        await WriteIndexAsync(Suite, "TC-100");
        await WriteIndexAsync(OtherSuite, "TC-200");
        WriteVerdictFile(_verdictDir, "TC-100", "grounded", 0.9);
        WriteVerdictFile(_verdictDir, "TC-200", "partial", 0.7);

        var (exit, out_, _) = await RunBatchAsync(OtherSuite);

        Assert.Equal(0, exit);
        var result = JsonSerializer.Deserialize<JsonElement>(out_.Trim());
        // Only TC-200 should be processed (belongs to OtherSuite)
        Assert.Equal(0, result.GetProperty("grounded").GetInt32());
        Assert.Equal(1, result.GetProperty("partial").GetInt32());
    }

    [Fact]
    public async Task BatchIngest_EmptyVerdictDir_ReturnsZero()
    {
        await WriteIndexAsync(Suite, "TC-100");
        // No verdict files created — directory is empty

        var (exit, out_, _) = await RunBatchAsync(Suite);

        Assert.Equal(0, exit);
        var result = JsonSerializer.Deserialize<JsonElement>(out_.Trim());
        Assert.Equal(0, result.GetProperty("grounded").GetInt32());
        Assert.Equal(0, result.GetProperty("partial").GetInt32());
        Assert.Equal(0, result.GetProperty("errors").GetInt32());
    }

    [Fact]
    public async Task PerTestMode_StillWorks_AfterBatchAdded()
    {
        // Confirm backward compatibility: --from still works
        var verdictFile = Path.Combine(_verdictDir, "critic-verdict-TC-100.json");
        var json = JsonSerializer.Serialize(new { verdict = "grounded", score = 0.92, findings = Array.Empty<object>() });
        File.WriteAllText(verdictFile, json);

        var (exit, out_, _) = await RunPerTestAsync(verdictFile);

        Assert.Equal(0, exit);
        var result = JsonSerializer.Deserialize<JsonElement>(out_.Trim());
        Assert.Equal("Verdict", result.GetProperty("outcome").GetString());
        Assert.Equal("grounded", result.GetProperty("verdict").GetString());
    }

    [Fact]
    public async Task BatchIngest_MissingIndex_ReturnsError()
    {
        // No _index.json created — command should fail with exit 1
        WriteVerdictFile(_verdictDir, "TC-100");

        var (exit, _, err) = await RunBatchAsync(Suite);

        Assert.Equal(1, exit);
        Assert.Contains("index", err, StringComparison.OrdinalIgnoreCase);
    }
}
