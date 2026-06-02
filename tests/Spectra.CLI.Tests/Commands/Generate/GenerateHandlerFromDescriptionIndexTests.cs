using System.Text.Json;
using Spectra.CLI.IO;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 049 — From-Description Write & Index Parity.
///
/// These tests drive the exact sequence that
/// <c>GenerateHandler.ExecuteFromDescriptionAsync</c> now performs after the
/// rewire: (1) load existing suite tests from the test files of record,
/// (2) dedup the newly generated test by id, (3) call
/// <c>TestPersistenceService.PersistAsync</c> with the full set. They simulate
/// the generator's output with a deterministic <see cref="TestCase"/> rather
/// than invoking the AI, but exercise the same persist+index integration the
/// handler now uses.
/// </summary>
public sealed class GenerateHandlerFromDescriptionIndexTests : IDisposable
{
    private readonly string _root;
    private readonly string _testsPath;
    private readonly TestPersistenceService _persistence = new(
        new TestFileWriter(), new IndexGenerator(), new IndexWriter());

    public GenerateHandlerFromDescriptionIndexTests()
    {
        _root = Directory.CreateTempSubdirectory("spectra-fromdesc-").FullName;
        _testsPath = Path.Combine(_root, "test-cases");
        Directory.CreateDirectory(_testsPath);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    [Fact]
    public async Task FromDescription_WritesTest_AndRegistersInIndex()
    {
        const string suite = "checkout";
        var generated = MakeFromDescriptionTest("TC-001", "guest checkout shows shipping estimate", Priority.High,
            tags: ["smoke", "checkout"], component: "checkout");

        await SimulateFromDescriptionAsync(suite, generated);

        var index = await ReadIndexAsync(suite);
        Assert.NotNull(index);
        Assert.Single(index!.Tests);
        var entry = index.Tests[0];
        Assert.Equal("TC-001", entry.Id);
        Assert.Equal("guest checkout shows shipping estimate", entry.Title);
        Assert.Equal("high", entry.Priority);
    }

    [Fact]
    public async Task FromDescription_IndexEntry_MatchesFileFrontmatter()
    {
        const string suite = "auth";
        var generated = MakeFromDescriptionTest("TC-042", "session expiry redirects to login", Priority.Medium,
            tags: ["regression", "auth", "session"], component: "session",
            dependsOn: "TC-040",
            sourceRefs: ["docs/auth.md", "docs/session.md"]);

        await SimulateFromDescriptionAsync(suite, generated);

        // Parse the on-disk frontmatter and the index entry and assert they agree.
        var parser = new TestCaseParser();
        var mdPath = Path.Combine(_testsPath, suite, "TC-042.md");
        var parseResult = parser.Parse(await File.ReadAllTextAsync(mdPath), "TC-042.md");
        Assert.True(parseResult.IsSuccess);
        var fromFile = parseResult.Value!;

        var index = await ReadIndexAsync(suite);
        var entry = index!.Tests.Single(e => e.Id == "TC-042");

        Assert.Equal(fromFile.Priority.ToString().ToLowerInvariant(), entry.Priority);
        Assert.Equal(fromFile.Tags.ToList(), entry.Tags.ToList());
        Assert.Equal(fromFile.Component, entry.Component);
        Assert.Equal(fromFile.DependsOn, entry.DependsOn);
        Assert.Equal(fromFile.SourceRefs.ToList(), entry.SourceRefs.ToList());
    }

    [Fact]
    public async Task FromDescription_PreservesExistingSuiteEntries()
    {
        const string suite = "regression";
        var pre = new[]
        {
            MakeFromDescriptionTest("TC-001", "First", Priority.High),
            MakeFromDescriptionTest("TC-002", "Second", Priority.Medium),
            MakeFromDescriptionTest("TC-003", "Third", Priority.Low),
        };
        await _persistence.PersistAsync(_testsPath, suite, pre, pre, CancellationToken.None);

        var newTest = MakeFromDescriptionTest("TC-099", "From-description add", Priority.High);

        await SimulateFromDescriptionAsync(suite, newTest);

        var index = await ReadIndexAsync(suite);
        Assert.NotNull(index);
        Assert.Equal(4, index!.Tests.Count);
        var ids = index.Tests.Select(e => e.Id).ToHashSet();
        Assert.Contains("TC-001", ids);
        Assert.Contains("TC-002", ids);
        Assert.Contains("TC-003", ids);
        Assert.Contains("TC-099", ids);
    }

    [Fact]
    public async Task FromDescription_ReRun_DoesNotDuplicateIndexEntry()
    {
        const string suite = "idempotency";
        var generated = MakeFromDescriptionTest("TC-007", "Re-run scenario", Priority.High);

        await SimulateFromDescriptionAsync(suite, generated);
        await SimulateFromDescriptionAsync(suite, generated);

        var index = await ReadIndexAsync(suite);
        Assert.NotNull(index);
        Assert.Single(index!.Tests);
        Assert.Equal("TC-007", index.Tests[0].Id);
    }

    /// <summary>
    /// Mirrors the sequence in <c>GenerateHandler.ExecuteFromDescriptionAsync</c>
    /// after Spec 049: load existing suite tests, dedup the new one by id,
    /// call PersistAsync with the union as the index set.
    /// </summary>
    private async Task SimulateFromDescriptionAsync(string suite, TestCase generated)
    {
        var suitePath = Path.Combine(_testsPath, suite);
        if (!Directory.Exists(suitePath)) Directory.CreateDirectory(suitePath);

        var existing = await LoadExistingAsync(suitePath);
        var allForIndex = existing
            .Where(t => t.Id != generated.Id)
            .Append(generated)
            .ToList();

        await _persistence.PersistAsync(_testsPath, suite, [generated], allForIndex, CancellationToken.None);
    }

    private async Task<List<TestCase>> LoadExistingAsync(string suitePath)
    {
        var tests = new List<TestCase>();
        if (!Directory.Exists(suitePath)) return tests;

        var parser = new TestCaseParser();
        var files = Directory.GetFiles(suitePath, "*.md")
            .Where(f => !Path.GetFileName(f).StartsWith("_"));

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file);
            var relPath = Path.GetRelativePath(_testsPath, file);
            var result = parser.Parse(content, relPath);
            if (result.IsSuccess) tests.Add(result.Value!);
        }
        return tests;
    }

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

    private static TestCase MakeFromDescriptionTest(
        string id,
        string title,
        Priority priority,
        IReadOnlyList<string>? tags = null,
        string? component = null,
        string? dependsOn = null,
        IReadOnlyList<string>? sourceRefs = null)
        => new()
        {
            Id = id,
            Title = title,
            Priority = priority,
            Tags = tags ?? [],
            Component = component,
            DependsOn = dependsOn,
            SourceRefs = sourceRefs ?? [],
            Steps = ["Open the app", "Perform the described action"],
            ExpectedResult = "The expected outcome occurs",
            FilePath = $"{id}.md",
        };
}
