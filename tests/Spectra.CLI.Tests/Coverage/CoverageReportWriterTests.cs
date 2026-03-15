using System.Text.Json;
using Spectra.CLI.Coverage;
using CoverageModels = Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Tests.Coverage;

/// <summary>
/// Unit tests for CoverageReportWriter.
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
        // Should be parseable as JSON
        var parsed = JsonDocument.Parse(json);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void FormatAsJson_IncludesSummary()
    {
        var report = CreateMinimalReport();

        var json = _writer.FormatAsJson(report);

        Assert.Contains("\"summary\"", json);
        Assert.Contains("\"total_tests\"", json);
        Assert.Contains("\"automated\"", json);
        Assert.Contains("\"manual_only\"", json);
    }

    [Fact]
    public void FormatAsJson_IncludesBySuite()
    {
        var report = CreateReportWithSuites();

        var json = _writer.FormatAsJson(report);

        Assert.Contains("\"by_suite\"", json);
        Assert.Contains("checkout", json);
    }

    [Fact]
    public void FormatAsJson_IncludesUnlinkedTests()
    {
        var report = CreateReportWithUnlinked();

        var json = _writer.FormatAsJson(report);

        Assert.Contains("\"unlinked_tests\"", json);
        Assert.Contains("TC-001", json);
    }

    [Fact]
    public void FormatAsJson_UsesSnakeCasePropertyNames()
    {
        var report = CreateMinimalReport();

        var json = _writer.FormatAsJson(report);

        Assert.Contains("\"generated_at\"", json);
        Assert.Contains("\"coverage_percentage\"", json);
        Assert.DoesNotContain("\"generatedAt\"", json);
        Assert.DoesNotContain("\"coveragePercentage\"", json);
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
        Assert.StartsWith("# Automation Coverage Report", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesGeneratedDate()
    {
        var report = CreateMinimalReport();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("Generated:", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesSummaryTable()
    {
        var report = CreateMinimalReport();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("## Summary", markdown);
        Assert.Contains("| Metric | Value |", markdown);
        Assert.Contains("| Total Tests |", markdown);
        Assert.Contains("| Automated |", markdown);
        Assert.Contains("| Coverage |", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesSuiteTable()
    {
        var report = CreateReportWithSuites();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("## Coverage by Suite", markdown);
        Assert.Contains("| Suite | Total | Automated | Coverage |", markdown);
        Assert.Contains("checkout", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesComponentTable()
    {
        var report = CreateReportWithComponents();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("## Coverage by Component", markdown);
        Assert.Contains("| Component | Total | Automated | Coverage |", markdown);
        Assert.Contains("cart", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesUnlinkedTests()
    {
        var report = CreateReportWithUnlinked();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("### Unlinked Tests", markdown);
        Assert.Contains("TC-001", markdown);
        Assert.Contains("Verify checkout", markdown);
    }

    [Fact]
    public void FormatAsMarkdown_IncludesOrphanedAutomation()
    {
        var report = CreateReportWithOrphans();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("### Orphaned Automation", markdown);
        Assert.Contains("test.cs", markdown);
        Assert.Contains("TC-999", markdown);
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
    public void FormatAsMarkdown_IncludesMismatches()
    {
        var report = CreateReportWithMismatches();

        var markdown = _writer.FormatAsMarkdown(report);

        Assert.Contains("### Link Mismatches", markdown);
        Assert.Contains("TC-001", markdown);
        Assert.Contains("Different file", markdown);
        Assert.Contains("a.cs", markdown);
        Assert.Contains("b.cs", markdown);
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
        Assert.Contains("\"summary\"", content);
    }

    [Fact]
    public async Task WriteAsync_CreatesMarkdownFile()
    {
        var report = CreateMinimalReport();
        var path = Path.Combine(_testDir, "report.md");

        await _writer.WriteAsync(path, report, CoverageReportFormat.Markdown);

        Assert.True(File.Exists(path));
        var content = await File.ReadAllTextAsync(path);
        Assert.StartsWith("# Automation Coverage Report", content);
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

    #region Helpers

    private static CoverageModels.CoverageReport CreateMinimalReport()
    {
        return new CoverageModels.CoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            Summary = new CoverageModels.CoverageSummary
            {
                TotalTests = 10,
                Automated = 8,
                ManualOnly = 2,
                CoveragePercentage = 80m
            }
        };
    }

    private static CoverageModels.CoverageReport CreateReportWithSuites()
    {
        return new CoverageModels.CoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            Summary = new CoverageModels.CoverageSummary
            {
                TotalTests = 10,
                Automated = 8,
                ManualOnly = 2,
                CoveragePercentage = 80m
            },
            BySuite =
            [
                new CoverageModels.SuiteCoverage
                {
                    Suite = "checkout",
                    Total = 5,
                    Automated = 4,
                    CoveragePercentage = 80m
                }
            ]
        };
    }

    private static CoverageModels.CoverageReport CreateReportWithComponents()
    {
        return new CoverageModels.CoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            Summary = new CoverageModels.CoverageSummary
            {
                TotalTests = 10,
                Automated = 8,
                ManualOnly = 2,
                CoveragePercentage = 80m
            },
            ByComponent =
            [
                new CoverageModels.ComponentCoverage
                {
                    Component = "cart",
                    Total = 5,
                    Automated = 4,
                    CoveragePercentage = 80m
                }
            ]
        };
    }

    private static CoverageModels.CoverageReport CreateReportWithUnlinked()
    {
        return new CoverageModels.CoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            Summary = new CoverageModels.CoverageSummary
            {
                TotalTests = 10,
                Automated = 8,
                ManualOnly = 2,
                CoveragePercentage = 80m
            },
            UnlinkedTests =
            [
                new CoverageModels.UnlinkedTest
                {
                    TestId = "TC-001",
                    Title = "Verify checkout",
                    Suite = "checkout",
                    Priority = "high"
                }
            ]
        };
    }

    private static CoverageModels.CoverageReport CreateReportWithOrphans()
    {
        return new CoverageModels.CoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            Summary = new CoverageModels.CoverageSummary
            {
                TotalTests = 10,
                Automated = 8,
                ManualOnly = 2,
                CoveragePercentage = 80m
            },
            OrphanedAutomation =
            [
                new CoverageModels.OrphanedAutomation
                {
                    File = "test.cs",
                    ReferencedIds = ["TC-999"]
                }
            ]
        };
    }

    private static CoverageModels.CoverageReport CreateReportWithBrokenLinks()
    {
        return new CoverageModels.CoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            Summary = new CoverageModels.CoverageSummary
            {
                TotalTests = 10,
                Automated = 8,
                ManualOnly = 2,
                CoveragePercentage = 80m
            },
            BrokenLinks =
            [
                new CoverageModels.BrokenLink
                {
                    TestId = "TC-001",
                    AutomatedBy = "missing.cs",
                    Reason = "File not found"
                }
            ]
        };
    }

    private static CoverageModels.CoverageReport CreateReportWithMismatches()
    {
        return new CoverageModels.CoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            Summary = new CoverageModels.CoverageSummary
            {
                TotalTests = 10,
                Automated = 8,
                ManualOnly = 2,
                CoveragePercentage = 80m
            },
            Mismatches =
            [
                new CoverageModels.LinkMismatch
                {
                    TestId = "TC-001",
                    Issue = "Different file",
                    TestAutomatedBy = "a.cs",
                    AutomationFile = "b.cs"
                }
            ]
        };
    }

    #endregion
}
