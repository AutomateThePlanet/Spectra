using System.Text;

namespace Spectra.CLI.Agent.Analysis;

/// <summary>
/// Formats a <see cref="CoverageSnapshot"/> into a markdown block for injection
/// into the behavior analysis prompt via the <c>{{coverage_context}}</c> placeholder.
/// </summary>
public static class CoverageContextFormatter
{
    private const int TitleMaxLength = 80;
    private const int SummaryModeThreshold = 500;

    /// <summary>
    /// Formats the snapshot as a markdown coverage context block.
    /// Returns empty string when snapshot has no data.
    /// </summary>
    public static string Format(CoverageSnapshot snapshot)
    {
        if (!snapshot.HasData)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## EXISTING COVERAGE — DO NOT DUPLICATE");
        sb.AppendLine();
        sb.AppendLine($"This suite already has {snapshot.ExistingTestCount} test cases.");

        // Criteria coverage section
        if (snapshot.TotalCriteriaCount > 0)
        {
            var coveredCount = snapshot.CoveredCriteriaIds.Count;
            sb.AppendLine();
            sb.AppendLine($"### Acceptance Criteria Coverage ({coveredCount}/{snapshot.TotalCriteriaCount})");

            if (snapshot.Mode == CoverageContextMode.Full && coveredCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Covered criteria (DO NOT generate behaviors for these):");
                foreach (var id in snapshot.CoveredCriteriaIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"- {id}");
                }
            }

            if (snapshot.UncoveredCriteria.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"### Uncovered Acceptance Criteria ({snapshot.UncoveredCriteria.Count})");
                sb.AppendLine("These criteria have ZERO linked tests — PRIORITIZE behaviors for these:");
                foreach (var criterion in snapshot.UncoveredCriteria)
                {
                    var source = criterion.Source is not null ? $" (source: {criterion.Source})" : "";
                    sb.AppendLine($"- {criterion.Id}: {criterion.Text} [{criterion.Priority}]{source}");
                }
            }
        }

        // Source refs coverage section
        if (snapshot.CoveredSourceRefs.Count > 0 || snapshot.UncoveredSourceRefs.Count > 0)
        {
            if (snapshot.CoveredSourceRefs.Count > 0 && snapshot.Mode == CoverageContextMode.Full)
            {
                sb.AppendLine();
                sb.AppendLine($"### Covered Documentation Sections ({snapshot.CoveredSourceRefs.Count})");
                sb.AppendLine("These doc sections already have linked tests:");
                foreach (var sref in snapshot.CoveredSourceRefs.OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"- {sref}");
                }
            }

            if (snapshot.UncoveredSourceRefs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"### Uncovered Documentation Sections ({snapshot.UncoveredSourceRefs.Count})");
                sb.AppendLine("These doc sections have NO linked tests — look for testable behaviors here:");
                foreach (var sref in snapshot.UncoveredSourceRefs)
                {
                    sb.AppendLine($"- {sref}");
                }
            }
        }

        // Test titles for dedup (full mode only)
        if (snapshot.Mode == CoverageContextMode.Full && snapshot.ExistingTestTitles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Existing Test Titles");
            sb.AppendLine("For deduplication — do NOT generate behaviors that overlap with these:");
            foreach (var title in snapshot.ExistingTestTitles)
            {
                var truncated = TruncateTitle(title);
                sb.AppendLine($"- {truncated}");
            }
        }
        else if (snapshot.Mode == CoverageContextMode.Summary && snapshot.ExistingTestCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"This suite has {snapshot.ExistingTestCount} existing tests — full title list omitted to conserve tokens.");
        }

        // Revised analysis instructions
        sb.AppendLine();
        sb.AppendLine("## ANALYSIS INSTRUCTIONS (REVISED)");
        sb.AppendLine();
        sb.AppendLine("Your job is to find ONLY the testable behaviors that are NOT already covered.");
        sb.AppendLine("The recommended count should reflect the ACTUAL GAP, not the total possible behaviors.");
        sb.AppendLine("If coverage is already high, recommending 0–5 new tests is correct.");

        return sb.ToString();
    }

    /// <summary>
    /// Truncates a title to <see cref="TitleMaxLength"/> characters.
    /// </summary>
    internal static string TruncateTitle(string title)
    {
        if (title.Length <= TitleMaxLength)
            return title;

        return title[..(TitleMaxLength - 3)] + "...";
    }
}
