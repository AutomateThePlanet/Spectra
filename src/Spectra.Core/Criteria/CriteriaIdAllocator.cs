using System.Globalization;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Criteria;

/// <summary>
/// Allocates acceptance-criterion IDs from the committed per-prefix high-water mark in the criteria index
/// (Spec 079). A number, once issued for a prefix, is never issued again. The effective mark is the maximum of
/// the stored mark and every ID present in the workspace, so it is never lower than what is on disk.
/// </summary>
/// <remarks>Pure; callers hold the criteria lock and persist the index after allocating.</remarks>
public static class CriteriaIdAllocator
{
    /// <summary>
    /// Splits an ID into its prefix (everything before the last <c>-</c>, upper-cased) and trailing number.
    /// Returns null for IDs that do not end in <c>-{digits}</c>.
    /// </summary>
    public static (string Prefix, int Number)? ParseId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var dash = id.LastIndexOf('-');
        if (dash <= 0 || dash == id.Length - 1)
        {
            return null;
        }

        var digits = id[(dash + 1)..];
        if (!digits.All(char.IsAsciiDigit)
            || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        return (id[..dash].ToUpperInvariant(), number);
    }

    /// <summary>The highest number issued (or present) for <paramref name="prefix"/>.</summary>
    public static int EffectiveMark(CriteriaIndex index, string prefix, IEnumerable<string> existingIds)
    {
        ArgumentNullException.ThrowIfNull(index);
        var key = prefix.ToUpperInvariant();

        var stored = index.IdHighWaterMarks is not null && index.IdHighWaterMarks.TryGetValue(key, out var mark) ? mark : 0;
        var onDisk = existingIds
            .Select(ParseId)
            .Where(p => p is not null && p.Value.Prefix == key)
            .Select(p => p!.Value.Number)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(stored, onDisk);
    }

    /// <summary>
    /// Issues <paramref name="count"/> new IDs for <paramref name="prefix"/> and raises the index's stored mark.
    /// </summary>
    public static IReadOnlyList<string> Allocate(
        CriteriaIndex index, string prefix, int count, IEnumerable<string> existingIds)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var key = prefix.ToUpperInvariant();
        var next = EffectiveMark(index, key, existingIds);
        var issued = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            next++;
            issued.Add($"{key}-{next.ToString("D3", CultureInfo.InvariantCulture)}");
        }

        if (count > 0)
        {
            index.IdHighWaterMarks ??= new Dictionary<string, int>(StringComparer.Ordinal);
            index.IdHighWaterMarks[key] = next;
        }

        return issued;
    }

    /// <summary>
    /// Raises every prefix's stored mark to at least the highest number present in <paramref name="existingIds"/>
    /// (migration seeding). Never lowers a mark. Returns the prefixes whose mark changed, with their new value.
    /// </summary>
    public static IReadOnlyDictionary<string, int> Seed(CriteriaIndex index, IEnumerable<string> existingIds)
    {
        ArgumentNullException.ThrowIfNull(index);
        var changed = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var group in existingIds.Select(ParseId).Where(p => p is not null).GroupBy(p => p!.Value.Prefix))
        {
            var max = group.Max(p => p!.Value.Number);
            var stored = index.IdHighWaterMarks is not null && index.IdHighWaterMarks.TryGetValue(group.Key, out var m) ? m : 0;
            if (max > stored)
            {
                index.IdHighWaterMarks ??= new Dictionary<string, int>(StringComparer.Ordinal);
                index.IdHighWaterMarks[group.Key] = max;
                changed[group.Key] = max;
            }
        }

        return changed;
    }
}
