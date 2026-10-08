namespace Spectra.CLI.Commands.Init;

/// <summary>
/// The workspace-local, never-committed paths SPECTRA adds to <c>.gitignore</c>, and the idempotent append. Spec 089
/// (F6, FR-022a): one list shared by <c>spectra init</c> (new workspaces) and <c>spectra update-skills</c> (existing ones),
/// so the two can't drift.
/// </summary>
public static class SpectraGitIgnore
{
    private const string Section = "# SPECTRA";

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
        ".spectra/updates/",
        ".spectra/repairs/",
    ];

    /// <summary>
    /// Appends every missing pattern under <c># SPECTRA</c> (the header only when absent), keeping the existing content;
    /// creates the file when there is none. Returns the patterns it added (none when the file was already complete).
    /// </summary>
    public static async Task<IReadOnlyList<string>> EnsureAsync(string root, CancellationToken ct = default)
    {
        var path = Path.Combine(root, ".gitignore");
        if (!File.Exists(path))
        {
            var created = new System.Text.StringBuilder();
            created.AppendLine(Section);
            foreach (var pattern in Patterns)
            {
                created.AppendLine(pattern);
            }

            await File.WriteAllTextAsync(path, created.ToString(), ct);
            return Patterns;
        }

        var content = await File.ReadAllTextAsync(path, ct);
        var missing = Patterns.Where(p => !content.Contains(p)).ToList();
        if (missing.Count == 0)
        {
            return [];
        }

        var sb = new System.Text.StringBuilder(content.TrimEnd());
        sb.AppendLine();
        sb.AppendLine();
        if (!content.Contains(Section))
        {
            sb.AppendLine(Section);
        }

        foreach (var pattern in missing)
        {
            sb.AppendLine(pattern);
        }

        await File.WriteAllTextAsync(path, sb.ToString(), ct);
        return missing;
    }
}
