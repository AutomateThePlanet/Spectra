using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.IO;
using Spectra.CLI.Options;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 063 — command-level contract for the inverted update seam. A valid edit persists with the
/// original id (exit 0); an out-of-scope protected-field change fails loud DRIFT_DETECTED (exit 5);
/// malformed/schema-invalid edits fail loud (exit 5/6) and persist nothing; a missing original is
/// rejected (exit 1). No model, no network.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class IngestUpdateBoundaryTests : IDisposable
{
    private readonly string _dir;
    private readonly string _testsPath;
    private const string Suite = "checkout";
    private const string TestId = "TC-100";

    public IngestUpdateBoundaryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-ingest-upd-{Guid.NewGuid():N}");
        _testsPath = Path.Combine(_dir, "test-cases");
        Directory.CreateDirectory(_testsPath);

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

    private async Task WriteOriginalAsync(Priority priority = Priority.High, string? component = null)
    {
        var original = new TestCase
        {
            Id = TestId,
            FilePath = $"{TestId}.md",
            Title = "Original title",
            Priority = priority,
            Component = component,
            Steps = ["original step"],
            ExpectedResult = "original result"
        };
        var path = TestFileWriter.GetFilePath(_testsPath, Suite, TestId);
        await new TestFileWriter().WriteAsync(path, original, default);
    }

    private static string EditJson(string id = TestId, string title = "Edited title", string priority = "high") => $$"""
        [
          {
            "id": "{{id}}",
            "title": "{{title}}",
            "priority": "{{priority}}",
            "steps": ["edited step"],
            "expected_result": "edited result"
          }
        ]
        """;

    private async Task<(int Exit, string Out, string Err)> RunAsync(string editJson, string testId = TestId)
    {
        var from = Path.Combine(_dir, "edit.json");
        File.WriteAllText(from, editJson);

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
                new[] { "ai", "ingest-update", Suite, "--test-id", testId, "--from", from, "--output-format", "json" });
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
    public async Task ValidEdit_Exit0_PersistsWithOriginalId()
    {
        await WriteOriginalAsync();
        var (exit, stdout, _) = await RunAsync(EditJson(title: "Edited title"));

        Assert.Equal(0, exit);
        Assert.Contains("TC-100", stdout);
        Assert.Contains("\"success\":true", stdout.Replace(" ", ""));

        var file = Path.Combine(_testsPath, Suite, "TC-100.md");
        Assert.True(File.Exists(file));
        Assert.Contains("Edited title", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task DriftOnProtectedField_Exit5_DriftDetected_NothingChanged()
    {
        await WriteOriginalAsync(priority: Priority.High);
        var before = await File.ReadAllTextAsync(Path.Combine(_testsPath, Suite, "TC-100.md"));

        var (exit, _, stderr) = await RunAsync(EditJson(priority: "low")); // out-of-scope change

        Assert.Equal(5, exit);
        Assert.Contains("DRIFT_DETECTED", stderr);

        var after = await File.ReadAllTextAsync(Path.Combine(_testsPath, Suite, "TC-100.md"));
        Assert.Equal(before, after); // byte-for-byte unchanged
    }

    [Fact]
    public async Task MalformedEdit_Exit5_NothingChanged()
    {
        await WriteOriginalAsync();
        var before = await File.ReadAllTextAsync(Path.Combine(_testsPath, Suite, "TC-100.md"));

        var (exit, _, stderr) = await RunAsync("not a json array at all");

        Assert.Equal(5, exit);
        Assert.Contains("EMPTY_CONTENT", stderr);
        Assert.Equal(before, await File.ReadAllTextAsync(Path.Combine(_testsPath, Suite, "TC-100.md")));
    }

    [Fact]
    public async Task SchemaInvalidEdit_Exit6_NothingChanged()
    {
        await WriteOriginalAsync();
        var (exit, _, stderr) = await RunAsync(EditJson(id: "BADID"));

        Assert.Equal(6, exit);
        Assert.Contains("SCHEMA_INVALID", stderr);
    }

    [Fact]
    public async Task MissingOriginal_Exit1()
    {
        // No original written to disk.
        var (exit, _, stderr) = await RunAsync(EditJson(), testId: "TC-404");
        Assert.Equal(1, exit);
        Assert.Contains("TC-404", stderr);
    }
}
