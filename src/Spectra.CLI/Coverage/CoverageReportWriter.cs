using System.Text;
using System.Text.Json;
using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Coverage;

/// <summary>
/// Writes unified coverage reports in various formats.
/// </summary>
public sealed class CoverageReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Writes a unified coverage report to a file.
    /// </summary>
    public async Task WriteAsync(
        string path,
        UnifiedCoverageReport report,
        CoverageReportFormat format,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(report);

        var content = format switch
        {
            CoverageReportFormat.Json => FormatAsJson(report),
            CoverageReportFormat.Markdown => FormatAsMarkdown(report),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content, ct);
    }

    /// <summary>
    /// Formats the report as JSON.
    /// </summary>
    public string FormatAsJson(UnifiedCoverageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    /// <summary>
    /// Formats the report as Markdown.
    /// </summary>
    public string FormatAsMarkdown(UnifiedCoverageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();

        sb.AppendLine("# Unified Coverage Report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Documentation Coverage
        var doc = report.DocumentationCoverage;
        sb.AppendLine("## Documentation Coverage");
        sb.AppendLine();
        sb.AppendLine($"**{doc.Percentage:F1}%** — {doc.CoveredDocs} of {doc.TotalDocs} documents covered");
        sb.AppendLine();

        if (doc.UndocumentedTestCount > 0)
        {
            var totalTests = doc.Details.Sum(d => d.TestCount) + doc.UndocumentedTestCount;
            var undocPct = totalTests > 0
                ? (doc.UndocumentedTestCount * 100.0m) / totalTests
                : 0m;
            sb.AppendLine($"Undocumented tests: {doc.UndocumentedTestCount} ({undocPct:F1}%)");
            sb.AppendLine();
            sb.AppendLine($"> :warning: {doc.UndocumentedTestCount} test cases have no documentation source. These may indicate documentation gaps.");
            sb.AppendLine();
        }

        if (doc.Details.Count > 0)
        {
            sb.AppendLine("| Document | Tests | Covered |");
            sb.AppendLine("|----------|-------|---------|");
            foreach (var detail in doc.Details)
            {
                var status = detail.Covered ? "Yes" : "No";
                sb.AppendLine($"| {detail.Doc} | {detail.TestCount} | {status} |");
            }
            sb.AppendLine();
        }

        // Acceptance Criteria Coverage
        var criteria = report.AcceptanceCriteriaCoverage;
        sb.AppendLine("## Acceptance Criteria Coverage");
        sb.AppendLine();

        if (!criteria.HasCriteriaFile && criteria.TotalCriteria == 0)
        {
            sb.AppendLine("No acceptance criteria file found. Run `spectra ai analyze --extract-criteria` to extract from documentation, or create `docs/criteria/_criteria_index.yaml` manually.");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"**{criteria.Percentage:F1}%** — {criteria.CoveredCriteria} of {criteria.TotalCriteria} acceptance criteria covered");
            if (!criteria.HasCriteriaFile)
            {
                sb.AppendLine();
                sb.AppendLine("*Acceptance criteria discovered from test metadata (no `_criteria_index.yaml` file).*");
            }
            sb.AppendLine();

            if (criteria.Details.Count > 0)
            {
                sb.AppendLine("| Criterion | Text | Tests | Covered |");
                sb.AppendLine("|-----------|------|-------|---------|");
                foreach (var detail in criteria.Details)
                {
                    var text = detail.Text ?? "—";
                    var tests = detail.Tests.Count > 0 ? string.Join(", ", detail.Tests) : "(none)";
                    var status = detail.Covered ? "Yes" : "No";
                    sb.AppendLine($"| {detail.Id} | {text} | {tests} | {status} |");
                }
                sb.AppendLine();
            }
        }

        // Automation Coverage
        var auto = report.AutomationCoverage;
        sb.AppendLine("## Automation Coverage");
        sb.AppendLine();
        sb.AppendLine($"**{auto.Percentage:F1}%** — {auto.Automated} of {auto.TotalTests} tests automated");
        sb.AppendLine();

        if (auto.BySuite.Count > 0)
        {
            sb.AppendLine("### By Suite");
            sb.AppendLine();
            sb.AppendLine("| Suite | Total | Automated | Coverage |");
            sb.AppendLine("|-------|-------|-----------|----------|");
            foreach (var suite in auto.BySuite)
            {
                sb.AppendLine($"| {suite.Suite} | {suite.Total} | {suite.Automated} | {suite.CoveragePercentage:F1}% |");
            }
            sb.AppendLine();
        }

        // Issues
        if (auto.UnlinkedTests.Count > 0)
        {
            sb.AppendLine($"### Unlinked Tests ({auto.UnlinkedTests.Count})");
            sb.AppendLine();
            foreach (var test in auto.UnlinkedTests)
            {
                sb.AppendLine($"- **{test.TestId}**: {test.Title} ({test.Suite})");
            }
            sb.AppendLine();
        }

        if (auto.OrphanedAutomation.Count > 0)
        {
            sb.AppendLine($"### Orphaned Automation ({auto.OrphanedAutomation.Count})");
            sb.AppendLine();
            foreach (var orphan in auto.OrphanedAutomation)
            {
                sb.AppendLine($"- **{orphan.File}**: references {string.Join(", ", orphan.ReferencedIds)}");
            }
            sb.AppendLine();
        }

        if (auto.BrokenLinks.Count > 0)
        {
            sb.AppendLine($"### Broken Links ({auto.BrokenLinks.Count})");
            sb.AppendLine();
            foreach (var broken in auto.BrokenLinks)
            {
                sb.AppendLine($"- **{broken.TestId}**: `{broken.AutomatedBy}` ({broken.Reason})");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats the report as plain text.
    /// </summary>
    public string FormatAsText(UnifiedCoverageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();

        sb.AppendLine("UNIFIED COVERAGE REPORT");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        var doc = report.DocumentationCoverage;
        sb.AppendLine("DOCUMENTATION COVERAGE");
        sb.AppendLine(new string('-', 30));
        sb.AppendLine($"  {doc.Percentage:F1}% — {doc.CoveredDocs}/{doc.TotalDocs} documents covered");
        foreach (var detail in doc.Details)
        {
            var mark = detail.Covered ? "+" : "-";
            sb.AppendLine($"  [{mark}] {detail.Doc} ({detail.TestCount} tests)");
        }
        sb.AppendLine();

        var criteria = report.AcceptanceCriteriaCoverage;
        sb.AppendLine("ACCEPTANCE CRITERIA COVERAGE");
        sb.AppendLine(new string('-', 30));
        if (criteria.TotalCriteria > 0)
        {
            sb.AppendLine($"  {criteria.Percentage:F1}% — {criteria.CoveredCriteria}/{criteria.TotalCriteria} acceptance criteria covered");
            foreach (var detail in criteria.Details)
            {
                var mark = detail.Covered ? "+" : "-";
                var text = detail.Text ?? detail.Id;
                sb.AppendLine($"  [{mark}] {detail.Id}: {text}");
            }
        }
        else
        {
            sb.AppendLine("  No acceptance criteria defined.");
        }
        sb.AppendLine();

        var auto = report.AutomationCoverage;
        sb.AppendLine("AUTOMATION COVERAGE");
        sb.AppendLine(new string('-', 30));
        sb.AppendLine($"  {auto.Percentage:F1}% — {auto.Automated}/{auto.TotalTests} tests automated");
        foreach (var suite in auto.BySuite)
        {
            sb.AppendLine($"  {suite.Suite}: {suite.CoveragePercentage:F1}% ({suite.Automated}/{suite.Total})");
        }
        sb.AppendLine();

        return sb.ToString();
    }
}

/// <summary>
/// Supported coverage report formats.
/// </summary>
public enum CoverageReportFormat
{
    /// <summary>JSON format with snake_case property names.</summary>
    Json,

    /// <summary>Human-readable Markdown format.</summary>
    Markdown
}
