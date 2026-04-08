using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Parsing;

/// <summary>
/// Merges imported criteria with existing criteria. Supports merge and replace modes.
/// </summary>
public sealed class CriteriaMerger
{
    /// <summary>
    /// Result of a merge operation.
    /// </summary>
    public sealed record MergeResult
    {
        public required IReadOnlyList<AcceptanceCriterion> Criteria { get; init; }
        public int MergedCount { get; init; }
        public int NewCount { get; init; }
        public int ReplacedCount { get; init; }
    }

    /// <summary>
    /// Merges new criteria into existing, matching by ID first, then by Source.
    /// </summary>
    public MergeResult Merge(
        IReadOnlyList<AcceptanceCriterion> existing,
        IReadOnlyList<AcceptanceCriterion> incoming)
    {
        var result = new List<AcceptanceCriterion>(existing);
        var merged = 0;
        var added = 0;

        foreach (var item in incoming)
        {
            var matchIdx = FindMatch(result, item);
            if (matchIdx >= 0)
            {
                // Update existing, preserving the original ID
                var originalId = result[matchIdx].Id;
                var updated = new AcceptanceCriterion
                {
                    Id = originalId,
                    Text = item.Text,
                    Rfc2119 = item.Rfc2119,
                    Source = item.Source,
                    SourceType = item.SourceType,
                    SourceDoc = item.SourceDoc,
                    SourceSection = item.SourceSection,
                    Component = item.Component,
                    Priority = item.Priority,
                    Tags = item.Tags,
                    LinkedTestIds = item.LinkedTestIds
                };
                result[matchIdx] = updated;
                merged++;
            }
            else
            {
                result.Add(item);
                added++;
            }
        }

        return new MergeResult
        {
            Criteria = result,
            MergedCount = merged,
            NewCount = added
        };
    }

    /// <summary>
    /// Replaces all existing criteria with incoming. Returns the incoming list.
    /// </summary>
    public MergeResult Replace(
        IReadOnlyList<AcceptanceCriterion> existing,
        IReadOnlyList<AcceptanceCriterion> incoming)
    {
        return new MergeResult
        {
            Criteria = incoming.ToList(),
            MergedCount = 0,
            NewCount = incoming.Count,
            ReplacedCount = existing.Count
        };
    }

    private static int FindMatch(List<AcceptanceCriterion> list, AcceptanceCriterion item)
    {
        // Match by ID first
        if (!string.IsNullOrEmpty(item.Id))
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Id, item.Id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        // Match by source
        if (!string.IsNullOrEmpty(item.Source))
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Source, item.Source, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        return -1;
    }
}
