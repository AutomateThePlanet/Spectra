using Spectra.Core.Models.Coverage;
using Spectra.Core.Text;

namespace Spectra.Core.Criteria;

/// <summary>An extracted criterion plus the existing ID the model asked to keep (null or empty for none).</summary>
public sealed record IncomingCriterion(AcceptanceCriterion Criterion, string? RequestedId);

/// <summary>A matched criterion: <see cref="Previous"/> is the existing criterion whose ID it keeps, or null (new).</summary>
public sealed record MatchedCriterion(AcceptanceCriterion Criterion, AcceptanceCriterion? Previous);

/// <summary>Outcome of <see cref="CriteriaMatcher.Match"/>.</summary>
public sealed record CriteriaMatchResult(
    bool IsValid,
    IReadOnlyList<string> InvalidIds,
    IReadOnlyList<MatchedCriterion> Items);

/// <summary>
/// Matches freshly extracted criteria to a document's previous criteria (Spec 079 FR-004/FR-005). Model-free:
/// the model's returned IDs are validated strictly, then criteria without an ID fall back to a one-to-one match
/// on the <see cref="TextNormalizer"/> key. New IDs are not allocated here.
/// </summary>
public static class CriteriaMatcher
{
    public static CriteriaMatchResult Match(
        IReadOnlyList<IncomingCriterion> incoming, IReadOnlyList<AcceptanceCriterion> previous)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(previous);

        var previousById = previous
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // 1. Validate: a requested ID must belong to this doc and appear at most once.
        var invalid = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in incoming.Select(i => i.RequestedId).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!.Trim()))
        {
            if ((!previousById.ContainsKey(id) || !seen.Add(id))
                && !invalid.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                invalid.Add(id);
            }
        }

        if (invalid.Count > 0)
        {
            return new CriteriaMatchResult(false, invalid, []);
        }

        // 2. Explicit IDs claim their previous criterion first, so the text fallback can never steal them.
        var claimed = new HashSet<AcceptanceCriterion>(ReferenceEqualityComparer.Instance);
        var matches = new AcceptanceCriterion?[incoming.Count];
        for (var i = 0; i < incoming.Count; i++)
        {
            var requested = incoming[i].RequestedId?.Trim();
            if (!string.IsNullOrWhiteSpace(requested))
            {
                matches[i] = previousById[requested];
                claimed.Add(matches[i]!);
            }
        }

        // 3. Text fallback, one-to-one, in response order.
        for (var i = 0; i < incoming.Count; i++)
        {
            if (matches[i] is not null || !string.IsNullOrWhiteSpace(incoming[i].RequestedId))
            {
                continue;
            }

            var key = TextNormalizer.Normalize(incoming[i].Criterion.Text);
            var match = previous.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.Id) && !claimed.Contains(p) && TextNormalizer.Normalize(p.Text) == key);
            if (match is not null)
            {
                matches[i] = match;
                claimed.Add(match);
            }
        }

        // 5. Kept IDs; unchanged text (by key) keeps the previous stored text verbatim.
        var items = new List<MatchedCriterion>(incoming.Count);
        for (var i = 0; i < incoming.Count; i++)
        {
            var criterion = incoming[i].Criterion;
            var prev = matches[i];
            if (prev is not null)
            {
                criterion.Id = prev.Id;
                if (TextNormalizer.Normalize(prev.Text) == TextNormalizer.Normalize(criterion.Text))
                {
                    criterion.Text = prev.Text;
                }
            }
            else
            {
                criterion.Id = string.Empty;
            }

            items.Add(new MatchedCriterion(criterion, prev));
        }

        return new CriteriaMatchResult(true, [], items);
    }
}
