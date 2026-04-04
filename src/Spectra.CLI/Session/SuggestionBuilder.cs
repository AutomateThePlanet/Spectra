using Spectra.CLI.Agent.Analysis;
using Spectra.Core.Models;

namespace Spectra.CLI.Session;

/// <summary>
/// Derives suggestions from behavior analysis results minus already generated tests.
/// </summary>
public static class SuggestionBuilder
{
    /// <summary>
    /// Builds suggestions from analysis result, excluding already-covered behaviors.
    /// </summary>
    public static List<SessionSuggestion> Build(
        BehaviorAnalysisResult analysis,
        int generatedCount)
    {
        var suggestions = new List<SessionSuggestion>();
        var index = 1;

        // Get remaining behaviors by category
        var remaining = analysis.TotalBehaviors - analysis.AlreadyCovered - generatedCount;
        if (remaining <= 0)
            return suggestions;

        // Build suggestions from the behavior breakdown
        foreach (var (category, totalCount) in analysis.Breakdown.OrderByDescending(kvp => kvp.Value))
        {
            if (totalCount <= 0) continue;

            // Estimate how many of this category were already generated
            // Proportional distribution of generated tests across categories
            var categoryRatio = (double)totalCount / analysis.TotalBehaviors;
            var estimatedGenerated = (int)Math.Round(categoryRatio * (analysis.AlreadyCovered + generatedCount));
            var categoryRemaining = Math.Max(0, totalCount - estimatedGenerated);

            if (categoryRemaining <= 0) continue;

            var categoryName = category.ToString();
            var label = categoryName switch
            {
                "HappyPath" => "happy path",
                "Negative" => "error scenario",
                "EdgeCase" => "edge case",
                "Security" => "security check",
                "Performance" => "performance scenario",
                _ => categoryName.ToLowerInvariant()
            };

            // Create up to 3 suggestions per category
            var count = Math.Min(categoryRemaining, 3);
            for (var i = 0; i < count; i++)
            {
                suggestions.Add(new SessionSuggestion
                {
                    Index = index++,
                    Title = $"Additional {label} test case {(count > 1 ? $"({i + 1}/{count})" : "")}".Trim(),
                    Category = categoryName.ToLowerInvariant(),
                    Status = SuggestionStatus.Pending
                });
            }
        }

        return suggestions;
    }
}
