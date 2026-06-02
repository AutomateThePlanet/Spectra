using System.Text;
using System.Text.Json;
using Spectra.CLI.Commands.Index;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.Core.Index;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Commands.Index;

/// <summary>
/// Spec 049 — User Story 2 backfill. Asserts that `spectra index --rebuild`
/// regenerates each suite's _index.json from the .md files of record,
/// recovering any tests that were previously unindexed (e.g., pre-fix
/// from-description tests) and continuing past malformed files.
/// </summary>
public sealed class IndexHandlerRebuildTests : IDisposable
{
    private readonly string _root;
    private readonly string _testsPath;
    private readonly string _originalCwd;

    public IndexHandlerRebuildTests()
    {
        _root = Directory.CreateTempSubdirectory("spectra-rebuild-").FullName;
        _testsPath = Path.Combine(_root, "test-cases");
        Directory.CreateDirectory(_testsPath);

        // No spectra.config.json: IndexHandler defaults testsDir to "test-cases"
        // when the config file is absent. Providing a partial config triggers
        // System.Text.Json `required`-member validation and an Error return.

        _originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_root);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalCwd);
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    [Fact]
    public async Task IndexRebuild_RecoversUnindexedFromDescriptionTest()
    {
        const string suite = "checkout";
        var suiteDir = Path.Combine(_testsPath, suite);
        Directory.CreateDirectory(suiteDir);

        // Disk state: one valid .md file, but _index.json does NOT mention it.
        // Simulates the pre-Spec-049 from-description bug state.
        var writer = new TestFileWriter();
        await writer.WriteAsync(Path.Combine(suiteDir, "TC-049.md"), new TestCase
        {
            Id = "TC-049",
            Title = "Guest checkout shipping estimate",
            Priority = Priority.High,
            Tags = ["smoke"],
            Component = "checkout",
            Steps = ["Open cart", "Proceed to checkout"],
            ExpectedResult = "Shipping estimate is shown",
            FilePath = "TC-049.md",
        });

        // Write an out-of-date index that has no entry for TC-049.
        var emptyIndex = new MetadataIndex
        {
            Suite = suite,
            GeneratedAt = DateTime.UtcNow.AddDays(-1),
            Tests = [],
        };
        await new IndexWriter().WriteAsync(Path.Combine(suiteDir, "_index.json"), emptyIndex);

        var handler = new IndexHandler(VerbosityLevel.Quiet, dryRun: false);
        var exitCode = await handler.ExecuteAsync(suite: null, rebuild: true);

        Assert.Equal(ExitCodes.Success, exitCode);

        var rebuilt = await ReadIndexAsync(suite);
        Assert.NotNull(rebuilt);
        Assert.Single(rebuilt!.Tests);
        var entry = rebuilt.Tests[0];
        Assert.Equal("TC-049", entry.Id);
        Assert.Equal("Guest checkout shipping estimate", entry.Title);
        Assert.Equal("high", entry.Priority);
    }

    [Fact]
    public async Task IndexRebuild_ContinuesPastMalformedFiles()
    {
        const string suite = "regression";
        var suiteDir = Path.Combine(_testsPath, suite);
        Directory.CreateDirectory(suiteDir);

        // One valid file.
        var writer = new TestFileWriter();
        await writer.WriteAsync(Path.Combine(suiteDir, "TC-001.md"), new TestCase
        {
            Id = "TC-001",
            Title = "Valid test",
            Priority = Priority.High,
            Steps = ["Step 1"],
            ExpectedResult = "OK",
            FilePath = "TC-001.md",
        });

        // One deliberately broken file (no frontmatter at all).
        await File.WriteAllTextAsync(
            Path.Combine(suiteDir, "TC-002.md"),
            "this is not a valid spectra test file\nno frontmatter here\n",
            Encoding.UTF8);

        var handler = new IndexHandler(VerbosityLevel.Quiet, dryRun: false);
        var exitCode = await handler.ExecuteAsync(suite: null, rebuild: true);

        // The handler reports errors via non-zero exit code, but the valid file is still indexed.
        Assert.Equal(ExitCodes.Error, exitCode);

        var rebuilt = await ReadIndexAsync(suite);
        Assert.NotNull(rebuilt);
        Assert.Single(rebuilt!.Tests);
        Assert.Equal("TC-001", rebuilt.Tests[0].Id);
    }

    [Fact]
    public async Task IndexRebuild_PreservesExistingIndexedTests()
    {
        const string suite = "stable";
        var suiteDir = Path.Combine(_testsPath, suite);
        Directory.CreateDirectory(suiteDir);

        // Three fully indexed tests.
        var writer = new TestFileWriter();
        var tests = new[]
        {
            MakeTest("TC-001", "First", Priority.High, ["smoke"]),
            MakeTest("TC-002", "Second", Priority.Medium, ["regression"]),
            MakeTest("TC-003", "Third", Priority.Low, []),
        };
        foreach (var t in tests)
        {
            await writer.WriteAsync(Path.Combine(suiteDir, $"{t.Id}.md"), t);
        }
        var pristine = new IndexGenerator().Generate(suite, tests);
        await new IndexWriter().WriteAsync(Path.Combine(suiteDir, "_index.json"), pristine);

        var handler = new IndexHandler(VerbosityLevel.Quiet, dryRun: false);
        var exitCode = await handler.ExecuteAsync(suite: null, rebuild: true);

        Assert.Equal(ExitCodes.Success, exitCode);

        var rebuilt = await ReadIndexAsync(suite);
        Assert.NotNull(rebuilt);
        Assert.Equal(tests.Length, rebuilt!.Tests.Count);
        var rebuiltIds = rebuilt.Tests.Select(e => e.Id).ToHashSet();
        Assert.Equal(tests.Select(t => t.Id).ToHashSet(), rebuiltIds);

        // Field-level equivalence (modulo GeneratedAt).
        foreach (var t in tests)
        {
            var entry = rebuilt.Tests.Single(e => e.Id == t.Id);
            Assert.Equal(t.Title, entry.Title);
            Assert.Equal(t.Priority.ToString().ToLowerInvariant(), entry.Priority);
            Assert.Equal(t.Tags.ToList(), entry.Tags.ToList());
        }
    }

    private static TestCase MakeTest(string id, string title, Priority priority, IReadOnlyList<string> tags) => new()
    {
        Id = id,
        Title = title,
        Priority = priority,
        Tags = tags,
        Steps = ["Step"],
        ExpectedResult = "OK",
        FilePath = $"{id}.md",
    };

    private async Task<MetadataIndex?> ReadIndexAsync(string suite)
    {
        var path = Path.Combine(_testsPath, suite, "_index.json");
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<MetadataIndex>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
