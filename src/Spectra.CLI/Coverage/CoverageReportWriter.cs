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

        // Requirements Coverage
        var req = report.RequirementsCoverage;
        sb.AppendLine("## Requirements Coverage");
        sb.AppendLine();

        if (!req.HasRequirementsFile && req.TotalRequirements == 0)
        {
            sb.AppendLine("No requirements file found. Create `docs/requirements/_requirements.yaml` to track requirements coverage.");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"**{req.Percentage:F1}%** — {req.CoveredRequirements} of {req.TotalRequirements} requirements covered");
            if (!req.HasRequirementsFile)
            {
                sb.AppendLine();
                sb.AppendLine("*Requirements discovered from test metadata (no `_requirements.yaml` file).*");
            }
            sb.AppendLine();

            if (req.Details.Count > 0)
            {
                sb.AppendLine("| Requirement | Title | Tests | Covered |");
                sb.AppendLine("|-------------|-------|-------|---------|");
                foreach (var detail in req.Details)
                {
                    var title = detail.Title ?? "—";
                    var tests = detail.Tests.Count > 0 ? string.Join(", ", detail.Tests) : "(none)";
                    var status = detail.Covered ? "Yes" : "No";
                    sb.AppendLine($"| {detail.Id} | {title} | {tests} | {status} |");
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

        var req = report.RequirementsCoverage;
        sb.AppendLine("REQUIREMENTS COVERAGE");
        sb.AppendLine(new string('-', 30));
        if (req.TotalRequirements > 0)
        {
            sb.AppendLine($"  {req.Percentage:F1}% — {req.CoveredRequirements}/{req.TotalRequirements} requirements covered");
            foreach (var detail in req.Details)
            {
                var mark = detail.Covered ? "+" : "-";
                var title = detail.Title ?? detail.Id;
                sb.AppendLine($"  [{mark}] {detail.Id}: {title}");
            }
        }
        else
        {
            sb.AppendLine("  No requirements defined.");
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
