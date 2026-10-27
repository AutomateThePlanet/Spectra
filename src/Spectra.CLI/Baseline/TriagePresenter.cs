using Spectra.Core.Baseline;

namespace Spectra.CLI.Baseline;

/// <summary>
/// Spec 084 (FR-022): the human summary of a triage run — per-suite bucket counts, the unbaselined line with the
/// recovery command, one line per group with its reasons, the <c>no_tests</c> hints and the notes. Printed in every
/// mode (stderr in JSON mode, stdout otherwise).
/// </summary>
public static class TriagePresenter
{
    public static void Write(TextWriter writer, TriageRun run, bool autoAck, bool dryRun, int autoAcked)
    {
        foreach (var suite in run.Suites)
        {
            writer.WriteLine($"Update triage — {suite.Suite} ({suite.TestCount} tests)");
            // Spec 086: RELINK is shown only when a test has it, so a workspace without renames reads as under 085.
            var counts = BucketRules.WorstFirst.Where(b => b != Bucket.Relink || suite.Tests.Any(t => t.Bucket == b)).Select(b =>
            {
                var n = suite.Tests.Count(t => t.Bucket == b);
                var suffix = b == Bucket.Cosmetic && n > 0 && autoAck ? (dryRun ? " (would be auto-acked)" : " (auto-acked)") : "";
                return $"{b.Wire()} {n}{suffix}";
            });
            writer.WriteLine("  " + string.Join(" · ", counts));

            if (suite.UnbaselinedIds.Count > 0 || suite.PartialIds.Count > 0)
            {
                writer.WriteLine($"  UNBASELINED {suite.UnbaselinedIds.Count} ({suite.PartialIds.Count} partial) — "
                    + $"run 'spectra ai source-baseline --init --suite {suite.Suite}'");
            }

            foreach (var group in run.Groups.Where(g => g.Suite == suite.Suite))
            {
                var refs = string.Join(", ", group.Items.Select(i => i.Key));
                var tests = group.Tests.Count == 1 ? "1 test " : $"{group.Tests.Count} tests";
                writer.WriteLine($"  {group.Id,-4} {group.Bucket.Wire(),-16} {tests,-9} {refs}   {string.Join("; ", group.Why)}");
            }
        }

        if (run.NoTests.Count > 0)
        {
            writer.WriteLine("  Not referenced by any test (changed docs): "
                + string.Join(", ", run.NoTests.Select(n => $"{n.Doc} › {n.Heading}")));
        }

        foreach (var note in run.Notes)
        {
            writer.WriteLine($"  note: {note}");
        }

        if (dryRun)
        {
            writer.WriteLine($"  Dry run: nothing written ({run.AutoAcks.Count} cosmetic ref(s) would be auto-acked).");
        }
        else if (autoAcked > 0)
        {
            writer.WriteLine($"  Auto-acknowledged {autoAcked} cosmetic ref(s).");
        }
    }
}
