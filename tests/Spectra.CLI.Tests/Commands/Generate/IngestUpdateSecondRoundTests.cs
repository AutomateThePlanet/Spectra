using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.IO;
using Spectra.CLI.Options;
using Spectra.Core.Index;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 075 — regression guard for FR2 (update path): ingest-update must store bare filenames in
/// _index.json. Before the fix, LoadExistingTestsAsync in IngestUpdateCommand used
/// Path.GetRelativePath(testsPath, file), yielding suite-prefixed paths. After the fix it uses
/// Path.GetRelativePath(suitePath, file), yielding bare filenames.
/// </summary>
[Collection("WorkingDirectory")]
public sealed class IngestUpdateSecondRoundTests : IDisposable
{
    private readonly string _dir;
    private readonly string _testsPath;
    private const string Suite = "checkout";
    private const string TestId = "TC-100";

    public IngestUpdateSecondRoundTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-upd-2nd-{Guid.NewGuid():N}");
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

    private async Task WriteOriginalAsync()
    {
        var original = new TestCase
        {
            Id = TestId,
            FilePath = $"{TestId}.md",
            Title = "Original title",
            Priority = Priority.High,
            Steps = ["original step"],
            ExpectedResult = "original result"
        };
        var path = TestFileWriter.GetFilePath(_testsPath, Suite, TestId);
        await new TestFileWriter().WriteAsync(path, original, default);

        // Also write a second test so LoadExistingTestsAsync finds multiple files
        var second = new TestCase
        {
            Id = "TC-101",
            FilePath = "TC-101.md",
            Title = "Second test",
            Priority = Priority.Medium,
            Steps = ["step 1"],
            ExpectedResult = "result 1"
        };
        await new TestFileWriter().WriteAsync(TestFileWriter.GetFilePath(_testsPath, Suite, "TC-101"), second, default);

        // Write a bare-path index so the command can find the original
        var suitePath = Path.Combine(_testsPath, Suite);
        var indexPath = IndexWriter.GetIndexPath(suitePath);
        var index = new Spectra.Core.Models.MetadataIndex
        {
            Suite = Suite,
            GeneratedAt = DateTime.UtcNow,
            Tests = new List<Spectra.Core.Models.TestIndexEntry>
            {
                new() { Id = TestId, File = $"{TestId}.md", Title = "Original title", Priority = "high" },
                new() { Id = "TC-101", File = "TC-101.md", Title = "Second test", Priority = "medium" }
            }
        };
        await new IndexWriter().WriteAsync(indexPath, index, default);
    }

    private static string EditJson(string id = TestId, string title = "Updated title") => $$"""
        [
          {
            "id": "{{id}}",
            "title": "{{title}}",
            "priority": "high",
            "steps": ["updated step"],
            "expected_result": "updated result"
          }
        ]
        """;

    private async Task<(int Exit, string Out, string Err)> RunUpdateAsync(string editJson, string testId = TestId)
    {
        var from = Path.Combine(_dir, $"edit-{Guid.NewGuid():N}.json");
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
    public async Task IngestUpdate_AfterUpdate_IndexFileFieldsRemainBare()
    {
        await WriteOriginalAsync();

        // Run ingest-update on TC-100 — LoadExistingTestsAsync picks up both TC-100.md and TC-101.md
        // Before fix: both get suite-prefixed paths "checkout\TC-100.md", "checkout\TC-101.md"
        // After fix: both keep bare paths "TC-100.md", "TC-101.md"
        var (exit, _, _) = await RunUpdateAsync(EditJson(title: "Updated title"));
        Assert.Equal(0, exit);

        var suitePath = Path.Combine(_testsPath, Suite);
        var indexPath = IndexWriter.GetIndexPath(suitePath);
        var index = await new IndexWriter().ReadAsync(indexPath, default);
        Assert.NotNull(index);

        // All file fields in the index must be bare — no suite-directory prefix
        foreach (var entry in index.Tests)
        {
            Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), entry.File);
            Assert.DoesNotContain("/", entry.File);
            Assert.DoesNotContain(Suite, entry.File);
            Assert.EndsWith(".md", entry.File);
        }
    }

    [Fact]
    public async Task IngestUpdate_UpdatedTest_FileFieldIsOriginalBarePath()
    {
        await WriteOriginalAsync();

        var (exit, _, _) = await RunUpdateAsync(EditJson(title: "Updated title"));
        Assert.Equal(0, exit);

        var suitePath = Path.Combine(_testsPath, Suite);
        var index = await new IndexWriter().ReadAsync(IndexWriter.GetIndexPath(suitePath), default);
        Assert.NotNull(index);

        var tc100 = index.Tests.FirstOrDefault(t => t.Id == TestId);
        Assert.NotNull(tc100);
        Assert.Equal($"{TestId}.md", tc100.File);
    }
}
