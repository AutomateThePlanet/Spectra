using System.Text.Json;
using Spectra.CLI.Dashboard;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Dashboard;

/// <summary>
/// Unit tests for DataCollector.
/// </summary>
public class DataCollectorTests : IDisposable
{
    private readonly string _testDir;

    public DataCollectorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-datacollector-test-{Guid.NewGuid():N}");
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
    public async Task CollectAsync_EmptyDirectory_ReturnsEmptyData()
    {
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data);
        Assert.Empty(data.Suites);
        Assert.Empty(data.Tests);
        Assert.Empty(data.Runs);
        Assert.Equal("1.0.0", data.Version);
    }

    [Fact]
    public async Task CollectAsync_NoTestsDirectory_ReturnsEmptyData()
    {
        // Create only reports directory
        Directory.CreateDirectory(Path.Combine(_testDir, "reports"));
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.Empty(data.Suites);
        Assert.Empty(data.Tests);
    }

    [Fact]
    public async Task CollectAsync_WithSuiteIndex_ReturnsSuiteStats()
    {
        await CreateSuiteIndexAsync("checkout", ["TC-001", "TC-002", "TC-003"]);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.Single(data.Suites);
        var suite = data.Suites[0];
        Assert.Equal("checkout", suite.Name);
        Assert.Equal(3, suite.TestCount);
    }

    [Fact]
    public async Task CollectAsync_WithMultipleSuites_ReturnsAllSuites()
    {
        await CreateSuiteIndexAsync("checkout", ["TC-001", "TC-002"]);
        await CreateSuiteIndexAsync("payments", ["TC-101"]);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.Equal(2, data.Suites.Count);
        Assert.Contains(data.Suites, s => s.Name == "checkout");
        Assert.Contains(data.Suites, s => s.Name == "payments");
    }

    [Fact]
    public async Task CollectAsync_WithTests_BuildsTestEntries()
    {
        await CreateSuiteIndexAsync("checkout", ["TC-001", "TC-002"]);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.Equal(2, data.Tests.Count);
        Assert.Contains(data.Tests, t => t.Id == "TC-001");
        Assert.Contains(data.Tests, t => t.Id == "TC-002");
        Assert.All(data.Tests, t => Assert.Equal("checkout", t.Suite));
    }

    [Fact]
    public async Task CollectAsync_SetsGeneratedAt()
    {
        var collector = new DataCollector(_testDir);
        var before = DateTime.UtcNow;

        var data = await collector.CollectAsync();

        var after = DateTime.UtcNow;
        Assert.True(data.GeneratedAt >= before);
        Assert.True(data.GeneratedAt <= after);
    }

    [Fact]
    public async Task CollectAsync_SetsRepositoryName()
    {
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.Equal(Path.GetFileName(_testDir), data.Repository);
    }

    [Fact]
    public async Task CollectAsync_WithMalformedIndex_SkipsIt()
    {
        // Create a malformed index file
        var suitePath = Path.Combine(_testDir, "tests", "broken");
        Directory.CreateDirectory(suitePath);
        await File.WriteAllTextAsync(Path.Combine(suitePath, "_index.json"), "{ invalid json }");

        // Create a valid index
        await CreateSuiteIndexAsync("valid", ["TC-001"]);

        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        // Should only have the valid suite
        Assert.Single(data.Suites);
        Assert.Equal("valid", data.Suites[0].Name);
    }

    [Fact]
    public async Task CollectAsync_WithReportFiles_ReturnsRunHistory()
    {
        var reportsPath = Path.Combine(_testDir, "reports");
        Directory.CreateDirectory(reportsPath);

        var report = new
        {
            RunId = "run-001",
            Suite = "checkout",
            Status = "completed",
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow,
            StartedBy = "tester",
            Total = 10,
            Passed = 8,
            Failed = 1,
            Skipped = 1,
            Blocked = 0
        };
        await File.WriteAllTextAsync(
            Path.Combine(reportsPath, "run-001.json"),
            JsonSerializer.Serialize(report));

        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.Single(data.Runs);
        var run = data.Runs[0];
        Assert.Equal("run-001", run.RunId);
        Assert.Equal("checkout", run.Suite);
        Assert.Equal(10, run.Total);
        Assert.Equal(8, run.Passed);
        Assert.Equal(1, run.Failed);
    }

    [Fact]
    public async Task CollectAsync_WithPriorityStats_BuildsByPriority()
    {
        var index = new MetadataIndex
        {
            Suite = "checkout",
            GeneratedAt = DateTime.UtcNow,
            Tests =
            [
                new TestIndexEntry { Id = "TC-001", Title = "Test 1", File = "TC-001.md", Priority = "high", Tags = [] },
                new TestIndexEntry { Id = "TC-002", Title = "Test 2", File = "TC-002.md", Priority = "high", Tags = [] },
                new TestIndexEntry { Id = "TC-003", Title = "Test 3", File = "TC-003.md", Priority = "medium", Tags = [] }
            ]
        };
        await WriteIndexAsync("checkout", index);

        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.Single(data.Suites);
        var suite = data.Suites[0];
        Assert.Equal(2, suite.ByPriority["high"]);
        Assert.Equal(1, suite.ByPriority["medium"]);
    }

    [Fact]
    public async Task CollectAsync_WithComponentStats_BuildsByComponent()
    {
        var index = new MetadataIndex
        {
            Suite = "checkout",
            GeneratedAt = DateTime.UtcNow,
            Tests =
            [
                new TestIndexEntry { Id = "TC-001", Title = "Test 1", File = "TC-001.md", Priority = "high", Tags = [], Component = "cart" },
                new TestIndexEntry { Id = "TC-002", Title = "Test 2", File = "TC-002.md", Priority = "high", Tags = [], Component = "cart" },
                new TestIndexEntry { Id = "TC-003", Title = "Test 3", File = "TC-003.md", Priority = "medium", Tags = [], Component = "payment" }
            ]
        };
        await WriteIndexAsync("checkout", index);

        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.Single(data.Suites);
        var suite = data.Suites[0];
        Assert.Equal(2, suite.ByComponent["cart"]);
        Assert.Equal(1, suite.ByComponent["payment"]);
    }

    [Fact]
    public async Task CollectAsync_CollectsTags()
    {
        var index = new MetadataIndex
        {
            Suite = "checkout",
            GeneratedAt = DateTime.UtcNow,
            Tests =
            [
                new TestIndexEntry { Id = "TC-001", Title = "Test 1", File = "TC-001.md", Priority = "high", Tags = ["smoke", "regression"] },
                new TestIndexEntry { Id = "TC-002", Title = "Test 2", File = "TC-002.md", Priority = "high", Tags = ["regression", "api"] }
            ]
        };
        await WriteIndexAsync("checkout", index);

        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        var suite = data.Suites[0];
        Assert.Contains("smoke", suite.Tags);
        Assert.Contains("regression", suite.Tags);
        Assert.Contains("api", suite.Tags);
    }

    private async Task CreateSuiteIndexAsync(string suiteName, string[] testIds)
    {
        var index = new MetadataIndex
        {
            Suite = suiteName,
            GeneratedAt = DateTime.UtcNow,
            Tests = testIds.Select(id => new TestIndexEntry
            {
                Id = id,
                Title = $"Test {id}",
                File = $"{id}.md",
                Priority = "high",
                Tags = []
            }).ToList()
        };

        await WriteIndexAsync(suiteName, index);
    }

    private async Task WriteIndexAsync(string suiteName, MetadataIndex index)
    {
        var suitePath = Path.Combine(_testDir, "tests", suiteName);
        Directory.CreateDirectory(suitePath);

        var indexPath = Path.Combine(suitePath, "_index.json");
        await File.WriteAllTextAsync(indexPath, JsonSerializer.Serialize(index, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }));
    }
}
