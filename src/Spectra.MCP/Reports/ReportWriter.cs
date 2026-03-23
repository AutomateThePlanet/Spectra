using System.Globalization;
using System.Text;
using System.Text.Json;
using Spectra.Core.Models.Execution;

namespace Spectra.MCP.Reports;

/// <summary>
/// Writes execution reports to JSON, Markdown, and HTML files.
/// </summary>
public sealed class ReportWriter
{
    private readonly string _reportsPath;

    public string ReportsPath => _reportsPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ReportWriter(string reportsPath)
    {
        _reportsPath = reportsPath;
    }

    /// <summary>
    /// Writes JSON, Markdown, and HTML reports.
    /// </summary>
    public async Task<(string JsonPath, string MarkdownPath, string HtmlPath)> WriteAsync(ExecutionReport report)
    {
        Directory.CreateDirectory(_reportsPath);

        var timestamp = report.CompletedAt.ToUniversalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var author = SanitizeFileName(report.ExecutedBy);
        var suite = SanitizeFileName(report.Suite);
        var baseName = $"{timestamp}_{author}_{suite}";
        var jsonPath = Path.Combine(_reportsPath, $"{baseName}.json");
        var mdPath = Path.Combine(_reportsPath, $"{baseName}.md");
        var htmlPath = Path.Combine(_reportsPath, $"{baseName}.html");

        await WriteJsonAsync(report, jsonPath);
        await WriteMarkdownAsync(report, mdPath);
        await WriteHtmlAsync(report, htmlPath);

        // Copy banner image for HTML report
        CopyBannerAsset(_reportsPath);

        return (jsonPath, mdPath, htmlPath);
    }

    /// <summary>
    /// Writes the JSON report.
    /// </summary>
    public async Task WriteJsonAsync(ExecutionReport report, string path)
    {
        var json = JsonSerializer.Serialize(report, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    /// <summary>
    /// Sanitizes a string for use in filenames by replacing invalid characters.
    /// </summary>
    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "unknown";
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(input.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray());
        return sanitized.Length > 50 ? sanitized[..50] : sanitized;
    }

    /// <summary>
    /// Formats duration in milliseconds to human-readable string.
    /// </summary>
    private static string FormatDuration(long? milliseconds)
    {
        if (!milliseconds.HasValue || milliseconds < 0) return "-";

        var ms = milliseconds.Value;
        if (ms < 1000) return $"{ms}ms";

        var ts = TimeSpan.FromMilliseconds(ms);
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.Seconds}.{ts.Milliseconds / 100}s";
    }

    /// <summary>
    /// Formats the total run duration.
    /// </summary>
    private static string FormatRunDuration(ExecutionReport report)
    {
        // Ensure UTC normalization to avoid timezone issues
        var startUtc = report.StartedAt.ToUniversalTime();
        var endUtc = report.CompletedAt.ToUniversalTime();
        var duration = endUtc - startUtc;

        // Guard against negative durations
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

        if (duration.TotalHours >= 1) return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
        if (duration.TotalMinutes >= 1) return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        if (duration.TotalSeconds >= 1) return $"{duration.Seconds}.{duration.Milliseconds / 100}s";
        return $"{(int)duration.TotalMilliseconds}ms";
    }

    /// <summary>
    /// Writes the Markdown report.
    /// </summary>
    public async Task WriteMarkdownAsync(ExecutionReport report, string path)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# Execution Report: {report.Suite}");
        sb.AppendLine();
        sb.AppendLine($"**Run ID**: `{report.RunId}`");
        sb.AppendLine($"**Status**: {report.Status}");
        sb.AppendLine($"**Environment**: {report.Environment ?? "default"}");
        sb.AppendLine($"**Executed By**: {report.ExecutedBy}");
        sb.AppendLine($"**Started**: {report.StartedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Completed**: {report.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Duration**: {FormatRunDuration(report)}");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Count |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Total | {report.Summary.Total} |");
        sb.AppendLine($"| Passed | {report.Summary.Passed} |");
        sb.AppendLine($"| Failed | {report.Summary.Failed} |");
        sb.AppendLine($"| Skipped | {report.Summary.Skipped} |");
        sb.AppendLine($"| Blocked | {report.Summary.Blocked} |");
        sb.AppendLine($"| Pass Rate | {report.Summary.PassRate}% |");
        sb.AppendLine();

        // Filters if applied
        if (report.Filters?.HasFilters == true)
        {
            sb.AppendLine("## Filters Applied");
            sb.AppendLine();
            if (report.Filters.Priority.HasValue)
                sb.AppendLine($"- **Priority**: {report.Filters.Priority}");
            if (report.Filters.Tags?.Count > 0)
                sb.AppendLine($"- **Tags**: {string.Join(", ", report.Filters.Tags)}");
            if (!string.IsNullOrEmpty(report.Filters.Component))
                sb.AppendLine($"- **Component**: {report.Filters.Component}");
            if (report.Filters.TestIds?.Count > 0)
                sb.AppendLine($"- **Test IDs**: {string.Join(", ", report.Filters.TestIds)}");
            sb.AppendLine();
        }

        // Failed tests section
        var failed = report.Results.Where(r => r.Status == TestStatus.Failed).ToList();
        if (failed.Count > 0)
        {
            sb.AppendLine("## Failed Tests");
            sb.AppendLine();
            foreach (var test in failed)
            {
                sb.AppendLine($"### {test.TestId}: {test.Title}");
                sb.AppendLine();
                sb.AppendLine($"- **Duration**: {FormatDuration(test.DurationMs)}");
                if (!string.IsNullOrEmpty(test.Notes))
                {
                    sb.AppendLine($"- **Reason**: {test.Notes}");
                }
                if (!string.IsNullOrEmpty(test.Preconditions))
                {
                    sb.AppendLine($"- **Preconditions**: {test.Preconditions}");
                }
                if (test.Steps is { Count: > 0 })
                {
                    sb.AppendLine();
                    sb.AppendLine("**Steps:**");
                    for (var i = 0; i < test.Steps.Count; i++)
                    {
                        sb.AppendLine($"{i + 1}. {test.Steps[i]}");
                    }
                }
                if (!string.IsNullOrEmpty(test.ExpectedResult))
                {
                    sb.AppendLine($"- **Expected Result**: {test.ExpectedResult}");
                }
                sb.AppendLine();
            }
        }

        // Skipped tests section
        var skipped = report.Results.Where(r => r.Status == TestStatus.Skipped).ToList();
        if (skipped.Count > 0)
        {
            sb.AppendLine("## Skipped Tests");
            sb.AppendLine();
            sb.AppendLine("| Test ID | Title | Reason |");
            sb.AppendLine("|---------|-------|--------|");
            foreach (var test in skipped)
            {
                var reason = string.IsNullOrEmpty(test.Notes) ? "-" : test.Notes;
                sb.AppendLine($"| {test.TestId} | {test.Title} | {reason} |");
            }
            sb.AppendLine();
        }

        // Blocked tests section
        var blocked = report.Results.Where(r => r.Status == TestStatus.Blocked).ToList();
        if (blocked.Count > 0)
        {
            sb.AppendLine("## Blocked Tests");
            sb.AppendLine();
            sb.AppendLine("| Test ID | Title | Blocked By | Reason |");
            sb.AppendLine("|---------|-------|------------|--------|");
            foreach (var test in blocked)
            {
                var blockedBy = string.IsNullOrEmpty(test.BlockedBy) ? "-" : test.BlockedBy;
                var reason = string.IsNullOrEmpty(test.Notes) ? "-" : test.Notes;
                sb.AppendLine($"| {test.TestId} | {test.Title} | {blockedBy} | {reason} |");
            }
            sb.AppendLine();
        }

        // All results table
        sb.AppendLine("## All Results");
        sb.AppendLine();
        sb.AppendLine("| Test ID | Title | Status | Attempt | Duration |");
        sb.AppendLine("|---------|-------|--------|---------|----------|");
        foreach (var test in report.Results)
        {
            var duration = FormatDuration(test.DurationMs);
            var status = GetStatusText(test.Status);
            sb.AppendLine($"| {test.TestId} | {test.Title} | {status} | {test.Attempt} | {duration} |");
        }
        sb.AppendLine();

        // Footer
        sb.AppendLine("---");
        sb.AppendLine($"*Generated by Spectra MCP Server*");

        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static string GetStatusText(TestStatus status)
    {
        return status switch
        {
            TestStatus.Passed => "PASSED",
            TestStatus.Failed => "FAILED",
            TestStatus.Skipped => "SKIPPED",
            TestStatus.Blocked => "BLOCKED",
            TestStatus.Pending => "PENDING",
            TestStatus.InProgress => "IN_PROGRESS",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// Writes the HTML report with a light, professional design.
    /// </summary>
    public async Task WriteHtmlAsync(ExecutionReport report, string path)
    {
        var passRate = report.Summary.Total > 0
            ? (report.Summary.Passed * 100.0 / report.Summary.Total).ToString("F1", CultureInfo.InvariantCulture)
            : "0";

        var passRateDegrees = double.Parse(passRate, CultureInfo.InvariantCulture) * 3.6;

        var failed = report.Results.Where(r => r.Status == TestStatus.Failed).ToList();
        var blocked = report.Results.Where(r => r.Status == TestStatus.Blocked).ToList();
        var skipped = report.Results.Where(r => r.Status == TestStatus.Skipped).ToList();

        // Failed tests with expandable details
        var failedHtml = failed.Count > 0
            ? string.Join("\n", failed.Select(t => $@"
                <details class=""test-card failed"">
                    <summary>
                        <span class=""test-id"">{Escape(t.TestId)}</span>
                        <span class=""test-title"">{Escape(t.Title)}</span>
                        <span class=""status-badge failed"">FAILED</span>
                    </summary>
                    <div class=""test-details"">
                        <div class=""detail-row""><strong>Duration:</strong> {FormatDuration(t.DurationMs)}</div>
                        {(string.IsNullOrEmpty(t.Notes) ? "" : $@"<div class=""detail-row failure-reason""><strong>Reason:</strong> {Escape(t.Notes)}</div>")}
                        {RenderTestContent(t, _reportsPath)}
                    </div>
                </details>"))
            : "<p class=\"no-items\">No failed tests</p>";

        // Skipped tests with expandable details
        var skippedHtml = skipped.Count > 0
            ? string.Join("\n", skipped.Select(t => $@"
                <details class=""test-card skipped"">
                    <summary>
                        <span class=""test-id"">{Escape(t.TestId)}</span>
                        <span class=""test-title"">{Escape(t.Title)}</span>
                        <span class=""status-badge skipped"">SKIPPED</span>
                    </summary>
                    <div class=""test-details"">
                        {(string.IsNullOrEmpty(t.Notes) ? "<div class=\"detail-row\">No reason provided</div>" : $@"<div class=""detail-row""><strong>Reason:</strong> {Escape(t.Notes)}</div>")}
                        {RenderTestContent(t, _reportsPath)}
                    </div>
                </details>"))
            : "";

        // Blocked tests with expandable details
        var blockedHtml = blocked.Count > 0
            ? string.Join("\n", blocked.Select(t => $@"
                <details class=""test-card blocked"">
                    <summary>
                        <span class=""test-id"">{Escape(t.TestId)}</span>
                        <span class=""test-title"">{Escape(t.Title)}</span>
                        <span class=""status-badge blocked"">BLOCKED</span>
                    </summary>
                    <div class=""test-details"">
                        <div class=""detail-row""><strong>Blocked By:</strong> {Escape(t.BlockedBy ?? "Unknown")}</div>
                        {(string.IsNullOrEmpty(t.Notes) ? "" : $@"<div class=""detail-row""><strong>Reason:</strong> {Escape(t.Notes)}</div>")}
                        {RenderTestContent(t, _reportsPath)}
                    </div>
                </details>"))
            : "";

        var allResultsHtml = string.Join("\n", report.Results.Select(t =>
        {
            var statusClass = t.Status.ToString().ToLowerInvariant();
            var isPassing = t.Status == TestStatus.Passed;

            if (isPassing)
            {
                return $@"
                <tr class=""{statusClass}"">
                    <td class=""test-id"">{Escape(t.TestId)}</td>
                    <td>{Escape(t.Title)}</td>
                    <td><span class=""status-badge {statusClass}"">{t.Status}</span></td>
                    <td>{FormatDuration(t.DurationMs)}</td>
                </tr>";
            }
            else
            {
                // Non-passing tests get expandable rows
                var detailsContent = new StringBuilder();
                detailsContent.Append($"<div class=\"expanded-details\">");
                detailsContent.Append($"<div class=\"detail-item\"><strong>Duration:</strong> {FormatDuration(t.DurationMs)}</div>");

                if (!string.IsNullOrEmpty(t.Notes))
                {
                    detailsContent.Append($"<div class=\"detail-item reason\"><strong>Reason:</strong> {Escape(t.Notes)}</div>");
                }
                if (!string.IsNullOrEmpty(t.BlockedBy))
                {
                    detailsContent.Append($"<div class=\"detail-item\"><strong>Blocked By:</strong> {Escape(t.BlockedBy)}</div>");
                }
                detailsContent.Append(RenderTestContent(t, _reportsPath));
                detailsContent.Append("</div>");

                return $@"
                <tr class=""{statusClass} expandable-row"">
                    <td colspan=""4"">
                        <details class=""table-details"">
                            <summary>
                                <span class=""test-id"">{Escape(t.TestId)}</span>
                                <span class=""test-title-cell"">{Escape(t.Title)}</span>
                                <span class=""status-badge {statusClass}"">{t.Status}</span>
                                <span class=""duration-cell"">{FormatDuration(t.DurationMs)}</span>
                            </summary>
                            {detailsContent}
                        </details>
                    </td>
                </tr>";
            }
        }));

        var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Test Execution Report - {Escape(report.Suite)}</title>
    <link rel=""preconnect"" href=""https://fonts.googleapis.com"">
    <link rel=""preconnect"" href=""https://fonts.gstatic.com"" crossorigin>
    <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap"" rel=""stylesheet"">
    <style>
        :root {{
            --color-navy: #1B2A4A;
            --color-navy-light: #2D3F5E;
            --color-passed: #16a34a;
            --color-passed-bg: #dcfce7;
            --color-failed: #dc2626;
            --color-failed-bg: #fee2e2;
            --color-skipped: #6b7280;
            --color-skipped-bg: #f3f4f6;
            --color-blocked: #d97706;
            --color-blocked-bg: #f3e8ff;
            --color-bg: #F9FAFB;
            --color-card: #ffffff;
            --color-border: #E5E7EB;
            --color-text: #1e293b;
            --color-text-muted: #64748b;
            --color-primary: #3b82f6;
            --color-primary-light: #eff6ff;
        }}
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        body {{
            font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: var(--color-bg);
            color: var(--color-text);
            line-height: 1.6;
            padding: 0;
        }}
        .report-nav {{
            background: linear-gradient(135deg, var(--color-navy) 0%, var(--color-navy-light) 100%);
            padding: 8px 24px;
            display: flex;
            align-items: center;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.15);
        }}
        .report-nav img {{
            height: 42px;
            width: auto;
            object-fit: contain;
        }}
        .container {{ max-width: 1200px; margin: 0 auto; padding: 2rem; }}
        header {{
            text-align: center;
            margin-bottom: 2rem;
            padding: 2rem;
            background: var(--color-card);
            border-radius: 12px;
            border: 1px solid var(--color-border);
            box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
        }}
        h1 {{
            font-size: 2rem;
            font-weight: 700;
            margin-bottom: 0.25rem;
            color: var(--color-text);
        }}
        .suite-name {{ font-size: 1.1rem; color: var(--color-text-muted); margin-bottom: 1.5rem; }}
        .meta-info {{
            display: flex;
            justify-content: center;
            gap: 2rem;
            flex-wrap: wrap;
        }}
        .meta-item {{ color: var(--color-text-muted); font-size: 0.9rem; }}
        .meta-item strong {{ color: var(--color-text); }}

        /* Summary Cards */
        .summary-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
            gap: 1rem;
            margin-bottom: 2rem;
        }}
        .summary-card {{
            background: var(--color-card);
            border-radius: 12px;
            padding: 1.25rem;
            text-align: center;
            border: 1px solid var(--color-border);
            box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
            border-left: 4px solid transparent;
        }}
        .summary-card.passed {{ border-left-color: var(--color-passed); }}
        .summary-card.failed {{ border-left-color: var(--color-failed); }}
        .summary-card.skipped {{ border-left-color: var(--color-skipped); }}
        .summary-card.blocked {{ border-left-color: var(--color-blocked); }}
        .summary-card.total {{ border-left-color: var(--color-primary); }}
        .summary-number {{
            font-size: 2.5rem;
            font-weight: 700;
            line-height: 1;
        }}
        .summary-card.passed .summary-number {{ color: var(--color-passed); }}
        .summary-card.failed .summary-number {{ color: var(--color-failed); }}
        .summary-card.skipped .summary-number {{ color: var(--color-skipped); }}
        .summary-card.blocked .summary-number {{ color: var(--color-blocked); }}
        .summary-card.total .summary-number {{ color: var(--color-primary); }}
        .summary-label {{ color: var(--color-text-muted); margin-top: 0.5rem; text-transform: uppercase; font-size: 0.75rem; letter-spacing: 0.05em; font-weight: 500; }}

        /* Pass Rate Circle */
        .pass-rate-container {{
            display: flex;
            justify-content: center;
            margin-bottom: 2rem;
        }}
        .pass-rate-circle {{
            width: 180px;
            height: 180px;
            border-radius: 50%;
            background: conic-gradient(
                var(--color-passed) 0deg {passRateDegrees.ToString("F1", CultureInfo.InvariantCulture)}deg,
                var(--color-failed-bg) {passRateDegrees.ToString("F1", CultureInfo.InvariantCulture)}deg 360deg
            );
            display: flex;
            align-items: center;
            justify-content: center;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
        }}
        .pass-rate-inner {{
            width: 140px;
            height: 140px;
            border-radius: 50%;
            background: var(--color-card);
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
        }}
        .pass-rate-value {{ font-size: 2.5rem; font-weight: 700; color: var(--color-passed); }}
        .pass-rate-label {{ color: var(--color-text-muted); font-size: 0.85rem; }}

        /* Sections */
        section {{
            margin-bottom: 2rem;
            background: var(--color-card);
            border-radius: 12px;
            padding: 1.5rem;
            border: 1px solid var(--color-border);
            box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
        }}
        h2 {{
            font-size: 1.25rem;
            margin-bottom: 1rem;
            color: var(--color-text);
            display: flex;
            align-items: center;
            gap: 0.5rem;
        }}
        h2 .count {{
            font-size: 0.9rem;
            color: var(--color-text-muted);
            font-weight: 400;
        }}

        /* Test Cards (Expandable) */
        .test-card {{
            border-radius: 8px;
            margin-bottom: 0.75rem;
            border: 1px solid var(--color-border);
            overflow: hidden;
        }}
        .test-card summary {{
            padding: 1rem;
            cursor: pointer;
            display: flex;
            align-items: center;
            gap: 1rem;
            list-style: none;
            user-select: none;
        }}
        .test-card summary::-webkit-details-marker {{ display: none; }}
        .test-card summary::before {{
            content: '\25B6';
            font-size: 0.7rem;
            color: var(--color-text-muted);
            transition: transform 0.2s;
        }}
        .test-card[open] summary::before {{ transform: rotate(90deg); }}
        .test-card.failed {{ background: var(--color-failed-bg); border-color: var(--color-failed); }}
        .test-card.skipped {{ background: var(--color-skipped-bg); border-color: var(--color-skipped); }}
        .test-card.blocked {{ background: var(--color-blocked-bg); border-color: var(--color-blocked); }}
        .test-id {{ font-family: 'JetBrains Mono', 'Fira Code', monospace; color: var(--color-text-muted); font-size: 0.85rem; }}
        .test-title {{ flex: 1; font-weight: 500; }}
        .test-details {{
            padding: 1rem;
            padding-top: 0;
            background: rgba(255, 255, 255, 0.5);
        }}
        .detail-row {{
            padding: 0.5rem 0;
            font-size: 0.9rem;
            border-top: 1px solid rgba(0, 0, 0, 0.05);
        }}
        .detail-row:first-child {{ border-top: none; }}
        .failure-reason {{
            color: var(--color-failed);
            background: rgba(220, 38, 38, 0.05);
            padding: 0.75rem;
            border-radius: 6px;
            margin-top: 0.5rem;
        }}
        .no-items {{ color: var(--color-text-muted); font-style: italic; padding: 1rem 0; }}

        /* Status Badges */
        .status-badge {{
            padding: 0.25rem 0.75rem;
            border-radius: 9999px;
            font-size: 0.7rem;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.025em;
            display: inline-block;
            min-width: 70px;
            text-align: center;
        }}
        .status-badge.passed {{ background: var(--color-passed-bg); color: var(--color-passed); }}
        .status-badge.failed {{ background: var(--color-failed-bg); color: var(--color-failed); }}
        .status-badge.skipped {{ background: var(--color-skipped-bg); color: var(--color-skipped); }}
        .status-badge.blocked {{ background: #f3e8ff; color: #6b21a8; }}

        /* Results Table */
        .table-container {{ overflow-x: auto; }}
        table {{ width: 100%; border-collapse: collapse; font-size: 0.9rem; table-layout: fixed; }}
        th, td {{ padding: 0.875rem 1rem; text-align: left; border-bottom: 1px solid var(--color-border); overflow: hidden; text-overflow: ellipsis; }}
        th {{ color: var(--color-text-muted); font-weight: 600; font-size: 11px; text-transform: uppercase; letter-spacing: 0.05em; background: var(--color-bg); }}
        th:nth-child(1), td:nth-child(1) {{ width: 100px; }}
        th:nth-child(3), td:nth-child(3) {{ width: 100px; text-align: center; }}
        th:nth-child(4), td:nth-child(4) {{ width: 80px; text-align: right; }}
        tr:hover {{ background: var(--color-primary-light); }}
        tr.failed {{ background: rgba(254, 226, 226, 0.3); }}
        tr.blocked {{ background: rgba(254, 243, 199, 0.3); }}
        tr.skipped {{ background: rgba(243, 244, 246, 0.3); }}

        /* Expandable table rows */
        .expandable-row td {{ padding: 0; }}
        .table-details {{ width: 100%; }}
        .table-details summary {{
            padding: 0.875rem 1rem;
            display: grid;
            grid-template-columns: 100px 1fr 100px 80px;
            gap: 1rem;
            align-items: center;
            cursor: pointer;
            list-style: none;
        }}
        .table-details summary::-webkit-details-marker {{ display: none; }}
        .table-details summary::before {{
            content: '\25B6';
            font-size: 0.6rem;
            color: var(--color-text-muted);
            position: absolute;
            left: 0.5rem;
            transition: transform 0.2s;
        }}
        .table-details[open] summary::before {{ transform: rotate(90deg); }}
        .table-details summary {{ position: relative; padding-left: 1.5rem; }}
        .test-title-cell {{ flex: 1; }}
        .duration-cell {{ text-align: right; color: var(--color-text-muted); }}
        .expanded-details {{
            padding: 1rem 1.5rem;
            background: rgba(0, 0, 0, 0.02);
            border-top: 1px solid var(--color-border);
        }}
        .detail-item {{
            padding: 0.5rem 0;
            font-size: 0.9rem;
        }}
        .detail-item.reason {{
            background: var(--color-failed-bg);
            padding: 0.75rem;
            border-radius: 6px;
            margin-top: 0.5rem;
            color: var(--color-failed);
        }}
        .expandable-row.skipped .detail-item.reason {{
            background: var(--color-skipped-bg);
            color: var(--color-skipped);
        }}
        .expandable-row.blocked .detail-item.reason {{
            background: var(--color-blocked-bg);
            color: var(--color-blocked);
        }}

        /* Test Content */
        .test-content {{ margin-top: 1rem; padding-top: 1rem; border-top: 1px solid var(--color-border); }}
        .test-content h4 {{ font-size: 0.85rem; color: var(--color-text-muted); text-transform: uppercase; letter-spacing: 0.05em; margin: 0.75rem 0 0.25rem; }}
        .test-content h4:first-child {{ margin-top: 0; }}
        .test-steps {{ list-style: none; counter-reset: step; padding: 0; }}
        .test-steps li {{ counter-increment: step; padding: 0.4rem 0 0.4rem 2rem; position: relative; font-size: 0.9rem; }}
        .test-steps li::before {{ content: counter(step); position: absolute; left: 0; width: 1.5rem; height: 1.5rem; border-radius: 50%; background: var(--color-primary-light); color: var(--color-primary); font-size: 0.75rem; font-weight: 600; display: flex; align-items: center; justify-content: center; }}
        .test-data {{ background: var(--color-bg); padding: 0.75rem; border-radius: 6px; font-family: monospace; font-size: 0.85rem; white-space: pre-wrap; }}
        .screenshot-gallery {{ display: flex; gap: 0.75rem; flex-wrap: wrap; margin-top: 0.5rem; }}
        .screenshot-thumb {{ width: 160px; height: 100px; object-fit: cover; border-radius: 6px; border: 1px solid var(--color-border); cursor: pointer; }}
        .screenshot-thumb:hover {{ box-shadow: 0 2px 8px rgba(0,0,0,0.15); }}
        .screenshot-caption {{ font-size: 0.8rem; color: var(--color-text-muted); margin-top: 0.25rem; font-style: italic; }}

        footer {{
            text-align: center;
            padding: 2rem;
            color: var(--color-text-muted);
            font-size: 0.85rem;
        }}
        footer a {{ color: var(--color-primary); text-decoration: none; }}
        footer a:hover {{ text-decoration: underline; }}

        @media (max-width: 768px) {{
            body {{ padding: 1rem; }}
            .meta-info {{ gap: 1rem; }}
            .summary-grid {{ grid-template-columns: repeat(2, 1fr); }}
        }}
    </style>
</head>
<body>
    <nav class=""report-nav"">
        <img src=""assets/spectra_dashboard_banner_2.png"" alt=""SPECTRA"">
    </nav>
    <div class=""container"">
        <header>
            <h1>Test Execution Report</h1>
            <div class=""suite-name"">{Escape(report.Suite)}</div>
            <div class=""meta-info"">
                <div class=""meta-item""><strong>Run ID:</strong> {Escape(report.RunId.Length > 8 ? report.RunId[..8] + "..." : report.RunId)}</div>
                <div class=""meta-item""><strong>Executed By:</strong> {Escape(report.ExecutedBy)}</div>
                <div class=""meta-item""><strong>Duration:</strong> {FormatRunDuration(report)}</div>
                <div class=""meta-item""><strong>Completed:</strong> {report.CompletedAt:MMM dd, yyyy HH:mm}</div>
            </div>
        </header>

        <div class=""pass-rate-container"">
            <div class=""pass-rate-circle"">
                <div class=""pass-rate-inner"">
                    <div class=""pass-rate-value"">{passRate}%</div>
                    <div class=""pass-rate-label"">Pass Rate</div>
                </div>
            </div>
        </div>

        <div class=""summary-grid"">
            <div class=""summary-card total"">
                <div class=""summary-number"">{report.Summary.Total}</div>
                <div class=""summary-label"">Total Tests</div>
            </div>
            <div class=""summary-card passed"">
                <div class=""summary-number"">{report.Summary.Passed}</div>
                <div class=""summary-label"">Passed</div>
            </div>
            <div class=""summary-card failed"">
                <div class=""summary-number"">{report.Summary.Failed}</div>
                <div class=""summary-label"">Failed</div>
            </div>
            <div class=""summary-card skipped"">
                <div class=""summary-number"">{report.Summary.Skipped}</div>
                <div class=""summary-label"">Skipped</div>
            </div>
            <div class=""summary-card blocked"">
                <div class=""summary-number"">{report.Summary.Blocked}</div>
                <div class=""summary-label"">Blocked</div>
            </div>
        </div>

        {(failed.Count > 0 ? $@"
        <section>
            <h2>Failed Tests <span class=""count"">({failed.Count})</span></h2>
            {failedHtml}
        </section>" : "")}

        {(skipped.Count > 0 ? $@"
        <section>
            <h2>Skipped Tests <span class=""count"">({skipped.Count})</span></h2>
            {skippedHtml}
        </section>" : "")}

        {(blocked.Count > 0 ? $@"
        <section>
            <h2>Blocked Tests <span class=""count"">({blocked.Count})</span></h2>
            {blockedHtml}
        </section>" : "")}

        <section>
            <h2>All Test Results <span class=""count"">({report.Summary.Total})</span></h2>
            <div class=""table-container"">
                <table>
                    <thead>
                        <tr>
                            <th>Test ID</th>
                            <th>Title</th>
                            <th>Status</th>
                            <th>Duration</th>
                        </tr>
                    </thead>
                    <tbody>
                        {allResultsHtml}
                    </tbody>
                </table>
            </div>
        </section>

        <footer>
            Generated by <a href=""https://github.com/anthropics/spectra"">SPECTRA</a> MCP Server
        </footer>
    </div>
</body>
</html>";

        await File.WriteAllTextAsync(path, html);
    }

    /// <summary>
    /// Renders test case content (preconditions, steps, expected result, test data, screenshots) as HTML.
    /// </summary>
    private string RenderTestContent(TestResultEntry t, string reportsPath)
    {
        var hasContent = !string.IsNullOrEmpty(t.Preconditions)
            || t.Steps is { Count: > 0 }
            || !string.IsNullOrEmpty(t.ExpectedResult)
            || !string.IsNullOrEmpty(t.TestData)
            || t.ScreenshotPaths is { Count: > 0 };

        if (!hasContent) return "";

        var sb = new StringBuilder();
        sb.Append("<div class=\"test-content\">");

        if (!string.IsNullOrEmpty(t.Preconditions))
        {
            sb.Append($"<h4>Preconditions</h4><div class=\"detail-row\">{Escape(t.Preconditions)}</div>");
        }

        if (t.Steps is { Count: > 0 })
        {
            sb.Append("<h4>Steps</h4><ol class=\"test-steps\">");
            foreach (var step in t.Steps)
            {
                sb.Append($"<li>{Escape(step)}</li>");
            }
            sb.Append("</ol>");
        }

        if (!string.IsNullOrEmpty(t.ExpectedResult))
        {
            sb.Append($"<h4>Expected Result</h4><div class=\"detail-row\">{Escape(t.ExpectedResult)}</div>");
        }

        if (!string.IsNullOrEmpty(t.TestData))
        {
            sb.Append($"<h4>Test Data</h4><div class=\"test-data\">{Escape(t.TestData)}</div>");
        }

        if (t.ScreenshotPaths is { Count: > 0 })
        {
            sb.Append("<h4>Screenshots</h4><div class=\"screenshot-gallery\">");
            foreach (var screenshotPath in t.ScreenshotPaths)
            {
                var fullPath = Path.Combine(reportsPath, screenshotPath);
                if (File.Exists(fullPath))
                {
                    var bytes = File.ReadAllBytes(fullPath);
                    var base64 = Convert.ToBase64String(bytes);
                    var ext = Path.GetExtension(fullPath).ToLowerInvariant();
                    var mime = ext switch
                    {
                        ".webp" => "image/webp",
                        ".png" => "image/png",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".gif" => "image/gif",
                        _ => "image/png"
                    };

                    // Extract caption from notes if available
                    var caption = "";
                    if (!string.IsNullOrEmpty(t.Notes))
                    {
                        var marker = $"[Screenshot: {screenshotPath}] ";
                        var idx = t.Notes.IndexOf(marker, StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            var captionStart = idx + marker.Length;
                            var captionEnd = t.Notes.IndexOf('\n', captionStart);
                            caption = captionEnd >= 0
                                ? t.Notes[captionStart..captionEnd].Trim()
                                : t.Notes[captionStart..].Trim();
                        }
                    }

                    sb.Append($"<div><img class=\"screenshot-thumb\" src=\"data:{mime};base64,{base64}\" alt=\"Screenshot\" loading=\"lazy\" onclick=\"window.open(this.src)\" />");
                    if (!string.IsNullOrEmpty(caption))
                    {
                        sb.Append($"<div class=\"screenshot-caption\">{Escape(caption)}</div>");
                    }
                    sb.Append("</div>");
                }
                else
                {
                    sb.Append($"<div class=\"detail-row\">Screenshot not found: {Escape(screenshotPath)}</div>");
                }
            }
            sb.Append("</div>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return System.Net.WebUtility.HtmlEncode(text);
    }

    /// <summary>
    /// Copies the SPECTRA banner image to the report output's assets/ directory.
    /// </summary>
    private static void CopyBannerAsset(string reportsPath)
    {
        // Look for banner in dashboard-site or assets directory
        var current = Environment.CurrentDirectory;
        string[] searchPaths =
        [
            Path.Combine(current, "dashboard-site", "spectra_dashboard_banner_2.png"),
            Path.Combine(current, "assets", "spectra_dashboard_banner_2.png")
        ];

        string? bannerSource = null;
        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                bannerSource = path;
                break;
            }
        }

        if (bannerSource is null) return;

        var assetsDir = Path.Combine(reportsPath, "assets");
        Directory.CreateDirectory(assetsDir);
        File.Copy(bannerSource, Path.Combine(assetsDir, "spectra_dashboard_banner_2.png"), overwrite: true);
    }
}
