using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.IO;
using Spectra.CLI.Options;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 077 — regression guard for FR2: <c>ingest-update {suite} --all</c> batch mode.
/// Verifies that all staged update files in <c>.spectra/updates/{suite}/updated-{id}.json</c>
/// are ingested in a single call, summary counts match, and per-entry <c>--test-id --from</c>
/// mode remains backward-compatible.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class IngestUpdateBatchTests : IDisposable
{
    private readonly string _dir;
    private readonly string _testsPath;
    private const string Suite = "checkout";
    private const string TestId1 = "TC-100";
    private const string TestId2 = "TC-101";

    public IngestUpdateBatchTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-update-batch-{Guid.NewGuid():N}");
        _testsPath = Path.Combine(_dir, "test-cases");
        Directory.CreateDirectory(_testsPath);
        Directory.CreateDirectory(Path.Combine(_dir, "test-cases", Suite));

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

    private async Task WriteOriginalTestAsync(string id, string title = "Original title")
    {
        var testCase = new TestCase
        {
            Id = id,
            FilePath = $"{id}.md",
            Title = title,
            Priority = Priority.High,
            Steps = ["original step"],
            ExpectedResult = "original result"
        };
        var path = TestFileWriter.GetFilePath(_testsPath, Suite, id);
        await new TestFileWriter().WriteAsync(path, testCase, default);
    }

    private void WriteUpdateStagingFile(string id, string title = "Updated title")
    {
        var stagingDir = Path.Combine(_dir, ".spectra", "updates", Suite);
        Directory.CreateDirectory(stagingDir);
        var json = JsonSerializer.Serialize(new[]
        {
            new
            {
                id,
                title,
                priority = "high",
                steps = new[] { "updated step" },
                expected_result = "updated result"
            }
        });
        File.WriteAllText(Path.Combine(stagingDir, $"updated-{id}.json"), json);
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
                new[] { "ai", "ingest-update", suite, "--all", "--output-format", "json" });
            return (exit, outW.ToString(), errW.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Directory.SetCurrentDirectory(original);
        }
    }

    private async Task<(int Exit, string Out, string Err)> RunPerEntryAsync(
        string suite, string testId, string fromFile)
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
                new[] { "ai", "ingest-update", suite, "--test-id", testId, "--from", fromFile, "--output-format", "json" });
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
    public async Task BatchIngest_AllStagedUpdates_CountsMatch()
    {
        // Setup: two original tests on disk, two staged update files
        await WriteOriginalTestAsync(TestId1, "Original TC-100");
        await WriteOriginalTestAsync(TestId2, "Original TC-101");
        WriteUpdateStagingFile(TestId1, "Updated TC-100");
        WriteUpdateStagingFile(TestId2, "Updated TC-101");

        var (exit, out_, _) = await RunBatchAsync(Suite);

        Assert.Equal(0, exit);
        var result = JsonSerializer.Deserialize<JsonElement>(out_.Trim());
        Assert.Equal(2, result.GetProperty("written").GetInt32());
        Assert.Equal(0, result.GetProperty("errors").GetInt32());
    }

    [Fact]
    public async Task BatchIngest_NoStagingDir_ReturnsZero()
    {
        // No .spectra/updates/{suite}/ directory
        await WriteOriginalTestAsync(TestId1);

        var (exit, out_, _) = await RunBatchAsync(Suite);

        Assert.Equal(0, exit);
        var result = JsonSerializer.Deserialize<JsonElement>(out_.Trim());
        Assert.Equal(0, result.GetProperty("written").GetInt32());
    }

    [Fact]
    public async Task BatchIngest_MalformedUpdateFile_FailsLoudPerEntry_ContinuesRest()
    {
        // TC-100 has a valid staging file; TC-101 has a malformed one
        await WriteOriginalTestAsync(TestId1, "Original TC-100");
        await WriteOriginalTestAsync(TestId2, "Original TC-101");
        WriteUpdateStagingFile(TestId1, "Updated TC-100");

        var stagingDir = Path.Combine(_dir, ".spectra", "updates", Suite);
        Directory.CreateDirectory(stagingDir);
        File.WriteAllText(Path.Combine(stagingDir, $"updated-{TestId2}.json"), "NOT VALID JSON");

        var (exit, out_, _) = await RunBatchAsync(Suite);

        Assert.Equal(0, exit); // batch continues even with partial errors
        var result = JsonSerializer.Deserialize<JsonElement>(out_.Trim());
        Assert.Equal(1, result.GetProperty("written").GetInt32());
        Assert.Equal(1, result.GetProperty("errors").GetInt32());
    }

    [Fact]
    public async Task PerEntryMode_StillWorks_AfterBatchAdded()
    {
        // Confirm backward compatibility: --test-id --from still works
        await WriteOriginalTestAsync(TestId1, "Original TC-100");
        var from = Path.Combine(_dir, "edit.json");
        File.WriteAllText(from, JsonSerializer.Serialize(new[]
        {
            new { id = TestId1, title = "Edited", priority = "high", steps = new[] { "s" }, expected_result = "r" }
        }));

        var (exit, out_, _) = await RunPerEntryAsync(Suite, TestId1, from);

        Assert.Equal(0, exit);
        var result = JsonSerializer.Deserialize<JsonElement>(out_.Trim());
        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(TestId1, result.GetProperty("id").GetString());
    }

    [Fact]
    public async Task BatchIngest_StagedUpdateForMissingOriginal_IsSkipped()
    {
        // Staging file exists but original test is not on disk
        WriteUpdateStagingFile(TestId1, "Update for nonexistent test");
        // No WriteOriginalTestAsync call

        var (exit, out_, _) = await RunBatchAsync(Suite);

        Assert.Equal(0, exit);
        var result = JsonSerializer.Deserialize<JsonElement>(out_.Trim());
        Assert.Equal(0, result.GetProperty("written").GetInt32());
    }
}
