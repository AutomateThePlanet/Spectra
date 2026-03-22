namespace Spectra.Core.BugReporting;

/// <summary>
/// Writes bug reports as local Markdown files with attachment copies.
/// </summary>
public sealed class BugReportWriter
{
    /// <summary>
    /// Saves a bug report to reports/{runId}/bugs/BUG-{testId}.md
    /// and copies attachments to reports/{runId}/bugs/attachments/.
    /// </summary>
    /// <returns>The path to the saved bug report file.</returns>
    public async Task<string> WriteLocalBugReportAsync(
        string reportsPath,
        string runId,
        string testId,
        string content,
        IReadOnlyList<string>? attachmentPaths = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(testId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var bugsDir = Path.Combine(reportsPath, runId, "bugs");
        Directory.CreateDirectory(bugsDir);

        var fileName = GetUniqueFileName(bugsDir, testId);
        var filePath = Path.Combine(bugsDir, fileName);

        await File.WriteAllTextAsync(filePath, content, ct);

        // Copy attachments if any
        if (attachmentPaths is { Count: > 0 })
        {
            var attachmentsDir = Path.Combine(bugsDir, "attachments");
            Directory.CreateDirectory(attachmentsDir);

            foreach (var srcPath in attachmentPaths)
            {
                if (!File.Exists(srcPath)) continue;

                var destPath = Path.Combine(attachmentsDir, Path.GetFileName(srcPath));
                File.Copy(srcPath, destPath, overwrite: true);
            }
        }

        return filePath;
    }

    private static string GetUniqueFileName(string directory, string testId)
    {
        var baseName = $"BUG-{testId}.md";
        if (!File.Exists(Path.Combine(directory, baseName)))
            return baseName;

        for (var i = 2; i < 1000; i++)
        {
            var name = $"BUG-{testId}-{i}.md";
            if (!File.Exists(Path.Combine(directory, name)))
                return name;
        }

        return baseName; // fallback
    }
}
