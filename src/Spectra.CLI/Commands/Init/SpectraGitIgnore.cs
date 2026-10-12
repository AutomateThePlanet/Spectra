using Spectra.Core.Text;

namespace Spectra.CLI.Commands.Init;

/// <summary>
/// The workspace-local, never-committed paths SPECTRA adds to <c>.gitignore</c>. Spec 089 (F6): one list shared by
/// <c>spectra init</c> and <c>spectra update-skills</c>. Spec 090 (FR-D1–FR-D3): <c>update-skills</c> offers each
/// pattern only once (so a pattern the user removed stays removed), never creates the file, checks presence by whole
/// line, and appends without changing the file's existing bytes.
/// </summary>
public static class SpectraGitIgnore
{
    private const string Section = "# SPECTRA";
    private const string Updates = ".spectra/updates/";
    private const string Repairs = ".spectra/repairs/";
    private const string GenerationPlan = ".spectra/generation-plan/";

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
    ];

    /// <summary>
    /// Spec 090 (FR-D1): what a workspace from before 090 (no record in its skills manifest) counts as already offered —
    /// the 089 list without the two staging folders 089 introduced. Spec 094: and without every later entry, so a
    /// pre-090 workspace is offered those too.
    /// </summary>
    public static IReadOnlyList<string> Pre090Offered { get; } =
        Patterns.Where(p => p is not Updates and not Repairs and not GenerationPlan).ToList();

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
    /// <c>spectra init</c>: creates <c>.gitignore</c> with the full list when absent; otherwise appends the patterns that
    /// are neither present nor negated. Returns the patterns added.
    /// </summary>
    public static async Task<IReadOnlyList<string>> EnsureForInitAsync(string root, CancellationToken ct = default)
    {
        var path = Path.Combine(root, ".gitignore");
        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(path, Section + "\n" + string.Concat(Patterns.Select(p => p + "\n")), ct);
            return Patterns;
        }

        var lines = Lines(await File.ReadAllTextAsync(path, ct));
        var missing = Patterns.Where(p => !IsPresent(lines, p) && !IsNegated(lines, p)).ToList();
        await AppendAsync(path, missing, ct);
        return missing;
    }

    /// <summary>
    /// <c>spectra update-skills</c> (FR-D1): offers the patterns not yet offered to this workspace. With no
    /// <c>.gitignore</c> nothing is created or recorded (<c>MissingFile</c>). Otherwise a candidate already present or
    /// negated is recorded without being added, and the rest are appended. Returns what was added and the offered set
    /// to record.
    /// </summary>
    public static async Task<(IReadOnlyList<string> Added, IReadOnlyList<string> Candidates, List<string> Offered, bool MissingFile)>
        OfferNewAsync(string root, IReadOnlyList<string>? offered, CancellationToken ct = default)
    {
        var already = new HashSet<string>(offered ?? Pre090Offered, StringComparer.Ordinal);
        var candidates = Patterns.Where(p => !already.Contains(p)).ToList();
        var path = Path.Combine(root, ".gitignore");
        if (!File.Exists(path))
        {
            return ([], candidates, [.. already], true);
        }

        var lines = Lines(await File.ReadAllTextAsync(path, ct));
        var add = candidates.Where(p => !IsPresent(lines, p) && !IsNegated(lines, p)).ToList();
        await AppendAsync(path, add, ct);
        already.UnionWith(candidates);
        return (add, candidates, Patterns.Where(already.Contains).Concat(already.Where(p => !Patterns.Contains(p))).ToList(), false);
    }

    /// <summary>
    /// Spec 090 (FR-D3): appends without changing the existing bytes — one newline first only when the file doesn't end
    /// with one, the <c># SPECTRA</c> header only when absent, every line in the file's own newline style.
    /// </summary>
    private static async Task AppendAsync(string path, IReadOnlyList<string> patterns, CancellationToken ct)
    {
        if (patterns.Count == 0)
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

        if (!Lines(text).Any(l => l.Trim() == Section))
        {
            sb.Append(Section).Append(newline);
        }

        foreach (var pattern in patterns)
        {
            sb.Append(pattern).Append(newline);
        }

        await File.AppendAllTextAsync(path, sb.ToString(), ct);
    }

    private static string[] Lines(string text) => text.Replace("\r\n", "\n").Split('\n');
}
