using System.Text.RegularExpressions;

namespace Spectra.CLI.Tests.Golden;

/// <summary>
/// Spec 099 (FR-002): the values that differ between two runs of the same build, replaced by <c>&lt;mask:name&gt;</c>
/// before the golden comparison. Every rule here was shown to vary between two captures on c24d23e (tasks.md Notes);
/// a rule never removes a line, it only replaces a value.
/// </summary>
public static partial class GoldenMasks
{
    /// <summary>An ISO-8601 date-time (with a time part), as JSON, YAML and Markdown write it.</summary>
    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|\\u002B\d{2}:\d{2}|[+-]\d{2}:\d{2})?")]
    private static partial Regex Timestamp();

    /// <summary>
    /// A clock value written out as a YAML object (the docs manifest's <c>generated_at</c>): every indented
    /// <c>key: value</c> line directly under the key.
    /// </summary>
    [GeneratedRegex(@"(?m)^(?<key>generated_at:\r?\n)(?<body>(?:[ \t]+[a-z_]+:[^\r\n]*\r?\n)+)")]
    private static partial Regex ClockObject();

    [GeneratedRegex(@"(?m)^(?<indent>[ \t]+[a-z_]+:)[^\r\n]*")]
    private static partial Regex ClockObjectLine();

    /// <summary>The progress page's local time of day.</summary>
    [GeneratedRegex(@"Last updated: \d{2}:\d{2}:\d{2}")]
    private static partial Regex LocalTime();

    /// <summary>A document's write date in the docs index (the day the run wrote the fixture).</summary>
    [GeneratedRegex(@"\*\*Last Modified:\*\* \d{4}-\d{2}-\d{2}")]
    private static partial Regex LastModified();

    /// <summary>
    /// run-status-file: the progress page and result file reflect the commands run earlier in the same process
    /// (DashboardHandler keeps a static progress manager), so two runs in one test process differ. Listed, not compared
    /// (as <c>VerificationWorkspace.Tree</c> excludes them).
    /// </summary>
    public static bool IsRunStatusFile(string relativePath) =>
        relativePath is ".spectra-progress.html" or ".spectra-result.json";

    public static string Apply(string text, string workspaceRoot)
    {
        // workspace-root: each run uses a fresh temp folder.
        foreach (var form in RootForms(workspaceRoot))
        {
            text = text.Replace(form, "<mask:workspace-root>", StringComparison.OrdinalIgnoreCase);
        }

        // workspace-name: the folder name alone (spectra-084-baseline-<guid>).
        text = text.Replace(Path.GetFileName(workspaceRoot.TrimEnd('\\', '/')), "<mask:workspace-name>", StringComparison.OrdinalIgnoreCase);

        // clock-object first (its date line would otherwise become a plain timestamp mask).
        text = ClockObject().Replace(text, m => m.Groups["key"].Value
            + ClockObjectLine().Replace(m.Groups["body"].Value, l => l.Groups["indent"].Value + " <mask:clock>"));
        text = Timestamp().Replace(text, "<mask:timestamp>");
        text = LocalTime().Replace(text, "Last updated: <mask:local-time>");
        text = LastModified().Replace(text, "**Last Modified:** <mask:file-date>");
        return text;
    }

    private static IEnumerable<string> RootForms(string root)
    {
        var trimmed = root.TrimEnd('\\', '/');
        var forms = new[]
        {
            trimmed.Replace("\\", "\\\\"),   // inside JSON strings
            trimmed,
            trimmed.Replace('\\', '/'),
        };

        // Longest first, so the escaped form is replaced before the plain one is found inside it.
        return forms.Distinct().OrderByDescending(f => f.Length);
    }
}
