using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Output;

public class TerminologyAuditTests
{
    /// <summary>
    /// Ensures SKILL content uses "acceptance criteria" (not "requirements") in user-facing text.
    /// Excludes: directory paths (docs/requirements/), hidden CLI alias (--extract-requirements).
    /// </summary>
    [Fact]
    public void SkillContent_DoesNotContain_OldRequirementTerminology()
    {
        var allowedExceptions = new[]
        {
            "docs/requirements/",        // Directory path, not terminology
            "--extract-requirements",     // Hidden backward-compat alias
        };

        foreach (var (name, content) in SkillContent.All)
        {
            var lines = content.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Skip if line contains an allowed exception
                if (allowedExceptions.Any(ex => line.Contains(ex, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Check for old terminology
                Assert.DoesNotContain("requirement coverage", line, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("requirements coverage", line, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("extracting requirements", line, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Ensures agent content uses "acceptance criteria" (not "requirements") in user-facing text.
    /// </summary>
    [Fact]
    public void AgentContent_DoesNotContain_OldRequirementTerminology()
    {
        var allowedExceptions = new[]
        {
            "docs/requirements/",
            "--extract-requirements",
        };

        foreach (var (name, content) in AgentContent.All)
        {
            var lines = content.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (allowedExceptions.Any(ex => line.Contains(ex, StringComparison.OrdinalIgnoreCase)))
                    continue;

                Assert.DoesNotContain("requirement coverage", line, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("requirements coverage", line, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("extracting requirements", line, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Verifies the AnalyzeCoverageResult uses acceptanceCriteria (not requirements) as field name.
    /// </summary>
    [Fact]
    public void AnalyzeCoverageResult_UsesAcceptanceCriteriaFieldName()
    {
        var result = new CLI.Results.AnalyzeCoverageResult
        {
            Command = "analyze-coverage",
            Status = "completed",
            AcceptanceCriteria = new CLI.Results.CoverageSection { Percentage = 50, Covered = 5, Total = 10 }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        });

        Assert.Contains("\"acceptanceCriteria\"", json);
        Assert.DoesNotContain("\"requirements\"", json);
    }
}
