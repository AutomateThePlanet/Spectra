using Spectra.Core.Criteria;

namespace Spectra.CLI.Criteria;

/// <summary>
/// Spec 079 FR-012a: the human-readable changeset summary. Callers write it to stderr in JSON mode (so stdout
/// stays exactly the JSON result) and to stdout in human mode. Never printed on failure.
/// </summary>
public static class CriteriaChangesetPresenter
{
    public static void Write(TextWriter writer, CriteriaChangeset changeset, string? heading = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(changeset);

        writer.WriteLine(heading ?? $"Criteria changeset for {changeset.Doc}");
        if (changeset.IsEmpty)
        {
            writer.WriteLine("  No changes.");
            return;
        }

        foreach (var a in changeset.Added)
            writer.WriteLine($"  + {a.Id}  {a.Text}");
        foreach (var m in changeset.Modified)
            writer.WriteLine($"  ~ {m.Id}  \"{m.OldText}\" → \"{m.NewText}\"");
        foreach (var r in changeset.Removed)
        {
            var refs = r.ReferencingTests > 0
                ? $"  (still referenced by {r.ReferencingTests} test{(r.ReferencingTests == 1 ? "" : "s")}: {string.Join(", ", r.TestIds)})"
                : "";
            writer.WriteLine($"  - {r.Id}  \"{r.OldText}\"{refs}");
        }

        foreach (var md in changeset.Metadata)
            writer.WriteLine($"  * {md.Id}  {md.Field}: {md.Old ?? "(none)"} → {md.New ?? "(none)"}");
    }

    public static void WriteDuplicates(TextWriter writer, IReadOnlyList<DuplicateCriterionId> duplicates)
    {
        foreach (var d in duplicates)
            writer.WriteLine($"Warning: criterion id {d.Id} is defined more than once ({string.Join(", ", d.Files)}); not renumbered.");
    }
}
