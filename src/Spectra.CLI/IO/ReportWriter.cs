using System.Text;
using System.Text.Json;
using Spectra.Core.Models;

namespace Spectra.CLI.IO;

/// <summary>
/// Writes coverage reports in various formats.
/// </summary>
public sealed class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Writes a report to a file in the specified format.
    /// </summary>
    public async Task WriteAsync(
        string path,
        CoverageReport report,
        ReportFormat format,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(report);

        var content = format switch
        {
            ReportFormat.Json => FormatAsJson(report),
            ReportFormat.Markdown => FormatAsMarkdown(report),
            ReportFormat.Text => FormatAsText(report),
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
    /// Formats report as JSON.
    /// </summary>
    public string FormatAsJson(CoverageReport report)
    {
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    /// <summary>
    /// Formats report as Markdown.
    /// </summary>
    public string FormatAsMarkdown(CoverageReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Test Coverage Report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Total Documents | {report.TotalDocuments} |");
        sb.AppendLine($"| Covered Documents | {report.CoveredDocuments} |");
        sb.AppendLine($"| Uncovered Documents | {report.UncoveredDocuments} |");
        sb.AppendLine($"| Coverage | {report.CoveragePercentage:F1}% |");
        sb.AppendLine($"| Total Tests | {report.TotalTests} |");
        sb.AppendLine();

        // Suites
        if (report.Suites.Count > 0)
        {
            sb.AppendLine("## Suites");
            sb.AppendLine();
            sb.AppendLine("| Suite | Tests | Documents Covered |");
            sb.AppendLine("|-------|-------|-------------------|");

            foreach (var suite in report.Suites)
            {
                sb.AppendLine($"| {suite.Name} | {suite.TestCount} | {suite.DocumentsCovered} |");
            }

            sb.AppendLine();
        }

        // Coverage Gaps
        if (report.Gaps.Count > 0)
        {
            sb.AppendLine("## Coverage Gaps");
            sb.AppendLine();

            var criticalGaps = report.Gaps.Where(g => g.Severity == GapSeverity.Critical).ToList();
            var highGaps = report.Gaps.Where(g => g.Severity == GapSeverity.High).ToList();

            if (criticalGaps.Count > 0)
            {
                sb.AppendLine("### Critical");
                sb.AppendLine();
                foreach (var gap in criticalGaps)
                {
                    sb.AppendLine($"- **{gap.DocumentPath}**");
                    sb.AppendLine($"  - {gap.Reason}");
                    if (gap.Suggestion is not null)
                    {
                        sb.AppendLine($"  - Suggestion: {gap.Suggestion}");
                    }
                }
                sb.AppendLine();
            }

            if (highGaps.Count > 0)
            {
                sb.AppendLine("### High Priority");
                sb.AppendLine();
                foreach (var gap in highGaps)
                {
                    sb.AppendLine($"- **{gap.DocumentPath}**");
                    sb.AppendLine($"  - {gap.Reason}");
                }
                sb.AppendLine();
            }
        }

        // Uncovered Documents
        var uncovered = report.Documents.Where(d => !d.IsCovered).ToList();
        if (uncovered.Count > 0)
        {
            sb.AppendLine("## Uncovered Documents");
            sb.AppendLine();
            foreach (var doc in uncovered)
            {
                sb.AppendLine($"- {doc.Path}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats report as plain text.
    /// </summary>
    public string FormatAsText(CoverageReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("TEST COVERAGE REPORT");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine();
        sb.AppendLine("SUMMARY");
        sb.AppendLine(new string('-', 30));
        sb.AppendLine($"  Total Documents:     {report.TotalDocuments}");
        sb.AppendLine($"  Covered Documents:   {report.CoveredDocuments}");
        sb.AppendLine($"  Uncovered Documents: {report.UncoveredDocuments}");
        sb.AppendLine($"  Coverage:            {report.CoveragePercentage:F1}%");
        sb.AppendLine($"  Total Tests:         {report.TotalTests}");
        sb.AppendLine();

        if (report.Gaps.Count > 0)
        {
            sb.AppendLine("COVERAGE GAPS");
            sb.AppendLine(new string('-', 30));
            foreach (var gap in report.Gaps.OrderByDescending(g => g.Severity))
            {
                sb.AppendLine($"  [{gap.Severity}] {gap.DocumentPath}");
                sb.AppendLine($"          {gap.Reason}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

/// <summary>
/// Supported report formats.
/// </summary>
public enum ReportFormat
{
    Json,
    Markdown,
    Text
}
