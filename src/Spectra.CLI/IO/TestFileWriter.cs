using System.Text;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.IO;

/// <summary>
/// Writes test case files in Markdown format.
/// </summary>
public sealed class TestFileWriter
{
    /// <summary>
    /// Writes a test case to a file.
    /// </summary>
    public async Task WriteAsync(string path, TestCase testCase, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(testCase);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = FormatTestCase(testCase);
        await File.WriteAllTextAsync(path, content, ct);
    }

    /// <summary>
    /// Formats a test case as Markdown with YAML frontmatter.
    /// </summary>
    public string FormatTestCase(TestCase testCase)
    {
        var sb = new StringBuilder();

        // YAML frontmatter
        sb.AppendLine("---");
        sb.AppendLine($"id: {testCase.Id}");
        sb.AppendLine($"priority: {testCase.Priority.ToString().ToLowerInvariant()}");

        if (testCase.Tags.Count > 0)
        {
            sb.AppendLine("tags:");
            foreach (var tag in testCase.Tags)
            {
                sb.AppendLine($"  - {tag}");
            }
        }

        if (!string.IsNullOrWhiteSpace(testCase.Component))
        {
            sb.AppendLine($"component: {testCase.Component}");
        }

        if (!string.IsNullOrWhiteSpace(testCase.DependsOn))
        {
            sb.AppendLine($"depends_on: {testCase.DependsOn}");
        }

        if (testCase.SourceRefs.Count > 0)
        {
            sb.AppendLine("source_refs:");
            foreach (var sourceRef in testCase.SourceRefs)
            {
                sb.AppendLine($"  - {sourceRef}");
            }
        }

        if (!string.IsNullOrWhiteSpace(testCase.ScenarioFromDoc))
        {
            sb.AppendLine($"scenario_from_doc: \"{EscapeYamlString(testCase.ScenarioFromDoc)}\"");
        }

        if (testCase.EstimatedDuration.HasValue)
        {
            var duration = testCase.EstimatedDuration.Value;
            sb.AppendLine($"estimated_duration: {FormatDuration(duration)}");
        }

        if (testCase.Criteria.Count > 0)
        {
            sb.AppendLine("criteria:");
            foreach (var criterion in testCase.Criteria)
            {
                sb.AppendLine($"  - {criterion}");
            }
        }
        else
        {
            sb.AppendLine("criteria: []");
        }

        // Orphaned status fields
        if (!string.IsNullOrWhiteSpace(testCase.Status))
        {
            sb.AppendLine($"status: {testCase.Status}");
        }

        if (!string.IsNullOrWhiteSpace(testCase.OrphanedReason))
        {
            sb.AppendLine($"orphaned_reason: \"{EscapeYamlString(testCase.OrphanedReason)}\"");
        }

        if (testCase.OrphanedDate.HasValue)
        {
            sb.AppendLine($"orphaned_date: {testCase.OrphanedDate.Value:yyyy-MM-dd}");
        }

        // Grounding metadata (if verified)
        if (testCase.Grounding is not null)
        {
            sb.AppendLine("grounding:");
            sb.AppendLine($"  verdict: {testCase.Grounding.Verdict.ToString().ToLowerInvariant()}");
            sb.AppendLine($"  score: {testCase.Grounding.Score:F2}");
            sb.AppendLine($"  generator: {testCase.Grounding.Generator}");
            sb.AppendLine($"  critic: {testCase.Grounding.Critic}");
            sb.AppendLine($"  verified_at: {testCase.Grounding.VerifiedAt:yyyy-MM-ddTHH:mm:ssZ}");

            if (testCase.Grounding.UnverifiedClaims.Count > 0)
            {
                sb.AppendLine("  unverified_claims:");
                foreach (var claim in testCase.Grounding.UnverifiedClaims)
                {
                    sb.AppendLine($"    - \"{EscapeYamlString(claim)}\"");
                }
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();

        // Title
        sb.AppendLine($"# {testCase.Title}");
        sb.AppendLine();

        // Preconditions (if any)
        if (!string.IsNullOrWhiteSpace(testCase.Preconditions))
        {
            sb.AppendLine("## Preconditions");
            sb.AppendLine();
            sb.AppendLine(testCase.Preconditions);
            sb.AppendLine();
        }

        // Steps
        sb.AppendLine("## Steps");
        sb.AppendLine();
        for (var i = 0; i < testCase.Steps.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {testCase.Steps[i]}");
        }
        sb.AppendLine();

        // Expected Result
        sb.AppendLine("## Expected Result");
        sb.AppendLine();
        sb.AppendLine(testCase.ExpectedResult);

        // Test Data (if any)
        if (!string.IsNullOrWhiteSpace(testCase.TestData))
        {
            sb.AppendLine();
            sb.AppendLine("## Test Data");
            sb.AppendLine();
            sb.AppendLine(testCase.TestData);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the standard file path for a test case.
    /// </summary>
    public static string GetFilePath(string testsDir, string suite, string testId)
    {
        return Path.Combine(testsDir, suite, $"{testId}.md");
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h{duration.Minutes}m";
        }
        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m";
        }
        return $"{(int)duration.TotalSeconds}s";
    }

    private static string EscapeYamlString(string value)
    {
        return value.Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
