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
        Directory.CreateDirectory(Path.Combine(_testDir, ".execution", "reports"));
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
        var reportsPath = Path.Combine(_testDir, ".execution", "reports");
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

    #region Trend Calculation Tests

    [Fact]
    public async Task CollectAsync_WithNoRuns_ReturnsEmptyTrends()
    {
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Trends);
        Assert.Empty(data.Trends.Points);
        Assert.Equal(0m, data.Trends.OverallPassRate);
        Assert.Equal("stable", data.Trends.Direction);
    }

    [Fact]
    public async Task CollectAsync_WithSingleRun_CalculatesPassRate()
    {
        await CreateRunReportAsync("run-001", "checkout", DateTime.UtcNow, 10, 8, 1, 1, 0);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Trends);
        Assert.Equal(80m, data.Trends.OverallPassRate);
        Assert.Equal("stable", data.Trends.Direction);
    }

    [Fact]
    public async Task CollectAsync_WithMultipleRuns_AggregatesByDate()
    {
        var baseDate = DateTime.UtcNow.Date;
        await CreateRunReportAsync("run-001", "checkout", baseDate.AddHours(9), 10, 8, 2, 0, 0);
        await CreateRunReportAsync("run-002", "checkout", baseDate.AddHours(14), 10, 9, 1, 0, 0);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Trends);
        Assert.Single(data.Trends.Points); // Same day, should aggregate
        var point = data.Trends.Points[0];
        Assert.Equal(baseDate, point.Date);
        Assert.Equal(20, point.Total);
        Assert.Equal(17, point.Passed);
        Assert.Equal(3, point.Failed);
        Assert.Equal(85m, point.PassRate);
    }

    [Fact]
    public async Task CollectAsync_WithMultipleDays_CreatesTrendPoints()
    {
        var baseDate = DateTime.UtcNow.Date;
        await CreateRunReportAsync("run-001", "checkout", baseDate.AddDays(-2), 10, 6, 4, 0, 0);
        await CreateRunReportAsync("run-002", "checkout", baseDate.AddDays(-1), 10, 8, 2, 0, 0);
        await CreateRunReportAsync("run-003", "checkout", baseDate, 10, 9, 1, 0, 0);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Trends);
        Assert.Equal(3, data.Trends.Points.Count);
        // Points should be ordered by date ascending
        Assert.Equal(baseDate.AddDays(-2), data.Trends.Points[0].Date);
        Assert.Equal(baseDate.AddDays(-1), data.Trends.Points[1].Date);
        Assert.Equal(baseDate, data.Trends.Points[2].Date);
    }

    [Fact]
    public async Task CollectAsync_WithImprovingTrend_DetectsImprovement()
    {
        var baseDate = DateTime.UtcNow.Date;
        // First half: 50% pass rate
        await CreateRunReportAsync("run-001", "checkout", baseDate.AddDays(-4), 10, 5, 5, 0, 0);
        await CreateRunReportAsync("run-002", "checkout", baseDate.AddDays(-3), 10, 5, 5, 0, 0);
        // Second half: 90% pass rate (improvement > 5%)
        await CreateRunReportAsync("run-003", "checkout", baseDate.AddDays(-1), 10, 9, 1, 0, 0);
        await CreateRunReportAsync("run-004", "checkout", baseDate, 10, 9, 1, 0, 0);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Trends);
        Assert.Equal("improving", data.Trends.Direction);
    }

    [Fact]
    public async Task CollectAsync_WithDecliningTrend_DetectsDecline()
    {
        var baseDate = DateTime.UtcNow.Date;
        // First half: 90% pass rate
        await CreateRunReportAsync("run-001", "checkout", baseDate.AddDays(-4), 10, 9, 1, 0, 0);
        await CreateRunReportAsync("run-002", "checkout", baseDate.AddDays(-3), 10, 9, 1, 0, 0);
        // Second half: 50% pass rate (decline > 5%)
        await CreateRunReportAsync("run-003", "checkout", baseDate.AddDays(-1), 10, 5, 5, 0, 0);
        await CreateRunReportAsync("run-004", "checkout", baseDate, 10, 5, 5, 0, 0);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Trends);
        Assert.Equal("declining", data.Trends.Direction);
    }

    [Fact]
    public async Task CollectAsync_WithStableTrend_DetectsStable()
    {
        var baseDate = DateTime.UtcNow.Date;
        // Consistent pass rate (within 5%)
        await CreateRunReportAsync("run-001", "checkout", baseDate.AddDays(-3), 10, 8, 2, 0, 0);
        await CreateRunReportAsync("run-002", "checkout", baseDate.AddDays(-2), 10, 8, 2, 0, 0);
        await CreateRunReportAsync("run-003", "checkout", baseDate.AddDays(-1), 10, 8, 2, 0, 0);
        await CreateRunReportAsync("run-004", "checkout", baseDate, 10, 8, 2, 0, 0);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Trends);
        Assert.Equal("stable", data.Trends.Direction);
    }

    [Fact]
    public async Task CollectAsync_WithMultipleSuites_CalculatesSuiteTrends()
    {
        var baseDate = DateTime.UtcNow.Date;
        await CreateRunReportAsync("run-001", "checkout", baseDate, 10, 9, 1, 0, 0);
        await CreateRunReportAsync("run-002", "payments", baseDate, 10, 7, 3, 0, 0);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Trends);
        Assert.Equal(2, data.Trends.BySuite.Count);
        var checkoutTrend = data.Trends.BySuite.First(s => s.Suite == "checkout");
        var paymentsTrend = data.Trends.BySuite.First(s => s.Suite == "payments");
        Assert.Equal(90m, checkoutTrend.PassRate);
        Assert.Equal(70m, paymentsTrend.PassRate);
    }

    [Fact]
    public async Task CollectAsync_SuiteTrend_CalculatesRunCount()
    {
        var baseDate = DateTime.UtcNow.Date;
        await CreateRunReportAsync("run-001", "checkout", baseDate.AddDays(-2), 10, 8, 2, 0, 0);
        await CreateRunReportAsync("run-002", "checkout", baseDate.AddDays(-1), 10, 9, 1, 0, 0);
        await CreateRunReportAsync("run-003", "checkout", baseDate, 10, 9, 1, 0, 0);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Trends);
        var checkoutTrend = data.Trends.BySuite.First(s => s.Suite == "checkout");
        Assert.Equal(3, checkoutTrend.RunCount);
    }

    [Fact]
    public async Task CollectAsync_SuiteTrend_CalculatesChange()
    {
        var baseDate = DateTime.UtcNow.Date;
        // Older runs: 70% pass rate
        await CreateRunReportAsync("run-001", "checkout", baseDate.AddDays(-3), 10, 7, 3, 0, 0);
        await CreateRunReportAsync("run-002", "checkout", baseDate.AddDays(-2), 10, 7, 3, 0, 0);
        // Recent runs: 90% pass rate
        await CreateRunReportAsync("run-003", "checkout", baseDate.AddDays(-1), 10, 9, 1, 0, 0);
        await CreateRunReportAsync("run-004", "checkout", baseDate, 10, 9, 1, 0, 0);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Trends);
        var checkoutTrend = data.Trends.BySuite.First(s => s.Suite == "checkout");
        Assert.True(checkoutTrend.Change > 0); // Should show improvement
    }

    [Fact]
    public async Task CollectAsync_SortedRunsDescending()
    {
        var baseDate = DateTime.UtcNow.Date;
        await CreateRunReportAsync("run-001", "checkout", baseDate.AddDays(-2), 10, 8, 2, 0, 0);
        await CreateRunReportAsync("run-002", "checkout", baseDate, 10, 9, 1, 0, 0);
        await CreateRunReportAsync("run-003", "checkout", baseDate.AddDays(-1), 10, 7, 3, 0, 0);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        // Runs should be sorted by date descending (most recent first)
        Assert.Equal("run-002", data.Runs[0].RunId);
        Assert.Equal("run-003", data.Runs[1].RunId);
        Assert.Equal("run-001", data.Runs[2].RunId);
    }

    [Fact]
    public async Task CollectAsync_MatchesLastRunToSuite()
    {
        await CreateSuiteIndexAsync("checkout", ["TC-001", "TC-002"]);
        var baseDate = DateTime.UtcNow.Date;
        await CreateRunReportAsync("run-001", "checkout", baseDate.AddDays(-1), 10, 8, 2, 0, 0);
        await CreateRunReportAsync("run-002", "checkout", baseDate, 10, 9, 1, 0, 0);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        var suite = data.Suites.First(s => s.Name == "checkout");
        Assert.NotNull(suite.LastRun);
        Assert.Equal("run-002", suite.LastRun.RunId);
    }

    [Fact]
    public async Task CollectAsync_WithZeroTests_ReturnsZeroPassRate()
    {
        await CreateRunReportAsync("run-001", "checkout", DateTime.UtcNow, 0, 0, 0, 0, 0);
        var collector = new DataCollector(_testDir);

        var data = await collector.CollectAsync();

        Assert.NotNull(data.Trends);
        Assert.Equal(0m, data.Trends.OverallPassRate);
    }

    #endregion

    #region Coverage Fragment Anchor Tests

    [Fact]
    public async Task CollectAsync_SourceRefsWithFragmentAnchors_MatchesDocPaths()
    {
        // Create docs directory with a doc file
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "checkout.md"), "# Checkout\nPayment flow docs.");

        // Create a suite with test entries that have fragment-anchored source_refs
        var index = new MetadataIndex
        {
            Suite = "checkout",
            GeneratedAt = DateTime.UtcNow,
            Tests =
            [
                new TestIndexEntry
                {
                    Id = "TC-001", Title = "Payment Flow", File = "TC-001.md",
                    Priority = "high", Tags = [],
                    SourceRefs = ["docs/checkout.md#Payment-Flow"]
                },
                new TestIndexEntry
                {
                    Id = "TC-002", Title = "Cart Total", File = "TC-002.md",
                    Priority = "high", Tags = [],
                    SourceRefs = ["docs/checkout.md#Cart-Total"]
                }
            ]
        };
        await WriteIndexAsync("checkout", index);

        var collector = new DataCollector(_testDir);
        var data = await collector.CollectAsync();

        // Documentation coverage should be non-zero since tests reference docs/checkout.md via fragments
        Assert.NotNull(data.CoverageSummary);
        Assert.True(data.CoverageSummary.Documentation.Percentage > 0,
            "Documentation coverage should be non-zero when source_refs have fragment anchors");
        Assert.Equal(1, data.CoverageSummary.Documentation.Covered);
        Assert.Equal(1, data.CoverageSummary.Documentation.Total);
        Assert.Equal(100m, data.CoverageSummary.Documentation.Percentage);
    }

    [Fact]
    public async Task CollectAsync_SourceRefsWithoutFragments_StillMatches()
    {
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "checkout.md"), "# Checkout");

        var index = new MetadataIndex
        {
            Suite = "checkout",
            GeneratedAt = DateTime.UtcNow,
            Tests =
            [
                new TestIndexEntry
                {
                    Id = "TC-001", Title = "Test 1", File = "TC-001.md",
                    Priority = "high", Tags = [],
                    SourceRefs = ["docs/checkout.md"]
                }
            ]
        };
        await WriteIndexAsync("checkout", index);

        var collector = new DataCollector(_testDir);
        var data = await collector.CollectAsync();

        Assert.NotNull(data.CoverageSummary);
        Assert.Equal(1, data.CoverageSummary.Documentation.Covered);
    }

    [Fact]
    public async Task CollectAsync_SourceRefToNonExistentDoc_DoesNotInflateCoverage()
    {
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "real.md"), "# Real Doc");

        var index = new MetadataIndex
        {
            Suite = "checkout",
            GeneratedAt = DateTime.UtcNow,
            Tests =
            [
                new TestIndexEntry
                {
                    Id = "TC-001", Title = "Test 1", File = "TC-001.md",
                    Priority = "high", Tags = [],
                    SourceRefs = ["docs/nonexistent.md#Section"]
                }
            ]
        };
        await WriteIndexAsync("checkout", index);

        var collector = new DataCollector(_testDir);
        var data = await collector.CollectAsync();

        Assert.NotNull(data.CoverageSummary);
        // The real doc exists but isn't referenced, the referenced doc doesn't exist
        Assert.Equal(0, data.CoverageSummary.Documentation.Covered);
        Assert.Equal(1, data.CoverageSummary.Documentation.Total);
        Assert.Equal(0m, data.CoverageSummary.Documentation.Percentage);
    }

    [Fact]
    public async Task CollectAsync_WithUndocumentedTests_PopulatesUndocumentedMetric()
    {
        // Create suite with mix of documented and undocumented tests
        var index = new MetadataIndex
        {
            Suite = "checkout",
            GeneratedAt = DateTime.UtcNow,
            Tests =
            [
                new TestIndexEntry
                {
                    Id = "TC-001", Title = "Documented test", File = "TC-001.md",
                    Priority = "high", Tags = [],
                    SourceRefs = ["docs/checkout.md#Flow"]
                },
                new TestIndexEntry
                {
                    Id = "TC-002", Title = "Undocumented test 1", File = "TC-002.md",
                    Priority = "medium", Tags = [],
                    SourceRefs = []
                },
                new TestIndexEntry
                {
                    Id = "TC-003", Title = "Undocumented test 2", File = "TC-003.md",
                    Priority = "low", Tags = [],
                    SourceRefs = []
                }
            ]
        };

        await WriteIndexAsync("checkout", index);
        var collector = new DataCollector(_testDir);
        var data = await collector.CollectAsync();

        Assert.NotNull(data.CoverageSummary);
        Assert.Equal(2, data.CoverageSummary.Documentation.UndocumentedTestCount);
        Assert.NotNull(data.CoverageSummary.Documentation.UndocumentedTestIds);
        Assert.Contains("TC-002", data.CoverageSummary.Documentation.UndocumentedTestIds);
        Assert.Contains("TC-003", data.CoverageSummary.Documentation.UndocumentedTestIds);
    }

    [Fact]
    public async Task CollectAsync_NoUndocumentedTests_ZeroUndocumentedCount()
    {
        // Create docs directory so there's something to measure
        var docsDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(docsDir);
        await File.WriteAllTextAsync(Path.Combine(docsDir, "checkout.md"), "# Checkout\nFlow docs.");

        var index = new MetadataIndex
        {
            Suite = "checkout",
            GeneratedAt = DateTime.UtcNow,
            Tests =
            [
                new TestIndexEntry
                {
                    Id = "TC-001", Title = "Documented test", File = "TC-001.md",
                    Priority = "high", Tags = [],
                    SourceRefs = ["docs/checkout.md"]
                }
            ]
        };

        await WriteIndexAsync("checkout", index);
        var collector = new DataCollector(_testDir);
        var data = await collector.CollectAsync();

        Assert.NotNull(data.CoverageSummary);
        Assert.Equal(0, data.CoverageSummary.Documentation.UndocumentedTestCount);
        Assert.Null(data.CoverageSummary.Documentation.UndocumentedTestIds);
    }

    #endregion

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

    private async Task CreateRunReportAsync(
        string runId,
        string suite,
        DateTime startedAt,
        int total,
        int passed,
        int failed,
        int skipped,
        int blocked)
    {
        var reportsPath = Path.Combine(_testDir, ".execution", "reports");
        Directory.CreateDirectory(reportsPath);

        var report = new
        {
            RunId = runId,
            Suite = suite,
            Status = "completed",
            StartedAt = startedAt,
            CompletedAt = startedAt.AddMinutes(5),
            StartedBy = "tester",
            Total = total,
            Passed = passed,
            Failed = failed,
            Skipped = skipped,
            Blocked = blocked
        };

        await File.WriteAllTextAsync(
            Path.Combine(reportsPath, $"{runId}.json"),
            JsonSerializer.Serialize(report));
    }
}
