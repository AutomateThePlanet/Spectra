using System.Text.Json;
using Spectra.CLI.Dashboard;
using Spectra.Core.Models;
using Spectra.Core.Models.Dashboard;

namespace Spectra.CLI.Tests.Dashboard;

/// <summary>
/// Tests for coverage data generation in DataCollector.
/// </summary>
public class CoverageDataTests : IDisposable
{
    private readonly string _testDir;

    public CoverageDataTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-coverage-data-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task CollectAsync_WithNoTests_ReturnsEmptyCoverageData()
    {
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Coverage);
        Assert.Empty(data.Coverage.Nodes);
        Assert.Empty(data.Coverage.Links);
    }

    [Fact]
    public async Task CollectAsync_WithTests_CreatesTestNodes()
    {
        await CreateSuiteIndexAsync("checkout", [
            new TestIndexEntry { Id = "TC-001", Title = "Test 1", File = "TC-001.md", Priority = "high", Tags = [] },
            new TestIndexEntry { Id = "TC-002", Title = "Test 2", File = "TC-002.md", Priority = "high", Tags = [] }
        ]);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Coverage);
        var testNodes = data.Coverage.Nodes.Where(n => n.Type == NodeType.Test).ToList();
        Assert.Equal(2, testNodes.Count);
        Assert.Contains(testNodes, n => n.Id == "test:TC-001");
        Assert.Contains(testNodes, n => n.Id == "test:TC-002");
    }

    [Fact]
    public async Task CollectAsync_WithSourceRefs_CreatesDocumentNodes()
    {
        await CreateSuiteIndexAsync("checkout", [
            new TestIndexEntry
            {
                Id = "TC-001",
                Title = "Test 1",
                File = "TC-001.md",
                Priority = "high",
                Tags = [],
                SourceRefs = ["docs/checkout.md", "docs/cart.md"]
            }
        ]);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Coverage);
        var docNodes = data.Coverage.Nodes.Where(n => n.Type == NodeType.Document).ToList();
        Assert.Equal(2, docNodes.Count);
    }

    [Fact]
    public async Task CollectAsync_WithSourceRefs_CreatesDocumentToTestLinks()
    {
        await CreateSuiteIndexAsync("checkout", [
            new TestIndexEntry
            {
                Id = "TC-001",
                Title = "Test 1",
                File = "TC-001.md",
                Priority = "high",
                Tags = [],
                SourceRefs = ["docs/checkout.md"]
            }
        ]);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Coverage);
        var links = data.Coverage.Links.Where(l => l.Type == "document_to_test").ToList();
        Assert.Single(links);
        Assert.Contains(links, l => l.Target == "test:TC-001");
    }

    [Fact]
    public async Task CollectAsync_DeduplicatesDocumentNodes()
    {
        await CreateSuiteIndexAsync("checkout", [
            new TestIndexEntry
            {
                Id = "TC-001",
                Title = "Test 1",
                File = "TC-001.md",
                Priority = "high",
                Tags = [],
                SourceRefs = ["docs/checkout.md"]
            },
            new TestIndexEntry
            {
                Id = "TC-002",
                Title = "Test 2",
                File = "TC-002.md",
                Priority = "high",
                Tags = [],
                SourceRefs = ["docs/checkout.md"] // Same doc
            }
        ]);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Coverage);
        var docNodes = data.Coverage.Nodes.Where(n => n.Type == NodeType.Document).ToList();
        Assert.Single(docNodes);
        var docLinks = data.Coverage.Links.Where(l => l.Type == "document_to_test").ToList();
        Assert.Equal(2, docLinks.Count); // Both tests link to same doc
    }

    [Fact]
    public async Task CollectAsync_TestWithoutAutomation_HasPartialStatus()
    {
        await CreateSuiteIndexAsync("checkout", [
            new TestIndexEntry
            {
                Id = "TC-001",
                Title = "Test 1",
                File = "TC-001.md",
                Priority = "high",
                Tags = []
                // No automated_by
            }
        ]);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Coverage);
        var testNode = data.Coverage.Nodes.First(n => n.Id == "test:TC-001");
        Assert.Equal(CoverageStatus.Partial, testNode.Status);
    }

    [Fact]
    public async Task CollectAsync_NodesSortedByTypeAndId()
    {
        await CreateSuiteIndexAsync("checkout", [
            new TestIndexEntry
            {
                Id = "TC-002",
                Title = "Test 2",
                File = "TC-002.md",
                Priority = "high",
                Tags = [],
                SourceRefs = ["docs/checkout.md"]
            },
            new TestIndexEntry
            {
                Id = "TC-001",
                Title = "Test 1",
                File = "TC-001.md",
                Priority = "high",
                Tags = [],
                SourceRefs = ["docs/cart.md"]
            }
        ]);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Coverage);
        // Should be sorted: automation, document, test (alphabetically by type)
        var types = data.Coverage.Nodes.Select(n => n.Type).ToList();
        var expectedOrder = types.OrderBy(t => t).ToList();
        Assert.Equal(expectedOrder, types);
    }

    [Fact]
    public async Task CollectAsync_LinksSortedBySourceAndTarget()
    {
        await CreateSuiteIndexAsync("checkout", [
            new TestIndexEntry
            {
                Id = "TC-002",
                Title = "Test 2",
                File = "TC-002.md",
                Priority = "high",
                Tags = [],
                SourceRefs = ["docs/b.md"]
            },
            new TestIndexEntry
            {
                Id = "TC-001",
                Title = "Test 1",
                File = "TC-001.md",
                Priority = "high",
                Tags = [],
                SourceRefs = ["docs/a.md"]
            }
        ]);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Coverage);
        var sources = data.Coverage.Links.Select(l => l.Source).ToList();
        var expectedOrder = sources.OrderBy(s => s).ToList();
        Assert.Equal(expectedOrder, sources);
    }

    [Fact]
    public async Task CollectAsync_DocumentNodeHasCorrectName()
    {
        await CreateSuiteIndexAsync("checkout", [
            new TestIndexEntry
            {
                Id = "TC-001",
                Title = "Test 1",
                File = "TC-001.md",
                Priority = "high",
                Tags = [],
                SourceRefs = ["docs/checkout-flow.md"]
            }
        ]);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Coverage);
        var docNode = data.Coverage.Nodes.First(n => n.Type == NodeType.Document);
        Assert.Equal("checkout-flow.md", docNode.Name);
        Assert.Equal("docs/checkout-flow.md", docNode.Path);
    }

    [Fact]
    public async Task CollectAsync_TestNodeHasCorrectNameAndPath()
    {
        await CreateSuiteIndexAsync("checkout", [
            new TestIndexEntry
            {
                Id = "TC-001",
                Title = "Verify checkout completes",
                File = "TC-001.md",
                Priority = "high",
                Tags = []
            }
        ]);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Coverage);
        var testNode = data.Coverage.Nodes.First(n => n.Type == NodeType.Test);
        Assert.Equal("Verify checkout completes", testNode.Name);
        Assert.Equal("TC-001.md", testNode.Path);
    }

    private async Task CreateSuiteIndexAsync(string suiteName, TestIndexEntry[] tests)
    {
        var index = new MetadataIndex
        {
            Suite = suiteName,
            GeneratedAt = DateTime.UtcNow,
            Tests = tests.ToList()
        };

        var suitePath = Path.Combine(_testDir, "tests", suiteName);
        Directory.CreateDirectory(suitePath);

        var indexPath = Path.Combine(suitePath, "_index.json");
        await File.WriteAllTextAsync(indexPath, JsonSerializer.Serialize(index, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }));
    }
}
