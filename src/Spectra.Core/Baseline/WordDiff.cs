namespace Spectra.Core.Baseline;

/// <summary>A run of consecutive removed (<see cref="Added"/> = false) or added words.</summary>
public sealed record DiffRun(bool Added, IReadOnlyList<string> Words);

/// <summary>
/// The word diff of two normalised texts. <see cref="Computed"/> is false when either side has more words than the
/// cap; <see cref="Reason"/> then says why.
/// </summary>
public sealed record WordDiffResult(
    bool Computed,
    string? Reason,
    IReadOnlyList<DiffRun> Runs,
    IReadOnlyList<string> RemovedWords,
    IReadOnlyList<string> AddedWords)
{
    public int Removed => RemovedWords.Count;

    public int Added => AddedWords.Count;
}

/// <summary>
/// Spec 084 (FR-013, research R3): a deterministic word diff of two <see cref="Text.TextNormalizer"/> outputs.
/// Words are whitespace-separated tokens with leading and trailing punctuation and symbols trimmed. The common prefix
/// and suffix are trimmed first; the middle is diffed with a longest-common-subsequence table when it has at most
/// 4,000,000 cells (delete before insert on ties), otherwise the whole middle counts as removed and added — a
/// conservative fallback that can only make a change look bigger.
/// </summary>
public static class WordDiff
{
    public const int DefaultCap = 5000;

    private const long CellBudget = 4_000_000;

    public static IReadOnlyList<string> Tokenize(string normalised)
    {
        var tokens = new List<string>();
        foreach (var raw in normalised.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var start = 0;
            var end = raw.Length;
            while (start < end && IsTrimmed(raw[start])) start++;
            while (end > start && IsTrimmed(raw[end - 1])) end--;
            if (end > start)
            {
                tokens.Add(raw[start..end]);
            }
        }

        return tokens;
    }

    public static WordDiffResult Compute(string oldNormalised, string newNormalised, int cap = DefaultCap)
    {
        var a = Tokenize(oldNormalised);
        var b = Tokenize(newNormalised);
        if (a.Count > cap || b.Count > cap)
        {
            return new WordDiffResult(false, $"over {cap} words", [], [], []);
        }

        var prefix = 0;
        while (prefix < a.Count && prefix < b.Count && a[prefix] == b[prefix]) prefix++;
        var suffix = 0;
        while (suffix < a.Count - prefix && suffix < b.Count - prefix
               && a[a.Count - 1 - suffix] == b[b.Count - 1 - suffix]) suffix++;

        var oldMiddle = a.Skip(prefix).Take(a.Count - prefix - suffix).ToList();
        var newMiddle = b.Skip(prefix).Take(b.Count - prefix - suffix).ToList();

        var ops = (long)oldMiddle.Count * newMiddle.Count <= CellBudget
            ? Lcs(oldMiddle, newMiddle)
            : oldMiddle.Select(w => (false, w)).Concat(newMiddle.Select(w => (true, w))).ToList();

        var runs = new List<DiffRun>();
        var removed = new List<string>();
        var added = new List<string>();
        foreach (var (isAdded, word) in ops)
        {
            (isAdded ? added : removed).Add(word);
            if (runs.Count > 0 && runs[^1].Added == isAdded)
            {
                ((List<string>)runs[^1].Words).Add(word);
            }
            else
            {
                runs.Add(new DiffRun(isAdded, new List<string> { word }));
            }
        }

        return new WordDiffResult(true, null, runs, removed, added);
    }

    private static bool IsTrimmed(char c) => char.IsPunctuation(c) || char.IsSymbol(c);

    /// <summary>The removed/added words of an LCS diff; equal words are dropped.</summary>
    private static List<(bool Added, string Word)> Lcs(List<string> a, List<string> b)
    {
        var n = a.Count;
        var m = b.Count;
        var ops = new List<(bool, string)>();
        if (n == 0 || m == 0)
        {
            ops.AddRange(a.Select(w => (false, w)));
            ops.AddRange(b.Select(w => (true, w)));
            return ops;
        }

        // length[i, j] = LCS length of a[i..] and b[j..]; at most 5,000 words a side, so ushort is enough.
        var width = m + 1;
        var length = new ushort[(n + 1) * width];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                length[i * width + j] = a[i] == b[j]
                    ? (ushort)(length[(i + 1) * width + j + 1] + 1)
                    : Math.Max(length[(i + 1) * width + j], length[i * width + j + 1]);
            }
        }

        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y])
            {
                x++;
                y++;
            }
            else if (length[(x + 1) * width + y] >= length[x * width + y + 1])
            {
                ops.Add((false, a[x++]));
            }
            else
            {
                ops.Add((true, b[y++]));
            }
        }

        while (x < n) ops.Add((false, a[x++]));
        while (y < m) ops.Add((true, b[y++]));
        return ops;
    }
}
