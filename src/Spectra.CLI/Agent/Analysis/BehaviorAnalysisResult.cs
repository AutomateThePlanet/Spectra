using Spectra.Core.Models;

namespace Spectra.CLI.Agent.Analysis;

/// <summary>
/// Complete result of AI-powered documentation analysis for testable behaviors.
/// </summary>
public sealed record BehaviorAnalysisResult
{
    /// <summary>
    /// Total distinct testable behaviors found.
    /// </summary>
    public required int TotalBehaviors { get; init; }

    /// <summary>
    /// Count per behavior category.
    /// </summary>
    public required IReadOnlyDictionary<BehaviorCategory, int> Breakdown { get; init; }

    /// <summary>
    /// Full list of identified behaviors.
    /// </summary>
    public required IReadOnlyList<IdentifiedBehavior> Behaviors { get; init; }

    /// <summary>
    /// Number of behaviors already covered by existing tests.
    /// </summary>
    public required int AlreadyCovered { get; init; }

    /// <summary>
    /// Recommended number of new tests to generate (TotalBehaviors - AlreadyCovered).
    /// </summary>
    public int RecommendedCount => Math.Max(0, TotalBehaviors - AlreadyCovered);

    /// <summary>
    /// Number of source documents analyzed.
    /// </summary>
    public required int DocumentsAnalyzed { get; init; }

    /// <summary>
    /// Approximate total word count of analyzed documentation.
    /// </summary>
    public required int TotalWords { get; init; }

    /// <summary>
    /// Gets the remaining (uncovered) behaviors by category.
    /// </summary>
    public IReadOnlyDictionary<BehaviorCategory, int> GetRemainingByCategory(
        IReadOnlyList<BehaviorCategory>? generatedCategories = null)
    {
        if (generatedCategories is null || generatedCategories.Count == 0)
            return Breakdown;

        var remaining = new Dictionary<BehaviorCategory, int>(Breakdown);
        foreach (var cat in generatedCategories)
        {
            if (remaining.ContainsKey(cat))
                remaining[cat] = 0;
        }

        // Remove zero entries
        return remaining
            .Where(kvp => kvp.Value > 0)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}
