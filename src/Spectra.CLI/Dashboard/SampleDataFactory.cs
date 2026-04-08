using Spectra.Core.Models.Dashboard;

namespace Spectra.CLI.Dashboard;

/// <summary>
/// Creates sample dashboard data for preview mode.
/// </summary>
public static class SampleDataFactory
{
    /// <summary>
    /// Creates a deterministic set of sample data for branding preview.
    /// </summary>
    public static DashboardData CreateSampleData()
    {
        var baseDate = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        return new DashboardData
        {
            GeneratedAt = DateTime.UtcNow,
            Repository = "sample-project",
            Suites = CreateSampleSuites(),
            Tests = CreateSampleTests(),
            Runs = CreateSampleRuns(baseDate),
            Trends = CreateSampleTrends(baseDate),
            CoverageSummary = CreateSampleCoverage()
        };
    }

    private static IReadOnlyList<SuiteStats> CreateSampleSuites() =>
    [
        new SuiteStats
        {
            Name = "checkout",
            TestCount = 15,
            AutomationCoverage = 73.3m,
            ByPriority = new Dictionary<string, int> { ["high"] = 5, ["medium"] = 7, ["low"] = 3 },
            ByComponent = new Dictionary<string, int> { ["Cart"] = 8, ["Payment"] = 7 },
            Tags = ["smoke", "regression", "e2e"]
        },
        new SuiteStats
        {
            Name = "authentication",
            TestCount = 10,
            AutomationCoverage = 60.0m,
            ByPriority = new Dictionary<string, int> { ["high"] = 4, ["medium"] = 4, ["low"] = 2 },
            ByComponent = new Dictionary<string, int> { ["Login"] = 5, ["SSO"] = 3, ["MFA"] = 2 },
            Tags = ["smoke", "security"]
        },
        new SuiteStats
        {
            Name = "search",
            TestCount = 8,
            AutomationCoverage = 37.5m,
            ByPriority = new Dictionary<string, int> { ["high"] = 2, ["medium"] = 4, ["low"] = 2 },
            ByComponent = new Dictionary<string, int> { ["Search"] = 5, ["Filters"] = 3 },
            Tags = ["regression"]
        }
    ];

    private static IReadOnlyList<TestEntry> CreateSampleTests() =>
    [
        new TestEntry { Id = "TC-101", Suite = "checkout", Title = "Add item to cart", File = "TC-101.md", Priority = "high", Component = "Cart", Tags = ["smoke"], HasAutomation = true, AutomatedBy = "tests/e2e/cart.spec.ts", SourceRefs = ["docs/checkout.md"] },
        new TestEntry { Id = "TC-102", Suite = "checkout", Title = "Remove item from cart", File = "TC-102.md", Priority = "medium", Component = "Cart", Tags = ["regression"], HasAutomation = true, SourceRefs = ["docs/checkout.md"] },
        new TestEntry { Id = "TC-103", Suite = "checkout", Title = "Apply discount code", File = "TC-103.md", Priority = "medium", Component = "Payment", Tags = ["regression"], HasAutomation = false, SourceRefs = ["docs/checkout.md"] },
        new TestEntry { Id = "TC-104", Suite = "checkout", Title = "Process credit card payment", File = "TC-104.md", Priority = "high", Component = "Payment", Tags = ["smoke", "e2e"], HasAutomation = true, AutomatedBy = "tests/e2e/payment.spec.ts", SourceRefs = ["docs/checkout.md"] },
        new TestEntry { Id = "TC-201", Suite = "authentication", Title = "Login with valid credentials", File = "TC-201.md", Priority = "high", Component = "Login", Tags = ["smoke"], HasAutomation = true, AutomatedBy = "tests/e2e/login.spec.ts", SourceRefs = ["docs/auth.md"] },
        new TestEntry { Id = "TC-202", Suite = "authentication", Title = "Login with invalid password", File = "TC-202.md", Priority = "high", Component = "Login", Tags = ["security"], HasAutomation = true, SourceRefs = ["docs/auth.md"] },
        new TestEntry { Id = "TC-203", Suite = "authentication", Title = "SSO redirect flow", File = "TC-203.md", Priority = "medium", Component = "SSO", Tags = ["smoke"], HasAutomation = false, SourceRefs = [] },
        new TestEntry { Id = "TC-204", Suite = "authentication", Title = "MFA verification", File = "TC-204.md", Priority = "medium", Component = "MFA", Tags = ["security"], HasAutomation = false, SourceRefs = [] },
        new TestEntry { Id = "TC-301", Suite = "search", Title = "Search by keyword", File = "TC-301.md", Priority = "high", Component = "Search", Tags = ["regression"], HasAutomation = true, AutomatedBy = "tests/e2e/search.spec.ts", SourceRefs = ["docs/search.md"] },
        new TestEntry { Id = "TC-302", Suite = "search", Title = "Filter results by category", File = "TC-302.md", Priority = "medium", Component = "Filters", Tags = ["regression"], HasAutomation = false, SourceRefs = ["docs/search.md"] }
    ];

    private static IReadOnlyList<RunSummary> CreateSampleRuns(DateTime baseDate) =>
    [
        new RunSummary
        {
            RunId = "run-001",
            Suite = "checkout",
            Status = "completed",
            StartedAt = baseDate,
            CompletedAt = baseDate.AddMinutes(45),
            StartedBy = "qa-engineer",
            Passed = 12,
            Failed = 2,
            Skipped = 1,
            Blocked = 0,
            Total = 15,
            DurationSeconds = 2712
        },
        new RunSummary
        {
            RunId = "run-002",
            Suite = "authentication",
            Status = "completed",
            StartedAt = baseDate.AddDays(1),
            CompletedAt = baseDate.AddDays(1).AddMinutes(30),
            StartedBy = "qa-engineer",
            Passed = 6,
            Failed = 3,
            Skipped = 1,
            Blocked = 0,
            Total = 10,
            DurationSeconds = 1805
        }
    ];

    private static TrendData CreateSampleTrends(DateTime baseDate) =>
        new()
        {
            OverallPassRate = 72.0m,
            Direction = "improving",
            Points =
            [
                new TrendPoint { Date = baseDate.AddDays(-4), PassRate = 60.0m, Total = 33, Passed = 20 },
                new TrendPoint { Date = baseDate.AddDays(-3), PassRate = 65.0m, Total = 33, Passed = 21 },
                new TrendPoint { Date = baseDate.AddDays(-2), PassRate = 70.0m, Total = 33, Passed = 23 },
                new TrendPoint { Date = baseDate.AddDays(-1), PassRate = 75.0m, Total = 33, Passed = 25 },
                new TrendPoint { Date = baseDate, PassRate = 72.0m, Total = 33, Passed = 24 }
            ]
        };

    private static CoverageSummaryData CreateSampleCoverage() =>
        new()
        {
            Documentation = new DocumentationSectionData
            {
                Covered = 3,
                Total = 4,
                Percentage = 75.0m,
                Details =
                [
                    new DocumentationCoverageDetail { Doc = "docs/checkout.md", TestCount = 4, Covered = true, TestIds = ["TC-101", "TC-102", "TC-103", "TC-104"] },
                    new DocumentationCoverageDetail { Doc = "docs/auth.md", TestCount = 2, Covered = true, TestIds = ["TC-201", "TC-202"] },
                    new DocumentationCoverageDetail { Doc = "docs/search.md", TestCount = 2, Covered = true, TestIds = ["TC-301", "TC-302"] },
                    new DocumentationCoverageDetail { Doc = "docs/admin.md", TestCount = 0, Covered = false, TestIds = [] }
                ]
            },
            AcceptanceCriteria = new AcceptanceCriteriaSectionData
            {
                Covered = 3,
                Total = 6,
                Percentage = 50.0m,
                HasCriteriaFile = true
            },
            Automation = new AutomationSectionData
            {
                Covered = 4,
                Total = 10,
                Percentage = 40.0m,
                Details =
                [
                    new AutomationSuiteDetail { Suite = "checkout", Total = 4, Automated = 3, Percentage = 75.0m },
                    new AutomationSuiteDetail { Suite = "authentication", Total = 4, Automated = 2, Percentage = 50.0m },
                    new AutomationSuiteDetail { Suite = "search", Total = 2, Automated = 1, Percentage = 50.0m }
                ]
            }
        };
}
