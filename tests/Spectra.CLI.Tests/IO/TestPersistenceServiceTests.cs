using System.Text.Json;
using Spectra.CLI.IO;
using Spectra.Core.Index;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.IO;

public sealed class TestPersistenceServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _testsPath;
    private readonly TestPersistenceService _service;

    public TestPersistenceServiceTests()
    {
        _root = Directory.CreateTempSubdirectory("spectra-persist-").FullName;
        _testsPath = Path.Combine(_root, "test-cases");
        Directory.CreateDirectory(_testsPath);
        _service = new TestPersistenceService(new TestFileWriter(), new IndexGenerator(), new IndexWriter());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; never let cleanup throw out of a test.
        }
    }

    [Fact]
    public async Task PersistAsync_WritesAllTestsToWrite_AsMdFiles()
    {
        const string suite = "checkout";
        var tests = new[]
        {
            CreateTest("TC-001", "First test", Priority.High),
            CreateTest("TC-002", "Second test", Priority.Medium),
            CreateTest("TC-003", "Third test", Priority.Low),
        };

        await _service.PersistAsync(_testsPath, suite, tests, tests, CancellationToken.None);

        foreach (var t in tests)
        {
            var path = Path.Combine(_testsPath, suite, $"{t.Id}.md");
            Assert.True(File.Exists(path), $"Expected file {path} to exist");
            var body = await File.ReadAllTextAsync(path);
            Assert.Contains($"id: {t.Id}", body);
            Assert.Contains($"# {t.Title}", body);
        }
    }

    [Fact]
    public async Task PersistAsync_WritesIndexJson_WithFullSet()
    {
        const string suite = "auth";
        var tests = new[]
        {
            CreateTest("TC-010", "Login happy path", Priority.High),
            CreateTest("TC-005", "Logout", Priority.Medium),
            CreateTest("TC-020", "Password reset", Priority.Low),
        };

        await _service.PersistAsync(_testsPath, suite, tests, tests, CancellationToken.None);

        var index = await ReadIndexAsync(suite);
        Assert.NotNull(index);
        Assert.Equal(suite, index!.Suite);
        Assert.Equal(3, index.Tests.Count);
        // IndexGenerator.Generate orders entries by id ascending.
        Assert.Equal(new[] { "TC-005", "TC-010", "TC-020" }, index.Tests.Select(e => e.Id).ToArray());
    }

    [Fact]
    public async Task PersistAsync_LowercasesPriorityInIndex()
    {
        const string suite = "smoke";
        var tests = new[]
        {
            CreateTest("TC-100", "High prio", Priority.High),
            CreateTest("TC-101", "Medium prio", Priority.Medium),
            CreateTest("TC-102", "Low prio", Priority.Low),
        };

        await _service.PersistAsync(_testsPath, suite, tests, tests, CancellationToken.None);

        var index = await ReadIndexAsync(suite);
        Assert.NotNull(index);
        var byId = index!.Tests.ToDictionary(e => e.Id, e => e.Priority);
        Assert.Equal("high", byId["TC-100"]);
        Assert.Equal("medium", byId["TC-101"]);
        Assert.Equal("low", byId["TC-102"]);
    }

    [Fact]
    public async Task PersistAsync_OverwritesPreExistingIndex()
    {
        const string suite = "regression";
        var firstBatch = new[] { CreateTest("TC-001", "First", Priority.High) };
        await _service.PersistAsync(_testsPath, suite, firstBatch, firstBatch, CancellationToken.None);

        var secondNew = CreateTest("TC-002", "Second", Priority.Medium);
        var allAfter = new[] { firstBatch[0], secondNew };
        await _service.PersistAsync(_testsPath, suite, new[] { secondNew }, allAfter, CancellationToken.None);

        var index = await ReadIndexAsync(suite);
        Assert.NotNull(index);
        Assert.Equal(2, index!.Tests.Count);
        Assert.Contains(index.Tests, e => e.Id == "TC-001");
        Assert.Contains(index.Tests, e => e.Id == "TC-002");
    }

    [Fact]
    public async Task PersistAsync_EmptyTestsToWrite_StillRegeneratesIndex()
    {
        const string suite = "rebuild";
        var existing = new[]
        {
            CreateTest("TC-001", "Existing one", Priority.High),
            CreateTest("TC-002", "Existing two", Priority.Medium),
        };

        await _service.PersistAsync(_testsPath, suite, Array.Empty<TestCase>(), existing, CancellationToken.None);

        var index = await ReadIndexAsync(suite);
        Assert.NotNull(index);
        Assert.Equal(2, index!.Tests.Count);
        // No files should have been written since testsToWrite was empty.
        var suiteDir = Path.Combine(_testsPath, suite);
        var mdFiles = Directory.GetFiles(suiteDir, "*.md");
        Assert.Empty(mdFiles);
    }

    [Fact]
    public async Task PersistAsync_CreatesSuiteDirectoryIfMissing()
    {
        const string suite = "fresh-suite";
        var t = CreateTest("TC-001", "First ever test", Priority.High);

        await _service.PersistAsync(_testsPath, suite, new[] { t }, new[] { t }, CancellationToken.None);

        Assert.True(Directory.Exists(Path.Combine(_testsPath, suite)));
        Assert.True(File.Exists(Path.Combine(_testsPath, suite, "TC-001.md")));
        Assert.True(File.Exists(Path.Combine(_testsPath, suite, "_index.json")));
    }

    [Fact]
    public async Task PersistAsync_WhenWriterThrows_PropagatesException()
    {
        // Force TestFileWriter to fail: pre-create a *file* where the suite directory
        // would be. Directory.CreateDirectory(...) will then throw IOException
        // because a file with the same name already exists.
        const string suite = "fail-suite";
        var blocking = Path.Combine(_testsPath, suite);
        await File.WriteAllTextAsync(blocking, "blocker");

        var tests = new[] { CreateTest("TC-001", "T", Priority.High) };

        await Assert.ThrowsAnyAsync<IOException>(() =>
            _service.PersistAsync(_testsPath, suite, tests, tests, CancellationToken.None));
    }

    [Fact]
    public async Task PersistAsync_WhenIndexWriterThrows_PropagatesException()
    {
        // Force IndexWriter to fail: pre-create a *directory* at the _index.json
        // path. The .md write succeeds, then File.WriteAllTextAsync on _index.json
        // fails because the path points at a directory, not a file.
        const string suite = "fail-index";
        var suiteDir = Path.Combine(_testsPath, suite);
        Directory.CreateDirectory(suiteDir);
        Directory.CreateDirectory(Path.Combine(suiteDir, "_index.json"));

        var tests = new[] { CreateTest("TC-001", "T", Priority.High) };

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _service.PersistAsync(_testsPath, suite, tests, tests, CancellationToken.None));

        // The test file must still have been written before the index write failed.
        Assert.True(File.Exists(Path.Combine(suiteDir, "TC-001.md")));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 1)]
    [InlineData(0, 5)]
    public async Task PersistenceService_NeverWritesFileWithoutIndex(int existingCount, int newCount)
    {
        var suite = $"invariant-{existingCount}-{newCount}";
        var existing = Enumerable.Range(1, existingCount)
            .Select(i => CreateTest($"TC-{i:D3}", $"Existing {i}", Priority.Medium))
            .ToList();
        var newTests = Enumerable.Range(100, newCount)
            .Select(i => CreateTest($"TC-{i:D3}", $"New {i}", Priority.High))
            .ToList();
        var allForIndex = existing.Concat(newTests).ToList();

        await _service.PersistAsync(_testsPath, suite, newTests, allForIndex, CancellationToken.None);

        var index = await ReadIndexAsync(suite);
        Assert.NotNull(index);
        var indexedIds = index!.Tests.Select(e => e.Id).ToHashSet();
        Assert.Equal(allForIndex.Select(t => t.Id).ToHashSet(), indexedIds);

        foreach (var t in newTests)
        {
            Assert.True(File.Exists(Path.Combine(_testsPath, suite, $"{t.Id}.md")),
                $"INV-2 violation: file for {t.Id} not written");
            Assert.Contains(t.Id, indexedIds);
        }
    }

    private static TestCase CreateTest(string id, string title, Priority priority) => new()
    {
        Id = id,
        Title = title,
        Priority = priority,
        Steps = ["Step 1"],
        ExpectedResult = "Expected",
        FilePath = $"suite/{id}.md",
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
