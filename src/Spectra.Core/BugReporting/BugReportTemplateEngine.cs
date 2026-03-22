using System.Text;

namespace Spectra.Core.BugReporting;

/// <summary>
/// Populates bug report templates with context data or composes reports directly.
/// </summary>
public sealed class BugReportTemplateEngine
{
    /// <summary>
    /// Replaces {{variable}} placeholders in template content with values from context.
    /// Unrecognized placeholders are left as-is.
    /// </summary>
    public string PopulateTemplate(string templateContent, BugReportContext context)
    {
        ArgumentNullException.ThrowIfNull(templateContent);
        ArgumentNullException.ThrowIfNull(context);

        var result = templateContent;

        result = result.Replace("{{title}}", context.GenerateTitle());
        result = result.Replace("{{test_id}}", context.TestId);
        result = result.Replace("{{test_title}}", context.TestTitle);
        result = result.Replace("{{suite_name}}", context.SuiteName);
        result = result.Replace("{{environment}}", context.Environment);
        result = result.Replace("{{severity}}", context.Severity);
        result = result.Replace("{{run_id}}", context.RunId);
        result = result.Replace("{{failed_steps}}", context.FailedSteps);
        result = result.Replace("{{expected_result}}", context.ExpectedResult);
        result = result.Replace("{{attachments}}", FormatAttachments(context.Attachments));
        result = result.Replace("{{source_refs}}", FormatList(context.SourceRefs));
        result = result.Replace("{{requirements}}", FormatList(context.Requirements));
        result = result.Replace("{{component}}", context.Component ?? "N/A");

        return result;
    }

    /// <summary>
    /// Composes a bug report directly from context when no template is available.
    /// </summary>
    public string ComposeReport(BugReportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sb = new StringBuilder();

        sb.AppendLine($"## {context.GenerateTitle()}");
        sb.AppendLine();
        sb.AppendLine($"**Test Case:** {context.TestId} - {context.TestTitle}");
        sb.AppendLine($"**Suite:** {context.SuiteName}");
        sb.AppendLine($"**Environment:** {context.Environment}");
        sb.AppendLine($"**Severity:** {context.Severity}");
        sb.AppendLine($"**Execution Run:** {context.RunId}");
        sb.AppendLine();
        sb.AppendLine("### Steps to Reproduce");
        sb.AppendLine();
        sb.AppendLine(context.FailedSteps);
        sb.AppendLine();
        sb.AppendLine("### Expected Result");
        sb.AppendLine();
        sb.AppendLine(context.ExpectedResult);
        sb.AppendLine();
        sb.AppendLine("### Actual Result");
        sb.AppendLine();
        sb.AppendLine("[Describe what actually happened]");
        sb.AppendLine();

        if (context.Attachments.Count > 0)
        {
            sb.AppendLine("### Screenshots");
            sb.AppendLine();
            sb.AppendLine(FormatAttachments(context.Attachments));
            sb.AppendLine();
        }

        sb.AppendLine("### Traceability");
        sb.AppendLine();

        if (context.SourceRefs.Count > 0)
            sb.AppendLine($"- **Source Documentation:** {FormatList(context.SourceRefs)}");
        if (context.Requirements.Count > 0)
            sb.AppendLine($"- **Requirements:** {FormatList(context.Requirements)}");
        if (context.Component is not null)
            sb.AppendLine($"- **Component:** {context.Component}");

        return sb.ToString();
    }

    private static string FormatAttachments(IReadOnlyList<string> attachments)
    {
        if (attachments.Count == 0) return "";

        var sb = new StringBuilder();
        foreach (var path in attachments)
        {
            var fileName = Path.GetFileName(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp")
            {
                sb.AppendLine($"![{fileName}]({path})");
            }
            else
            {
                sb.AppendLine($"- [{fileName}]({path})");
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static string FormatList(IReadOnlyList<string> items)
    {
        return items.Count == 0 ? "" : string.Join(", ", items);
    }
}
