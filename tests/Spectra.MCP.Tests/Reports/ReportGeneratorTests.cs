using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Reports;

namespace Spectra.MCP.Tests.Reports;

public class ReportGeneratorTests
{
    private readonly ReportGenerator _generator = new();

    [Fact]
    public void Generate_BasicRun_ReturnsValidReport()
    {
        var run = CreateTestRun();
        var results = new[]
        {
            CreateResult("TC-001", TestStatus.Passed),
            CreateResult("TC-002", TestStatus.Failed),
            CreateResult("TC-003", TestStatus.Skipped)
        };

        var report = _generator.Generate(run, results);

        Assert.Equal(run.RunId, report.RunId);
        Assert.Equal(run.Suite, report.Suite);
        Assert.Equal(run.StartedBy, report.ExecutedBy);
        Assert.Equal(3, report.Results.Count);
    }

    [Fact]
    public void Generate_CalculatesSummaryCorrectly()
    {
        var run = CreateTestRun();
        var results = new[]
        {
            CreateResult("TC-001", TestStatus.Passed),
            CreateResult("TC-002", TestStatus.Passed),
            CreateResult("TC-003", TestStatus.Failed),
            CreateResult("TC-004", TestStatus.Skipped),
            CreateResult("TC-005", TestStatus.Blocked)
        };

        var report = _generator.Generate(run, results);

        Assert.Equal(5, report.Summary.Total);
        Assert.Equal(2, report.Summary.Passed);
        Assert.Equal(1, report.Summary.Failed);
        Assert.Equal(1, report.Summary.Skipped);
        Assert.Equal(1, report.Summary.Blocked);
        Assert.Equal(40.0, report.Summary.PassRate);
    }

    [Fact]
    public void Generate_WithMultipleAttempts_IncludesAllAttempts()
    {
        var run = CreateTestRun();
        var results = new[]
        {
            CreateResult("TC-001", TestStatus.Failed, attempt: 1),
            CreateResult("TC-001", TestStatus.Passed, attempt: 2),
            CreateResult("TC-002", TestStatus.Passed, attempt: 1)
        };

        var report = _generator.Generate(run, results);

        // All attempts are included in the report (T085)
        Assert.Equal(3, report.Results.Count);
        Assert.Equal(3, report.Summary.Total); // 2 passed, 1 failed

        // Verify both attempts of TC-001 are present
        var tc001Attempts = report.Results.Where(r => r.TestId == "TC-001").ToList();
        Assert.Equal(2, tc001Attempts.Count);
        Assert.Contains(tc001Attempts, r => r.Attempt == 1 && r.Status == TestStatus.Failed);
        Assert.Contains(tc001Attempts, r => r.Attempt == 2 && r.Status == TestStatus.Passed);
    }

    [Fact]
    public void Generate_WithTestTitles_UsesProvidedTitles()
    {
        var run = CreateTestRun();
        var results = new[] { CreateResult("TC-001", TestStatus.Passed) };
        var titles = new Dictionary<string, string> { ["TC-001"] = "Login Test" };

        var report = _generator.Generate(run, results, titles);

        Assert.Equal("Login Test", report.Results[0].Title);
    }

    [Fact]
    public void Generate_WithoutTestTitles_UsesTestId()
    {
        var run = CreateTestRun();
        var results = new[] { CreateResult("TC-001", TestStatus.Passed) };

        var report = _generator.Generate(run, results);

        Assert.Equal("TC-001", report.Results[0].Title);
    }

    [Fact]
    public void Generate_CalculatesDuration()
    {
        var run = CreateTestRun();
        var now = DateTime.UtcNow;
        var result = new TestResult
        {
            RunId = run.RunId,
            TestId = "TC-001",
            TestHandle = "handle-1",
            Status = TestStatus.Passed,
            Attempt = 1,
            StartedAt = now.AddSeconds(-5),
            CompletedAt = now
        };

        var report = _generator.Generate(run, [result]);

        Assert.NotNull(report.Results[0].DurationMs);
        Assert.True(report.Results[0].DurationMs >= 5000);
    }

    [Fact]
    public void Generate_IncludesBlockedByInfo()
    {
        var run = CreateTestRun();
        var result = new TestResult
        {
            RunId = run.RunId,
            TestId = "TC-002",
            TestHandle = "handle-2",
            Status = TestStatus.Blocked,
            Attempt = 1,
            BlockedBy = "TC-001"
        };

        var report = _generator.Generate(run, [result]);

        Assert.Equal("TC-001", report.Results[0].BlockedBy);
    }

    [Fact]
    public void Generate_IncludesNotes()
    {
        var run = CreateTestRun();
        var result = new TestResult
        {
            RunId = run.RunId,
            TestId = "TC-001",
            TestHandle = "handle-1",
            Status = TestStatus.Failed,
            Attempt = 1,
            Notes = "Button was disabled"
        };

        var report = _generator.Generate(run, [result]);

        Assert.Equal("Button was disabled", report.Results[0].Notes);
    }

    [Fact]
    public void Generate_IncludesFilters()
    {
        var run = new Run
        {
            RunId = Guid.NewGuid().ToString(),
            Suite = "checkout",
            Status = RunStatus.Completed,
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            StartedBy = "test-user",
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Filters = new RunFilters
            {
                Priority = Priority.High,
                Tags = ["smoke"]
            }
        };
        var results = new[] { CreateResult("TC-001", TestStatus.Passed) };

        var report = _generator.Generate(run, results);

        Assert.NotNull(report.Filters);
        Assert.Equal(Priority.High, report.Filters.Priority);
        Assert.Contains("smoke", report.Filters.Tags!);
    }

    [Fact]
    public void GenerateSummary_FromCounts_ReturnsCorrectValues()
    {
        var counts = new Dictionary<TestStatus, int>
        {
            [TestStatus.Passed] = 10,
            [TestStatus.Failed] = 2,
            [TestStatus.Skipped] = 1,
            [TestStatus.Blocked] = 0
        };

        var summary = _generator.GenerateSummary(counts);

        Assert.Equal(13, summary.Total);
        Assert.Equal(10, summary.Passed);
        Assert.Equal(2, summary.Failed);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(0, summary.Blocked);
    }

    [Fact]
    public void Generate_EmptyResults_ReturnsEmptyReport()
    {
        var run = CreateTestRun();

        var report = _generator.Generate(run, []);

        Assert.Empty(report.Results);
        Assert.Equal(0, report.Summary.Total);
    }

    [Fact]
    public void Generate_PopulatesPriorityTagsComponent_FromTestCases()
    {
        var run = CreateTestRun();
        var results = new[] { CreateResult("TC-001", TestStatus.Passed) };
        var testCases = new Dictionary<string, TestCase>
        {
            ["TC-001"] = CreateTestCase("TC-001", priority: Priority.High,
                tags: ["smoke", "auth"], component: "Authentication")
        };

        var report = _generator.Generate(run, results, testTitles: null, testCases);

        var entry = report.Results[0];
        Assert.Equal(Priority.High, entry.Priority);
        Assert.Equal(["smoke", "auth"], entry.Tags!);
        Assert.Equal("Authentication", entry.Component);
    }

    [Fact]
    public void Generate_PopulatesCriteriaAndSourceRefs_FromTestCases()
    {
        var run = CreateTestRun();
        var results = new[] { CreateResult("TC-001", TestStatus.Failed) };
        var testCases = new Dictionary<string, TestCase>
        {
            ["TC-001"] = CreateTestCase("TC-001",
                criteria: ["AC-12", "AC-13"], sourceRefs: ["docs/auth/login.md"])
        };

        var report = _generator.Generate(run, results, testTitles: null, testCases);

        var entry = report.Results[0];
        Assert.Equal(["AC-12", "AC-13"], entry.Criteria!);
        Assert.Equal(["docs/auth/login.md"], entry.SourceRefs!);
    }

    [Fact]
    public void Generate_LeavesEnrichmentNull_WhenTestCaseAbsent()
    {
        var run = CreateTestRun();
        var results = new[] { CreateResult("TC-001", TestStatus.Passed) };

        // No testCases dictionary supplied.
        var report = _generator.Generate(run, results);

        var entry = report.Results[0];
        Assert.Null(entry.Priority);
        Assert.Null(entry.Tags);
        Assert.Null(entry.Component);
        Assert.Null(entry.Criteria);
        Assert.Null(entry.SourceRefs);
    }

    [Fact]
    public void Generate_NormalizesEmptyCollectionsToNull()
    {
        var run = CreateTestRun();
        var results = new[] { CreateResult("TC-001", TestStatus.Passed) };
        var testCases = new Dictionary<string, TestCase>
        {
            // Empty tags/criteria/sourceRefs (the CreateTestCase defaults).
            ["TC-001"] = CreateTestCase("TC-001", priority: Priority.Low)
        };

        var report = _generator.Generate(run, results, testTitles: null, testCases);

        var entry = report.Results[0];
        Assert.Equal(Priority.Low, entry.Priority);
        Assert.Null(entry.Tags);
        Assert.Null(entry.Criteria);
        Assert.Null(entry.SourceRefs);
    }

    [Fact]
    public void Generate_ComputesTiming_FromDurations()
    {
        var run = CreateTestRun();
        var now = DateTime.UtcNow;
        var results = new[]
        {
            new TestResult
            {
                RunId = run.RunId, TestId = "TC-001", TestHandle = "h1",
                Status = TestStatus.Passed, Attempt = 1,
                StartedAt = now.AddSeconds(-4), CompletedAt = now
            },
            new TestResult
            {
                RunId = run.RunId, TestId = "TC-002", TestHandle = "h2",
                Status = TestStatus.Passed, Attempt = 1,
                StartedAt = now.AddSeconds(-6), CompletedAt = now
            }
        };

        var report = _generator.Generate(run, results);

        Assert.NotNull(report.Timing);
        // ~4000ms + ~6000ms = ~10000ms total; ~5000ms average.
        Assert.True(report.Timing!.TotalTestDurationMs >= 10000);
        Assert.True(report.Timing.AverageTestDurationMs >= 5000);
    }

    [Fact]
    public void Generate_TimingNull_WhenNoDurations()
    {
        var run = CreateTestRun();
        var results = new[] { CreateResult("TC-001", TestStatus.Skipped) };

        var report = _generator.Generate(run, results);

        Assert.Null(report.Timing);
    }

    private static TestCase CreateTestCase(
        string id,
        Priority priority = Priority.Medium,
        IReadOnlyList<string>? tags = null,
        string? component = null,
        IReadOnlyList<string>? criteria = null,
        IReadOnlyList<string>? sourceRefs = null) => new()
    {
        Id = id,
        FilePath = $"{id}.md",
        Title = $"Title {id}",
        ExpectedResult = "Expected",
        Priority = priority,
        Tags = tags ?? [],
        Component = component,
        Criteria = criteria ?? [],
        SourceRefs = sourceRefs ?? []
    };

    private static Run CreateTestRun() => new()
    {
        RunId = Guid.NewGuid().ToString(),
        Suite = "checkout",
        Status = RunStatus.Completed,
        StartedAt = DateTime.UtcNow.AddMinutes(-30),
        StartedBy = "test-user",
        UpdatedAt = DateTime.UtcNow,
        CompletedAt = DateTime.UtcNow
    };

    private static TestResult CreateResult(string testId, TestStatus status, int attempt = 1) => new()
    {
        RunId = "run-1",
        TestId = testId,
        TestHandle = $"handle-{testId}-{attempt}",
        Status = status,
        Attempt = attempt
    };
}
