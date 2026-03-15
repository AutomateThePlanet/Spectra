using System.Text;
using System.Text.Json;
using CoverageModels = Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Coverage;

/// <summary>
/// Writes automation coverage reports in various formats.
/// </summary>
public sealed class CoverageReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Writes a coverage report to a file.
    /// </summary>
    public async Task WriteAsync(
        string path,
        CoverageModels.CoverageReport report,
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
    public string FormatAsJson(CoverageModels.CoverageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    /// <summary>
    /// Formats the report as Markdown.
    /// </summary>
    public string FormatAsMarkdown(CoverageModels.CoverageReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# Automation Coverage Report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Total Tests | {report.Summary.TotalTests} |");
        sb.AppendLine($"| Automated | {report.Summary.Automated} |");
        sb.AppendLine($"| Manual Only | {report.Summary.ManualOnly} |");
        sb.AppendLine($"| Coverage | {report.Summary.CoveragePercentage:F1}% |");
        sb.AppendLine();

        // Coverage by Suite
        if (report.BySuite.Count > 0)
        {
            sb.AppendLine("## Coverage by Suite");
            sb.AppendLine();
            sb.AppendLine("| Suite | Total | Automated | Coverage |");
            sb.AppendLine("|-------|-------|-----------|----------|");

            foreach (var suite in report.BySuite.OrderBy(s => s.Suite))
            {
                sb.AppendLine($"| {suite.Suite} | {suite.Total} | {suite.Automated} | {suite.CoveragePercentage:F1}% |");
            }
            sb.AppendLine();
        }

        // Coverage by Component
        if (report.ByComponent.Count > 0)
        {
            sb.AppendLine("## Coverage by Component");
            sb.AppendLine();
            sb.AppendLine("| Component | Total | Automated | Coverage |");
            sb.AppendLine("|-----------|-------|-----------|----------|");

            foreach (var component in report.ByComponent.OrderBy(c => c.Component))
            {
                sb.AppendLine($"| {component.Component} | {component.Total} | {component.Automated} | {component.CoveragePercentage:F1}% |");
            }
            sb.AppendLine();
        }

        // Issues Section
        var hasIssues = report.UnlinkedTests.Count > 0 ||
                       report.OrphanedAutomation.Count > 0 ||
                       report.BrokenLinks.Count > 0 ||
                       report.Mismatches.Count > 0;

        if (hasIssues)
        {
            sb.AppendLine("## Issues");
            sb.AppendLine();
        }

        // Unlinked Tests
        if (report.UnlinkedTests.Count > 0)
        {
            sb.AppendLine($"### Unlinked Tests ({report.UnlinkedTests.Count})");
            sb.AppendLine();
            sb.AppendLine("Tests without automation links:");
            sb.AppendLine();

            foreach (var test in report.UnlinkedTests.OrderBy(t => t.TestId))
            {
                sb.AppendLine($"- **{test.TestId}**: {test.Title}");
                sb.AppendLine($"  - Suite: {test.Suite}");
                if (!string.IsNullOrEmpty(test.Priority))
                {
                    sb.AppendLine($"  - Priority: {test.Priority}");
                }
            }
            sb.AppendLine();
        }

        // Orphaned Automation
        if (report.OrphanedAutomation.Count > 0)
        {
            sb.AppendLine($"### Orphaned Automation ({report.OrphanedAutomation.Count})");
            sb.AppendLine();
            sb.AppendLine("Automation files referencing non-existent tests:");
            sb.AppendLine();

            foreach (var orphan in report.OrphanedAutomation.OrderBy(o => o.File))
            {
                sb.AppendLine($"- **{orphan.File}**");
                sb.AppendLine($"  - References: {string.Join(", ", orphan.ReferencedIds)}");
            }
            sb.AppendLine();
        }

        // Broken Links
        if (report.BrokenLinks.Count > 0)
        {
            sb.AppendLine($"### Broken Links ({report.BrokenLinks.Count})");
            sb.AppendLine();
            sb.AppendLine("Tests with invalid automation references:");
            sb.AppendLine();

            foreach (var broken in report.BrokenLinks.OrderBy(b => b.TestId))
            {
                sb.AppendLine($"- **{broken.TestId}**: `{broken.AutomatedBy}`");
                sb.AppendLine($"  - Reason: {broken.Reason}");
            }
            sb.AppendLine();
        }

        // Mismatches
        if (report.Mismatches.Count > 0)
        {
            sb.AppendLine($"### Link Mismatches ({report.Mismatches.Count})");
            sb.AppendLine();
            sb.AppendLine("Inconsistent bidirectional links:");
            sb.AppendLine();

            foreach (var mismatch in report.Mismatches.OrderBy(m => m.TestId))
            {
                sb.AppendLine($"- **{mismatch.TestId}**");
                sb.AppendLine($"  - Issue: {mismatch.Issue}");
                if (!string.IsNullOrEmpty(mismatch.TestAutomatedBy))
                {
                    sb.AppendLine($"  - Test automated_by: `{mismatch.TestAutomatedBy}`");
                }
                if (!string.IsNullOrEmpty(mismatch.AutomationFile))
                {
                    sb.AppendLine($"  - Automation file: `{mismatch.AutomationFile}`");
                }
            }
            sb.AppendLine();
        }

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
