using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.Options;
using Spectra.Core.Index;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 075 — regression guard for FR2: the index writer must store bare filenames in _index.json
/// on second and subsequent generation rounds. Before the fix, LoadExistingTestsAsync used
/// Path.GetRelativePath(testsPath, file) which yielded "suite\TC-100.md" (suite-prefixed). After
/// the fix it uses Path.GetRelativePath(suitePath, file) which yields bare "TC-100.md".
/// </summary>
[Collection("WorkingDirectory")]
public sealed class IngestTestsSecondRoundTests : IDisposable
{
    private readonly string _dir;
    private readonly string _testsPath;
    private const string Suite = "unit-converter";

    public IngestTestsSecondRoundTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-ingest-2nd-{Guid.NewGuid():N}");
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

    private static string TestJson(string id, string title = "Test title") => $$"""
        [
          {
            "id": "{{id}}",
            "title": "{{title}}",
            "priority": "high",
            "steps": ["step 1"],
            "expected_result": "result"
          }
        ]
        """;

    private async Task<(int Exit, string Out, string Err)> RunIngestAsync(string jsonContent)
    {
        var from = Path.Combine(_dir, $"gen-{Guid.NewGuid():N}.json");
        File.WriteAllText(from, jsonContent);

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
                new[] { "ai", "ingest-tests", Suite, "--from", from, "--output-format", "json" });
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
    public async Task SecondRoundIngest_IndexFileField_RemainsBare()
    {
        // First round: ingest TC-100 — creates _index.json with bare "TC-100.md"
        var (exit1, _, _) = await RunIngestAsync(TestJson("TC-100", "First test"));
        Assert.Equal(0, exit1);

        // Second round: ingest TC-101 — LoadExistingTestsAsync picks up TC-100.md from disk
        // Before the fix: TC-100's file field is re-written as "unit-converter\TC-100.md" (suite-prefixed)
        // After the fix: TC-100's file field stays bare "TC-100.md"
        var (exit2, _, _) = await RunIngestAsync(TestJson("TC-101", "Second test"));
        Assert.Equal(0, exit2);

        var suitePath = Path.Combine(_testsPath, Suite);
        var indexPath = IndexWriter.GetIndexPath(suitePath);
        Assert.True(File.Exists(indexPath), "_index.json must exist after second ingest");

        var index = await new IndexWriter().ReadAsync(indexPath, default);
        Assert.NotNull(index);

        var tc100 = index.Tests.FirstOrDefault(t => t.Id == "TC-100");
        Assert.NotNull(tc100);

        // The file field must be bare — no suite-directory prefix
        Assert.Equal("TC-100.md", tc100.File);
        Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), tc100.File);
        Assert.DoesNotContain("/", tc100.File);
        Assert.DoesNotContain(Suite, tc100.File);
    }

    [Fact]
    public async Task SecondRoundIngest_AllExistingEntries_HaveBarePaths()
    {
        // First round: ingest three tests
        var (exit1, _, _) = await RunIngestAsync($$"""
            [
              {"id":"TC-200","title":"T200","priority":"high","steps":["s"],"expected_result":"r"},
              {"id":"TC-201","title":"T201","priority":"medium","steps":["s"],"expected_result":"r"},
              {"id":"TC-202","title":"T202","priority":"low","steps":["s"],"expected_result":"r"}
            ]
            """);
        Assert.Equal(0, exit1);

        // Second round: add one more — LoadExistingTestsAsync picks up all three from disk
        var (exit2, _, _) = await RunIngestAsync(TestJson("TC-203", "Fourth test"));
        Assert.Equal(0, exit2);

        var suitePath = Path.Combine(_testsPath, Suite);
        var index = await new IndexWriter().ReadAsync(IndexWriter.GetIndexPath(suitePath), default);
        Assert.NotNull(index);

        // Every entry — both the pre-existing and the new one — must have a bare file path
        foreach (var entry in index.Tests)
        {
            Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), entry.File);
            Assert.DoesNotContain("/", entry.File);
            Assert.DoesNotContain(Suite, entry.File);
            Assert.EndsWith(".md", entry.File);
        }
    }
}
