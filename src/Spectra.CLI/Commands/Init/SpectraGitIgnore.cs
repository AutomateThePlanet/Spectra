using Spectra.Core.Text;

namespace Spectra.CLI.Commands.Init;

/// <summary>
/// The workspace-local, never-committed paths SPECTRA adds to <c>.gitignore</c>. Spec 089 (F6): one list shared by
/// <c>spectra init</c> and <c>spectra update-skills</c>. Spec 090 (FR-D1–FR-D3): <c>update-skills</c> offers each
/// pattern only once (so a pattern the user removed stays removed), never creates the file, checks presence by whole
/// line, and appends without changing the file's existing bytes. Spec 096 (FR-009): what was offered is recorded in
/// <c>.gitignore</c> itself — <c># spectra-offered: p1, p2, …</c> lines — so it is per repository, not per clone.
/// </summary>
public static class SpectraGitIgnore
{
    private const string Section = "# SPECTRA";
    private const string Updates = ".spectra/updates/";
    private const string Repairs = ".spectra/repairs/";
    private const string GenerationPlan = ".spectra/generation-plan/";
    private const string Verdicts = ".spectra/verdicts/";
    private const string Dropped = ".spectra/dropped-tests.json";

    /// <summary>Spec 096 (FR-009): the prefix of an offered-marker line.</summary>
    public const string MarkerPrefix = "# spectra-offered:";

    public static IReadOnlyList<string> Patterns { get; } =
    [
        // Spec 040 lifecycle: PID/sentinel/lock/HWM are workspace-local derived state; never committed (FR-010, FR-020).
        ".execution/*.db",
        ".execution/*.db-*",
        ".spectra.lock",
        ".spectra/.pid",
        ".spectra/.cancel",
        ".spectra/id-allocator.lock",
        ".spectra/id-allocator.json",
        ".spectra/progress.json",
        ".spectra/analysis.json",
        ".spectra/generated.json",
        ".spectra/updated.json",
        ".spectra/criteria.json",
        ".spectra/critic-verdict.json",
        ".spectra/seam-progress.html",
        ".spectra/skills-manifest.json",
        // Spec 078: run-status files every long-running command writes (incl. dry runs).
        ".spectra-result.json",
        ".spectra-progress.html",
        // Spec 079: cross-process lock for criteria index/file writes.
        ".spectra/criteria.lock",
        // Spec 084: the baseline lock and the update-triage detail (scratch). The baselines
        // (test-cases/*/_source-baseline.json) and .spectra/source-snapshots/ are committed.
        ".spectra/baseline.lock",
        ".spectra/update-triage.json",
        // Spec 089: the staging folders of update edits and critic repairs (scratch, consumed by ingest-update --all).
        Updates,
        Repairs,
        // Spec 094: the per-suite generation plans (scratch, replaced by each analysis).
        GenerationPlan,
        // Spec 096 (FR-008): critic verdicts and fingerprint records (Spec 071/090), and the drop trail (Spec 071).
        Verdicts,
        Dropped,
    ];

    /// <summary>
    /// Spec 090 (FR-D1): what a workspace from before 090 (no record in its skills manifest) counts as already offered —
    /// the 089 list without the two staging folders 089 introduced. Spec 094: and without every later entry, so a
    /// pre-090 workspace is offered those too.
    /// </summary>
    public static IReadOnlyList<string> Pre090Offered { get; } =
        Patterns.Where(p => p is not Updates and not Repairs and not GenerationPlan and not Verdicts and not Dropped).ToList();

    /// <summary>Spec 096 (FR-009): every pattern named by a <c># spectra-offered:</c> line.</summary>
    public static IReadOnlyList<string> Marked(IEnumerable<string> lines) =>
        lines.Select(l => l.Trim())
            .Where(l => l.StartsWith(MarkerPrefix, StringComparison.Ordinal))
            .SelectMany(l => l[MarkerPrefix.Length..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Spec 090 (FR-D2): a whole trimmed, non-comment line equal to the pattern or to the pattern without its trailing
    /// <c>/</c>. A longer line that merely contains the pattern doesn't count.
    /// </summary>
    public static bool IsPresent(IEnumerable<string> lines, string pattern) =>
        lines.Select(l => l.Trim()).Where(l => !l.StartsWith('#')).Any(l => Matches(l, pattern));

    /// <summary>Spec 090 (FR-D2): a <c>!pattern</c> line — the user opted out; the pattern is never added.</summary>
    public static bool IsNegated(IEnumerable<string> lines, string pattern) =>
        lines.Select(l => l.Trim()).Any(l => l.StartsWith('!') && Matches(l[1..].Trim(), pattern));

    private static bool Matches(string line, string pattern) =>
        string.Equals(line, pattern, StringComparison.Ordinal)
        || string.Equals(line, pattern.TrimEnd('/'), StringComparison.Ordinal);

    /// <summary>
    /// <c>spectra init</c>: creates <c>.gitignore</c> with the full list and one marker line when absent; otherwise
    /// appends the patterns that are neither present nor negated, plus one marker line naming every pattern not yet
    /// marked (negated ones too). Returns the patterns added.
    /// </summary>
    public static async Task<IReadOnlyList<string>> EnsureForInitAsync(string root, CancellationToken ct = default)
    {
        var path = Path.Combine(root, ".gitignore");
        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(path,
                Section + "\n" + string.Concat(Patterns.Select(p => p + "\n")) + MarkerLine(Patterns) + "\n", ct);
            return Patterns;
        }

        var lines = Lines(await File.ReadAllTextAsync(path, ct));
        var marked = Marked(lines).ToHashSet(StringComparer.Ordinal);
        var missing = Patterns.Where(p => !IsPresent(lines, p) && !IsNegated(lines, p)).ToList();
        await AppendAsync(path, missing, Patterns.Where(p => !marked.Contains(p)).ToList(), ct);
        return missing;
    }

    /// <summary>
    /// <c>spectra update-skills</c> (FR-D1; Spec 096 FR-009): offers the patterns not yet offered to this repository.
    /// Offered = the marker lines ∪ the legacy per-clone record (<paramref name="legacyOffered"/>, migrated once) ∪
    /// <see cref="Pre090Offered"/> only when neither exists. With no <c>.gitignore</c> nothing is created
    /// (<c>MissingFile</c>). Otherwise a candidate already present or negated is not added, the rest are appended, and
    /// one marker line names every offered pattern not marked yet. Returns what was added and the candidates.
    /// </summary>
    public static async Task<(IReadOnlyList<string> Added, IReadOnlyList<string> Candidates, bool MissingFile)>
        OfferNewAsync(string root, IReadOnlyList<string>? legacyOffered, CancellationToken ct = default)
    {
        var path = Path.Combine(root, ".gitignore");
        var lines = File.Exists(path) ? Lines(await File.ReadAllTextAsync(path, ct)) : [];
        var marked = Marked(lines);
        var already = new HashSet<string>(marked, StringComparer.Ordinal);
        already.UnionWith(legacyOffered ?? []);
        if (marked.Count == 0 && legacyOffered is null)
        {
            already.UnionWith(Pre090Offered);
        }

        var candidates = Patterns.Where(p => !already.Contains(p)).ToList();
        if (!File.Exists(path))
        {
            return ([], candidates, true);
        }

        var add = candidates.Where(p => !IsPresent(lines, p) && !IsNegated(lines, p)).ToList();
        already.UnionWith(candidates);
        var markedSet = marked.ToHashSet(StringComparer.Ordinal);
        var toMark = Patterns.Where(already.Contains)
            .Concat(already.Where(p => !Patterns.Contains(p)).Order(StringComparer.Ordinal))
            .Where(p => !markedSet.Contains(p))
            .ToList();
        await AppendAsync(path, add, toMark, ct);
        return (add, candidates, false);
    }

    private static string MarkerLine(IEnumerable<string> patterns) => $"{MarkerPrefix} {string.Join(", ", patterns)}";

    /// <summary>
    /// Spec 090 (FR-D3): appends without changing the existing bytes — one newline first only when the file doesn't end
    /// with one, the <c># SPECTRA</c> header only when absent, every line in the file's own newline style. Spec 096: then
    /// one marker line for <paramref name="toMark"/>, when not empty.
    /// </summary>
    private static async Task AppendAsync(string path, IReadOnlyList<string> patterns, IReadOnlyList<string> toMark, CancellationToken ct)
    {
        if (patterns.Count == 0 && toMark.Count == 0)
        {
            return;
        }

        var text = await File.ReadAllTextAsync(path, ct);
        var newline = text.Contains('\n') ? Newlines.Detect(text) : "\n";
        var sb = new System.Text.StringBuilder();
        if (text.Length > 0 && !text.EndsWith('\n'))
        {
            sb.Append(newline);
        }

        if (patterns.Count > 0 && !Lines(text).Any(l => l.Trim() == Section))
        {
            sb.Append(Section).Append(newline);
        }

        foreach (var pattern in patterns)
        {
            sb.Append(pattern).Append(newline);
        }

        if (toMark.Count > 0)
        {
            sb.Append(MarkerLine(toMark)).Append(newline);
        }

        await File.AppendAllTextAsync(path, sb.ToString(), ct);
    }

    private static string[] Lines(string text) => text.Replace("\r\n", "\n").Split('\n');
}
