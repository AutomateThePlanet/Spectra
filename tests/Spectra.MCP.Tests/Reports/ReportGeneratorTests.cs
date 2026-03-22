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
