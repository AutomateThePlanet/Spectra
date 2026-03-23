using System.Text.Json;
using Spectra.CLI.Coverage;
using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Tests.Coverage;

/// <summary>
/// Unit tests for CoverageReportWriter (unified three-section report).
/// </summary>
public class CoverageReportWriterTests : IDisposable
{
    private readonly string _testDir;
    private readonly CoverageReportWriter _writer;

    public CoverageReportWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-report-writer-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _writer = new CoverageReportWriter();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    #region FormatAsJson Tests

    [Fact]
    public void FormatAsJson_ReturnsValidJson()
    {
        var report = CreateMinimalReport();

        var json = _writer.FormatAsJson(report);

        Assert.NotEmpty(json);
        var parsed = JsonDocument.Parse(json);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void FormatAsJson_IncludesThreeSections()
    {
        var report = CreateMinimalReport();

        var json = _writer.FormatAsJson(report);

        Assert.Contains("\"documentation_coverage\"", json);
        Assert.Contains("\"requirements_coverage\"", json);
        Assert.Contains("\"automation_coverage\"", json);
    }

    [Fact]
    public void FormatAsJson_IncludesDocumentationDetails()
    {
        var report = CreateReportWithDocs();

        var json = _writer.FormatAsJson(report);

        Assert.Contains("\"total_docs\"", json);
        Assert.Contains("\"covered_docs\"", json);
        Assert.Contains("docs/auth.md", json);
    }

    [Fact]
    public void FormatAsJson_IncludesRequirementsDetails()
    {
        var report = CreateReportWithRequirements();

        var json = _writer.FormatAsJson(report);

        Assert.Contains("\"total_requirements\"", json);
        Assert.Contains("\"has_requirements_file\"", json);
        Assert.Contains("REQ-001", json);
    }

    [Fact]
    public void FormatAsJson_IncludesAutomationDetails()
    {
        var report = CreateReportWithAutomation();

        var json = _writer.FormatAsJson(report);

        Assert.Contains("\"total_tests\"", json);
        Assert.Contains("\"automated\"", json);
        Assert.Contains("checkout", json);
    }

    [Fact]
    public void FormatAsJson_UsesSnakeCasePropertyNames()
    {
        var report = CreateMinimalReport();

        var json = _writer.FormatAsJson(report);

        Assert.Contains("\"generated_at\"", json);
        Assert.Contains("\"documentation_coverage\"", json);
        Assert.DoesNotContain("\"documentationCoverage\"", json);
    }

    [Fact]
    public void FormatAsJson_ThrowsOnNullReport()
    {
        Assert.Throws<ArgumentNullException>(() => _writer.FormatAsJson(null!));
    }

    #endregion

    #region FormatAsMarkdown Tests

    [Fact]
    public void FormatAsMarkdown_ReturnsValidMarkdown()
    {
        var report = CreateMinimalReport();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.NotEmpty(markdown);
        Assert.StartsWith("# Unified Coverage Report", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesDocumentationSection()
    {
        var report = CreateReportWithDocs();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("## Documentation Coverage", markdown);
        Assert.Contains("docs/auth.md", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesRequirementsSection()
    {
        var report = CreateReportWithRequirements();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("## Requirements Coverage", markdown);
        Assert.Contains("REQ-001", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesAutomationSection()
    {
        var report = CreateReportWithAutomation();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("## Automation Coverage", markdown);
        Assert.Contains("### By Suite", markdown);
        Assert.Contains("checkout", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesUnlinkedTests()
    {
        var report = CreateReportWithUnlinked();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("### Unlinked Tests", markdown);
        Assert.Contains("TC-001", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesOrphanedAutomation()
    {
        var report = CreateReportWithOrphans();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("### Orphaned Automation", markdown);
        Assert.Contains("test.cs", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesBrokenLinks()
    {
        var report = CreateReportWithBrokenLinks();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("### Broken Links", markdown);
        Assert.Contains("TC-001", markdown);
        Assert.Contains("File not found", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_NoRequirementsFile_ShowsMessage()
    {
        var report = CreateMinimalReport();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("No requirements file found", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_ThrowsOnNullReport()
    {
        Assert.Throws<ArgumentNullException>(() => _writer.FormatAsMarkdown(null!));
    }

    #endregion

    #region WriteAsync Tests

    [Fact]
    public async Task WriteAsync_CreatesJsonFile()
    {
        var report = CreateMinimalReport();
        var path = Path.Combine(_testDir, "report.json");

        await _writer.WriteAsync(path, report, CoverageReportFormat.Json);

        Assert.True(File.Exists(path));
        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("\"documentation_coverage\"", content);
    }

    [Fact]
    public async Task WriteAsync_CreatesMarkdownFile()
    {
        var report = CreateMinimalReport();
        var path = Path.Combine(_testDir, "report.md");

        await _writer.WriteAsync(path, report, CoverageReportFormat.Markdown);

        Assert.True(File.Exists(path));
        var content = await File.ReadAllTextAsync(path);
        Assert.StartsWith("# Unified Coverage Report", content);
    }

    [Fact]
    public async Task WriteAsync_CreatesDirectory()
    {
        var report = CreateMinimalReport();
        var path = Path.Combine(_testDir, "nested", "dir", "report.json");

        await _writer.WriteAsync(path, report, CoverageReportFormat.Json);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task WriteAsync_ThrowsOnNullPath()
    {
        var report = CreateMinimalReport();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _writer.WriteAsync(null!, report, CoverageReportFormat.Json));
    }

    [Fact]
    public async Task WriteAsync_ThrowsOnEmptyPath()
    {
        var report = CreateMinimalReport();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _writer.WriteAsync("", report, CoverageReportFormat.Json));
    }

    [Fact]
    public async Task WriteAsync_ThrowsOnNullReport()
    {
        var path = Path.Combine(_testDir, "report.json");

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _writer.WriteAsync(path, null!, CoverageReportFormat.Json));
    }

    #endregion

    #region Undocumented Tests

    [Fact]
    public void FormatAsJson_IncludesUndocumentedTestFields()
    {
        var report = CreateReportWithUndocumentedTests();

        var json = _writer.FormatAsJson(report);

        Assert.Contains("\"undocumented_test_count\"", json);
        Assert.Contains("\"undocumented_test_ids\"", json);
        Assert.Contains("TC-010", json);
        Assert.Contains("TC-011", json);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesUndocumentedSection()
    {
        var report = CreateReportWithUndocumentedTests();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("Undocumented tests: 2", markdown);
        Assert.Contains("2 test cases have no documentation source", markdown);
        Assert.Contains("documentation gaps", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_NoUndocumentedTests_OmitsSection()
    {
        var report = CreateMinimalReport();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.DoesNotContain("Undocumented tests:", markdown);
        Assert.DoesNotContain("documentation gaps", markdown);
    }

    #endregion

    #region FormatAsText Tests

    [Fact]
    public void FormatAsText_ReturnsValidText()
    {
        var report = CreateMinimalReport();

        var text = _writer.FormatAsText(report);

        Assert.NotEmpty(text);
        Assert.Contains("UNIFIED COVERAGE REPORT", text);
        Assert.Contains("DOCUMENTATION COVERAGE", text);
        Assert.Contains("REQUIREMENTS COVERAGE", text);
        Assert.Contains("AUTOMATION COVERAGE", text);
    }

    #endregion

    #region Helpers

    private static UnifiedCoverageReport CreateMinimalReport()
    {
        return new UnifiedCoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            DocumentationCoverage = new DocumentationCoverage
            {
                TotalDocs = 2,
                CoveredDocs = 1,
                Percentage = 50m
            },
            RequirementsCoverage = new RequirementsCoverage
            {
                TotalRequirements = 0,
                CoveredRequirements = 0,
                Percentage = 0m,
                HasRequirementsFile = false
            },
            AutomationCoverage = new AutomationCoverage
            {
                TotalTests = 10,
                Automated = 8,
                Percentage = 80m
            }
        };
    }

    private static UnifiedCoverageReport CreateReportWithDocs()
    {
        return new UnifiedCoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            DocumentationCoverage = new DocumentationCoverage
            {
                TotalDocs = 2,
                CoveredDocs = 1,
                Percentage = 50m,
                Details =
                [
                    new DocumentCoverageDetail
                    {
                        Doc = "docs/auth.md",
                        TestCount = 3,
                        Covered = true,
                        TestIds = ["TC-001", "TC-002", "TC-003"]
                    },
                    new DocumentCoverageDetail
                    {
                        Doc = "docs/admin.md",
                        TestCount = 0,
                        Covered = false
                    }
                ]
            },
            RequirementsCoverage = new RequirementsCoverage
            {
                TotalRequirements = 0,
                CoveredRequirements = 0,
                Percentage = 0m,
                HasRequirementsFile = false
            },
            AutomationCoverage = new AutomationCoverage
            {
                TotalTests = 3,
                Automated = 2,
                Percentage = 66.67m
            }
        };
    }

    private static UnifiedCoverageReport CreateReportWithRequirements()
    {
        return new UnifiedCoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            DocumentationCoverage = new DocumentationCoverage
            {
                TotalDocs = 1,
                CoveredDocs = 1,
                Percentage = 100m
            },
            RequirementsCoverage = new RequirementsCoverage
            {
                TotalRequirements = 2,
                CoveredRequirements = 1,
                Percentage = 50m,
                HasRequirementsFile = true,
                Details =
                [
                    new RequirementCoverageDetail
                    {
                        Id = "REQ-001",
                        Title = "Login must work",
                        Tests = ["TC-001"],
                        Covered = true
                    },
                    new RequirementCoverageDetail
                    {
                        Id = "REQ-002",
                        Title = "Logout must work",
                        Tests = [],
                        Covered = false
                    }
                ]
            },
            AutomationCoverage = new AutomationCoverage
            {
                TotalTests = 1,
                Automated = 1,
                Percentage = 100m
            }
        };
    }

    private static UnifiedCoverageReport CreateReportWithAutomation()
    {
        return new UnifiedCoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            DocumentationCoverage = new DocumentationCoverage
            {
                TotalDocs = 1,
                CoveredDocs = 1,
                Percentage = 100m
            },
            RequirementsCoverage = new RequirementsCoverage
            {
                TotalRequirements = 0,
                CoveredRequirements = 0,
                Percentage = 0m,
                HasRequirementsFile = false
            },
            AutomationCoverage = new AutomationCoverage
            {
                TotalTests = 5,
                Automated = 4,
                Percentage = 80m,
                BySuite =
                [
                    new SuiteCoverage
                    {
                        Suite = "checkout",
                        Total = 5,
                        Automated = 4,
                        CoveragePercentage = 80m
                    }
                ]
            }
        };
    }

    private static UnifiedCoverageReport CreateReportWithUnlinked()
    {
        var report = CreateMinimalReport();
        return new UnifiedCoverageReport
        {
            GeneratedAt = report.GeneratedAt,
            DocumentationCoverage = report.DocumentationCoverage,
            RequirementsCoverage = report.RequirementsCoverage,
            AutomationCoverage = new AutomationCoverage
            {
                TotalTests = 10,
                Automated = 8,
                Percentage = 80m,
                UnlinkedTests =
                [
                    new UnlinkedTest
                    {
                        TestId = "TC-001",
                        Title = "Verify checkout",
                        Suite = "checkout",
                        Priority = "high"
                    }
                ]
            }
        };
    }

    private static UnifiedCoverageReport CreateReportWithOrphans()
    {
        var report = CreateMinimalReport();
        return new UnifiedCoverageReport
        {
            GeneratedAt = report.GeneratedAt,
            DocumentationCoverage = report.DocumentationCoverage,
            RequirementsCoverage = report.RequirementsCoverage,
            AutomationCoverage = new AutomationCoverage
            {
                TotalTests = 10,
                Automated = 8,
                Percentage = 80m,
                OrphanedAutomation =
                [
                    new OrphanedAutomation
                    {
                        File = "test.cs",
                        ReferencedIds = ["TC-999"]
                    }
                ]
            }
        };
    }

    private static UnifiedCoverageReport CreateReportWithUndocumentedTests()
    {
        return new UnifiedCoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            DocumentationCoverage = new DocumentationCoverage
            {
                TotalDocs = 2,
                CoveredDocs = 1,
                Percentage = 50m,
                UndocumentedTestCount = 2,
                UndocumentedTestIds = ["TC-010", "TC-011"],
                Details =
                [
                    new DocumentCoverageDetail
                    {
                        Doc = "docs/auth.md",
                        TestCount = 3,
                        Covered = true,
                        TestIds = ["TC-001", "TC-002", "TC-003"]
                    },
                    new DocumentCoverageDetail
                    {
                        Doc = "docs/admin.md",
                        TestCount = 0,
                        Covered = false
                    }
                ]
            },
            RequirementsCoverage = new RequirementsCoverage
            {
                TotalRequirements = 0,
                CoveredRequirements = 0,
                Percentage = 0m,
                HasRequirementsFile = false
            },
            AutomationCoverage = new AutomationCoverage
            {
                TotalTests = 5,
                Automated = 3,
                Percentage = 60m
            }
        };
    }

    private static UnifiedCoverageReport CreateReportWithBrokenLinks()
    {
        var report = CreateMinimalReport();
        return new UnifiedCoverageReport
        {
            GeneratedAt = report.GeneratedAt,
            DocumentationCoverage = report.DocumentationCoverage,
            RequirementsCoverage = report.RequirementsCoverage,
            AutomationCoverage = new AutomationCoverage
            {
                TotalTests = 10,
                Automated = 8,
                Percentage = 80m,
                BrokenLinks =
                [
                    new BrokenLink
                    {
                        TestId = "TC-001",
                        AutomatedBy = "missing.cs",
                        Reason = "File not found"
                    }
                ]
            }
        };
    }

    #endregion
}
