using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Reports;

namespace Spectra.MCP.Tests.Reports;

/// <summary>
/// Rendering tests for the report-enrichment fields (priority/tags/component/criteria/source_refs)
/// and the run-level timing breakdown across JSON, Markdown, and HTML.
/// </summary>
public class ReportWriterEnrichmentTests : IDisposable
{
    private readonly string _dir;
    private readonly ReportWriter _writer;

    public ReportWriterEnrichmentTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "spectra-report-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _writer = new ReportWriter(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    // ── JSON ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Json_ContainsEnrichmentFields_WhenPresent()
    {
        var report = CreateReport(EnrichedEntry(TestStatus.Failed));
        var path = Path.Combine(_dir, "r.json");

        await _writer.WriteJsonAsync(report, path);
        var json = await File.ReadAllTextAsync(path);

        Assert.Contains("\"priority\": \"High\"", json);
        Assert.Contains("\"component\": \"Authentication\"", json);
        Assert.Contains("\"tags\"", json);
        Assert.Contains("\"criteria\"", json);
        Assert.Contains("\"source_refs\"", json);
        Assert.Contains("AC-12", json);
        Assert.Contains("docs/auth/login.md", json);
    }

    [Fact]
    public async Task Json_OmitsEnrichmentFields_WhenAbsent()
    {
        var report = CreateReport(BareEntry(TestStatus.Passed));
        var path = Path.Combine(_dir, "r.json");

        await _writer.WriteJsonAsync(report, path);
        var json = await File.ReadAllTextAsync(path);

        Assert.DoesNotContain("\"priority\"", json);
        Assert.DoesNotContain("\"tags\"", json);
        Assert.DoesNotContain("\"component\"", json);
        Assert.DoesNotContain("\"criteria\"", json);
        Assert.DoesNotContain("\"source_refs\"", json);
    }

    [Fact]
    public async Task Json_ContainsTiming_WhenPresent_AndOmits_WhenNull()
    {
        var withTiming = CreateReport(EnrichedEntry(TestStatus.Failed),
            timing: new ReportTiming { TotalTestDurationMs = 15360, AverageTestDurationMs = 5120 });
        var pathA = Path.Combine(_dir, "a.json");
        await _writer.WriteJsonAsync(withTiming, pathA);
        var jsonA = await File.ReadAllTextAsync(pathA);
        Assert.Contains("\"timing\"", jsonA);
        Assert.Contains("\"total_test_duration_ms\": 15360", jsonA);
        Assert.Contains("\"average_test_duration_ms\": 5120", jsonA);

        var noTiming = CreateReport(BareEntry(TestStatus.Passed), timing: null);
        var pathB = Path.Combine(_dir, "b.json");
        await _writer.WriteJsonAsync(noTiming, pathB);
        var jsonB = await File.ReadAllTextAsync(pathB);
        Assert.DoesNotContain("\"timing\"", jsonB);
    }

    // ── Markdown ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Markdown_AllResultsTable_HasPriorityColumn()
    {
        var report = CreateReport(EnrichedEntry(TestStatus.Failed));
        var path = Path.Combine(_dir, "r.md");

        await _writer.WriteMarkdownAsync(report, path);
        var md = await File.ReadAllTextAsync(path);

        Assert.Contains("| Test ID | Title | Status | Priority | Attempt | Duration |", md);
        // The failed-test detail block surfaces component/tags/criteria/source docs.
        Assert.Contains("**Component**: Authentication", md);
        Assert.Contains("**Tags**: smoke, auth", md);
        Assert.Contains("**Criteria**: AC-12, AC-13", md);
        Assert.Contains("**Source Docs**: docs/auth/login.md", md);
    }

    [Fact]
    public async Task Markdown_Header_ShowsTiming_WhenPresent()
    {
        var report = CreateReport(EnrichedEntry(TestStatus.Failed),
            timing: new ReportTiming { TotalTestDurationMs = 15360, AverageTestDurationMs = 5120 });
        var path = Path.Combine(_dir, "r.md");

        await _writer.WriteMarkdownAsync(report, path);
        var md = await File.ReadAllTextAsync(path);

        Assert.Contains("**Total Test Time**:", md);
        Assert.Contains("**Avg per Test**:", md);
    }

    // ── HTML ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Html_RendersEnrichment_ForNonPassingTest()
    {
        var report = CreateReport(EnrichedEntry(TestStatus.Failed));
        var path = Path.Combine(_dir, "r.html");

        await _writer.WriteHtmlAsync(report, path);
        var html = await File.ReadAllTextAsync(path);

        Assert.Contains("<th>Priority</th>", html);
        Assert.Contains("<strong>Priority:</strong>", html);
        Assert.Contains("<strong>Component:</strong>", html);
        Assert.Contains("<strong>Tags:</strong>", html);
        Assert.Contains("<strong>Criteria:</strong>", html);
        Assert.Contains("<strong>Source Docs:</strong>", html);
    }

    [Fact]
    public async Task Html_Header_ShowsTiming_WhenPresent()
    {
        var report = CreateReport(EnrichedEntry(TestStatus.Failed),
            timing: new ReportTiming { TotalTestDurationMs = 15360, AverageTestDurationMs = 5120 });
        var path = Path.Combine(_dir, "r.html");

        await _writer.WriteHtmlAsync(report, path);
        var html = await File.ReadAllTextAsync(path);

        Assert.Contains("Total Test Time:", html);
        Assert.Contains("Avg per Test:", html);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static TestResultEntry EnrichedEntry(TestStatus status) => new()
    {
        TestId = "TC-001",
        Title = "Verify login",
        Status = status,
        Attempt = 1,
        DurationMs = 5120,
        Priority = Priority.High,
        Tags = ["smoke", "auth"],
        Component = "Authentication",
        Criteria = ["AC-12", "AC-13"],
        SourceRefs = ["docs/auth/login.md"]
    };

    private static TestResultEntry BareEntry(TestStatus status) => new()
    {
        TestId = "TC-002",
        Title = "Bare test",
        Status = status,
        Attempt = 1
    };

    private static ExecutionReport CreateReport(TestResultEntry entry, ReportTiming? timing = null)
    {
        var counts = new Dictionary<TestStatus, int> { [entry.Status] = 1 };
        return new ExecutionReport
        {
            RunId = "run-123456789",
            Suite = "checkout",
            Environment = "default",
            StartedAt = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 6, 8, 10, 5, 0, DateTimeKind.Utc),
            ExecutedBy = "tester",
            Status = RunStatus.Completed,
            Summary = ReportSummary.FromCounts(counts),
            Results = [entry],
            Timing = timing
        };
    }
}
