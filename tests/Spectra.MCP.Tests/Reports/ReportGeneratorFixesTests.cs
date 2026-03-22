using System.Text.Json;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Reports;

namespace Spectra.MCP.Tests.Reports;

public class ReportGeneratorFixesTests
{
    [Fact]
    public void Generate_WithTestTitles_UsesActualTitles()
    {
        // Arrange
        var generator = new ReportGenerator();
        var run = CreateTestRun();
        var results = new List<TestResult>
        {
            new() { RunId = "run1", TestId = "TC-001", TestHandle = "h1", Status = TestStatus.Passed, Attempt = 1 },
            new() { RunId = "run1", TestId = "TC-002", TestHandle = "h2", Status = TestStatus.Failed, Attempt = 1, Notes = "Failed reason" }
        };
        var testTitles = new Dictionary<string, string>
        {
            ["TC-001"] = "Verify successful login with valid credentials",
            ["TC-002"] = "Verify error message for invalid password"
        };

        // Act
        var report = generator.Generate(run, results, testTitles);

        // Assert - titles should be actual titles, not IDs
        Assert.Equal("Verify successful login with valid credentials", report.Results[0].Title);
        Assert.Equal("Verify error message for invalid password", report.Results[1].Title);
    }

    [Fact]
    public void Generate_WithoutTestTitles_FallsBackToTestId()
    {
        // Arrange
        var generator = new ReportGenerator();
        var run = CreateTestRun();
        var results = new List<TestResult>
        {
            new() { RunId = "run1", TestId = "TC-001", TestHandle = "h1", Status = TestStatus.Passed, Attempt = 1 }
        };

        // Act
        var report = generator.Generate(run, results, null);

        // Assert
        Assert.Equal("TC-001", report.Results[0].Title);
    }

    [Fact]
    public void Generate_DurationMs_IsPositiveWithMismatchedTimezones()
    {
        // Arrange - simulate the bug: started_at with +02:00 offset, completed_at in UTC
        var generator = new ReportGenerator();
        var run = CreateTestRun();

        // Simulate local time 23:16 (+02:00) = 21:16 UTC
        var localStart = new DateTimeOffset(2024, 3, 19, 23, 16, 30, TimeSpan.FromHours(2));
        var startedAt = localStart.LocalDateTime;  // Local datetime

        // Completed at 21:22 UTC = 6 minutes after start
        var completedAt = new DateTime(2024, 3, 19, 21, 22, 30, DateTimeKind.Utc);

        var results = new List<TestResult>
        {
            new()
            {
                RunId = "run1",
                TestId = "TC-001",
                TestHandle = "h1",
                Status = TestStatus.Passed,
                Attempt = 1,
                StartedAt = startedAt,
                CompletedAt = completedAt
            }
        };

        // Act
        var report = generator.Generate(run, results);

        // Assert - duration should be positive (approximately 6 minutes = 360000ms)
        Assert.NotNull(report.Results[0].DurationMs);
        Assert.True(report.Results[0].DurationMs >= 0, $"Duration should be positive but was {report.Results[0].DurationMs}");
    }

    [Fact]
    public void ExecutionReport_DurationMinutes_IsPositiveWithMismatchedTimezones()
    {
        // Arrange - simulate the bug: started_at with +02:00 offset, completed_at in UTC
        // Local time 23:16 (+02:00) = 21:16 UTC
        var localStart = new DateTimeOffset(2024, 3, 19, 23, 16, 30, TimeSpan.FromHours(2));
        var startedAt = localStart.LocalDateTime;

        // Completed at 21:22 UTC = 6 minutes after start
        var completedAt = new DateTime(2024, 3, 19, 21, 22, 30, DateTimeKind.Utc);

        var report = new ExecutionReport
        {
            RunId = "run1",
            Suite = "test",
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ExecutedBy = "tester",
            Status = RunStatus.Completed,
            Summary = new ReportSummary { Total = 1, Passed = 1, Failed = 0, Skipped = 0, Blocked = 0 },
            Results = []
        };

        // Act & Assert - duration should be positive
        Assert.True(report.DurationMinutes >= 0, $"DurationMinutes should be positive but was {report.DurationMinutes}");
    }

    [Fact]
    public void TestResultEntry_Status_SerializesAsString()
    {
        // Arrange
        var entry = new TestResultEntry
        {
            TestId = "TC-001",
            Title = "Test",
            Status = TestStatus.Passed,
            Attempt = 1
        };

        // Act
        var json = JsonSerializer.Serialize(entry);

        // Assert - status should be "Passed" string, not 2 (the enum integer)
        Assert.Contains("\"status\":\"Passed\"", json);
        Assert.DoesNotContain("\"status\":2", json);
    }

    [Fact]
    public void ExecutionReport_Status_SerializesAsString()
    {
        // Arrange
        var report = new ExecutionReport
        {
            RunId = "run1",
            Suite = "test",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ExecutedBy = "tester",
            Status = RunStatus.Completed,
            Summary = new ReportSummary { Total = 1, Passed = 1, Failed = 0, Skipped = 0, Blocked = 0 },
            Results = []
        };

        // Act
        var json = JsonSerializer.Serialize(report);

        // Assert - status should be "Completed" string, not 3 (the enum integer)
        Assert.Contains("\"status\":\"Completed\"", json);
        Assert.DoesNotContain("\"status\":3", json);
    }

    private static Run CreateTestRun()
    {
        return new Run
        {
            RunId = "run1",
            Suite = "test-suite",
            Status = RunStatus.Completed,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            StartedBy = "tester",
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
    }
}
