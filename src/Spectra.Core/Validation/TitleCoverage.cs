using System.Globalization;

namespace Spectra.Core.Validation;

/// <summary>Spec 095: the existing test that covers a behaviour, and how similar their titles are.</summary>
public sealed record CoverageMatch(string TestId, double Similarity);

/// <summary>
/// Spec 095 (FR-001, FR-005; research R1, R2): the one per-behaviour coverage rule. A behaviour is covered when an
/// existing test's title is at least <c>threshold</c> similar to the behaviour's title — the same measure as
/// <see cref="DuplicateDetector.CalculateTitleSimilarity"/> (<see cref="DuplicateDetector.NormalizeTitle"/>, then
/// <c>1 − Levenshtein / max length</c>). The match is the most similar test; equal similarities go to the lowest id
/// (ordinal). A title that is empty after normalisation never covers and is never covered. Titles are normalised once;
/// pairs whose length bound can't reach the threshold (or beat the current best) are skipped, and the distance stops
/// as soon as it can no longer qualify — so the result equals the plain measure, only faster.
/// </summary>
public static class TitleCoverage
{
    public static IReadOnlyList<CoverageMatch?> Match(
        IReadOnlyList<string> behaviourTitles,
        IReadOnlyList<(string Id, string Title)> tests,
        double threshold)
    {
        ArgumentNullException.ThrowIfNull(behaviourTitles);
        ArgumentNullException.ThrowIfNull(tests);

        var normalisedTests = tests
            .Select(t => (t.Id, Title: DuplicateDetector.NormalizeTitle(t.Title ?? string.Empty)))
            .Where(t => t.Title.Length > 0)
            .OrderBy(t => t.Id, StringComparer.Ordinal)
            .Select(t => (t.Id, t.Title, Counts: CharCounts(t.Title)))
            .ToList();

        var results = new CoverageMatch?[behaviourTitles.Count];
        if (normalisedTests.Count == 0)
        {
            return results;
        }

        var previous = new int[0];
        var current = new int[0];
        for (var b = 0; b < behaviourTitles.Count; b++)
        {
            var behaviour = DuplicateDetector.NormalizeTitle(behaviourTitles[b] ?? string.Empty);
            if (behaviour.Length == 0)
            {
                continue;
            }

            var pattern = behaviour.Length <= 64 ? new BitPattern(behaviour) : null;
            var behaviourCounts = CharCounts(behaviour);

            string? bestId = null;
            var best = double.NegativeInfinity;
            foreach (var (id, title, counts) in normalisedTests)
            {
                // Ordered by id, so a later test only wins with a strictly higher similarity.
                var floor = Math.Max(threshold, best);
                var maxLength = Math.Max(behaviour.Length, title.Length);
                var bound = (double)Math.Min(behaviour.Length, title.Length) / maxLength;
                if (bound < floor || (bestId is not null && bound <= best))
                {
                    continue;
                }

                double similarity;
                if (string.Equals(behaviour, title, StringComparison.Ordinal))
                {
                    similarity = 1.0;
                }
                else
                {
                    // The largest distance that still reaches the floor (and, with a best, beats it).
                    var limit = (int)Math.Floor((1.0 - floor) * maxLength + 1e-9);
                    if (BagDistance(behaviourCounts, counts) > limit)
                    {
                        continue;
                    }

                    var distance = pattern is not null
                        ? pattern.Distance(title)
                        : BoundedDistance(behaviour, title, limit, ref previous, ref current);
                    if (distance > limit)
                    {
                        continue;
                    }

                    similarity = 1.0 - ((double)distance / maxLength);
                }

                if (similarity >= threshold && (bestId is null || similarity > best))
                {
                    best = similarity;
                    bestId = id;
                }
            }

            if (bestId is not null)
            {
                results[b] = new CoverageMatch(bestId, best);
            }
        }

        return results;
    }

    private const int Buckets = 48;

    /// <summary>Character counts in <see cref="Buckets"/> buckets (letters, digits, space, the rest folded).</summary>
    private static int[] CharCounts(string text)
    {
        var counts = new int[Buckets];
        foreach (var c in text)
        {
            counts[Bucket(c)]++;
        }

        return counts;
    }

    private static int Bucket(char c) => c switch
    {
        >= 'a' and <= 'z' => c - 'a',
        >= '0' and <= '9' => 26 + (c - '0'),
        ' ' => 36,
        _ => 37 + (c % 11),
    };

    /// <summary>
    /// A lower bound of the Levenshtein distance: each edit changes the character multiset by at most one character on
    /// each side, so the distance is at least the larger one-sided count difference (merged buckets only lower it).
    /// </summary>
    private static int BagDistance(int[] a, int[] b)
    {
        int more = 0, less = 0;
        for (var i = 0; i < Buckets; i++)
        {
            var d = a[i] - b[i];
            if (d > 0)
            {
                more += d;
            }
            else
            {
                less -= d;
            }
        }

        return Math.Max(more, less);
    }

    /// <summary>
    /// Analyze L1: the warning for a coverage threshold outside (0, 1] (used as given), or null. Spec 098 (F9):
    /// <paramref name="key"/> names the config key the value came from.
    /// </summary>
    public static string? ThresholdWarning(double threshold, string key = "generation.coverage_threshold") =>
        threshold is > 0 and <= 1
            ? null
            : $"{key} = {threshold.ToString(CultureInfo.InvariantCulture)} is outside (0, 1]; "
              + "every behaviour (≤ 0) or none (> 1) is treated as covered.";

    /// <summary>
    /// Myers' bit-parallel edit distance (Hyyrö's global form) for a pattern of at most 64 characters: one 64-bit word per
    /// text character. Exactly the Levenshtein distance.
    /// </summary>
    private sealed class BitPattern
    {
        private readonly ulong[] _ascii = new ulong[128];
        private readonly Dictionary<char, ulong> _other = [];
        private readonly ulong _last;
        private readonly int _length;

        public BitPattern(string pattern)
        {
            _length = pattern.Length;
            _last = 1UL << (_length - 1);
            for (var i = 0; i < pattern.Length; i++)
            {
                var c = pattern[i];
                if (c < 128)
                {
                    _ascii[c] |= 1UL << i;
                }
                else
                {
                    _other[c] = _other.GetValueOrDefault(c) | (1UL << i);
                }
            }
        }

        public int Distance(string text)
        {
            var mask = _length == 64 ? ulong.MaxValue : (1UL << _length) - 1;
            var pv = mask;
            var mv = 0UL;
            var score = _length;
            foreach (var c in text)
            {
                var eq = c < 128 ? _ascii[c] : _other.GetValueOrDefault(c);
                var xv = eq | mv;
                var xh = (((eq & pv) + pv) ^ pv) | eq;
                var ph = mv | ~(xh | pv);
                var mh = pv & xh;
                if ((ph & _last) != 0)
                {
                    score++;
                }
                else if ((mh & _last) != 0)
                {
                    score--;
                }

                ph = (ph << 1) | 1UL;
                mh <<= 1;
                pv = (mh | ~(xv | ph)) & mask;
                mv = ph & xv & mask;
            }

            return score;
        }
    }

    /// <summary>
    /// The Levenshtein distance of <paramref name="a"/> and <paramref name="b"/>, or any value above
    /// <paramref name="limit"/> as soon as every cell of a row exceeds it. Two reused rows.
    /// </summary>
    private static int BoundedDistance(string a, string b, int limit, ref int[] previous, ref int[] current)
    {
        var m = b.Length;
        if (previous.Length < m + 1)
        {
            previous = new int[m + 1];
            current = new int[m + 1];
        }

        for (var j = 0; j <= m; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            var ai = a[i - 1];
            for (var j = 1; j <= m; j++)
            {
                var cost = ai == b[j - 1] ? 0 : 1;
                var value = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                current[j] = value;
                if (value < rowMin)
                {
                    rowMin = value;
                }
            }

            if (rowMin > limit)
            {
                return limit + 1;
            }

            (previous, current) = (current, previous);
        }

        return previous[m];
    }
}
